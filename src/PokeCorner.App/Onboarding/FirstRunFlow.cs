using System.IO;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.App.Onboarding;

public static class FirstRunFlow
{
    public const string TutorialRevision = "global-update-v1";

    public static bool NeedsStarter(SaveData loaded) => loaded.Individuals.Count == 0;

    public static bool NeedsTour(SaveData loaded) =>
        loaded.Tutorial.TutorialRevision == TutorialRevision && loaded.Tutorial.Status is not ("completed" or "skipped");

    public static PokemonIndividual? AvailableCompanion(SaveData loaded, CatalogPackage catalog) =>
        loaded.Individuals.FirstOrDefault(p => catalog.CanObtain(p.CurrentSpeciesId, p.IsShiny));

    public static void CompleteStarter(SaveData next, PokemonIndividual starter, bool newGame, CatalogPackage catalog)
    {
        if (next.Individuals.Count != 0 || !catalog.CanIssueDirectly(starter.CurrentSpeciesId, starter.IsShiny))
            throw new InvalidDataException("Стартовый питомец недоступен или коллекция уже заполнена.");
        next.Individuals.Add(starter);
        next.ActiveIndividualId = starter.Id;
        next.DiscoveredSpeciesIds.Add(starter.CurrentSpeciesId);
        if (starter.IsShiny) next.ShinySpeciesIds.Add(starter.CurrentSpeciesId);
        next.StarterSelectionPending = false;
        if (newGame) { next.ShopCoins = 20; next.FeedingTreats = 5; }
        SaveMigration.EnsureGift(next, starter.CurrentSpeciesId, catalog);
    }

    public static void RestoreCompanionAndGift(SaveData next, CatalogPackage catalog)
    {
        if (next.Individuals.Count != 0) next.StarterSelectionPending = false;
        if (next.ActiveIndividualId is null)
            next.ActiveIndividualId = AvailableCompanion(next, catalog)?.Id;
        SaveMigration.EnsureGift(next, next.Individuals.FirstOrDefault(p => p.Id == next.ActiveIndividualId)?.CurrentSpeciesId, catalog);
    }

    public static void SetTourProgress(SaveData next, int step, string status)
    {
        if (step is < 1 or > 6 || status is not ("in-progress" or "completed" or "skipped"))
            throw new ArgumentOutOfRangeException(nameof(step));
        next.Tutorial.TutorialRevision = TutorialRevision;
        next.Tutorial.CurrentStep = step;
        next.Tutorial.Status = status;
    }

    public static PokemonIndividual HatchChosen(SpeciesEvolutionChain chosenChain, CatchRoller roller, DateTimeOffset nowUtc, CatalogPackage catalog)
    {
        var result = roller.RollFromChain(chosenChain);
        if (!catalog.CanIssueDirectly(result.SpeciesId, false)) throw new InvalidDataException("Starter must be an available family root.");
        if (result.IsShiny && !catalog.CanIssueDirectly(result.SpeciesId, true)) result = result with { IsShiny = false };
        return result.ToIndividual(nowUtc);
    }
}
