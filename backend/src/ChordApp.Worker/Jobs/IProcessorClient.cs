using ChordApp.Domain;
using ChordApp.Worker.Contracts;

namespace ChordApp.Worker.Jobs;

/// <summary>Porta de comunicação com o detector Python, independente do transporte HTTP.</summary>
public interface IProcessorClient
{
    Task<ProcessResult> AnalyzeAsync(Music music, string? audioPath, CancellationToken cancellationToken);
}
