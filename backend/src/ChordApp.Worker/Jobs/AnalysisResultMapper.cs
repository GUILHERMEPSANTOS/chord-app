using ChordApp.Domain;
using ChordApp.Domain.Rules;
using ChordApp.Infrastructure;
using ChordApp.Worker.Contracts;

namespace ChordApp.Worker.Jobs;

/// <summary>Valida os segmentos devolvidos pelo Python e aplica resultados por modelo à música.</summary>
public sealed class AnalysisResultMapper
{
    public void Apply(MusicDb db, Music music, ProcessResult result)
    {
        if (result.DurationSeconds is double measured) music.DurationSeconds = measured;
        var modelResults = result.Results ?? new Dictionary<string, List<ProcessChord>>
        {
            [RecognitionModels.LvChordia] = result.Chords
        };

        var segments = new List<ChordSegment>();
        foreach (var (model, chords) in modelResults)
        {
            if (!RecognitionModels.IsAllowed(model))
                throw new InvalidDataException("Modelo desconhecido no resultado.");

            foreach (var chord in chords)
            {
                if (!double.IsFinite(chord.StartTime) || !double.IsFinite(chord.EndTime))
                    throw new InvalidDataException("Tempo inválido no resultado.");

                var start = Math.Max(0, chord.StartTime);
                var end = Math.Min(music.DurationSeconds, chord.EndTime);
                if (end <= start) continue;

                ChordRules.Validate(start, end, music.DurationSeconds, chord.Chord);
                segments.Add(new ChordSegment
                {
                    MusicId = music.Id,
                    Model = model,
                    StartTime = start,
                    EndTime = end,
                    Chord = chord.Chord,
                    Confidence = chord.Confidence
                });
            }
        }

        db.Chords.AddRange(segments);
        if (!segments.Any(segment => segment.Model == music.SelectedModel))
            music.SelectedModel = RecognitionModels.LvChordia;

        music.BtcError = result.ModelErrors?.GetValueOrDefault(RecognitionModels.BtcIsmir19) is not null
            ? "BTC-ISMIR19 não concluiu a análise deste áudio. Consulte os logs do processador."
            : null;
        music.Key = result.Key;
        music.Status = AnalysisStatus.Completed;
    }
}
