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
            FeedingTreats = 7,
            ShopCoins = 15,
            ShopRemainderUnits = 200,
            ShopCatchLuckyCharmActive = true
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
        Assert.Equal(15, loaded.ShopCoins);
        Assert.Equal(200, loaded.ShopRemainderUnits);
        Assert.True(loaded.ShopCatchLuckyCharmActive);
    }

    /// The full trip for the cry-volume setting: through the JSON file AND through Hydrate,
    /// which is where a newly added AppSettings property is most easily forgotten (SaveData
    /// persists the whole settings object, so serialization picks it up for free — the
    /// field-by-field copy in Hydrate does not).
    [Fact]
    public void Save_ThenLoad_ThenHydrate_RoundTripsCrySoundVolume()
    {
        var store = new JsonSaveStore(_tempFile);
        store.Save(new SaveData
        {
            Individuals = new List<PokemonIndividual>(),
            Settings = new AppSettings { CrySoundVolume = 35 }
        });

        var loaded = store.Load();
        Assert.NotNull(loaded);
        Assert.Equal(35, loaded!.Settings.CrySoundVolume);

        var state = new AppState();
        state.Hydrate(loaded);
        Assert.Equal(35, state.Settings.CrySoundVolume);
    }

    /// Silence must survive a restart too — 0 is a real choice, and a "0 means unset, fall back
    /// to the default" bug anywhere on this path would turn the sound back on by itself.
    [Fact]
    public void Save_ThenLoad_ThenHydrate_ZeroCrySoundVolumeStaysSilent()
    {
        var store = new JsonSaveStore(_tempFile);
        store.Save(new SaveData
        {
            Individuals = new List<PokemonIndividual>(),
            Settings = new AppSettings { CrySoundVolume = 0 }
        });

        var state = new AppState();
        state.Hydrate(store.Load()!);

        Assert.Equal(0, state.Settings.CrySoundVolume);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsIsFavorite()
    {
        var store = new JsonSaveStore(_tempFile);
        var pinned = new PokemonIndividual
        {
            SpeciesId = 4,
            EvolutionChainId = 4,
            ChainLength = 3,
            Nature = Nature.Hardy,
            IsShiny = false,
            CaughtAtUtc = DateTimeOffset.UtcNow,
            IsFavorite = true
        };
        var plain = new PokemonIndividual
        {
            SpeciesId = 7,
            EvolutionChainId = 7,
            ChainLength = 3,
            Nature = Nature.Jolly,
            IsShiny = false,
            CaughtAtUtc = DateTimeOffset.UtcNow
        };
        store.Save(new SaveData
        {
            Individuals = new List<PokemonIndividual> { pinned, plain },
            Settings = new AppSettings()
        });

        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.True(loaded!.Individuals.Single(i => i.Id == pinned.Id).IsFavorite);
        Assert.False(loaded.Individuals.Single(i => i.Id == plain.Id).IsFavorite);
    }

    /// Nicknames ride the same path IsFavorite does — a plain settable property on the
    /// individual, which System.Text.Json round-trips with no schema-migration code — so this
    /// asserts the whole save/load trip rather than trusting that.
    [Fact]
    public void Save_ThenLoad_RoundTripsNickname()
    {
        var store = new JsonSaveStore(_tempFile);
        var named = new PokemonIndividual
        {
            SpeciesId = 25,
            EvolutionChainId = 25,
            ChainLength = 2,
            Nature = Nature.Hardy,
            IsShiny = false,
            CaughtAtUtc = DateTimeOffset.UtcNow,
            Nickname = "Sparky"
        };
        var unnamed = new PokemonIndividual
        {
            SpeciesId = 7,
            EvolutionChainId = 7,
            ChainLength = 3,
            Nature = Nature.Jolly,
            IsShiny = false,
            CaughtAtUtc = DateTimeOffset.UtcNow
        };
        store.Save(new SaveData
        {
            Individuals = new List<PokemonIndividual> { named, unnamed },
            Settings = new AppSettings()
        });

        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Equal("Sparky", loaded!.Individuals.Single(i => i.Id == named.Id).Nickname);
        Assert.Null(loaded.Individuals.Single(i => i.Id == unnamed.Id).Nickname);
    }

    /// A blank nickname must never reach the save file as a real one: the sanitizing setter
    /// normalizes it to null on the way in, so it deserializes back as "no nickname" too.
    [Fact]
    public void Save_ThenLoad_WhitespaceOnlyNickname_ComesBackAsNoNickname()
    {
        var store = new JsonSaveStore(_tempFile);
        var individual = new PokemonIndividual
        {
            SpeciesId = 25,
            EvolutionChainId = 25,
            ChainLength = 2,
            Nature = Nature.Hardy,
            IsShiny = false,
            CaughtAtUtc = DateTimeOffset.UtcNow,
            Nickname = "   "
        };
        Assert.Null(individual.Nickname);

        store.Save(new SaveData { Individuals = new List<PokemonIndividual> { individual }, Settings = new AppSettings() });

        Assert.Null(store.Load()!.Individuals.Single().Nickname);
    }

    [Fact]
    public void Load_SaveFileWrittenBeforeIsFavoriteExisted_DefaultsToNotFavorite()
    {
        // Older saves simply have no isFavorite member. System.Text.Json leaves the property
        // at its default rather than failing, so no schema migration is needed.
        var legacyJson = """
        {
          "Individuals": [
            {
              "Id": "3f2504e0-4f89-11d3-9a0c-0305e82c3301",
              "SpeciesId": 25,
              "EvolutionChainId": 10,
              "ChainLength": 2,
              "Nature": 0,
              "IsShiny": false,
              "CaughtAtUtc": "2026-01-01T00:00:00+00:00",
              "AccumulatedCarePoints": 5,
              "CurrentStage": 0
            }
          ],
          "Settings": {}
        }
        """;
        File.WriteAllText(_tempFile, legacyJson);
        var store = new JsonSaveStore(_tempFile);

        var loaded = store.Load();

        Assert.NotNull(loaded);
        var individual = Assert.Single(loaded!.Individuals);
        Assert.False(individual.IsFavorite);
        Assert.Equal(5, individual.AccumulatedCarePoints);
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

    [Fact]
    public void Save_LeavesNoTempFileBehindAfterSuccess()
    {
        var store = new JsonSaveStore(_tempFile);

        store.Save(new SaveData { Individuals = new(), Settings = new AppSettings(), FeedingTreats = 0 });

        Assert.True(File.Exists(_tempFile));
        Assert.False(File.Exists(_tempFile + ".tmp"));
    }

    [Fact]
    public void Load_CorruptedJsonFile_ReturnsNullInsteadOfThrowing()
    {
        File.WriteAllText(_tempFile, "{not valid json");
        var store = new JsonSaveStore(_tempFile);

        var loaded = store.Load();

        Assert.Null(loaded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhitespaceFilePath_Throws(string filePath)
    {
        Assert.Throws<ArgumentException>(() => new JsonSaveStore(filePath));
    }

    [Fact]
    public void Constructor_NullFilePath_Throws()
    {
        // ArgumentException.ThrowIfNullOrWhiteSpace throws ArgumentNullException for null,
        // which is itself an ArgumentException.
        Assert.Throws<ArgumentNullException>(() => new JsonSaveStore(null!));
    }
}
