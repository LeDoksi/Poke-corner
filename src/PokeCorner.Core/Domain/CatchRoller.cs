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
        IReadOnlySet<int> ownedChainIds,
        IReadOnlySet<Nature> ownedNaturesForDuplicateChain)
    {
        if (allChains.Count == 0)
        {
            throw new ArgumentException("At least one species chain is required to roll a catch.", nameof(allChains));
        }

        var chain = PickChain(allChains, ownedChainIds);
        var isDuplicate = ownedChainIds.Contains(chain.ChainId);

        var isShiny = _random.NextDouble() < (isDuplicate ? BaseShinyChance * DuplicateShinyChanceMultiplier : BaseShinyChance);
        var nature = isDuplicate
            ? PickNatureBiasedTowardUnowned(ownedNaturesForDuplicateChain)
            : PickUniformNature();

        return new CatchResult(chain.ChainId, chain.StarterSpeciesId, nature, isShiny, isDuplicate, chain.ChainLength);
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
