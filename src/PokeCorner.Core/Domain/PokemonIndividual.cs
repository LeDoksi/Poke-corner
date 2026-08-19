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
