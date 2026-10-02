using System.Net;
using System.Text;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.Core.Tests.PokeApi;

public class PokeApiClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _jsonResponse;
        public StubHandler(string jsonResponse) => _jsonResponse = jsonResponse;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_jsonResponse, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task FetchEvolutionChainAsync_ParsesIdStarterCaptureRateAndChainLength()
    {
        const string json = """
        {
            "id": 1,
            "chain": {
                "species": { "name": "bulbasaur", "url": "https://pokeapi.co/api/v2/pokemon-species/1/" },
                "evolves_to": [
                    {
                        "species": { "name": "ivysaur", "url": "https://pokeapi.co/api/v2/pokemon-species/2/" },
                        "evolves_to": [
                            {
                                "species": { "name": "venusaur", "url": "https://pokeapi.co/api/v2/pokemon-species/3/" },
                                "evolves_to": []
                            }
                        ]
                    }
                ]
            },
            "capture_rate": 45
        }
        """;
        var client = new PokeApiClient(new StubHandler(json));

        var chain = await client.FetchEvolutionChainAsync(chainId: 1);

        Assert.Equal(1, chain.ChainId);
        Assert.Equal(1, chain.StarterSpeciesId);
        Assert.Equal(45, chain.CaptureRate);
        Assert.Equal(3, chain.ChainLength);
        Assert.Equal(new[] { 1, 2, 3 }, chain.StageSpeciesIds);
    }

    [Fact]
    public async Task FetchEvolutionChainAsync_SingleStageSpecies_ChainLengthIsOne()
    {
        const string json = """
        {
            "id": 132,
            "chain": {
                "species": { "name": "ditto", "url": "https://pokeapi.co/api/v2/pokemon-species/132/" },
                "evolves_to": []
            },
            "capture_rate": 35
        }
        """;
        var client = new PokeApiClient(new StubHandler(json));

        var chain = await client.FetchEvolutionChainAsync(chainId: 132);

        Assert.Equal(1, chain.ChainLength);
        Assert.Equal(new[] { 132 }, chain.StageSpeciesIds);
    }

    [Fact]
    public async Task FetchEvolutionChainAsync_SpeciesUrlWithoutNumericId_ThrowsClearInvalidOperationException()
    {
        const string json = """
        {
            "id": 1,
            "chain": {
                "species": { "name": "bulbasaur", "url": "https://pokeapi.co/api/v2/pokemon-species/not-a-number/" },
                "evolves_to": []
            },
            "capture_rate": 45
        }
        """;
        var client = new PokeApiClient(new StubHandler(json));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.FetchEvolutionChainAsync(chainId: 1));

        Assert.Contains("not-a-number", ex.Message);
    }

    /// Types come off the SAME pokemon/{id}/ body FetchAnimatedSpriteAsync already reads.
    /// Slots are returned out of order here on purpose: PokeAPI's ordering is not guaranteed
    /// and a Butterfree must read "Bug, Flying", never "Flying, Bug".
    [Fact]
    public async Task FetchTypesAsync_ReturnsTypeNamesInSlotOrder()
    {
        const string json = """
        {
            "types": [
                { "slot": 2, "type": { "name": "flying", "url": "https://pokeapi.co/api/v2/type/3/" } },
                { "slot": 1, "type": { "name": "bug", "url": "https://pokeapi.co/api/v2/type/7/" } }
            ]
        }
        """;
        var client = new PokeApiClient(new StubHandler(json));

        var types = await client.FetchTypesAsync(speciesId: 12);

        Assert.Equal(new[] { "bug", "flying" }, types);
    }

    [Fact]
    public async Task FetchTypesAsync_SingleType_ReturnsOneEntry()
    {
        const string json = """{ "types": [ { "slot": 1, "type": { "name": "electric" } } ] }""";
        var client = new PokeApiClient(new StubHandler(json));

        Assert.Equal(new[] { "electric" }, await client.FetchTypesAsync(speciesId: 25));
    }

    /// A species whose types are missing or malformed must degrade to "no badges" rather than
    /// throwing — one odd entry cannot be allowed to abort a 649-species catalog crawl.
    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "types": [] }""")]
    [InlineData("""{ "types": "not-an-array" }""")]
    [InlineData("""{ "types": [ { "slot": 1 } ] }""")]
    public async Task FetchTypesAsync_MissingOrMalformedTypes_ReturnsEmptyInsteadOfThrowing(string json)
    {
        var client = new PokeApiClient(new StubHandler(json));

        Assert.Empty(await client.FetchTypesAsync(speciesId: 1));
    }
}
