namespace PokeCorner.Core.Domain;

public enum EvolutionStage { Stage1 = 0, Stage2 = 1, Stage3 = 2 }

public sealed class PokemonIndividual
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required int SpeciesId { get; init; }
    private int _evolutionChainId;

    /// The individual's evolution chain, fixed at catch time. Backed by a field purely so
    /// <see cref="RemapEvolutionChainId"/> can exist — an init-only accessor cannot be called
    /// from an instance method, and the legacy-offline-pool migration has to rewrite this
    /// value in place on load (see <see cref="LegacyOfflineChains"/>).
    public required int EvolutionChainId
    {
        get => _evolutionChainId;
        init => _evolutionChainId = value;
    }

    /// The ONLY mutation path for <see cref="EvolutionChainId"/>, and deliberately internal:
    /// nothing outside Core should be rewriting an individual's chain, and the one caller is
    /// the save-load migration in <see cref="LegacyOfflineChains.TryMigrate"/>.
    internal void RemapEvolutionChainId(int chainId) => _evolutionChainId = chainId;
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
