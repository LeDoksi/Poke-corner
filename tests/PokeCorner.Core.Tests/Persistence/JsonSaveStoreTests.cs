using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using Xunit;

namespace PokeCorner.Core.Tests.Persistence;

public class JsonSaveStoreTests : IDisposable
{
    private readonly string _tempFile;

    public JsonSaveStoreTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), "PokeCornerSaveTests_" + Guid.NewGuid() + ".json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    [Fact]
    public void Load_NoFileYet_ReturnsNull()
    {
        var store = new JsonSaveStore(_tempFile);

        Assert.Null(store.Load());
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsIndividualsSettingsAndActiveId()
    {
        var store = new JsonSaveStore(_tempFile);
        var individual = new PokemonIndividual
        {
            SpeciesId = 25,
            EvolutionChainId = 10,
            ChainLength = 2,
            Nature = Nature.Jolly,
            IsShiny = true,
            CaughtAtUtc = DateTimeOffset.UtcNow
        };
        var data = new SaveData
        {
            Individuals = new List<PokemonIndividual> { individual },
            ActiveIndividualId = individual.Id,
            Settings = new AppSettings { CareMode = CareMode.Hardcore, PetSizePixels = 128 },
            FeedingTreats = 7
        };

        store.Save(data);
        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Single(loaded!.Individuals);
        Assert.Equal(25, loaded.Individuals[0].SpeciesId);
        Assert.True(loaded.Individuals[0].IsShiny);
        Assert.Equal(individual.Id, loaded.ActiveIndividualId);
        Assert.Equal(CareMode.Hardcore, loaded.Settings.CareMode);
        Assert.Equal(128, loaded.Settings.PetSizePixels);
        Assert.Equal(7, loaded.FeedingTreats);
    }

    [Fact]
    public void Save_CreatesParentDirectoryIfMissing()
    {
        var nestedPath = Path.Combine(Path.GetTempPath(), "PokeCornerSaveTests_" + Guid.NewGuid(), "state.json");
        var store = new JsonSaveStore(nestedPath);

        store.Save(new SaveData { Individuals = new(), Settings = new AppSettings(), FeedingTreats = 0 });

        Assert.True(File.Exists(nestedPath));
        Directory.Delete(Path.GetDirectoryName(nestedPath)!, recursive: true);
    }
}
