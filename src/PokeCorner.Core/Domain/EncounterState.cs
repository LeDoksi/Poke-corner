namespace PokeCorner.Core.Domain;

public sealed class EncounterState
{
    public Guid Id { get; set; }
    public string Source { get; set; } = "activity";
    public string Phase { get; set; } = "sealed";
    public DateTimeOffset ReservedAtUtc { get; set; }
    public int? KnownSpeciesId { get; set; }
    public Guid? GiftIndividualId { get; set; }
    public PokemonIndividual? Individual { get; set; }
    public RarityTier? Rarity { get; set; }
    public bool WasDuplicateSpecies { get; set; }
}

public sealed record EncounterOpenResult(PokemonIndividual Individual, RarityTier Rarity, bool WasDuplicateSpecies, bool Repeated);
public sealed record EncounterResolvedResult(Guid Id, int SpeciesId, bool IsShiny, RarityTier Rarity, bool IsClosed);
