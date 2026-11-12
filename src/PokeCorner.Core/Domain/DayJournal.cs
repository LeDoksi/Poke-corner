using PokeCorner.Core.Persistence;

namespace PokeCorner.Core.Domain;

public enum DayEventKind { Met, Hatched, Expedition, Award, Running }

/// One line of the «Сегодня» column (D-38): something that happened since local midnight, or is under way.
public sealed record DayEvent(DayEventKind Kind, DateTimeOffset AtUtc, string Text, bool IsShiny = false);

/// Today's diary page. Read straight from the save (who joined today and how, awards, the trip and the nest),
/// so it needs no new saved field and can never disagree with the collection.
public static class DayJournal
{
    public static IReadOnlyList<DayEvent> Today(SaveData data, DateTimeOffset now, Func<int, string> speciesName, TimeSpan? localOffset = null)
    {
        var offset = localOffset ?? TimeZoneInfo.Local.GetUtcOffset(now);
        var dayStart = new DateTimeOffset(now.ToOffset(offset).Date, offset);
        bool IsToday(DateTimeOffset at) => at >= dayStart && at <= now;
        var events = new List<DayEvent>();
        foreach (var pet in data.Individuals.Where(p => IsToday(p.CaughtAtUtc)))
        {
            var id = pet.SpeciesId;
            var name = string.IsNullOrWhiteSpace(pet.Nickname) ? speciesName(id) : $"{pet.Nickname} ({speciesName(id)})";
            var shine = pet.IsShiny ? ", шайни" : "";
            events.Add(pet.AcquisitionSource switch
            {
                "nursery" => new(DayEventKind.Hatched, pet.CaughtAtUtc, $"В питомнике вылупился {name}{shine}", pet.IsShiny),
                "expedition" => new(DayEventKind.Expedition, pet.CaughtAtUtc, $"Из похода пришёл {name}{shine}", pet.IsShiny),
                "starter" or "update-gift" => new(DayEventKind.Met, pet.CaughtAtUtc, $"К нам присоединился {name}{shine}", pet.IsShiny),
                _ => new(DayEventKind.Met, pet.CaughtAtUtc, $"Встретили: {name}{shine}", pet.IsShiny)
            });
        }

        var awards = data.AwardLedger.Values.Where(a => IsToday(a.AwardedAtUtc)).ToArray();
        if (awards.Length > 0)
            events.Add(new(DayEventKind.Award, awards.Max(a => a.AwardedAtUtc),
                awards.Length == 1 ? "Новая награда в альбоме" : $"Новые награды в альбоме: {awards.Length}"));
        if (data.Expedition is { Status: "claimed" } claimed && IsToday(claimed.ReadyAtUtc))
            events.Add(new(DayEventKind.Expedition, claimed.ReadyAtUtc, "Команда вернулась из похода"));
        return events.OrderByDescending(e => e.AtUtc).ToArray();
    }

    /// What is still under way: the trip and the nest, with the moment each ends.
    public static IReadOnlyList<DayEvent> Running(SaveData data, DateTimeOffset now)
    {
        var running = new List<DayEvent>();
        if (data.Expedition is { Status: "running" } trip && now < trip.ReadyAtUtc)
            running.Add(new(DayEventKind.Expedition, trip.ReadyAtUtc, "Команда в походе"));
        if (data.Nest is { Status: "running" } nest && now < nest.ReadyAtUtc)
            running.Add(new(DayEventKind.Hatched, nest.ReadyAtUtc, "Яйцо в питомнике"));
        return running;
    }

    public static string Remaining(DateTimeOffset readyAtUtc, DateTimeOffset now)
    {
        var left = readyAtUtc - now;
        if (left <= TimeSpan.Zero) return "готово";
        return left.TotalHours >= 1 ? $"{(int)left.TotalHours} ч {left.Minutes:D2} мин" : left.TotalMinutes >= 1 ? $"{left.Minutes} мин" : "меньше минуты";
    }
}
