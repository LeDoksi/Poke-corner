# Collection, Shop & Visual Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement both approved-but-unexecuted design specs together: [Collection & Progress](../specs/2026-08-20-collection-and-progress-design.md) (Pet/Feed split, live progress bars, Pokédex/Catch log tabs) and [Shop, Visual Overhaul & Sprites](../specs/2026-08-20-shop-visuals-and-sprites-design.md) (coin-based shop with eggs, procedural pet reactions, a Pokédex-dark WPF theme, the full Gen 1-5 catalog + animated sprites, a dynamic tray icon, and an activity-rate recalibration for realistic gamer click rates).

**Architecture:** Core domain changes land first (recalibrated constants, `FeedingService.Pet`/`Feed`, `ShopService`, `RarityClassifier`, `CatchRoller.RollCatchFromTier`, `SpeciesCatalog`, `GifFrameDecoder`) since every App-layer task depends on at least one of them. App-layer tasks then build UI surfaces one at a time (Settings/Collection wiring, progress bars, Pokédex tab, Shop popup) and apply the shared `PokedexTheme.xaml` resource dictionary to each surface as it's built, rather than as a separate pass that touches already-finished files again. Sprite/catalog/tray-icon/reaction wiring lands last since it depends on the composition root (`App.xaml.cs`) being otherwise stable.

**Tech Stack:** .NET 8, WPF (`net8.0-windows`), Windows Forms interop (`NotifyIcon`), xUnit, `System.Drawing.Common` (new — for `GifFrameDecoder` and tray icon bitmap conversion).

## Global Constraints

- No existing public interface may change in a way that breaks existing callers without this plan updating every call site in the same task — `CatchRoller.RollCatch` gains an optional trailing parameter (default preserves old behavior), never a breaking signature change.
- Every activity/Care-Point-denominated constant (existing or new) uses the recalibrated values from Task 1 — see the design spec's §0 table. Coin prices are already final and need no further scaling.
- The app must keep working fully offline after first successful sync — `SpeciesCatalog`'s network refresh and the animated-sprite fetch both degrade to existing fallbacks (offline starter chains, static sprite) exactly as already proven in the Windows Shell plan.
- Cosmetics/accessories in the shop and the MSI installer are explicitly out of scope for this plan.

---

## Task 1: Recalibrate activity-based constants for realistic gamer click rates

**Files:**
- Modify: `src/PokeCorner.Core/Activity/ActivityAccumulator.cs`
- Modify: `src/PokeCorner.Core/Domain/EvolutionCalculator.cs`
- Modify: `src/PokeCorner.Core/Domain/FeedingService.cs`
- Test: `tests/PokeCorner.Core.Tests/Domain/EvolutionCalculatorTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `ActivityAccumulator.CatchCheckIntervalUnits = 22_500`, `EvolutionThresholds.Stage2Threshold = 37_500`, `EvolutionThresholds.Stage3Threshold = 150_000`, `FeedingService.ActivityUnitsPerTreatEarned = 3_750` — every later task's code and tests must use these exact values (via the constants, never hardcoded).

`ActivityAccumulatorTests.cs` and `AppStateTests.cs` already reference these constants symbolically (never hardcode the raw numbers), so they need no changes. Only `EvolutionCalculatorTests.cs` hardcodes the old raw threshold numbers in `InlineData` and must be updated in lockstep.

- [ ] **Step 1: Update the hardcoded threshold expectations in the failing test**

Replace the `[Theory]`'s `InlineData` rows in `tests/PokeCorner.Core.Tests/Domain/EvolutionCalculatorTests.cs`:

```csharp
    [Theory]
    [InlineData(0, 3, EvolutionStage.Stage1)]
    [InlineData(37499, 3, EvolutionStage.Stage1)]
    [InlineData(37500, 3, EvolutionStage.Stage2)]
    [InlineData(149999, 3, EvolutionStage.Stage2)]
    [InlineData(150000, 3, EvolutionStage.Stage3)]
    [InlineData(999_999, 3, EvolutionStage.Stage3)]
    public void StageForPoints_ThreeStageChain_MatchesThresholds(int points, int chainLength, EvolutionStage expected)
    {
        Assert.Equal(expected, EvolutionCalculator.StageForPoints(points, chainLength));
    }
```

(The rest of the file — `StageForPoints_SingleStageChain_NeverEvolves`, `StageForPoints_TwoStageChain_CapsAtStage2` — is unchanged; both already use `999_999`, a value comfortably above any threshold.)

- [ ] **Step 2: Run the test to verify it fails against the current (un-recalibrated) constants**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter StageForPoints_ThreeStageChain_MatchesThresholds`
Expected: FAIL — e.g. `StageForPoints(37500, 3)` currently returns `Stage3` (old threshold was 20000), not the expected `Stage2`.

- [ ] **Step 3: Recalibrate the three constants**

In `src/PokeCorner.Core/Activity/ActivityAccumulator.cs`, change:
```csharp
    // Recalibrated for realistic gamer click rates (150 APM / 9000 units/hour, based on
    // real Dota 2 APM data — see docs/superpowers/specs/2026-08-20-shop-visuals-and-sprites-design.md
    // §0) rather than the ~1200 units/hour this constant originally, silently, assumed.
    // ~2.5 hours of active play still yields roughly one catch-check.
    public const long CatchCheckIntervalUnits = 22_500;
```

In `src/PokeCorner.Core/Domain/EvolutionCalculator.cs`, change:
```csharp
public static class EvolutionThresholds
{
    // Recalibrated ×7.5 alongside CatchCheckIntervalUnits — see that constant's comment.
    public const int Stage2Threshold = 37_500;
    public const int Stage3Threshold = 150_000;
}
```

In `src/PokeCorner.Core/Domain/FeedingService.cs`, change:
```csharp
    // Recalibrated ×7.5 alongside ActivityAccumulator.CatchCheckIntervalUnits.
    public const long ActivityUnitsPerTreatEarned = 3_750;
```
(Leave `TreatCostPerFeed` and the rest of the file untouched for now — Task 3 replaces `TryFeed` entirely.)

- [ ] **Step 4: Run the full Core test suite**

Run: `dotnet test tests/PokeCorner.Core.Tests`
Expected: PASS, all tests green (no other test hardcodes these raw numbers).

- [ ] **Step 5: Commit**

```bash
git add src/PokeCorner.Core/Activity/ActivityAccumulator.cs src/PokeCorner.Core/Domain/EvolutionCalculator.cs src/PokeCorner.Core/Domain/FeedingService.cs tests/PokeCorner.Core.Tests/Domain/EvolutionCalculatorTests.cs
git commit -m "Recalibrate activity-based constants for realistic gamer click rates (x7.5)"
```

---

## Task 2: `ActivityAccumulator.UnitsSinceLastCatchCheck` getter

**Files:**
- Modify: `src/PokeCorner.Core/Activity/ActivityAccumulator.cs`
- Test: `tests/PokeCorner.Core.Tests/Activity/ActivityAccumulatorTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `ActivityAccumulator.UnitsSinceLastCatchCheck : long` (public getter) — later App-layer progress-bar code reads this to compute "progress toward next catch."

- [ ] **Step 1: Write the failing test**

```csharp
// Add to tests/PokeCorner.Core.Tests/Activity/ActivityAccumulatorTests.cs
[Fact]
public void UnitsSinceLastCatchCheck_ReflectsPartialProgressTowardNextInterval()
{
    var accumulator = new ActivityAccumulator();

    accumulator.AddUnits(100);

    Assert.Equal(100, accumulator.UnitsSinceLastCatchCheck);
}

[Fact]
public void UnitsSinceLastCatchCheck_ResetsAfterCrossingInterval()
{
    var accumulator = new ActivityAccumulator();

    accumulator.AddUnits(ActivityAccumulator.CatchCheckIntervalUnits + 400);

    Assert.Equal(400, accumulator.UnitsSinceLastCatchCheck);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter ActivityAccumulatorTests`
Expected: FAIL — `UnitsSinceLastCatchCheck` doesn't exist.

- [ ] **Step 3: Expose the getter**

In `src/PokeCorner.Core/Activity/ActivityAccumulator.cs`, add alongside `TotalUnits`:
```csharp
    public long UnitsSinceLastCatchCheck => _unitsSinceLastCatchCheck;
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter ActivityAccumulatorTests`
Expected: PASS, all tests green (6 total).

- [ ] **Step 5: Commit**

```bash
git add src/PokeCorner.Core/Activity/ActivityAccumulator.cs tests/PokeCorner.Core.Tests/Activity/ActivityAccumulatorTests.cs
git commit -m "Expose ActivityAccumulator.UnitsSinceLastCatchCheck for progress-bar consumers"
```

---

## Task 3: `FeedingService.Pet`/`Feed` replacing `TryFeed`; remove `FeedingMode`; `AppState.RecomputeStage`

**Files:**
- Modify: `src/PokeCorner.Core/Domain/FeedingService.cs`
- Delete: `src/PokeCorner.Core/Domain/FeedingMode.cs`
- Modify: `src/PokeCorner.Core/AppSettings.cs`
- Modify: `src/PokeCorner.Core/AppState.cs`
- Modify: `tests/PokeCorner.Core.Tests/Domain/FeedingServiceTests.cs`
- Modify: `tests/PokeCorner.Core.Tests/AppStateTests.cs`

**Interfaces:**
- Consumes: `EvolutionCalculator.StageForPoints` (existing), `FeedingInventory` (existing), `CareState` (existing).
- Produces: `FeedingService.Pet(PokemonIndividual, CareState, DateTimeOffset)`, `FeedingService.Feed(PokemonIndividual, FeedingInventory, CareState, DateTimeOffset) : bool`, `FeedingService.PetCarePointsBonus = 110`, `FeedingService.FeedCarePointsBonus = 2250`, `AppState.RecomputeStage(PokemonIndividual)`, `AppSettings.ShowProgressPanel : bool` (default `false`) — later App-layer tasks (4, 9, 11) call all of these.

This task removes `FeedingMode` from the codebase entirely — no deprecated shim, per the design spec. `App.xaml.cs`, `CollectionView.xaml.cs`, `SettingsView.xaml`/`.xaml.cs` still reference it after this task and **will not compile** until Task 9 updates them — that is expected; Task 9 must land before the App project is built/tested again, but the Core project and its tests are fully green after this task on their own.

- [ ] **Step 1: Write the failing tests**

Replace the entire contents of `tests/PokeCorner.Core.Tests/Domain/FeedingServiceTests.cs`:

```csharp
using PokeCorner.Core.Domain;
using Xunit;

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
    public void Pet_ClearsRunAwayCooldown()
    {
        var service = new FeedingService();
        var individual = MakeIndividual();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { RanAwayUntilUtc = now + TimeSpan.FromHours(1) };

        service.Pet(individual, state, now);

        Assert.Null(state.RanAwayUntilUtc);
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
    public void Feed_ClearsRunAwayCooldown()
    {
        var service = new FeedingService();
        var individual = MakeIndividual();
        var now = DateTimeOffset.UtcNow;
        var inventory = new FeedingInventory { Treats = 1 };
        var state = new CareState { RanAwayUntilUtc = now + TimeSpan.FromHours(1) };

        service.Feed(individual, inventory, state, now);

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
```

Also replace `Hydrate_CopiesAllAppSettingsProperties` in `tests/PokeCorner.Core.Tests/AppStateTests.cs` (remove the `FeedingMode` set/assert, add `ShowProgressPanel` coverage instead) and add a new direct `RecomputeStage` test:

```csharp
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
```

(Remove the old `FeedingTreats = 0` line's neighboring `FeedingMode = FeedingMode.Economy` from the settings object above — it's simply absent from the new version shown, which is the complete replacement.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter "FeedingServiceTests|AppStateTests"`
Expected: FAIL to compile — `service.Pet`/`service.Feed`/`state.RecomputeStage`/`AppSettings.ShowProgressPanel` don't exist yet, and `FeedingMode` still exists so the old test file's removal isn't itself an error, but the new members are missing.

- [ ] **Step 3: Replace `FeedingService.TryFeed` with `Pet`/`Feed`**

Replace the entire contents of `src/PokeCorner.Core/Domain/FeedingService.cs`:

```csharp
namespace PokeCorner.Core.Domain;

public sealed class FeedingService
{
    // Recalibrated ×7.5 alongside ActivityAccumulator.CatchCheckIntervalUnits.
    public const long ActivityUnitsPerTreatEarned = 3_750;
    public const int PetCarePointsBonus = 110;
    public const int FeedCarePointsBonus = 2_250;

    /// Always available, free. Triggered by a left-click on the desktop sprite.
    public void Pet(PokemonIndividual individual, CareState careState, DateTimeOffset nowUtc)
    {
        individual.AccumulatedCarePoints += PetCarePointsBonus;
        careState.LastInteractionUtc = nowUtc;
        careState.RanAwayUntilUtc = null;
    }

    /// Only available through the Collection popup, and only when a treat is in stock.
    public bool Feed(PokemonIndividual individual, FeedingInventory inventory, CareState careState, DateTimeOffset nowUtc)
    {
        if (inventory.Treats < 1) return false;

        inventory.Treats -= 1;
        individual.AccumulatedCarePoints += FeedCarePointsBonus;
        careState.LastInteractionUtc = nowUtc;
        careState.RanAwayUntilUtc = null;
        return true;
    }

    public void EarnTreatsFromActivity(FeedingInventory inventory, long activityUnits)
    {
        var totalUnits = inventory.RemainderUnits + activityUnits;
        inventory.Treats += (int)(totalUnits / ActivityUnitsPerTreatEarned);
        inventory.RemainderUnits = totalUnits % ActivityUnitsPerTreatEarned;
    }
}
```

Delete `src/PokeCorner.Core/Domain/FeedingMode.cs` entirely.

- [ ] **Step 4: Remove `FeedingMode` from `AppSettings`, add `ShowProgressPanel`**

In `src/PokeCorner.Core/AppSettings.cs`, remove the `FeedingMode` property line and add `ShowProgressPanel`:

```csharp
using PokeCorner.Core.Domain;

namespace PokeCorner.Core;

public sealed class AppSettings
{
    public CareMode CareMode { get; set; } = CareMode.Chill;
    public bool TokenTrackingEnabled { get; set; } = false;
    public bool ActivityTrackingEnabled { get; set; } = true;
    public bool AutoHideInFullscreen { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public string ScreenCorner { get; set; } = "BottomRight";
    public int MonitorIndex { get; set; } = 0;
    public int PetSizePixels { get; set; } = 96;
    public bool ShowProgressPanel { get; set; } = false;
}
```

- [ ] **Step 5: Add `AppState.RecomputeStage`, use it from `ApplyActivityUnits`, remove the `FeedingMode` hydrate line**

In `src/PokeCorner.Core/AppState.cs`:

```csharp
    public void ApplyActivityUnits(long units, DateTimeOffset nowUtc)
    {
        var active = Collection.GetActive();
        if (active is not null)
        {
            active.AccumulatedCarePoints = (int)Math.Min((long)active.AccumulatedCarePoints + units, int.MaxValue);
            RecomputeStage(active);
        }

        Activity.AddUnits(units);
    }

    /// Single place that turns AccumulatedCarePoints into CurrentStage — used by
    /// ApplyActivityUnits and by every App-layer call site that mutates Care Points
    /// directly (Pet/Feed clicks, shop's Care Points Booster), so none of them duplicate
    /// this line.
    public void RecomputeStage(PokemonIndividual individual)
    {
        individual.CurrentStage = EvolutionCalculator.StageForPoints(individual.AccumulatedCarePoints, individual.ChainLength);
    }
```

And in `Hydrate`, delete the line `Settings.FeedingMode = data.Settings.FeedingMode;` (leave every other `Settings.X = data.Settings.X;` line untouched).

- [ ] **Step 6: Run the Core test suite**

Run: `dotnet test tests/PokeCorner.Core.Tests`
Expected: PASS, all tests green. (The App project will not build yet — that's expected until Task 9; do not attempt to build/test `PokeCorner.App` or `PokeCorner.App.Tests` in this task.)

- [ ] **Step 7: Commit**

```bash
git add src/PokeCorner.Core/Domain/FeedingService.cs src/PokeCorner.Core/AppSettings.cs src/PokeCorner.Core/AppState.cs tests/PokeCorner.Core.Tests/Domain/FeedingServiceTests.cs tests/PokeCorner.Core.Tests/AppStateTests.cs
git rm src/PokeCorner.Core/Domain/FeedingMode.cs
git commit -m "Replace FeedingService.TryFeed with Pet/Feed; remove FeedingMode; add AppState.RecomputeStage"
```

---

## Task 4: `RarityClassifier`, `ShopInventory`, `ShopService`

**Files:**
- Create: `src/PokeCorner.Core/Domain/RarityClassifier.cs`
- Create: `src/PokeCorner.Core/Domain/ShopInventory.cs`
- Create: `src/PokeCorner.Core/Domain/ShopService.cs`
- Test: `tests/PokeCorner.Core.Tests/Domain/RarityClassifierTests.cs`
- Test: `tests/PokeCorner.Core.Tests/Domain/ShopServiceTests.cs`

**Interfaces:**
- Consumes: `FeedingInventory` (existing), `PokemonIndividual` (existing).
- Produces: `enum RarityTier { Common, Uncommon, Rare, Legendary }`, `RarityClassifier.Classify(int captureRate) : RarityTier`, `class ShopInventory { int Coins; long RemainderUnits; bool CatchLuckyCharmActive; }`, `ShopService` with the methods/constants below — Task 5 (`RollCatchFromTier`), Task 6 (persistence), and Tasks 13/14/16 (App-layer wiring) all depend on these exact names.

- [ ] **Step 1: Write the failing `RarityClassifier` test**

```csharp
// tests/PokeCorner.Core.Tests/Domain/RarityClassifierTests.cs
using PokeCorner.Core.Domain;
using Xunit;

namespace PokeCorner.Core.Tests.Domain;

public class RarityClassifierTests
{
    [Theory]
    [InlineData(255, RarityTier.Common)]
    [InlineData(150, RarityTier.Common)]
    [InlineData(149, RarityTier.Uncommon)]
    [InlineData(70, RarityTier.Uncommon)]
    [InlineData(69, RarityTier.Rare)]
    [InlineData(30, RarityTier.Rare)]
    [InlineData(29, RarityTier.Legendary)]
    [InlineData(3, RarityTier.Legendary)]
    public void Classify_MapsCaptureRateToTier(int captureRate, RarityTier expected)
    {
        Assert.Equal(expected, RarityClassifier.Classify(captureRate));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter RarityClassifierTests`
Expected: FAIL — `RarityClassifier`/`RarityTier` don't exist.

- [ ] **Step 3: Write `RarityClassifier.cs`**

```csharp
// src/PokeCorner.Core/Domain/RarityClassifier.cs
namespace PokeCorner.Core.Domain;

public enum RarityTier { Common, Uncommon, Rare, Legendary }

public static class RarityClassifier
{
    public static RarityTier Classify(int captureRate) => captureRate switch
    {
        >= 150 => RarityTier.Common,
        >= 70 => RarityTier.Uncommon,
        >= 30 => RarityTier.Rare,
        _ => RarityTier.Legendary
    };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter RarityClassifierTests`
Expected: PASS, 8 cases green.

- [ ] **Step 5: Write the failing `ShopService` tests**

```csharp
// tests/PokeCorner.Core.Tests/Domain/ShopServiceTests.cs
using PokeCorner.Core.Domain;
using Xunit;

namespace PokeCorner.Core.Tests.Domain;

public class ShopServiceTests
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
    public void EarnCoinsFromActivity_ConvertsUnitsAtFixedRate()
    {
        var service = new ShopService();
        var shop = new ShopInventory();

        service.EarnCoinsFromActivity(shop, ShopService.ActivityUnitsPerCoinEarned * 4 + 50);

        Assert.Equal(4, shop.Coins);
        Assert.Equal(50, shop.RemainderUnits);
    }

    [Fact]
    public void BuyTreatPack_WithoutEnoughCoins_Fails()
    {
        var service = new ShopService();
        var shop = new ShopInventory { Coins = ShopService.TreatPackPrice - 1 };
        var feeding = new FeedingInventory();

        var bought = service.BuyTreatPack(shop, feeding);

        Assert.False(bought);
        Assert.Equal(0, feeding.Treats);
    }

    [Fact]
    public void BuyTreatPack_WithEnoughCoins_SpendsCoinsAndAddsTreats()
    {
        var service = new ShopService();
        var shop = new ShopInventory { Coins = ShopService.TreatPackPrice };
        var feeding = new FeedingInventory();

        var bought = service.BuyTreatPack(shop, feeding);

        Assert.True(bought);
        Assert.Equal(0, shop.Coins);
        Assert.Equal(ShopService.TreatPackAmount, feeding.Treats);
    }

    [Fact]
    public void BuyCarePointsBooster_WithEnoughCoins_SpendsCoinsAndAddsCarePoints()
    {
        var service = new ShopService();
        var shop = new ShopInventory { Coins = ShopService.CarePointsBoosterPrice };
        var individual = MakeIndividual();

        var bought = service.BuyCarePointsBooster(shop, individual);

        Assert.True(bought);
        Assert.Equal(0, shop.Coins);
        Assert.Equal(ShopService.CarePointsBoosterAmount, individual.AccumulatedCarePoints);
    }

    [Fact]
    public void BuyCarePointsBooster_WithoutEnoughCoins_Fails()
    {
        var service = new ShopService();
        var shop = new ShopInventory { Coins = ShopService.CarePointsBoosterPrice - 1 };
        var individual = MakeIndividual();

        var bought = service.BuyCarePointsBooster(shop, individual);

        Assert.False(bought);
        Assert.Equal(0, individual.AccumulatedCarePoints);
    }

    [Fact]
    public void BuyCatchLuckyCharm_WithEnoughCoins_ActivatesCharm()
    {
        var service = new ShopService();
        var shop = new ShopInventory { Coins = ShopService.CatchLuckyCharmPrice };

        var bought = service.BuyCatchLuckyCharm(shop);

        Assert.True(bought);
        Assert.Equal(0, shop.Coins);
        Assert.True(shop.CatchLuckyCharmActive);
    }

    [Fact]
    public void BuyCatchLuckyCharm_WithoutEnoughCoins_Fails()
    {
        var service = new ShopService();
        var shop = new ShopInventory { Coins = ShopService.CatchLuckyCharmPrice - 1 };

        var bought = service.BuyCatchLuckyCharm(shop);

        Assert.False(bought);
        Assert.False(shop.CatchLuckyCharmActive);
    }

    [Theory]
    [InlineData(RarityTier.Common, ShopService.EggPriceCommon)]
    [InlineData(RarityTier.Uncommon, ShopService.EggPriceUncommon)]
    [InlineData(RarityTier.Rare, ShopService.EggPriceRare)]
    [InlineData(RarityTier.Legendary, ShopService.EggPriceLegendary)]
    public void EggPriceForTier_ReturnsExpectedPrice(RarityTier tier, int expectedPrice)
    {
        Assert.Equal(expectedPrice, ShopService.EggPriceForTier(tier));
    }

    [Fact]
    public void TrySpendForEgg_WithEnoughCoins_SpendsExactTierPrice()
    {
        var service = new ShopService();
        var shop = new ShopInventory { Coins = ShopService.EggPriceLegendary };

        var spent = service.TrySpendForEgg(shop, RarityTier.Legendary);

        Assert.True(spent);
        Assert.Equal(0, shop.Coins);
    }

    [Fact]
    public void TrySpendForEgg_WithoutEnoughCoins_Fails()
    {
        var service = new ShopService();
        var shop = new ShopInventory { Coins = ShopService.EggPriceRare - 1 };

        var spent = service.TrySpendForEgg(shop, RarityTier.Rare);

        Assert.False(spent);
        Assert.Equal(ShopService.EggPriceRare - 1, shop.Coins);
    }
}
```

- [ ] **Step 6: Run tests to verify they fail**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter ShopServiceTests`
Expected: FAIL — `ShopInventory`/`ShopService` don't exist.

- [ ] **Step 7: Write `ShopInventory.cs`**

```csharp
// src/PokeCorner.Core/Domain/ShopInventory.cs
namespace PokeCorner.Core.Domain;

public sealed class ShopInventory
{
    public int Coins { get; set; }
    public long RemainderUnits { get; set; }
    public bool CatchLuckyCharmActive { get; set; }
}
```

- [ ] **Step 8: Write `ShopService.cs`**

```csharp
// src/PokeCorner.Core/Domain/ShopService.cs
namespace PokeCorner.Core.Domain;

public sealed class ShopService
{
    // ~10 coins/hour of active play at the recalibrated 9000-units/hour baseline (see
    // FeedingService.ActivityUnitsPerTreatEarned's comment) — one coin roughly every 6
    // minutes of engaged play.
    public const long ActivityUnitsPerCoinEarned = 900;

    public const int TreatPackPrice = 20;
    public const int TreatPackAmount = 5;
    public const int CarePointsBoosterPrice = 20;
    public const int CarePointsBoosterAmount = 7_500;
    public const int CatchLuckyCharmPrice = 30;
    public const double CatchLuckyCharmShinyMultiplier = 4.0;

    // Deliberately non-linear (+50, +70, +150 between tiers) — Legendary is meant to take
    // ~28-30 hours of play to afford, meaningfully harder than the tiers below it, not just
    // proportionally scaled.
    public const int EggPriceCommon = 30;
    public const int EggPriceUncommon = 80;
    public const int EggPriceRare = 150;
    public const int EggPriceLegendary = 300;

    public void EarnCoinsFromActivity(ShopInventory shop, long activityUnits)
    {
        var totalUnits = shop.RemainderUnits + activityUnits;
        shop.Coins += (int)(totalUnits / ActivityUnitsPerCoinEarned);
        shop.RemainderUnits = totalUnits % ActivityUnitsPerCoinEarned;
    }

    public bool BuyTreatPack(ShopInventory shop, FeedingInventory feeding)
    {
        if (shop.Coins < TreatPackPrice) return false;
        shop.Coins -= TreatPackPrice;
        feeding.Treats += TreatPackAmount;
        return true;
    }

    // Caller (App layer) must call AppState.RecomputeStage(activeIndividual) after a
    // successful purchase — this method only mutates AccumulatedCarePoints, matching the
    // pattern FeedingService.Pet/Feed already established.
    public bool BuyCarePointsBooster(ShopInventory shop, PokemonIndividual activeIndividual)
    {
        if (shop.Coins < CarePointsBoosterPrice) return false;
        shop.Coins -= CarePointsBoosterPrice;
        activeIndividual.AccumulatedCarePoints += CarePointsBoosterAmount;
        return true;
    }

    public bool BuyCatchLuckyCharm(ShopInventory shop)
    {
        if (shop.Coins < CatchLuckyCharmPrice) return false;
        shop.Coins -= CatchLuckyCharmPrice;
        shop.CatchLuckyCharmActive = true;
        return true;
    }

    public static int EggPriceForTier(RarityTier tier) => tier switch
    {
        RarityTier.Common => EggPriceCommon,
        RarityTier.Uncommon => EggPriceUncommon,
        RarityTier.Rare => EggPriceRare,
        RarityTier.Legendary => EggPriceLegendary,
        _ => throw new ArgumentOutOfRangeException(nameof(tier))
    };

    // Only spends the currency — the App layer performs the actual catch roll via
    // CatchRoller.RollCatchFromTier and adds the result to the collection (Task 13),
    // mirroring how activity-triggered catches already work. Kept separate so ShopService,
    // like FeedingService, never needs to know about PokemonCollection.
    public bool TrySpendForEgg(ShopInventory shop, RarityTier tier)
    {
        var price = EggPriceForTier(tier);
        if (shop.Coins < price) return false;
        shop.Coins -= price;
        return true;
    }
}
```

- [ ] **Step 9: Run tests to verify they pass**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter ShopServiceTests`
Expected: PASS, 11 tests green.

- [ ] **Step 10: Commit**

```bash
git add src/PokeCorner.Core/Domain/RarityClassifier.cs src/PokeCorner.Core/Domain/ShopInventory.cs src/PokeCorner.Core/Domain/ShopService.cs tests/PokeCorner.Core.Tests/Domain/RarityClassifierTests.cs tests/PokeCorner.Core.Tests/Domain/ShopServiceTests.cs
git commit -m "Add RarityClassifier and ShopService (coins, treat packs, boosters, eggs)"
```

---

## Task 5: `CatchRoller.RollCatchFromTier` and shiny-chance multiplier

**Files:**
- Modify: `src/PokeCorner.Core/Domain/CatchRoller.cs`
- Test: `tests/PokeCorner.Core.Tests/Domain/CatchRollerTests.cs`

**Interfaces:**
- Consumes: `RarityClassifier.Classify` (Task 4), existing `SpeciesEvolutionChain`/`CatchResult`/`Nature`.
- Produces: `CatchRoller.RollCatch(..., double shinyChanceMultiplier = 1.0)` (existing signature, one new optional trailing parameter — fully backward compatible, no existing call site or test changes needed), `CatchRoller.RollCatchFromTier(IReadOnlyList<SpeciesEvolutionChain> allChains, RarityTier tier, IReadOnlySet<int> ownedChainIds, IReadOnlySet<Nature> ownedNaturesForDuplicateChain, double shinyChanceMultiplier = 1.0) : CatchResult` — Task 15 (activity-triggered catches, Lucky Charm) and Task 13 (egg hatching) both call these.

- [ ] **Step 1: Write the failing tests**

Add to `tests/PokeCorner.Core.Tests/Domain/CatchRollerTests.cs` (the existing `CommonUnowned`/`RareOwned` fixtures and all existing tests stay untouched):

```csharp
    [Fact]
    public void RollCatch_ShinyChanceMultiplier_IncreasesShinyRate()
    {
        var chains = new[] { CommonUnowned };
        var owned = new HashSet<int>();

        var baselineShinyCount = CountShiny(chains, owned, trials: 20_000, seed: 10, shinyChanceMultiplier: 1.0);
        var boostedShinyCount = CountShiny(chains, owned, trials: 20_000, seed: 11, shinyChanceMultiplier: 4.0);

        Assert.True(boostedShinyCount > baselineShinyCount,
            $"Expected boosted shiny rate ({boostedShinyCount}) > baseline shiny rate ({baselineShinyCount})");
    }

    [Fact]
    public void RollCatchFromTier_OnlyPicksFromMatchingTier()
    {
        var legendary = CommonUnowned with { ChainId = 99, CaptureRate = 3 }; // RarityTier.Legendary
        var chains = new[] { CommonUnowned, legendary }; // CommonUnowned is CaptureRate 255 -> RarityTier.Common
        var roller = new CatchRoller(new Random(Seed: 20));

        for (var i = 0; i < 100; i++)
        {
            var result = roller.RollCatchFromTier(chains, RarityTier.Legendary, new HashSet<int>(), new HashSet<Nature>());
            Assert.Equal(legendary.ChainId, result.ChainId);
        }
    }

    [Fact]
    public void RollCatchFromTier_NoChainsInTier_Throws()
    {
        var chains = new[] { CommonUnowned }; // only RarityTier.Common available
        var roller = new CatchRoller(new Random(Seed: 21));

        Assert.Throws<InvalidOperationException>(() =>
            roller.RollCatchFromTier(chains, RarityTier.Legendary, new HashSet<int>(), new HashSet<Nature>()));
    }

    private static int CountShiny(IReadOnlyList<SpeciesEvolutionChain> chains, IReadOnlySet<int> ownedChainIds, int trials, int seed, double shinyChanceMultiplier)
    {
        var roller = new CatchRoller(new Random(seed));
        var shinyCount = 0;
        for (var i = 0; i < trials; i++)
        {
            var result = roller.RollCatch(chains, ownedChainIds, ownedNaturesForDuplicateChain: new HashSet<Nature>(), shinyChanceMultiplier);
            if (result.IsShiny) shinyCount++;
        }
        return shinyCount;
    }
```

(This adds a second, parameterized `CountShiny` overload alongside the file's existing no-multiplier-parameter `CountShiny` — keep both; the existing one is still used by `RollCatch_Duplicate_ShinyChanceIsHigherThanNewCatch`.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter CatchRollerTests`
Expected: FAIL to compile — `shinyChanceMultiplier` parameter and `RollCatchFromTier` don't exist yet.

- [ ] **Step 3: Add the multiplier parameter and `RollCatchFromTier`**

In `src/PokeCorner.Core/Domain/CatchRoller.cs`, change the `RollCatch` signature and its `isShiny` line:

```csharp
    public CatchResult RollCatch(
        IReadOnlyList<SpeciesEvolutionChain> allChains,
        IReadOnlySet<int> ownedChainIds,
        IReadOnlySet<Nature> ownedNaturesForDuplicateChain,
        double shinyChanceMultiplier = 1.0)
    {
        if (allChains.Count == 0)
        {
            throw new ArgumentException("At least one species chain is required to roll a catch.", nameof(allChains));
        }

        var chain = PickChain(allChains, ownedChainIds);
        var isDuplicate = ownedChainIds.Contains(chain.ChainId);

        var isShiny = _random.NextDouble() < (isDuplicate ? BaseShinyChance * DuplicateShinyChanceMultiplier : BaseShinyChance) * shinyChanceMultiplier;
        var nature = isDuplicate
            ? PickNatureBiasedTowardUnowned(ownedNaturesForDuplicateChain)
            : PickUniformNature();

        return new CatchResult(chain.ChainId, chain.StarterSpeciesId, nature, isShiny, isDuplicate, chain.ChainLength);
    }

    // Guaranteed-tier catch for shop eggs — filters to the requested rarity first, then
    // reuses the existing weighting/shiny/nature logic among just that subset.
    public CatchResult RollCatchFromTier(
        IReadOnlyList<SpeciesEvolutionChain> allChains,
        RarityTier tier,
        IReadOnlySet<int> ownedChainIds,
        IReadOnlySet<Nature> ownedNaturesForDuplicateChain,
        double shinyChanceMultiplier = 1.0)
    {
        var tierChains = allChains.Where(c => RarityClassifier.Classify(c.CaptureRate) == tier).ToList();
        if (tierChains.Count == 0)
        {
            throw new InvalidOperationException($"No species available in rarity tier {tier} — caller must check availability before offering this purchase.");
        }
        return RollCatch(tierChains, ownedChainIds, ownedNaturesForDuplicateChain, shinyChanceMultiplier);
    }
```

(Everything else in the file — `PickChain`, `PickUniformNature`, `PickNatureBiasedTowardUnowned`, the constants at the top — is unchanged.)

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter CatchRollerTests`
Expected: PASS, all tests green (existing 4 + 3 new = 7).

- [ ] **Step 5: Commit**

```bash
git add src/PokeCorner.Core/Domain/CatchRoller.cs tests/PokeCorner.Core.Tests/Domain/CatchRollerTests.cs
git commit -m "Add CatchRoller.RollCatchFromTier and shiny-chance multiplier for shop eggs/lucky charm"
```

---

## Task 6: Persist `ShopInventory` in `AppState`/`SaveData`

**Files:**
- Modify: `src/PokeCorner.Core/Persistence/SaveData.cs`
- Modify: `src/PokeCorner.Core/AppState.cs`
- Test: `tests/PokeCorner.Core.Tests/AppStateTests.cs`
- Test: `tests/PokeCorner.Core.Tests/Persistence/JsonSaveStoreTests.cs`

**Interfaces:**
- Consumes: `ShopInventory` (Task 4).
- Produces: `AppState.Shop : ShopInventory` (like the existing `AppState.Feeding`), `SaveData.ShopCoins`/`ShopRemainderUnits`/`ShopCatchLuckyCharmActive` — Task 10 (App-layer `PersistState`/growth tick) reads/writes these.

- [ ] **Step 1: Write the failing tests**

Add to `tests/PokeCorner.Core.Tests/AppStateTests.cs`:

```csharp
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
```

Add to `tests/PokeCorner.Core.Tests/Persistence/JsonSaveStoreTests.cs`, extending the existing round-trip test rather than adding a new one:

```csharp
    [Fact]
    public void Save_ThenLoad_RoundTripsIndividualsSettingsAndActiveId()
    {
        var store = new JsonSaveStore(_tempFile);
        var individual = new PokemonIndividual
        {
            SpeciesId = 25,
            EvolutionChainId = 10,
            ChainLength = 2,
            Nature = Nature.Jolly,
            IsShiny = true,
            CaughtAtUtc = DateTimeOffset.UtcNow
        };
        var data = new SaveData
        {
            Individuals = new List<PokemonIndividual> { individual },
            ActiveIndividualId = individual.Id,
            Settings = new AppSettings { CareMode = CareMode.Hardcore, PetSizePixels = 128 },
            FeedingTreats = 7,
            ShopCoins = 15,
            ShopRemainderUnits = 200,
            ShopCatchLuckyCharmActive = true
        };

        store.Save(data);
        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Single(loaded!.Individuals);
        Assert.Equal(25, loaded.Individuals[0].SpeciesId);
        Assert.True(loaded.Individuals[0].IsShiny);
        Assert.Equal(individual.Id, loaded.ActiveIndividualId);
        Assert.Equal(CareMode.Hardcore, loaded.Settings.CareMode);
        Assert.Equal(128, loaded.Settings.PetSizePixels);
        Assert.Equal(7, loaded.FeedingTreats);
        Assert.Equal(15, loaded.ShopCoins);
        Assert.Equal(200, loaded.ShopRemainderUnits);
        Assert.True(loaded.ShopCatchLuckyCharmActive);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter "AppStateTests|JsonSaveStoreTests"`
Expected: FAIL to compile — `SaveData.ShopCoins` etc. and `AppState.Shop` don't exist.

- [ ] **Step 3: Add the fields to `SaveData`**

In `src/PokeCorner.Core/Persistence/SaveData.cs`:

```csharp
using PokeCorner.Core.Domain;

namespace PokeCorner.Core.Persistence;

public sealed class SaveData
{
    public required List<PokemonIndividual> Individuals { get; init; }
    public Guid? ActiveIndividualId { get; init; }
    public required AppSettings Settings { get; init; }
    public int FeedingTreats { get; init; }
    public int ShopCoins { get; init; }
    public long ShopRemainderUnits { get; init; }
    public bool ShopCatchLuckyCharmActive { get; init; }
    public Dictionary<Guid, CareState> CareStates { get; init; } = new();
}
```

- [ ] **Step 4: Add `AppState.Shop` and wire it into `Hydrate`**

In `src/PokeCorner.Core/AppState.cs`, add the property alongside `Feeding`:

```csharp
    public ShopInventory Shop { get; } = new();
```

And in `Hydrate`, add alongside the existing `Feeding.Treats = data.FeedingTreats;` line:

```csharp
        Shop.Coins = data.ShopCoins;
        Shop.RemainderUnits = data.ShopRemainderUnits;
        Shop.CatchLuckyCharmActive = data.ShopCatchLuckyCharmActive;
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/PokeCorner.Core.Tests`
Expected: PASS, all tests green.

- [ ] **Step 6: Commit**

```bash
git add src/PokeCorner.Core/Persistence/SaveData.cs src/PokeCorner.Core/AppState.cs tests/PokeCorner.Core.Tests/AppStateTests.cs tests/PokeCorner.Core.Tests/Persistence/JsonSaveStoreTests.cs
git commit -m "Persist ShopInventory (coins, lucky charm) in AppState/SaveData"
```

---

## Task 7: `SpeciesCatalog` — full Gen 1-5 catalog with offline fallback

**Files:**
- Modify: `src/PokeCorner.Core/PokeApi/PokeApiClient.cs`
- Create: `src/PokeCorner.Core/PokeApi/SpeciesCatalog.cs`
- Test: `tests/PokeCorner.Core.Tests/PokeApi/SpeciesCatalogTests.cs`

**Interfaces:**
- Consumes: `PokeApiClient`, `SpriteCache.LoadOfflineStarters()` (existing).
- Produces: `PokeApiClient.FetchEvolutionChainIdForSpeciesAsync(int) : Task<int>`, `class SpeciesCatalog { SpeciesCatalog(PokeApiClient, SpriteCache, string catalogFilePath); IReadOnlyList<SpeciesEvolutionChain> LoadCachedOrFallback(); Task RefreshFromNetworkAsync(int maxSpeciesId = 649); }` — Task 20 wires this into the composition root; this is what actually populates Rare/Legendary tiers for the shop and Pokédex tab (on the 7-chain offline fallback alone those tiers are near-empty).

This task's test and implementation code is carried over unchanged from the already-reviewed, not-yet-executed `docs/superpowers/plans/2026-08-19-pokecorner-full-experience-and-installer.md` (Task 1) — no redesign needed.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/PokeCorner.Core.Tests/PokeApi/SpeciesCatalogTests.cs
using System.Net;
using System.Text;
using System.Text.Json;
using PokeCorner.Core.Domain;
using PokeCorner.Core.PokeApi;
using Xunit;

namespace PokeCorner.Core.Tests.PokeApi;

public class SpeciesCatalogTests : IDisposable
{
    private sealed class RoutingStubHandler : HttpMessageHandler
    {
        private readonly Func<Uri, string> _respond;
        public RoutingStubHandler(Func<Uri, string> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_respond(request.RequestUri!), Encoding.UTF8, "application/json")
            });
    }

    private readonly string _tempDir;
    private readonly string _catalogFile;
    private readonly SpriteCache _spriteCache;

    public SpeciesCatalogTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PokeCornerCatalogTests_" + Guid.NewGuid());
        _catalogFile = Path.Combine(_tempDir, "catalog.json");
        _spriteCache = new SpriteCache(Path.Combine(_tempDir, "sprites"));
    }

    public void Dispose() { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }

    [Fact]
    public void LoadCachedOrFallback_NoCacheFile_ReturnsOfflineStarters()
    {
        var catalog = new SpeciesCatalog(new PokeApiClient(new RoutingStubHandler(_ => "{}")), _spriteCache, _catalogFile);

        var result = catalog.LoadCachedOrFallback();

        Assert.Equal(_spriteCache.LoadOfflineStarters().Count, result.Count);
    }

    [Fact]
    public async Task RefreshFromNetworkAsync_TwoSpeciesSharingAChain_DedupesToOneChainAndWritesCacheFile()
    {
        string Respond(Uri uri)
        {
            var path = uri.AbsolutePath;
            if (path.Contains("pokemon-species/1")) return """{"evolution_chain":{"url":"https://pokeapi.co/api/v2/evolution-chain/1/"}}""";
            if (path.Contains("pokemon-species/2")) return """{"evolution_chain":{"url":"https://pokeapi.co/api/v2/evolution-chain/1/"}}""";
            if (path.Contains("pokemon-species/3")) return """{"evolution_chain":{"url":"https://pokeapi.co/api/v2/evolution-chain/2/"}}""";
            if (path.Contains("evolution-chain/1")) return """{"id":1,"chain":{"species":{"name":"a","url":"https://pokeapi.co/api/v2/pokemon-species/1/"},"evolves_to":[{"species":{"name":"b","url":"https://pokeapi.co/api/v2/pokemon-species/2/"},"evolves_to":[]}]},"capture_rate":45}""";
            if (path.Contains("evolution-chain/2")) return """{"id":2,"chain":{"species":{"name":"c","url":"https://pokeapi.co/api/v2/pokemon-species/3/"},"evolves_to":[]},"capture_rate":255}""";
            throw new InvalidOperationException($"Unexpected request: {uri}");
        }
        var catalog = new SpeciesCatalog(new PokeApiClient(new RoutingStubHandler(Respond)), _spriteCache, _catalogFile);

        await catalog.RefreshFromNetworkAsync(maxSpeciesId: 3);

        Assert.True(File.Exists(_catalogFile));
        var cached = JsonSerializer.Deserialize<List<SpeciesEvolutionChain>>(File.ReadAllText(_catalogFile))!;
        Assert.Equal(2, cached.Count);
        Assert.Contains(cached, c => c.ChainId == 1 && c.ChainLength == 2);
        Assert.Contains(cached, c => c.ChainId == 2 && c.ChainLength == 1);
    }

    [Fact]
    public async Task LoadCachedOrFallback_AfterRefresh_ReturnsCachedChainsNotOfflineFallback()
    {
        string Respond(Uri uri)
        {
            if (uri.AbsolutePath.Contains("pokemon-species/1")) return """{"evolution_chain":{"url":"https://pokeapi.co/api/v2/evolution-chain/1/"}}""";
            return """{"id":1,"chain":{"species":{"name":"a","url":"https://pokeapi.co/api/v2/pokemon-species/1/"},"evolves_to":[]},"capture_rate":45}""";
        }
        var catalog = new SpeciesCatalog(new PokeApiClient(new RoutingStubHandler(Respond)), _spriteCache, _catalogFile);
        await catalog.RefreshFromNetworkAsync(maxSpeciesId: 1);

        var result = catalog.LoadCachedOrFallback();

        Assert.Single(result);
        Assert.Equal(1, result[0].ChainId);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter SpeciesCatalogTests`
Expected: FAIL — `SpeciesCatalog` and `PokeApiClient.FetchEvolutionChainIdForSpeciesAsync` don't exist.

- [ ] **Step 3: Add `FetchEvolutionChainIdForSpeciesAsync` to `PokeApiClient`**

```csharp
// Add inside PokeApiClient (src/PokeCorner.Core/PokeApi/PokeApiClient.cs), alongside FetchCaptureRateAsync:
public async Task<int> FetchEvolutionChainIdForSpeciesAsync(int speciesId)
{
    var json = await _httpClient.GetStringAsync($"pokemon-species/{speciesId}/");
    using var doc = JsonDocument.Parse(json);
    var chainUrl = doc.RootElement.GetProperty("evolution_chain").GetProperty("url").GetString()!;
    return ExtractIdFromUrl(chainUrl);
}
```

(`ExtractIdFromUrl` is already a private static method on `PokeApiClient` — reuse it as-is.)

- [ ] **Step 4: Write `SpeciesCatalog.cs`**

```csharp
// src/PokeCorner.Core/PokeApi/SpeciesCatalog.cs
using System.Text.Json;
using PokeCorner.Core.Domain;

namespace PokeCorner.Core.PokeApi;

public sealed class SpeciesCatalog
{
    private readonly PokeApiClient _client;
    private readonly SpriteCache _spriteCache;
    private readonly string _catalogFilePath;

    public SpeciesCatalog(PokeApiClient client, SpriteCache spriteCache, string catalogFilePath)
    {
        _client = client;
        _spriteCache = spriteCache;
        _catalogFilePath = catalogFilePath;
    }

    public IReadOnlyList<SpeciesEvolutionChain> LoadCachedOrFallback()
    {
        if (File.Exists(_catalogFilePath))
        {
            var cached = JsonSerializer.Deserialize<List<SpeciesEvolutionChain>>(File.ReadAllText(_catalogFilePath));
            if (cached is { Count: > 0 }) return cached;
        }
        return _spriteCache.LoadOfflineStarters();
    }

    public async Task RefreshFromNetworkAsync(int maxSpeciesId = 649)
    {
        var seenChainIds = new HashSet<int>();
        var chains = new List<SpeciesEvolutionChain>();

        for (var speciesId = 1; speciesId <= maxSpeciesId; speciesId++)
        {
            var chainId = await _client.FetchEvolutionChainIdForSpeciesAsync(speciesId);
            if (!seenChainIds.Add(chainId)) continue;

            chains.Add(await _client.FetchEvolutionChainAsync(chainId));
        }

        var directory = Path.GetDirectoryName(_catalogFilePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(_catalogFilePath, JsonSerializer.Serialize(chains));
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter SpeciesCatalogTests`
Expected: PASS, 3 tests green.

- [ ] **Step 6: Commit**

```bash
git add src/PokeCorner.Core/PokeApi/PokeApiClient.cs src/PokeCorner.Core/PokeApi/SpeciesCatalog.cs tests/PokeCorner.Core.Tests/PokeApi/SpeciesCatalogTests.cs
git commit -m "Add SpeciesCatalog: full Gen 1-5 catch pool with offline fallback"
```

---

## Task 8: `GifFrameDecoder` — animated sprite decoding

**Files:**
- Modify: `src/PokeCorner.Core/PokeApi/PokeApiClient.cs`
- Modify: `src/PokeCorner.Core/PokeCorner.Core.csproj`
- Create: `src/PokeCorner.Core/PokeApi/GifFrameDecoder.cs`
- Test: `tests/PokeCorner.Core.Tests/PokeApi/GifFrameDecoderTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `PokeApiClient.FetchAnimatedSpriteAsync(int speciesId) : Task<byte[]?>`, `static class GifFrameDecoder { IReadOnlyList<byte[]> DecodeFramesAsPng(byte[] gifBytes); }` — Task 21 wires these into `RefreshOverlayForActiveIndividual`.

Carried over unchanged from `docs/superpowers/plans/2026-08-19-pokecorner-full-experience-and-installer.md` (Task 2) — no redesign needed.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/PokeCorner.Core.Tests/PokeApi/GifFrameDecoderTests.cs
using System.Drawing;
using System.Drawing.Imaging;
using PokeCorner.Core.PokeApi;
using Xunit;

namespace PokeCorner.Core.Tests.PokeApi;

public class GifFrameDecoderTests
{
    private static byte[] MakeSingleFrameGif()
    {
        using var stream = new MemoryStream();
        using var frame0 = new Bitmap(4, 4);
        frame0.Save(stream, ImageFormat.Gif);
        return stream.ToArray();
    }

    [Fact]
    public void DecodeFramesAsPng_SingleFrameGif_ReturnsOneFrame()
    {
        var gifBytes = MakeSingleFrameGif();

        var frames = GifFrameDecoder.DecodeFramesAsPng(gifBytes);

        Assert.Single(frames);
        Assert.All(frames, f => Assert.True(f.Length > 0));
    }
}
```

> **This test is intentionally thin** — building a genuine multi-frame GIF fixture by hand is a large amount of binary-format code disproportionate to this task. The single-frame case proves the decode/re-encode pipeline works end-to-end; Step 4's manual check against a real animated PokeAPI sprite is this task's actual multi-frame proof.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter GifFrameDecoderTests`
Expected: FAIL — `GifFrameDecoder` doesn't exist.

- [ ] **Step 3: Write `GifFrameDecoder.cs`**

Add `System.Drawing.Common` to `src/PokeCorner.Core/PokeCorner.Core.csproj` (add an `<ItemGroup>` if none exists yet — currently there's one for `EmbeddedResource`, add this as a second one or into the same group):

```xml
<ItemGroup>
  <PackageReference Include="System.Drawing.Common" Version="8.0.10" />
</ItemGroup>
```

```csharp
// src/PokeCorner.Core/PokeApi/GifFrameDecoder.cs
using System.Drawing;
using System.Drawing.Imaging;

namespace PokeCorner.Core.PokeApi;

public static class GifFrameDecoder
{
    public static IReadOnlyList<byte[]> DecodeFramesAsPng(byte[] gifBytes)
    {
        using var sourceStream = new MemoryStream(gifBytes);
        using var image = Image.FromStream(sourceStream);
        var dimension = new FrameDimension(image.FrameDimensionsList[0]);
        var frameCount = image.GetFrameCount(dimension);

        var frames = new List<byte[]>(frameCount);
        for (var i = 0; i < frameCount; i++)
        {
            image.SelectActiveFrame(dimension, i);
            using var frameStream = new MemoryStream();
            using var frameBitmap = new Bitmap(image);
            frameBitmap.Save(frameStream, ImageFormat.Png);
            frames.Add(frameStream.ToArray());
        }
        return frames;
    }
}
```

- [ ] **Step 4: Add `FetchAnimatedSpriteAsync` and manually verify against a real sprite**

```csharp
// Add inside PokeApiClient:
public async Task<byte[]?> FetchAnimatedSpriteAsync(int speciesId)
{
    var json = await _httpClient.GetStringAsync($"pokemon/{speciesId}/");
    using var doc = JsonDocument.Parse(json);
    var animatedUrl = doc.RootElement
        .GetProperty("sprites").GetProperty("versions")
        .GetProperty("generation-v").GetProperty("black-white")
        .GetProperty("animated").GetProperty("front_default");

    if (animatedUrl.ValueKind == JsonValueKind.Null) return null;

    return await _httpClient.GetByteArrayAsync(animatedUrl.GetString());
}
```

Run a throwaway manual check (scratch console snippet or REPL — not a permanent test):
```csharp
var client = new PokeApiClient(new HttpClientHandler());
var gif = await client.FetchAnimatedSpriteAsync(25); // Pikachu — known to have an animated sprite
var frames = GifFrameDecoder.DecodeFramesAsPng(gif!);
Console.WriteLine(frames.Count); // expect > 1
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter GifFrameDecoderTests`
Expected: PASS, 1 test green.

- [ ] **Step 6: Commit**

```bash
git add src/PokeCorner.Core/PokeApi/GifFrameDecoder.cs src/PokeCorner.Core/PokeApi/PokeApiClient.cs src/PokeCorner.Core/PokeCorner.Core.csproj tests/PokeCorner.Core.Tests/PokeApi/GifFrameDecoderTests.cs
git commit -m "Add animated Gen-V sprite decoding via GifFrameDecoder"
```

---

## Task 9: `PendingEgg` — timed egg incubation

**Files:**
- Create: `src/PokeCorner.Core/Domain/PendingEgg.cs`
- Create: `src/PokeCorner.Core/Domain/EggIncubationService.cs`
- Modify: `src/PokeCorner.Core/Persistence/SaveData.cs`
- Modify: `src/PokeCorner.Core/AppState.cs`
- Test: `tests/PokeCorner.Core.Tests/Domain/EggIncubationServiceTests.cs`
- Test: `tests/PokeCorner.Core.Tests/AppStateTests.cs`

**Interfaces:**
- Consumes: `RarityTier` (Task 4).
- Produces: `class PendingEgg { Guid Id; RarityTier Tier; DateTimeOffset ReadyAtUtc; }`, `class EggIncubationService { static TimeSpan HatchDuration; PendingEgg StartIncubating(RarityTier, DateTimeOffset); bool IsReady(PendingEgg, DateTimeOffset); }`, `AppState.PendingEggs : List<PendingEgg>` — Task 14 (shop egg purchase) creates these, Task 13 (Catch-log hatch button) hatches them.

Per the latest revision: buying an egg no longer catches instantly — it starts a 1-hour incubation. The egg appears in the Collection's Catch log as a pending card; once ready, a "Вылупить!" button there triggers the actual catch roll (Task 13).

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/PokeCorner.Core.Tests/Domain/EggIncubationServiceTests.cs
using PokeCorner.Core.Domain;
using Xunit;

namespace PokeCorner.Core.Tests.Domain;

public class EggIncubationServiceTests
{
    [Fact]
    public void StartIncubating_SetsReadyAtUtcOneHourLater()
    {
        var service = new EggIncubationService();
        var now = DateTimeOffset.UtcNow;

        var egg = service.StartIncubating(RarityTier.Rare, now);

        Assert.Equal(RarityTier.Rare, egg.Tier);
        Assert.Equal(now + TimeSpan.FromHours(1), egg.ReadyAtUtc);
    }

    [Fact]
    public void IsReady_BeforeReadyTime_ReturnsFalse()
    {
        var service = new EggIncubationService();
        var now = DateTimeOffset.UtcNow;
        var egg = service.StartIncubating(RarityTier.Common, now);

        Assert.False(service.IsReady(egg, now + TimeSpan.FromMinutes(59)));
    }

    [Fact]
    public void IsReady_AtOrAfterReadyTime_ReturnsTrue()
    {
        var service = new EggIncubationService();
        var now = DateTimeOffset.UtcNow;
        var egg = service.StartIncubating(RarityTier.Common, now);

        Assert.True(service.IsReady(egg, now + TimeSpan.FromHours(1)));
    }
}
```

Add to `tests/PokeCorner.Core.Tests/AppStateTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter "EggIncubationServiceTests|AppStateTests"`
Expected: FAIL to compile — `PendingEgg`/`EggIncubationService`/`AppState.PendingEggs`/`SaveData.PendingEggs` don't exist.

- [ ] **Step 3: Write `PendingEgg.cs` and `EggIncubationService.cs`**

```csharp
// src/PokeCorner.Core/Domain/PendingEgg.cs
namespace PokeCorner.Core.Domain;

public sealed class PendingEgg
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required RarityTier Tier { get; init; }
    public required DateTimeOffset ReadyAtUtc { get; init; }
}
```

```csharp
// src/PokeCorner.Core/Domain/EggIncubationService.cs
namespace PokeCorner.Core.Domain;

public sealed class EggIncubationService
{
    public static readonly TimeSpan HatchDuration = TimeSpan.FromHours(1);

    public PendingEgg StartIncubating(RarityTier tier, DateTimeOffset nowUtc) =>
        new() { Tier = tier, ReadyAtUtc = nowUtc + HatchDuration };

    public bool IsReady(PendingEgg egg, DateTimeOffset nowUtc) => nowUtc >= egg.ReadyAtUtc;
}
```

- [ ] **Step 4: Add `SaveData.PendingEggs` and `AppState.PendingEggs`**

In `src/PokeCorner.Core/Persistence/SaveData.cs`, add alongside `CareStates`:
```csharp
    public List<PendingEgg> PendingEggs { get; init; } = new();
```

In `src/PokeCorner.Core/AppState.cs`, add the property:
```csharp
    public List<PendingEgg> PendingEggs { get; } = new();
```

And in `Hydrate`, add:
```csharp
        PendingEggs.AddRange(data.PendingEggs);
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/PokeCorner.Core.Tests`
Expected: PASS, all tests green.

- [ ] **Step 6: Commit**

```bash
git add src/PokeCorner.Core/Domain/PendingEgg.cs src/PokeCorner.Core/Domain/EggIncubationService.cs src/PokeCorner.Core/Persistence/SaveData.cs src/PokeCorner.Core/AppState.cs tests/PokeCorner.Core.Tests/Domain/EggIncubationServiceTests.cs tests/PokeCorner.Core.Tests/AppStateTests.cs
git commit -m "Add PendingEgg/EggIncubationService: eggs now take 1 hour to hatch"
```

---

## Task 10: `AppMenuWindow` (single tabbed menu) + double-click-to-open + Pet/Feed wiring

**Files:**
- Create: `src/PokeCorner.App/Popup/AppMenuWindow.xaml`
- Create: `src/PokeCorner.App/Popup/AppMenuWindow.xaml.cs`
- Modify: `src/PokeCorner.App/App.xaml.cs`
- Modify: `src/PokeCorner.App/Overlay/OverlayWindow.xaml.cs`
- Modify: `src/PokeCorner.App/Popup/CollectionView.xaml`
- Modify: `src/PokeCorner.App/Popup/CollectionView.xaml.cs`
- Modify: `src/PokeCorner.App/Popup/SettingsView.xaml`
- Modify: `src/PokeCorner.App/Popup/SettingsView.xaml.cs`

**Interfaces:**
- Consumes: `FeedingService.Pet`/`Feed` (Task 3), `AppState.RecomputeStage` (Task 3), `ShopService` (Task 4).
- Produces: `AppMenuWindow(CollectionView, SettingsView)`, `AppMenuWindow.SelectTab(int)`, `OverlayWindow.MenuRequested : event Action?`, `App.PetActiveIndividual()`/`FeedActiveIndividual()`, `App.OpenMenu(int tabIndex)` — every later App-layer task that adds a tab (Shop: Task 14, Catch: Task 15) extends `AppMenuWindow`'s constructor and this same `OpenMenu` method rather than introducing new window-hosting.

Per the latest revision: instead of three separate popup `Window`s (Collection/Settings/Shop), there is **one** `AppMenuWindow` with a `TabControl`. Tray menu items and a double-click on the desktop sprite all funnel into the same `OpenMenu(tabIndex)` — if the window is already open, it just switches tabs and activates; otherwise it's constructed fresh. This task builds it with two tabs (Коллекция=0, Настройки=1); Tasks 15/16 each add one more tab.

This task makes the whole solution build again (Task 3 left it non-compiling by design, since `FeedingMode` was removed from Core). No new automated tests — `App.xaml.cs`'s private methods have no existing xUnit coverage; correctness is verified by Step 5's build and later manual smoke tests.

- [ ] **Step 1: Add double-click detection to `OverlayWindow`**

In `src/PokeCorner.App/Overlay/OverlayWindow.xaml.cs`, add `public event Action? MenuRequested;` alongside `PetClicked`, and change the start of `SpriteImage_MouseLeftButtonDown`:

```csharp
    private void SpriteImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MenuRequested?.Invoke();
            return;
        }

        _mouseDownPosition = e.GetPosition(this);
        _isDragging = false;
        SpriteImage.CaptureMouse();
    }
```

(Everything else in the file is unchanged — `ClickCount` is reliable at `MouseLeftButtonDown` time, unlike the click-vs-drag distinction below it, which is why this check can sit safely at the very top before any of that logic runs.)

- [ ] **Step 2: Write `AppMenuWindow.xaml`/`.xaml.cs`**

```xaml
<!-- src/PokeCorner.App/Popup/AppMenuWindow.xaml -->
<Window x:Class="PokeCorner.App.Popup.AppMenuWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="PokéCorner" Width="480" Height="540"
        WindowStartupLocation="CenterScreen">
    <TabControl x:Name="MenuTabControl">
        <TabItem x:Name="CollectionTab" Header="Коллекция" />
        <TabItem x:Name="SettingsTab" Header="Настройки" />
    </TabControl>
</Window>
```

```csharp
// src/PokeCorner.App/Popup/AppMenuWindow.xaml.cs
using System.Windows;

namespace PokeCorner.App.Popup;

public partial class AppMenuWindow : Window
{
    public AppMenuWindow(CollectionView collectionView, SettingsView settingsView)
    {
        InitializeComponent();
        CollectionTab.Content = collectionView;
        SettingsTab.Content = settingsView;
    }

    public void SelectTab(int index)
    {
        if (index >= 0 && index < MenuTabControl.Items.Count)
        {
            MenuTabControl.SelectedIndex = index;
        }
    }
}
```

- [ ] **Step 3: Replace `TryFeedActiveIndividual` with `PetActiveIndividual`/`FeedActiveIndividual`, and the two separate window-openers with one `OpenMenu`**

In `src/PokeCorner.App/App.xaml.cs`:

Replace the `_collectionWindow`/`_settingsWindow` fields with:
```csharp
    private AppMenuWindow? _menuWindow;
    private ShopService _shopService = null!;
    private const int CollectionTabIndex = 0;
    private const int SettingsTabIndex = 1;
```

In `OnStartup`, add `_shopService = new ShopService();` alongside `_feedingService = new FeedingService();`, change `_overlayWindow.PetClicked += TryFeedActiveIndividual;` to `_overlayWindow.PetClicked += PetActiveIndividual;`, add `_overlayWindow.MenuRequested += () => OpenMenu(CollectionTabIndex);`, and change the tray subscriptions:
```csharp
        _trayIconService.OpenCollectionRequested += () => OpenMenu(CollectionTabIndex);
        _trayIconService.OpenSettingsRequested += () => OpenMenu(SettingsTabIndex);
```

In `OnGrowthTick`, add coin earning alongside the existing treat earning:
```csharp
        if (units > 0)
        {
            _appState.ApplyActivityUnits(units, DateTimeOffset.UtcNow);
            _feedingService.EarnTreatsFromActivity(_appState.Feeding, units);
            _shopService.EarnCoinsFromActivity(_appState.Shop, units);
        }
```

Replace `TryFeedActiveIndividual` with:
```csharp
    private void PetActiveIndividual()
    {
        var active = _appState.Collection.GetActive();
        if (active is null) return;

        var careState = _appState.GetOrCreateCareState(active.Id);
        _feedingService.Pet(active, careState, DateTimeOffset.UtcNow);
        _appState.RecomputeStage(active);
        _trayIconService.ShowNotification("PokéCorner", "Погладил!");
    }

    private void FeedActiveIndividual()
    {
        var active = _appState.Collection.GetActive();
        if (active is null) return;

        var careState = _appState.GetOrCreateCareState(active.Id);
        var fed = _feedingService.Feed(active, _appState.Feeding, careState, DateTimeOffset.UtcNow);
        if (fed)
        {
            _appState.RecomputeStage(active);
            _trayIconService.ShowNotification("PokéCorner", "Покормил!");
        }
        else
        {
            _trayIconService.ShowNotification("PokéCorner", "Не хватает лакомств — накопи ещё активности.");
        }
    }
```

Replace `OpenCollectionWindow` and `OpenSettingsWindow` entirely with:
```csharp
    private void OpenMenu(int tabIndex)
    {
        try
        {
            if (_menuWindow is null)
            {
                var collectionVm = new CollectionViewModel(_appState.Collection, _appState.CareStateByIndividualId, _careModeService, _appState.Settings.CareMode);
                var collectionView = new CollectionView(collectionVm, _appState, PetActiveIndividual, FeedActiveIndividual, RefreshOverlayForActiveIndividual);
                var settingsView = new SettingsView(new SettingsViewModel(_appState.Settings));
                _menuWindow = new AppMenuWindow(collectionView, settingsView);
                _menuWindow.Closed += (_, _) => _menuWindow = null;
                _menuWindow.Show();
            }
            _menuWindow.SelectTab(tabIndex);
            _menuWindow.Activate();
            ReassertOverlayTopmost();
        }
        catch (Exception ex)
        {
            _trayIconService.ShowNotification("PokéCorner — ошибка", ex.Message);
        }
    }
```

(`CollectionViewModel`'s constructor keeps its current 4-argument form here — Task 14 extends it with a chain-list argument and updates this exact call site again.)

Update `PersistState()` to write the Shop and pending-egg fields alongside the existing ones:
```csharp
        var data = new SaveData
        {
            Individuals = _appState.Collection.Individuals.ToList(),
            ActiveIndividualId = _appState.Collection.ActiveIndividualId,
            Settings = _appState.Settings,
            FeedingTreats = _appState.Feeding.Treats,
            ShopCoins = _appState.Shop.Coins,
            ShopRemainderUnits = _appState.Shop.RemainderUnits,
            ShopCatchLuckyCharmActive = _appState.Shop.CatchLuckyCharmActive,
            PendingEggs = _appState.PendingEggs,
            CareStates = new Dictionary<Guid, CareState>(_appState.CareStateByIndividualId)
        };
```

- [ ] **Step 4: Split the Pet/Feed buttons in `CollectionView`**

In `src/PokeCorner.App/Popup/CollectionView.xaml`, replace the single `FeedButton` in the top `DockPanel`:

```xaml
        <DockPanel Margin="8">
            <StackPanel DockPanel.Dock="Right" Orientation="Horizontal">
                <Button x:Name="PetButton" Content="Погладить" Click="PetButton_Click" Margin="0,0,4,0"
                        ToolTip="Бесплатно, всегда доступно. Тот же эффект, что и клик по питомцу на рабочем столе." />
                <Button x:Name="FeedButton" Content="Покормить" Click="FeedButton_Click"
                        ToolTip="Требует одно лакомство, даёт больше очков роста, чем поглаживание." />
            </StackPanel>
            <StackPanel>
                <TextBlock x:Name="CarePointsText" FontWeight="Bold" VerticalAlignment="Center" />
                <TextBlock Text="Care Points — очки роста активного питомца, растут от активности за ПК" FontSize="10" Foreground="Gray" />
                <TextBlock x:Name="TreatsText" FontSize="10" Foreground="Gray" />
            </StackPanel>
        </DockPanel>
```

In `src/PokeCorner.App/Popup/CollectionView.xaml.cs`, extend the constructor and add `PetButton_Click`:

```csharp
public partial class CollectionView : System.Windows.Controls.UserControl
{
    private readonly CollectionViewModel _viewModel;
    private readonly AppState _appState;
    private readonly Action _onPetRequested;
    private readonly Action _onFeedRequested;
    private readonly Action _onStateChanged;

    public CollectionView(CollectionViewModel viewModel, AppState appState, Action onPetRequested, Action onFeedRequested, Action onStateChanged)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _appState = appState;
        _onPetRequested = onPetRequested;
        _onFeedRequested = onFeedRequested;
        _onStateChanged = onStateChanged;
        Refresh();
    }

    private void PetButton_Click(object sender, RoutedEventArgs e)
    {
        _onPetRequested();
        _onStateChanged();
        Refresh();
    }

    private void FeedButton_Click(object sender, RoutedEventArgs e)
    {
        _onFeedRequested();
        _onStateChanged();
        Refresh();
    }
```

Update `Refresh()` — treats are now always shown (Feed is always treat-gated regardless of any mode, since `FeedingMode` no longer exists):

```csharp
    public void Refresh()
    {
        var active = _appState.Collection.GetActive();
        CarePointsText.Text = active is null ? "Нет активного питомца" : $"Care Points: {active.AccumulatedCarePoints}";
        EntriesList.ItemsSource = _viewModel.GetEntries(DateTimeOffset.UtcNow);

        var progressToNextTreat = _appState.Feeding.RemainderUnits;
        TreatsText.Text = $"Лакомства: {_appState.Feeding.Treats} (ещё {FeedingService.ActivityUnitsPerTreatEarned - progressToNextTreat} активности до следующего)";
    }
```

(Keep `SelectButton_Click`/`ReleaseButton_Click` exactly as they already are.)

- [ ] **Step 5: Remove `FeedingMode` from Settings UI**

In `src/PokeCorner.App/Popup/SettingsView.xaml`, delete the entire "Режим кормления" `TextBlock`/`ComboBox`/`TextBlock` block (the `FeedingModeCombo` and its two neighboring `TextBlock`s).

In `src/PokeCorner.App/Popup/SettingsView.xaml.cs`, remove the `FeedingModeCombo.SelectedIndex = ...` line from `LoadFromSettings` and delete the entire `FeedingModeCombo_SelectionChanged` method.

- [ ] **Step 6: Build and run the full test suite**

Run: `dotnet build PokeCorner.sln` — expect success (this is the first App-layer build since Task 3 broke it).
Run: `dotnet test` — expect all tests (Core + App) green.

- [ ] **Step 7: Manual verification**

Launch the app. Double-click the desktop sprite — confirm one window opens showing the Коллекция tab. Click "Настройки" in the tray menu while that window is still open — confirm it switches tabs in the *same* window rather than opening a second one. Click "Погладить"/"Покормить" — confirm they behave differently (feed requires a treat, pet is always free).

- [ ] **Step 8: Commit**

```bash
git add src/PokeCorner.App/Popup/AppMenuWindow.xaml src/PokeCorner.App/Popup/AppMenuWindow.xaml.cs src/PokeCorner.App/App.xaml.cs src/PokeCorner.App/Overlay/OverlayWindow.xaml.cs src/PokeCorner.App/Popup/CollectionView.xaml src/PokeCorner.App/Popup/CollectionView.xaml.cs src/PokeCorner.App/Popup/SettingsView.xaml src/PokeCorner.App/Popup/SettingsView.xaml.cs
git commit -m "Consolidate Collection/Settings into one tabbed AppMenuWindow; add double-click-to-open; wire Pet/Feed split"
```

---

## Task 11: `ProgressCalculator`, live progress bars, and the active pet's sprite in the Catch log

**Files:**
- Create: `src/PokeCorner.App/Popup/ProgressCalculator.cs`
- Modify: `src/PokeCorner.App/Popup/CollectionView.xaml`
- Modify: `src/PokeCorner.App/Popup/CollectionView.xaml.cs`
- Modify: `src/PokeCorner.App/App.xaml.cs`
- Test: `tests/PokeCorner.App.Tests/Popup/ProgressCalculatorTests.cs`

**Interfaces:**
- Consumes: `ActivityAccumulator.UnitsSinceLastCatchCheck`/`CatchCheckIntervalUnits` (Task 2), `EvolutionThresholds` (Task 1), `FeedingInventory.RemainderUnits`/`FeedingService.ActivityUnitsPerTreatEarned` (existing).
- Produces: `readonly record struct ProgressInfo(double CatchFraction, double EvolutionFraction, double TreatFraction)`, `static class ProgressCalculator { ProgressInfo Compute(ActivityAccumulator, PokemonIndividual?, FeedingInventory); }`, `App._activeSpriteBytes : byte[]?` (kept in sync wherever the overlay's sprite is decoded) — Task 12 (floating panel) reuses `ProgressCalculator`; Task 15 (Catch tab reveal) reuses the sprite-bytes pattern.

- [ ] **Step 1: Write the failing `ProgressCalculator` tests**

```csharp
// tests/PokeCorner.App.Tests/Popup/ProgressCalculatorTests.cs
using PokeCorner.App.Popup;
using PokeCorner.Core.Activity;
using PokeCorner.Core.Domain;
using Xunit;

namespace PokeCorner.App.Tests.Popup;

public class ProgressCalculatorTests
{
    private static PokemonIndividual MakeIndividual(int carePoints, int chainLength = 3) => new()
    {
        SpeciesId = 1,
        EvolutionChainId = 1,
        ChainLength = chainLength,
        Nature = Nature.Hardy,
        IsShiny = false,
        CaughtAtUtc = DateTimeOffset.UtcNow,
        AccumulatedCarePoints = carePoints
    };

    [Fact]
    public void Compute_CatchFraction_ReflectsUnitsSinceLastCheck()
    {
        var activity = new ActivityAccumulator();
        activity.AddUnits(ActivityAccumulator.CatchCheckIntervalUnits / 2);

        var result = ProgressCalculator.Compute(activity, active: null, new FeedingInventory());

        Assert.Equal(0.5, result.CatchFraction, precision: 3);
    }

    [Fact]
    public void Compute_EvolutionFraction_NoActiveIndividual_IsZero()
    {
        var result = ProgressCalculator.Compute(new ActivityAccumulator(), active: null, new FeedingInventory());

        Assert.Equal(0.0, result.EvolutionFraction);
    }

    [Fact]
    public void Compute_EvolutionFraction_Stage1_ReflectsProgressTowardStage2Threshold()
    {
        var individual = MakeIndividual(EvolutionThresholds.Stage2Threshold / 2);

        var result = ProgressCalculator.Compute(new ActivityAccumulator(), individual, new FeedingInventory());

        Assert.Equal(0.5, result.EvolutionFraction, precision: 3);
    }

    [Fact]
    public void Compute_EvolutionFraction_Stage2_ReflectsProgressBetweenStage2AndStage3Thresholds()
    {
        var halfway = EvolutionThresholds.Stage2Threshold + (EvolutionThresholds.Stage3Threshold - EvolutionThresholds.Stage2Threshold) / 2;
        var individual = MakeIndividual(halfway);
        individual.CurrentStage = EvolutionStage.Stage2;

        var result = ProgressCalculator.Compute(new ActivityAccumulator(), individual, new FeedingInventory());

        Assert.Equal(0.5, result.EvolutionFraction, precision: 3);
    }

    [Fact]
    public void Compute_EvolutionFraction_FinalStage_IsFull()
    {
        var individual = MakeIndividual(EvolutionThresholds.Stage3Threshold, chainLength: 3);
        individual.CurrentStage = EvolutionStage.Stage3;

        var result = ProgressCalculator.Compute(new ActivityAccumulator(), individual, new FeedingInventory());

        Assert.Equal(1.0, result.EvolutionFraction);
    }

    [Fact]
    public void Compute_EvolutionFraction_SingleStageChain_IsAlwaysFull()
    {
        var individual = MakeIndividual(0, chainLength: 1);

        var result = ProgressCalculator.Compute(new ActivityAccumulator(), individual, new FeedingInventory());

        Assert.Equal(1.0, result.EvolutionFraction);
    }

    [Fact]
    public void Compute_TreatFraction_ReflectsRemainderUnits()
    {
        var feeding = new FeedingInventory { RemainderUnits = FeedingService.ActivityUnitsPerTreatEarned / 4 };

        var result = ProgressCalculator.Compute(new ActivityAccumulator(), active: null, feeding);

        Assert.Equal(0.25, result.TreatFraction, precision: 3);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/PokeCorner.App.Tests --filter ProgressCalculatorTests`
Expected: FAIL — `ProgressCalculator`/`ProgressInfo` don't exist.

- [ ] **Step 3: Write `ProgressCalculator.cs`**

```csharp
// src/PokeCorner.App/Popup/ProgressCalculator.cs
using PokeCorner.Core.Activity;
using PokeCorner.Core.Domain;

namespace PokeCorner.App.Popup;

public readonly record struct ProgressInfo(double CatchFraction, double EvolutionFraction, double TreatFraction);

public static class ProgressCalculator
{
    public static ProgressInfo Compute(ActivityAccumulator activity, PokemonIndividual? active, FeedingInventory feeding)
    {
        var catchFraction = Math.Clamp((double)activity.UnitsSinceLastCatchCheck / ActivityAccumulator.CatchCheckIntervalUnits, 0.0, 1.0);
        var treatFraction = Math.Clamp((double)feeding.RemainderUnits / FeedingService.ActivityUnitsPerTreatEarned, 0.0, 1.0);
        var evolutionFraction = active is null ? 0.0 : ComputeEvolutionFraction(active);

        return new ProgressInfo(catchFraction, evolutionFraction, treatFraction);
    }

    private static double ComputeEvolutionFraction(PokemonIndividual individual)
    {
        if (individual.CurrentStage == EvolutionStage.Stage1 && individual.ChainLength >= 2)
        {
            return Math.Clamp((double)individual.AccumulatedCarePoints / EvolutionThresholds.Stage2Threshold, 0.0, 1.0);
        }
        if (individual.CurrentStage == EvolutionStage.Stage2 && individual.ChainLength >= 3)
        {
            var span = EvolutionThresholds.Stage3Threshold - EvolutionThresholds.Stage2Threshold;
            var progress = individual.AccumulatedCarePoints - EvolutionThresholds.Stage2Threshold;
            return Math.Clamp((double)progress / span, 0.0, 1.0);
        }
        // Final stage for this chain length — nowhere further to progress.
        return 1.0;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/PokeCorner.App.Tests --filter ProgressCalculatorTests`
Expected: PASS, 7 tests green.

- [ ] **Step 5: Add progress bars and the active pet's sprite to `CollectionView`**

In `src/PokeCorner.App/Popup/CollectionView.xaml`, add an `Image` next to the Care Points text and three `ProgressBar`s below the top `DockPanel`:

```xaml
        <DockPanel Margin="8">
            <StackPanel DockPanel.Dock="Right" Orientation="Horizontal">
                <Button x:Name="PetButton" Content="Погладить" Click="PetButton_Click" Margin="0,0,4,0"
                        ToolTip="Бесплатно, всегда доступно. Тот же эффект, что и клик по питомцу на рабочем столе." />
                <Button x:Name="FeedButton" Content="Покормить" Click="FeedButton_Click"
                        ToolTip="Требует одно лакомство, даёт больше очков роста, чем поглаживание." />
            </StackPanel>
            <Image x:Name="ActiveSpriteImage" Width="48" Height="48" Margin="0,0,8,0"
                   RenderOptions.BitmapScalingMode="NearestNeighbor" DockPanel.Dock="Left" />
            <StackPanel>
                <TextBlock x:Name="CarePointsText" FontWeight="Bold" VerticalAlignment="Center" />
                <TextBlock Text="Care Points — очки роста активного питомца, растут от активности за ПК" FontSize="10" Foreground="Gray" />
                <TextBlock x:Name="TreatsText" FontSize="10" Foreground="Gray" />
            </StackPanel>
        </DockPanel>

        <StackPanel Margin="8,0,8,4">
            <TextBlock Text="До поимки" FontSize="10" Foreground="Gray" />
            <ProgressBar x:Name="CatchProgressBar" Minimum="0" Maximum="1" Height="8" Margin="0,0,0,4" />
            <TextBlock Text="До эволюции" FontSize="10" Foreground="Gray" />
            <ProgressBar x:Name="EvolutionProgressBar" Minimum="0" Maximum="1" Height="8" Margin="0,0,0,4" />
            <TextBlock Text="До лакомства" FontSize="10" Foreground="Gray" />
            <ProgressBar x:Name="TreatProgressBar" Minimum="0" Maximum="1" Height="8" />
        </StackPanel>
```

In `src/PokeCorner.App/Popup/CollectionView.xaml.cs`, add a sprite-source delegate parameter, a shared static PNG-decode helper, and a 1-second live-refresh `DispatcherTimer` started/stopped on `Loaded`/`Unloaded`:

```csharp
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PokeCorner.Core;
using PokeCorner.Core.Domain;

namespace PokeCorner.App.Popup;

public partial class CollectionView : System.Windows.Controls.UserControl
{
    private readonly CollectionViewModel _viewModel;
    private readonly AppState _appState;
    private readonly Action _onPetRequested;
    private readonly Action _onFeedRequested;
    private readonly Action _onStateChanged;
    private readonly Func<byte[]?> _getActiveSpriteBytes;
    private DispatcherTimer? _liveRefreshTimer;

    public CollectionView(CollectionViewModel viewModel, AppState appState, Action onPetRequested, Action onFeedRequested, Action onStateChanged, Func<byte[]?> getActiveSpriteBytes)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _appState = appState;
        _onPetRequested = onPetRequested;
        _onFeedRequested = onFeedRequested;
        _onStateChanged = onStateChanged;
        _getActiveSpriteBytes = getActiveSpriteBytes;
        Loaded += (_, _) => StartLiveRefresh();
        Unloaded += (_, _) => StopLiveRefresh();
        Refresh();
    }

    private void StartLiveRefresh()
    {
        _liveRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _liveRefreshTimer.Tick += (_, _) => Refresh();
        _liveRefreshTimer.Start();
    }

    private void StopLiveRefresh()
    {
        _liveRefreshTimer?.Stop();
        _liveRefreshTimer = null;
    }

    private void PetButton_Click(object sender, RoutedEventArgs e)
    {
        _onPetRequested();
        _onStateChanged();
        Refresh();
    }

    private void FeedButton_Click(object sender, RoutedEventArgs e)
    {
        _onFeedRequested();
        _onStateChanged();
        Refresh();
    }

    public void Refresh()
    {
        var active = _appState.Collection.GetActive();
        CarePointsText.Text = active is null ? "Нет активного питомца" : $"Care Points: {active.AccumulatedCarePoints}";
        EntriesList.ItemsSource = _viewModel.GetEntries(DateTimeOffset.UtcNow);

        var progressToNextTreat = _appState.Feeding.RemainderUnits;
        TreatsText.Text = $"Лакомства: {_appState.Feeding.Treats} (ещё {FeedingService.ActivityUnitsPerTreatEarned - progressToNextTreat} активности до следующего)";

        var progress = ProgressCalculator.Compute(_appState.Activity, active, _appState.Feeding);
        CatchProgressBar.Value = progress.CatchFraction;
        EvolutionProgressBar.Value = progress.EvolutionFraction;
        TreatProgressBar.Value = progress.TreatFraction;

        var spriteBytes = _getActiveSpriteBytes();
        ActiveSpriteImage.Source = spriteBytes is null ? null : DecodePng(spriteBytes);
    }

    private static System.Windows.Media.ImageSource DecodePng(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    // ... SelectButton_Click / ReleaseButton_Click unchanged from before
}
```

- [ ] **Step 6: Wire the sprite-bytes field and update the `CollectionView` construction call site in `App.xaml.cs`**

Add a field `private byte[]? _activeSpriteBytes;`. In `RefreshOverlayForActiveIndividual`, inside the existing `if (spriteBytes is not null) { _overlayWindow.SetSpriteFrames(...); }` block, add: `_activeSpriteBytes = spriteBytes;`.

In `OpenMenu`, update the `CollectionView` construction call to pass the new delegate:
```csharp
                var collectionView = new CollectionView(collectionVm, _appState, PetActiveIndividual, FeedActiveIndividual, RefreshOverlayForActiveIndividual, () => _activeSpriteBytes);
```

- [ ] **Step 7: Build and run the full test suite**

Run: `dotnet build PokeCorner.sln && dotnet test`
Expected: build succeeds, all tests green.

- [ ] **Step 8: Manual verification**

Launch the app, open the menu window (double-click the sprite), stay on Коллекция — confirm the active pet's sprite is visible next to Care Points, and click "Погладить" on the desktop sprite while the popup stays open — confirm the catch/evolution/treat bars visibly move within ~1 second without needing to close/reopen the window.

- [ ] **Step 9: Commit**

```bash
git add src/PokeCorner.App/Popup/ProgressCalculator.cs src/PokeCorner.App/Popup/CollectionView.xaml src/PokeCorner.App/Popup/CollectionView.xaml.cs src/PokeCorner.App/App.xaml.cs tests/PokeCorner.App.Tests/Popup/ProgressCalculatorTests.cs
git commit -m "Add live-refreshing progress bars and the active pet's sprite to CollectionView"
```

---

## Task 12: Floating progress panel + Settings toggle

**Files:**
- Create: `src/PokeCorner.App/Overlay/ProgressPanelWindow.xaml`
- Create: `src/PokeCorner.App/Overlay/ProgressPanelWindow.xaml.cs`
- Modify: `src/PokeCorner.App/App.xaml.cs`
- Modify: `src/PokeCorner.App/Popup/SettingsView.xaml`
- Modify: `src/PokeCorner.App/Popup/SettingsView.xaml.cs`

**Interfaces:**
- Consumes: `ProgressCalculator.Compute`/`ProgressInfo` (Task 11), `AppSettings.ShowProgressPanel` (Task 3).
- Produces: `ProgressPanelWindow.UpdateProgress(ProgressInfo)`, `ProgressPanelWindow.PositionBelow(OverlayWindow)`.

- [ ] **Step 1: Write `ProgressPanelWindow.xaml`**

```xaml
<Window x:Class="PokeCorner.App.Overlay.ProgressPanelWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="#CC1A1F3C"
        ShowInTaskbar="False"
        Topmost="True"
        ResizeMode="NoResize"
        SizeToContent="WidthAndHeight">
    <StackPanel Margin="6" Width="90">
        <ProgressBar x:Name="CatchBar" Minimum="0" Maximum="1" Height="5" Margin="0,0,0,2" Foreground="#FFD166" Background="#252B4A" BorderThickness="0" />
        <ProgressBar x:Name="EvolutionBar" Minimum="0" Maximum="1" Height="5" Margin="0,0,0,2" Foreground="#4C8DF6" Background="#252B4A" BorderThickness="0" />
        <ProgressBar x:Name="TreatBar" Minimum="0" Maximum="1" Height="5" Foreground="#52C97A" Background="#252B4A" BorderThickness="0" />
    </StackPanel>
</Window>
```

- [ ] **Step 2: Write `ProgressPanelWindow.xaml.cs`**

```csharp
using PokeCorner.App.Popup;

namespace PokeCorner.App.Overlay;

public partial class ProgressPanelWindow : System.Windows.Window
{
    public ProgressPanelWindow()
    {
        InitializeComponent();
    }

    public void UpdateProgress(ProgressInfo progress)
    {
        CatchBar.Value = progress.CatchFraction;
        EvolutionBar.Value = progress.EvolutionFraction;
        TreatBar.Value = progress.TreatFraction;
    }

    /// Positions the panel directly under the overlay sprite, horizontally centered on it —
    /// called whenever the overlay itself is repositioned (corner/monitor/size settings change).
    public void PositionBelow(OverlayWindow overlay)
    {
        Width = 100;
        Left = overlay.Left + (overlay.Width - Width) / 2;
        Top = overlay.Top + overlay.Height + 4;
    }
}
```

- [ ] **Step 3: Wire it into `App.xaml.cs`'s settings reconciliation**

Add a field: `private ProgressPanelWindow? _progressPanelWindow;`

In `ReconcileSettingsState()`, add alongside the existing toggle-handling blocks:

```csharp
        if (_appState.Settings.ShowProgressPanel && _progressPanelWindow is null)
        {
            _progressPanelWindow = new ProgressPanelWindow();
            _progressPanelWindow.PositionBelow(_overlayWindow);
            _progressPanelWindow.Show();
        }
        else if (!_appState.Settings.ShowProgressPanel && _progressPanelWindow is not null)
        {
            _progressPanelWindow.Close();
            _progressPanelWindow = null;
        }
```

At the end of that same method (after the existing corner/monitor/size repositioning block), reposition the panel too if it's open:

```csharp
        _progressPanelWindow?.PositionBelow(_overlayWindow);
```

In `OnGrowthTick`, after the existing `_feedingService.EarnTreatsFromActivity`/`_shopService.EarnCoinsFromActivity` calls, update the panel if it's showing:

```csharp
        _progressPanelWindow?.UpdateProgress(ProgressCalculator.Compute(_appState.Activity, _appState.Collection.GetActive(), _appState.Feeding));
```

(Add `using PokeCorner.App.Popup;` to `App.xaml.cs` for `ProgressCalculator`/`ProgressInfo`.)

- [ ] **Step 4: Add the Settings checkbox**

In `src/PokeCorner.App/Popup/SettingsView.xaml`, add after the `PetSizeSlider`:

```xaml
        <CheckBox x:Name="ShowProgressPanelCheck" Content="Показывать прогресс рядом с питомцем" Margin="0,10,0,0" Checked="ShowProgressPanelCheck_Changed" Unchecked="ShowProgressPanelCheck_Changed" />
        <TextBlock Text="Компактная панель под питомцем на рабочем столе с теми же тремя барами, что и в коллекции." FontSize="10" Foreground="Gray" TextWrapping="Wrap" Margin="0,2,0,0" />
```

In `src/PokeCorner.App/Popup/SettingsView.xaml.cs`, add to `LoadFromSettings`:
```csharp
        ShowProgressPanelCheck.IsChecked = settings.ShowProgressPanel;
```
And a new handler:
```csharp
    private void ShowProgressPanelCheck_Changed(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_isLoadingInitialState) return;
        _viewModel.Settings.ShowProgressPanel = ShowProgressPanelCheck.IsChecked == true;
    }
```

- [ ] **Step 5: Build and run the full test suite**

Run: `dotnet build PokeCorner.sln && dotnet test`
Expected: build succeeds, all tests green.

- [ ] **Step 6: Manual verification**

Launch the app, open Settings, check "Показывать прогресс рядом с питомцем" — confirm a small translucent panel with three bars appears directly under the desktop sprite and updates roughly every 5 seconds. Uncheck it — confirm the panel disappears.

- [ ] **Step 7: Commit**

```bash
git add src/PokeCorner.App/Overlay/ProgressPanelWindow.xaml src/PokeCorner.App/Overlay/ProgressPanelWindow.xaml.cs src/PokeCorner.App/App.xaml.cs src/PokeCorner.App/Popup/SettingsView.xaml src/PokeCorner.App/Popup/SettingsView.xaml.cs
git commit -m "Add optional floating progress panel toggled from Settings"
```

---

## Task 13: Pokédex tab + pending-egg cards (with hatch progress bar) in Catch log

**Files:**
- Modify: `src/PokeCorner.App/Popup/CollectionViewModel.cs`
- Modify: `src/PokeCorner.App/Popup/CollectionView.xaml`
- Modify: `src/PokeCorner.App/Popup/CollectionView.xaml.cs`
- Modify: `src/PokeCorner.App/App.xaml.cs`
- Modify: `tests/PokeCorner.App.Tests/Popup/CollectionViewModelTests.cs`

**Interfaces:**
- Consumes: `RarityClassifier.Classify` (Task 4), `EggIncubationService.HatchDuration` (Task 9), `CatchRoller.RollCatchFromTier` (Task 5).
- Produces: `CollectionEntry` gains `RarityTier Rarity`/`double EvolutionFraction`; new `record PokedexEntry(int SpeciesId, string SpeciesName, RarityTier Rarity)`; new `record PendingEggEntry(Guid EggId, RarityTier Tier, double HatchFraction, bool IsReady)`; `CollectionViewModel.GetPokedexEntries()`/`GetPendingEggEntries(DateTimeOffset)`; `App.HatchEgg(Guid eggId) : bool` — this task's UI is the last consumer of the extended `CollectionViewModel` constructor; Task 18's theme is the last consumer of this task's XAML.

`CollectionViewModel`'s constructor gains two new parameters: the currently-loaded chain catalog (for `RarityClassifier` lookups) and the live pending-eggs list. Every existing `new CollectionViewModel(...)` call site (in `App.xaml.cs` and in tests) must be updated.

- [ ] **Step 1: Write the failing tests**

First read the current `tests/PokeCorner.App.Tests/Popup/CollectionViewModelTests.cs` in full. Add two trailing arguments to every existing `new CollectionViewModel(...)` call in that file: an empty `Array.Empty<SpeciesEvolutionChain>()` (or a small fixture list, if a test specifically needs chain data — none of the *existing* tests do) and an empty `new List<PendingEgg>()`. Then add these new tests:

```csharp
    [Fact]
    public void GetEntries_IncludesRarityFromChainCaptureRate()
    {
        var collection = new PokemonCollection();
        var individual = MakeIndividual(evolutionChainId: 1);
        collection.Add(individual);
        var chains = new[] { new SpeciesEvolutionChain(ChainId: 1, StarterSpeciesId: individual.SpeciesId, CaptureRate: 255, ChainLength: 3, StageSpeciesIds: new[] { individual.SpeciesId }) };
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, chains, new List<PendingEgg>());

        var entries = vm.GetEntries(DateTimeOffset.UtcNow);

        Assert.Equal(RarityTier.Common, entries.Single().Rarity);
    }

    [Fact]
    public void GetPokedexEntries_DedupesBySpeciesIdAcrossIndividuals()
    {
        var collection = new PokemonCollection();
        var chainId = 1;
        var speciesId = 10;
        collection.Add(MakeIndividual(evolutionChainId: chainId, speciesId: speciesId));
        collection.Add(MakeIndividual(evolutionChainId: chainId, speciesId: speciesId));
        var chains = new[] { new SpeciesEvolutionChain(ChainId: chainId, StarterSpeciesId: speciesId, CaptureRate: 100, ChainLength: 3, StageSpeciesIds: new[] { speciesId }) };
        var vm = new CollectionViewModel(collection, new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, chains, new List<PendingEgg>());

        var pokedex = vm.GetPokedexEntries();

        Assert.Single(pokedex);
        Assert.Equal(speciesId, pokedex[0].SpeciesId);
    }

    [Fact]
    public void GetPokedexEntries_NoIndividualsOwnAnySpecies_IsEmpty()
    {
        var vm = new CollectionViewModel(new PokemonCollection(), new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg>());

        Assert.Empty(vm.GetPokedexEntries());
    }

    [Fact]
    public void GetPendingEggEntries_NotYetReady_HatchFractionBetweenZeroAndOneAndNotReady()
    {
        var now = DateTimeOffset.UtcNow;
        var egg = new PendingEgg { Tier = RarityTier.Rare, ReadyAtUtc = now + TimeSpan.FromMinutes(30) }; // half-incubated
        var vm = new CollectionViewModel(new PokemonCollection(), new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg> { egg });

        var entries = vm.GetPendingEggEntries(now);

        Assert.Equal(egg.Id, entries.Single().EggId);
        Assert.False(entries.Single().IsReady);
        Assert.InRange(entries.Single().HatchFraction, 0.45, 0.55);
    }

    [Fact]
    public void GetPendingEggEntries_PastReadyTime_IsReady()
    {
        var now = DateTimeOffset.UtcNow;
        var egg = new PendingEgg { Tier = RarityTier.Common, ReadyAtUtc = now - TimeSpan.FromMinutes(1) };
        var vm = new CollectionViewModel(new PokemonCollection(), new Dictionary<Guid, CareState>(), new CareModeService(), CareMode.Chill, Array.Empty<SpeciesEvolutionChain>(), new List<PendingEgg> { egg });

        var entries = vm.GetPendingEggEntries(now);

        Assert.True(entries.Single().IsReady);
        Assert.Equal(1.0, entries.Single().HatchFraction);
    }
```

(If `MakeIndividual` in that file doesn't already accept optional `evolutionChainId`/`speciesId` parameters, extend its signature to add them, following whatever default-parameter pattern the file already uses elsewhere — don't replace the helper, extend it.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/PokeCorner.App.Tests --filter CollectionViewModelTests`
Expected: FAIL to compile — the constructor's two new parameters and `GetPokedexEntries`/`GetPendingEggEntries`/`Rarity` don't exist yet.

- [ ] **Step 3: Extend `CollectionViewModel.cs`**

```csharp
// src/PokeCorner.App/Popup/CollectionViewModel.cs
using PokeCorner.Core.Domain;

namespace PokeCorner.App.Popup;

public sealed record CollectionEntry(
    Guid IndividualId,
    int SpeciesId,
    string SpeciesName,
    bool IsShiny,
    bool IsActive,
    bool IsAvailableToActivate,
    DateTimeOffset? AvailableAgainAtUtc,
    RarityTier Rarity,
    double EvolutionFraction);

public sealed record PokedexEntry(int SpeciesId, string SpeciesName, RarityTier Rarity);

public sealed record PendingEggEntry(Guid EggId, RarityTier Tier, double HatchFraction, bool IsReady);

public sealed class CollectionViewModel
{
    private readonly PokemonCollection _collection;
    private readonly IDictionary<Guid, CareState> _careStates;
    private readonly CareModeService _careModeService;
    private readonly CareMode _careMode;
    private readonly IReadOnlyList<SpeciesEvolutionChain> _chains;
    private readonly IReadOnlyList<PendingEgg> _pendingEggs;

    public CollectionViewModel(PokemonCollection collection, IDictionary<Guid, CareState> careStates, CareModeService careModeService, CareMode careMode, IReadOnlyList<SpeciesEvolutionChain> chains, IReadOnlyList<PendingEgg> pendingEggs)
    {
        _collection = collection;
        _careStates = careStates;
        _careModeService = careModeService;
        _careMode = careMode;
        _chains = chains;
        _pendingEggs = pendingEggs;
    }

    public IReadOnlyList<CollectionEntry> GetEntries(DateTimeOffset nowUtc) =>
        _collection.Individuals.Select(individual =>
        {
            var careState = _careStates.TryGetValue(individual.Id, out var state) ? state : new CareState();
            var isAvailable = _careMode != CareMode.Hardcore || _careModeService.IsAvailableToPlaceOnDesktop(careState, nowUtc);
            return new CollectionEntry(
                individual.Id,
                individual.SpeciesId,
                SpeciesNames.GetName(individual.SpeciesId),
                individual.IsShiny,
                IsActive: individual.Id == _collection.ActiveIndividualId,
                IsAvailableToActivate: isAvailable,
                AvailableAgainAtUtc: isAvailable ? null : careState.RanAwayUntilUtc,
                Rarity: RarityForChain(individual.EvolutionChainId),
                EvolutionFraction: ProgressCalculator.Compute(new Core.Activity.ActivityAccumulator(), individual, new FeedingInventory()).EvolutionFraction);
        }).ToList();

    public IReadOnlyList<PokedexEntry> GetPokedexEntries() =>
        _collection.Individuals
            .Select(i => i.SpeciesId)
            .Distinct()
            .Select(speciesId =>
            {
                var owningIndividual = _collection.Individuals.First(i => i.SpeciesId == speciesId);
                return new PokedexEntry(speciesId, SpeciesNames.GetName(speciesId), RarityForChain(owningIndividual.EvolutionChainId));
            })
            .ToList();

    public IReadOnlyList<PendingEggEntry> GetPendingEggEntries(DateTimeOffset nowUtc) =>
        _pendingEggs.Select(egg =>
        {
            var startedAtUtc = egg.ReadyAtUtc - EggIncubationService.HatchDuration;
            var elapsedSeconds = (nowUtc - startedAtUtc).TotalSeconds;
            var fraction = Math.Clamp(elapsedSeconds / EggIncubationService.HatchDuration.TotalSeconds, 0.0, 1.0);
            return new PendingEggEntry(egg.Id, egg.Tier, fraction, nowUtc >= egg.ReadyAtUtc);
        }).ToList();

    private RarityTier RarityForChain(int evolutionChainId)
    {
        var chain = _chains.FirstOrDefault(c => c.ChainId == evolutionChainId);
        return chain is null ? RarityTier.Common : RarityClassifier.Classify(chain.CaptureRate);
    }

    public bool TrySetActive(Guid id, DateTimeOffset nowUtc)
    {
        if (!_careStates.TryGetValue(id, out var careState))
        {
            careState = new CareState();
            _careStates[id] = careState;
        }
        var isAvailable = _careMode != CareMode.Hardcore || _careModeService.IsAvailableToPlaceOnDesktop(careState, nowUtc);
        if (!isAvailable) return false;

        _collection.SetActive(id);
        _careModeService.RegisterInteraction(careState, nowUtc);
        return true;
    }

    public bool Release(Guid id) => _collection.Release(id);
}
```

- [ ] **Step 4: Add the Pokédex tab and pending-egg cards to `CollectionView.xaml`**

Wrap the existing content in a `TabControl` with "Catch log" and "Pokédex" tabs, and add a pending-eggs section to the Catch log tab, right after the progress bars added in Task 11:

```xaml
<UserControl x:Class="PokeCorner.App.Popup.CollectionView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="clr-namespace:PokeCorner.App.Popup">
    <UserControl.Resources>
        <BooleanToVisibilityConverter x:Key="BoolToVis" />
        <local:BoolToYesNoConverter x:Key="BoolToYesNo" />
    </UserControl.Resources>
    <TabControl>
        <TabItem Header="Catch log">
            <StackPanel>
                <!-- existing DockPanel (sprite, Care Points, Pet/Feed buttons) and the three
                     progress bars from Task 11 stay exactly where they are, unchanged -->

                <TextBlock Text="Яйца в инкубации" FontWeight="Bold" Margin="8,4,8,0" />
                <ItemsControl x:Name="PendingEggsList" Margin="8,2,8,4">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Border Padding="6" Margin="0,0,0,4" BorderBrush="Gray" BorderThickness="1" CornerRadius="3">
                                <StackPanel>
                                    <TextBlock Text="{Binding Tier}" FontWeight="Bold" />
                                    <ProgressBar Value="{Binding HatchFraction, Mode=OneWay}" Minimum="0" Maximum="1" Height="6" Margin="0,4,0,4" />
                                    <Button Content="Вылупить!" Tag="{Binding EggId}" Click="HatchButton_Click" IsEnabled="{Binding IsReady}" HorizontalAlignment="Left" />
                                </StackPanel>
                            </Border>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>

                <Grid Margin="8,4,8,0">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="140" />
                        <ColumnDefinition Width="70" />
                        <ColumnDefinition Width="90" />
                        <ColumnDefinition Width="*" />
                    </Grid.ColumnDefinitions>
                    <TextBlock Grid.Column="0" Text="Покемон" FontWeight="Bold" />
                    <TextBlock Grid.Column="1" Text="На столе" FontWeight="Bold" />
                    <TextBlock Grid.Column="2" Text="Доступен" FontWeight="Bold" ToolTip="В Hardcore-режиме питомец может временно &quot;обидеться&quot; и стать недоступным" />
                </Grid>

                <ItemsControl x:Name="EntriesList" Margin="8,2,8,8">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Border Padding="6" Margin="0,0,0,4" BorderBrush="Gray" BorderThickness="1" CornerRadius="3">
                                <StackPanel>
                                    <Grid>
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="140" />
                                            <ColumnDefinition Width="70" />
                                            <ColumnDefinition Width="90" />
                                            <ColumnDefinition Width="*" />
                                        </Grid.ColumnDefinitions>
                                        <StackPanel Grid.Column="0" Orientation="Horizontal">
                                            <TextBlock Text="{Binding SpeciesName}" />
                                            <TextBlock Text=" ✨" Visibility="{Binding IsShiny, Converter={StaticResource BoolToVis}}" ToolTip="Шайни — редкая цветовая форма" />
                                        </StackPanel>
                                        <TextBlock Grid.Column="1" Text="{Binding IsActive, Converter={StaticResource BoolToYesNo}}" />
                                        <TextBlock Grid.Column="2" Text="{Binding IsAvailableToActivate, Converter={StaticResource BoolToYesNo}}" />
                                        <StackPanel Grid.Column="3" Orientation="Horizontal" HorizontalAlignment="Right">
                                            <Button Content="Выбрать" Tag="{Binding IndividualId}" Click="SelectButton_Click" Margin="4,0"
                                                    ToolTip="Сделать этого покемона активным — он появится на рабочем столе" />
                                            <Button Content="Отпустить" Tag="{Binding IndividualId}" Click="ReleaseButton_Click"
                                                    ToolTip="Насовсем удалить этого покемона из коллекции" />
                                        </StackPanel>
                                    </Grid>
                                    <ProgressBar Value="{Binding EvolutionFraction, Mode=OneWay}" Minimum="0" Maximum="1" Height="4" Margin="0,4,0,0"
                                                 ToolTip="Прогресс до следующей эволюции" />
                                </StackPanel>
                            </Border>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </StackPanel>
        </TabItem>
        <TabItem Header="Pokédex">
            <StackPanel>
                <TextBlock x:Name="PokedexCountText" FontWeight="Bold" Margin="8" />
                <ItemsControl x:Name="PokedexGrid" Margin="4">
                    <ItemsControl.ItemsPanel>
                        <ItemsPanelTemplate>
                            <WrapPanel />
                        </ItemsPanelTemplate>
                    </ItemsControl.ItemsPanel>
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Border Width="100" Padding="6" Margin="4" BorderBrush="Gray" BorderThickness="1" CornerRadius="3">
                                <StackPanel>
                                    <TextBlock Text="{Binding SpeciesName}" FontWeight="Bold" TextWrapping="Wrap" />
                                    <TextBlock Text="{Binding Rarity}" FontSize="10" Foreground="Gray" />
                                </StackPanel>
                            </Border>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </StackPanel>
        </TabItem>
    </TabControl>
</UserControl>
```

(The `<!-- ... -->` comment marks where Task 10/11's existing `DockPanel`/progress-bar XAML already sits — keep that content exactly as-is, just now nested one level deeper inside the "Catch log" `TabItem` instead of directly under the root `UserControl`.)

- [ ] **Step 5: Wire `HatchButton_Click` and Pokédex/egg population in `CollectionView.xaml.cs`**

Add the constructor parameter and handler:

```csharp
    private readonly Func<Guid, bool> _onHatchRequested;

    public CollectionView(CollectionViewModel viewModel, AppState appState, Action onPetRequested, Action onFeedRequested, Action onStateChanged, Func<byte[]?> getActiveSpriteBytes, Func<Guid, bool> onHatchRequested)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _appState = appState;
        _onPetRequested = onPetRequested;
        _onFeedRequested = onFeedRequested;
        _onStateChanged = onStateChanged;
        _getActiveSpriteBytes = getActiveSpriteBytes;
        _onHatchRequested = onHatchRequested;
        Loaded += (_, _) => StartLiveRefresh();
        Unloaded += (_, _) => StopLiveRefresh();
        Refresh();
    }

    private void HatchButton_Click(object sender, RoutedEventArgs e)
    {
        var eggId = (Guid)((System.Windows.Controls.Button)sender).Tag;
        if (_onHatchRequested(eggId))
        {
            _onStateChanged();
        }
        Refresh();
    }
```

Add to `Refresh()`:
```csharp
        PendingEggsList.ItemsSource = _viewModel.GetPendingEggEntries(DateTimeOffset.UtcNow);

        var pokedex = _viewModel.GetPokedexEntries();
        PokedexCountText.Text = $"Pokédex: {pokedex.Count} species";
        PokedexGrid.ItemsSource = pokedex;
```

- [ ] **Step 6: Add `App.HatchEgg` and update the `CollectionViewModel`/`CollectionView` construction call site**

In `src/PokeCorner.App/App.xaml.cs`, add a field `private EggIncubationService _eggIncubationService = null!;`, instantiate it in `OnStartup` alongside `_catchRoller`: `_eggIncubationService = new EggIncubationService();`. Add:

```csharp
    private bool HatchEgg(Guid eggId)
    {
        var egg = _appState.PendingEggs.FirstOrDefault(e => e.Id == eggId);
        if (egg is null || !_eggIncubationService.IsReady(egg, DateTimeOffset.UtcNow)) return false;

        var availableChains = _spriteCache.LoadOfflineStarters(); // Task 20 swaps this to _speciesCatalog.LoadCachedOrFallback()
        var ownedChainIds = _appState.Collection.Individuals.Select(i => i.EvolutionChainId).ToHashSet();
        var ownedNatures = _appState.Collection.Individuals
            .Where(i => ownedChainIds.Contains(i.EvolutionChainId))
            .Select(i => i.Nature)
            .ToHashSet();

        var result = _catchRoller.RollCatchFromTier(availableChains, egg.Tier, ownedChainIds, ownedNatures);
        _appState.Collection.Add(new PokemonIndividual
        {
            SpeciesId = result.SpeciesId,
            EvolutionChainId = result.ChainId,
            ChainLength = result.ChainLength,
            Nature = result.Nature,
            IsShiny = result.IsShiny,
            CaughtAtUtc = DateTimeOffset.UtcNow
        });
        _appState.PendingEggs.Remove(egg);
        return true;
    }
```

In `OpenMenu`, update both construction calls:
```csharp
                var collectionVm = new CollectionViewModel(_appState.Collection, _appState.CareStateByIndividualId, _careModeService, _appState.Settings.CareMode, _spriteCache.LoadOfflineStarters(), _appState.PendingEggs);
                var collectionView = new CollectionView(collectionVm, _appState, PetActiveIndividual, FeedActiveIndividual, RefreshOverlayForActiveIndividual, () => _activeSpriteBytes, HatchEgg);
```

- [ ] **Step 7: Build and run the full test suite**

Run: `dotnet build PokeCorner.sln && dotnet test`
Expected: build succeeds, all tests green.

- [ ] **Step 8: Manual verification**

Launch the app, open the menu window — confirm two tabs ("Catch log" / "Pokédex"). Pokédex shows one card per ever-owned species (not per individual) with a rarity label, **no** placeholder cells for uncaught species. (Egg cards and the hatch button are verified end-to-end once Task 14 lets you actually buy one.)

- [ ] **Step 9: Commit**

```bash
git add src/PokeCorner.App/Popup/CollectionViewModel.cs src/PokeCorner.App/Popup/CollectionView.xaml src/PokeCorner.App/Popup/CollectionView.xaml.cs src/PokeCorner.App/App.xaml.cs tests/PokeCorner.App.Tests/Popup/CollectionViewModelTests.cs
git commit -m "Add Pokedex tab and pending-egg hatch cards to CollectionView"
```

---

## Task 14: Shop tab — treat packs, boosters, and eggs that start incubating

**Files:**
- Create: `src/PokeCorner.App/Popup/ShopView.xaml`
- Create: `src/PokeCorner.App/Popup/ShopView.xaml.cs`
- Modify: `src/PokeCorner.App/Popup/ProgressCalculator.cs`
- Modify: `src/PokeCorner.App/Popup/AppMenuWindow.xaml`
- Modify: `src/PokeCorner.App/Popup/AppMenuWindow.xaml.cs`
- Modify: `src/PokeCorner.App/App.xaml.cs`
- Modify: `src/PokeCorner.App/Tray/TrayIconService.cs`
- Test: `tests/PokeCorner.App.Tests/Popup/ProgressCalculatorTests.cs`

**Interfaces:**
- Consumes: `ShopService` (Task 4), `EggIncubationService` (Task 9).
- Produces: `ProgressCalculator.ComputeCoinFraction(ShopInventory) : double`, `App.BuyEgg(RarityTier) : bool`, `TrayIconService.OpenShopRequested : event Action?` — Task 15 (Catch tab) is the next thing to touch `App.HatchEgg`, unrelated to this task's `BuyEgg`.

Per the latest revision, buying an egg here does **not** catch instantly — it spends coins and starts a 1-hour incubation (`EggIncubationService.StartIncubating`), landing in `AppState.PendingEggs` where Task 13's Catch-log cards and hatch button take over.

- [ ] **Step 1: Write the failing `ComputeCoinFraction` test**

Add to `tests/PokeCorner.App.Tests/Popup/ProgressCalculatorTests.cs`:

```csharp
    [Fact]
    public void ComputeCoinFraction_ReflectsRemainderUnits()
    {
        var shop = new ShopInventory { RemainderUnits = ShopService.ActivityUnitsPerCoinEarned / 4 };

        var result = ProgressCalculator.ComputeCoinFraction(shop);

        Assert.Equal(0.25, result, precision: 3);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/PokeCorner.App.Tests --filter ComputeCoinFraction`
Expected: FAIL — `ProgressCalculator.ComputeCoinFraction` doesn't exist.

- [ ] **Step 3: Add `ComputeCoinFraction` to `ProgressCalculator`**

Add this method to `src/PokeCorner.App/Popup/ProgressCalculator.cs`, alongside `Compute`:

```csharp
    public static double ComputeCoinFraction(ShopInventory shop) =>
        Math.Clamp((double)shop.RemainderUnits / ShopService.ActivityUnitsPerCoinEarned, 0.0, 1.0);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/PokeCorner.App.Tests --filter ComputeCoinFraction`
Expected: PASS.

- [ ] **Step 5: Write `ShopView.xaml`/`.xaml.cs`**

```xaml
<!-- src/PokeCorner.App/Popup/ShopView.xaml -->
<UserControl x:Class="PokeCorner.App.Popup.ShopView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <StackPanel Margin="8">
        <TextBlock x:Name="CoinsText" FontWeight="Bold" FontSize="14" Margin="0,0,0,4" />
        <ProgressBar x:Name="CoinProgressBar" Minimum="0" Maximum="1" Height="6" Margin="0,0,0,8" ToolTip="Прогресс до следующей монеты" />

        <TextBlock Text="Расходники" FontWeight="Bold" Margin="0,0,0,4" />
        <Button x:Name="TreatPackButton" Click="TreatPackButton_Click" Margin="0,0,0,4" HorizontalAlignment="Left" />
        <Button x:Name="CarePointsBoosterButton" Click="CarePointsBoosterButton_Click" Margin="0,0,0,4" HorizontalAlignment="Left" />
        <Button x:Name="CatchLuckyCharmButton" Click="CatchLuckyCharmButton_Click" Margin="0,0,0,12" HorizontalAlignment="Left" />

        <TextBlock Text="Яйца — вылупляются через 1 час после покупки" FontWeight="Bold" Margin="0,0,0,4" TextWrapping="Wrap" />
        <Button x:Name="EggCommonButton" Click="EggCommonButton_Click" Margin="0,0,0,4" HorizontalAlignment="Left" />
        <Button x:Name="EggUncommonButton" Click="EggUncommonButton_Click" Margin="0,0,0,4" HorizontalAlignment="Left" />
        <Button x:Name="EggRareButton" Click="EggRareButton_Click" Margin="0,0,0,4" HorizontalAlignment="Left" />
        <Button x:Name="EggLegendaryButton" Click="EggLegendaryButton_Click" HorizontalAlignment="Left" />

        <TextBlock x:Name="MessageText" FontSize="11" Foreground="Gray" Margin="0,8,0,0" TextWrapping="Wrap" />
    </StackPanel>
</UserControl>
```

```csharp
// src/PokeCorner.App/Popup/ShopView.xaml.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PokeCorner.Core;
using PokeCorner.Core.Domain;

namespace PokeCorner.App.Popup;

public partial class ShopView : System.Windows.Controls.UserControl
{
    private readonly AppState _appState;
    private readonly ShopService _shopService;
    private readonly Func<RarityTier, bool> _tryBuyEgg;
    private readonly Action _onStateChanged;
    private DispatcherTimer? _liveRefreshTimer;

    public ShopView(AppState appState, ShopService shopService, Func<RarityTier, bool> tryBuyEgg, Action onStateChanged)
    {
        InitializeComponent();
        _appState = appState;
        _shopService = shopService;
        _tryBuyEgg = tryBuyEgg;
        _onStateChanged = onStateChanged;
        Loaded += (_, _) => StartLiveRefresh();
        Unloaded += (_, _) => StopLiveRefresh();
        Refresh();
    }

    private void StartLiveRefresh()
    {
        _liveRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _liveRefreshTimer.Tick += (_, _) => Refresh();
        _liveRefreshTimer.Start();
    }

    private void StopLiveRefresh()
    {
        _liveRefreshTimer?.Stop();
        _liveRefreshTimer = null;
    }

    private void TreatPackButton_Click(object sender, RoutedEventArgs e) =>
        ReportResult(_shopService.BuyTreatPack(_appState.Shop, _appState.Feeding), "Куплена пачка лакомств.", "Не хватает монет.");

    private void CarePointsBoosterButton_Click(object sender, RoutedEventArgs e)
    {
        var active = _appState.Collection.GetActive();
        if (active is null)
        {
            MessageText.Text = "Нет активного питомца на рабочем столе.";
            return;
        }
        var bought = _shopService.BuyCarePointsBooster(_appState.Shop, active);
        if (bought) _appState.RecomputeStage(active);
        ReportResult(bought, "Бустер роста применён.", "Не хватает монет.");
    }

    private void CatchLuckyCharmButton_Click(object sender, RoutedEventArgs e) =>
        ReportResult(_shopService.BuyCatchLuckyCharm(_appState.Shop), "Талисман удачи активирован на следующую поимку.", "Не хватает монет.");

    private void EggCommonButton_Click(object sender, RoutedEventArgs e) => BuyEgg(RarityTier.Common);
    private void EggUncommonButton_Click(object sender, RoutedEventArgs e) => BuyEgg(RarityTier.Uncommon);
    private void EggRareButton_Click(object sender, RoutedEventArgs e) => BuyEgg(RarityTier.Rare);
    private void EggLegendaryButton_Click(object sender, RoutedEventArgs e) => BuyEgg(RarityTier.Legendary);

    private void BuyEgg(RarityTier tier) =>
        ReportResult(_tryBuyEgg(tier), "Яйцо отправлено в инкубацию — загляни в Коллекцию через час.", "Не хватает монет.");

    private void ReportResult(bool success, string successMessage, string failureMessage)
    {
        MessageText.Text = success ? successMessage : failureMessage;
        if (success) _onStateChanged();
        Refresh();
    }

    public void Refresh()
    {
        CoinsText.Text = $"Монеты: {_appState.Shop.Coins}";
        CoinProgressBar.Value = ProgressCalculator.ComputeCoinFraction(_appState.Shop);
        TreatPackButton.Content = $"Пачка лакомств (5 шт) — {ShopService.TreatPackPrice}";
        CarePointsBoosterButton.Content = $"Бустер роста (+{ShopService.CarePointsBoosterAmount} CP) — {ShopService.CarePointsBoosterPrice}";
        CatchLuckyCharmButton.Content = $"Талисман удачи — {ShopService.CatchLuckyCharmPrice}";
        EggCommonButton.Content = $"Яйцо: Обычное — {ShopService.EggPriceCommon}";
        EggUncommonButton.Content = $"Яйцо: Необычное — {ShopService.EggPriceUncommon}";
        EggRareButton.Content = $"Яйцо: Редкое — {ShopService.EggPriceRare}";
        EggLegendaryButton.Content = $"Яйцо: Легендарное — {ShopService.EggPriceLegendary}";
    }
}
```

- [ ] **Step 6: Add the Shop tab to `AppMenuWindow`**

In `src/PokeCorner.App/Popup/AppMenuWindow.xaml`, add a third `TabItem` after `SettingsTab`:
```xaml
        <TabItem x:Name="ShopTab" Header="Магазин" />
```

In `src/PokeCorner.App/Popup/AppMenuWindow.xaml.cs`, extend the constructor:
```csharp
    public AppMenuWindow(CollectionView collectionView, SettingsView settingsView, ShopView shopView)
    {
        InitializeComponent();
        CollectionTab.Content = collectionView;
        SettingsTab.Content = settingsView;
        ShopTab.Content = shopView;
    }
```

- [ ] **Step 7: Wire the tray "Магазин" item and `App.BuyEgg`**

In `src/PokeCorner.App/Tray/TrayIconService.cs`, add `public event Action? OpenShopRequested;` alongside the other events, and add a menu item in the constructor (right after "Настройки", before the separator):
```csharp
        menu.Items.Add("Коллекция", null, (_, _) => OpenCollectionRequested?.Invoke());
        menu.Items.Add("Настройки", null, (_, _) => OpenSettingsRequested?.Invoke());
        menu.Items.Add("Магазин", null, (_, _) => OpenShopRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
```

In `src/PokeCorner.App/App.xaml.cs`, add `private const int ShopTabIndex = 2;` alongside the other tab-index constants, subscribe `_trayIconService.OpenShopRequested += () => OpenMenu(ShopTabIndex);` in `OnStartup`, and add:

```csharp
    private bool BuyEgg(RarityTier tier)
    {
        if (!_shopService.TrySpendForEgg(_appState.Shop, tier)) return false;
        _appState.PendingEggs.Add(_eggIncubationService.StartIncubating(tier, DateTimeOffset.UtcNow));
        return true;
    }
```

Update `OpenMenu` to construct and wire in the `ShopView`:
```csharp
                var shopView = new ShopView(_appState, _shopService, BuyEgg, RefreshOverlayForActiveIndividual);
                _menuWindow = new AppMenuWindow(collectionView, settingsView, shopView);
```

- [ ] **Step 8: Build and run the full test suite**

Run: `dotnet build PokeCorner.sln && dotnet test`
Expected: build succeeds, all tests green.

- [ ] **Step 9: Manual verification**

Launch the app, open the menu window, switch to "Магазин" — confirm coin balance, coin progress bar, and all 7 buttons with correct prices. With 0 coins, buy attempts show "Не хватает монет." without crashing. Buying an egg (once you have enough coins) does **not** immediately add a Pokémon — confirm it instead shows up as a pending card on the Catch log tab (Task 13) with a 1-hour countdown.

- [ ] **Step 10: Commit**

```bash
git add src/PokeCorner.App/Popup/ShopView.xaml src/PokeCorner.App/Popup/ShopView.xaml.cs src/PokeCorner.App/Popup/ProgressCalculator.cs src/PokeCorner.App/Popup/AppMenuWindow.xaml src/PokeCorner.App/Popup/AppMenuWindow.xaml.cs src/PokeCorner.App/App.xaml.cs src/PokeCorner.App/Tray/TrayIconService.cs tests/PokeCorner.App.Tests/Popup/ProgressCalculatorTests.cs
git commit -m "Add Shop tab: treat packs, boosters, lucky charm, and eggs that start incubating"
```

---

## Task 15: Catch tab — shake/reveal animation, shared by activity catches and egg hatches

**Files:**
- Create: `src/PokeCorner.App/Popup/CatchReveal.cs`
- Create: `src/PokeCorner.App/Popup/CatchView.xaml`
- Create: `src/PokeCorner.App/Popup/CatchView.xaml.cs`
- Modify: `src/PokeCorner.App/Popup/AppMenuWindow.xaml`
- Modify: `src/PokeCorner.App/Popup/AppMenuWindow.xaml.cs`
- Modify: `src/PokeCorner.App/App.xaml.cs`

**Interfaces:**
- Consumes: `CatchRoller.RollCatch`/`RollCatchFromTier` (Task 5), `RarityClassifier` (Task 4), `SpriteCache`/`PokeApiClient` (existing), `App.HatchEgg` (Task 13).
- Produces: `record CatchReveal(CatchResult Result, RarityTier Rarity)`, `CatchView.PlayReveal(CatchReveal) : Task`, `CatchView.Refresh()`, `AppMenuWindow.ShowCatchReveal(CatchReveal)`, `App.TryPerformCatch() : CatchReveal?` — no later task depends on these beyond Task 20 (catalog wiring) swapping `TryPerformCatch`'s/`HatchEgg`'s chain source, same one-line swap pattern as everywhere else in this plan.

Per the latest revision, the tray "Поймать!" item no longer catches directly — it opens the menu window on the Catch tab, where the actual "Поймать!" button lives, plays a shake animation, then reveals the sprite/name/rarity. Egg hatching (Task 13's `HatchEgg`) is upgraded here to trigger the exact same reveal.

- [ ] **Step 1: Write `CatchReveal.cs`**

```csharp
// src/PokeCorner.App/Popup/CatchReveal.cs
using PokeCorner.Core.Domain;

namespace PokeCorner.App.Popup;

public sealed record CatchReveal(CatchResult Result, RarityTier Rarity);
```

- [ ] **Step 2: Write `CatchView.xaml`**

```xaml
<UserControl x:Class="PokeCorner.App.Popup.CatchView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid Margin="16">
        <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center">
            <TextBlock x:Name="StatusText" FontWeight="Bold" HorizontalAlignment="Center" Margin="0,0,0,12" TextWrapping="Wrap" TextAlignment="Center" Width="220" />
            <TextBlock Text="До поимки" FontSize="10" Foreground="Gray" HorizontalAlignment="Center" />
            <ProgressBar x:Name="CatchProgressBar" Minimum="0" Maximum="1" Width="160" Height="8" Margin="0,0,0,16" ToolTip="Прогресс до следующей поимки" />
            <Canvas Width="80" Height="80" HorizontalAlignment="Center">
                <Ellipse x:Name="PokeBallShape" Width="80" Height="80" RenderTransformOrigin="0.5,0.5">
                    <Ellipse.Fill>
                        <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                            <GradientStop Color="#E63946" Offset="0" />
                            <GradientStop Color="#E63946" Offset="0.5" />
                            <GradientStop Color="White" Offset="0.5" />
                            <GradientStop Color="White" Offset="1" />
                        </LinearGradientBrush>
                    </Ellipse.Fill>
                    <Ellipse.RenderTransform>
                        <RotateTransform x:Name="PokeBallRotate" Angle="0" />
                    </Ellipse.RenderTransform>
                </Ellipse>
                <Ellipse Width="16" Height="16" Canvas.Left="32" Canvas.Top="32" Fill="White" Stroke="Black" StrokeThickness="2" />
            </Canvas>
            <Image x:Name="RevealSpriteImage" Width="96" Height="96" Opacity="0" RenderOptions.BitmapScalingMode="NearestNeighbor" Margin="0,12,0,0" />
            <TextBlock x:Name="RevealNameText" FontWeight="Bold" FontSize="16" HorizontalAlignment="Center" Opacity="0" />
            <TextBlock x:Name="RevealRarityText" FontSize="12" HorizontalAlignment="Center" Opacity="0" />
            <Button x:Name="CatchButton" Content="Поймать!" Click="CatchButton_Click" Margin="0,16,0,0" HorizontalAlignment="Center" />
        </StackPanel>
    </Grid>
</UserControl>
```

- [ ] **Step 3: Write `CatchView.xaml.cs`**

```csharp
// src/PokeCorner.App/Popup/CatchView.xaml.cs
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PokeCorner.Core;

namespace PokeCorner.App.Popup;

public partial class CatchView : System.Windows.Controls.UserControl
{
    private readonly AppState _appState;
    private readonly Func<CatchReveal?> _tryPerformCatch;
    private readonly Func<int, Task<byte[]?>> _fetchSprite;
    private readonly Func<bool> _isCatchReady;
    private readonly Action _onStateChanged;
    private DispatcherTimer? _liveRefreshTimer;

    public CatchView(AppState appState, Func<CatchReveal?> tryPerformCatch, Func<int, Task<byte[]?>> fetchSprite, Func<bool> isCatchReady, Action onStateChanged)
    {
        InitializeComponent();
        _appState = appState;
        _tryPerformCatch = tryPerformCatch;
        _fetchSprite = fetchSprite;
        _isCatchReady = isCatchReady;
        _onStateChanged = onStateChanged;
        Loaded += (_, _) => StartLiveRefresh();
        Unloaded += (_, _) => StopLiveRefresh();
        Refresh();
    }

    private void StartLiveRefresh()
    {
        _liveRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _liveRefreshTimer.Tick += (_, _) => Refresh();
        _liveRefreshTimer.Start();
    }

    private void StopLiveRefresh()
    {
        _liveRefreshTimer?.Stop();
        _liveRefreshTimer = null;
    }

    public void Refresh()
    {
        var progress = ProgressCalculator.Compute(_appState.Activity, _appState.Collection.GetActive(), _appState.Feeding);
        CatchProgressBar.Value = progress.CatchFraction;
        SetReady(_isCatchReady());
    }

    private void SetReady(bool ready)
    {
        CatchButton.IsEnabled = ready;
        StatusText.Text = ready ? "Готов новый покемон! Жми «Поймать!»." : "Копи активность за ПК — полоса поимки ещё не заполнилась.";
    }

    private async void CatchButton_Click(object sender, RoutedEventArgs e)
    {
        var reveal = _tryPerformCatch();
        if (reveal is null)
        {
            StatusText.Text = "Пока не готово — играй ещё.";
            return;
        }
        _onStateChanged();
        await PlayReveal(reveal);
    }

    public async Task PlayReveal(CatchReveal reveal)
    {
        SetReady(false);
        RevealSpriteImage.Opacity = 0;
        RevealNameText.Opacity = 0;
        RevealRarityText.Opacity = 0;

        var shake = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(900) };
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(-15, KeyTime.FromPercent(0.15)));
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(15, KeyTime.FromPercent(0.35)));
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(-15, KeyTime.FromPercent(0.55)));
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(15, KeyTime.FromPercent(0.75)));
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1.0)));

        var tcs = new TaskCompletionSource();
        shake.Completed += (_, _) => tcs.SetResult();
        PokeBallRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, shake);
        await tcs.Task;

        var spriteBytes = await _fetchSprite(reveal.Result.SpeciesId);
        if (spriteBytes is not null)
        {
            using var stream = new MemoryStream(spriteBytes);
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            RevealSpriteImage.Source = bitmap;
        }

        RevealNameText.Text = SpeciesNames.GetName(reveal.Result.SpeciesId) + (reveal.Result.IsShiny ? " ✨" : "");
        RevealRarityText.Text = reveal.Rarity + (reveal.Result.WasDuplicateSpecies ? " · уже был в коллекции" : " · новый вид!");

        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(400));
        RevealSpriteImage.BeginAnimation(OpacityProperty, fadeIn);
        RevealNameText.BeginAnimation(OpacityProperty, fadeIn);
        RevealRarityText.BeginAnimation(OpacityProperty, fadeIn);

        StatusText.Text = "Загляни в Коллекцию, чтобы посмотреть на нового покемона.";
    }
}
```

- [ ] **Step 4: Add the Catch tab to `AppMenuWindow`, with a direct-select reveal method**

In `src/PokeCorner.App/Popup/AppMenuWindow.xaml`, add a fourth `TabItem`:
```xaml
        <TabItem x:Name="CatchTab" Header="Поймать" />
```

In `src/PokeCorner.App/Popup/AppMenuWindow.xaml.cs`:
```csharp
public partial class AppMenuWindow : Window
{
    private readonly CatchView _catchView;

    public AppMenuWindow(CollectionView collectionView, SettingsView settingsView, ShopView shopView, CatchView catchView)
    {
        InitializeComponent();
        CollectionTab.Content = collectionView;
        SettingsTab.Content = settingsView;
        ShopTab.Content = shopView;
        CatchTab.Content = catchView;
        _catchView = catchView;
    }

    public void SelectTab(int index)
    {
        if (index >= 0 && index < MenuTabControl.Items.Count)
        {
            MenuTabControl.SelectedIndex = index;
        }
    }

    public async void ShowCatchReveal(CatchReveal reveal)
    {
        MenuTabControl.SelectedItem = CatchTab;
        await _catchView.PlayReveal(reveal);
    }
}
```

- [ ] **Step 5: Replace `OnCatchRequested` with `TryPerformCatch`, upgrade `HatchEgg`, and wire the Catch tab**

In `src/PokeCorner.App/App.xaml.cs`, add `private const int CatchTabIndex = 3;` alongside the other tab-index constants. Change the tray subscription from `_trayIconService.CatchRequested += OnCatchRequested;` to:
```csharp
        _trayIconService.CatchRequested += () => OpenMenu(CatchTabIndex);
```

Delete `OnCatchRequested` entirely and replace it with:
```csharp
    private CatchReveal? TryPerformCatch()
    {
        if (!_catchReady) return null;

        // PokemonCollection.Add() auto-activates via ActiveIndividualId ??= individual.Id
        // whenever nothing is currently active — correct for onboarding, but the only way
        // ActiveIndividualId is null with a non-empty collection here is a deliberate
        // Hardcore run-away, and nobody should auto-appear on the desk after that.
        var hadNoActiveInNonEmptyCollection = _appState.Collection.ActiveIndividualId is null && _appState.Collection.Individuals.Count > 0;

        var availableChains = _spriteCache.LoadOfflineStarters(); // Task 20 swaps this to _speciesCatalog.LoadCachedOrFallback()
        var ownedChainIds = _appState.Collection.Individuals.Select(i => i.EvolutionChainId).ToHashSet();
        var ownedNatures = _appState.Collection.Individuals
            .Where(i => ownedChainIds.Contains(i.EvolutionChainId))
            .Select(i => i.Nature)
            .ToHashSet();

        var shinyMultiplier = _appState.Shop.CatchLuckyCharmActive ? ShopService.CatchLuckyCharmShinyMultiplier : 1.0;
        var result = _catchRoller.RollCatch(availableChains, ownedChainIds, ownedNatures, shinyMultiplier);
        _appState.Shop.CatchLuckyCharmActive = false; // consumed regardless of outcome

        _appState.Collection.Add(new PokemonIndividual
        {
            SpeciesId = result.SpeciesId,
            EvolutionChainId = result.ChainId,
            ChainLength = result.ChainLength,
            Nature = result.Nature,
            IsShiny = result.IsShiny,
            CaughtAtUtc = DateTimeOffset.UtcNow
        });

        if (hadNoActiveInNonEmptyCollection)
        {
            _appState.Collection.ClearActive();
        }

        _catchReady = false;
        _trayIconService.SetCatchReady(false);
        RefreshOverlayForActiveIndividual();

        var chain = availableChains.FirstOrDefault(c => c.ChainId == result.ChainId);
        var rarity = chain is null ? RarityTier.Common : RarityClassifier.Classify(chain.CaptureRate);
        return new CatchReveal(result, rarity);
    }
```

Upgrade `HatchEgg` (Task 13) to trigger the same reveal on success:
```csharp
    private bool HatchEgg(Guid eggId)
    {
        var egg = _appState.PendingEggs.FirstOrDefault(e => e.Id == eggId);
        if (egg is null || !_eggIncubationService.IsReady(egg, DateTimeOffset.UtcNow)) return false;

        var availableChains = _spriteCache.LoadOfflineStarters(); // Task 20 swaps this to _speciesCatalog.LoadCachedOrFallback()
        var ownedChainIds = _appState.Collection.Individuals.Select(i => i.EvolutionChainId).ToHashSet();
        var ownedNatures = _appState.Collection.Individuals
            .Where(i => ownedChainIds.Contains(i.EvolutionChainId))
            .Select(i => i.Nature)
            .ToHashSet();

        var result = _catchRoller.RollCatchFromTier(availableChains, egg.Tier, ownedChainIds, ownedNatures);
        _appState.Collection.Add(new PokemonIndividual
        {
            SpeciesId = result.SpeciesId,
            EvolutionChainId = result.ChainId,
            ChainLength = result.ChainLength,
            Nature = result.Nature,
            IsShiny = result.IsShiny,
            CaughtAtUtc = DateTimeOffset.UtcNow
        });
        _appState.PendingEggs.Remove(egg);

        _menuWindow?.ShowCatchReveal(new CatchReveal(result, egg.Tier));
        return true;
    }
```

Update `OpenMenu` to construct and wire in the `CatchView`:
```csharp
                var catchView = new CatchView(_appState, TryPerformCatch, speciesId => _spriteCache.GetOrFetchAsync(speciesId, () => _pokeApiClient.FetchSpriteAsync(speciesId)), () => _catchReady, RefreshOverlayForActiveIndividual);
                _menuWindow = new AppMenuWindow(collectionView, settingsView, shopView, catchView);
```

- [ ] **Step 6: Build and run the full test suite**

Run: `dotnet build PokeCorner.sln && dotnet test`
Expected: build succeeds, all tests green.

- [ ] **Step 7: Manual verification**

Lower `ActivityAccumulator.CatchCheckIntervalUnits` temporarily (or generate enough real activity) to trigger a catch-ready notification. Click "Поймать!" in the tray — confirm it opens the menu window directly on the Catch tab, showing the catch-progress bar and an enabled button. Click the button — confirm the Poké Ball visibly shakes, then the caught species' sprite/name/rarity fade in. Once an egg (Task 14) is ready to hatch, click "Вылупить!" on its Catch-log card — confirm the window switches to the Catch tab and plays the exact same reveal animation.

- [ ] **Step 8: Commit**

```bash
git add src/PokeCorner.App/Popup/CatchReveal.cs src/PokeCorner.App/Popup/CatchView.xaml src/PokeCorner.App/Popup/CatchView.xaml.cs src/PokeCorner.App/Popup/AppMenuWindow.xaml src/PokeCorner.App/Popup/AppMenuWindow.xaml.cs src/PokeCorner.App/App.xaml.cs
git commit -m "Add Catch tab with shake/reveal animation, shared by activity catches and egg hatches"
```

---

## Task 16: Starter selection — choose 1 of 3 Common starters on first run

**Files:**
- Modify: `src/PokeCorner.Core/Domain/CatchRoller.cs`
- Modify: `src/PokeCorner.App/Onboarding/FirstRunFlow.cs`
- Create: `src/PokeCorner.App/Onboarding/StarterSelectionWindow.xaml`
- Create: `src/PokeCorner.App/Onboarding/StarterSelectionWindow.xaml.cs`
- Modify: `src/PokeCorner.App/App.xaml.cs`
- Test: `tests/PokeCorner.Core.Tests/Domain/CatchRollerTests.cs`

**Interfaces:**
- Consumes: `RarityClassifier` (Task 4).
- Produces: `CatchRoller.RollFromChain(SpeciesEvolutionChain) : CatchResult`, `FirstRunFlow.HatchChosen(SpeciesEvolutionChain, CatchRoller, DateTimeOffset) : PokemonIndividual` (replacing `HatchStarter`, now unused and deleted) — nothing later depends on this beyond `OnStartup` itself.

- [ ] **Step 1: Write the failing `RollFromChain` test**

Add to `tests/PokeCorner.Core.Tests/Domain/CatchRollerTests.cs`:

```csharp
    [Fact]
    public void RollFromChain_ReturnsResultForGivenChainAndIsNeverADuplicate()
    {
        var roller = new CatchRoller(new Random(Seed: 30));

        var result = roller.RollFromChain(CommonUnowned);

        Assert.Equal(CommonUnowned.ChainId, result.ChainId);
        Assert.Equal(CommonUnowned.StarterSpeciesId, result.SpeciesId);
        Assert.False(result.WasDuplicateSpecies);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter RollFromChain`
Expected: FAIL — `RollFromChain` doesn't exist.

- [ ] **Step 3: Add `RollFromChain` to `CatchRoller`**

```csharp
    // Used for starter selection: the player has already picked the species, so this
    // skips the weighted chain-picking logic entirely and just rolls nature/shiny for the
    // one chain they chose. Always WasDuplicateSpecies: false — it's necessarily the very
    // first individual of a brand-new collection.
    public CatchResult RollFromChain(SpeciesEvolutionChain chain)
    {
        var isShiny = _random.NextDouble() < BaseShinyChance;
        var nature = PickUniformNature();
        return new CatchResult(chain.ChainId, chain.StarterSpeciesId, nature, isShiny, WasDuplicateSpecies: false, chain.ChainLength);
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter RollFromChain`
Expected: PASS.

- [ ] **Step 5: Replace `FirstRunFlow.HatchStarter` with `HatchChosen`**

Read the current `src/PokeCorner.App/Onboarding/FirstRunFlow.cs` and its test file (if one exists, likely `tests/PokeCorner.App.Tests/Onboarding/FirstRunFlowTests.cs`) in full first. Replace the `HatchStarter` method with:

```csharp
    public static PokemonIndividual HatchChosen(SpeciesEvolutionChain chosenChain, CatchRoller roller, DateTimeOffset nowUtc)
    {
        var result = roller.RollFromChain(chosenChain);
        return new PokemonIndividual
        {
            SpeciesId = result.SpeciesId,
            EvolutionChainId = result.ChainId,
            ChainLength = result.ChainLength,
            Nature = result.Nature,
            IsShiny = result.IsShiny,
            CaughtAtUtc = nowUtc
        };
    }
```

Update or replace any existing test that called `HatchStarter` to call `HatchChosen` instead, passing a single fixture `SpeciesEvolutionChain` as the chosen chain and asserting the returned `PokemonIndividual`'s `SpeciesId`/`EvolutionChainId`/`ChainLength` match that chain.

- [ ] **Step 6: Write `StarterSelectionWindow.xaml`**

```xaml
<Window x:Class="PokeCorner.App.Onboarding.StarterSelectionWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Выбери своего первого покемона!" Width="420" Height="260"
        WindowStartupLocation="CenterScreen" ResizeMode="NoResize">
    <StackPanel Margin="16">
        <TextBlock Text="Выбери своего первого покемона!" FontWeight="Bold" FontSize="16" HorizontalAlignment="Center" Margin="0,0,0,16" />
        <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
            <StackPanel Margin="8" Width="110">
                <Image x:Name="Candidate1Image" Width="80" Height="80" RenderOptions.BitmapScalingMode="NearestNeighbor" />
                <TextBlock x:Name="Candidate1Name" HorizontalAlignment="Center" FontWeight="Bold" Margin="0,4,0,4" TextWrapping="Wrap" TextAlignment="Center" />
                <Button x:Name="Candidate1Button" Content="Выбрать" Click="Candidate1Button_Click" HorizontalAlignment="Center" />
            </StackPanel>
            <StackPanel x:Name="Candidate2Panel" Margin="8" Width="110">
                <Image x:Name="Candidate2Image" Width="80" Height="80" RenderOptions.BitmapScalingMode="NearestNeighbor" />
                <TextBlock x:Name="Candidate2Name" HorizontalAlignment="Center" FontWeight="Bold" Margin="0,4,0,4" TextWrapping="Wrap" TextAlignment="Center" />
                <Button x:Name="Candidate2Button" Content="Выбрать" Click="Candidate2Button_Click" HorizontalAlignment="Center" />
            </StackPanel>
            <StackPanel x:Name="Candidate3Panel" Margin="8" Width="110">
                <Image x:Name="Candidate3Image" Width="80" Height="80" RenderOptions.BitmapScalingMode="NearestNeighbor" />
                <TextBlock x:Name="Candidate3Name" HorizontalAlignment="Center" FontWeight="Bold" Margin="0,4,0,4" TextWrapping="Wrap" TextAlignment="Center" />
                <Button x:Name="Candidate3Button" Content="Выбрать" Click="Candidate3Button_Click" HorizontalAlignment="Center" />
            </StackPanel>
        </StackPanel>
    </StackPanel>
</Window>
```

- [ ] **Step 7: Write `StarterSelectionWindow.xaml.cs`**

```csharp
using System.IO;
using System.Windows;
using PokeCorner.Core.Domain;

namespace PokeCorner.App.Onboarding;

public partial class StarterSelectionWindow : Window
{
    private readonly IReadOnlyList<SpeciesEvolutionChain> _candidates;
    private readonly Func<int, Task<byte[]?>> _fetchSprite;

    public SpeciesEvolutionChain? Chosen { get; private set; }

    public StarterSelectionWindow(IReadOnlyList<SpeciesEvolutionChain> candidates, Func<int, Task<byte[]?>> fetchSprite)
    {
        InitializeComponent();
        _candidates = candidates;
        _fetchSprite = fetchSprite;

        if (_candidates.Count < 2) Candidate2Panel.Visibility = Visibility.Collapsed;
        if (_candidates.Count < 3) Candidate3Panel.Visibility = Visibility.Collapsed;

        Loaded += async (_, _) => await LoadSpritesAsync();
    }

    private async Task LoadSpritesAsync()
    {
        var images = new[] { Candidate1Image, Candidate2Image, Candidate3Image };
        var names = new[] { Candidate1Name, Candidate2Name, Candidate3Name };

        for (var i = 0; i < _candidates.Count && i < 3; i++)
        {
            names[i].Text = App.Popup.SpeciesNames.GetName(_candidates[i].StarterSpeciesId);
            var bytes = await _fetchSprite(_candidates[i].StarterSpeciesId);
            if (bytes is not null) images[i].Source = DecodePng(bytes);
        }
    }

    private void Candidate1Button_Click(object sender, RoutedEventArgs e) => Choose(0);
    private void Candidate2Button_Click(object sender, RoutedEventArgs e) => Choose(1);
    private void Candidate3Button_Click(object sender, RoutedEventArgs e) => Choose(2);

    private void Choose(int index)
    {
        if (index >= _candidates.Count) return;
        Chosen = _candidates[index];
        DialogResult = true;
        Close();
    }

    private static System.Windows.Media.ImageSource DecodePng(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
```

(`App.Popup.SpeciesNames` is referenced with its full namespace here since `StarterSelectionWindow` lives in `PokeCorner.App.Onboarding`, a sibling namespace — add `using PokeCorner.App.Popup;` instead if preferred, either works.)

- [ ] **Step 8: Wire starter selection into `OnStartup`**

In `src/PokeCorner.App/App.xaml.cs`, replace the existing onboarding block:

```csharp
        if (FirstRunFlow.NeedsOnboarding(loaded))
        {
            var allChains = _spriteCache.LoadOfflineStarters();
            var commonCandidates = allChains
                .Where(c => RarityClassifier.Classify(c.CaptureRate) == RarityTier.Common)
                .OrderBy(_ => Random.Shared.Next())
                .Take(3)
                .ToList();
            if (commonCandidates.Count == 0)
            {
                // Defensive fallback — shouldn't happen given the offline catalog's known
                // capture rates, but never leave the player with zero choices.
                commonCandidates = allChains.Take(3).ToList();
            }

            var selectionWindow = new StarterSelectionWindow(commonCandidates, speciesId => _spriteCache.GetOrFetchAsync(speciesId, () => _pokeApiClient.FetchSpriteAsync(speciesId)));
            var dialogChoseOne = selectionWindow.ShowDialog() == true;
            var chosenChain = dialogChoseOne ? selectionWindow.Chosen! : commonCandidates[0]; // window closed without a pick (e.g. Alt+F4) still needs a starter

            var starter = FirstRunFlow.HatchChosen(chosenChain, _catchRoller, DateTimeOffset.UtcNow);
            _appState.Collection.Add(starter);
        }
```

- [ ] **Step 9: Build and run the full test suite**

Run: `dotnet build PokeCorner.sln && dotnet test`
Expected: build succeeds, all tests green.

- [ ] **Step 10: Manual verification**

Delete `%AppData%\PokeCorner` (back it up first if it has data you want to keep), launch the app — confirm a "Выбери своего первого покемона!" window appears showing 3 candidates with sprites and names, and clicking one hatches that exact species onto the desktop.

- [ ] **Step 11: Commit**

```bash
git add src/PokeCorner.Core/Domain/CatchRoller.cs src/PokeCorner.App/Onboarding/FirstRunFlow.cs src/PokeCorner.App/Onboarding/StarterSelectionWindow.xaml src/PokeCorner.App/Onboarding/StarterSelectionWindow.xaml.cs src/PokeCorner.App/App.xaml.cs tests/PokeCorner.Core.Tests/Domain/CatchRollerTests.cs
git commit -m "Add starter selection: choose 1 of 3 Common Pokemon on first run"
```

---

## Task 17: Onboarding tutorial (first run + replay from Settings)

**Files:**
- Create: `src/PokeCorner.App/Onboarding/OnboardingWindow.xaml`
- Create: `src/PokeCorner.App/Onboarding/OnboardingWindow.xaml.cs`
- Modify: `src/PokeCorner.App/App.xaml.cs`
- Modify: `src/PokeCorner.App/Popup/SettingsView.xaml`
- Modify: `src/PokeCorner.App/Popup/SettingsView.xaml.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `OnboardingWindow` (no public API beyond its constructor — a self-contained step wizard) — nothing later depends on it.

- [ ] **Step 1: Write `OnboardingWindow.xaml`**

```xaml
<Window x:Class="PokeCorner.App.Onboarding.OnboardingWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="PokéCorner — обучение" Width="380" Height="260"
        WindowStartupLocation="CenterScreen" ResizeMode="NoResize">
    <StackPanel Margin="16">
        <TextBlock x:Name="TitleText" FontWeight="Bold" FontSize="16" Margin="0,0,0,12" TextWrapping="Wrap" />
        <TextBlock x:Name="BodyText" TextWrapping="Wrap" Height="100" />
        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,16,0,0">
            <Button x:Name="SkipButton" Content="Пропустить" Click="SkipButton_Click" Margin="0,0,8,0" />
            <Button x:Name="BackButton" Content="Назад" Click="BackButton_Click" Margin="0,0,8,0" />
            <Button x:Name="NextButton" Content="Далее" Click="NextButton_Click" />
        </StackPanel>
    </StackPanel>
</Window>
```

- [ ] **Step 2: Write `OnboardingWindow.xaml.cs`**

```csharp
using System.Windows;

namespace PokeCorner.App.Onboarding;

public partial class OnboardingWindow : Window
{
    private static readonly (string Title, string Body)[] Steps =
    {
        ("Добро пожаловать в PokéCorner!", "Это твой личный покемон-питомец, который живёт в углу экрана и растёт вместе с тобой."),
        ("Рост от активности", "Питомец растёт от твоей активности за ПК — кликов мышкой и клавиатуры. Чем больше ты играешь и работаешь, тем быстрее он эволюционирует."),
        ("Погладить и покормить", "Клик по питомцу на рабочем столе гладит его — бесплатно и всегда доступно. Кормление даёт больше очков роста, но требует лакомство — открой меню и загляни на вкладку «Коллекция»."),
        ("Поимка новых покемонов", "Когда шкала поимки заполнится, в трее появится пункт «Поймать!» — открой его и лови нового покемона на вкладке «Поймать»."),
        ("Коллекция и Pokédex", "Все пойманные покемоны хранятся в «Коллекции». Вкладка «Pokédex» показывает все виды, которых ты когда-либо поймал."),
        ("Магазин", "Зарабатывай монеты активностью и трать их на лакомства, бустеры или яйца редких покемонов — яйца вылупляются через час."),
        ("Меню приложения", "Двойной клик по питомцу на рабочем столе или иконка в трее открывают это меню в любой момент — все вкладки в одном окне.")
    };

    private int _stepIndex;

    public OnboardingWindow()
    {
        InitializeComponent();
        ShowStep(0);
    }

    private void ShowStep(int index)
    {
        _stepIndex = Math.Clamp(index, 0, Steps.Length - 1);
        TitleText.Text = Steps[_stepIndex].Title;
        BodyText.Text = Steps[_stepIndex].Body;
        BackButton.IsEnabled = _stepIndex > 0;
        NextButton.Content = _stepIndex == Steps.Length - 1 ? "Готово" : "Далее";
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => ShowStep(_stepIndex - 1);

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_stepIndex == Steps.Length - 1)
        {
            Close();
            return;
        }
        ShowStep(_stepIndex + 1);
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e) => Close();
}
```

- [ ] **Step 3: Show it once on first run**

In `src/PokeCorner.App/App.xaml.cs`, right after the starter-selection block added in Task 16 (after `_appState.Collection.Add(starter);`), add:

```csharp
            new OnboardingWindow().ShowDialog();
```

(Blocking — nothing else meaningfully needs to happen on a brand-new first launch before the player has seen this.)

- [ ] **Step 4: Add a "Показать обучение" button to Settings**

In `src/PokeCorner.App/Popup/SettingsView.xaml`, add near the bottom (after the `PetSizeSlider`/`ShowProgressPanelCheck` block from Task 12):

```xaml
        <Button x:Name="ShowOnboardingButton" Content="Показать обучение" Click="ShowOnboardingButton_Click" Margin="0,16,0,0" HorizontalAlignment="Left" />
```

In `src/PokeCorner.App/Popup/SettingsView.xaml.cs`, add:

```csharp
    private void ShowOnboardingButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        new PokeCorner.App.Onboarding.OnboardingWindow().Show();
    }
```

(Non-modal here — unlike the blocking first-run tour, replaying it from Settings shouldn't freeze the Settings window the player launched it from.)

- [ ] **Step 5: Build and run the full test suite**

Run: `dotnet build PokeCorner.sln && dotnet test`
Expected: build succeeds, all tests green.

- [ ] **Step 6: Manual verification**

Delete `%AppData%\PokeCorner`, launch the app, pick a starter (Task 16) — confirm the onboarding tour appears immediately after, walk through all 7 steps with Next/Back, then Skip on a later run. Open Settings, click "Показать обучение" — confirm it replays from the beginning without blocking the rest of the app.

- [ ] **Step 7: Commit**

```bash
git add src/PokeCorner.App/Onboarding/OnboardingWindow.xaml src/PokeCorner.App/Onboarding/OnboardingWindow.xaml.cs src/PokeCorner.App/App.xaml.cs src/PokeCorner.App/Popup/SettingsView.xaml src/PokeCorner.App/Popup/SettingsView.xaml.cs
git commit -m "Add first-run onboarding tutorial with a Settings replay button"
```

---

## Task 18: Pokédex-dark WPF theme

**Files:**
- Create: `src/PokeCorner.App/Theme/PokedexTheme.xaml`
- Create: `src/PokeCorner.App/Popup/RarityToBrushConverter.cs`
- Modify: `src/PokeCorner.App/App.xaml`
- Modify: `src/PokeCorner.App/Popup/CollectionView.xaml`

**Interfaces:**
- Consumes: nothing new.
- Produces: theme brushes/styles keyed as shown below — applied automatically to every `Window` in the app except `OverlayWindow`/`ProgressPanelWindow` (both already set a local `Background`, which wins over an implicit style).

- [ ] **Step 1: Write `PokedexTheme.xaml`**

```xaml
<!-- src/PokeCorner.App/Theme/PokedexTheme.xaml -->
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <SolidColorBrush x:Key="WindowBackgroundBrush" Color="#1A1F3C" />
    <SolidColorBrush x:Key="CardBackgroundBrush" Color="#252B4A" />
    <SolidColorBrush x:Key="AccentRedBrush" Color="#E63946" />
    <SolidColorBrush x:Key="AccentYellowBrush" Color="#FFD166" />
    <SolidColorBrush x:Key="PrimaryTextBrush" Color="#F1F1F1" />
    <SolidColorBrush x:Key="SecondaryTextBrush" Color="#9AA0C3" />

    <SolidColorBrush x:Key="RarityCommonBrush" Color="#9AA0C3" />
    <SolidColorBrush x:Key="RarityUncommonBrush" Color="#52C97A" />
    <SolidColorBrush x:Key="RarityRareBrush" Color="#4C8DF6" />
    <SolidColorBrush x:Key="RarityLegendaryBrush" Color="#FFB020" />

    <!-- Implicit (unkeyed) TargetType styles apply to every matching element in every
         window that merges this dictionary — including AppMenuWindow, StarterSelectionWindow,
         and OnboardingWindow, none of which need individual per-window styling code. This is
         safe for OverlayWindow (Background="{x:Null}") and ProgressPanelWindow
         (Background="#CC1A1F3C") because both already set a LOCAL Background value in their
         own XAML, and a local value always wins over a style setter in WPF — so neither one's
         carefully-tuned transparency is affected by this dictionary being merged app-wide. -->
    <Style TargetType="Window">
        <Setter Property="Background" Value="{StaticResource WindowBackgroundBrush}" />
        <Setter Property="Foreground" Value="{StaticResource PrimaryTextBrush}" />
    </Style>

    <Style TargetType="Button">
        <Setter Property="Background" Value="{StaticResource AccentRedBrush}" />
        <Setter Property="Foreground" Value="{StaticResource PrimaryTextBrush}" />
        <Setter Property="Padding" Value="10,5" />
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border Background="{TemplateBinding Background}" CornerRadius="4" Padding="{TemplateBinding Padding}">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        <Style.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
                <Setter Property="Opacity" Value="0.85" />
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
                <Setter Property="Opacity" Value="0.4" />
            </Trigger>
        </Style.Triggers>
    </Style>

    <Style TargetType="ProgressBar">
        <Setter Property="Background" Value="{StaticResource CardBackgroundBrush}" />
        <Setter Property="Foreground" Value="{StaticResource AccentYellowBrush}" />
        <Setter Property="BorderThickness" Value="0" />
    </Style>

    <Style TargetType="TabItem">
        <Setter Property="Background" Value="{StaticResource CardBackgroundBrush}" />
        <Setter Property="Foreground" Value="{StaticResource SecondaryTextBrush}" />
        <Setter Property="Padding" Value="10,4" />
        <Style.Triggers>
            <Trigger Property="IsSelected" Value="True">
                <Setter Property="Background" Value="{StaticResource AccentRedBrush}" />
                <Setter Property="Foreground" Value="{StaticResource PrimaryTextBrush}" />
            </Trigger>
        </Style.Triggers>
    </Style>

    <Style TargetType="TextBlock">
        <Setter Property="Foreground" Value="{StaticResource PrimaryTextBrush}" />
    </Style>
</ResourceDictionary>
```

- [ ] **Step 2: Merge the theme in `App.xaml`**

```xaml
<Application x:Class="PokeCorner.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="clr-namespace:PokeCorner.App">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Theme/PokedexTheme.xaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

- [ ] **Step 3: Add rarity-to-brush coloring in the Pokédex grid**

```csharp
// src/PokeCorner.App/Popup/RarityToBrushConverter.cs
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using PokeCorner.Core.Domain;

namespace PokeCorner.App.Popup;

public sealed class RarityToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            RarityTier.Common => "RarityCommonBrush",
            RarityTier.Uncommon => "RarityUncommonBrush",
            RarityTier.Rare => "RarityRareBrush",
            RarityTier.Legendary => "RarityLegendaryBrush",
            _ => "RarityCommonBrush"
        };
        return Application.Current.Resources[key];
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

In `CollectionView.xaml`, add the converter to `UserControl.Resources` and use it on the Pokédex card's rarity `TextBlock`:

```xaml
        <local:BoolToYesNoConverter x:Key="BoolToYesNo" />
        <local:RarityToBrushConverter x:Key="RarityToBrush" />
```

```xaml
                                    <TextBlock Text="{Binding Rarity}" FontSize="10" Foreground="{Binding Rarity, Converter={StaticResource RarityToBrush}}" />
```

- [ ] **Step 4: Build and run the full test suite**

Run: `dotnet build PokeCorner.sln && dotnet test`
Expected: build succeeds, all tests green (pure XAML/styling, no new unit-testable logic).

- [ ] **Step 5: Manual verification**

Launch the app — confirm the menu window (all four tabs), starter selection, and onboarding windows all show the dark-blue background with red buttons and yellow progress bars. Open the desktop sprite (click/drag) — confirm `OverlayWindow` is still fully transparent outside the sprite bounds and click-through/drag still work exactly as before. Enable the floating progress panel (Task 12) — confirm its own translucent dark background is unaffected.

- [ ] **Step 6: Commit**

```bash
git add src/PokeCorner.App/Theme/PokedexTheme.xaml src/PokeCorner.App/Popup/RarityToBrushConverter.cs src/PokeCorner.App/App.xaml src/PokeCorner.App/Popup/CollectionView.xaml
git commit -m "Add Pokedex-dark WPF theme, applied app-wide except the transparent overlay windows"
```

---

## Task 19: Dynamic tray icon (active pet sprite)

**Files:**
- Modify: `src/PokeCorner.App/Tray/TrayIconService.cs`
- Modify: `src/PokeCorner.App/App.xaml.cs`

**Interfaces:**
- Consumes: nothing new (uses `System.Drawing`, already available via `UseWindowsForms`).
- Produces: `TrayIconService.SetSpriteIcon(byte[] pngBytes) : void` — called from `RefreshOverlayForActiveIndividual`.

- [ ] **Step 1: Add `SetSpriteIcon` and a code-drawn Poké Ball fallback to `TrayIconService`**

Replace the icon-related fields (`_activeIcon`/`_inactiveIcon`) and constructor body:

```csharp
    private readonly NotifyIcon _notifyIcon;
    private Icon _currentSpriteIcon;
    private bool _isActivityTrackingIndicatorOn = true;

    public TrayIconService()
    {
        _currentSpriteIcon = DrawPlaceholderPokeBallIcon();

        var menu = new ContextMenuStrip();
        menu.Items.Add("Коллекция", null, (_, _) => OpenCollectionRequested?.Invoke());
        menu.Items.Add("Настройки", null, (_, _) => OpenSettingsRequested?.Invoke());
        menu.Items.Add("Магазин", null, (_, _) => OpenShopRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        _catchMenuItem = new ToolStripMenuItem("Поймать! 🎣", null, (_, _) => CatchRequested?.Invoke()) { Enabled = false };
        menu.Items.Add(_catchMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => ExitRequested?.Invoke());

        _notifyIcon = new NotifyIcon
        {
            Icon = _currentSpriteIcon,
            Visible = true,
            Text = "PokéCorner",
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => OpenCollectionRequested?.Invoke();
    }

    /// Called from App.xaml.cs right where the overlay's sprite PNG bytes are already
    /// decoded — the tray icon always matches whoever is currently on the desktop.
    public void SetSpriteIcon(byte[] pngBytes)
    {
        using var stream = new MemoryStream(pngBytes);
        using var bitmap = new Bitmap(stream);
        var newIcon = Icon.FromHandle(bitmap.GetHicon());

        _currentSpriteIcon.Dispose();
        _currentSpriteIcon = newIcon;
        _notifyIcon.Icon = _isActivityTrackingIndicatorOn ? _currentSpriteIcon : DesaturateIcon(_currentSpriteIcon);
    }

    private static Icon DrawPlaceholderPokeBallIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.FillPie(Brushes.Red, 1, 1, 30, 30, 180, 180);
            g.FillPie(Brushes.White, 1, 1, 30, 30, 0, 180);
            g.FillRectangle(Brushes.Black, 1, 14, 30, 4);
            g.FillEllipse(Brushes.White, 12, 12, 8, 8);
            g.DrawEllipse(Pens.Black, 12, 12, 8, 8);
            g.DrawEllipse(Pens.Black, 1, 1, 30, 30);
        }
        return Icon.FromHandle(bitmap.GetHicon());
    }

    private static Icon DesaturateIcon(Icon source)
    {
        using var bitmap = source.ToBitmap();
        var matrix = new System.Drawing.Imaging.ColorMatrix(new float[][]
        {
            new float[] { 0.3f, 0.3f, 0.3f, 0, 0 },
            new float[] { 0.59f, 0.59f, 0.59f, 0, 0 },
            new float[] { 0.11f, 0.11f, 0.11f, 0, 0 },
            new float[] { 0, 0, 0, 1, 0 },
            new float[] { 0, 0, 0, 0, 1 }
        });
        using var attributes = new System.Drawing.Imaging.ImageAttributes();
        attributes.SetColorMatrix(matrix);
        using var result = new Bitmap(bitmap.Width, bitmap.Height);
        using (var g = Graphics.FromImage(result))
        {
            g.DrawImage(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height), 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, attributes);
        }
        return Icon.FromHandle(result.GetHicon());
    }
```

Update `SetActivityTrackingIndicator` to re-apply desaturation to the current sprite icon instead of swapping between two fixed icons:

```csharp
    public void SetActivityTrackingIndicator(bool isActive)
    {
        _isActivityTrackingIndicatorOn = isActive;
        _notifyIcon.Icon = isActive ? _currentSpriteIcon : DesaturateIcon(_currentSpriteIcon);
        _notifyIcon.Text = isActive ? "PokéCorner — трекинг активности включён" : "PokéCorner — трекинг активности выключен";
    }
```

Update `Dispose` to clean up `_currentSpriteIcon` instead of the old fields:

```csharp
    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _currentSpriteIcon.Dispose();
    }
```

(`OpenShopRequested`/the "Магазин" menu item and `_catchMenuItem`/`SetCatchReady` already exist from Tasks 14/15 — this task doesn't touch them beyond what's already shown above for context.)

- [ ] **Step 2: Call `SetSpriteIcon` from `App.xaml.cs`**

In `RefreshOverlayForActiveIndividual`, right after the line added in Task 11 (`_activeSpriteBytes = spriteBytes;`), add:

```csharp
            _trayIconService.SetSpriteIcon(spriteBytes);
```

- [ ] **Step 3: Build and run the full test suite**

Run: `dotnet build PokeCorner.sln && dotnet test`
Expected: build succeeds, all tests green.

- [ ] **Step 4: Manual verification**

Launch the app — confirm the tray icon shows a small Poké Ball briefly (before the starter's sprite loads) then switches to the active pet's sprite. Switch the active individual via Collection — confirm the tray icon updates to match. Toggle "Трекинг активности" off in Settings — confirm the tray icon visibly desaturates without changing which pet it shows.

- [ ] **Step 5: Commit**

```bash
git add src/PokeCorner.App/Tray/TrayIconService.cs src/PokeCorner.App/App.xaml.cs
git commit -m "Add dynamic tray icon: active pet sprite, desaturated when tracking is off"
```

---

## Task 20: Wire `SpeciesCatalog` into the composition root

**Files:**
- Modify: `src/PokeCorner.App/App.xaml.cs`

**Interfaces:**
- Consumes: `SpeciesCatalog` (Task 7).
- Produces: nothing new — pure call-site rewiring.

- [ ] **Step 1: Instantiate and refresh the catalog at startup**

Add a field `private SpeciesCatalog _speciesCatalog = null!;`. In `OnStartup`, right after `_spriteCache`/`_pokeApiClient` are created:

```csharp
        _speciesCatalog = new SpeciesCatalog(_pokeApiClient, _spriteCache, Path.Combine(AppPaths.GetAppDataDirectory(), "catalog.json"));
        _ = _speciesCatalog.RefreshFromNetworkAsync(); // fire-and-forget; today's rolls still use whatever LoadCachedOrFallback returns right now
```

- [ ] **Step 2: Replace every catch-pool `_spriteCache.LoadOfflineStarters()` call site with `_speciesCatalog.LoadCachedOrFallback()`**

Three call sites change (all added across Tasks 10/13/15 of this plan): `OpenMenu`'s `CollectionViewModel` construction, `TryPerformCatch`'s `availableChains`, and `HatchEgg`'s `availableChains`. **Do not** change Task 16's starter-selection call site (`OnStartup`'s `allChains = _spriteCache.LoadOfflineStarters();`) — that one runs before any network sync could possibly have happened and is intentionally always offline.

- [ ] **Step 3: Build and run the full test suite**

Run: `dotnet build PokeCorner.sln && dotnet test`
Expected: build succeeds, all tests green.

- [ ] **Step 4: Manual verification**

Delete `%AppData%\PokeCorner\catalog.json` if present, launch the app, wait a couple of minutes for the network crawl (watch for the file to appear). Force several catches (tray "Поймать!" or shop eggs) both before and after `catalog.json` exists — confirm a visibly wider species spread once synced, and confirm Rare/Legendary eggs become purchasable (previously disabled-by-empty-tier on the 7-chain fallback). Disconnect from the network, delete `catalog.json`, relaunch — confirm the app still starts, still shows starter selection, and still rolls catches from the offline fallback.

- [ ] **Step 5: Commit**

```bash
git add src/PokeCorner.App/App.xaml.cs
git commit -m "Wire SpeciesCatalog into catch/hatch rolls, replacing the 7-chain-only offline pool"
```

---

## Task 21: Wire animated sprites into the overlay and tray icon

**Files:**
- Modify: `src/PokeCorner.App/App.xaml.cs`

**Interfaces:**
- Consumes: `GifFrameDecoder`, `PokeApiClient.FetchAnimatedSpriteAsync` (Task 8).
- Produces: nothing new — call-site rewiring inside `RefreshOverlayForActiveIndividual`.

- [ ] **Step 1: Replace the static-sprite-first fetch with an animated-sprite-first fetch**

In `RefreshOverlayForActiveIndividual`, replace the block that currently does:
```csharp
spriteBytes = await _spriteCache.GetOrFetchAsync(displaySpeciesId, () => _pokeApiClient.FetchSpriteAsync(displaySpeciesId));
```
with:
```csharp
byte[]? animatedBytes = null;
try
{
    animatedBytes = await _pokeApiClient.FetchAnimatedSpriteAsync(displaySpeciesId);
}
catch
{
    // Same defense-in-depth as the static-sprite catch below — network errors already
    // degrade to null inside PokeApiClient's normal paths; this guards unexpected ones.
}

if (animatedBytes is not null)
{
    if (generation != _overlayRefreshGeneration) return;
    var pngFrames = GifFrameDecoder.DecodeFramesAsPng(animatedBytes);
    _overlayWindow.SetSpriteFrames(pngFrames.Select(DecodeSprite).ToList());
    _activeSpriteBytes = pngFrames[0];
    _trayIconService.SetSpriteIcon(pngFrames[0]);
    if (_isFullscreenHiding) { _overlayWindow.Hide(); } else { _overlayWindow.Show(); }
    return;
}

spriteBytes = await _spriteCache.GetOrFetchAsync(displaySpeciesId, () => _pokeApiClient.FetchSpriteAsync(displaySpeciesId));
```

(The existing `try`/`catch` around the `spriteBytes` fetch, the `generation` staleness check, and the rest of the method below it — the `if (spriteBytes is not null) { ... }` block from Tasks 11/19 and the final show/hide logic — stay exactly as they are, now reached only when no animated sprite exists for this species.)

- [ ] **Step 2: Build and run the full test suite**

Run: `dotnet build PokeCorner.sln && dotnet test`
Expected: build succeeds, all tests green.

- [ ] **Step 3: Manual verification**

Set the active individual to a species with a known animated sprite (Pikachu, species 25) — confirm the desktop sprite visibly cycles through multiple poses, the tray icon shows the animation's first frame, and the Catch-log sprite (Task 11) still displays correctly. Switch to a species with no animated sprite — confirm it falls back to the static sprite without crashing. Spot-check Task Manager CPU usage stays low.

- [ ] **Step 4: Commit**

```bash
git add src/PokeCorner.App/App.xaml.cs
git commit -m "Wire animated Gen-V sprites into the overlay and tray icon, replacing static-only frames"
```

---

## Task 22: Procedural pet reactions

**Files:**
- Modify: `src/PokeCorner.App/Overlay/OverlayWindow.xaml`
- Modify: `src/PokeCorner.App/Overlay/OverlayWindow.xaml.cs`
- Modify: `src/PokeCorner.App/App.xaml.cs`

**Interfaces:**
- Consumes: `CareModeService.ComputeMood` (existing, previously unused).
- Produces: `OverlayWindow.PlayPetReaction()`, `PlayFeedReaction()`, `PlayEvolutionFlash()`, `PlayRanAwayReaction()`, `SetMoodTint(double mood)`.

- [ ] **Step 1: Add transform/animation infrastructure to `OverlayWindow.xaml`**

```xaml
    <Grid>
        <Image x:Name="SpriteImage" Stretch="Uniform"
               RenderOptions.BitmapScalingMode="NearestNeighbor"
               MouseLeftButtonDown="SpriteImage_MouseLeftButtonDown"
               MouseMove="SpriteImage_MouseMove"
               MouseLeftButtonUp="SpriteImage_MouseLeftButtonUp"
               RenderTransformOrigin="0.5,0.5">
            <Image.RenderTransform>
                <TransformGroup>
                    <ScaleTransform x:Name="SpriteScaleTransform" ScaleX="1" ScaleY="1" />
                    <TranslateTransform x:Name="SpriteTranslateTransform" X="0" Y="0" />
                </TransformGroup>
            </Image.RenderTransform>
        </Image>
        <TextBlock x:Name="ReactionGlyphText" HorizontalAlignment="Center" VerticalAlignment="Top" FontSize="18" Opacity="0" IsHitTestVisible="False" />
    </Grid>
```

- [ ] **Step 2: Add the reaction/mood methods to `OverlayWindow.xaml.cs`**

Add `using System.Windows.Media.Animation;` alongside the existing usings, and add these methods:

```csharp
    public void PlayPetReaction() => PlayBounce(peakScale: 1.15, durationMs: 300, glyph: "❤");

    public void PlayFeedReaction() => PlayBounce(peakScale: 1.3, durationMs: 400, glyph: "+2250");

    public void PlayEvolutionFlash()
    {
        var flash = new DoubleAnimation(1.0, 1.4, TimeSpan.FromMilliseconds(200)) { AutoReverse = true };
        SpriteScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, flash);
        SpriteScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, flash);
    }

    public void PlayRanAwayReaction()
    {
        var slide = new DoubleAnimation(0, Height, TimeSpan.FromMilliseconds(400));
        var fade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(400));
        SpriteTranslateTransform.BeginAnimation(TranslateTransform.YProperty, slide);
        SpriteImage.BeginAnimation(OpacityProperty, fade);
    }

    /// mood: 0.0 (long-neglected) to 1.0 (just interacted with) — CareModeService.ComputeMood's
    /// output, applied continuously so the sprite visibly droops/dims as Hardcore-mode
    /// neglect approaches the run-away threshold, not just at the moment it triggers.
    public void SetMoodTint(double mood)
    {
        var droopOffset = (1.0 - mood) * 10;
        SpriteTranslateTransform.Y = droopOffset;
        SpriteImage.Opacity = 0.5 + mood * 0.5;
    }

    private void PlayBounce(double peakScale, int durationMs, string glyph)
    {
        var bounce = new DoubleAnimation(1.0, peakScale, TimeSpan.FromMilliseconds(durationMs / 2.0))
        {
            AutoReverse = true,
            EasingFunction = new QuadraticEase()
        };
        SpriteScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, bounce);
        SpriteScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, bounce);

        ReactionGlyphText.Text = glyph;
        ReactionGlyphText.Opacity = 1.0;
        var fadeUp = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(durationMs * 2));
        ReactionGlyphText.BeginAnimation(OpacityProperty, fadeUp);
    }
```

(`PlayFeedReaction`'s `"+2250"` glyph hardcodes `FeedingService.FeedCarePointsBonus`'s current value as display flavor text — purely cosmetic, update it to match if that constant is ever retuned again.)

- [ ] **Step 3: Wire reactions into `App.xaml.cs`'s existing call sites**

In `PetActiveIndividual` (Task 10), track the stage before mutating and call the reaction:
```csharp
    private void PetActiveIndividual()
    {
        var active = _appState.Collection.GetActive();
        if (active is null) return;

        var stageBefore = active.CurrentStage;
        var careState = _appState.GetOrCreateCareState(active.Id);
        _feedingService.Pet(active, careState, DateTimeOffset.UtcNow);
        _appState.RecomputeStage(active);
        _trayIconService.ShowNotification("PokéCorner", "Погладил!");
        _overlayWindow.PlayPetReaction();
        if (active.CurrentStage != stageBefore) _overlayWindow.PlayEvolutionFlash();
    }
```

Apply the same pattern to `FeedActiveIndividual`:

```csharp
    private void FeedActiveIndividual()
    {
        var active = _appState.Collection.GetActive();
        if (active is null) return;

        var stageBefore = active.CurrentStage;
        var careState = _appState.GetOrCreateCareState(active.Id);
        var fed = _feedingService.Feed(active, _appState.Feeding, careState, DateTimeOffset.UtcNow);
        if (fed)
        {
            _appState.RecomputeStage(active);
            _trayIconService.ShowNotification("PokéCorner", "Покормил!");
            _overlayWindow.PlayFeedReaction();
            if (active.CurrentStage != stageBefore) _overlayWindow.PlayEvolutionFlash();
        }
        else
        {
            _trayIconService.ShowNotification("PokéCorner", "Не хватает лакомств — накопи ещё активности.");
        }
    }
```

In `CheckHardcoreNeglect`, add the mood tint on every poll and the run-away reaction right before the existing `RefreshOverlayForActiveIndividual()` call:

```csharp
    private void CheckHardcoreNeglect()
    {
        if (_appState.Settings.CareMode != CareMode.Hardcore) return;

        var active = _appState.Collection.GetActive();
        if (active is null) return;

        var careState = _appState.GetOrCreateCareState(active.Id);
        var now = DateTimeOffset.UtcNow;
        _overlayWindow.SetMoodTint(_careModeService.ComputeMood(careState, now));

        if (!_careModeService.ShouldRunAway(careState, now, _appState.Settings.CareMode)) return;

        _careModeService.TriggerRunAway(careState, now);
        _overlayWindow.PlayRanAwayReaction();
        _appState.Collection.ClearActive();
        RefreshOverlayForActiveIndividual();
        _trayIconService.ShowNotification("PokéCorner", "Питомец расстроен и не хочет возвращаться на стол ещё час.");
    }
```

- [ ] **Step 4: Build and run the full test suite**

Run: `dotnet build PokeCorner.sln && dotnet test`
Expected: build succeeds, all tests green.

- [ ] **Step 5: Manual verification**

Click the desktop sprite — confirm a quick bounce + fading heart. Feed via the menu window — confirm a bigger bounce + fading "+2250". Switch to Hardcore mode, wait without interacting — confirm the sprite gradually droops/dims, and after the neglect threshold, slides/fades away before the popup notification. Trigger an evolution — confirm a scale-pulse flash at the moment it happens.

- [ ] **Step 6: Commit**

```bash
git add src/PokeCorner.App/Overlay/OverlayWindow.xaml src/PokeCorner.App/Overlay/OverlayWindow.xaml.cs src/PokeCorner.App/App.xaml.cs
git commit -m "Add procedural pet reactions: pet/feed bounce, evolution flash, mood tint, run-away slide"
```

---

## Task 23: Final integration pass

**Files:** none new — verification only.

**Interfaces:** none.

- [ ] **Step 1: Full solution build**

Run: `dotnet build PokeCorner.sln -c Debug`
Expected: zero errors, zero warnings introduced by this plan's changes.

- [ ] **Step 2: Full automated test suite**

Run: `dotnet test`
Expected: 100% pass across `PokeCorner.Core.Tests` and `PokeCorner.App.Tests`.

- [ ] **Step 3: End-to-end manual smoke test**

Launch the built `PokeCorner.App.exe` directly and walk through, in order:
1. Fresh `%AppData%\PokeCorner` (back up the existing folder first if present) — confirm starter selection (3 Common candidates with sprites/names) appears, choosing one hatches that exact species, and the onboarding tour immediately follows (all 7 steps, Back/Next/Skip all work).
2. Double-click the desktop sprite — confirm one window opens on the Коллекция tab, showing the active pet's sprite, live progress bars, and separate Погладить/Покормить buttons with different effects.
3. Click "Настройки" in the tray while that window is open — confirm it switches tabs in the *same* window, not a second one. Confirm no leftover "Режим кормления" control, "Показать обучение" replays onboarding non-modally, the progress-panel checkbox works, and the pet-size slider still ranges 48-384.
4. Switch to the Магазин tab — confirm coin balance + coin progress bar, buy a treat pack/booster/lucky charm (once affordable), then buy an egg — confirm it does **not** instantly add a Pokémon.
5. Switch to Коллекция — confirm the purchased egg appears as a pending card with a hatch progress bar; after an hour (or by temporarily shortening `EggIncubationService.HatchDuration` for the test, reverting after), click "Вылупить!" — confirm the window jumps to the Поймать tab and plays the shake/reveal animation.
6. Force an activity-triggered catch-ready state (lower `ActivityAccumulator.CatchCheckIntervalUnits` temporarily) — click "Поймать!" in the tray, confirm it opens directly to the Поймать tab with a catch-progress bar, click the button, confirm the same shake/reveal animation plays.
7. Confirm the tray icon shows the active pet's sprite and desaturates when activity tracking is toggled off.
8. Switch to Hardcore care mode, let the pet run away — confirm the mood-droop animation over time, the slide-away reaction at the threshold, the popup, and the 1-hour cooldown all still work.
9. Confirm every window (menu, starter selection, onboarding) shows the dark Pokédex theme, while the desktop sprite and floating progress panel (if enabled) stay fully transparent/translucent as before.
10. Restart the app — confirm coins, lucky-charm state, pending eggs (with correct remaining time), and all Collection & Progress data round-trip correctly.

- [ ] **Step 4: Report results**

If every check in Step 3 passes, this plan is complete — no commit needed for this task. If anything fails, fix it as a follow-up commit referencing the specific task whose behavior regressed, then re-run Steps 1-3.
