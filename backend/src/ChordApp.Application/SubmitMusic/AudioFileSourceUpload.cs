using ChordApp.Api;
using ChordApp.Domain;
using ChordApp.Infrastructure.Repositories;


namespace ChordApp.Application.SubmitMusic
{
    public class AudioFileSourceUpload(IMusicRepository musicRepository) : IMusicSubmission<AudioFileSource>
    {
        public async Task<SubmissionResult?> SubmitAsync(AudioFileSource source, CancellationToken cancellationToken)
        {
            if (source.Length == 0 || source.Length > 30 * 1024 * 1024)
                return null;

            var extension = Path.GetExtension(source.FileName).ToLowerInvariant();

            if (extension is not (".mp3" or ".wav"))
                return null;

            var id = Guid.NewGuid();
            var path = TempFiles.PathFor(id, extension);

            try
            {
                await using (var output = File.Create(path))
                {
                    await source.Content.CopyToAsync(output, cancellationToken);
                }

                if (!await AudioValidation.HasSignatureAsync(path, extension, cancellationToken))
                    return null;


                var duration = await AudioValidation.DurationAsync(path, cancellationToken);

                if (duration is null || duration < 1 || duration > 900)
                    return null;


                var music = Music.Create(fileName: Path.GetFileName(source.FileName), durationSeconds: duration.Value);

                await musicRepository.Save(music);

                return new SubmissionResult(id);
            }
            finally
            {
                
                if (!await musicRepository.ExistsAsync(id))
                {
                    TempFiles.Delete(id);
                }
            }
        }
    }
}