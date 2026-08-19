using System.Text.Json;

namespace PokeCorner.Core.PokeApi;

public sealed class PokeApiClient
{
    private readonly HttpClient _httpClient;

    public PokeApiClient(HttpMessageHandler handler)
    {
        _httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://pokeapi.co/api/v2/") };
    }

    public async Task<Domain.SpeciesEvolutionChain> FetchEvolutionChainAsync(int chainId)
    {
        var json = await _httpClient.GetStringAsync($"evolution-chain/{chainId}/");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var chainNode = root.GetProperty("chain");

        var speciesUrl = chainNode.GetProperty("species").GetProperty("url").GetString()!;
        var starterSpeciesId = ExtractIdFromUrl(speciesUrl);

        // capture_rate lives on the species resource (pokemon-species/{id}/), not on the
        // evolution-chain resource, so it takes a second call.
        var captureRate = await FetchCaptureRateAsync(starterSpeciesId);
        var stageSpeciesIds = CollectStageSpeciesIds(chainNode);

        return new Domain.SpeciesEvolutionChain(
            ChainId: root.GetProperty("id").GetInt32(),
            StarterSpeciesId: starterSpeciesId,
            CaptureRate: captureRate,
            ChainLength: stageSpeciesIds.Count,
            StageSpeciesIds: stageSpeciesIds);
    }

    public async Task<int> FetchCaptureRateAsync(int speciesId)
    {
        var json = await _httpClient.GetStringAsync($"pokemon-species/{speciesId}/");
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("capture_rate").GetInt32();
    }

    public async Task<byte[]> FetchSpriteAsync(int speciesId)
    {
        return await _httpClient.GetByteArrayAsync(
            $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/{speciesId}.png");
    }

    private static int ExtractIdFromUrl(string url)
    {
        var lastSegment = url.TrimEnd('/').Split('/')[^1];
        if (!int.TryParse(lastSegment, out var id))
        {
            throw new InvalidOperationException($"Could not extract a numeric id from URL: {url}");
        }
        return id;
    }

    /// <summary>
    /// Walks the evolution chain tree and collects the species id at each stage along the
    /// longest branch (mirroring the branch-picking behavior of the chain-length computation),
    /// so the returned list's length always matches the resulting ChainLength.
    /// </summary>
    private static List<int> CollectStageSpeciesIds(JsonElement chainNode)
    {
        var speciesUrl = chainNode.GetProperty("species").GetProperty("url").GetString()!;
        var speciesId = ExtractIdFromUrl(speciesUrl);

        if (!chainNode.TryGetProperty("evolves_to", out var evolvesTo) || evolvesTo.GetArrayLength() == 0)
        {
            return new List<int> { speciesId };
        }

        List<int>? longestChildPath = null;
        foreach (var child in evolvesTo.EnumerateArray())
        {
            var childPath = CollectStageSpeciesIds(child);
            if (longestChildPath is null || childPath.Count > longestChildPath.Count)
            {
                longestChildPath = childPath;
            }
        }

        var result = new List<int> { speciesId };
        result.AddRange(longestChildPath!);
        return result;
    }
}
