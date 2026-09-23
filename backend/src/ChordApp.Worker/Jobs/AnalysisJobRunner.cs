using ChordApp.Domain;
using ChordApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ChordApp.Worker.Jobs;

/// <summary>Reserva uma música pendente, processa e salva resultado ou falha; remove o áudio temporário.</summary>
public sealed class AnalysisJobRunner(
    MusicDb db,
    IProcessorClient processor,
    AnalysisResultMapper mapper,
    ILogger<AnalysisJobRunner> logger)
{
    public async Task<bool> TryRunNextAsync(CancellationToken cancellationToken)
    {
        var music = await db.Musics
            .FirstOrDefaultAsync(item => item.Status == AnalysisStatus.Pending, cancellationToken);
        if (music is null) return false;

        var path = TempFiles.Find(music.Id);
        if (path is null && music.SourceUrl is null)
        {
            music.Status = AnalysisStatus.Failed;
            music.Error = "Arquivo temporário indisponível.";
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        music.Status = AnalysisStatus.Processing;
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var result = await processor.AnalyzeAsync(music, path, cancellationToken);
            mapper.Apply(db, music, result);
        }
        catch (ProcessorRejectedException exception)
        {
            music.Status = AnalysisStatus.Failed;
            music.Error = exception.Message;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao analisar música {MusicId}", music.Id);
            music.Status = AnalysisStatus.Failed;
            music.Error = "Falha no processamento. Consulte os logs.";
        }
        finally
        {
            try
            {
                await db.SaveChangesAsync(CancellationToken.None);
            }
            finally
            {
                TempFiles.Delete(music.Id);
            }
        }

        return true;
    }
}
