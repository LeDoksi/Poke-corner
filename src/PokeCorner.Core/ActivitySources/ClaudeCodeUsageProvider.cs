using System.Text.Json;

namespace PokeCorner.Core.ActivitySources;

public sealed class ClaudeCodeUsageProvider : IActivitySource
{
    /// <summary>
    /// Raw Claude Code token counts run 100-1000x the scale of a mouse click or keystroke;
    /// dividing by this before mixing into the shared activity pool keeps game pacing sane.
    /// </summary>
    private const long TokenToActivityUnitDivisor = 50;

    private readonly string _projectsDirectory;
    private long _totalCountedSoFar;
    private DateTime _totalCountedSoFarDate;

    public ClaudeCodeUsageProvider(string claudeHomeDirectory)
    {
        _projectsDirectory = Path.Combine(claudeHomeDirectory, "projects");
        // Establish the baseline from whatever already happened today, so the first
        // ReadNewUnitsSinceLastCall() call only reports usage that occurs after construction
        // instead of retroactively dumping the whole day's history.
        _totalCountedSoFarDate = DateTimeOffset.UtcNow.Date;
        _totalCountedSoFar = SumTodaysTokens();
    }

    public long ReadNewUnitsSinceLastCall()
    {
        var today = DateTimeOffset.UtcNow.Date;
        if (today != _totalCountedSoFarDate)
        {
            _totalCountedSoFar = 0;
            _totalCountedSoFarDate = today;
        }

        var todayTotal = SumTodaysTokens();
        var newRawUnits = Math.Max(0, todayTotal - _totalCountedSoFar);
        _totalCountedSoFar = todayTotal;
        return newRawUnits / TokenToActivityUnitDivisor;
    }

    private long SumTodaysTokens()
    {
        if (!Directory.Exists(_projectsDirectory)) return 0;

        var today = DateTimeOffset.UtcNow.Date;
        long total = 0;

        foreach (var file in Directory.EnumerateFiles(_projectsDirectory, "*.jsonl", SearchOption.AllDirectories))
        {
            // Skip files from other days entirely without opening them: it's both a perf win
            // (no need to re-read/re-parse stale history every poll) and it incidentally
            // reduces (without eliminating) the odds of touching an actively-written file.
            if (File.GetLastWriteTimeUtc(file).Date != today) continue;

            try
            {
                foreach (var line in File.ReadLines(file))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    JsonDocument doc;
                    try
                    {
                        doc = JsonDocument.Parse(line);
                    }
                    catch (JsonException)
                    {
                        continue; // skip malformed/partial lines
                    }

                    using (doc)
                    {
                        if (!doc.RootElement.TryGetProperty("timestamp", out var timestampElement)) continue;
                        if (!DateTimeOffset.TryParse(timestampElement.GetString(), out var timestamp)) continue;
                        if (timestamp.UtcDateTime.Date != today) continue;

                        if (!doc.RootElement.TryGetProperty("message", out var message)) continue;
                        if (!message.TryGetProperty("usage", out var usage)) continue;

                        total += GetLongOrZero(usage, "input_tokens");
                        total += GetLongOrZero(usage, "output_tokens");
                        total += GetLongOrZero(usage, "cache_creation_input_tokens");
                        total += GetLongOrZero(usage, "cache_read_input_tokens");
                    }
                }
            }
            catch (IOException)
            {
                // Most likely the current session's file, actively being written to.
                // Skip it for this poll rather than crashing the caller; we'll catch up next time.
            }
            catch (UnauthorizedAccessException)
            {
                // Can't read this file this poll — skip it and continue with the rest.
            }
        }

        return total;
    }

    private static long GetLongOrZero(JsonElement obj, string propertyName) =>
        obj.TryGetProperty(propertyName, out var value) && value.TryGetInt64(out var number) ? number : 0;
}
