namespace ChordApp.Api.Contracts.Responses;

/// <summary>Música e acordes devolvidos pela API ao navegador.</summary>
public sealed record MusicResponse(
    Guid Id,
    string FileName,
    string? SourceUrl, 
    double DurationSeconds, 
    string? Key, 
    string Status, 
    string? Error, 
    int ProgressPercent, 
    string? ProgressStage, 
    List<ChordResponse> Chords
);

