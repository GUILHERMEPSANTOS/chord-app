namespace ChordApp.Domain.Rules;

/// <summary>Vocabulário aceito e validação de tempos e nomes de acordes.</summary>
public static class ChordRules
{
    private static readonly HashSet<string> Allowed =
        [
            .. Enumerable
                .Range(0, 12)
                .SelectMany(noteIndex => new[] {
                    Note(noteIndex),
                    Note(noteIndex) + "m",
                    Note(noteIndex) + "7",
                    Note(noteIndex) + "maj7",
                    Note(noteIndex) + "m7",
                    Note(noteIndex) + "dim",
                    Note(noteIndex) + "dim7",
                    Note(noteIndex) + "m7b5" }),
            "N",
        ];

    public static bool IsAllowed(string chord) => Allowed.Contains(chord);
    public static void Validate(double start, double end, double duration, string chord)
    {
        if (!double.IsFinite(start) || !double.IsFinite(end) || start < 0 || end <= start || end > duration + 0.01 || !IsAllowed(chord))
            throw new ArgumentException("Segmento ou acorde inválido.");
    }

    public static string Note(int pitchClass)
        => new[] {
        "C",
        "C#",
        "D",
        "D#",
        "E",
        "F",
        "F#",
        "G",
        "G#",
        "A",
        "A#",
        "B"
    }[pitchClass];
}
