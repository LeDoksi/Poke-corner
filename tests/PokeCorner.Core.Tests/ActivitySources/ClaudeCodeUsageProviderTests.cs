using PokeCorner.Core.ActivitySources;
using Xunit;

namespace PokeCorner.Core.Tests.ActivitySources;

public class ClaudeCodeUsageProviderTests : IDisposable
{
    private readonly string _tempHome;

    public ClaudeCodeUsageProviderTests()
    {
        _tempHome = Path.Combine(Path.GetTempPath(), "PokeCornerTests_" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(_tempHome, "projects", "fake-project"));
    }

    public void Dispose() => Directory.Delete(_tempHome, recursive: true);

    private void WriteSessionFile(string content)
    {
        var path = Path.Combine(_tempHome, "projects", "fake-project", Guid.NewGuid() + ".jsonl");
        File.WriteAllText(path, content);
    }

    [Fact]
    public void ReadNewUnitsSinceLastCall_NoFiles_ReturnsZero()
    {
        var provider = new ClaudeCodeUsageProvider(_tempHome);

        Assert.Equal(0, provider.ReadNewUnitsSinceLastCall());
    }

    [Fact]
    public void ReadNewUnitsSinceLastCall_SumsTodaysInputOutputAndCacheTokens()
    {
        var today = DateTimeOffset.UtcNow.ToString("O");
        WriteSessionFile(
            "{\"timestamp\":\"" + today + "\",\"message\":{\"usage\":{\"input_tokens\":2,\"output_tokens\":635,\"cache_creation_input_tokens\":562,\"cache_read_input_tokens\":133657}}}\n" +
            "garbage line that is not JSON, must be skipped\n" +
            "{\"timestamp\":\"" + today + "\",\"message\":{\"usage\":{\"input_tokens\":1,\"output_tokens\":10,\"cache_creation_input_tokens\":0,\"cache_read_input_tokens\":0}}}");

        var provider = new ClaudeCodeUsageProvider(_tempHome);
        var units = provider.ReadNewUnitsSinceLastCall();

        Assert.Equal(2 + 635 + 562 + 133657 + 1 + 10, units);
    }

    [Fact]
    public void ReadNewUnitsSinceLastCall_ExcludesEntriesFromPreviousDays()
    {
        var yesterday = DateTimeOffset.UtcNow.AddDays(-1).ToString("O");
        WriteSessionFile(
            "{\"timestamp\":\"" + yesterday + "\",\"message\":{\"usage\":{\"input_tokens\":100,\"output_tokens\":100,\"cache_creation_input_tokens\":0,\"cache_read_input_tokens\":0}}}");

        var provider = new ClaudeCodeUsageProvider(_tempHome);

        Assert.Equal(0, provider.ReadNewUnitsSinceLastCall());
    }

    [Fact]
    public void ReadNewUnitsSinceLastCall_SecondCall_OnlyCountsUnitsAddedSinceFirstCall()
    {
        var today = DateTimeOffset.UtcNow.ToString("O");
        WriteSessionFile(
            "{\"timestamp\":\"" + today + "\",\"message\":{\"usage\":{\"input_tokens\":100,\"output_tokens\":0,\"cache_creation_input_tokens\":0,\"cache_read_input_tokens\":0}}}");
        var provider = new ClaudeCodeUsageProvider(_tempHome);
        provider.ReadNewUnitsSinceLastCall();

        WriteSessionFile(
            "{\"timestamp\":\"" + today + "\",\"message\":{\"usage\":{\"input_tokens\":50,\"output_tokens\":0,\"cache_creation_input_tokens\":0,\"cache_read_input_tokens\":0}}}");
        var secondCallUnits = provider.ReadNewUnitsSinceLastCall();

        Assert.Equal(50, secondCallUnits);
    }
}
