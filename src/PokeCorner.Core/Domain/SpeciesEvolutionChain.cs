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

    /// PokeAPI type names for each stage ("electric", "bug"/"flying"), positionally aligned with
    /// <see cref="StageSpeciesIds"/>. Normalized like StageSpeciesNames — inner lists too — so a
    /// catalog without types degrades to "no badges" instead of a null reaching the UI.
    public IReadOnlyList<IReadOnlyList<string>> StageTypes { get; init; } =
        StageTypes?.Select(types => types ?? (IReadOnlyList<string>)Array.Empty<string>()).ToList()
        ?? (IReadOnlyList<IReadOnlyList<string>>)Array.Empty<IReadOnlyList<string>>();
}

public sealed record CatchResult(int ChainId, int SpeciesId, Nature Nature, bool IsShiny, bool WasDuplicateSpecies, int ChainLength)
{
    public PokemonIndividual ToIndividual(DateTimeOffset caughtAtUtc) => new()
    {
        SpeciesId = SpeciesId,
        EvolutionChainId = ChainId,
        ChainLength = ChainLength,
        Nature = Nature,
        IsShiny = IsShiny,
        CaughtAtUtc = caughtAtUtc
    };
}

public static class SpeciesEvolutionChainExtensions
{
    /// The species to draw for an individual: its chain's species at the current stage, or at
    /// DisplayStageOverride when that stays within [0, CurrentStage] (a hand-edited save is a
    /// trust boundary). Falls back to the caught species when the chain isn't in the catalog.
    public static int ResolveDisplaySpeciesId(this PokemonIndividual individual, IReadOnlyList<SpeciesEvolutionChain> lookupChains)
    {
        if (individual.CurrentSpeciesId > 0)
            return individual.DisplayStageOverride is { } reachedIndex && reachedIndex >= 0 &&
                individual.ReachedSpeciesIds is { } reached && reachedIndex < reached.Count
                ? reached[reachedIndex] : individual.CurrentSpeciesId;

        var chain = lookupChains.FirstOrDefault(c => c.ChainId == individual.EvolutionChainId);
        if (chain is null) return individual.SpeciesId;

        var stageIndex = individual.DisplayStageOverride is { } overrideIndex && overrideIndex >= 0 && overrideIndex <= (int)individual.CurrentStage
            ? overrideIndex
            : (int)individual.CurrentStage;
        return stageIndex < chain.StageSpeciesIds.Count ? chain.StageSpeciesIds[stageIndex] : individual.SpeciesId;
    }

    /// Resolves a species' display name from the chain that contains it, at the same stage
    /// index. Falls back to "Pokemon #{id}" when the species is in no available chain.
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
