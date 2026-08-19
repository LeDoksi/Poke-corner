using System.Text.Json;

namespace PokeCorner.Core.ActivitySources;

public sealed class ClaudeCodeUsageProvider : IActivitySource
{
    private readonly string _projectsDirectory;
    private long _totalCountedSoFar;
    private DateTime _totalCountedSoFarDate = DateTime.MinValue;

    public ClaudeCodeUsageProvider(string claudeHomeDirectory)
    {
        _projectsDirectory = Path.Combine(claudeHomeDirectory, "projects");
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
        var newUnits = Math.Max(0, todayTotal - _totalCountedSoFar);
        _totalCountedSoFar = todayTotal;
        return newUnits;
    }

    private long SumTodaysTokens()
    {
        if (!Directory.Exists(_projectsDirectory)) return 0;

        var today = DateTimeOffset.UtcNow.Date;
        long total = 0;

        foreach (var file in Directory.EnumerateFiles(_projectsDirectory, "*.jsonl", SearchOption.AllDirectories))
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

        return total;
    }

    private static long GetLongOrZero(JsonElement obj, string propertyName) =>
        obj.TryGetProperty(propertyName, out var value) && value.TryGetInt64(out var number) ? number : 0;
}
