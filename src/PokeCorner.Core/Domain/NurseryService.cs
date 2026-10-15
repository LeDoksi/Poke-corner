using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.Core.Domain;

public sealed record NurseryRequest(IReadOnlyList<Guid> ParentIds, int? SelectedSpeciesId, Guid OperationId, DateTimeOffset NowUtc);
public sealed record NurseryPreview(IReadOnlyList<NurseryOutcome> Outcomes, IReadOnlyDictionary<string, int> Berries,
    int Hours, DateTimeOffset ReadyAtUtc, IReadOnlyList<string> TraitIds, IReadOnlyDictionary<int, double> ShinyChances,
    bool SelectionUseful, IReadOnlyDictionary<int, double> OutcomeChances,
    IReadOnlyDictionary<int, IReadOnlyList<string>> TraitsBySpecies,
    IReadOnlyDictionary<int, IReadOnlyList<string>> ShinyTraitsBySpecies);
public sealed record NurseryAction(bool Success, string Message, bool Repeated = false, PokemonIndividual? Child = null);

// Mutate only a proposed SaveData; AppState.CommitAsync publishes the complete candidate after the write.
public sealed class NurseryService
{
    private readonly CatalogPackage catalog;
    private readonly SaveData state;
    private readonly NurseryCatalog recipes;
    private readonly TraitRules traits;
    private readonly LevelProgression levels;
    private readonly ShopService shop;
    private readonly decimal maxReduction;
    private readonly Dictionary<int, int[]> edges;

    public NurseryService(CatalogPackage catalog, SaveData state)
    {
        this.catalog = catalog;
        this.state = state;
        recipes = new NurseryCatalog(catalog);
        traits = new TraitRules(catalog.DataRoot);
        levels = new LevelProgression(catalog.DataRoot);
        shop = new ShopService(catalog.DataRoot);
        using var balance = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.DataRoot, "final", "balance.json")));
        maxReduction = balance.RootElement.GetProperty("maxNurseryTimeReduction").GetDecimal();
        using var rules = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.DataRoot, "final", "catalog-rules.json")));
        edges = rules.RootElement.GetProperty("evolutionEdges").EnumerateArray()
            .GroupBy(x => x.GetProperty("from").GetInt32())
            .ToDictionary(g => g.Key, g => g.Select(x => x.GetProperty("to").GetInt32()).ToArray());
    }

    public NurseryPreview Preview(IReadOnlyList<Guid> parentIds, int? selectedSpeciesId = null, DateTimeOffset? now = null)
    {
        var (parents, recipe) = Pair(parentIds);
        if (state.Nest?.Status is "running" or "ready") throw new InvalidOperationException("Гнездо уже занято.");
        if (parentIds.Any(ExpeditionService.BusyIndividualIds(state).Contains))
            throw new InvalidOperationException("Родитель уже занят.");
        if (!recipe.IsAvailable) throw new InvalidOperationException("У этой пары нет доступного потомства.");
        if (selectedSpeciesId is not null && (!recipe.SelectionUseful ||
            !recipe.AvailableOutcomes.Any(x => x.SpeciesId == selectedSpeciesId)))
            throw new InvalidOperationException("Выбранный вид недоступен для этой пары.");
        var berries = recipe.BaseBerries.ToDictionary(x => x.Key, x => x.Value);
        if (selectedSpeciesId is not null)
            foreach (var (id, count) in recipe.SelectedSurcharge) berries[id] = berries.GetValueOrDefault(id) + count;
        var traitsBySpecies = recipe.AvailableOutcomes.ToDictionary(x => x.SpeciesId,
            x => (IReadOnlyList<string>)Inheritance(parents, x.SpeciesId, false));
        var shinyTraitsBySpecies = recipe.AvailableOutcomes.ToDictionary(x => x.SpeciesId,
            x => (IReadOnlyList<string>)(x.ShinyAvailable ? Inheritance(parents, x.SpeciesId, true) : []));
        var visibleSpecies = selectedSpeciesId is { } chosen ? new[] { chosen } :
            recipe.AvailableOutcomes.Select(x => x.SpeciesId);
        var traitIds = visibleSpecies.SelectMany(id => traitsBySpecies[id].Concat(shinyTraitsBySpecies[id]))
            .Distinct().Order().ToArray();
        var reduction = Math.Min(maxReduction, TraitRules.Stack(parents.SelectMany(p =>
            new[] { p.FirstTraitId, p.SecondTraitId }.Where(id => id == "caring-parent")
                .Select(_ => traits.Value("caring-parent", levels.TraitTierForXp(p.TotalXp))))));
        var hours = recipe.Hours!.Value;
        var ready = (now ?? DateTimeOffset.UtcNow).ToUniversalTime()
            .AddTicks(checked((long)(TimeSpan.FromHours(hours).Ticks * (1m - reduction))));
        var weight = recipe.AvailableOutcomes.Sum(x => x.Weight);
        return new(recipe.AvailableOutcomes, berries, hours, ready, traitIds,
            recipe.AvailableOutcomes.ToDictionary(x => x.SpeciesId,
                x => ShinyRules.Chance(ShinySource.Nursery, state.NurseryLuckBonus,
                    state.ShopCatchLuckyCharmActive, x.ShinyAvailable)), recipe.SelectionUseful,
            recipe.AvailableOutcomes.ToDictionary(x => x.SpeciesId,
                x => selectedSpeciesId is null ? (double)x.Weight / weight : x.SpeciesId == selectedSpeciesId ? 1d : 0d),
            traitsBySpecies, shinyTraitsBySpecies);
    }

    public NurseryAction Start(NurseryRequest request)
    {
        if (request.OperationId == Guid.Empty) throw new InvalidOperationException("Требуется ID запуска.");
        var fingerprint = Fingerprint(request.ParentIds, request.SelectedSpeciesId);
        var key = $"nursery:start:{request.OperationId}";
        if (state.CompletedOperations.TryGetValue(key, out var previous))
        {
            if (previous != fingerprint) throw new InvalidOperationException("ID запуска уже использован с другой парой или исходом.");
            return new(true, "Гнездо уже запущено.", true);
        }
        if (state.SchemaVersion != 2) throw new InvalidOperationException("Требуется сохранение v2.");
        var preview = Preview(request.ParentIds, request.SelectedSpeciesId, request.NowUtc);
        var (parents, recipe) = Pair(request.ParentIds);
        if (preview.Berries.Any(x => state.Berries.GetValueOrDefault(x.Key) < x.Value))
            throw new InvalidOperationException("Недостаточно ягод для гнезда.");
        var random = Draw(request.OperationId);
        NurseryOutcome outcome;
        if (request.SelectedSpeciesId is { } chosen)
        {
            var choice = shop.TrySpendChoiceItem(state, "offspring-charm", recipe.AvailableOutcomes.Count, request.OperationId);
            if (!choice.Success) throw new InvalidOperationException(choice.Message);
            outcome = recipe.AvailableOutcomes.Single(x => x.SpeciesId == chosen);
        }
        else
        {
            var total = recipe.AvailableOutcomes.Sum(x => x.Weight);
            var pick = random.Next(total);
            outcome = recipe.AvailableOutcomes.First(x => (pick -= x.Weight) < 0);
        }
        var shinyChance = ShinyRules.Chance(ShinySource.Nursery, state.NurseryLuckBonus,
            state.ShopCatchLuckyCharmActive, outcome.ShinyAvailable);
        var shinyDraw = new ShinyDraw(random.NextDouble(), shinyChance);
        var isShiny = ShinyRules.Resolve(ShinySource.Nursery, shinyDraw);
        var pool = Inheritance(parents, outcome.SpeciesId, isShiny);
        var species = catalog.Species.Single(x => x.Id == outcome.SpeciesId);
        var child = new PokemonIndividual
        {
            Id = request.OperationId, SpeciesId = species.Id, CurrentSpeciesId = species.Id,
            FamilyRootId = species.FamilyRootId, EvolutionChainId = species.EvolutionChainId,
            ChainLength = ChainLength(species.Id, []), ReachedSpeciesIds = [species.Id],
            Nature = (Nature)random.Next(Enum.GetValues<Nature>().Length), IsShiny = isShiny,
            CaughtAtUtc = request.NowUtc.ToUniversalTime(), AcquisitionSource = "nursery",
            FirstTraitId = pool[random.Next(pool.Length)], ParentIds = request.ParentIds.ToList()
        };
        if (state.Individuals.Any(x => x.Id == child.Id)) throw new InvalidOperationException("ID запуска уже принадлежит питомцу.");
        foreach (var (id, count) in preview.Berries) state.Berries[id] -= count;
        shop.TryConsumeLuckyCharm(state, outcome.ShinyAvailable);
        state.Nest = new NurseryState
        {
            Id = request.OperationId, ParticipantIds = request.ParentIds.ToList(), Status = "running",
            StartedAtUtc = request.NowUtc.ToUniversalTime(), ReadyAtUtc = preview.ReadyAtUtc,
            DurationMinutes = checked(preview.Hours * 60), ResourcePrices = preview.Berries.ToDictionary(x => x.Key, x => x.Value),
            Child = child, ShinySupported = outcome.ShinyAvailable, ShinyDraw = shinyDraw,
            SelectedSpeciesId = request.SelectedSpeciesId,
            PossibleOutcomeSpeciesIds = preview.Outcomes.Select(x => x.SpeciesId).ToList(),
            Conditions = JsonSerializer.SerializeToElement(new { request.SelectedSpeciesId }),
            Outcomes = JsonSerializer.SerializeToElement(new { child.SpeciesId, child.IsShiny })
        };
        state.CompletedOperations[key] = fingerprint;
        if (request.SelectedSpeciesId is not null)
            AchievementMetrics.Record(state, "special:trophy-special-offspring-choice");
        return new(true, "Гнездо запущено.");
    }

    public NurseryAction Hatch(Guid id, Guid operationId, DateTimeOffset? now = null)
    {
        if (id == Guid.Empty || operationId == Guid.Empty) throw new InvalidOperationException("Требуется ID вылупления.");
        if (state.CompletedOperations.TryGetValue($"nursery:hatch:{id}", out var completed))
            return completed == operationId
                ? new(true, "Малыш уже встречен.", true, FrozenChild(id))
                : new(false, "Малыш уже был встречен.");
        var nest = state.Nest;
        if (nest?.Id != id || nest.Status is not ("running" or "ready"))
            throw new InvalidOperationException("Гнездо не найдено.");
        if (nest.Status == "running" && (now ?? DateTimeOffset.UtcNow).ToUniversalTime() < nest.ReadyAtUtc)
            return new(false, "Малыш ещё не готов.");
        if (state.Individuals.Any(x => x.Id == nest.Child.Id)) throw new InvalidDataException("Потомок уже выдан.");
        state.Individuals.Add(Copy(nest.Child));
        state.HatchedNurseryResults[id] = Copy(nest.Child);
        state.DiscoveredSpeciesIds.Add(nest.Child.SpeciesId);
        if (nest.Child.IsShiny) state.ShinySpeciesIds.Add(nest.Child.SpeciesId);
        nest.LuckBeforeHatch = state.NurseryLuckBonus;
        state.NurseryLuckBonus = ShinyRules.NextNurseryLuck(state.NurseryLuckBonus, nest.Child.IsShiny, nest.ShinySupported);
        nest.Status = "hatched";
        nest.HatchOperationId = operationId;
        state.CompletedOperations[$"nursery:hatch:{id}"] = operationId;
        state.Metrics["hatched-children"] = checked((state.Metrics.GetValueOrDefault("hatched-children") ?? 0) + 1);
        return new(true, "Малыш появился.", false, Copy(nest.Child));
    }

    private PokemonIndividual? FrozenChild(Guid id) =>
        state.HatchedNurseryResults.TryGetValue(id, out var saved) ? Copy(saved)
        : state.Nest is { Status: "hatched" } nest && nest.Id == id ? Copy(nest.Child) : null;

    private static PokemonIndividual Copy(PokemonIndividual individual) =>
        JsonSerializer.Deserialize<PokemonIndividual>(JsonSerializer.Serialize(individual))!;

    public bool RefreshReady(DateTimeOffset now)
    {
        if (state.Nest?.Status != "running" || now.ToUniversalTime() < state.Nest.ReadyAtUtc) return false;
        state.Nest.Status = "ready";
        return true;
    }

    private (PokemonIndividual[] Parents, NurseryRecipe Recipe) Pair(IReadOnlyList<Guid> ids)
    {
        if (ids is null || ids.Count != 2 || ids.Contains(Guid.Empty) || ids[0] == ids[1])
            throw new InvalidOperationException("Выберите двух разных родителей.");
        var parents = ids.Select(id => state.Individuals.SingleOrDefault(x => x.Id == id)
            ?? throw new InvalidOperationException("Родитель не найден.")).ToArray();
        var recipe = recipes.Find(parents[0], parents[1])
            ?? throw new InvalidOperationException("Для этой пары нет рецепта.");
        return (parents, recipe);
    }

    private string[] Inheritance(PokemonIndividual[] parents, int speciesId, bool isShiny)
    {
        var allowed = traits.Allowed(speciesId, isShiny, catalog).ToHashSet();
        var inherited = parents.SelectMany(p => new[] { p.FirstTraitId, p.SecondTraitId })
            .Where(id => id is not null && allowed.Contains(id)).Select(id => id!).Distinct().Order().ToArray();
        var pool = inherited.Length > 0 ? inherited : allowed.Order().ToArray();
        if (pool.Length == 0) throw new InvalidDataException("Нет допустимой особенности для потомка.");
        return pool;
    }

    private int ChainLength(int speciesId, HashSet<int> path)
    {
        if (!path.Add(speciesId)) throw new InvalidDataException("Цикл эволюции в каталоге.");
        var length = 1 + (edges.TryGetValue(speciesId, out var next)
            ? next.Select(id => ChainLength(id, path)).Max() : 0);
        path.Remove(speciesId);
        if (length is < 1 or > 3) throw new InvalidDataException("Неверная длина эволюции.");
        return length;
    }

    private static Guid Fingerprint(IReadOnlyList<Guid> parents, int? selected)
    {
        if (parents is null || parents.Count != 2) throw new InvalidOperationException("Выберите двух родителей.");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join(":", parents.Order().Select(x => x.ToString("N"))) + ":" + selected));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static Random Draw(Guid id) =>
        new(BitConverter.ToInt32(SHA256.HashData(id.ToByteArray()), 0));
}
