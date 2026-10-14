using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.Core.Domain;

public sealed record EvolutionBranch(int SpeciesId, string Name, bool Available, string? Reason);
public sealed record EvolutionPreview(IReadOnlyList<EvolutionBranch> Branches, string BerryId, int BerryCost,
    int BerriesOwned, int CareRequired, int CareOwned, int StonesOwned, bool IsBusy, string? Reason);
public sealed record EvolutionAction(bool Success, string Message, int? SpeciesId = null, bool Repeated = false);

public sealed class EvolutionService
{
    private sealed record Edge(int From, int To, string Berry, int Quantity, bool NormalAvailable, bool ShinyAvailable);

    private readonly CatalogPackage catalog;
    private readonly SaveData state;
    private readonly Dictionary<int, Edge[]> edges;

    public EvolutionService(CatalogPackage catalog, SaveData state)
    {
        this.catalog = catalog;
        this.state = state;
        using var rules = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.DataRoot, "final", "catalog-rules.json")));
        using var balance = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.DataRoot, "final", "balance.json")));
        var cost = balance.RootElement.GetProperty("evolutionBerries").GetInt32();
        edges = rules.RootElement.GetProperty("evolutionEdges").EnumerateArray()
            .Select(x => new Edge(x.GetProperty("from").GetInt32(), x.GetProperty("to").GetInt32(),
                x.GetProperty("berry").GetString()!, x.GetProperty("quantity").GetInt32(),
                x.GetProperty("normalAvailable").GetBoolean(), x.GetProperty("shinyAvailable").GetBoolean()))
            .GroupBy(x => x.From).ToDictionary(g => g.Key, g => g.OrderBy(x => x.To).ToArray());
        if (edges.Values.Any(group => group.Any(x => x.Quantity != cost || x.Berry != group[0].Berry)))
            throw new InvalidDataException("Несогласованный рецепт эволюции в каталоге.");
    }

    public EvolutionPreview Preview(Guid individualId)
    {
        var individual = state.Individuals.FirstOrDefault(x => x.Id == individualId)
            ?? throw new InvalidOperationException("Этот покемон больше не найден.");
        var next = edges.GetValueOrDefault(individual.CurrentSpeciesId) ?? [];
        var branches = next.Select(edge =>
        {
            var species = catalog.Species.FirstOrDefault(x => x.Id == edge.To);
            var available = species is not null && (individual.IsShiny ? edge.ShinyAvailable : edge.NormalAvailable)
                && catalog.CanObtain(edge.To, individual.IsShiny) && species.FamilyRootId == individual.FamilyRootId;
            return new EvolutionBranch(edge.To, species?.DisplayName ?? $"#{edge.To}", available,
                available ? null : "Вариант этого вида пока недоступен.");
        }).ToArray();
        var busy = ExpeditionService.BusyIndividualIds(state).Contains(individualId);
        var required = EvolutionCalculator.RequiredCareForNext(individual.CurrentStage);
        var berry = next.FirstOrDefault()?.Berry ?? string.Empty;
        var count = next.FirstOrDefault()?.Quantity ?? 0;
        var owned = state.Berries.GetValueOrDefault(berry);
        var reason = next.Length == 0 ? "Дальше этот вид не эволюционирует."
            : branches.All(x => !x.Available) ? "Для следующей формы нет доступного варианта."
            : busy ? "Покемон занят в походе или питомнике."
            : individual.AccumulatedCarePoints < required ? $"Нужно ещё {required - individual.AccumulatedCarePoints:N0} ухода."
            : owned < count ? $"Не хватает {count - owned} ягод {berry}."
            : null;
        return new EvolutionPreview(branches, berry, count, owned, required, individual.AccumulatedCarePoints,
            state.Items.GetValueOrDefault("evolution-stone"), busy, reason);
    }

    public EvolutionAction Evolve(Guid individualId, int? selectedBranch, Guid operationId)
    {
        if (state.SchemaVersion != 2 || operationId == Guid.Empty)
            return new(false, "Требуется сохранение v2 и ID действия.");
        var key = $"evolution:{operationId}";
        var fingerprint = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"evolution:{individualId:N}:{selectedBranch?.ToString() ?? "random"}")).AsSpan(0, 16));
        if (state.CompletedOperations.TryGetValue(key, out var previous))
            return previous == fingerprint && state.EvolutionResults.TryGetValue(operationId, out var previousSpecies)
                ? new(true, "Эволюция уже сохранена.", previousSpecies, true)
                : new(false, "ID действия уже использован с другим покемоном или выбором.");

        var individual = state.Individuals.FirstOrDefault(x => x.Id == individualId);
        if (individual is null) return new(false, "Этот покемон больше не найден.");
        var preview = Preview(individualId);
        if (preview.Reason is not null) return new(false, preview.Reason);
        var available = preview.Branches.Where(x => x.Available).ToArray();
        if (selectedBranch is { } selected && !available.Any(x => x.SpeciesId == selected))
            return new(false, "Выбранная ветвь недоступна.");
        var needsStone = selectedBranch is not null && available.Length > 1;
        if (needsStone && preview.StonesOwned < 1) return new(false, "Для выбора ветви нужен камень эволюции.");
        var chosen = selectedBranch ?? available[BinaryPrimitives.ReadUInt32LittleEndian(
            SHA256.HashData(Encoding.UTF8.GetBytes($"evolution-draw:{operationId:N}:{individualId:N}"))) % (uint)available.Length].SpeciesId;

        state.Berries[preview.BerryId] -= preview.BerryCost;
        if (needsStone) state.Items["evolution-stone"]--;
        var from = individual.CurrentSpeciesId;
        individual.CurrentSpeciesId = chosen;
        individual.CurrentStage++;
        individual.ReachedSpeciesIds.Add(chosen);
        state.DiscoveredSpeciesIds.Add(chosen);
        if (individual.IsShiny) state.ShinySpeciesIds.Add(chosen);
        state.EvolutionTransitions.Add($"{from}->{chosen}");
        state.CompletedOperations[key] = fingerprint;
        state.EvolutionResults[operationId] = chosen;
        return new(true, $"Эволюция: {available.Single(x => x.SpeciesId == chosen).Name}.", chosen);
    }
}
