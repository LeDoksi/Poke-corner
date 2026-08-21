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

    /// Player-pinned "favorite" flag. Mutable (not init-only) because it is toggled from the
    /// roster long after the individual was created, and a plain public setter is all
    /// System.Text.Json's reflection-based (de)serializer needs to round-trip it through the
    /// save file — no schema-migration code required, older saves simply deserialize to false.
    public bool IsFavorite { get; set; }
}
