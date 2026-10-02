using PokeCorner.Core.Activity;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;

namespace PokeCorner.Core;

public sealed class AppState
{
    public PokemonCollection Collection { get; } = new();
    public AppSettings Settings { get; } = new();
    public ActivityAccumulator Activity { get; } = new();
    public FeedingInventory Feeding { get; } = new();
    public ShopInventory Shop { get; } = new();
    public Dictionary<Guid, CareState> CareStateByIndividualId { get; } = new();
    public List<PendingEgg> PendingEggs { get; } = new();

    public event Action? CatchRollDue;

    public AppState()
    {
        Activity.CatchCheckDue += () => CatchRollDue?.Invoke();
    }

    public void ApplyActivityUnits(long units)
    {
        var active = Collection.GetActive();
        if (active is not null)
        {
            active.AccumulatedCarePoints = (int)Math.Min((long)active.AccumulatedCarePoints + units, int.MaxValue);
            // Deliberately does NOT advance CurrentStage. Evolution used to fire the instant
            // the threshold was crossed, which took the moment away from the player — it now
            // waits for an explicit "Эволюционировать!" press, mirroring how a ready egg waits
            // for "Вылупить!". See IsReadyToEvolve/TryEvolve.
        }

        Activity.AddUnits(units);
    }

    /// Whether this individual has banked enough Care Points to advance a stage. Purely a
    /// query — it never mutates the individual, so UI can poll it every refresh tick to decide
    /// whether the evolve button is enabled.
    public bool IsReadyToEvolve(PokemonIndividual individual) =>
        EvolutionCalculator.StageForPoints(individual.AccumulatedCarePoints, individual.ChainLength) > individual.CurrentStage;

    /// Applies the pending evolution, if there is one. Returns false (and changes nothing)
    /// when the individual isn't ready, so a stale button click can't skip a stage or
    /// double-evolve.
    public bool TryEvolve(PokemonIndividual individual)
    {
        var nextStage = EvolutionCalculator.StageForPoints(individual.AccumulatedCarePoints, individual.ChainLength);
        if (nextStage <= individual.CurrentStage) return false;

        individual.CurrentStage = nextStage;
        return true;
    }

    /// Sets (or clears) which already-reached stage's sprite is cosmetically shown for this
    /// individual. No-op for an out-of-range index — negative, or past CurrentStage — since
    /// displaying a stage never reached would leak an evolution the player hasn't earned yet.
    /// Passing CurrentStage's own index normalizes to null; the two mean the same thing and
    /// null is the cheaper one to check at every read site.
    public void SetDisplayStage(PokemonIndividual individual, int? stageIndex)
    {
        if (stageIndex is not { } index) { individual.DisplayStageOverride = null; return; }
        if (index < 0 || index > (int)individual.CurrentStage) return;

        individual.DisplayStageOverride = index == (int)individual.CurrentStage ? null : index;
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

    /// <summary>
    /// Populates this AppState from a loaded SaveData: the single source of truth for
    /// "how a SaveData becomes a live AppState." Callers should use this instead of
    /// hand-copying individual properties, so a newly added AppSettings property is never
    /// silently missed at load time.
    /// </summary>
    public void Hydrate(SaveData data)
    {
        foreach (var individual in data.Individuals)
        {
            // Rescue individuals caught under the old hand-authored offline starter pool,
            // whose invented chain ids collide with unrelated REAL PokeAPI chains now that the
            // bundled catalog is the real data. Safe to run unconditionally on every load: it
            // only fires on an exact (SpeciesId, ChainLength, EvolutionChainId) match, and is
            // idempotent because the replacement ids are outside the matched set.
            LegacyOfflineChains.TryMigrate(individual);
            Collection.Add(individual);
        }

        if (data.ActiveIndividualId is { } activeId &&
            data.Individuals.Any(i => i.Id == activeId))
        {
            Collection.SetActive(activeId);
        }
        else if (data.ActiveIndividualId is null)
        {
            Collection.ClearActive();
        }

        Settings.CareMode = data.Settings.CareMode;
        Settings.AutoHideInFullscreen = data.Settings.AutoHideInFullscreen;
        Settings.StartWithWindows = data.Settings.StartWithWindows;
        Settings.ScreenCorner = data.Settings.ScreenCorner;
        Settings.MonitorIndex = data.Settings.MonitorIndex;
        Settings.PetSizePixels = data.Settings.PetSizePixels;
        Settings.ShowProgressPanel = data.Settings.ShowProgressPanel;
        Settings.SuperHardcoreEnabled = data.Settings.SuperHardcoreEnabled;
        Settings.CrySoundVolume = data.Settings.CrySoundVolume;
        Settings.PetPositionX = data.Settings.PetPositionX;
        Settings.PetPositionY = data.Settings.PetPositionY;

        Activity.UnitsSinceLastCatchCheck = data.CatchAccumulatorUnits;

        Feeding.Treats = data.FeedingTreats;
        Feeding.RemainderUnits = data.FeedingRemainderUnits;

        Shop.Coins = data.ShopCoins;
        Shop.RemainderUnits = data.ShopRemainderUnits;
        Shop.CatchLuckyCharmActive = data.ShopCatchLuckyCharmActive;

        foreach (var (individualId, careState) in data.CareStates)
        {
            CareStateByIndividualId[individualId] = careState;
        }

        PendingEggs.AddRange(data.PendingEggs);
    }
}
