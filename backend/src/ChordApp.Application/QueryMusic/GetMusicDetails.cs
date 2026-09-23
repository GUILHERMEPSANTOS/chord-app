using ChordApp.Domain;
using ChordApp.Infrastructure.Repositories;

namespace ChordApp.Application.QueryMusic;

/// <summary>Consulta uma música com acordes do modelo selecionado e progresso atual.</summary>
public sealed class GetMusicDetails(IMusicRepository repository, IProcessingProgressReader progressReader)
{
    public async Task<MusicDetails?> ExecuteAsync(Guid id, CancellationToken cancellationToken)
    {
        var music = await repository.GetWithChordsAsync(id, cancellationToken);
        if (music is null)
        {
            return null;
        }

        var percent = music.Status == AnalysisStatus.Completed ? 100 : 0;
        string? stage = null;

        if (music.Status == AnalysisStatus.Processing)
        {
            percent = 2;
            stage = "Preparando processamento";

            var progress = await progressReader.ReadAsync(id, cancellationToken);
            if (progress is not null)
            {
                percent = Math.Clamp(progress.Percent, 0, 100);
                stage = progress.Stage;
            }
        }

        var availableModels = music.Chords.Select(chord => chord.Model).Distinct().OrderBy(model => model).ToList();
        var selectedModel = availableModels.Contains(music.SelectedModel)
            ? music.SelectedModel
            : availableModels.FirstOrDefault() ?? music.SelectedModel;

        return new MusicDetails(
            music.Id,
            music.FileName,
            music.SourceUrl,
            music.DurationSeconds,
            music.Key,
            music.Status.ToString(),
            music.Error,
            percent,
            stage,
            selectedModel,
            availableModels,
            music.BtcError,
            music.Chords
                .Where(chord => chord.Model == selectedModel)
                .OrderBy(chord => chord.StartTime)
                .Select(chord => new ChordDetails(
                    chord.Id,
                    chord.StartTime,
                    chord.EndTime,
                    chord.Chord,
                    chord.Confidence,
                    chord.Corrected))
                .ToList());
    }
}
