using System.Net;
using System.Text;
using PokeCorner.Core.PokeApi;
using Xunit;

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
    }
}
