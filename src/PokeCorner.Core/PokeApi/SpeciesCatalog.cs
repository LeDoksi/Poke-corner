using System.Text.Json;
using PokeCorner.Core.Domain;

namespace PokeCorner.Core.PokeApi;

public sealed class SpeciesCatalog
{
    private readonly PokeApiClient _client;
    private readonly SpriteCache _spriteCache;
    private readonly string _catalogFilePath;

    public SpeciesCatalog(PokeApiClient client, SpriteCache spriteCache, string catalogFilePath)
    {
        _client = client;
        _spriteCache = spriteCache;
        _catalogFilePath = catalogFilePath;
    }

    public IReadOnlyList<SpeciesEvolutionChain> LoadCachedOrFallback() =>
        IsCacheUsable(TryLoadCache(), out var cached) ? cached! : _spriteCache.LoadOfflineStarters();

    /// Whether the on-disk cache is schema-current, i.e. worth serving. False also means a
    /// re-crawl is due regardless of the file's age — App's startup staleness check reads
    /// this so a pre-names catalog.json actually gets refreshed instead of being silently
    /// ignored in favour of the bundled fallback on every launch, forever.
    public bool IsCacheSchemaCurrent() => IsCacheUsable(TryLoadCache(), out _);

    /// A cached catalog written before FetchEvolutionChainAsync captured display names
    /// deserializes fine (SpeciesEvolutionChain normalizes the missing property to an empty
    /// list) but resolves EVERY species to "Pokemon #{id}". Age alone can't tell those files
    /// apart from good ones, so presence of name data is the real freshness signal: one chain
    /// carrying names is enough evidence the file came from the current crawler.
    ///
    /// Types are now held to the same standard, for the identical reason: a catalog crawled
    /// before types were captured would otherwise be served forever, leaving every type badge
    /// permanently blank while the bundled fallback right next to it has the data. Failing the
    /// check sends the file back through RefreshFromNetworkAsync and serves the (complete)
    /// bundled catalog meanwhile — the same degradation path the names change established.
    private static bool IsCacheUsable(List<SpeciesEvolutionChain>? cached, out IReadOnlyList<SpeciesEvolutionChain>? usable)
    {
        usable = cached is { Count: > 0 }
            && cached.Any(c => c.StageSpeciesNames.Count > 0)
            && cached.Any(c => c.StageTypes.Any(types => types.Count > 0))
            ? cached
            : null;
        return usable is not null;
    }

    /// Last parse of the cache file, keyed by its write time. Callers hit this several times a
    /// second (the shop's tier checks, every catch, sprite resolution) and used to re-read and
    /// re-parse the whole file each time. Keying on the write time rather than caching forever
    /// means a background RefreshFromNetworkAsync rewrite is still picked up on the next call.
    /// One reference, swapped whole, so a reader never sees a time from one parse and data from another.
    private sealed record ParsedCache(DateTime WriteTimeUtc, List<SpeciesEvolutionChain>? Chains);
    private ParsedCache? _parsed;

    private List<SpeciesEvolutionChain>? TryLoadCache()
    {
        if (!File.Exists(_catalogFilePath)) return null;
        var writeTimeUtc = File.GetLastWriteTimeUtc(_catalogFilePath);
        if (_parsed is { } parsed && parsed.WriteTimeUtc == writeTimeUtc) return parsed.Chains;

        var chains = ParseCacheFile();
        _parsed = new ParsedCache(writeTimeUtc, chains);
        return chains;
    }

    private List<SpeciesEvolutionChain>? ParseCacheFile()
    {
        try
        {
            return JsonSerializer.Deserialize<List<SpeciesEvolutionChain>>(File.ReadAllText(_catalogFilePath));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Corrupted or unreadable cache file (e.g. process killed mid-write during a
            // previous RefreshFromNetworkAsync): degrade to the offline fallback instead of
            // throwing, mirroring SpriteCache.GetOrFetchAsync's graceful degradation.
            return null;
        }
    }

    /// <summary>
    /// The catalog as a LOOKUP table — everything <see cref="LoadCachedOrFallback"/> returns,
    /// plus the reserved legacy chains that already-caught individuals may have been migrated
    /// onto (see <see cref="LegacyOfflineChains"/>). Use this anywhere a stored
    /// EvolutionChainId is resolved to a species/name/rarity.
    ///
    /// Deliberately separate from <see cref="LoadCachedOrFallback"/> rather than folded into
    /// it: that method is also the catch/hatch ROLL pool, and rolling from the legacy chains
    /// would let a player catch a second, duplicate copy of a line that already exists in the
    /// real catalog under its real id.
    /// </summary>
    public IReadOnlyList<SpeciesEvolutionChain> LoadForLookup() =>
        LoadCachedOrFallback().Concat(LegacyOfflineChains.All).ToList();

    public async Task RefreshFromNetworkAsync(int maxSpeciesId = 649)
    {
        var seenChainIds = new HashSet<int>();
        var chains = new List<SpeciesEvolutionChain>();

        for (var speciesId = 1; speciesId <= maxSpeciesId; speciesId++)
        {
            var chainId = await _client.FetchEvolutionChainIdForSpeciesAsync(speciesId);
            if (!seenChainIds.Add(chainId)) continue;

            chains.Add(await _client.FetchEvolutionChainAsync(chainId));
        }

        var directory = Path.GetDirectoryName(_catalogFilePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        // Temp file + move, so a reader on another thread (the crawl runs on the thread pool)
        // never parses a half-written file.
        var tempPath = _catalogFilePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(chains));
        File.Move(tempPath, _catalogFilePath, overwrite: true);
    }
}
