namespace ChordApp.Application.QueryMusic;

/// <summary>Resumo de uma música usado na listagem.</summary>
public sealed record MusicListItem(
    Guid Id,
    string FileName,
    double DurationSeconds,
    string? Key,
    string Status,
    DateTimeOffset CreatedAt);
