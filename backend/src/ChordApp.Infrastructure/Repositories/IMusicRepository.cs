using ChordApp.Domain;

namespace ChordApp.Infrastructure.Repositories;

/// <summary>Contrato de persistência usado pelos casos de uso da aplicação.</summary>
public interface IMusicRepository
{
    Task Save(Music music);
    Task<bool> ExistsAsync(Guid id);
    Task<IReadOnlyList<Music>> ListAsync(CancellationToken cancellationToken);
    Task<Music?> GetWithChordsAsync(Guid id, CancellationToken cancellationToken);
    Task<Music?> GetWithChordsForUpdateAsync(Guid id, CancellationToken cancellationToken);
    Task<ChordSegment?> GetChordAsync(Guid musicId, Guid chordId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
