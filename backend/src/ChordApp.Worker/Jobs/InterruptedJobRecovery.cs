using ChordApp.Domain;
using ChordApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ChordApp.Worker.Jobs;

/// <summary>Marca como falhas as análises interrompidas quando o processo Worker reinicia.</summary>
public sealed class InterruptedJobRecovery(MusicDb db)
{
    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        var interrupted = await db.Musics
            .Where(music => music.Status == AnalysisStatus.Processing)
            .ToListAsync(cancellationToken);

        foreach (var music in interrupted)
        {
            music.Status = AnalysisStatus.Failed;
            music.Error = "Processamento interrompido; envie o áudio novamente.";
            TempFiles.Delete(music.Id);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
