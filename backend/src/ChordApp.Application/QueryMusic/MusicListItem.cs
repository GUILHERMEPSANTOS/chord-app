namespace ChordApp.Application.QueryMusic;

public sealed record MusicListItem(
    Guid Id,
    string FileName,
    double DurationSeconds,
    string? Key,
    string Status,
    DateTimeOffset CreatedAt);
