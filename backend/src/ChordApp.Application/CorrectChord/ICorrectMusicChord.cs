namespace ChordApp.Application.CorrectChord;

/// <summary>Contrato para corrigir manualmente um acorde salvo.</summary>
public interface ICorrectMusicChord
{
    Task<CorrectChordResult> ExecuteAsync(Guid musicId, Guid chordId, string chordName, CancellationToken cancellationToken);
}
