using PokeCorner.App.Input;
using Xunit;

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
    public void RecordEvent_FromMultipleThreads_AllEventsCounted()
    {
        var counter = new InputEventCounter();
        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 1000; i++) counter.RecordEvent();
        }));

        Task.WaitAll(tasks.ToArray());

        Assert.Equal(8000, counter.DrainUnits());
    }
}
