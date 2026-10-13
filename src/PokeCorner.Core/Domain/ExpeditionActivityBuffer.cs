using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.Core.Domain;

// UI-thread buffer for activity below the expedition's 500-action acceleration step.
public sealed class ExpeditionActivityBuffer
{
    private Guid? tripId;
    public long PendingActions { get; private set; }

    public void Sync(Guid? runningTripId)
    {
        if (tripId == runningTripId) return;
        tripId = runningTripId;
        PendingActions = 0;
    }

    public void Credit(Guid? runningTripId, long actions)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(actions);
        Sync(runningTripId);
        if (tripId is not null) PendingActions = checked(PendingActions + actions);
    }

    public void ApplyTo(SaveData candidate, CatalogPackage catalog, DateTimeOffset now)
    {
        if (candidate.Expedition is not { Status: "running" } trip || trip.Id != tripId) return;
        var service = new ExpeditionService(catalog, candidate);
        if (PendingActions > 0) service.ApplyActivityActions(PendingActions, now);
        service.RefreshReady(now);
    }

    public async Task CommitAsync(AppState state, BackgroundSaveWriter writer, CatalogPackage catalog,
        Func<int> pendingCatchCount, Action<SaveData>? afterPublish, DateTimeOffset now)
    {
        Sync(state.RunningExpeditionId);
        var committedTrip = tripId;
        var committedActions = PendingActions;
        await state.CommitAsync(writer, candidate =>
        {
            if (candidate.Expedition?.Id != committedTrip) return;
            var service = new ExpeditionService(catalog, candidate);
            if (committedActions > 0) service.ApplyActivityActions(committedActions, now);
            service.RefreshReady(now);
        }, pendingCatchCount, afterPublish);
        if (tripId == committedTrip) PendingActions -= committedActions;
        Sync(state.RunningExpeditionId);
    }
}
