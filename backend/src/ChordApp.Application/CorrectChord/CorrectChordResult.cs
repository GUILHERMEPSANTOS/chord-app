using ChordApp.Application.QueryMusic;

namespace ChordApp.Application.CorrectChord;

/// <summary>Estado e acorde resultante da tentativa de correção.</summary>
public sealed record CorrectChordResult(CorrectChordStatus Status, ChordDetails? Chord = null);
