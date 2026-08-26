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

    /// The app owner's real affected individual: a Pikachu caught from the OLD hand-authored
    /// offline pool, whose invented chain id 10 is really the 3-stage Pichu/Pikachu/Raichu
    /// chain in the real PokeAPI data the bundled catalog now carries. Left alone, it would
    /// display as "Покемон #172" (Pichu) — the exact bug this migration exists to kill.
    [Fact]
    public void Hydrate_OldOfflinePoolPikachu_IsRemappedOntoALegacyChainIdAndOtherwiseUntouched()
    {
        var pikachu = new PokemonIndividual
        {
            SpeciesId = 25,
            ChainLength = 2,
            EvolutionChainId = 10,
            Nature = Nature.Jolly,
            IsShiny = true,
            CaughtAtUtc = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero),
            AccumulatedCarePoints = 4321,
            CurrentStage = EvolutionStage.Stage2,
            IsFavorite = true
        };
        var id = pikachu.Id;
        var state = new AppState();

        state.Hydrate(new SaveData { Individuals = new List<PokemonIndividual> { pikachu }, Settings = new AppSettings() });

        var migrated = Assert.Single(state.Collection.Individuals);
        Assert.Equal(900004, migrated.EvolutionChainId);

        // Everything else about the individual must survive completely unchanged.
        Assert.Equal(id, migrated.Id);
        Assert.Equal(25, migrated.SpeciesId);
        Assert.Equal(2, migrated.ChainLength);
        Assert.Equal(4321, migrated.AccumulatedCarePoints);
        Assert.Equal(EvolutionStage.Stage2, migrated.CurrentStage);
        Assert.Equal(Nature.Jolly, migrated.Nature);
        Assert.True(migrated.IsShiny);
        Assert.True(migrated.IsFavorite);

        // ...and the legacy chain it now points at reproduces its ORIGINAL 2-stage shape,
        // rather than the real 3-stage chain that would have retconned it into a Pichu.
        var legacy = Assert.Single(LegacyOfflineChains.All, c => c.ChainId == 900004);
        Assert.Equal(2, legacy.ChainLength);
        Assert.Equal(new[] { 25, 26 }, legacy.StageSpeciesIds);
        Assert.Equal("Pikachu", legacy.StageSpeciesNames[0]);
    }

    [Fact]
    public void Hydrate_MigrationIsIdempotent_ASecondPassDoesNotShiftTheChainIdAgain()
    {
        SaveData Save(PokemonIndividual individual) =>
            new() { Individuals = new List<PokemonIndividual> { individual }, Settings = new AppSettings() };

        var pikachu = new PokemonIndividual
        {
            SpeciesId = 25, ChainLength = 2, EvolutionChainId = 10,
            Nature = Nature.Jolly, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow
        };

        new AppState().Hydrate(Save(pikachu));
        Assert.Equal(900004, pikachu.EvolutionChainId);

        new AppState().Hydrate(Save(pikachu));
        Assert.Equal(900004, pikachu.EvolutionChainId);
    }

    /// The migration keys on the FULL (SpeciesId, ChainLength, EvolutionChainId) triple, so an
    /// individual legitimately caught from the real catalog on chain 4 — which really is
    /// Caterpie's line, species 10 — must not be touched.
    [Fact]
    public void Hydrate_RealCatalogIndividualOnAColldingChainId_IsLeftAlone()
    {
        var caterpie = new PokemonIndividual
        {
            SpeciesId = 10, ChainLength = 3, EvolutionChainId = 4,
            Nature = Nature.Hardy, IsShiny = false, CaughtAtUtc = DateTimeOffset.UtcNow
        };
        var state = new AppState();

        state.Hydrate(new SaveData { Individuals = new List<PokemonIndividual> { caterpie }, Settings = new AppSettings() });

        Assert.Equal(4, Assert.Single(state.Collection.Individuals).EvolutionChainId);
    }

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
    public void TryEvolve_WhenReady_AdvancesStageAndReturnsTrue()
    {
        var state = new AppState();
        var individual = MakeIndividual();
        individual.AccumulatedCarePoints = EvolutionThresholds.Stage2Threshold;

        Assert.True(state.IsReadyToEvolve(individual));
        Assert.True(state.TryEvolve(individual));
        Assert.Equal(EvolutionStage.Stage2, individual.CurrentStage);
    }

    [Fact]
    public void TryEvolve_WhenNotReady_IsANoOpAndReturnsFalse()
    {
        var state = new AppState();
        var individual = MakeIndividual();
        individual.AccumulatedCarePoints = EvolutionThresholds.Stage2Threshold - 1;

        Assert.False(state.IsReadyToEvolve(individual));
        Assert.False(state.TryEvolve(individual));
        Assert.Equal(EvolutionStage.Stage1, individual.CurrentStage);
    }

    /// The behavior change this replaced RecomputeStage for: crossing the threshold used to
    /// evolve the pet instantly, which took the moment away from the player.
    [Fact]
    public void ApplyActivityUnits_CrossingThreshold_DoesNotAutoEvolveButMarksReady()
    {
        var state = new AppState();
        var individual = MakeIndividual();
        state.Collection.Add(individual);

        state.ApplyActivityUnits(EvolutionThresholds.Stage2Threshold, DateTimeOffset.UtcNow);

        Assert.Equal(EvolutionStage.Stage1, individual.CurrentStage);
        Assert.True(state.IsReadyToEvolve(individual));
    }

    [Fact]
    public void TryEvolve_CalledTwiceOnTheSameThreshold_OnlyAdvancesOneStage()
    {
        var state = new AppState();
        var individual = MakeIndividual();
        individual.AccumulatedCarePoints = EvolutionThresholds.Stage2Threshold;

        Assert.True(state.TryEvolve(individual));
        Assert.False(state.TryEvolve(individual));
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

    [Fact]
    public void Hydrate_RoundTripsPendingEggs()
    {
        var state = new AppState();
        var egg = new PendingEgg { Tier = RarityTier.Rare, ReadyAtUtc = DateTimeOffset.UtcNow.AddMinutes(30) };
        var data = new SaveData
        {
            Individuals = new List<PokemonIndividual>(),
            Settings = new AppSettings(),
            FeedingTreats = 0,
            PendingEggs = new List<PendingEgg> { egg }
        };

        state.Hydrate(data);

        Assert.Single(state.PendingEggs);
        Assert.Equal(egg.Tier, state.PendingEggs[0].Tier);
        Assert.Equal(egg.ReadyAtUtc, state.PendingEggs[0].ReadyAtUtc);
    }
}
