using ChordApp.Domain;
using ChordApp.Domain.Rules;
using ChordApp.Infrastructure.Repositories;

namespace ChordApp.Application.SubmitMusic
{
    /// <summary>Valida a URL e cria uma análise pendente sem baixar áudio na API.</summary>
    public class MusicSubmissionYoutube(IMusicRepository musicRepository) : IMusicSubmission<YouTubeSource>
    {        
        public async Task<SubmissionResult?> SubmitAsync(YouTubeSource source, CancellationToken cancellationToken)
        {
            if (!RecognitionModels.IsAllowed(source.Model))
                return null;

            var canonical = YouTubeUrls.Canonical(source.Url);

            if (canonical == null)
                  return default;

            var fileName = $"YouTube {canonical.Split('=')[1]}";

            var music = Music.Create(fileName, canonical);
            music.SelectedModel = source.Model;

            await musicRepository.Save(music);

            return new SubmissionResult(music.Id);
        }
    }
}
