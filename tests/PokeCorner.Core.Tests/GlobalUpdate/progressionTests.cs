using System.Text.Json;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
namespace PokeCorner.Core.Tests.GlobalUpdate;
public sealed class ProgressionTests
{
    private sealed class BoundedIndexRandom(int index) : Random
    {
        public override int Next(int maxValue) => Math.Min(index, maxValue - 1);
    }

    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/design-v2"));
    private static PokeCorner.Core.PokeApi.CatalogPackage Catalog(NurseryCoreTests fixture) =>
        fixture.Catalog([1, 2, 3, 679, 714, 715], 714);
    private static PokemonIndividual Individual(int species = 1, bool shiny = false) => new() { SpeciesId = species, EvolutionChainId = 1, ChainLength = 3, Nature = Nature.Hardy, IsShiny = shiny, CaughtAtUtc = DateTimeOffset.UtcNow, CurrentSpeciesId = species, FamilyRootId = species, ReachedSpeciesIds = [species], FirstTraitId = "gatherer" };
    [Fact] public void ExactThresholds()
    {
        var levels = new LevelProgression(Root);
        using var balance = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root, "final", "balance.json")));
        var rows = balance.RootElement.GetProperty("levels").EnumerateArray().ToArray();
        Assert.Equal(40, rows.Length);
        foreach (var row in rows) { var xp = row.GetProperty("totalXp").GetInt32(); var level = row.GetProperty("level").GetInt32(); Assert.Equal(level, levels.LevelForXp(xp)); if (xp > 0) Assert.Equal(level - 1, levels.LevelForXp(xp - 1)); }
        Assert.Equal(1200, levels.XpForLevel(10)); Assert.Equal(2800, levels.XpForLevel(20)); Assert.Equal(4800, levels.XpForLevel(30)); Assert.Equal(6400, levels.XpForLevel(40));
        Assert.Equal(1, levels.TraitTierForXp(2799)); Assert.Equal(2, levels.TraitTierForXp(2800)); Assert.Equal(3, levels.TraitTierForXp(4800));
    }
    [Fact] public void CrossingTenOnceAndCap()
    {
        using var fixture = new NurseryCoreTests(); var catalog = Catalog(fixture);
        var levels = new LevelProgression(Root); var individual = Individual(); individual.TotalXp = 1199;
        Assert.Equal(1, levels.AddXp(individual, 1, catalog)); Assert.NotNull(individual.SecondTraitId); Assert.NotEqual(individual.FirstTraitId, individual.SecondTraitId);
        var saved = JsonSerializer.Deserialize<PokemonIndividual>(JsonSerializer.Serialize(individual))!;
        Assert.Equal(5200, levels.AddXp(saved, int.MaxValue, catalog)); Assert.Equal(individual.SecondTraitId, saved.SecondTraitId);
        Assert.Equal(0, levels.AddXp(saved, 1, catalog)); Assert.Equal(6400, saved.TotalXp);
    }

    [Theory]
    [InlineData(679, false)]
    [InlineData(714, true)]
    public void LevelTenRejectsFastGrowerWhenNextVariantAssetIsMissing(int speciesId, bool shiny)
    {
        using var fixture = new NurseryCoreTests();
        var catalog = fixture.Catalog([679, 714, 715], 714);
        Assert.False(catalog.CanObtain(speciesId == 679 ? 680 : 715, shiny));
        var individual = Individual(speciesId, shiny);
        individual.TotalXp = 1199;
        var oldPool = new TraitRules(catalog.DataRoot).Allowed(speciesId)
            .Where(id => id != individual.FirstTraitId).ToArray();
        var fastIndex = Array.IndexOf(oldPool, "fast-grower");
        Assert.True(fastIndex >= 0);
        Assert.Equal(1, new LevelProgression(catalog.DataRoot).AddXp(individual, 1, catalog, new BoundedIndexRandom(fastIndex)));
        Assert.NotEqual("fast-grower", individual.SecondTraitId);
    }

    [Fact]
    public void LevelTenCanAwardFastGrowerWhenNextVariantAssetExists()
    {
        using var fixture = new NurseryCoreTests();
        var catalog = fixture.Catalog([714, 715], 714);
        Assert.True(catalog.CanObtain(715, false));
        var individual = Individual(714);
        individual.TotalXp = 1199;
        var oldPool = new TraitRules(catalog.DataRoot).Allowed(714)
            .Where(id => id != individual.FirstTraitId).ToArray();
        var fastIndex = Array.IndexOf(oldPool, "fast-grower");
        Assert.True(fastIndex >= 0);
        Assert.Equal(1, new LevelProgression(catalog.DataRoot).AddXp(individual, 1, catalog, new BoundedIndexRandom(fastIndex)));
        Assert.Equal("fast-grower", individual.SecondTraitId);
    }
    [Fact] public void TerminalExclusionAndExistingTrait()
    {
        using var fixture = new NurseryCoreTests(); var catalog = Catalog(fixture);
        var traits = new TraitRules(Root); Assert.Equal(11, traits.Allowed(1).Count); Assert.Equal(10, traits.Allowed(3).Count);
        Assert.Contains("fast-grower", traits.Allowed(1)); Assert.DoesNotContain("fast-grower", traits.Allowed(3));
        var individual = Individual(3); individual.FirstTraitId = "fast-grower"; individual.TotalXp = 1199;
        new LevelProgression(Root).AddXp(individual, 1, catalog); Assert.Equal("fast-grower", individual.FirstTraitId); Assert.NotEqual("fast-grower", individual.SecondTraitId);
    }
    [Fact] public void TraitTiersAndRepeatOrder()
    {
        var traits = new TraitRules(Root); Assert.Equal(0.05m, traits.Value("mentor", 1)); Assert.Equal(0.10m, traits.Value("mentor", 2)); Assert.Equal(0.15m, traits.Value("mentor", 3));
        Assert.Equal(0.1875m, TraitRules.Stack([0.05m, 0.15m, 0.05m])); Assert.Equal(0.1875m, TraitRules.Stack([0.05m, 0.05m, 0.15m]));
    }
    [Fact] public void ExactCarryAndRoundtrip()
    {
        var repeated = new RationalRemainder(); long sum = 0; for (var i = 0; i < 20; i++) sum += repeated.Settle(105, 100); Assert.Equal(21, sum);
        var split = new RationalRemainder { Numerator = 2, Denominator = 7 }; var whole = new RationalRemainder { Numerator = 2, Denominator = 7 };
        long splitTotal = 0; for (var i = 0; i < 12; i++) splitTotal += split.Settle(105, 100);
        split = JsonSerializer.Deserialize<RationalRemainder>(JsonSerializer.Serialize(split))!;
        for (var i = 0; i < 8; i++) splitTotal += split.Settle(105, 100);
        Assert.Equal(whole.Settle(21, 1), splitTotal); Assert.Equal(whole.Numerator, split.Numerator); Assert.Equal(whole.Denominator, split.Denominator);
    }
    [Fact] public void InvalidOrOverflowLeavesState()
    {
        var r = new RationalRemainder { Numerator = 2, Denominator = 11 };
        Assert.Throws<ArgumentOutOfRangeException>(() => r.Settle(1, 0)); Assert.Throws<ArgumentOutOfRangeException>(() => r.Settle(-1, 2));
        Assert.Throws<OverflowException>(() => r.Settle(1, long.MaxValue)); Assert.Equal(2, r.Numerator); Assert.Equal(11, r.Denominator);
    }
    [Fact] public void SaveSchemaCarriesAndValidatesSharedRemainders()
    {
        var individual = Individual(); individual.XpRemainder = new RationalRemainder { Numerator = 2, Denominator = 7 };
        var data = new SaveData
        {
            SchemaVersion = 2, CatalogRevision = "test", BalanceRevision = "test", MigrationId = SaveMigration.MigrationId,
            Individuals = [individual], Settings = new AppSettings(),
            Berries = SaveMigration.BerryIds.ToDictionary(id => id, _ => 0),
            Items = SaveMigration.ItemIds.ToDictionary(id => id, _ => 0),
            BerryRemainders = SaveMigration.BerryIds.ToDictionary(id => id, _ => new RationalRemainder()),
            CoinRemainder = new RationalRemainder { Numerator = 3, Denominator = 5 }
        };
        JsonSaveStore.ValidateData(data);
        var loaded = JsonSaveStore.Clone(data);
        Assert.Equal(2, loaded.Individuals[0].XpRemainder.Numerator);
        Assert.Equal(7, loaded.Individuals[0].XpRemainder.Denominator);
        Assert.Equal(3, loaded.CoinRemainder.Numerator);
        Assert.Equal(5, loaded.CoinRemainder.Denominator);
        loaded.CoinRemainder.Denominator = 0;
        Assert.Throws<JsonException>(() => JsonSaveStore.ValidateData(loaded));
        loaded.CoinRemainder.Denominator = 5;
        loaded.Individuals[0].XpRemainder.Numerator = 7;
        Assert.Throws<JsonException>(() => JsonSaveStore.ValidateData(loaded));
    }
    [Theory]
    [InlineData(1200, 1, 1)]
    [InlineData(6400, 1, 0)]
    public void LoadedV2MissingSecondTraitIsRepairedOnce(int totalXp, int award, int expectedGain)
    {
        using var fixture = new NurseryCoreTests(); var catalog = Catalog(fixture);
        var individual = Individual(3);
        individual.TotalXp = totalXp;
        var data = new SaveData
        {
            SchemaVersion = 2, CatalogRevision = "test", BalanceRevision = "test", MigrationId = SaveMigration.MigrationId,
            Individuals = [individual], Settings = new AppSettings(),
            Berries = SaveMigration.BerryIds.ToDictionary(id => id, _ => 0),
            Items = SaveMigration.ItemIds.ToDictionary(id => id, _ => 0),
            BerryRemainders = SaveMigration.BerryIds.ToDictionary(id => id, _ => new RationalRemainder())
        };
        JsonSaveStore.ValidateData(data);
        var snapshot = JsonSaveStore.Clone(data);
        var levels = new LevelProgression(Root);
        Assert.Equal(expectedGain, levels.AddXp(snapshot.Individuals[0], award, catalog));
        var chosen = snapshot.Individuals[0].SecondTraitId;
        Assert.NotNull(chosen);
        Assert.NotEqual(snapshot.Individuals[0].FirstTraitId, chosen);
        Assert.DoesNotContain("fast-grower", new TraitRules(Root).Allowed(3));
        var committed = JsonSaveStore.Clone(snapshot);
        JsonSaveStore.ValidateData(committed);
        Assert.Equal(chosen, committed.Individuals[0].SecondTraitId);
        Assert.Equal(0, levels.AddXp(committed.Individuals[0], 0, catalog));
        Assert.Equal(chosen, committed.Individuals[0].SecondTraitId);
    }
}
