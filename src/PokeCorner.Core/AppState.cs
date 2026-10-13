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
    public List<EncounterState> Encounters { get; } = new();
    public bool ActivityNewGuaranteed { get; private set; }

    private SaveData? _metadata;
    private readonly Queue<Action> _deferredMutations = new();
    private readonly List<Guid> _pendingActivityEncounterIds = new();
    private TaskCompletionSource? _idle;
    public bool IsCommitting { get; private set; }
    public Task WhenIdle => _idle?.Task ?? Task.CompletedTask;
    public int PendingActivityEncounterCount => _pendingActivityEncounterIds.Count;
    public bool HasRunningExpedition => _metadata?.Expedition?.Status == "running";
    public DateTimeOffset? ExpeditionReadyAtUtc => _metadata?.Expedition?.Status == "running" ? _metadata.Expedition.ReadyAtUtc : null;
    public IReadOnlySet<Guid> BusyIndividualIds => _metadata is null ? new HashSet<Guid>() : ExpeditionService.BusyIndividualIds(_metadata);

    public void QueueActivityEncounter(Guid operationId)
    {
        if (operationId == Guid.Empty) throw new ArgumentException("Operation ID is required.", nameof(operationId));
        if (!_pendingActivityEncounterIds.Contains(operationId)) _pendingActivityEncounterIds.Add(operationId);
    }

    public async Task CommitQueuedActivityEncountersAsync(BackgroundSaveWriter writer, EncounterService service,
        Func<int> pendingCatchCount, Action<SaveData>? afterPublish = null)
    {
        var ids = _pendingActivityEncounterIds.ToArray();
        if (ids.Length == 0) return;
        await CommitAsync(writer, next =>
        {
            foreach (var id in ids) service.Reserve(next, "activity", id);
        }, pendingCatchCount, afterPublish);
        _pendingActivityEncounterIds.RemoveAll(ids.Contains);
    }

    public SaveData ToSaveDataIncludingPendingActivity(EncounterService service, int pendingCatchCount)
    {
        var next = ToSaveData(pendingCatchCount);
        foreach (var id in _pendingActivityEncounterIds) service.Reserve(next, "activity", id);
        return next;
    }

    public void RunWhenReady(Action mutation)
    {
        if (IsCommitting) _deferredMutations.Enqueue(mutation);
        else mutation();
    }

    public async Task CommitAsync(BackgroundSaveWriter writer, Action<SaveData> update, Func<int> pendingCatchCount, Action<SaveData>? afterPublish = null, SaveData? replacement = null)
    {
        while (IsCommitting) await WhenIdle;
        IsCommitting = true;
        var idle = _idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var next = replacement is null ? ToSaveData(pendingCatchCount()) : JsonSaveStore.Clone(replacement);
            update(next);
            if (next.SchemaVersion == 2) next.Items["treats"] = next.FeedingTreats;
            JsonSaveStore.ValidateData(next);
            await writer.CommitAndPublishAsync(next, () => { Hydrate(next); afterPublish?.Invoke(next); });
        }
        finally
        {
            IsCommitting = false;
            try
            {
                while (!IsCommitting && _deferredMutations.TryDequeue(out var mutation)) mutation();
            }
            finally { idle.TrySetResult(); }
        }
    }

    public async Task FlushAsync(BackgroundSaveWriter writer, Func<int> pendingCatchCount)
    {
        while (IsCommitting) await WhenIdle;
        await writer.CommitAsync(ToSaveData(pendingCatchCount()));
    }

    public event Action? CatchRollDue;

    public AppState()
    {
        Activity.CatchCheckDue += () => CatchRollDue?.Invoke();
    }

    public void ApplyActivityUnits(long units)
    {
        if (IsCommitting) { RunWhenReady(() => ApplyActivityUnits(units)); return; }
        ArgumentOutOfRangeException.ThrowIfNegative(units);
        var active = Collection.GetActive();
        if (active is not null)
        {
            active.AccumulatedCarePoints = (int)Math.Min((long)active.AccumulatedCarePoints + units, int.MaxValue);
            // Deliberately does NOT advance CurrentStage: evolving waits for an explicit
            // "Эволюционировать!" press, like a ready egg waits for "Вылупить!" (IsReadyToEvolve/TryEvolve).
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
        if (IsCommitting) return false;
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
        if (IsCommitting)
        {
            var id = individual.Id;
            RunWhenReady(() =>
            {
                var current = Collection.Individuals.FirstOrDefault(i => i.Id == id);
                if (current is not null) SetDisplayStage(current, stageIndex);
            });
            return;
        }
        if (stageIndex is not { } index) { individual.DisplayStageOverride = null; return; }
        if (index < 0 || index > (int)individual.CurrentStage
            || individual.CurrentSpeciesId > 0 && index >= individual.ReachedSpeciesIds.Count) return;

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
        _metadata = JsonSaveStore.Clone(data);
        foreach (var existing in Collection.Individuals.ToArray()) Collection.Release(existing.Id);
        CareStateByIndividualId.Clear();
        PendingEggs.Clear();
        Encounters.Clear();
        Encounters.AddRange(data.Encounters);
        ActivityNewGuaranteed = data.ActivityNewGuaranteed;
        foreach (var individual in data.Individuals)
        {
            // Rescue individuals caught under the early hand-authored offline pool, whose invented chain
            // ids collide with real PokeAPI chains. Safe on every load: it only fires on an exact
            // (SpeciesId, ChainLength, EvolutionChainId) match and is idempotent.
            if (data.SchemaVersion == 1) LegacyOfflineChains.TryMigrate(individual);
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

        Settings.CareMode = CareMode.Chill;
        Settings.AutoHideInFullscreen = data.Settings.AutoHideInFullscreen;
        Settings.StartWithWindows = data.Settings.StartWithWindows;
        Settings.ScreenCorner = data.Settings.ScreenCorner;
        Settings.MonitorIndex = data.Settings.MonitorIndex;
        Settings.PetSizePixels = data.Settings.PetSizePixels;
        Settings.ShowProgressPanel = data.Settings.ShowProgressPanel;
        Settings.SuperHardcoreEnabled = false;
        Settings.CrySoundVolume = data.Settings.CrySoundVolume;
        Settings.EffectsSoundVolume = data.Settings.EffectsSoundVolume;
        Settings.PetPositionX = data.Settings.PetPositionX;
        Settings.PetPositionY = data.Settings.PetPositionY;

        Activity.UnitsSinceLastCatchCheck = data.CatchAccumulatorUnits;

        Feeding.Treats = data.FeedingTreats;
        Feeding.RemainderUnits = data.FeedingRemainderUnits;

        Shop.Coins = data.ShopCoins;
        Shop.RemainderUnits = data.ShopRemainderUnits;
        Shop.CatchLuckyCharmActive = data.ShopCatchLuckyCharmActive;
        Shop.Berries.Clear();
        foreach (var (id, count) in data.Berries) Shop.Berries[id] = count;
        Shop.Items.Clear();
        foreach (var (id, count) in data.Items) Shop.Items[id] = count;

        foreach (var (individualId, careState) in data.CareStates)
        {
            CareStateByIndividualId[individualId] = new CareState
            {
                LastInteractionUtc = careState.LastInteractionUtc,
                LastNeglectCheckUtc = careState.LastNeglectCheckUtc,
                LastPetUtc = careState.LastPetUtc
            };
        }

        PendingEggs.AddRange(data.PendingEggs);
    }

    /// The other half of <see cref="Hydrate"/>; keep the two field lists in step (the
    /// round-trip test fails when they drift). pendingCatchCount lives in the App layer.
    public SaveData ToSaveData(int pendingCatchCount)
    {
        var data = _metadata is null ? new SaveData { Individuals = [], Settings = new() } : JsonSaveStore.Clone(_metadata);
        data.Individuals = Collection.Individuals.ToList();
        data.ActiveIndividualId = Collection.ActiveIndividualId;
        data.Settings = Settings;
        data.FeedingTreats = Feeding.Treats;
        data.CatchAccumulatorUnits = Activity.UnitsSinceLastCatchCheck;
        data.FeedingRemainderUnits = Feeding.RemainderUnits;
        data.ShopCoins = Shop.Coins;
        data.ShopRemainderUnits = Shop.RemainderUnits;
        data.ShopCatchLuckyCharmActive = Shop.CatchLuckyCharmActive;
        data.Berries = new(Shop.Berries);
        data.Items = new(Shop.Items);
        data.PendingEggs = PendingEggs.ToList();
        data.Encounters = Encounters.ToList();
        data.ActivityNewGuaranteed = ActivityNewGuaranteed;
        data.CareStates = new(CareStateByIndividualId);
        data.PendingCatchCount = pendingCatchCount;
        if (data.SchemaVersion == 2)
        {
            data.Items["treats"] = data.FeedingTreats;
            foreach (var individual in data.Individuals)
            {
                data.DiscoveredSpeciesIds.UnionWith(individual.ReachedSpeciesIds);
                if (individual.IsShiny) data.ShinySpeciesIds.UnionWith(individual.ReachedSpeciesIds);
            }
            _metadata!.DiscoveredSpeciesIds.UnionWith(data.DiscoveredSpeciesIds);
            _metadata.ShinySpeciesIds.UnionWith(data.ShinySpeciesIds);
        }
        return JsonSaveStore.Clone(data);
    }
}
