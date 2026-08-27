namespace PokeCorner.Core.Domain;

public sealed record SpeciesEvolutionChain(
    int ChainId,
    int StarterSpeciesId,
    int CaptureRate,
    int ChainLength,
    IReadOnlyList<int> StageSpeciesIds,
    IReadOnlyList<string> StageSpeciesNames,
    /// Defaulted rather than required so the 9 hand-authored rescue chains in
    /// LegacyOfflineChains and the test fixtures that predate types keep compiling unchanged —
    /// the normalization below turns the omission into an empty list, which every consumer
    /// already has to handle anyway for old on-disk catalogs.
    IReadOnlyList<IReadOnlyList<string>>? StageTypes = null)
{
    /// Display names for each stage, positionally aligned with <see cref="StageSpeciesIds"/>.
    /// Normalized to an empty list when absent so an older catalog.json written before names
    /// were captured (System.Text.Json passes null for the missing property) can still be
    /// deserialized and consumed without a NullReferenceException at every lookup site.
    public IReadOnlyList<string> StageSpeciesNames { get; init; } = StageSpeciesNames ?? Array.Empty<string>();

    public IReadOnlyList<int> StageSpeciesIds { get; init; } = StageSpeciesIds ?? Array.Empty<int>();

    /// PokeAPI type names for each stage ("electric", "bug"/"flying"), positionally aligned
    /// with <see cref="StageSpeciesIds"/>. Most species have one type, some have two.
    ///
    /// Normalized exactly like StageSpeciesNames and for exactly the same reason: a
    /// catalog.json written before types were captured deserializes with this property missing,
    /// and an empty list must degrade to "no badges" rather than crashing every card that
    /// renders one. The inner lists are normalized too — a null element in the JSON array
    /// would otherwise reach the UI as a null enumeration.
    public IReadOnlyList<IReadOnlyList<string>> StageTypes { get; init; } =
        StageTypes?.Select(types => types ?? (IReadOnlyList<string>)Array.Empty<string>()).ToList()
        ?? (IReadOnlyList<IReadOnlyList<string>>)Array.Empty<IReadOnlyList<string>>();
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

    /// Resolves a species' PokeAPI type names the same way <see cref="ResolveSpeciesName"/>
    /// resolves its display name: find the chain containing the species, read the entry at the
    /// same stage index.
    ///
    /// Returns an EMPTY list rather than a placeholder when the species is absent or the
    /// catalog predates type capture — the UI renders no badges at all in that case, which is
    /// the honest reading. There is no equivalent of the "Pokemon #{id}" fallback here: a made
    /// up type would be a factual claim, whereas a made up name is only a cosmetic one.
    public static IReadOnlyList<string> ResolveSpeciesTypes(this IReadOnlyList<SpeciesEvolutionChain> chains, int speciesId)
    {
        foreach (var chain in chains)
        {
            for (var stage = 0; stage < chain.StageSpeciesIds.Count; stage++)
            {
                if (chain.StageSpeciesIds[stage] != speciesId) continue;
                if (stage < chain.StageTypes.Count && chain.StageTypes[stage].Count > 0)
                {
                    return chain.StageTypes[stage];
                }
            }
        }

        return Array.Empty<string>();
    }
}
