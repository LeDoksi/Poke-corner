using PokeCorner.Core.Audio;

namespace PokeCorner.Core.Tests.Audio;

/// The stored setting is a 0-100 percent; MediaPlayer.Volume is a 0.0-1.0 fraction. These pin
/// the conversion, and especially its two ends: 0 must be exact silence rather than a very
/// small positive volume, and a value from outside the slider's range (a hand-edited
/// state.json) must clamp instead of reaching the player.
public class CryVolumeTests
{
    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(1, 0.01)]
    [InlineData(50, 0.5)]
    [InlineData(70, 0.7)]
    [InlineData(100, 1.0)]
    public void ToPlayerVolume_ConvertsPercentToTheZeroToOneRange(int percent, double expected) =>
        Assert.Equal(expected, CryVolume.ToPlayerVolume(percent), precision: 10);

    [Theory]
    [InlineData(-1)]
    [InlineData(-1000)]
    [InlineData(int.MinValue)]
    public void ToPlayerVolume_BelowRange_ClampsToSilence(int percent) =>
        Assert.Equal(0.0, CryVolume.ToPlayerVolume(percent));

    [Theory]
    [InlineData(101)]
    [InlineData(900)]
    [InlineData(int.MaxValue)]
    public void ToPlayerVolume_AboveRange_ClampsToFullVolume(int percent) =>
        Assert.Equal(1.0, CryVolume.ToPlayerVolume(percent));

    /// Zero is the one value a slider user can land on that must produce NO sound at all, so
    /// it gets its own assertion rather than riding on the theory above: anything above 0.0,
    /// however small, is still audible on a quiet desktop.
    [Fact]
    public void ToPlayerVolume_Zero_IsExactlySilentNotMerelyQuiet()
    {
        Assert.Equal(0.0, CryVolume.ToPlayerVolume(0));
        Assert.False(CryVolume.ToPlayerVolume(0) > 0.0);
    }
}
