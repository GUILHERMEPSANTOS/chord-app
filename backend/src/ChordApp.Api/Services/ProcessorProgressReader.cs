using System.Net.Http.Json;
using ChordApp.Api.Contracts.Processor;
using ChordApp.Application.QueryMusic;

namespace ChordApp.Api.Services;

/// <summary>Consulta a rota de progresso do processador Python para a API.</summary>
public sealed class ProcessorProgressReader(IHttpClientFactory clients) : IProcessingProgressReader
{
    public async Task<ProcessingProgress?> ReadAsync(
        Guid musicId,
        CancellationToken cancellationToken
    )
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));

        var snapshot = await clients
            .CreateClient("processor")
            .GetFromJsonAsync<ProgressSnapshot>($"/progress/{musicId}", timeout.Token);

        return snapshot is null ? null : new ProcessingProgress(snapshot.Percent, snapshot.Stage);
    }
}
