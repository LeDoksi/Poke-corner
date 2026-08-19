using System.Net.Http;
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
