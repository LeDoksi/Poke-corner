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
        var chainLength = ComputeChainLength(chainNode);

        return new Domain.SpeciesEvolutionChain(
            ChainId: root.GetProperty("id").GetInt32(),
            StarterSpeciesId: starterSpeciesId,
            CaptureRate: captureRate,
            ChainLength: chainLength);
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

    private static int ExtractIdFromUrl(string url) =>
        int.Parse(url.TrimEnd('/').Split('/')[^1]);

    private static int ComputeChainLength(JsonElement chainNode)
    {
        if (!chainNode.TryGetProperty("evolves_to", out var evolvesTo) || evolvesTo.GetArrayLength() == 0)
        {
            return 1;
        }

        var longestChildLength = 0;
        foreach (var child in evolvesTo.EnumerateArray())
        {
            longestChildLength = Math.Max(longestChildLength, ComputeChainLength(child));
        }
        return 1 + longestChildLength;
    }
}
