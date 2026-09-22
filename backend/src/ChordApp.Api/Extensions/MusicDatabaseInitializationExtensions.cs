using ChordApp.Domain;
using ChordApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ChordApp.Api.Extensions;

public static class MusicDatabaseInitializationExtensions
{
    public static async Task InitializeMusicDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MusicDb>();

        await db.Database.EnsureCreatedAsync();

        var interrupted = await db.Musics
            .Where(m => m.Status == AnalysisStatus.Processing)
            .ToListAsync();

        foreach (var music in interrupted)
        {
            music.Status = AnalysisStatus.Failed;
            music.Error = "Processamento interrompido; envie o áudio novamente.";
            TempFiles.Delete(music.Id);
        }

        await db.SaveChangesAsync();
    }
}
