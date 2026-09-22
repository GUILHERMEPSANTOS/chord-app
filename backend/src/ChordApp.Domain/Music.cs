namespace ChordApp.Domain;

public enum AnalysisStatus { Pending, Processing, Completed, Failed }

public sealed class Music
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = "";
    public string? SourceUrl { get; set; }
    public double DurationSeconds { get; set; }
    public string? Key { get; set; }
    public AnalysisStatus Status { get; set; } = AnalysisStatus.Pending;
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ChordSegment> Chords { get; set; } = [];
}

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

public static class ChordRules
{
    private static readonly HashSet<string> Allowed =
        Enumerable.Range(0, 12).SelectMany(i => new[] { Note(i), Note(i) + "m", Note(i) + "7", Note(i) + "maj7", Note(i) + "m7", Note(i) + "dim", Note(i) + "dim7", Note(i) + "m7b5" }).Append("N").ToHashSet();

    public static bool IsAllowed(string chord) => Allowed.Contains(chord);
    public static void Validate(double start, double end, double duration, string chord)
    {
        if (!double.IsFinite(start) || !double.IsFinite(end) || start < 0 || end <= start || end > duration + 0.01 || !IsAllowed(chord))
            throw new ArgumentException("Segmento ou acorde inválido.");
    }

    public static string Note(int pitchClass) => new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" }[pitchClass];
}
