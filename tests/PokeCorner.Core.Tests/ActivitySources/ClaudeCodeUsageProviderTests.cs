using System.Reflection;
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

    private string WriteSessionFile(string content)
    {
        var path = Path.Combine(_tempHome, "projects", "fake-project", Guid.NewGuid() + ".jsonl");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void ReadNewUnitsSinceLastCall_NoFiles_ReturnsZero()
    {
        var provider = new ClaudeCodeUsageProvider(_tempHome);

        Assert.Equal(0, provider.ReadNewUnitsSinceLastCall());
    }

    [Fact]
    public void ReadNewUnitsSinceLastCall_SumsTodaysInputOutputAndCacheTokens_DividedIntoActivityUnits()
    {
        // Construct with no files yet so the constructor's baseline read doesn't consume this data.
        var provider = new ClaudeCodeUsageProvider(_tempHome);
        var today = DateTimeOffset.UtcNow.ToString("O");
        WriteSessionFile(
            "{\"timestamp\":\"" + today + "\",\"message\":{\"usage\":{\"input_tokens\":2,\"output_tokens\":635,\"cache_creation_input_tokens\":562,\"cache_read_input_tokens\":133657}}}\n" +
            "garbage line that is not JSON, must be skipped\n" +
            "{\"timestamp\":\"" + today + "\",\"message\":{\"usage\":{\"input_tokens\":1,\"output_tokens\":10,\"cache_creation_input_tokens\":0,\"cache_read_input_tokens\":0}}}");

        var units = provider.ReadNewUnitsSinceLastCall();

        const long rawTotal = 2 + 635 + 562 + 133657 + 1 + 10;
        Assert.Equal(rawTotal / 50, units);
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
        var provider = new ClaudeCodeUsageProvider(_tempHome); // no files yet, baseline 0
        var today = DateTimeOffset.UtcNow.ToString("O");
        WriteSessionFile(
            "{\"timestamp\":\"" + today + "\",\"message\":{\"usage\":{\"input_tokens\":100,\"output_tokens\":0,\"cache_creation_input_tokens\":0,\"cache_read_input_tokens\":0}}}");
        var firstCallUnits = provider.ReadNewUnitsSinceLastCall();
        Assert.Equal(2, firstCallUnits); // 100 raw tokens / 50 divisor

        WriteSessionFile(
            "{\"timestamp\":\"" + today + "\",\"message\":{\"usage\":{\"input_tokens\":50,\"output_tokens\":0,\"cache_creation_input_tokens\":0,\"cache_read_input_tokens\":0}}}");
        var secondCallUnits = provider.ReadNewUnitsSinceLastCall();

        Assert.Equal(1, secondCallUnits); // 50 raw tokens / 50 divisor
    }

    [Fact]
    public void ReadNewUnitsSinceLastCall_DayRollover_ResetsCounterAndReturnsFullNewDayTotal()
    {
        var today = DateTimeOffset.UtcNow.ToString("O");
        WriteSessionFile(
            "{\"timestamp\":\"" + today + "\",\"message\":{\"usage\":{\"input_tokens\":100,\"output_tokens\":0,\"cache_creation_input_tokens\":0,\"cache_read_input_tokens\":0}}}");
        var provider = new ClaudeCodeUsageProvider(_tempHome);

        // The constructor already consumed today's existing data as the starting baseline,
        // so the first call after construction sees nothing new.
        var firstCallUnits = provider.ReadNewUnitsSinceLastCall();
        Assert.Equal(0, firstCallUnits);

        // Simulate day rollover by setting _totalCountedSoFarDate to yesterday via reflection.
        var dateField = typeof(ClaudeCodeUsageProvider).GetField("_totalCountedSoFarDate", BindingFlags.NonPublic | BindingFlags.Instance);
        dateField!.SetValue(provider, DateTime.UtcNow.Date.AddDays(-1));

        // Second call on "new day" with the same data should count it as fresh again.
        var secondCallUnits = provider.ReadNewUnitsSinceLastCall();
        Assert.Equal(2, secondCallUnits); // 100 raw tokens / 50 divisor
    }

    [Fact]
    public void Constructor_WithExistingTodayData_FirstCallReturnsZeroNotWholeDayTotal()
    {
        var today = DateTimeOffset.UtcNow.ToString("O");
        WriteSessionFile(
            "{\"timestamp\":\"" + today + "\",\"message\":{\"usage\":{\"input_tokens\":100,\"output_tokens\":0,\"cache_creation_input_tokens\":0,\"cache_read_input_tokens\":0}}}");

        var provider = new ClaudeCodeUsageProvider(_tempHome);

        Assert.Equal(0, provider.ReadNewUnitsSinceLastCall());
    }

    [Fact]
    public void ReadNewUnitsSinceLastCall_FileWithOldMtime_ExcludedEvenIfInternalTimestampIsToday()
    {
        var provider = new ClaudeCodeUsageProvider(_tempHome); // baseline 0, no files yet
        var today = DateTimeOffset.UtcNow.ToString("O");
        var path = WriteSessionFile(
            "{\"timestamp\":\"" + today + "\",\"message\":{\"usage\":{\"input_tokens\":500,\"output_tokens\":0,\"cache_creation_input_tokens\":0,\"cache_read_input_tokens\":0}}}");
        // Prove the mtime check (not the internal timestamp field) is what excludes this file:
        // the JSON's own "timestamp" says today, but the file's mtime is backdated to yesterday.
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-1));

        var units = provider.ReadNewUnitsSinceLastCall();

        Assert.Equal(0, units);
    }

    [Fact]
    public void ReadNewUnitsSinceLastCall_LockedFile_IsSkippedButOtherFilesStillCounted()
    {
        var provider = new ClaudeCodeUsageProvider(_tempHome); // baseline 0, no files yet
        var today = DateTimeOffset.UtcNow.ToString("O");
        var lockedPath = WriteSessionFile(
            "{\"timestamp\":\"" + today + "\",\"message\":{\"usage\":{\"input_tokens\":5000,\"output_tokens\":0,\"cache_creation_input_tokens\":0,\"cache_read_input_tokens\":0}}}");
        WriteSessionFile(
            "{\"timestamp\":\"" + today + "\",\"message\":{\"usage\":{\"input_tokens\":100,\"output_tokens\":0,\"cache_creation_input_tokens\":0,\"cache_read_input_tokens\":0}}}");

        using var lockStream = new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.None);

        var exception = Record.Exception(() =>
        {
            var units = provider.ReadNewUnitsSinceLastCall();
            Assert.Equal(2, units); // only the 100-token file counted (100/50); locked 5000-token file skipped
        });

        Assert.Null(exception);
    }

    [Fact]
    public void ReadNewUnitsSinceLastCall_AppliesTokenToActivityUnitDivisor()
    {
        const long divisor = 50; // matches ClaudeCodeUsageProvider.TokenToActivityUnitDivisor
        var provider = new ClaudeCodeUsageProvider(_tempHome); // baseline 0, no files yet
        var today = DateTimeOffset.UtcNow.ToString("O");
        WriteSessionFile(
            "{\"timestamp\":\"" + today + "\",\"message\":{\"usage\":{\"input_tokens\":" + (divisor * 7) + ",\"output_tokens\":0,\"cache_creation_input_tokens\":0,\"cache_read_input_tokens\":0}}}");

        var units = provider.ReadNewUnitsSinceLastCall();

        Assert.Equal(7, units);
    }
}
