namespace ChordApp.Api.Contracts.Responses;

/// <summary>Segmento de acorde devolvido pela API ao navegador.</summary>
public sealed record ChordResponse(
    Guid Id, 
    double StartTime,
    double EndTime, 
    string Chord, 
    double? Confidence, 
    bool Corrected);
