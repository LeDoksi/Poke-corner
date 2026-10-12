using PokeCorner.Core.Domain;

namespace PokeCorner.Core.Tests.Domain;

public class FeedingServiceTests
{
    private static PokemonIndividual MakeIndividual() => new()
    {
        SpeciesId = 1,
        EvolutionChainId = 1,
        ChainLength = 3,
        Nature = Nature.Hardy,
        IsShiny = false,
        CaughtAtUtc = DateTimeOffset.UtcNow
    };

    [Fact]
    public void Pet_AddsCarePointsAndUpdatesInteractionTimer()
    {
        var service = new FeedingService();
        var individual = MakeIndividual();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { LastInteractionUtc = now - TimeSpan.FromHours(1) };

        service.Pet(individual, state, now);

        Assert.Equal(FeedingService.PetCarePointsBonus, individual.AccumulatedCarePoints);
        Assert.Equal(now, state.LastInteractionUtc);
    }

    [Fact]
    public void Pet_WithinCooldown_ReturnsFalseAndDoesNotAddCarePoints()
    {
        var service = new FeedingService();
        var individual = MakeIndividual();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { LastPetUtc = now };

        var petted = service.Pet(individual, state, now + FeedingService.PetCooldown - TimeSpan.FromMilliseconds(1));

        Assert.False(petted);
        Assert.Equal(0, individual.AccumulatedCarePoints);
    }

    [Fact]
    public void Pet_AfterCooldownElapses_SucceedsAgain()
    {
        var service = new FeedingService();
        var individual = MakeIndividual();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { LastPetUtc = now };

        var petted = service.Pet(individual, state, now + FeedingService.PetCooldown);

        Assert.True(petted);
        Assert.Equal(FeedingService.PetCarePointsBonus, individual.AccumulatedCarePoints);
    }

    [Fact]
    public void Feed_WithoutTreats_FailsAndDoesNotAddCarePoints()
    {
        var service = new FeedingService();
        var individual = MakeIndividual();
        var inventory = new FeedingInventory { Treats = 0 };

        var fed = service.Feed(individual, inventory, new CareState(), DateTimeOffset.UtcNow);

        Assert.False(fed);
        Assert.Equal(0, individual.AccumulatedCarePoints);
    }

    [Fact]
    public void Feed_WithTreats_SpendsOneTreatAndAddsCarePoints()
    {
        var service = new FeedingService();
        var individual = MakeIndividual();
        var inventory = new FeedingInventory { Treats = 3 };

        var fed = service.Feed(individual, inventory, new CareState(), DateTimeOffset.UtcNow);

        Assert.True(fed);
        Assert.Equal(2, inventory.Treats);
        Assert.Equal(FeedingService.FeedCarePointsBonus, individual.AccumulatedCarePoints);
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

        // 5 calls of 800 = 4000 units total; crosses the 3750-unit threshold once, leaving
        // a 250-unit remainder — proves partial progress isn't truncated away each call.
        for (var i = 0; i < 5; i++)
        {
            service.EarnTreatsFromActivity(inventory, 800);
        }

        Assert.Equal(1, inventory.Treats);
        Assert.Equal(250, inventory.RemainderUnits);
    }
}
