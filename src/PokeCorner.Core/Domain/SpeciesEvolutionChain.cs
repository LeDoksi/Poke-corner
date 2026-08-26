namespace PokeCorner.Core.Domain;

public sealed record SpeciesEvolutionChain(
    int ChainId,
    int StarterSpeciesId,
    int CaptureRate,
    int ChainLength,
    IReadOnlyList<int> StageSpeciesIds,
    IReadOnlyList<string> StageSpeciesNames)
{
    /// Display names for each stage, positionally aligned with <see cref="StageSpeciesIds"/>.
    /// Normalized to an empty list when absent so an older catalog.json written before names
    /// were captured (System.Text.Json passes null for the missing property) can still be
    /// deserialized and consumed without a NullReferenceException at every lookup site.
    public IReadOnlyList<string> StageSpeciesNames { get; init; } = StageSpeciesNames ?? Array.Empty<string>();

    public IReadOnlyList<int> StageSpeciesIds { get; init; } = StageSpeciesIds ?? Array.Empty<int>();
}

public sealed record CatchResult(int ChainId, int SpeciesId, Nature Nature, bool IsShiny, bool WasDuplicateSpecies, int ChainLength);

public static class SpeciesEvolutionChainExtensions
{
    /// Resolves a species' display name from catalog data by finding the chain that contains
    /// the species and reading the name at the same stage index. Replaces the retired
    /// hand-translated SpeciesNames dictionary: the bundled catalog now carries PokeAPI's own
    /// English names for every species, so there is nothing left to maintain by hand.
    /// Falls back to "Pokemon #{id}" only when the species is genuinely absent from every
    /// available chain (shouldn't happen with the full bundled catalog, but never crashes).
    public static string ResolveSpeciesName(this IReadOnlyList<SpeciesEvolutionChain> chains, int speciesId)
    {
        foreach (var chain in chains)
        {
            for (var stage = 0; stage < chain.StageSpeciesIds.Count; stage++)
            {
                if (chain.StageSpeciesIds[stage] != speciesId) continue;
                if (stage < chain.StageSpeciesNames.Count && !string.IsNullOrWhiteSpace(chain.StageSpeciesNames[stage]))
                {
                    return chain.StageSpeciesNames[stage];
                }
            }
        }

        return $"Pokemon #{speciesId}";
    }
}
