namespace ChordApp.Worker.Contracts;

/// <summary>Um acorde detectado pelo Python, com início e fim em segundos.</summary>
public sealed record ProcessChord(double StartTime, double EndTime, string Chord, double? Confidence);
