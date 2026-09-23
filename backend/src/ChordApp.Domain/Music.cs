namespace ChordApp.Domain;

/// <summary>Música analisada, com origem, estado, tom, modelo selecionado e acordes.</summary>
public sealed class Music
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = "";
    public string? SourceUrl { get; set; }
    public double DurationSeconds { get; set; }
    public string? Key { get; set; }
    public AnalysisStatus Status { get; set; } = AnalysisStatus.Pending;
    public string? Error { get; set; }
    public string SelectedModel { get; set; } = RecognitionModels.LvChordia;
    public string? BtcError { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ChordSegment> Chords { get; set; } = [];

    public Music() { }

    private Music(
        Guid id,
        string fileName,
        string? sourceUrl = null,
        double? durationSeconds = null)
    {

        Id = id;
        FileName = fileName;
        SourceUrl = sourceUrl;
        if (durationSeconds.HasValue)
        {
            DurationSeconds = durationSeconds.Value;
        }
    }

    public static Music Create(
        string fileName,
        string? sourceUrl = null,
        double? durationSeconds = null)
    {
        return new Music(
            Guid.NewGuid(),
            fileName,
            sourceUrl,
            durationSeconds
        );
    }
}
