using ChordApp.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ChordApp.Api.Extensions;

/// <summary>Prepara o esquema do banco antes de a API aceitar requisições.</summary>
public static class MusicDatabaseInitializationExtensions
{
    public static async Task InitializeMusicDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MusicDb>();

        await db.Database.EnsureCreatedAsync();

        if (db.Database.IsNpgsql())
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Musics\" ADD COLUMN IF NOT EXISTS \"SelectedModel\" text NOT NULL DEFAULT 'lv-chordia'");
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Musics\" ADD COLUMN IF NOT EXISTS \"BtcError\" text NULL");
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Chords\" ADD COLUMN IF NOT EXISTS \"Model\" text NOT NULL DEFAULT 'lv-chordia'");
            await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS \"IX_Chords_MusicId_Model_StartTime\" ON \"Chords\" (\"MusicId\", \"Model\", \"StartTime\")");
        }
    }
}
