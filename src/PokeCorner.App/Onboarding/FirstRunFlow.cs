using System.IO;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.App.Onboarding;

public static class FirstRunFlow
{
    public static bool NeedsOnboarding(SaveData? loaded) =>
        loaded is null || loaded.Individuals.Count == 0;

    public static PokemonIndividual HatchChosen(SpeciesEvolutionChain chosenChain, CatchRoller roller, DateTimeOffset nowUtc, CatalogPackage catalog)
    {
        var result = roller.RollFromChain(chosenChain);
        if (!catalog.CanIssueDirectly(result.SpeciesId, false)) throw new InvalidDataException("Starter must be an available family root.");
        if (result.IsShiny && !catalog.CanIssueDirectly(result.SpeciesId, true)) result = result with { IsShiny = false };
        return result.ToIndividual(nowUtc);
    }
}
