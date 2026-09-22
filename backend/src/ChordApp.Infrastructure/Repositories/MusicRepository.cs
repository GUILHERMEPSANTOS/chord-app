using ChordApp.Domain;
using Microsoft.EntityFrameworkCore;

namespace ChordApp.Infrastructure.Repositories
{
    public class MusicRepository(MusicDb musicDb) : IMusicRepository
    {
        public async Task<bool> ExistsAsync(Guid guid)
        {
            return await musicDb.Musics.AnyAsync(music => music.Id == guid);
        }

        public async Task Save(Music music)
        {
            await musicDb.Musics.AddAsync(music);

            await musicDb.SaveChangesAsync();
        }
    }
}
