using ChordApp.Domain;
using Microsoft.EntityFrameworkCore;

namespace ChordApp.Infrastructure.Repositories;

public sealed class MusicRepository(MusicDb musicDb) : IMusicRepository
{
    public async Task<IReadOnlyList<Music>> ListAsync(CancellationToken cancellationToken)
    {
        return await musicDb.Musics
            .AsNoTracking()
            .OrderByDescending(music => music.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<Music?> GetWithChordsAsync(Guid id, CancellationToken cancellationToken)
    {
        return musicDb.Musics
            .AsNoTracking()
            .Include(music => music.Chords)
            .FirstOrDefaultAsync(music => music.Id == id, cancellationToken);
    }

    public Task<Music?> GetWithChordsForUpdateAsync(Guid id, CancellationToken cancellationToken)
    {
        return musicDb.Musics.Include(music => music.Chords)
            .FirstOrDefaultAsync(music => music.Id == id, cancellationToken);
    }

    public Task<ChordSegment?> GetChordAsync(
        Guid musicId,
        Guid chordId,
        CancellationToken cancellationToken)
    {
        return musicDb.Chords.FirstOrDefaultAsync(
            chord => chord.Id == chordId && chord.MusicId == musicId,
            cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return musicDb.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid id)
    {
        return musicDb.Musics.AnyAsync(music => music.Id == id);
    }

    public async Task Save(Music music)
    {
        await musicDb.Musics.AddAsync(music);
        await musicDb.SaveChangesAsync();
    }
}
