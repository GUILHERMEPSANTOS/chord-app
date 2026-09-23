using ChordApp.Domain;
using ChordApp.Infrastructure;
using ChordApp.Infrastructure.Repositories;


namespace ChordApp.Application.SubmitMusic
{
    /// <summary>Valida e guarda temporariamente um MP3/WAV antes de criar o job.</summary>
    public class AudioFileSourceUpload(IMusicRepository musicRepository) : IMusicSubmission<AudioFileSource>
    {
        public async Task<SubmissionResult?> SubmitAsync(AudioFileSource source, CancellationToken cancellationToken)
        {
            if (!RecognitionModels.IsAllowed(source.Model))
                return null;

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
                music.Id = id;
                music.SelectedModel = source.Model;

                await musicRepository.Save(music);

                return new SubmissionResult(music.Id);
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
