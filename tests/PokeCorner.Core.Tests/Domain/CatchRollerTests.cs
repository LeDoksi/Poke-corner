using PokeCorner.Core.Domain;
using Xunit;

namespace PokeCorner.Core.Tests.Domain;

public class CatchRollerTests
{
    private static readonly SpeciesEvolutionChain CommonUnowned = new(ChainId: 1, StarterSpeciesId: 10, CaptureRate: 255, ChainLength: 3, StageSpeciesIds: new[] { 10 }, StageSpeciesNames: new[] { "Caterpie" });
    private static readonly SpeciesEvolutionChain RareOwned = new(ChainId: 2, StarterSpeciesId: 20, CaptureRate: 3, ChainLength: 3, StageSpeciesIds: new[] { 20 }, StageSpeciesNames: new[] { "Raticate" });

    [Fact]
    public void RollCatch_UnownedChainWeighsMuchHigherThanOwnedChain()
    {
        // With one common unowned chain and one rare-but-owned chain of equal base
        // capture rate, the unowned one should win the overwhelming majority of rolls
        // because owned chains are penalized (spec: "виды, которых ещё нет, весят больше").
        var equalRateOwned = RareOwned with { CaptureRate = 255 };
        var chains = new[] { CommonUnowned, equalRateOwned };
        var owned = new[] { Owned(equalRateOwned.ChainId) };
        var roller = new CatchRoller(new Random(Seed: 1));

        var unownedWins = 0;
        for (var i = 0; i < 1000; i++)
        {
            var result = roller.RollCatch(chains, owned);
            if (result.ChainId == CommonUnowned.ChainId) unownedWins++;
        }

        Assert.True(unownedWins > 800, $"Expected unowned chain to win the large majority of rolls, got {unownedWins}/1000");
    }

    [Fact]
    public void RollCatch_OnlyOwnedChainsAvailable_StillProducesDuplicate()
    {
        var chains = new[] { CommonUnowned };
        var owned = new[] { Owned(CommonUnowned.ChainId) };
        var roller = new CatchRoller(new Random(Seed: 2));

        var result = roller.RollCatch(chains, owned);

        Assert.Equal(CommonUnowned.ChainId, result.ChainId);
        Assert.True(result.WasDuplicateSpecies);
    }

    [Fact]
    public void RollCatch_Duplicate_ShinyChanceIsHigherThanNewCatch()
    {
        var chains = new[] { CommonUnowned };
        var owned = new[] { Owned(CommonUnowned.ChainId) };

        var newCatchShinyCount = CountShiny(chains, owned: Array.Empty<PokemonIndividual>(), trials: 20_000, seed: 3);
        var duplicateShinyCount = CountShiny(chains, owned, trials: 20_000, seed: 4);

        Assert.True(duplicateShinyCount > newCatchShinyCount,
            $"Expected duplicate shiny rate ({duplicateShinyCount}) > new-catch shiny rate ({newCatchShinyCount})");
    }

    [Fact]
    public void RollCatch_Duplicate_PrefersNatureNotYetOwnedForThatSpecies()
    {
        var chains = new[] { CommonUnowned };
        var owned = Enum.GetValues<Nature>().Where(n => n != Nature.Jolly).Select(n => Owned(CommonUnowned.ChainId, n)).ToList();
        var roller = new CatchRoller(new Random(Seed: 5));

        var jollyCount = 0;
        for (var i = 0; i < 500; i++)
        {
            var result = roller.RollCatch(chains, owned);
            if (result.Nature == Nature.Jolly) jollyCount++;
        }

        // Jolly is the only not-yet-owned nature for this species; the duplicate bias
        // should make it come up far more often than the 1/25 uniform baseline (~20).
        Assert.True(jollyCount > 35, $"Expected Jolly to come up far more than the uniform baseline, got {jollyCount}/500");
    }

    [Fact]
    public void RollCatch_ShinyChanceMultiplier_IncreasesShinyRate()
    {
        var chains = new[] { CommonUnowned };
        var owned = Array.Empty<PokemonIndividual>();

        var baselineShinyCount = CountShiny(chains, owned, trials: 20_000, seed: 10, shinyChanceMultiplier: 1.0);
        var boostedShinyCount = CountShiny(chains, owned, trials: 20_000, seed: 11, shinyChanceMultiplier: 4.0);

        Assert.True(boostedShinyCount > baselineShinyCount,
            $"Expected boosted shiny rate ({boostedShinyCount}) > baseline shiny rate ({baselineShinyCount})");
    }

    [Fact]
    public void RollCatchFromTier_OnlyPicksFromMatchingTier()
    {
        var legendary = CommonUnowned with { ChainId = 99, CaptureRate = 3 }; // RarityTier.Legendary
        var chains = new[] { CommonUnowned, legendary }; // CommonUnowned is CaptureRate 255 -> RarityTier.Common
        var roller = new CatchRoller(new Random(Seed: 20));

        for (var i = 0; i < 100; i++)
        {
            var result = roller.RollCatchFromTier(chains, RarityTier.Legendary, Array.Empty<PokemonIndividual>());
            Assert.Equal(legendary.ChainId, result.ChainId);
        }
    }

    [Fact]
    public void RollCatchFromTier_NoChainsInTier_Throws()
    {
        var chains = new[] { CommonUnowned }; // only RarityTier.Common available
        var roller = new CatchRoller(new Random(Seed: 21));

        Assert.Throws<InvalidOperationException>(() =>
            roller.RollCatchFromTier(chains, RarityTier.Legendary, Array.Empty<PokemonIndividual>()));
    }

    [Fact]
    public void RollFromChain_ReturnsResultForGivenChainAndIsNeverADuplicate()
    {
        var roller = new CatchRoller(new Random(Seed: 30));

        var result = roller.RollFromChain(CommonUnowned);

        Assert.Equal(CommonUnowned.ChainId, result.ChainId);
        Assert.Equal(CommonUnowned.StarterSpeciesId, result.SpeciesId);
        Assert.False(result.WasDuplicateSpecies);
    }

    [Fact]
    public void RollCatch_DuplicateNatureBias_UsesOnlyThePickedChainsNatures()
    {
        // The picked chain owns only Hardy; a DIFFERENT chain owns every other nature. Only the
        // picked chain's natures may count as "already owned", so Hardy must be the rare one —
        // the old caller-side filter fed every owned nature in, which made Hardy the favourite.
        var otherChain = RareOwned.ChainId;
        var owned = Enum.GetValues<Nature>().Where(n => n != Nature.Hardy).Select(n => Owned(otherChain, n))
            .Append(Owned(CommonUnowned.ChainId, Nature.Hardy))
            .ToList();
        var roller = new CatchRoller(new Random(Seed: 42));

        var hardyCount = 0;
        const int trials = 5000;
        for (var i = 0; i < trials; i++)
        {
            if (roller.RollCatch(new[] { CommonUnowned }, owned).Nature == Nature.Hardy) hardyCount++;
        }

        // Correct: Hardy weighs 0.3 against 24 x 1.0 → ~1.2%. Old (all owned natures counted): all 25
        // weigh 0.3 alike → uniform 4%.
        Assert.True(hardyCount / (double)trials < 0.025, $"Expected Hardy (owned on the picked chain) to be rare, got {hardyCount}/{trials}");
    }

    private static PokemonIndividual Owned(int chainId, Nature nature = Nature.Hardy) => new()
    {
        SpeciesId = 1,
        EvolutionChainId = chainId,
        ChainLength = 3,
        Nature = nature,
        IsShiny = false,
        CaughtAtUtc = DateTimeOffset.UnixEpoch
    };

    private static int CountShiny(IReadOnlyList<SpeciesEvolutionChain> chains, IReadOnlyCollection<PokemonIndividual> owned, int trials, int seed, double shinyChanceMultiplier = 1.0)
    {
        var roller = new CatchRoller(new Random(seed));
        var shinyCount = 0;
        for (var i = 0; i < trials; i++)
        {
            if (roller.RollCatch(chains, owned, shinyChanceMultiplier).IsShiny) shinyCount++;
        }
        return shinyCount;
    }
}
