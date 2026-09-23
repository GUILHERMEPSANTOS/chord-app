using ChordApp.Domain;
using ChordApp.Infrastructure.Repositories;

namespace ChordApp.Application.QueryMusic;

public enum SelectMusicModelStatus { Success, NotFound, Unavailable }

public sealed class SelectMusicModel(IMusicRepository repository)
{
    public async Task<SelectMusicModelStatus> ExecuteAsync(Guid musicId, string model, CancellationToken cancellationToken)
    {
        if (!RecognitionModels.IsAllowed(model))
        {
            return SelectMusicModelStatus.Unavailable;
        }

        var music = await repository.GetWithChordsForUpdateAsync(musicId, cancellationToken);
        if (music is null)
        {
            return SelectMusicModelStatus.NotFound;
        }

        if (music.Status != AnalysisStatus.Completed || !music.Chords.Any(chord => chord.Model == model))
        {
            return SelectMusicModelStatus.Unavailable;
        }

        music.SelectedModel = model;
        await repository.SaveChangesAsync(cancellationToken);
        return SelectMusicModelStatus.Success;
    }
}
