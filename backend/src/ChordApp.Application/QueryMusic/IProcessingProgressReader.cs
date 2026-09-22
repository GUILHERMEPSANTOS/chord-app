namespace ChordApp.Application.QueryMusic;

public interface IProcessingProgressReader
{
    Task<ProcessingProgress?> ReadAsync(Guid musicId, CancellationToken cancellationToken);
}

public sealed record ProcessingProgress(int Percent, string Stage);
