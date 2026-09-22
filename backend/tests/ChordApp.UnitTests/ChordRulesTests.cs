using ChordApp.Domain;

namespace ChordApp.UnitTests;

public class ChordRulesTests
{
    [Theory]
    [InlineData("C")]
    [InlineData("Am")]
    [InlineData("F#")]
    [InlineData("N")]
    public void AcceptsSupportedChord(string chord) => Assert.True(ChordRules.IsAllowed(chord));

    [Theory]
    [InlineData("C7")]
    [InlineData("H")]
    [InlineData("")]
    public void RejectsUnsupportedChord(string chord) => Assert.False(ChordRules.IsAllowed(chord));

    [Fact]
    public void RejectsSegmentPastSongDuration() => Assert.Throws<ArgumentException>(() => ChordRules.Validate(3, 11, 10, "C"));

    [Fact]
    public void RejectsReversedInterval() => Assert.Throws<ArgumentException>(() => ChordRules.Validate(4, 3, 10, "Am"));
}
