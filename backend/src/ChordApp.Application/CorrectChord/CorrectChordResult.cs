using ChordApp.Application.QueryMusic;

namespace ChordApp.Application.CorrectChord;

public sealed record CorrectChordResult(CorrectChordStatus Status, ChordDetails? Chord = null);
