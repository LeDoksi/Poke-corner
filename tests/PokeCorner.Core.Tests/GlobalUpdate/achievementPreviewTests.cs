using System.Text.Json;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.Core.Tests.GlobalUpdate;

public sealed class AchievementPreviewTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MigratedEventSpecialHistoryStaysUnknownUntilRecorded(bool explicitNull)
    {
        using var fixture = new NurseryCoreTests();
        var catalog = fixture.Catalog([1, 2]);
        File.Copy(Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../..")),
            "docs/design-v2/final/achievement-rewards.json"), Path.Combine(catalog.DataRoot, "final/achievement-rewards.json"));
        var legacy = new SaveData { Individuals = [], Settings = new() };
        var lookup = new LegacyLookup([new SpeciesEvolutionChain(1, 1, 200, 1, [1], ["Bulbasaur"])], "[]"u8.ToArray());
        var migrated = SaveMigration.Migrate(legacy, lookup, catalog);
        const string key = "special:trophy-special-type-team";
        Assert.False(migrated.Metrics.ContainsKey(key));
        if (explicitNull) migrated.Metrics[key] = null;
        var service = new AchievementService(catalog);
        var before = JsonSerializer.Serialize(migrated);
        Assert.Null(service.Preview(migrated, "trophy-special-type-team").Value);
        Assert.Equal(0, service.Preview(migrated, "trophy-special-nick").Value);
        Assert.Equal(before, JsonSerializer.Serialize(migrated));
        AchievementMetrics.Record(migrated, key);
        Assert.Equal(1, service.Preview(migrated, "trophy-special-type-team").Value);
    }

    [Fact]
    public void PreviewUsesCanonicalNextThresholdAndDoesNotMutateSave()
    {
        using var fixture = new NurseryCoreTests();
        var catalog = fixture.Catalog([1, 2]);
        File.Copy(Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../..")),
            "docs/design-v2/final/achievement-rewards.json"), Path.Combine(catalog.DataRoot, "final/achievement-rewards.json"));
        var service = new AchievementService(catalog);
        var data = new SaveData { SchemaVersion = 2, Settings = new(), Individuals = [] };
        data.DiscoveredSpeciesIds.Add(1);
        data.AwardLedger["trophy-species-1"] = new();
        data.Metrics["successful-catches"] = null;
        data.Metrics["special:trophy-special-type-team"] = null;
        var before = JsonSerializer.Serialize(data);
        var next = service.Preview(data, "trophy-species-1");
        Assert.Equal("trophy-species-2", next.AwardId);
        Assert.Equal(1, next.Value);
        Assert.Equal(25, next.Target);
        Assert.False(next.Complete);
        Assert.Null(service.Preview(data, "trophy-catches-1").Value);
        Assert.Null(service.Preview(data, "trophy-special-type-team").Value);
        data.AwardLedger["trophy-species-2"] = new();
        data.AwardLedger["trophy-species-3"] = new();
        next = service.Preview(data, "trophy-species-1");
        Assert.Equal(service.ReachableSpeciesIds.Count, next.Target);
        Assert.Equal(1, next.Value);
        data.AwardLedger.Remove("trophy-species-2");
        data.AwardLedger.Remove("trophy-species-3");
        Assert.Equal(before, JsonSerializer.Serialize(data));
        Assert.Throws<ArgumentException>(() => service.Preview(data, "missing"));
    }
}
