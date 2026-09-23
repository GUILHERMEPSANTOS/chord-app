namespace ChordApp.Api.Contracts.Processor; 
public sealed record ProcessResult(
    string? Key,
    double? DurationSeconds,
    List<ProcessChord> Chords,
    Dictionary<string, List<ProcessChord>>? Results,
    Dictionary<string, string>? ModelErrors);
