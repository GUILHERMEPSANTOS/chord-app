namespace ChordApp.Worker.Contracts;

/// <summary>Resposta da análise Python, com resultados independentes por modelo.</summary>
public sealed record ProcessResult(
    string? Key,
    double? DurationSeconds,
    List<ProcessChord> Chords,
    Dictionary<string, List<ProcessChord>>? Results,
    Dictionary<string, string>? ModelErrors);
