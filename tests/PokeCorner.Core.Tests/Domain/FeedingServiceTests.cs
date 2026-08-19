using PokeCorner.Core.Domain;
using Xunit;

namespace PokeCorner.Core.Tests.Domain;

public class FeedingServiceTests
{
    [Fact]
    public void TryFeed_SimpleMode_AlwaysSucceedsAndDoesNotTouchInventory()
    {
        var service = new FeedingService();
        var inventory = new FeedingInventory { Treats = 0 };
        var state = new CareState { LastInteractionUtc = DateTimeOffset.UtcNow - TimeSpan.FromHours(1) };
        var now = DateTimeOffset.UtcNow;

        var fed = service.TryFeed(FeedingMode.Simple, inventory, state, now);

        Assert.True(fed);
        Assert.Equal(0, inventory.Treats);
        Assert.Equal(now, state.LastInteractionUtc);
    }

    [Fact]
    public void TryFeed_EconomyMode_WithoutTreats_Fails()
    {
        var service = new FeedingService();
        var inventory = new FeedingInventory { Treats = 0 };
        var state = new CareState();

        var fed = service.TryFeed(FeedingMode.Economy, inventory, state, DateTimeOffset.UtcNow);

        Assert.False(fed);
    }

    [Fact]
    public void TryFeed_EconomyMode_WithTreats_SpendsOneTreat()
    {
        var service = new FeedingService();
        var inventory = new FeedingInventory { Treats = 3 };
        var state = new CareState();

        var fed = service.TryFeed(FeedingMode.Economy, inventory, state, DateTimeOffset.UtcNow);

        Assert.True(fed);
        Assert.Equal(2, inventory.Treats);
    }

    [Fact]
    public void TryFeed_ClearsRunAwayCooldown()
    {
        var service = new FeedingService();
        var inventory = new FeedingInventory();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { RanAwayUntilUtc = now + TimeSpan.FromHours(1) };

        service.TryFeed(FeedingMode.Simple, inventory, state, now);

        Assert.Null(state.RanAwayUntilUtc);
    }

    [Fact]
    public void EarnTreatsFromActivity_ConvertsUnitsAtFixedRate()
    {
        var service = new FeedingService();
        var inventory = new FeedingInventory();

        service.EarnTreatsFromActivity(inventory, FeedingService.ActivityUnitsPerTreatEarned * 3 + 100);

        Assert.Equal(3, inventory.Treats);
    }

    [Fact]
    public void EarnTreatsFromActivity_CarriesRemainderAcrossCalls()
    {
        var service = new FeedingService();
        var inventory = new FeedingInventory();

        // ActivityUnitsPerTreatEarned = 500; five calls of 100 units should combine to 500 = 1 treat,
        // not truncate to 0 on each individual call.
        for (var i = 0; i < 5; i++)
        {
            service.EarnTreatsFromActivity(inventory, 100);
        }

        Assert.Equal(1, inventory.Treats);
    }
}
