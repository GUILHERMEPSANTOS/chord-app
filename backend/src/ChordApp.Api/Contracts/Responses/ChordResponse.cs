namespace ChordApp.Api.Contracts.Responses;

public sealed record ChordResponse(
    Guid Id, 
    double StartTime,
    double EndTime, 
    string Chord, 
    double? Confidence, 
    bool Corrected);
