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

    public IReadOnlyList<SpeciesEvolutionChain> LoadCachedOrFallback()
    {
        if (File.Exists(_catalogFilePath))
        {
            try
            {
                var cached = JsonSerializer.Deserialize<List<SpeciesEvolutionChain>>(File.ReadAllText(_catalogFilePath));
                if (cached is { Count: > 0 }) return cached;
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                // Corrupted or unreadable cache file (e.g. process killed mid-write during a
                // previous RefreshFromNetworkAsync): degrade to the offline fallback instead of
                // throwing, mirroring SpriteCache.GetOrFetchAsync's graceful degradation.
            }
        }
        return _spriteCache.LoadOfflineStarters();
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
        File.WriteAllText(_catalogFilePath, JsonSerializer.Serialize(chains));
    }
}
