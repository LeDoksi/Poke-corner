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
