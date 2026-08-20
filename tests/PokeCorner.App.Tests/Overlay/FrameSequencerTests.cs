// tests/PokeCorner.App.Tests/Overlay/FrameSequencerTests.cs
using PokeCorner.App.Overlay;
using Xunit;

namespace PokeCorner.App.Tests.Overlay;

public class FrameSequencerTests
{
    [Fact]
    public void FrameIndexAt_Zero_ReturnsFirstFrame()
    {
        var sequencer = new FrameSequencer(frameCount: 4, frameDuration: TimeSpan.FromMilliseconds(200));

        Assert.Equal(0, sequencer.FrameIndexAt(TimeSpan.Zero));
    }

    [Fact]
    public void FrameIndexAt_MidwayThroughSecondFrame_ReturnsIndexOne()
    {
        var sequencer = new FrameSequencer(frameCount: 4, frameDuration: TimeSpan.FromMilliseconds(200));

        Assert.Equal(1, sequencer.FrameIndexAt(TimeSpan.FromMilliseconds(250)));
    }

    [Fact]
    public void FrameIndexAt_PastFullCycle_WrapsAround()
    {
        var sequencer = new FrameSequencer(frameCount: 4, frameDuration: TimeSpan.FromMilliseconds(200));

        // Full cycle is 800ms; 850ms should be equivalent to 50ms => frame 0.
        Assert.Equal(0, sequencer.FrameIndexAt(TimeSpan.FromMilliseconds(850)));
    }

    [Fact]
    public void Constructor_NonPositiveFrameCount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameSequencer(0, TimeSpan.FromMilliseconds(200)));
    }
}
