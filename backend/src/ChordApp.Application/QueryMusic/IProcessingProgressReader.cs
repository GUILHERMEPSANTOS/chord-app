namespace ChordApp.Application.QueryMusic;

/// <summary>Porta de consulta do progresso mantido pelo processador.</summary>
public interface IProcessingProgressReader
{
    Task<ProcessingProgress?> ReadAsync(Guid musicId, CancellationToken cancellationToken);
}

/// <summary>Porcentagem e etapa atuais de uma análise.</summary>
public sealed record ProcessingProgress(int Percent, string Stage);
