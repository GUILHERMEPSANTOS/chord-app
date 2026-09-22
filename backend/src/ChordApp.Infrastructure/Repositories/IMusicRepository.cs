using ChordApp.Domain;

namespace ChordApp.Infrastructure.Repositories
{
    public interface IMusicRepository
    {
        Task Save(Music music);
        Task<bool> ExistsAsync(Guid guid);
    }
}
