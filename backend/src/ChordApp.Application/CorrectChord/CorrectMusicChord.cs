using ChordApp.Application.QueryMusic;
using ChordApp.Domain.Rules;
using ChordApp.Infrastructure.Repositories;

namespace ChordApp.Application.CorrectChord;

/// <summary>Valida e persiste a correção manual de um acorde.</summary>
public sealed class CorrectMusicChord(IMusicRepository repository) : ICorrectMusicChord
{
    public async Task<CorrectChordResult> ExecuteAsync(
        Guid musicId,
        Guid chordId,
        string chordName,
        CancellationToken cancellationToken)
    {
        var chord = await repository.GetChordAsync(musicId, chordId, cancellationToken);
        if (chord is null)
        {
            return new CorrectChordResult(CorrectChordStatus.NotFound);
        }

        if (!ChordRules.IsAllowed(chordName))
        {
            return new CorrectChordResult(CorrectChordStatus.InvalidChord);
        }

        chord.Chord = chordName;
        chord.Corrected = true;
        chord.Confidence = null;
        await repository.SaveChangesAsync(cancellationToken);

        return new CorrectChordResult(
            CorrectChordStatus.Success,
            new ChordDetails(
                chord.Id,
                chord.StartTime,
                chord.EndTime,
                chord.Chord,
                chord.Confidence,
                chord.Corrected));
    }
}
