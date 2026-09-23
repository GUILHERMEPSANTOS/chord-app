namespace ChordApp.Application.QueryMusic;

/// <summary>Dados completos de uma música exibidos pela API.</summary>
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
    string SelectedModel,
    IReadOnlyList<string> AvailableModels,
    string? BtcError,
    IReadOnlyList<ChordDetails> Chords);

/// <summary>Dados de um segmento de acorde na resposta da aplicação.</summary>
public sealed record ChordDetails(
    Guid Id,
    double StartTime,
    double EndTime,
    string Chord,
    double? Confidence,
    bool Corrected);
