using PokeCorner.Core.Persistence;

namespace PokeCorner.Core.Domain;

public sealed class ExpeditionState : TimedActivity
{
    public string LocationId { get; set; } = "";
    public string Difficulty { get; set; } = "";
    public long ActivityActions { get; set; }
    public decimal TraitTimeReduction { get; set; }
    public decimal CollectorBoost { get; set; }
    public decimal RarityBoost { get; set; } = 1;
    public HashSet<int> KnownSpeciesIdsAtStart { get; set; } = [];
    public ExpeditionRewards Rewards { get; set; } = new();
    public Guid? ClaimOperationId { get; set; }
}
