namespace ChordApp.Api.Contracts.Processor;

public sealed record ProcessChord(double StartTime, double EndTime, string Chord, double? Confidence);
