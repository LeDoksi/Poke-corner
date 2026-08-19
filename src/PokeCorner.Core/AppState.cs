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
