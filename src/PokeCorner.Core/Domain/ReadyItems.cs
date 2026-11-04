using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.Core.Domain;

public enum ReadyKind { Evolution, Expedition, Nursery, Catch }

/// One thing that is waiting for the player (D-31). Text is what the corner chip and the toast say.
public sealed record ReadyItem(ReadyKind Kind, string Text, Guid? IndividualId = null);

/// The single list of "ready" things that feeds the corner strip, the rail dots, the badge over the
/// desktop pet and the tray toasts, so these places never disagree.
public static class ReadyItems
{
    // Below this no stage can be ready even with the fastest trait; skips the preview for most pets.
    private const int CareFloor = 50_000;

    public static IReadOnlyList<ReadyItem> Collect(CatalogPackage catalog, SaveData data, int pendingCatches, DateTimeOffset now)
    {
        var items = new List<ReadyItem>();
        if (data.Expedition is { } trip && (trip.Status == "ready" || trip.Status == "running" && now >= trip.ReadyAtUtc))
            items.Add(new(ReadyKind.Expedition, "Поход вернулся"));
        if (data.Nest is { } nest && (nest.Status == "ready" || nest.Status == "running" && now >= nest.ReadyAtUtc))
            items.Add(new(ReadyKind.Nursery, "Малыш готов"));
        if (pendingCatches > 0)
            items.Add(new(ReadyKind.Catch, pendingCatches == 1 ? "Покебол готов" : $"Покеболы: {pendingCatches}"));

        var candidates = data.Individuals.Where(x => x.AccumulatedCarePoints >= CareFloor)
            .OrderByDescending(x => x.Id == data.ActiveIndividualId).ToArray();
        if (candidates.Length == 0 || data.SchemaVersion != 2) return items;
        var busy = ExpeditionService.BusyIndividualIds(data);
        var service = new EvolutionService(catalog, data);
        var evolutions = new List<ReadyItem>();
        foreach (var pet in candidates)
        {
            if (busy.Contains(pet.Id)) continue;
            var preview = service.Preview(pet.Id);
            if (preview.IsBusy || preview.CareOwned < preview.CareRequired || !preview.Branches.Any(x => x.Available)) continue;
            var name = string.IsNullOrWhiteSpace(pet.Nickname)
                ? catalog.Species.FirstOrDefault(x => x.Id == pet.CurrentSpeciesId)?.DisplayName ?? $"№{pet.CurrentSpeciesId:D4}"
                : pet.Nickname!;
            evolutions.Add(new(ReadyKind.Evolution, $"{name}: пора эволюционировать", pet.Id));
        }
        items.InsertRange(0, evolutions);
        return items;
    }
}
