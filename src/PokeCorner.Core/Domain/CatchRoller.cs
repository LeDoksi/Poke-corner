namespace PokeCorner.Core.Domain;

public sealed class CatchRoller
{
    private const double DuplicateChainWeightPenalty = 0.15;
    private const double BaseShinyChance = 1.0 / 512.0;
    private const double DuplicateShinyChanceMultiplier = 4.0;
    private const double DuplicateOwnedNatureWeight = 0.3;
    private const double DuplicateUnownedNatureWeight = 1.0;

    private readonly Random _random;

    public CatchRoller(Random? random = null) => _random = random ?? Random.Shared;

    public CatchResult RollCatch(
        IReadOnlyList<SpeciesEvolutionChain> allChains,
        IReadOnlyCollection<PokemonIndividual> owned,
        double shinyChanceMultiplier = 1.0)
    {
        if (allChains.Count == 0)
        {
            throw new ArgumentException("At least one species chain is required to roll a catch.", nameof(allChains));
        }

        var ownedChainIds = owned.Select(i => i.EvolutionChainId).ToHashSet();
        var chain = PickChain(allChains, ownedChainIds);
        var isDuplicate = ownedChainIds.Contains(chain.ChainId);
        // Only the PICKED chain's natures count as "already owned" — which chain that is isn't
        // known until PickChain returns, so callers can't pre-filter this (they used to try, and
        // ended up passing every nature in the collection).
        var ownedNaturesForDuplicateChain = owned.Where(i => i.EvolutionChainId == chain.ChainId).Select(i => i.Nature).ToHashSet();

        var isShiny = _random.NextDouble() < (isDuplicate ? BaseShinyChance * DuplicateShinyChanceMultiplier : BaseShinyChance) * shinyChanceMultiplier;
        var nature = isDuplicate
            ? PickNatureBiasedTowardUnowned(ownedNaturesForDuplicateChain)
            : PickUniformNature();

        return new CatchResult(chain.ChainId, chain.StarterSpeciesId, nature, isShiny, isDuplicate, chain.ChainLength);
    }

    /// Guaranteed-tier roll for a shop egg. Returns false — without calling
    /// consumeShinyMultiplier — when the catalog has no species of that tier, so a lucky charm
    /// isn't spent on a hatch that can't happen; otherwise consumes it exactly once and rolls.
    public bool TryRollCatchFromTier(IReadOnlyList<SpeciesEvolutionChain> allChains, RarityTier tier, IReadOnlyCollection<PokemonIndividual> owned, Func<double> consumeShinyMultiplier, out CatchResult result)
    {
        var tierChains = allChains.Where(c => RarityClassifier.Classify(c.CaptureRate) == tier).ToList();
        if (tierChains.Count == 0)
        {
            result = null!;
            return false;
        }

        result = RollCatch(tierChains, owned, consumeShinyMultiplier());
        return true;
    }

    // Used for starter selection: the player has already picked the species, so this
    // skips the weighted chain-picking logic entirely and just rolls nature/shiny for the
    // one chain they chose. Always WasDuplicateSpecies: false — it's necessarily the very
    // first individual of a brand-new collection.
    public CatchResult RollFromChain(SpeciesEvolutionChain chain)
    {
        var isShiny = _random.NextDouble() < BaseShinyChance;
        var nature = PickUniformNature();
        return new CatchResult(chain.ChainId, chain.StarterSpeciesId, nature, isShiny, WasDuplicateSpecies: false, chain.ChainLength);
    }

    private SpeciesEvolutionChain PickChain(IReadOnlyList<SpeciesEvolutionChain> allChains, IReadOnlySet<int> ownedChainIds)
    {
        var weights = allChains
            .Select(chain => (chain, weight: chain.CaptureRate * (ownedChainIds.Contains(chain.ChainId) ? DuplicateChainWeightPenalty : 1.0)))
            .ToList();

        var totalWeight = weights.Sum(w => w.weight);
        var roll = _random.NextDouble() * totalWeight;
        var cumulative = 0.0;
        foreach (var (chain, weight) in weights)
        {
            cumulative += weight;
            if (roll <= cumulative)
            {
                return chain;
            }
        }
        return weights[^1].chain;
    }

    private Nature PickUniformNature()
    {
        var values = Enum.GetValues<Nature>();
        return values[_random.Next(values.Length)];
    }

    private Nature PickNatureBiasedTowardUnowned(IReadOnlySet<Nature> ownedNatures)
    {
        var values = Enum.GetValues<Nature>();
        var weights = values.Select(n => ownedNatures.Contains(n) ? DuplicateOwnedNatureWeight : DuplicateUnownedNatureWeight).ToArray();
        var totalWeight = weights.Sum();
        var roll = _random.NextDouble() * totalWeight;
        var cumulative = 0.0;
        for (var i = 0; i < values.Length; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
            {
                return values[i];
            }
        }
        return values[^1];
    }
}
