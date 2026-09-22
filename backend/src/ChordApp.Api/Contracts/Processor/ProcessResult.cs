namespace ChordApp.Api.Contracts.Processor; 
public sealed record ProcessResult(string? Key, double? DurationSeconds, List<ProcessChord> Chords);
