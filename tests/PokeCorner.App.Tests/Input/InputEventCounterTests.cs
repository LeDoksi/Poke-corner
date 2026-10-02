using PokeCorner.App.Input;

namespace PokeCorner.App.Tests.Input;

public class InputEventCounterTests
{
    [Fact]
    public void DrainUnits_NoEventsRecorded_ReturnsZero()
    {
        var counter = new InputEventCounter();

        Assert.Equal(0, counter.DrainUnits());
    }

    [Fact]
    public void RecordEvent_ThenDrain_ReturnsCountAndResets()
    {
        var counter = new InputEventCounter();
        counter.RecordEvent();
        counter.RecordEvent();
        counter.RecordEvent();

        var first = counter.DrainUnits();
        var second = counter.DrainUnits();

        Assert.Equal(3, first);
        Assert.Equal(0, second);
    }

    [Fact]
    public async Task RecordEvent_FromMultipleThreads_AllEventsCounted()
    {
        var counter = new InputEventCounter();
        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 1000; i++) counter.RecordEvent();
        }));

        await Task.WhenAll(tasks);

        Assert.Equal(8000, counter.DrainUnits());
    }

    [Fact]
    public void RecordEvent_UpdatesLastHookRecordedUtc()
    {
        var counter = new InputEventCounter();
        var before = DateTimeOffset.UtcNow;

        counter.RecordEvent();

        Assert.True(counter.LastHookRecordedUtc >= before);
    }

    [Fact]
    public void RecordFallbackUnit_IncrementsPendingUnits_ButNotLastHookRecordedUtc()
    {
        var counter = new InputEventCounter();
        counter.RecordEvent(); // establish a real hook timestamp
        var hookTimestamp = counter.LastHookRecordedUtc;

        counter.RecordFallbackUnit();
        counter.RecordFallbackUnit();

        Assert.Equal(3, counter.DrainUnits()); // 1 from RecordEvent above + the 2 fallback credits
        // Unchanged: the fallback must not look like a real hook event, or the staleness
        // check it exists to satisfy would immediately reset and stop re-firing.
        Assert.Equal(hookTimestamp, counter.LastHookRecordedUtc);
    }
}
