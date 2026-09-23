namespace ChordApp.Application.CorrectChord;

public interface ICorrectMusicChord
{
    Task<CorrectChordResult> ExecuteAsync(Guid musicId, Guid chordId, string chordName, CancellationToken cancellationToken);
}
