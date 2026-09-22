using ChordApp.Domain;
using ChordApp.Domain.Rules;
using ChordApp.Infrastructure.Repositories;

namespace ChordApp.Application.SubmitMusic
{
    public class MusicSubmissionYoutube(IMusicRepository musicRepository) : IMusicSubmission<YouTubeSource>
    {        
        public async Task<SubmissionResult> SubmitAsync(YouTubeSource source, CancellationToken cancellationToken)
        {
            var canonical = YouTubeUrls.Canonical(source.Url);

            if (canonical == null)
                  return default;

            var fileName = $"YouTube {canonical.Split('=')[1]}";

            var music = Music.Create(fileName, canonical);

            await musicRepository.Save(music);

            return new SubmissionResult(music.Id);
        }
    }
}