using PokeCorner.Core.Activity;
using Xunit;

namespace PokeCorner.Core.Tests.Activity;

public class ActivityAccumulatorTests
{
    [Fact]
    public void AddUnits_AccumulatesTotal()
    {
        var accumulator = new ActivityAccumulator();

        accumulator.AddUnits(100);
        accumulator.AddUnits(50);

        Assert.Equal(150, accumulator.TotalUnits);
    }

    [Fact]
    public void AddUnits_BelowInterval_DoesNotRaiseCatchCheck()
    {
        var accumulator = new ActivityAccumulator();
        var raised = 0;
        accumulator.CatchCheckDue += () => raised++;

        accumulator.AddUnits(ActivityAccumulator.CatchCheckIntervalUnits - 1);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void AddUnits_CrossingInterval_RaisesCatchCheckOnce()
    {
        var accumulator = new ActivityAccumulator();
        var raised = 0;
        accumulator.CatchCheckDue += () => raised++;

        accumulator.AddUnits(ActivityAccumulator.CatchCheckIntervalUnits);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void AddUnits_CrossingIntervalMultipleTimesInOneCall_RaisesCatchCheckForEachInterval()
    {
        var accumulator = new ActivityAccumulator();
        var raised = 0;
        accumulator.CatchCheckDue += () => raised++;

        accumulator.AddUnits(ActivityAccumulator.CatchCheckIntervalUnits * 3);

        Assert.Equal(3, raised);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void AddUnits_NonPositiveInput_IsIgnored(long units)
    {
        var accumulator = new ActivityAccumulator();
        accumulator.AddUnits(100);

        accumulator.AddUnits(units);

        Assert.Equal(100, accumulator.TotalUnits);
    }
}
