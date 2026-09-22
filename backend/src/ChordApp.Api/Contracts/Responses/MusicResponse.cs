namespace ChordApp.Api.Contracts.Responses;

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

