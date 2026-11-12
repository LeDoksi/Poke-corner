using PokeCorner.Core.Domain;
using Xunit;

namespace PokeCorner.Core.Tests.GlobalUpdate;

public class SpareAndFamilyShinyTests
{
    private static readonly DateTimeOffset Day = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    private static SpareRules.Copy Copy(int species, bool shiny = false, bool keep = false, int level = 1, int ahead = 0, int day = 0) =>
        new(Guid.NewGuid(), species, shiny, keep, level, 0, Day.AddDays(day), ahead);

    [Fact]
    public void ShinyAndNormalOfOneSpeciesAreNotDuplicates()
    {
        Assert.Empty(SpareRules.Spare([Copy(4), Copy(4, shiny: true)]));
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
