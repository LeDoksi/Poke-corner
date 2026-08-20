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
        Settings.TokenTrackingEnabled = data.Settings.TokenTrackingEnabled;
        Settings.ActivityTrackingEnabled = data.Settings.ActivityTrackingEnabled;
        Settings.AutoHideInFullscreen = data.Settings.AutoHideInFullscreen;
        Settings.StartWithWindows = data.Settings.StartWithWindows;
        Settings.ScreenCorner = data.Settings.ScreenCorner;
        Settings.MonitorIndex = data.Settings.MonitorIndex;
        Settings.PetSizePixels = data.Settings.PetSizePixels;
        Settings.ShowProgressPanel = data.Settings.ShowProgressPanel;

        Feeding.Treats = data.FeedingTreats;

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
