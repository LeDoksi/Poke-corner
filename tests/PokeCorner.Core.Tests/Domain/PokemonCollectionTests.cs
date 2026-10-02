using PokeCorner.Core.Domain;

namespace PokeCorner.Core.Tests.Domain;

public class PokemonCollectionTests
{
    private static PokemonIndividual MakeIndividual(int speciesId = 1, int chainId = 1) => new()
    {
        SpeciesId = speciesId,
        EvolutionChainId = chainId,
        ChainLength = 3,
        Nature = Nature.Hardy,
        IsShiny = false,
        CaughtAtUtc = DateTimeOffset.UtcNow
    };

    [Fact]
    public void Add_FirstIndividual_BecomesActiveAutomatically()
    {
        var collection = new PokemonCollection();
        var individual = MakeIndividual();

        collection.Add(individual);

        Assert.Equal(individual.Id, collection.ActiveIndividualId);
        Assert.Same(individual, collection.GetActive());
    }

    [Fact]
    public void Add_SecondIndividual_DoesNotChangeActive()
    {
        var collection = new PokemonCollection();
        var first = MakeIndividual(1, 1);
        var second = MakeIndividual(2, 2);

        collection.Add(first);
        collection.Add(second);

        Assert.Equal(first.Id, collection.ActiveIndividualId);
    }

    [Fact]
    public void SetActive_UnknownId_Throws()
    {
        var collection = new PokemonCollection();
        collection.Add(MakeIndividual());

        Assert.Throws<InvalidOperationException>(() => collection.SetActive(Guid.NewGuid()));
    }

    [Fact]
    public void Release_ActiveIndividual_PromotesAnotherAsActive()
    {
        var collection = new PokemonCollection();
        var first = MakeIndividual(1, 1);
        var second = MakeIndividual(2, 2);
        collection.Add(first);
        collection.Add(second);

        var released = collection.Release(first.Id);

        Assert.True(released);
        Assert.Equal(second.Id, collection.ActiveIndividualId);
        Assert.DoesNotContain(collection.Individuals, i => i.Id == first.Id);
    }

    [Fact]
    public void Release_LastIndividual_LeavesNoActive()
    {
        var collection = new PokemonCollection();
        var only = MakeIndividual();
        collection.Add(only);

        collection.Release(only.Id);

        Assert.Null(collection.ActiveIndividualId);
        Assert.Null(collection.GetActive());
    }

    [Fact]
    public void ClearActive_LeavesDesktopEmptyWithoutRemovingAnyone()
    {
        var collection = new PokemonCollection();
        var individual = MakeIndividual();
        collection.Add(individual);

        collection.ClearActive();

        Assert.Null(collection.ActiveIndividualId);
        Assert.Null(collection.GetActive());
        Assert.Single(collection.Individuals);
    }
}
