namespace ChordApp.Domain;

public sealed class ChordSegment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MusicId { get; set; }
    public double StartTime { get; set; }
    public double EndTime { get; set; }
    public string Chord { get; set; } = "N";
    public double? Confidence { get; set; }
    public bool Corrected { get; set; }
}
