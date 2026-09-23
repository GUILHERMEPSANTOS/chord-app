namespace ChordApp.Api.Middleware;

/// <summary>
/// Registra falhas inesperadas das requisições HTTP e devolve uma resposta segura ao cliente.
/// As falhas de tarefas em segundo plano são tratadas pelo AnalysisWorker.
/// </summary>
public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Erro inesperado em {Method} {Path}; traceId={TraceId}",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(
                new { error = "Erro interno inesperado.", traceId = context.TraceIdentifier });
        }
    }
}
