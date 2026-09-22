namespace ChordApp.Application.QueryMusic;

public sealed record MusicDetails(
    Guid Id,
    string FileName,
    string? SourceUrl,
    double DurationSeconds,
    string? Key,
    string Status,
    string? Error,
    int ProgressPercent,
    string? ProgressStage,
    IReadOnlyList<ChordDetails> Chords);

public sealed record ChordDetails(
    Guid Id,
    double StartTime,
    double EndTime,
    string Chord,
    double? Confidence,
    bool Corrected);
