using System.Net;
using System.Text;
using System.Text.Json;
using PokeCorner.Core.Domain;
using PokeCorner.Core.PokeApi;

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

    private void WriteUsableCatalog(int chainCount, DateTime writeTimeUtc)
    {
        Directory.CreateDirectory(_tempDir);
        var chains = Enumerable.Range(1, chainCount)
            .Select(id => new SpeciesEvolutionChain(id, id, 255, 1, new[] { id }, new[] { $"mon{id}" }, new IReadOnlyList<string>[] { new[] { "normal" } }))
            .ToList();
        File.WriteAllText(_catalogFile, JsonSerializer.Serialize(chains));
        File.SetLastWriteTimeUtc(_catalogFile, writeTimeUtc);
    }

    /// The shop asks for the catalog four times a second; re-reading and re-parsing the file
    /// on every one of those was a measurable part of the UI stutter (POKE-18).
    [Fact]
    public void LoadCachedOrFallback_CalledTwice_DeserializesOnce()
    {
        WriteUsableCatalog(chainCount: 1, DateTime.UtcNow.AddMinutes(-5));
        var catalog = new SpeciesCatalog(new PokeApiClient(new RoutingStubHandler(_ => "{}")), _spriteCache, _catalogFile);

        var first = catalog.LoadCachedOrFallback();
        var second = catalog.LoadCachedOrFallback();

        Assert.Same(first, second);
    }

    [Fact]
    public void LoadCachedOrFallback_AfterFileRewritten_ReturnsNewContent()
    {
        var written = DateTime.UtcNow.AddMinutes(-5);
        WriteUsableCatalog(chainCount: 1, written);
        var catalog = new SpeciesCatalog(new PokeApiClient(new RoutingStubHandler(_ => "{}")), _spriteCache, _catalogFile);
        Assert.Single(catalog.LoadCachedOrFallback());

        WriteUsableCatalog(chainCount: 2, written.AddMinutes(1)); // e.g. a background RefreshFromNetworkAsync landed

        Assert.Equal(2, catalog.LoadCachedOrFallback().Count);
    }

    /// A crash between the temp write and the move leaves catalog.json.tmp behind in the install
    /// folder, which the MSI doesn't own — so it would even survive an uninstall.
    [Fact]
    public void Constructor_DeletesStaleTmpFile()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_catalogFile + ".tmp", "half-written");

        _ = new SpeciesCatalog(new PokeApiClient(new RoutingStubHandler(_ => "{}")), _spriteCache, _catalogFile);

        Assert.False(File.Exists(_catalogFile + ".tmp"));
    }

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
        var catalog = new SpeciesCatalog(new PokeApiClient(new RoutingStubHandler(RespondWithTypes)), _spriteCache, _catalogFile);

        await catalog.RefreshFromNetworkAsync(maxSpeciesId: 3);

        Assert.True(File.Exists(_catalogFile));
        var cached = JsonSerializer.Deserialize<List<SpeciesEvolutionChain>>(File.ReadAllText(_catalogFile))!;
        Assert.Equal(2, cached.Count);
        Assert.Contains(cached, c => c.ChainId == 1 && c.ChainLength == 2);
        Assert.Contains(cached, c => c.ChainId == 2 && c.ChainLength == 1);

        // Types are captured per stage by the same crawl, positionally aligned with the ids.
        var twoStage = cached.Single(c => c.ChainId == 1);
        Assert.Equal(new[] { "grass", "poison" }, twoStage.StageTypes[0]);
        Assert.Equal(new[] { "water" }, twoStage.StageTypes[1]);
    }

    /// Shared stub for the crawl: species -> chain id + capture rate, evolution-chain -> the
    /// tree, and pokemon/{id}/ -> the types array (the resource FetchAnimatedSpriteAsync
    /// already reads, now also the crawl's type source).
    private static string RespondWithTypes(Uri uri)
    {
        var path = uri.AbsolutePath;
        if (path.Contains("pokemon-species/1")) return """{"evolution_chain":{"url":"https://pokeapi.co/api/v2/evolution-chain/1/"},"capture_rate":45}""";
        if (path.Contains("pokemon-species/2")) return """{"evolution_chain":{"url":"https://pokeapi.co/api/v2/evolution-chain/1/"},"capture_rate":45}""";
        if (path.Contains("pokemon-species/3")) return """{"evolution_chain":{"url":"https://pokeapi.co/api/v2/evolution-chain/2/"},"capture_rate":255}""";
        if (path.Contains("evolution-chain/1")) return """{"id":1,"chain":{"species":{"name":"a","url":"https://pokeapi.co/api/v2/pokemon-species/1/"},"evolves_to":[{"species":{"name":"b","url":"https://pokeapi.co/api/v2/pokemon-species/2/"},"evolves_to":[]}]},"capture_rate":45}""";
        if (path.Contains("evolution-chain/2")) return """{"id":2,"chain":{"species":{"name":"c","url":"https://pokeapi.co/api/v2/pokemon-species/3/"},"evolves_to":[]},"capture_rate":255}""";
        // Slot 2 deliberately listed FIRST to prove the client sorts by slot.
        if (path.EndsWith("pokemon/1/")) return """{"types":[{"slot":2,"type":{"name":"poison"}},{"slot":1,"type":{"name":"grass"}}]}""";
        if (path.EndsWith("pokemon/2/")) return """{"types":[{"slot":1,"type":{"name":"water"}}]}""";
        if (path.EndsWith("pokemon/3/")) return """{"types":[{"slot":1,"type":{"name":"fire"}}]}""";
        throw new InvalidOperationException($"Unexpected request: {uri}");
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
        var catalog = new SpeciesCatalog(new PokeApiClient(new RoutingStubHandler(RespondWithTypes)), _spriteCache, _catalogFile);
        await catalog.RefreshFromNetworkAsync(maxSpeciesId: 1);

        var result = catalog.LoadCachedOrFallback();

        Assert.Single(result);
        Assert.Equal(1, result[0].ChainId);
    }

    /// Same reasoning as the missing-names case above, one schema generation later: a
    /// catalog.json crawled before types were captured is otherwise indistinguishable from a
    /// current one, so it would be served forever and every type badge would stay blank while
    /// the bundled fallback sitting right next to it has the data.
    [Fact]
    public void LoadCachedOrFallback_CacheFileWithNamesButNoTypes_ReturnsTypedOfflineFallback()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_catalogFile, JsonSerializer.Serialize(new List<SpeciesEvolutionChain>
        {
            new(ChainId: 1, StarterSpeciesId: 543, CaptureRate: 255, ChainLength: 1,
                StageSpeciesIds: new[] { 543 }, StageSpeciesNames: new[] { "Venipede" })
        }));
        var catalog = new SpeciesCatalog(new PokeApiClient(new RoutingStubHandler(_ => "{}")), _spriteCache, _catalogFile);

        var result = catalog.LoadCachedOrFallback();

        Assert.False(catalog.IsCacheSchemaCurrent());
        Assert.Equal(_spriteCache.LoadOfflineStarters().Count, result.Count);
        Assert.NotEmpty(result.ResolveSpeciesTypes(543));
    }

    /// The bundled catalog that actually ships must carry types for EVERY stage of EVERY
    /// chain — the whole point of the additive crawl. A partially-populated file would show
    /// badges on some cards and not others, which is exactly the "silently ship partial data"
    /// outcome this project keeps ruling out.
    [Fact]
    public void BundledOfflineCatalog_CarriesTypesForEveryStageOfEveryChain()
    {
        var chains = _spriteCache.LoadOfflineStarters();

        Assert.NotEmpty(chains);
        foreach (var chain in chains)
        {
            Assert.Equal(chain.StageSpeciesIds.Count, chain.StageTypes.Count);
            for (var stage = 0; stage < chain.StageSpeciesIds.Count; stage++)
            {
                Assert.True(chain.StageTypes[stage].Count > 0,
                    $"chain {chain.ChainId} stage {stage} (species {chain.StageSpeciesIds[stage]}) has no types");
            }
        }
    }

    /// Emolga — the species from the live bug report that started all of this. Confirms from
    /// the SHIPPED data that it really is a single-stage chain (so Part A's "never evolves"
    /// branch is the one it hits) and that it carries its real types.
    [Fact]
    public void BundledOfflineCatalog_Emolga_IsSingleStageAndElectricFlying()
    {
        var chains = _spriteCache.LoadOfflineStarters();
        var emolga = chains.Single(c => c.StageSpeciesIds.Contains(587));

        Assert.Equal(1, emolga.ChainLength);
        Assert.Equal(new[] { "electric", "flying" }, emolga.StageTypes[0]);
    }
}
