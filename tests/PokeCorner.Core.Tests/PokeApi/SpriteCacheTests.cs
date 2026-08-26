using System.Net.Http;
using PokeCorner.Core.Domain;
using PokeCorner.Core.PokeApi;
using Xunit;

namespace PokeCorner.Core.Tests.PokeApi;

public class SpriteCacheTests : IDisposable
{
    private readonly string _tempDir;

    public SpriteCacheTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PokeCornerSpriteCacheTests_" + Guid.NewGuid());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task GetOrFetchAsync_FirstCall_InvokesFetchAndWritesToDisk()
    {
        var cache = new SpriteCache(_tempDir);
        var fetchCallCount = 0;
        Task<byte[]> Fetch()
        {
            fetchCallCount++;
            return Task.FromResult(new byte[] { 1, 2, 3 });
        }

        var bytes = await cache.GetOrFetchAsync(speciesId: 25, Fetch);

        Assert.Equal(new byte[] { 1, 2, 3 }, bytes);
        Assert.Equal(1, fetchCallCount);
        Assert.True(File.Exists(Path.Combine(_tempDir, "25.png")));
    }

    [Fact]
    public async Task GetOrFetchAsync_SecondCall_ReadsFromDiskWithoutRefetching()
    {
        var cache = new SpriteCache(_tempDir);
        var fetchCallCount = 0;
        Task<byte[]> Fetch()
        {
            fetchCallCount++;
            return Task.FromResult(new byte[] { 9, 9, 9 });
        }

        await cache.GetOrFetchAsync(25, Fetch);
        var secondResult = await cache.GetOrFetchAsync(25, Fetch);

        Assert.Equal(new byte[] { 9, 9, 9 }, secondResult);
        Assert.Equal(1, fetchCallCount);
    }

    [Fact]
    public async Task GetOrFetchAsync_FetchThrowsHttpRequestException_ReturnsNullAndDoesNotCache()
    {
        var cache = new SpriteCache(_tempDir);
        Task<byte[]> Fetch() => throw new HttpRequestException("network unreachable");

        var bytes = await cache.GetOrFetchAsync(speciesId: 25, Fetch);

        Assert.Null(bytes);
        Assert.False(File.Exists(Path.Combine(_tempDir, "25.png")));
    }

    [Fact]
    public void LoadOfflineStarters_ReturnsNonEmptyBundledFallbackList()
    {
        var cache = new SpriteCache(_tempDir);

        var starters = cache.LoadOfflineStarters();

        Assert.NotEmpty(starters);
        Assert.All(starters, s => Assert.True(s.CaptureRate is >= 3 and <= 255));
        Assert.All(starters, s => Assert.Equal(s.ChainLength, s.StageSpeciesIds.Count));
        Assert.All(starters, s => Assert.Equal(s.ChainLength, s.StageSpeciesNames.Count));
    }

    /// Guards the single worst failure mode of the bundled-catalog approach: shipping a
    /// truncated or half-crawled file. A short catalog would silently narrow the catch pool
    /// and reintroduce exactly the "starter pool is too small / chain ids don't mean anything"
    /// problems the full crawl exists to eliminate, and nothing else would fail loudly.
    [Fact]
    public void LoadOfflineStarters_IsTheFullGen1To5Catalog_NotATruncatedCrawl()
    {
        var starters = new SpriteCache(_tempDir).LoadOfflineStarters();

        Assert.True(starters.Count > 300, $"bundled catalog has only {starters.Count} chains — looks truncated");
        Assert.Equal(starters.Count, starters.Select(s => s.ChainId).Distinct().Count());
        Assert.All(starters, s => Assert.All(s.StageSpeciesNames, name => Assert.False(string.IsNullOrWhiteSpace(name))));

        // Every rarity tier must be fillable, or the shop can sell an egg that never hatches.
        Assert.All(
            new[] { RarityTier.Common, RarityTier.Uncommon, RarityTier.Rare, RarityTier.Legendary },
            tier => Assert.Contains(starters, s => RarityClassifier.Classify(s.CaptureRate) == tier));

        // The regression this whole unification exists for: chain 10 is the REAL 3-stage
        // Pichu/Pikachu/Raichu chain, not the old hand-authored 2-stage "Pikachu" entry.
        var chain10 = Assert.Single(starters, s => s.ChainId == 10);
        Assert.Equal(new[] { 172, 25, 26 }, chain10.StageSpeciesIds);
        Assert.Equal(new[] { "Pichu", "Pikachu", "Raichu" }, chain10.StageSpeciesNames);
    }

    [Fact]
    public void LoadOfflineStarters_CalledTwice_ReturnsSameCachedInstance()
    {
        var cache = new SpriteCache(_tempDir);

        var first = cache.LoadOfflineStarters();
        var second = cache.LoadOfflineStarters();

        Assert.Same(first, second);
    }
}
