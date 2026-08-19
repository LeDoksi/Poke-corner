# PokéCorner Core Domain Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `PokeCorner.Core` — a platform-agnostic .NET 8 class library holding all game logic (growth, evolution, catching, care, feeding), the optional Claude Code usage provider, the PokeAPI client with local sprite cache, and JSON persistence — fully unit-tested and free of any WPF/UI dependency.

**Architecture:** One class library (`src/PokeCorner.Core`) with one xUnit test project (`tests/PokeCorner.Core.Tests`). Pure, deterministic logic classes (no I/O) are separated from the two I/O boundaries (`ClaudeCodeUsageProvider`, `PokeApiClient`, `JsonSaveStore`), which take injectable dependencies (`HttpMessageHandler`, file paths) so they stay unit-testable. This library is the dependency for the later `PokeCorner.App` (WPF) plan — nothing here references WPF, Windows Forms, or any Win32 API.

**Tech Stack:** .NET 8, C#, xUnit, `System.Text.Json`.

## Global Constraints

- Never store, log, or persist raw keystroke content or window titles — only aggregate counts (from the spec's activity-tracking privacy requirement; enforced in the App plan, but Core's `ActivityAccumulator` API must only accept opaque unit counts, never raw input data).
- All data stays local — no cloud sync, no accounts, no telemetry. The only network call anywhere in Core is `PokeApiClient` fetching species/sprite data, and it must work fully offline after first successful fetch (or via the bundled fallback) — from the spec's "Данные и edge cases" section.
- Token-usage tracking (`ClaudeCodeUsageProvider`) is optional and off by default — it must be a plug-in `IActivitySource`, never wired in unconditionally.
- Persistence lives under `%AppData%\PokeCorner` (Core must accept this as an injected path, never hardcode it directly in a way that blocks tests from using a temp directory).

---

## Task 1: Project scaffold, `Nature`, `PokemonIndividual`, `PokemonCollection`

**Files:**
- Create: `PokeCorner.sln`
- Create: `src/PokeCorner.Core/PokeCorner.Core.csproj`
- Create: `tests/PokeCorner.Core.Tests/PokeCorner.Core.Tests.csproj`
- Create: `src/PokeCorner.Core/Domain/Nature.cs`
- Create: `src/PokeCorner.Core/Domain/PokemonIndividual.cs`
- Create: `src/PokeCorner.Core/Domain/PokemonCollection.cs`
- Test: `tests/PokeCorner.Core.Tests/Domain/PokemonCollectionTests.cs`

**Interfaces:**
- Produces: `enum Nature` (25 values), `class PokemonIndividual { Guid Id; int SpeciesId; int EvolutionChainId; int ChainLength; Nature Nature; bool IsShiny; DateTimeOffset CaughtAtUtc; int AccumulatedCarePoints; EvolutionStage CurrentStage; }`, `class PokemonCollection { IReadOnlyList<PokemonIndividual> Individuals; Guid? ActiveIndividualId; void Add(PokemonIndividual); bool Release(Guid id); void SetActive(Guid id); void ClearActive(); PokemonIndividual? GetActive(); bool OwnsSpeciesChain(int evolutionChainId); }`
- Consumes: nothing (first task).

- [ ] **Step 1: Scaffold the solution and projects**

Run:
```bash
dotnet new sln -n PokeCorner
dotnet new classlib -n PokeCorner.Core -o src/PokeCorner.Core -f net8.0
dotnet new xunit -n PokeCorner.Core.Tests -o tests/PokeCorner.Core.Tests -f net8.0
dotnet sln add src/PokeCorner.Core/PokeCorner.Core.csproj tests/PokeCorner.Core.Tests/PokeCorner.Core.Tests.csproj
dotnet add tests/PokeCorner.Core.Tests/PokeCorner.Core.Tests.csproj reference src/PokeCorner.Core/PokeCorner.Core.csproj
```
Delete the generated `src/PokeCorner.Core/Class1.cs` and `tests/PokeCorner.Core.Tests/UnitTest1.cs` placeholder files.

- [ ] **Step 2: Write `Nature.cs`**

```csharp
namespace PokeCorner.Core.Domain;

public enum Nature
{
    Hardy, Lonely, Brave, Adamant, Naughty,
    Bold, Docile, Relaxed, Impish, Lax,
    Timid, Hasty, Serious, Jolly, Naive,
    Modest, Mild, Quiet, Bashful, Rash,
    Calm, Gentle, Sassy, Careful, Quirky
}
```

- [ ] **Step 3: Write the failing test for `PokemonCollection`**

```csharp
// tests/PokeCorner.Core.Tests/Domain/PokemonCollectionTests.cs
using PokeCorner.Core.Domain;
using Xunit;

namespace PokeCorner.Core.Tests.Domain;

public class PokemonCollectionTests
{
    private static PokemonIndividual MakeIndividual(int speciesId = 1, int chainId = 1) => new()
    {
        SpeciesId = speciesId,
        EvolutionChainId = chainId,
        ChainLength = 3,
        Nature = Nature.Hardy,
        IsShiny = false,
        CaughtAtUtc = DateTimeOffset.UtcNow
    };

    [Fact]
    public void Add_FirstIndividual_BecomesActiveAutomatically()
    {
        var collection = new PokemonCollection();
        var individual = MakeIndividual();

        collection.Add(individual);

        Assert.Equal(individual.Id, collection.ActiveIndividualId);
        Assert.Same(individual, collection.GetActive());
    }

    [Fact]
    public void Add_SecondIndividual_DoesNotChangeActive()
    {
        var collection = new PokemonCollection();
        var first = MakeIndividual(1, 1);
        var second = MakeIndividual(2, 2);

        collection.Add(first);
        collection.Add(second);

        Assert.Equal(first.Id, collection.ActiveIndividualId);
    }

    [Fact]
    public void SetActive_UnknownId_Throws()
    {
        var collection = new PokemonCollection();
        collection.Add(MakeIndividual());

        Assert.Throws<InvalidOperationException>(() => collection.SetActive(Guid.NewGuid()));
    }

    [Fact]
    public void Release_ActiveIndividual_PromotesAnotherAsActive()
    {
        var collection = new PokemonCollection();
        var first = MakeIndividual(1, 1);
        var second = MakeIndividual(2, 2);
        collection.Add(first);
        collection.Add(second);

        var released = collection.Release(first.Id);

        Assert.True(released);
        Assert.Equal(second.Id, collection.ActiveIndividualId);
        Assert.DoesNotContain(collection.Individuals, i => i.Id == first.Id);
    }

    [Fact]
    public void Release_LastIndividual_LeavesNoActive()
    {
        var collection = new PokemonCollection();
        var only = MakeIndividual();
        collection.Add(only);

        collection.Release(only.Id);

        Assert.Null(collection.ActiveIndividualId);
        Assert.Null(collection.GetActive());
    }

    [Fact]
    public void OwnsSpeciesChain_ReturnsTrueOnlyWhenChainPresent()
    {
        var collection = new PokemonCollection();
        collection.Add(MakeIndividual(chainId: 7));

        Assert.True(collection.OwnsSpeciesChain(7));
        Assert.False(collection.OwnsSpeciesChain(8));
    }

    [Fact]
    public void ClearActive_LeavesDesktopEmptyWithoutRemovingAnyone()
    {
        var collection = new PokemonCollection();
        var individual = MakeIndividual();
        collection.Add(individual);

        collection.ClearActive();

        Assert.Null(collection.ActiveIndividualId);
        Assert.Null(collection.GetActive());
        Assert.Single(collection.Individuals);
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test tests/PokeCorner.Core.Tests`
Expected: build FAILS — `PokemonIndividual`, `EvolutionStage`, `PokemonCollection` don't exist yet.

- [ ] **Step 5: Write `PokemonIndividual.cs`**

```csharp
namespace PokeCorner.Core.Domain;

public enum EvolutionStage { Stage1 = 0, Stage2 = 1, Stage3 = 2 }

public sealed class PokemonIndividual
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required int SpeciesId { get; init; }
    public required int EvolutionChainId { get; init; }
    public required int ChainLength { get; init; }
    public required Nature Nature { get; init; }
    public required bool IsShiny { get; init; }
    public required DateTimeOffset CaughtAtUtc { get; init; }
    public int AccumulatedCarePoints { get; set; }
    public EvolutionStage CurrentStage { get; set; } = EvolutionStage.Stage1;
}
```

- [ ] **Step 6: Write `PokemonCollection.cs`**

```csharp
namespace PokeCorner.Core.Domain;

public sealed class PokemonCollection
{
    private readonly List<PokemonIndividual> _individuals = new();

    public IReadOnlyList<PokemonIndividual> Individuals => _individuals;
    public Guid? ActiveIndividualId { get; private set; }

    public void Add(PokemonIndividual individual)
    {
        _individuals.Add(individual);
        ActiveIndividualId ??= individual.Id;
    }

    public bool Release(Guid id)
    {
        var removed = _individuals.RemoveAll(i => i.Id == id) > 0;
        if (removed && ActiveIndividualId == id)
        {
            ActiveIndividualId = _individuals.Count > 0 ? _individuals[0].Id : null;
        }
        return removed;
    }

    public void SetActive(Guid id)
    {
        if (!_individuals.Any(i => i.Id == id))
        {
            throw new InvalidOperationException($"Unknown individual id: {id}");
        }
        ActiveIndividualId = id;
    }

    /// Leaves the desktop empty without removing anyone from the collection — used when
    /// a Hardcore-mode individual runs away from neglect (spec: "никто не выходит
    /// автоматически"; the App plan's composition root calls this, then the user
    /// picks a replacement manually via the collection popup, or waits out the cooldown).
    public void ClearActive() => ActiveIndividualId = null;

    public PokemonIndividual? GetActive() =>
        _individuals.FirstOrDefault(i => i.Id == ActiveIndividualId);

    public bool OwnsSpeciesChain(int evolutionChainId) =>
        _individuals.Any(i => i.EvolutionChainId == evolutionChainId);
}
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test tests/PokeCorner.Core.Tests`
Expected: PASS, 7 tests green.

- [ ] **Step 8: Commit**

```bash
git add PokeCorner.sln src/PokeCorner.Core tests/PokeCorner.Core.Tests
git commit -m "Add PokeCorner.Core scaffold, Nature, PokemonIndividual, PokemonCollection"
```

---

## Task 2: `ActivityAccumulator` (unified activity → Care Points + catch-check trigger)

**Files:**
- Create: `src/PokeCorner.Core/Activity/ActivityAccumulator.cs`
- Test: `tests/PokeCorner.Core.Tests/Activity/ActivityAccumulatorTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `class ActivityAccumulator { long TotalUnits; void AddUnits(long units); event Action CatchCheckDue; }`, `const long ActivityAccumulator.CatchCheckIntervalUnits`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/PokeCorner.Core.Tests/Activity/ActivityAccumulatorTests.cs
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
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter ActivityAccumulatorTests`
Expected: FAIL — `PokeCorner.Core.Activity` namespace/class doesn't exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace PokeCorner.Core.Activity;

public sealed class ActivityAccumulator
{
    // Tuned so ~2-3 hours of medium-high activity yields roughly one catch-check
    // (see spec: 2026-08-19-desktop-pokemon-pet-design.md, "Поимка новых покемонов").
    public const long CatchCheckIntervalUnits = 3000;

    private long _unitsSinceLastCatchCheck;

    public long TotalUnits { get; private set; }

    public event Action? CatchCheckDue;

    public void AddUnits(long units)
    {
        TotalUnits += units;
        _unitsSinceLastCatchCheck += units;

        while (_unitsSinceLastCatchCheck >= CatchCheckIntervalUnits)
        {
            _unitsSinceLastCatchCheck -= CatchCheckIntervalUnits;
            CatchCheckDue?.Invoke();
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter ActivityAccumulatorTests`
Expected: PASS, 4 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/PokeCorner.Core/Activity tests/PokeCorner.Core.Tests/Activity
git commit -m "Add ActivityAccumulator driving Care Points and catch-check cadence"
```

---

## Task 3: `EvolutionCalculator`

**Files:**
- Create: `src/PokeCorner.Core/Domain/EvolutionCalculator.cs`
- Test: `tests/PokeCorner.Core.Tests/Domain/EvolutionCalculatorTests.cs`

**Interfaces:**
- Consumes: `EvolutionStage` (Task 1).
- Produces: `static class EvolutionThresholds { const int Stage2Threshold = 5000; const int Stage3Threshold = 20000; }`, `static class EvolutionCalculator { static EvolutionStage StageForPoints(int accumulatedCarePoints, int chainLength); }`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/PokeCorner.Core.Tests/Domain/EvolutionCalculatorTests.cs
using PokeCorner.Core.Domain;
using Xunit;

namespace PokeCorner.Core.Tests.Domain;

public class EvolutionCalculatorTests
{
    [Theory]
    [InlineData(0, 3, EvolutionStage.Stage1)]
    [InlineData(4999, 3, EvolutionStage.Stage1)]
    [InlineData(5000, 3, EvolutionStage.Stage2)]
    [InlineData(19999, 3, EvolutionStage.Stage2)]
    [InlineData(20000, 3, EvolutionStage.Stage3)]
    [InlineData(999_999, 3, EvolutionStage.Stage3)]
    public void StageForPoints_ThreeStageChain_MatchesThresholds(int points, int chainLength, EvolutionStage expected)
    {
        Assert.Equal(expected, EvolutionCalculator.StageForPoints(points, chainLength));
    }

    [Fact]
    public void StageForPoints_SingleStageChain_NeverEvolves()
    {
        Assert.Equal(EvolutionStage.Stage1, EvolutionCalculator.StageForPoints(999_999, chainLength: 1));
    }

    [Fact]
    public void StageForPoints_TwoStageChain_CapsAtStage2()
    {
        Assert.Equal(EvolutionStage.Stage2, EvolutionCalculator.StageForPoints(999_999, chainLength: 2));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter EvolutionCalculatorTests`
Expected: FAIL — `EvolutionCalculator` doesn't exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace PokeCorner.Core.Domain;

public static class EvolutionThresholds
{
    public const int Stage2Threshold = 5_000;
    public const int Stage3Threshold = 20_000;
}

public static class EvolutionCalculator
{
    public static EvolutionStage StageForPoints(int accumulatedCarePoints, int chainLength)
    {
        if (chainLength >= 3 && accumulatedCarePoints >= EvolutionThresholds.Stage3Threshold)
        {
            return EvolutionStage.Stage3;
        }
        if (chainLength >= 2 && accumulatedCarePoints >= EvolutionThresholds.Stage2Threshold)
        {
            return EvolutionStage.Stage2;
        }
        return EvolutionStage.Stage1;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter EvolutionCalculatorTests`
Expected: PASS, 8 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/PokeCorner.Core/Domain/EvolutionCalculator.cs tests/PokeCorner.Core.Tests/Domain/EvolutionCalculatorTests.cs
git commit -m "Add EvolutionCalculator with stage thresholds"
```

---

## Task 4: `CatchRoller` (new-vs-duplicate weighting, shiny/nature boost on duplicates)

**Files:**
- Create: `src/PokeCorner.Core/Domain/SpeciesEvolutionChain.cs`
- Create: `src/PokeCorner.Core/Domain/CatchRoller.cs`
- Test: `tests/PokeCorner.Core.Tests/Domain/CatchRollerTests.cs`

**Interfaces:**
- Consumes: `Nature` (Task 1).
- Produces: `sealed record SpeciesEvolutionChain(int ChainId, int StarterSpeciesId, int CaptureRate, int ChainLength)`, `sealed record CatchResult(int ChainId, int SpeciesId, Nature Nature, bool IsShiny, bool WasDuplicateSpecies, int ChainLength)`, `class CatchRoller { CatchRoller(Random? random = null); CatchResult RollCatch(IReadOnlyList<SpeciesEvolutionChain> allChains, IReadOnlySet<int> ownedChainIds, IReadOnlySet<Nature> ownedNaturesForDuplicateChain); }`.

`CatchResult.ChainLength` is carried straight through from the rolled `SpeciesEvolutionChain` so that whoever turns a `CatchResult` into a new `PokemonIndividual` (the App composition root, later plan) can set `PokemonIndividual.ChainLength` without a second lookup — `AppState.ApplyActivityUnits` (Task 7) reads it directly off the individual instead of resolving it dynamically.

Rarity source: PokeAPI's `capture_rate` (3-255, higher = more common) is used directly as the base roll weight — this is the same field the spec's inspiration project weights hatches by.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/PokeCorner.Core.Tests/Domain/CatchRollerTests.cs
using PokeCorner.Core.Domain;
using Xunit;

namespace PokeCorner.Core.Tests.Domain;

public class CatchRollerTests
{
    private static readonly SpeciesEvolutionChain CommonUnowned = new(ChainId: 1, StarterSpeciesId: 10, CaptureRate: 255, ChainLength: 3);
    private static readonly SpeciesEvolutionChain RareOwned = new(ChainId: 2, StarterSpeciesId: 20, CaptureRate: 3, ChainLength: 3);

    [Fact]
    public void RollCatch_UnownedChainWeighsMuchHigherThanOwnedChain()
    {
        // With one common unowned chain and one rare-but-owned chain of equal base
        // capture rate, the unowned one should win the overwhelming majority of rolls
        // because owned chains are penalized (spec: "виды, которых ещё нет, весят больше").
        var equalRateOwned = RareOwned with { CaptureRate = 255 };
        var chains = new[] { CommonUnowned, equalRateOwned };
        var owned = new HashSet<int> { equalRateOwned.ChainId };
        var roller = new CatchRoller(new Random(Seed: 1));

        var unownedWins = 0;
        for (var i = 0; i < 1000; i++)
        {
            var result = roller.RollCatch(chains, owned, ownedNaturesForDuplicateChain: new HashSet<Nature>());
            if (result.ChainId == CommonUnowned.ChainId) unownedWins++;
        }

        // With CaptureRate 255 on both sides and the 0.15 duplicate-chain penalty, the
        // true win rate is 255/(255+255*0.15) ≈ 87% (mean 870/1000, stddev ≈10.6) — 800
        // leaves a ~6.5-stddev margin so this never flakes while still proving the bias.
        Assert.True(unownedWins > 800, $"Expected unowned chain to win the large majority of rolls, got {unownedWins}/1000");
    }

    [Fact]
    public void RollCatch_OnlyOwnedChainsAvailable_StillProducesDuplicate()
    {
        var chains = new[] { CommonUnowned };
        var owned = new HashSet<int> { CommonUnowned.ChainId };
        var roller = new CatchRoller(new Random(Seed: 2));

        var result = roller.RollCatch(chains, owned, ownedNaturesForDuplicateChain: new HashSet<Nature>());

        Assert.Equal(CommonUnowned.ChainId, result.ChainId);
        Assert.True(result.WasDuplicateSpecies);
    }

    [Fact]
    public void RollCatch_Duplicate_ShinyChanceIsHigherThanNewCatch()
    {
        var chains = new[] { CommonUnowned };
        var owned = new HashSet<int> { CommonUnowned.ChainId };

        var newCatchShinyCount = CountShiny(chains, ownedChainIds: new HashSet<int>(), trials: 20_000, seed: 3);
        var duplicateShinyCount = CountShiny(chains, owned, trials: 20_000, seed: 4);

        Assert.True(duplicateShinyCount > newCatchShinyCount,
            $"Expected duplicate shiny rate ({duplicateShinyCount}) > new-catch shiny rate ({newCatchShinyCount})");
    }

    [Fact]
    public void RollCatch_Duplicate_PrefersNatureNotYetOwnedForThatSpecies()
    {
        var chains = new[] { CommonUnowned };
        var owned = new HashSet<int> { CommonUnowned.ChainId };
        var alreadyOwnedNatures = Enum.GetValues<Nature>().Where(n => n != Nature.Jolly).ToHashSet();
        var roller = new CatchRoller(new Random(Seed: 5));

        var jollyCount = 0;
        for (var i = 0; i < 500; i++)
        {
            var result = roller.RollCatch(chains, owned, alreadyOwnedNatures);
            if (result.Nature == Nature.Jolly) jollyCount++;
        }

        // Jolly is the only not-yet-owned nature for this species; with the 0.3 owned-
        // nature weight against 24 owned natures, its true share is 1/(1+24*0.3) ≈ 12%
        // (mean 61/500, stddev ≈7.3) — well above the 1/25 ≈ 4% uniform baseline. 35
        // leaves a ~3.6-stddev margin so this never flakes while still proving the bias.
        Assert.True(jollyCount > 35, $"Expected Jolly to come up far more than the uniform baseline, got {jollyCount}/500");
    }

    private static int CountShiny(IReadOnlyList<SpeciesEvolutionChain> chains, IReadOnlySet<int> ownedChainIds, int trials, int seed)
    {
        var roller = new CatchRoller(new Random(seed));
        var shinyCount = 0;
        for (var i = 0; i < trials; i++)
        {
            var result = roller.RollCatch(chains, ownedChainIds, ownedNaturesForDuplicateChain: new HashSet<Nature>());
            if (result.IsShiny) shinyCount++;
        }
        return shinyCount;
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter CatchRollerTests`
Expected: FAIL — `SpeciesEvolutionChain`, `CatchResult`, `CatchRoller` don't exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
// src/PokeCorner.Core/Domain/SpeciesEvolutionChain.cs
namespace PokeCorner.Core.Domain;

public sealed record SpeciesEvolutionChain(int ChainId, int StarterSpeciesId, int CaptureRate, int ChainLength);

public sealed record CatchResult(int ChainId, int SpeciesId, Nature Nature, bool IsShiny, bool WasDuplicateSpecies, int ChainLength);
```

```csharp
// src/PokeCorner.Core/Domain/CatchRoller.cs
namespace PokeCorner.Core.Domain;

public sealed class CatchRoller
{
    private const double DuplicateChainWeightPenalty = 0.15;
    private const double BaseShinyChance = 1.0 / 512.0;
    private const double DuplicateShinyChanceMultiplier = 4.0;
    private const double DuplicateOwnedNatureWeight = 0.3;
    private const double DuplicateUnownedNatureWeight = 1.0;

    private readonly Random _random;

    public CatchRoller(Random? random = null) => _random = random ?? Random.Shared;

    public CatchResult RollCatch(
        IReadOnlyList<SpeciesEvolutionChain> allChains,
        IReadOnlySet<int> ownedChainIds,
        IReadOnlySet<Nature> ownedNaturesForDuplicateChain)
    {
        if (allChains.Count == 0)
        {
            throw new ArgumentException("At least one species chain is required to roll a catch.", nameof(allChains));
        }

        var chain = PickChain(allChains, ownedChainIds);
        var isDuplicate = ownedChainIds.Contains(chain.ChainId);

        var isShiny = _random.NextDouble() < (isDuplicate ? BaseShinyChance * DuplicateShinyChanceMultiplier : BaseShinyChance);
        var nature = isDuplicate
            ? PickNatureBiasedTowardUnowned(ownedNaturesForDuplicateChain)
            : PickUniformNature();

        return new CatchResult(chain.ChainId, chain.StarterSpeciesId, nature, isShiny, isDuplicate, chain.ChainLength);
    }

    private SpeciesEvolutionChain PickChain(IReadOnlyList<SpeciesEvolutionChain> allChains, IReadOnlySet<int> ownedChainIds)
    {
        var weights = allChains
            .Select(chain => (chain, weight: chain.CaptureRate * (ownedChainIds.Contains(chain.ChainId) ? DuplicateChainWeightPenalty : 1.0)))
            .ToList();

        var totalWeight = weights.Sum(w => w.weight);
        var roll = _random.NextDouble() * totalWeight;
        var cumulative = 0.0;
        foreach (var (chain, weight) in weights)
        {
            cumulative += weight;
            if (roll <= cumulative)
            {
                return chain;
            }
        }
        return weights[^1].chain;
    }

    private Nature PickUniformNature()
    {
        var values = Enum.GetValues<Nature>();
        return values[_random.Next(values.Length)];
    }

    private Nature PickNatureBiasedTowardUnowned(IReadOnlySet<Nature> ownedNatures)
    {
        var values = Enum.GetValues<Nature>();
        var weights = values.Select(n => ownedNatures.Contains(n) ? DuplicateOwnedNatureWeight : DuplicateUnownedNatureWeight).ToArray();
        var totalWeight = weights.Sum();
        var roll = _random.NextDouble() * totalWeight;
        var cumulative = 0.0;
        for (var i = 0; i < values.Length; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
            {
                return values[i];
            }
        }
        return values[^1];
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter CatchRollerTests`
Expected: PASS, 4 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/PokeCorner.Core/Domain/SpeciesEvolutionChain.cs src/PokeCorner.Core/Domain/CatchRoller.cs tests/PokeCorner.Core.Tests/Domain/CatchRollerTests.cs
git commit -m "Add CatchRoller with new-vs-duplicate weighting and duplicate shiny/nature boost"
```

---

## Task 5: `CareModeService` (Chill decay / Hardcore run-away + cooldown)

**Files:**
- Create: `src/PokeCorner.Core/Domain/CareMode.cs`
- Create: `src/PokeCorner.Core/Domain/CareState.cs`
- Create: `src/PokeCorner.Core/Domain/CareModeService.cs`
- Test: `tests/PokeCorner.Core.Tests/Domain/CareModeServiceTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `enum CareMode { Chill, Hardcore }`, `class CareState { double Mood; DateTimeOffset LastInteractionUtc; DateTimeOffset? RanAwayUntilUtc; }`, `class CareModeService { double ComputeMood(CareState, DateTimeOffset now); bool ShouldRunAway(CareState, DateTimeOffset now, CareMode mode); void TriggerRunAway(CareState, DateTimeOffset now); bool IsAvailableToPlaceOnDesktop(CareState, DateTimeOffset now); void RegisterInteraction(CareState, DateTimeOffset now); }`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/PokeCorner.Core.Tests/Domain/CareModeServiceTests.cs
using PokeCorner.Core.Domain;
using Xunit;

namespace PokeCorner.Core.Tests.Domain;

public class CareModeServiceTests
{
    [Fact]
    public void ComputeMood_NoElapsedTime_IsFull()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { LastInteractionUtc = now };

        Assert.Equal(1.0, service.ComputeMood(state, now), precision: 5);
    }

    [Fact]
    public void ComputeMood_HalfwayThroughDecayPeriod_IsHalf()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { LastInteractionUtc = now - (CareModeTuning.MoodDecayFullPeriod / 2) };

        Assert.Equal(0.5, service.ComputeMood(state, now), precision: 2);
    }

    [Fact]
    public void ComputeMood_PastFullDecayPeriod_ClampsAtZero()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { LastInteractionUtc = now - CareModeTuning.MoodDecayFullPeriod - TimeSpan.FromDays(1) };

        Assert.Equal(0.0, service.ComputeMood(state, now));
    }

    [Fact]
    public void ShouldRunAway_ChillMode_NeverTrue()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { LastInteractionUtc = now - TimeSpan.FromDays(10) };

        Assert.False(service.ShouldRunAway(state, now, CareMode.Chill));
    }

    [Fact]
    public void ShouldRunAway_HardcoreMode_BeforeThreshold_False()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { LastInteractionUtc = now - CareModeTuning.HardcoreNeglectThreshold + TimeSpan.FromMinutes(1) };

        Assert.False(service.ShouldRunAway(state, now, CareMode.Hardcore));
    }

    [Fact]
    public void ShouldRunAway_HardcoreMode_AfterThreshold_True()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { LastInteractionUtc = now - CareModeTuning.HardcoreNeglectThreshold - TimeSpan.FromMinutes(1) };

        Assert.True(service.ShouldRunAway(state, now, CareMode.Hardcore));
    }

    [Fact]
    public void TriggerRunAway_SetsOneHourCooldown()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { LastInteractionUtc = now };

        service.TriggerRunAway(state, now);

        Assert.False(service.IsAvailableToPlaceOnDesktop(state, now));
        Assert.True(service.IsAvailableToPlaceOnDesktop(state, now + CareModeTuning.HardcoreRunAwayCooldown));
    }

    [Fact]
    public void RegisterInteraction_ClearsRunAwayCooldownAndResetsTimer()
    {
        var service = new CareModeService();
        var now = DateTimeOffset.UtcNow;
        var state = new CareState { LastInteractionUtc = now - TimeSpan.FromHours(10) };
        service.TriggerRunAway(state, now);

        service.RegisterInteraction(state, now);

        Assert.True(service.IsAvailableToPlaceOnDesktop(state, now));
        Assert.Equal(now, state.LastInteractionUtc);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter CareModeServiceTests`
Expected: FAIL — types don't exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
// src/PokeCorner.Core/Domain/CareMode.cs
namespace PokeCorner.Core.Domain;

public enum CareMode { Chill, Hardcore }

public static class CareModeTuning
{
    public static readonly TimeSpan MoodDecayFullPeriod = TimeSpan.FromHours(12);
    public static readonly TimeSpan HardcoreNeglectThreshold = TimeSpan.FromHours(6);
    public static readonly TimeSpan HardcoreRunAwayCooldown = TimeSpan.FromHours(1);
}
```

```csharp
// src/PokeCorner.Core/Domain/CareState.cs
namespace PokeCorner.Core.Domain;

public sealed class CareState
{
    public DateTimeOffset LastInteractionUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RanAwayUntilUtc { get; set; }
}
```

```csharp
// src/PokeCorner.Core/Domain/CareModeService.cs
namespace PokeCorner.Core.Domain;

public sealed class CareModeService
{
    public double ComputeMood(CareState state, DateTimeOffset nowUtc)
    {
        var elapsedSeconds = (nowUtc - state.LastInteractionUtc).TotalSeconds;
        var fraction = elapsedSeconds / CareModeTuning.MoodDecayFullPeriod.TotalSeconds;
        return Math.Clamp(1.0 - fraction, 0.0, 1.0);
    }

    public bool ShouldRunAway(CareState state, DateTimeOffset nowUtc, CareMode mode)
    {
        if (mode != CareMode.Hardcore) return false;
        if (state.RanAwayUntilUtc is not null) return false;
        return nowUtc - state.LastInteractionUtc >= CareModeTuning.HardcoreNeglectThreshold;
    }

    public void TriggerRunAway(CareState state, DateTimeOffset nowUtc)
    {
        state.RanAwayUntilUtc = nowUtc + CareModeTuning.HardcoreRunAwayCooldown;
    }

    public bool IsAvailableToPlaceOnDesktop(CareState state, DateTimeOffset nowUtc) =>
        state.RanAwayUntilUtc is null || nowUtc >= state.RanAwayUntilUtc;

    public void RegisterInteraction(CareState state, DateTimeOffset nowUtc)
    {
        state.LastInteractionUtc = nowUtc;
        state.RanAwayUntilUtc = null;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter CareModeServiceTests`
Expected: PASS, 8 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/PokeCorner.Core/Domain/CareMode.cs src/PokeCorner.Core/Domain/CareState.cs src/PokeCorner.Core/Domain/CareModeService.cs tests/PokeCorner.Core.Tests/Domain/CareModeServiceTests.cs
git commit -m "Add CareModeService: Chill decay and Hardcore run-away/cooldown"
```

---

## Task 6: `FeedingService` (Simple / Economy)

**Files:**
- Create: `src/PokeCorner.Core/Domain/FeedingMode.cs`
- Create: `src/PokeCorner.Core/Domain/FeedingInventory.cs`
- Create: `src/PokeCorner.Core/Domain/FeedingService.cs`
- Test: `tests/PokeCorner.Core.Tests/Domain/FeedingServiceTests.cs`

**Interfaces:**
- Consumes: `CareState`, `CareModeService`-adjacent fields (Task 5) — feeding registers an interaction on the same `CareState`.
- Produces: `enum FeedingMode { Simple, Economy }`, `class FeedingInventory { int Treats; }`, `class FeedingService { bool TryFeed(FeedingMode, FeedingInventory, CareState, DateTimeOffset now); void EarnTreatsFromActivity(FeedingInventory, long activityUnits); }`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/PokeCorner.Core.Tests/Domain/FeedingServiceTests.cs
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
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter FeedingServiceTests`
Expected: FAIL — types don't exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
// src/PokeCorner.Core/Domain/FeedingMode.cs
namespace PokeCorner.Core.Domain;

public enum FeedingMode { Simple, Economy }
```

```csharp
// src/PokeCorner.Core/Domain/FeedingInventory.cs
namespace PokeCorner.Core.Domain;

public sealed class FeedingInventory
{
    public int Treats { get; set; }
}
```

```csharp
// src/PokeCorner.Core/Domain/FeedingService.cs
namespace PokeCorner.Core.Domain;

public sealed class FeedingService
{
    public const int TreatCostPerFeed = 1;
    public const long ActivityUnitsPerTreatEarned = 500;

    public bool TryFeed(FeedingMode mode, FeedingInventory inventory, CareState state, DateTimeOffset nowUtc)
    {
        if (mode == FeedingMode.Economy)
        {
            if (inventory.Treats < TreatCostPerFeed) return false;
            inventory.Treats -= TreatCostPerFeed;
        }

        state.LastInteractionUtc = nowUtc;
        state.RanAwayUntilUtc = null;
        return true;
    }

    public void EarnTreatsFromActivity(FeedingInventory inventory, long activityUnits)
    {
        inventory.Treats += (int)(activityUnits / ActivityUnitsPerTreatEarned);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter FeedingServiceTests`
Expected: PASS, 5 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/PokeCorner.Core/Domain/FeedingMode.cs src/PokeCorner.Core/Domain/FeedingInventory.cs src/PokeCorner.Core/Domain/FeedingService.cs tests/PokeCorner.Core.Tests/Domain/FeedingServiceTests.cs
git commit -m "Add FeedingService with Simple/Economy modes"
```

---

## Task 7: `AppSettings` + `AppState` (composition root for game state)

**Files:**
- Create: `src/PokeCorner.Core/AppSettings.cs`
- Create: `src/PokeCorner.Core/AppState.cs`
- Test: `tests/PokeCorner.Core.Tests/AppStateTests.cs`

**Interfaces:**
- Consumes: `PokemonCollection`, `PokemonIndividual` (Task 1), `ActivityAccumulator` (Task 2), `CareState`, `CareMode` (Task 5), `FeedingInventory` (Task 6).
- Produces: `class AppSettings { CareMode CareMode; FeedingMode FeedingMode; bool TokenTrackingEnabled; bool ActivityTrackingEnabled; bool AutoHideInFullscreen; bool StartWithWindows; string ScreenCorner; int MonitorIndex; int PetSizePixels; }`, `class AppState { PokemonCollection Collection; AppSettings Settings; ActivityAccumulator Activity; FeedingInventory Feeding; Dictionary<Guid, CareState> CareStateByIndividualId; void ApplyActivityUnits(long units, DateTimeOffset nowUtc); event Action CatchRollDue; }`.

`ApplyActivityUnits` is the single entry point both the input hooks and the optional token provider call: it feeds the active individual's `AccumulatedCarePoints`, advances its `EvolutionStage` via `EvolutionCalculator`, and forwards units to `ActivityAccumulator` (whose `CatchCheckDue` is re-raised as `AppState.CatchRollDue` so the caller can invoke `CatchRoller` and add the result to `Collection`).

- [ ] **Step 1: Write the failing test**

```csharp
// tests/PokeCorner.Core.Tests/AppStateTests.cs
using PokeCorner.Core;
using PokeCorner.Core.Activity;
using PokeCorner.Core.Domain;
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
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter AppStateTests`
Expected: FAIL — `AppState`, `AppSettings` don't exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
// src/PokeCorner.Core/AppSettings.cs
using PokeCorner.Core.Domain;

namespace PokeCorner.Core;

public sealed class AppSettings
{
    public CareMode CareMode { get; set; } = CareMode.Chill;
    public FeedingMode FeedingMode { get; set; } = FeedingMode.Simple;
    public bool TokenTrackingEnabled { get; set; } = false;
    public bool ActivityTrackingEnabled { get; set; } = true;
    public bool AutoHideInFullscreen { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public string ScreenCorner { get; set; } = "BottomRight";
    public int MonitorIndex { get; set; } = 0;
    public int PetSizePixels { get; set; } = 96;
}
```

```csharp
// src/PokeCorner.Core/AppState.cs
using PokeCorner.Core.Activity;
using PokeCorner.Core.Domain;

namespace PokeCorner.Core;

public sealed class AppState
{
    public PokemonCollection Collection { get; } = new();
    public AppSettings Settings { get; } = new();
    public ActivityAccumulator Activity { get; } = new();
    public FeedingInventory Feeding { get; } = new();
    public Dictionary<Guid, CareState> CareStateByIndividualId { get; } = new();

    public event Action? CatchRollDue;

    public AppState()
    {
        Activity.CatchCheckDue += () => CatchRollDue?.Invoke();
    }

    public void ApplyActivityUnits(long units, DateTimeOffset nowUtc)
    {
        var active = Collection.GetActive();
        if (active is not null)
        {
            active.AccumulatedCarePoints += (int)Math.Min(units, int.MaxValue);
            active.CurrentStage = EvolutionCalculator.StageForPoints(active.AccumulatedCarePoints, active.ChainLength);
        }

        Activity.AddUnits(units);
    }

    public CareState GetOrCreateCareState(Guid individualId)
    {
        if (!CareStateByIndividualId.TryGetValue(individualId, out var state))
        {
            state = new CareState();
            CareStateByIndividualId[individualId] = state;
        }
        return state;
    }
}
```

`ChainLength` travels with each `PokemonIndividual` from the moment it's caught (`CatchResult.ChainLength`, Task 4), so `AppState` never needs to query the species catalog itself — it stays decoupled from `PokeApiClient` per this plan's file-structure boundary.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter AppStateTests`
Expected: PASS, 4 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/PokeCorner.Core/AppSettings.cs src/PokeCorner.Core/AppState.cs tests/PokeCorner.Core.Tests/AppStateTests.cs
git commit -m "Add AppSettings and AppState composition root"
```

---

## Task 8: `ClaudeCodeUsageProvider` (optional token activity source)

**Files:**
- Create: `src/PokeCorner.Core/ActivitySources/IActivitySource.cs`
- Create: `src/PokeCorner.Core/ActivitySources/ClaudeCodeUsageProvider.cs`
- Test: `tests/PokeCorner.Core.Tests/ActivitySources/ClaudeCodeUsageProviderTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `interface IActivitySource { long ReadNewUnitsSinceLastCall(); }`, `class ClaudeCodeUsageProvider : IActivitySource { ClaudeCodeUsageProvider(string claudeHomeDirectory); long ReadNewUnitsSinceLastCall(); }`.

This mirrors the format captured live from this machine (see spec section "Опциональный трекинг токенов"): one `.jsonl` file per session under `<claudeHome>/projects/<encoded-path>/<session-uuid>.jsonl`, with lines containing a `message.usage` object with `input_tokens`, `output_tokens`, `cache_creation_input_tokens`, `cache_read_input_tokens`, and a top-level `timestamp` field.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/PokeCorner.Core.Tests/ActivitySources/ClaudeCodeUsageProviderTests.cs
using PokeCorner.Core.ActivitySources;
using Xunit;

namespace PokeCorner.Core.Tests.ActivitySources;

public class ClaudeCodeUsageProviderTests : IDisposable
{
    private readonly string _tempHome;

    public ClaudeCodeUsageProviderTests()
    {
        _tempHome = Path.Combine(Path.GetTempPath(), "PokeCornerTests_" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(_tempHome, "projects", "fake-project"));
    }

    public void Dispose() => Directory.Delete(_tempHome, recursive: true);

    private void WriteSessionFile(string content)
    {
        var path = Path.Combine(_tempHome, "projects", "fake-project", Guid.NewGuid() + ".jsonl");
        File.WriteAllText(path, content);
    }

    [Fact]
    public void ReadNewUnitsSinceLastCall_NoFiles_ReturnsZero()
    {
        var provider = new ClaudeCodeUsageProvider(_tempHome);

        Assert.Equal(0, provider.ReadNewUnitsSinceLastCall());
    }

    [Fact]
    public void ReadNewUnitsSinceLastCall_SumsTodaysInputOutputAndCacheTokens()
    {
        var today = DateTimeOffset.UtcNow.ToString("O");
        WriteSessionFile(
            $$"""
            {"timestamp":"{{today}}","message":{"usage":{"input_tokens":2,"output_tokens":635,"cache_creation_input_tokens":562,"cache_read_input_tokens":133657}}}
            {{"garbage line that is not JSON, must be skipped"}}
            {"timestamp":"{{today}}","message":{"usage":{"input_tokens":1,"output_tokens":10,"cache_creation_input_tokens":0,"cache_read_input_tokens":0}}}
            """);

        var provider = new ClaudeCodeUsageProvider(_tempHome);
        var units = provider.ReadNewUnitsSinceLastCall();

        Assert.Equal(2 + 635 + 562 + 133657 + 1 + 10, units);
    }

    [Fact]
    public void ReadNewUnitsSinceLastCall_ExcludesEntriesFromPreviousDays()
    {
        var yesterday = DateTimeOffset.UtcNow.AddDays(-1).ToString("O");
        WriteSessionFile(
            $$"""
            {"timestamp":"{{yesterday}}","message":{"usage":{"input_tokens":100,"output_tokens":100,"cache_creation_input_tokens":0,"cache_read_input_tokens":0}}}
            """);

        var provider = new ClaudeCodeUsageProvider(_tempHome);

        Assert.Equal(0, provider.ReadNewUnitsSinceLastCall());
    }

    [Fact]
    public void ReadNewUnitsSinceLastCall_SecondCall_OnlyCountsUnitsAddedSinceFirstCall()
    {
        var today = DateTimeOffset.UtcNow.ToString("O");
        WriteSessionFile(
            $$"""
            {"timestamp":"{{today}}","message":{"usage":{"input_tokens":100,"output_tokens":0,"cache_creation_input_tokens":0,"cache_read_input_tokens":0}}}
            """);
        var provider = new ClaudeCodeUsageProvider(_tempHome);
        provider.ReadNewUnitsSinceLastCall();

        WriteSessionFile(
            $$"""
            {"timestamp":"{{today}}","message":{"usage":{"input_tokens":50,"output_tokens":0,"cache_creation_input_tokens":0,"cache_read_input_tokens":0}}}
            """);
        var secondCallUnits = provider.ReadNewUnitsSinceLastCall();

        Assert.Equal(50, secondCallUnits);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter ClaudeCodeUsageProviderTests`
Expected: FAIL — types don't exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
// src/PokeCorner.Core/ActivitySources/IActivitySource.cs
namespace PokeCorner.Core.ActivitySources;

public interface IActivitySource
{
    long ReadNewUnitsSinceLastCall();
}
```

```csharp
// src/PokeCorner.Core/ActivitySources/ClaudeCodeUsageProvider.cs
using System.Text.Json;

namespace PokeCorner.Core.ActivitySources;

public sealed class ClaudeCodeUsageProvider : IActivitySource
{
    private readonly string _projectsDirectory;
    private long _totalCountedSoFar;

    public ClaudeCodeUsageProvider(string claudeHomeDirectory)
    {
        _projectsDirectory = Path.Combine(claudeHomeDirectory, "projects");
    }

    public long ReadNewUnitsSinceLastCall()
    {
        var todayTotal = SumTodaysTokens();
        var newUnits = Math.Max(0, todayTotal - _totalCountedSoFar);
        _totalCountedSoFar = todayTotal;
        return newUnits;
    }

    private long SumTodaysTokens()
    {
        if (!Directory.Exists(_projectsDirectory)) return 0;

        var today = DateTimeOffset.UtcNow.Date;
        long total = 0;

        foreach (var file in Directory.EnumerateFiles(_projectsDirectory, "*.jsonl", SearchOption.AllDirectories))
        {
            foreach (var line in File.ReadLines(file))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                JsonDocument doc;
                try
                {
                    doc = JsonDocument.Parse(line);
                }
                catch (JsonException)
                {
                    continue; // skip malformed/partial lines
                }

                using (doc)
                {
                    if (!doc.RootElement.TryGetProperty("timestamp", out var timestampElement)) continue;
                    if (!DateTimeOffset.TryParse(timestampElement.GetString(), out var timestamp)) continue;
                    if (timestamp.UtcDateTime.Date != today) continue;

                    if (!doc.RootElement.TryGetProperty("message", out var message)) continue;
                    if (!message.TryGetProperty("usage", out var usage)) continue;

                    total += GetLongOrZero(usage, "input_tokens");
                    total += GetLongOrZero(usage, "output_tokens");
                    total += GetLongOrZero(usage, "cache_creation_input_tokens");
                    total += GetLongOrZero(usage, "cache_read_input_tokens");
                }
            }
        }

        return total;
    }

    private static long GetLongOrZero(JsonElement obj, string propertyName) =>
        obj.TryGetProperty(propertyName, out var value) && value.TryGetInt64(out var number) ? number : 0;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter ClaudeCodeUsageProviderTests`
Expected: PASS, 4 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/PokeCorner.Core/ActivitySources tests/PokeCorner.Core.Tests/ActivitySources
git commit -m "Add ClaudeCodeUsageProvider reading local session jsonl logs"
```

---

## Task 9: `PokeApiClient` + `SpriteCache` + offline starter fallback

**Files:**
- Create: `src/PokeCorner.Core/PokeApi/PokeApiClient.cs`
- Create: `src/PokeCorner.Core/PokeApi/SpriteCache.cs`
- Create: `src/PokeCorner.Core/PokeApi/OfflineStarterFallback.json` (embedded resource)
- Modify: `src/PokeCorner.Core/PokeCorner.Core.csproj` (embed the fallback JSON)
- Test: `tests/PokeCorner.Core.Tests/PokeApi/PokeApiClientTests.cs`
- Test: `tests/PokeCorner.Core.Tests/PokeApi/SpriteCacheTests.cs`

**Interfaces:**
- Consumes: `SpeciesEvolutionChain` (Task 4).
- Produces: `class PokeApiClient { PokeApiClient(HttpMessageHandler handler); Task<SpeciesEvolutionChain> FetchEvolutionChainAsync(int chainId); Task<byte[]> FetchSpriteAsync(int speciesId); }`, `class SpriteCache { SpriteCache(string cacheDirectory); Task<byte[]> GetOrFetchAsync(int speciesId, Func<Task<byte[]>> fetch); IReadOnlyList<SpeciesEvolutionChain> LoadOfflineStarters(); }`.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/PokeCorner.Core.Tests/PokeApi/PokeApiClientTests.cs
using System.Net;
using System.Text;
using PokeCorner.Core.PokeApi;
using Xunit;

namespace PokeCorner.Core.Tests.PokeApi;

public class PokeApiClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _jsonResponse;
        public StubHandler(string jsonResponse) => _jsonResponse = jsonResponse;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_jsonResponse, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task FetchEvolutionChainAsync_ParsesIdStarterCaptureRateAndChainLength()
    {
        const string json = """
        {
            "id": 1,
            "chain": {
                "species": { "name": "bulbasaur", "url": "https://pokeapi.co/api/v2/pokemon-species/1/" },
                "evolves_to": [
                    {
                        "species": { "name": "ivysaur", "url": "https://pokeapi.co/api/v2/pokemon-species/2/" },
                        "evolves_to": [
                            {
                                "species": { "name": "venusaur", "url": "https://pokeapi.co/api/v2/pokemon-species/3/" },
                                "evolves_to": []
                            }
                        ]
                    }
                ]
            },
            "capture_rate": 45
        }
        """;
        var client = new PokeApiClient(new StubHandler(json));

        var chain = await client.FetchEvolutionChainAsync(chainId: 1);

        Assert.Equal(1, chain.ChainId);
        Assert.Equal(1, chain.StarterSpeciesId);
        Assert.Equal(45, chain.CaptureRate);
        Assert.Equal(3, chain.ChainLength);
    }

    [Fact]
    public async Task FetchEvolutionChainAsync_SingleStageSpecies_ChainLengthIsOne()
    {
        const string json = """
        {
            "id": 132,
            "chain": {
                "species": { "name": "ditto", "url": "https://pokeapi.co/api/v2/pokemon-species/132/" },
                "evolves_to": []
            },
            "capture_rate": 35
        }
        """;
        var client = new PokeApiClient(new StubHandler(json));

        var chain = await client.FetchEvolutionChainAsync(chainId: 132);

        Assert.Equal(1, chain.ChainLength);
    }
}
```

```csharp
// tests/PokeCorner.Core.Tests/PokeApi/SpriteCacheTests.cs
using PokeCorner.Core.PokeApi;
using Xunit;

namespace PokeCorner.Core.Tests.PokeApi;

public class SpriteCacheTests : IDisposable
{
    private readonly string _tempDir;

    public SpriteCacheTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PokeCornerSpriteCacheTests_" + Guid.NewGuid());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task GetOrFetchAsync_FirstCall_InvokesFetchAndWritesToDisk()
    {
        var cache = new SpriteCache(_tempDir);
        var fetchCallCount = 0;
        Task<byte[]> Fetch()
        {
            fetchCallCount++;
            return Task.FromResult(new byte[] { 1, 2, 3 });
        }

        var bytes = await cache.GetOrFetchAsync(speciesId: 25, Fetch);

        Assert.Equal(new byte[] { 1, 2, 3 }, bytes);
        Assert.Equal(1, fetchCallCount);
        Assert.True(File.Exists(Path.Combine(_tempDir, "25.png")));
    }

    [Fact]
    public async Task GetOrFetchAsync_SecondCall_ReadsFromDiskWithoutRefetching()
    {
        var cache = new SpriteCache(_tempDir);
        var fetchCallCount = 0;
        Task<byte[]> Fetch()
        {
            fetchCallCount++;
            return Task.FromResult(new byte[] { 9, 9, 9 });
        }

        await cache.GetOrFetchAsync(25, Fetch);
        var secondResult = await cache.GetOrFetchAsync(25, Fetch);

        Assert.Equal(new byte[] { 9, 9, 9 }, secondResult);
        Assert.Equal(1, fetchCallCount);
    }

    [Fact]
    public void LoadOfflineStarters_ReturnsNonEmptyBundledFallbackList()
    {
        var cache = new SpriteCache(_tempDir);

        var starters = cache.LoadOfflineStarters();

        Assert.NotEmpty(starters);
        Assert.All(starters, s => Assert.True(s.CaptureRate is >= 3 and <= 255));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter "PokeApiClientTests|SpriteCacheTests"`
Expected: FAIL — types don't exist.

- [ ] **Step 3: Write the offline fallback data**

```json
// src/PokeCorner.Core/PokeApi/OfflineStarterFallback.json
[
  { "ChainId": 1, "StarterSpeciesId": 1, "CaptureRate": 45, "ChainLength": 3 },
  { "ChainId": 4, "StarterSpeciesId": 4, "CaptureRate": 45, "ChainLength": 3 },
  { "ChainId": 7, "StarterSpeciesId": 7, "CaptureRate": 45, "ChainLength": 3 },
  { "ChainId": 152, "StarterSpeciesId": 152, "CaptureRate": 45, "ChainLength": 3 },
  { "ChainId": 155, "StarterSpeciesId": 155, "CaptureRate": 45, "ChainLength": 3 },
  { "ChainId": 158, "StarterSpeciesId": 158, "CaptureRate": 45, "ChainLength": 3 },
  { "ChainId": 10, "StarterSpeciesId": 25, "CaptureRate": 190, "ChainLength": 2 }
]
```

- [ ] **Step 4: Embed the fallback JSON as a resource**

Edit `src/PokeCorner.Core/PokeCorner.Core.csproj`, add inside the `<Project>` element:

```xml
<ItemGroup>
  <EmbeddedResource Include="PokeApi/OfflineStarterFallback.json" />
</ItemGroup>
```

- [ ] **Step 5: Write `PokeApiClient.cs`**

```csharp
using System.Text.Json;

namespace PokeCorner.Core.PokeApi;

public sealed class PokeApiClient
{
    private readonly HttpClient _httpClient;

    public PokeApiClient(HttpMessageHandler handler)
    {
        _httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://pokeapi.co/api/v2/") };
    }

    public async Task<Domain.SpeciesEvolutionChain> FetchEvolutionChainAsync(int chainId)
    {
        var json = await _httpClient.GetStringAsync($"evolution-chain/{chainId}/");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var chainNode = root.GetProperty("chain");

        var speciesUrl = chainNode.GetProperty("species").GetProperty("url").GetString()!;
        var starterSpeciesId = ExtractIdFromUrl(speciesUrl);

        // capture_rate lives on the species resource (pokemon-species/{id}/), not on the
        // evolution-chain resource, so it takes a second call.
        var captureRate = await FetchCaptureRateAsync(starterSpeciesId);
        var chainLength = ComputeChainLength(chainNode);

        return new Domain.SpeciesEvolutionChain(
            ChainId: root.GetProperty("id").GetInt32(),
            StarterSpeciesId: starterSpeciesId,
            CaptureRate: captureRate,
            ChainLength: chainLength);
    }

    public async Task<int> FetchCaptureRateAsync(int speciesId)
    {
        var json = await _httpClient.GetStringAsync($"pokemon-species/{speciesId}/");
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("capture_rate").GetInt32();
    }

    public async Task<byte[]> FetchSpriteAsync(int speciesId)
    {
        return await _httpClient.GetByteArrayAsync(
            $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/{speciesId}.png");
    }

    private static int ExtractIdFromUrl(string url) =>
        int.Parse(url.TrimEnd('/').Split('/')[^1]);

    private static int ComputeChainLength(JsonElement chainNode)
    {
        if (!chainNode.TryGetProperty("evolves_to", out var evolvesTo) || evolvesTo.GetArrayLength() == 0)
        {
            return 1;
        }

        var longestChildLength = 0;
        foreach (var child in evolvesTo.EnumerateArray())
        {
            longestChildLength = Math.Max(longestChildLength, ComputeChainLength(child));
        }
        return 1 + longestChildLength;
    }
}
```

The test's `StubHandler` returns the same fixture JSON for every request regardless of URL, which is why one fixture can carry both the `chain`/`evolves_to` shape (for the first call) and `capture_rate` (for the second, `FetchCaptureRateAsync` call) — a normal simplification for a single-handler unit test. Against the real PokeAPI, the two calls hit genuinely different endpoints and responses, which is exactly what this method already does.

- [ ] **Step 6: Write `SpriteCache.cs`**

```csharp
using System.Reflection;
using System.Text.Json;

namespace PokeCorner.Core.PokeApi;

public sealed class SpriteCache
{
    private readonly string _cacheDirectory;

    public SpriteCache(string cacheDirectory)
    {
        _cacheDirectory = cacheDirectory;
        Directory.CreateDirectory(_cacheDirectory);
    }

    public async Task<byte[]> GetOrFetchAsync(int speciesId, Func<Task<byte[]>> fetch)
    {
        var path = Path.Combine(_cacheDirectory, $"{speciesId}.png");
        if (File.Exists(path))
        {
            return await File.ReadAllBytesAsync(path);
        }

        var bytes = await fetch();
        await File.WriteAllBytesAsync(path, bytes);
        return bytes;
    }

    public IReadOnlyList<Domain.SpeciesEvolutionChain> LoadOfflineStarters()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("PokeCorner.Core.PokeApi.OfflineStarterFallback.json")
            ?? throw new InvalidOperationException("OfflineStarterFallback.json embedded resource not found.");
        return JsonSerializer.Deserialize<List<Domain.SpeciesEvolutionChain>>(stream)!;
    }
}
```

> The embedded resource name follows `<AssemblyRootNamespace>.<FolderPath>.<FileName>` — verify with `dotnet build` plus a quick `Assembly.GetExecutingAssembly().GetManifestResourceNames()` dump if the resource isn't found on first run; adjust the string in `LoadOfflineStarters` to match exactly.

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter "PokeApiClientTests|SpriteCacheTests"`
Expected: PASS, 5 tests green.

- [ ] **Step 8: Commit**

```bash
git add src/PokeCorner.Core/PokeApi src/PokeCorner.Core/PokeCorner.Core.csproj tests/PokeCorner.Core.Tests/PokeApi
git commit -m "Add PokeApiClient, SpriteCache, and bundled offline starter fallback"
```

---

## Task 10: `JsonSaveStore` (persistence)

**Files:**
- Create: `src/PokeCorner.Core/Persistence/ISaveStore.cs`
- Create: `src/PokeCorner.Core/Persistence/SaveData.cs`
- Create: `src/PokeCorner.Core/Persistence/JsonSaveStore.cs`
- Test: `tests/PokeCorner.Core.Tests/Persistence/JsonSaveStoreTests.cs`

**Interfaces:**
- Consumes: `AppState`, `AppSettings`, `PokemonIndividual` (Tasks 1, 7).
- Produces: `class SaveData { List<PokemonIndividual> Individuals; Guid? ActiveIndividualId; AppSettings Settings; int FeedingTreats; }`, `interface ISaveStore { void Save(SaveData data); SaveData? Load(); }`, `class JsonSaveStore : ISaveStore { JsonSaveStore(string filePath); }`.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/PokeCorner.Core.Tests/Persistence/JsonSaveStoreTests.cs
using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using Xunit;

namespace PokeCorner.Core.Tests.Persistence;

public class JsonSaveStoreTests : IDisposable
{
    private readonly string _tempFile;

    public JsonSaveStoreTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), "PokeCornerSaveTests_" + Guid.NewGuid() + ".json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    [Fact]
    public void Load_NoFileYet_ReturnsNull()
    {
        var store = new JsonSaveStore(_tempFile);

        Assert.Null(store.Load());
    }

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
            FeedingTreats = 7
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
    }

    [Fact]
    public void Save_CreatesParentDirectoryIfMissing()
    {
        var nestedPath = Path.Combine(Path.GetTempPath(), "PokeCornerSaveTests_" + Guid.NewGuid(), "state.json");
        var store = new JsonSaveStore(nestedPath);

        store.Save(new SaveData { Individuals = new(), Settings = new AppSettings(), FeedingTreats = 0 });

        Assert.True(File.Exists(nestedPath));
        Directory.Delete(Path.GetDirectoryName(nestedPath)!, recursive: true);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter JsonSaveStoreTests`
Expected: FAIL — types don't exist.

- [ ] **Step 3: Write minimal implementation**

```csharp
// src/PokeCorner.Core/Persistence/SaveData.cs
using PokeCorner.Core.Domain;

namespace PokeCorner.Core.Persistence;

public sealed class SaveData
{
    public required List<PokemonIndividual> Individuals { get; init; }
    public Guid? ActiveIndividualId { get; init; }
    public required AppSettings Settings { get; init; }
    public int FeedingTreats { get; init; }
}
```

```csharp
// src/PokeCorner.Core/Persistence/ISaveStore.cs
namespace PokeCorner.Core.Persistence;

public interface ISaveStore
{
    void Save(SaveData data);
    SaveData? Load();
}
```

```csharp
// src/PokeCorner.Core/Persistence/JsonSaveStore.cs
using System.Text.Json;

namespace PokeCorner.Core.Persistence;

public sealed class JsonSaveStore : ISaveStore
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public JsonSaveStore(string filePath) => _filePath = filePath;

    public void Save(SaveData data)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(_filePath, JsonSerializer.Serialize(data, Options));
    }

    public SaveData? Load()
    {
        if (!File.Exists(_filePath)) return null;
        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<SaveData>(json, Options);
    }
}
```

`PokemonIndividual` uses `required` init-only properties with no parameterless constructor, which `System.Text.Json` handles natively in .NET 8 — no custom converter needed.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/PokeCorner.Core.Tests --filter JsonSaveStoreTests`
Expected: PASS, 3 tests green.

- [ ] **Step 5: Run the full Core test suite**

Run: `dotnet test tests/PokeCorner.Core.Tests`
Expected: PASS, all tests across all 10 tasks green (≈52 tests).

- [ ] **Step 6: Commit**

```bash
git add src/PokeCorner.Core/Persistence tests/PokeCorner.Core.Tests/Persistence
git commit -m "Add JsonSaveStore for AppState persistence, completing PokeCorner.Core"
```
