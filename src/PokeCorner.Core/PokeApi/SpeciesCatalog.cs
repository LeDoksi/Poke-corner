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

        // Left by a crash between RefreshFromNetworkAsync's temp write and its move.
        try { File.Delete(TempPath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private string TempPath => _catalogFilePath + ".tmp";

    public IReadOnlyList<SpeciesEvolutionChain> LoadCachedOrFallback() =>
        IsCacheUsable(TryLoadCache(), out var cached) ? cached! : _spriteCache.LoadOfflineStarters();

    /// Whether the on-disk cache is schema-current, i.e. worth serving. False also means a
    /// re-crawl is due regardless of the file's age — App's startup staleness check reads
    /// this so a pre-names catalog.json actually gets refreshed instead of being silently
    /// ignored in favour of the bundled fallback on every launch, forever.
    public bool IsCacheSchemaCurrent() => IsCacheUsable(TryLoadCache(), out _);

    /// A catalog.json written before the crawler captured display names or types deserializes
    /// fine but shows every species as "Pokemon #{id}" with no type badges. Age can't tell
    /// those files apart, so the presence of name and type data is the freshness signal: failing
    /// it serves the complete bundled catalog and sends the file back through
    /// RefreshFromNetworkAsync.
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
    /// second (shop tier checks, catches, sprite resolution); keying on the write time means a
    /// background RefreshFromNetworkAsync rewrite is still picked up on the next call. One
    /// reference, swapped whole, so a reader never sees a time from one parse and data from another.
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
        File.WriteAllText(TempPath, JsonSerializer.Serialize(chains));
        File.Move(TempPath, _catalogFilePath, overwrite: true);
    }
}
