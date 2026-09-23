using ChordApp.Domain;
using ChordApp.Infrastructure.Repositories;

namespace ChordApp.Application.QueryMusic;

/// <summary>Resultado possível da tentativa de seleção de modelo.</summary>
public enum SelectMusicModelStatus { Success, NotFound, Unavailable }

/// <summary>Alterna o modelo exibido quando há acordes salvos para ele.</summary>
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
