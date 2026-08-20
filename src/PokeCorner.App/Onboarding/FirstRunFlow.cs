using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;

namespace PokeCorner.App.Onboarding;

public static class FirstRunFlow
{
    public static bool NeedsOnboarding(SaveData? loaded) =>
        loaded is null || loaded.Individuals.Count == 0;

    public static PokemonIndividual HatchChosen(SpeciesEvolutionChain chosenChain, CatchRoller roller, DateTimeOffset nowUtc)
    {
        var result = roller.RollFromChain(chosenChain);
        return new PokemonIndividual
        {
            SpeciesId = result.SpeciesId,
            EvolutionChainId = result.ChainId,
            ChainLength = result.ChainLength,
            Nature = result.Nature,
            IsShiny = result.IsShiny,
            CaughtAtUtc = nowUtc
        };
    }
}
