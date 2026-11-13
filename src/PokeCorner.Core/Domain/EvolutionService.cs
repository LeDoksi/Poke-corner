using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.Core.Domain;

public sealed record EvolutionBranch(int SpeciesId, string Name, bool Available, string? Reason);
public sealed record EvolutionPreview(IReadOnlyList<EvolutionBranch> Branches, string BerryId, int BerryCost,
    int BerriesOwned, int CareRequired, int CareOwned, int StonesOwned, bool IsBusy, string? Reason)
{
    /// D-33: only missing care stands in the way, and an evolution stone can cover it.
    public bool StoneCanFinish => CareOwned < CareRequired && StonesOwned > 0 && ReasonWithStone is null;
    /// What would still block the evolution if a stone covered the care (null = nothing).
    public string? ReasonWithStone { get; init; }
}
public sealed record EvolutionAction(bool Success, string Message, int? SpeciesId = null, bool Repeated = false);

public sealed class EvolutionService
{
    private sealed record Edge(int From, int To, string Berry, int Quantity, bool NormalAvailable, bool ShinyAvailable);

    private readonly CatalogPackage catalog;
    private readonly SaveData state;
    private readonly Dictionary<int, Edge[]> edges;
    private readonly TraitRules traits;
    private readonly LevelProgression levels;

    public EvolutionService(CatalogPackage catalog, SaveData state)
    {
        this.catalog = catalog;
        this.state = state;
        traits = new TraitRules(catalog.DataRoot);
        levels = new LevelProgression(catalog.DataRoot);
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
        var required = EvolutionCalculator.RequiredCareForNext(individual, traits, levels, catalog);
        var berry = next.FirstOrDefault()?.Berry ?? string.Empty;
        var count = next.FirstOrDefault()?.Quantity ?? 0;
        var owned = state.Berries.GetValueOrDefault(berry);
        var withStone = next.Length == 0 ? "Дальше этот вид не эволюционирует."
            : branches.All(x => !x.Available) ? "Для следующей формы нет доступного варианта."
            : busy ? "Покемон занят в походе или питомнике."
            : owned < count ? $"Не хватает {count - owned} ягод {berry}."
            : null;
        var reason = next.Length == 0 ? "Дальше этот вид не эволюционирует."
            : branches.All(x => !x.Available) ? "Для следующей формы нет доступного варианта."
            : busy ? "Покемон занят в походе или питомнике."
            : individual.AccumulatedCarePoints < required ? $"Нужно ещё {required - individual.AccumulatedCarePoints:N0} ухода."
            : withStone;
        return new EvolutionPreview(branches, berry, count, owned, required, individual.AccumulatedCarePoints,
            state.Items.GetValueOrDefault("evolution-stone"), busy, reason) { ReasonWithStone = withStone };
    }

    /// selectedBranch picks the next form for free (D-33); useStone spends an evolution stone in place of missing care.
    public EvolutionAction Evolve(Guid individualId, int? selectedBranch, Guid operationId, bool useStone = false)
    {
        if (state.SchemaVersion != 2 || operationId == Guid.Empty)
            return new(false, "Требуется сохранение v2 и ID действия.");
        var key = $"evolution:{operationId}";
        var fingerprint = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"evolution:{individualId:N}:{selectedBranch?.ToString() ?? "random"}" + (useStone ? ":stone" : ""))).AsSpan(0, 16));
        if (state.CompletedOperations.TryGetValue(key, out var previous))
            return previous == fingerprint && state.EvolutionResults.TryGetValue(operationId, out var previousSpecies)
                ? new(true, "Эволюция уже сохранена.", previousSpecies, true)
                : new(false, "ID действия уже использован с другим покемоном или выбором.");

        var individual = state.Individuals.FirstOrDefault(x => x.Id == individualId);
        if (individual is null) return new(false, "Этот покемон больше не найден.");
        var preview = Preview(individualId);
        var careShort = preview.CareOwned < preview.CareRequired;
        var needsStone = useStone && careShort;
        if (useStone && !careShort) return new(false, "Уход уже набран — камень не нужен.");
        if (needsStone && preview.StonesOwned < 1) return new(false, "Нет камня эволюции.");
        if ((needsStone ? preview.ReasonWithStone : preview.Reason) is { } blocked) return new(false, blocked);
        var available = preview.Branches.Where(x => x.Available).ToArray();
        if (selectedBranch is { } selected && !available.Any(x => x.SpeciesId == selected))
            return new(false, "Выбранная ветвь недоступна.");
        var choseBranch = selectedBranch is not null && available.Length > 1;
        var canonicalLength = CanonicalLength(individual.SpeciesId, new HashSet<int>());
        if ((int)individual.CurrentStage + 1 >= individual.ChainLength)
        {
            if (individual.ChainLength != 1 || individual.CurrentStage != EvolutionStage.Stage1 ||
                individual.AcquisitionSource != "update-gift" || individual.SpeciesId != individual.FamilyRootId ||
                individual.CurrentSpeciesId != individual.SpeciesId ||
                !individual.ReachedSpeciesIds.SequenceEqual(new[] { individual.SpeciesId }) || canonicalLength < 2)
                return new(false, "Эволюционная цепочка этого покемона не согласована с каталогом.");
            individual.ReconcileGiftChainLength(canonicalLength);
        }
        var chosen = selectedBranch ?? available[BinaryPrimitives.ReadUInt32LittleEndian(
            SHA256.HashData(Encoding.UTF8.GetBytes($"evolution-draw:{operationId:N}:{individualId:N}"))) % (uint)available.Length].SpeciesId;

        state.Berries[preview.BerryId] -= preview.BerryCost;
        if (needsStone)
        {
            state.Items["evolution-stone"]--;
            // The stone fills the gap: the next stage counts from this threshold, as if the care had been earned.
            individual.AccumulatedCarePoints = Math.Max(individual.AccumulatedCarePoints, preview.CareRequired);
        }
        var from = individual.CurrentSpeciesId;
        individual.CurrentSpeciesId = chosen;
        individual.CurrentStage++;
        individual.ReachedSpeciesIds.Add(chosen);
        state.DiscoveredSpeciesIds.Add(chosen);
        if (individual.IsShiny) state.ShinySpeciesIds.Add(chosen);
        state.NormalSpeciesIds.Add(chosen);
        state.EvolutionTransitions.Add($"{from}->{chosen}");
        AchievementMetrics.Record(state, "special:trophy-special-berry-evolution");
        if (choseBranch) AchievementMetrics.Record(state, "special:trophy-special-branch-choice");
        state.CompletedOperations[key] = fingerprint;
        state.EvolutionResults[operationId] = chosen;
        return new(true, $"Эволюция: {available.Single(x => x.SpeciesId == chosen).Name}.", chosen);
    }

    /// How many stages a species still has in front of it, itself included (1 = it never evolves further).
    public static IReadOnlyDictionary<int, int> Depths(CatalogPackage catalog)
    {
        using var rules = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.DataRoot, "final", "catalog-rules.json")));
        var next = rules.RootElement.GetProperty("evolutionEdges").EnumerateArray()
            .GroupBy(x => x.GetProperty("from").GetInt32(), x => x.GetProperty("to").GetInt32())
            .ToDictionary(g => g.Key, g => g.ToArray());
        var depths = new Dictionary<int, int>();
        int Depth(int id, int guard) => depths.TryGetValue(id, out var known) ? known
            : depths[id] = 1 + (guard > 0 && next.TryGetValue(id, out var to) ? to.Max(child => Depth(child, guard - 1)) : 0);
        foreach (var species in catalog.Species) Depth(species.Id, 3);
        return depths;
    }

    /// A pet's chain is as long as its own branch. Saves from v1 counted every species of the family in a row
    /// (Meowth, Persian, Perrserker = 3), and a branch can be shorter than its family (Flapple): such a pet looked
    /// one evolution short for ever, with a care bar that led nowhere (Georgy 14.11, Persian). Only ever shortens.
    public static void SyncChainLengths(SaveData state, IReadOnlyDictionary<int, int>? depths)
    {
        if (depths is null) return;
        foreach (var pet in state.Individuals)
            if (pet.CurrentSpeciesId > 0 && depths.TryGetValue(pet.CurrentSpeciesId, out var depth)
                && (int)pet.CurrentStage + depth is var length and >= 1 && length < pet.ChainLength)
                pet.ReconcileGiftChainLength(length);
    }

    private int CanonicalLength(int speciesId, HashSet<int> path)
    {
        if (!path.Add(speciesId)) throw new InvalidDataException("Цикл в канонической цепочке эволюции.");
        var length = 1 + (edges.GetValueOrDefault(speciesId) ?? [])
            .Select(edge => CanonicalLength(edge.To, path)).DefaultIfEmpty(0).Max();
        path.Remove(speciesId);
        if (length is < 1 or > 3) throw new InvalidDataException("Неверная длина канонической цепочки эволюции.");
        return length;
    }
}
