using ChordApp.Infrastructure.Repositories;

namespace ChordApp.Application.QueryMusic;

public sealed class ListMusics(IMusicRepository repository)
{
    public async Task<IReadOnlyList<MusicListItem>> ExecuteAsync(CancellationToken cancellationToken)
    {
        var musics = await repository.ListAsync(cancellationToken);

        return musics.Select(music => new MusicListItem(
            music.Id,
            music.FileName,
            music.DurationSeconds,
            music.Key,
            music.Status.ToString(),
            music.CreatedAt)).ToList();
    }
}
