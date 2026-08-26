using System.Net;
using System.Text;
using System.Text.Json;
using PokeCorner.Core.Domain;
using PokeCorner.Core.PokeApi;
using Xunit;

namespace PokeCorner.Core.Tests.PokeApi;

public class SpeciesCatalogTests : IDisposable
{
    private sealed class RoutingStubHandler : HttpMessageHandler
    {
        private readonly Func<Uri, string> _respond;
        public RoutingStubHandler(Func<Uri, string> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_respond(request.RequestUri!), Encoding.UTF8, "application/json")
            });
    }

    private readonly string _tempDir;
    private readonly string _catalogFile;
    private readonly SpriteCache _spriteCache;

    public SpeciesCatalogTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PokeCornerCatalogTests_" + Guid.NewGuid());
        _catalogFile = Path.Combine(_tempDir, "catalog.json");
        _spriteCache = new SpriteCache(Path.Combine(_tempDir, "sprites"));
    }

    public void Dispose() { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }

    [Fact]
    public void LoadCachedOrFallback_NoCacheFile_ReturnsOfflineStarters()
    {
        var catalog = new SpeciesCatalog(new PokeApiClient(new RoutingStubHandler(_ => "{}")), _spriteCache, _catalogFile);

        var result = catalog.LoadCachedOrFallback();

        Assert.Equal(_spriteCache.LoadOfflineStarters().Count, result.Count);
    }

    [Fact]
    public async Task RefreshFromNetworkAsync_TwoSpeciesSharingAChain_DedupesToOneChainAndWritesCacheFile()
    {
        string Respond(Uri uri)
        {
            var path = uri.AbsolutePath;
            if (path.Contains("pokemon-species/1")) return """{"evolution_chain":{"url":"https://pokeapi.co/api/v2/evolution-chain/1/"},"capture_rate":45}""";
            if (path.Contains("pokemon-species/2")) return """{"evolution_chain":{"url":"https://pokeapi.co/api/v2/evolution-chain/1/"},"capture_rate":45}""";
            if (path.Contains("pokemon-species/3")) return """{"evolution_chain":{"url":"https://pokeapi.co/api/v2/evolution-chain/2/"},"capture_rate":255}""";
            if (path.Contains("evolution-chain/1")) return """{"id":1,"chain":{"species":{"name":"a","url":"https://pokeapi.co/api/v2/pokemon-species/1/"},"evolves_to":[{"species":{"name":"b","url":"https://pokeapi.co/api/v2/pokemon-species/2/"},"evolves_to":[]}]},"capture_rate":45}""";
            if (path.Contains("evolution-chain/2")) return """{"id":2,"chain":{"species":{"name":"c","url":"https://pokeapi.co/api/v2/pokemon-species/3/"},"evolves_to":[]},"capture_rate":255}""";
            throw new InvalidOperationException($"Unexpected request: {uri}");
        }
        var catalog = new SpeciesCatalog(new PokeApiClient(new RoutingStubHandler(Respond)), _spriteCache, _catalogFile);

        await catalog.RefreshFromNetworkAsync(maxSpeciesId: 3);

        Assert.True(File.Exists(_catalogFile));
        var cached = JsonSerializer.Deserialize<List<SpeciesEvolutionChain>>(File.ReadAllText(_catalogFile))!;
        Assert.Equal(2, cached.Count);
        Assert.Contains(cached, c => c.ChainId == 1 && c.ChainLength == 2);
        Assert.Contains(cached, c => c.ChainId == 2 && c.ChainLength == 1);
    }

    [Fact]
    public void LoadCachedOrFallback_CorruptedCacheFile_ReturnsOfflineStartersInsteadOfThrowing()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_catalogFile, "{ this is not valid JSON at all !!!");
        var catalog = new SpeciesCatalog(new PokeApiClient(new RoutingStubHandler(_ => "{}")), _spriteCache, _catalogFile);

        var result = catalog.LoadCachedOrFallback();

        Assert.Equal(_spriteCache.LoadOfflineStarters().Count, result.Count);
    }

    /// The "Pokemon #543 everywhere" bug: a catalog.json written by an earlier build that did
    /// not capture display names deserializes cleanly and passes the 7-day age check, so it
    /// kept being served ahead of the fully-named bundled fallback — and every
    /// ResolveSpeciesName call fell through to the numeric placeholder.
    [Fact]
    public void LoadCachedOrFallback_CacheFileWithoutSpeciesNames_ReturnsNamedOfflineFallback()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_catalogFile, JsonSerializer.Serialize(new List<SpeciesEvolutionChain>
        {
            new(ChainId: 1, StarterSpeciesId: 543, CaptureRate: 255, ChainLength: 1,
                StageSpeciesIds: new[] { 543 }, StageSpeciesNames: Array.Empty<string>())
        }));
        var catalog = new SpeciesCatalog(new PokeApiClient(new RoutingStubHandler(_ => "{}")), _spriteCache, _catalogFile);

        var result = catalog.LoadCachedOrFallback();

        Assert.False(catalog.IsCacheSchemaCurrent());
        Assert.Equal(_spriteCache.LoadOfflineStarters().Count, result.Count);
        Assert.Equal("venipede", result.ResolveSpeciesName(543), ignoreCase: true);
    }

    [Fact]
    public async Task LoadCachedOrFallback_AfterRefresh_ReturnsCachedChainsNotOfflineFallback()
    {
        string Respond(Uri uri)
        {
            if (uri.AbsolutePath.Contains("pokemon-species/1")) return """{"evolution_chain":{"url":"https://pokeapi.co/api/v2/evolution-chain/1/"},"capture_rate":45}""";
            return """{"id":1,"chain":{"species":{"name":"a","url":"https://pokeapi.co/api/v2/pokemon-species/1/"},"evolves_to":[]},"capture_rate":45}""";
        }
        var catalog = new SpeciesCatalog(new PokeApiClient(new RoutingStubHandler(Respond)), _spriteCache, _catalogFile);
        await catalog.RefreshFromNetworkAsync(maxSpeciesId: 1);

        var result = catalog.LoadCachedOrFallback();

        Assert.Single(result);
        Assert.Equal(1, result[0].ChainId);
    }
}
