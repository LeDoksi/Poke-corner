using PokeCorner.Core;
using PokeCorner.Core.Activity;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using Xunit;

namespace PokeCorner.Core.Tests;

public class AppStateTests
{
    private static PokemonIndividual MakeIndividual(int chainLength = 3) => new()
    {
        SpeciesId = 1,
        EvolutionChainId = 1,
        ChainLength = chainLength,
        Nature = Nature.Hardy,
        IsShiny = false,
        CaughtAtUtc = DateTimeOffset.UtcNow
    };

    [Fact]
    public void ApplyActivityUnits_GrowsActiveIndividualCarePoints()
    {
        var state = new AppState();
        var individual = MakeIndividual();
        state.Collection.Add(individual);

        state.ApplyActivityUnits(1234, DateTimeOffset.UtcNow);

        Assert.Equal(1234, individual.AccumulatedCarePoints);
    }

    [Fact]
    public void ApplyActivityUnits_PastStage2Threshold_EvolvesActiveIndividual()
    {
        var state = new AppState();
        var individual = MakeIndividual();
        state.Collection.Add(individual);

        state.ApplyActivityUnits(EvolutionThresholds.Stage2Threshold, DateTimeOffset.UtcNow);

        Assert.Equal(EvolutionStage.Stage2, individual.CurrentStage);
    }

    [Fact]
    public void ApplyActivityUnits_RunningTotalNearIntMax_ClampsInsteadOfOverflowing()
    {
        var state = new AppState();
        var individual = MakeIndividual();
        individual.AccumulatedCarePoints = int.MaxValue - 10;
        state.Collection.Add(individual);

        state.ApplyActivityUnits(1000, DateTimeOffset.UtcNow);

        Assert.Equal(int.MaxValue, individual.AccumulatedCarePoints);
    }

    [Fact]
    public void ApplyActivityUnits_NoActiveIndividual_DoesNotThrow()
    {
        var state = new AppState();

        var exception = Record.Exception(() => state.ApplyActivityUnits(500, DateTimeOffset.UtcNow));

        Assert.Null(exception);
    }

    [Fact]
    public void ApplyActivityUnits_CrossingCatchInterval_RaisesCatchRollDue()
    {
        var state = new AppState();
        state.Collection.Add(MakeIndividual());
        var raised = 0;
        state.CatchRollDue += () => raised++;

        state.ApplyActivityUnits(ActivityAccumulator.CatchCheckIntervalUnits, DateTimeOffset.UtcNow);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void Hydrate_RoundTripsCareStateByIndividualId()
    {
        var state = new AppState();
        var individual = MakeIndividual();
        var careState = new CareState
        {
            LastInteractionUtc = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(30),
            RanAwayUntilUtc = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(15)
        };
        var data = new SaveData
        {
            Individuals = new List<PokemonIndividual> { individual },
            ActiveIndividualId = individual.Id,
            Settings = new AppSettings(),
            FeedingTreats = 0,
            CareStates = new Dictionary<Guid, CareState> { [individual.Id] = careState }
        };

        state.Hydrate(data);

        Assert.True(state.CareStateByIndividualId.ContainsKey(individual.Id));
        Assert.Equal(careState.LastInteractionUtc, state.CareStateByIndividualId[individual.Id].LastInteractionUtc);
        Assert.Equal(careState.RanAwayUntilUtc, state.CareStateByIndividualId[individual.Id].RanAwayUntilUtc);
    }

    [Fact]
    public void Hydrate_CopiesAllAppSettingsProperties()
    {
        var state = new AppState();
        var settings = new AppSettings
        {
            CareMode = CareMode.Hardcore,
            TokenTrackingEnabled = true,
            ActivityTrackingEnabled = false,
            AutoHideInFullscreen = false,
            StartWithWindows = true,
            ScreenCorner = "TopLeft",
            MonitorIndex = 2,
            PetSizePixels = 64,
            ShowProgressPanel = true
        };
        var data = new SaveData
        {
            Individuals = new List<PokemonIndividual>(),
            Settings = settings,
            FeedingTreats = 0
        };

        state.Hydrate(data);

        Assert.Equal(CareMode.Hardcore, state.Settings.CareMode);
        Assert.True(state.Settings.TokenTrackingEnabled);
        Assert.False(state.Settings.ActivityTrackingEnabled);
        Assert.False(state.Settings.AutoHideInFullscreen);
        Assert.True(state.Settings.StartWithWindows);
        Assert.Equal("TopLeft", state.Settings.ScreenCorner);
        Assert.Equal(2, state.Settings.MonitorIndex);
        Assert.Equal(64, state.Settings.PetSizePixels);
        Assert.True(state.Settings.ShowProgressPanel);
    }

    [Fact]
    public void RecomputeStage_SetsCurrentStageFromAccumulatedCarePoints()
    {
        var state = new AppState();
        var individual = MakeIndividual();
        individual.AccumulatedCarePoints = EvolutionThresholds.Stage2Threshold;

        state.RecomputeStage(individual);

        Assert.Equal(EvolutionStage.Stage2, individual.CurrentStage);
    }

    [Fact]
    public void Hydrate_PopulatesCollectionFeedingAndActiveId()
    {
        var state = new AppState();
        var individual = MakeIndividual();
        var data = new SaveData
        {
            Individuals = new List<PokemonIndividual> { individual },
            ActiveIndividualId = individual.Id,
            Settings = new AppSettings(),
            FeedingTreats = 5
        };

        state.Hydrate(data);

        Assert.Single(state.Collection.Individuals);
        Assert.Equal(individual.Id, state.Collection.ActiveIndividualId);
        Assert.Equal(5, state.Feeding.Treats);
    }

    [Fact]
    public void Hydrate_NullActiveIndividualIdWithNonEmptyCollection_LeavesNoActiveIndividual()
    {
        var state = new AppState();
        var individual1 = MakeIndividual();
        var individual2 = MakeIndividual();
        var data = new SaveData
        {
            Individuals = new List<PokemonIndividual> { individual1, individual2 },
            ActiveIndividualId = null,
            Settings = new AppSettings(),
            FeedingTreats = 0
        };

        state.Hydrate(data);

        Assert.Equal(2, state.Collection.Individuals.Count);
        Assert.Null(state.Collection.ActiveIndividualId);
        Assert.Null(state.Collection.GetActive());
    }

    [Fact]
    public void Hydrate_RoundTripsShopInventory()
    {
        var state = new AppState();
        var data = new SaveData
        {
            Individuals = new List<PokemonIndividual>(),
            Settings = new AppSettings(),
            FeedingTreats = 0,
            ShopCoins = 42,
            ShopRemainderUnits = 300,
            ShopCatchLuckyCharmActive = true
        };

        state.Hydrate(data);

        Assert.Equal(42, state.Shop.Coins);
        Assert.Equal(300, state.Shop.RemainderUnits);
        Assert.True(state.Shop.CatchLuckyCharmActive);
    }
}
