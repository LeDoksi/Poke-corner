using PokeCorner.Core.Domain;
using PokeCorner.Core.PokeApi;
using Xunit.Abstractions;

namespace PokeCorner.App.Tests.GlobalUpdate;

public sealed class AchievementPackageTests(ITestOutputHelper output)
{
    [Fact]
    public void PackagedGoalCountsComeFromCurrentIssuableCatalog()
    {
        var catalog = CatalogPackage.Load(Path.Combine(AppContext.BaseDirectory, "Data"));
        var service = new AchievementService(catalog);
        output.WriteLine($"Current package: {catalog.Species.Count} species, " +
            $"{service.ReachableSpeciesIds.Count} reachable normal species, " +
            $"{service.ReachableLegendaryIds.Count} reachable legendary species.");
        Assert.All(service.ReachableSpeciesIds, id => Assert.True(catalog.CanObtain(id, false)));
        Assert.All(service.ReachableLegendaryIds, id => Assert.Contains(id, service.ReachableSpeciesIds));
    }
}
