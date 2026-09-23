using ChordApp.Worker.Jobs;

namespace ChordApp.Worker;

/// <summary>Busca jobs pendentes; cada análise é executada pelo AnalysisJobRunner.</summary>
public sealed class AnalysisWorker(
    IServiceScopeFactory scopes,
    ILogger<AnalysisWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // A API cria/atualiza o esquema ao iniciar. Aguarda se ela ainda não terminou.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RecoverInterruptedJobsAsync(stoppingToken);
                break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Banco ainda indisponível para recuperar jobs");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                bool processed;
                await using (var scope = scopes.CreateAsyncScope())
                {
                    var runner = scope.ServiceProvider.GetRequiredService<AnalysisJobRunner>();
                    processed = await runner.TryRunNextAsync(stoppingToken);
                }

                if (!processed)
                    await Task.Delay(TimeSpan.FromMilliseconds(1500), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falha ao buscar ou iniciar o próximo job");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task RecoverInterruptedJobsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var recovery = scope.ServiceProvider.GetRequiredService<InterruptedJobRecovery>();
        await recovery.RecoverAsync(cancellationToken);
    }
}
