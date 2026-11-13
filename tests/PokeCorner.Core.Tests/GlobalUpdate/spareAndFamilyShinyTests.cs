using PokeCorner.Core.Domain;
using Xunit;

namespace PokeCorner.Core.Tests.GlobalUpdate;

public class SpareAndFamilyShinyTests
{
    private static readonly DateTimeOffset Day = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    private static SpareRules.Copy Copy(int species, bool shiny = false, bool keep = false, int level = 1, int ahead = 0, int day = 0) =>
        new(Guid.NewGuid(), species, shiny, keep, level, 0, Day.AddDays(day), ahead);

    [Fact]
    public void ShinyAndNormalOfOneSpeciesAreDuplicatesBecauseShinyIsALook()
    {
        Assert.Single(SpareRules.Spare([Copy(4), Copy(4, shiny: true)]));
    }

    [Fact]
    public void AShinyArrivalOpensTheLookForTheWholeFamilyAndItSurvivesRelease()
    {
        PokemonIndividual Pet(int species, bool shiny) => new() { SpeciesId = species, CurrentSpeciesId = species, FamilyRootId = 1,
            EvolutionChainId = 1, ChainLength = 3, Nature = Nature.Hardy, IsShiny = shiny, CaughtAtUtc = DateTimeOffset.UnixEpoch, ReachedSpeciesIds = [species] };
        var normal = Pet(2, false); var shiny = Pet(1, true); var stranger = Pet(4, false); stranger.FamilyRootId = 4;
        var state = new PokeCorner.Core.Persistence.SaveData { Settings = new(), SchemaVersion = 2, Individuals = [normal, shiny, stranger] };
        ShinyRules.SyncLooks(state);
        Assert.True(normal.HasShinyLook); Assert.False(normal.LooksShiny); Assert.True(shiny.LooksShiny); Assert.False(stranger.HasShinyLook);
        normal.ShowShiny = true; shiny.ShowShiny = false; stranger.ShowShiny = true;
        Assert.True(normal.LooksShiny); Assert.False(shiny.LooksShiny); Assert.False(stranger.LooksShiny);
        state.Individuals.Remove(shiny); ShinyRules.SyncLooks(state);
        Assert.True(normal.LooksShiny);
    }

    [Fact]
    public void EachLookIsOpenedOnlyByAnArrivalOfItsOwnKind()
    {
        PokemonIndividual Pet(int species, int family, bool shiny) => new() { SpeciesId = species, CurrentSpeciesId = species, FamilyRootId = family,
            EvolutionChainId = family, ChainLength = 3, Nature = Nature.Hardy, IsShiny = shiny, CaughtAtUtc = DateTimeOffset.UnixEpoch, ReachedSpeciesIds = [species] };
        var roots = new Dictionary<int, int> { [1] = 1, [2] = 1, [4] = 4, [5] = 4, [7] = 7 };
        var loneShiny = Pet(1, 1, true); var shinyWithKin = Pet(4, 4, true); var kin = Pet(5, 4, false);
        // A save from before D-55: every arrival, shiny or not, sits in NormalSpeciesIds; family 7 was released long ago.
        var state = new PokeCorner.Core.Persistence.SaveData { Settings = new(), SchemaVersion = 2, Individuals = [loneShiny, shinyWithKin, kin],
            NormalSpeciesIds = [1, 4, 5, 7], ShinySpeciesIds = [1, 4] };
        ShinyRules.SyncLooks(state, roots);
        Assert.False(loneShiny.HasNormalLook); Assert.True(loneShiny.LooksShiny);
        loneShiny.ShowShiny = false; Assert.True(loneShiny.LooksShiny); // a closed look cannot be worn
        Assert.True(shinyWithKin.HasBothLooks); Assert.True(kin.HasBothLooks);
        Assert.Equal([4, 7], state.NormalFamilyIds.Order());
        Assert.Equal([4, 5, 7], ShinyRules.FamilyNormal(state.NormalSpeciesIds, state.NormalFamilyIds, roots).Order());
        // An ordinary arrival opens the ordinary look for the shiny that was there first, and it stays after release.
        var late = Pet(2, 1, false); state.Individuals.Add(late); ShinyRules.SyncLooks(state, roots);
        Assert.True(loneShiny.HasBothLooks); Assert.False(loneShiny.LooksShiny);
        state.Individuals.Remove(late); ShinyRules.SyncLooks(state, roots);
        Assert.True(loneShiny.HasNormalLook);
    }

    [Fact]
    public void AChainIsNeverLongerThanThePetsOwnBranch()
    {
        PokemonIndividual Pet(int species, EvolutionStage stage, int length) => new() { SpeciesId = 52, CurrentSpeciesId = species, FamilyRootId = 52,
            EvolutionChainId = 22, ChainLength = length, CurrentStage = stage, Nature = Nature.Hardy, IsShiny = false, CaughtAtUtc = DateTimeOffset.UnixEpoch, ReachedSpeciesIds = [species] };
        var depths = new Dictionary<int, int> { [52] = 2, [53] = 1, [863] = 1 };
        var persian = Pet(53, EvolutionStage.Stage2, 3); var meowth = Pet(52, EvolutionStage.Stage1, 3); var gift = Pet(52, EvolutionStage.Stage1, 1);
        var state = new PokeCorner.Core.Persistence.SaveData { Settings = new(), SchemaVersion = 2, Individuals = [persian, meowth, gift] };
        EvolutionService.SyncChainLengths(state, depths);
        Assert.Equal(2, persian.ChainLength); Assert.Equal(2, meowth.ChainLength); Assert.Equal(1, gift.ChainLength);
    }

    [Fact]
    public void AYoungerStageIsSpareNextToAGrownRelativeUnlessItCanStillOpenSomething()
    {
        var golem = Copy(76, level: 40) with { Reached = [74, 75, 76] };
        var geodude = Copy(74); var growing = Copy(74, ahead: 1); var loved = Copy(74, keep: true);
        Assert.Equal([geodude.Id], SpareRules.Spare([golem, geodude]));
        Assert.Empty(SpareRules.Spare([golem, growing]));
        Assert.Empty(SpareRules.Spare([golem, loved]));
        // A Golem met already grown never was a Geodude, but the catalog knows Geodude grows into it.
        Assert.Equal([geodude.Id], SpareRules.Spare([Copy(76, level: 40), geodude with { Ahead = [75, 76] }]));
        Assert.Empty(SpareRules.Spare([Copy(76, level: 40), geodude]));
    }

    [Fact]
    public void ASingleFullyGrownPokemonIsNeverSpare()
    {
        Assert.Empty(SpareRules.Spare([Copy(6, level: 40), Copy(9, level: 40)]));
    }

    [Fact]
    public void TheBestCopyStaysAndTheWeakerOneIsSpare()
    {
        var strong = Copy(6, level: 30); var weak = Copy(6, level: 5); var old = Copy(6, level: 5, day: -3);
        var spare = SpareRules.Spare([weak, strong, old]);
        Assert.DoesNotContain(strong.Id, spare);
        Assert.Equal(2, spare.Count);
    }

    [Fact]
    public void CopiesThatCanStillOpenNewFormsAreKept()
    {
        // Two forms ahead are still closed: three copies are all useful, the fourth is not.
        var copies = Enumerable.Range(0, 4).Select(n => Copy(133, level: 10 - n, ahead: 2)).ToArray();
        var spare = SpareRules.Spare(copies);
        Assert.Equal([copies[3].Id], spare);
    }

    [Fact]
    public void FavouritesAndTheActivePetAreNeverOffered()
    {
        var copies = new[] { Copy(25, keep: true), Copy(25, keep: true), Copy(25, level: 40) };
        Assert.Equal([copies[2].Id], SpareRules.Spare(copies));
    }

    [Fact]
    public void ShineOpensForSeenStagesOfTheSameFamilyOnly()
    {
        var roots = new Dictionary<int, int> { [4] = 4, [5] = 4, [6] = 4, [7] = 7, [8] = 7 };
        var open = ShinyRules.FamilyShiny(new HashSet<int> { 5 }, [4, 5, 7, 8], roots);
        Assert.Equal([4, 5], open.Order());
        // Charizard has not been seen yet, so it stays closed; a stage without shiny art stays closed too.
        Assert.Equal([5], ShinyRules.FamilyShiny(new HashSet<int> { 5 }, [4, 5], roots, id => id != 4).Order());
        Assert.Empty(ShinyRules.FamilyShiny(new HashSet<int>(), [4, 5], roots));
    }
}
