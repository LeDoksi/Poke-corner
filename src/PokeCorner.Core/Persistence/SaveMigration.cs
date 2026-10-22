using System.Security.Cryptography;
using System.Text.Json;
using PokeCorner.Core.Domain;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.Core.Persistence;

public sealed class LegacyLookup
{
    public IReadOnlyList<SpeciesEvolutionChain> Chains { get; }
    private readonly byte[] _originalBytes;
    public byte[] OriginalBytes => _originalBytes.ToArray();
    public string Sha256 { get; }
    public const string PreservedLookupFileName = "legacy-v1-lookup.json";

    public LegacyLookup(IEnumerable<SpeciesEvolutionChain> chains, byte[] originalBytes)
    {
        Chains = chains.ToArray();
        _originalBytes = originalBytes.ToArray();
        Sha256 = Convert.ToHexString(SHA256.HashData(_originalBytes)).ToLowerInvariant();
        if (Chains.Count == 0) throw new InvalidDataException("Отсутствует lookup v1.");
    }

    public static LegacyLookup Load(string dataRoot, string? existingCachePath = null, string? preservedLookupPath = null)
    {
        if (preservedLookupPath is not null && File.Exists(preservedLookupPath))
            return Read(preservedLookupPath);
        if (existingCachePath is not null && File.Exists(existingCachePath))
        {
            try { return Read(existingCachePath); }
            catch (JsonException) { }
        }
        var directory = Path.Combine(dataRoot, "final", "legacy-v1");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")));
        var file = manifest.RootElement.GetProperty("files").EnumerateArray()
            .Single(f => f.GetProperty("path").GetString() == "OfflineStarterFallback.json");
        var path = Path.Combine(directory, "OfflineStarterFallback.json");
        var bytes = File.ReadAllBytes(path);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (bytes.Length != file.GetProperty("bytes").GetInt64() || hash != file.GetProperty("sha256").GetString())
            throw new InvalidDataException("SHA lookup v1 не совпадает.");
        return Read(path);
    }

    private static LegacyLookup Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var chains = JsonSerializer.Deserialize<List<SpeciesEvolutionChain>>(bytes)
            ?? throw new InvalidDataException("Пустой lookup v1.");
        if (chains.Count == 0 || chains.Any(c => c is null || c.StageSpeciesIds is null || c.StageSpeciesIds.Count != c.ChainLength ||
            c.StageSpeciesIds.Count == 0 || c.StageSpeciesIds.Any(id => id <= 0)))
            throw new InvalidDataException("Неверный lookup v1.");
        return new(chains.Concat(LegacyOfflineChains.All), bytes);
    }
}

public static class SaveMigration
{
    public const string MigrationId = "global-update-v1";
    public static readonly string[] BerryIds = ["oran", "cheri", "pecha", "rawst", "aspear", "chesto"];
    public static readonly string[] TraitIds = ["gatherer", "treasure-hunter", "tracker", "finder", "mentor", "collector", "relic-finder", "terrain-expert", "fast-grower", "rarity-expert", "caring-parent"];
    public static readonly string[] ItemIds = ["treats", "care-booster", "lucky-charm", "evolution-stone", "offspring-charm", "xp-potion"];
    private static readonly int[] LegacyPhioneToManaphy = [489, 490];

    public static SaveData Migrate(SaveData v1, LegacyLookup legacyLookup, CatalogPackage catalog)
    {
        try { JsonSaveStore.ValidateData(v1); }
        catch (JsonException ex) { throw new InvalidDataException(ex.Message, ex); }
        if (v1.SchemaVersion != 1) throw new InvalidDataException("Миграция принимает только v1.");
        var next = JsonSaveStore.Clone(v1);
        next.StarterSelectionPending = false;
        JsonSaveStore.NormalizeLegacyCare(next);
        var rulesPath = Path.Combine(catalog.DataRoot, "final", "catalog-rules.json");
        var balancePath = Path.Combine(catalog.DataRoot, "final", "balance.json");
        using var rules = JsonDocument.Parse(File.ReadAllBytes(rulesPath));
        using var balance = JsonDocument.Parse(File.ReadAllBytes(balancePath));
        next.SchemaVersion = 2;
        next.CatalogRevision = catalog.CatalogRevision;
        next.BalanceRevision = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(balancePath))).ToLowerInvariant();
        next.MigrationId = MigrationId;
        next.PendingEggs.Clear();
        // Keep history-only discoveries, including supported IDs with currently unavailable artwork.
        // Exact ordinary metadata was already seeded once by the OLD-file loader, if absent.
        next.EvolutionTransitions = [];
        next.AwardLedger = [];
        next.TrophyShelfIds = [];
        next.Tutorial = new();
        next.Gift = new();
        next.Expedition = null;
        next.Nest = null;
        next.HatchedNurseryResults = [];
        next.CompletedOperations = [];
        next.Berries = BerryIds.ToDictionary(id => id, _ => 0);
        next.Items = ItemIds.ToDictionary(id => id, _ => 0);
        next.Items["treats"] = next.FeedingTreats;
        next.BerryRemainders = BerryIds.ToDictionary(id => id, _ => new RationalRemainder());
        next.CoinRemainder = new RationalRemainder();
        foreach (var individual in next.Individuals)
        {
            individual.TotalXp = 0;
            individual.XpRemainder = new RationalRemainder();
            individual.FirstTraitId = null;
            individual.SecondTraitId = null;
            individual.LegacyOrigin = null;
            individual.AcquisitionSource = "unknown";
            individual.ParentIds = [];
            InitializeIndividual(individual, legacyLookup, catalog, rules.RootElement, balance.RootElement);
            // A legacy Manaphy was a Phione first, so the player has seen both.
            var seen = individual.LegacyOrigin?.SpeciesId == LegacyPhioneToManaphy[0] && individual.CurrentSpeciesId == LegacyPhioneToManaphy[1]
                ? [LegacyPhioneToManaphy[0], .. individual.ReachedSpeciesIds] : individual.ReachedSpeciesIds;
            next.DiscoveredSpeciesIds.UnionWith(seen);
            if (individual.IsShiny) next.ShinySpeciesIds.UnionWith(seen);
            if (!individual.IsShiny || v1.UsesLegacyNormalDiscoveryInference)
                next.NormalSpeciesIds.UnionWith(seen);
            for (var i = 1; i < individual.ReachedSpeciesIds.Count; i++)
                next.EvolutionTransitions.Add($"{individual.ReachedSpeciesIds[i - 1]}->{individual.ReachedSpeciesIds[i]}");
        }
        next.Metrics = new()
        {
            ["successful-catches"] = null, ["claimed-expeditions"] = null, ["visited-regions"] = null,
            ["first-expedition-xp"] = null, ["first-feeding"] = null, ["distinct-cared-individuals"] = null,
            ["distinct-hatched-root-species"] = null, ["items-used"] = null,
            ["first-care-evolution-ready"] = next.Individuals.Any(i => EvolutionCalculator.StageForPoints(i.AccumulatedCarePoints, i.ChainLength) > i.CurrentStage) ? 1 : null
        };
        new AchievementService(catalog).Evaluate(next);
        var companion = next.Individuals.SingleOrDefault(p => p.Id == next.ActiveIndividualId);
        if (companion is not null) ReserveGift(next, companion.CurrentSpeciesId, catalog, rules.RootElement, balance.RootElement);
        JsonSaveStore.ValidateData(next);
        return next;
    }

    public static void InitializeIndividual(PokemonIndividual individual, LegacyLookup lookup, CatalogPackage catalog)
    {
        using var rules = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.DataRoot, "final", "catalog-rules.json")));
        using var balance = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.DataRoot, "final", "balance.json")));
        InitializeIndividual(individual, lookup, catalog, rules.RootElement, balance.RootElement);
    }

    private static void InitializeIndividual(PokemonIndividual individual, LegacyLookup lookup, CatalogPackage catalog, JsonElement rules, JsonElement balance)
    {
        var original = individual.LegacyOrigin ?? new()
        {
            SpeciesId = individual.SpeciesId, ChainId = individual.EvolutionChainId, ChainLength = individual.ChainLength,
            CurrentStage = individual.CurrentStage, LookupSha256 = lookup.Sha256
        };
        if (individual.AcquisitionSource == "unknown") individual.LegacyOrigin ??= original;
        var key = (original.SpeciesId, original.ChainLength, original.ChainId);
        var chainId = LegacyOfflineChains.Migration.TryGetValue(key, out var remapped) ? remapped : original.ChainId;
        var matches = lookup.Chains.Where(c => c.ChainId == chainId && c.StarterSpeciesId == original.SpeciesId &&
            c.ChainLength == original.ChainLength).ToArray();
        if (matches.Length != 1 || matches[0].StageSpeciesIds.Count != original.ChainLength)
            throw new InvalidDataException($"Неоднозначная или отсутствующая цепочка v1: {original.ChainId}, экземпляр {individual.Id}.");
        var path = matches[0].StageSpeciesIds.Take((int)individual.CurrentStage + 1).ToList();
        var edges = rules.GetProperty("evolutionEdges").EnumerateArray()
            .Select(e => (e.GetProperty("from").GetInt32(), e.GetProperty("to").GetInt32())).ToHashSet();
        if (path.Count != (int)individual.CurrentStage + 1)
            throw new InvalidDataException($"Неразрешимый путь эволюции v1: {individual.Id}.");
        // PokeAPI's chain 250 lists Phione before Manaphy with no evolution details, so v1 "evolved"
        // one into the other. It is no evolution: keep the Manaphy the player had (decision 2026-10-22).
        if (path.SequenceEqual(LegacyPhioneToManaphy)) path = [LegacyPhioneToManaphy[1]];
        if (path.Zip(path.Skip(1)).Any(e => !edges.Contains(e)))
            throw new InvalidDataException($"Неразрешимый путь эволюции v1: {individual.Id}.");
        var actual = catalog.Species.SingleOrDefault(s => s.Id == path[^1])
            ?? throw new InvalidDataException($"Вид v1 отсутствует в каталоге: {path[^1]}.");
        individual.CurrentSpeciesId = actual.Id;
        individual.FamilyRootId = actual.FamilyRootId;
        individual.ReachedSpeciesIds = path;
        LegacyOfflineChains.TryMigrate(individual);
        individual.FirstTraitId ??= ChooseTrait(actual.Id, rules, balance);
    }

    private static string ChooseTrait(int speciesId, JsonElement rules, JsonElement balance)
    {
        var traits = TraitRules.Allowed(speciesId, rules, balance);
        return traits[Random.Shared.Next(traits.Length)];
    }

    private static void ReserveGift(SaveData next, int excluded, CatalogPackage catalog, JsonElement rules, JsonElement balance)
    {
        var candidateIds = rules.GetProperty("giftRecipeCandidates").EnumerateArray().Select(p => p.GetString()!).ToHashSet();
        using var recipes = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.DataRoot, "nursery-recipes-draft.json")));
        var candidates = recipes.RootElement.GetProperty("recipes").EnumerateArray()
            .Where(r => r.GetProperty("active").GetBoolean() && candidateIds.Contains(r.GetProperty("id").GetString()!))
            .Select(r => new { Id = r.GetProperty("id").GetString()!, Hours = r.GetProperty("hours").GetInt32(), Parents = r.GetProperty("parentIds").EnumerateArray().Select(p => p.GetInt32()).ToArray() })
            .Where(r => r.Parents.Length == 2 && r.Parents.Distinct().Count() == 2 && !r.Parents.Contains(excluded) &&
                r.Parents.All(id => catalog.CanIssueDirectly(id, false) && catalog.Species.Single(s => s.Id == id).Rarity is "Common" or "Uncommon"))
            .OrderByDescending(r => r.Parents.Count(id => !next.DiscoveredSpeciesIds.Contains(id)))
            .ThenBy(r => r.Hours).ThenBy(r => r.Id, StringComparer.Ordinal).ToArray();
        if (candidates.Length == 0) throw new InvalidDataException("Нет допустимой совместимой подарочной пары.");
        var selected = candidates[0];
        next.Gift = new() { RecipeId = selected.Id, ExcludedSpeciesId = excluded };
        var canonicalRoots = new EncounterService(catalog);
        foreach (var id in selected.Parents)
        {
            var s = catalog.Species.Single(s => s.Id == id);
            next.Gift.Gifts.Add(new()
            {
                Individual = new()
                {
                    SpeciesId = id, CurrentSpeciesId = id, FamilyRootId = s.FamilyRootId,
                    EvolutionChainId = s.EvolutionChainId, ChainLength = canonicalRoots.DirectRootChainLength(id), Nature = (Nature)Random.Shared.Next(Enum.GetValues<Nature>().Length),
                    IsShiny = catalog.CanObtain(id, true) && Random.Shared.NextDouble() < balance.GetProperty("shinyBase").GetDouble(), CaughtAtUtc = DateTimeOffset.UtcNow, FirstTraitId = ChooseTrait(id, rules, balance),
                    AcquisitionSource = "update-gift", ReachedSpeciesIds = [id]
                }
            });
        }
    }

    public static void EnsureGift(SaveData next, int? excludedSpeciesId, CatalogPackage catalog)
    {
        if (next.Gift.Gifts.Count == 2) return;
        if (next.Gift.Gifts.Count != 0) throw new InvalidDataException("Неполный журнал подарка.");
        using var rules = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.DataRoot, "final", "catalog-rules.json")));
        using var balance = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.DataRoot, "final", "balance.json")));
        ReserveGift(next, excludedSpeciesId ?? 0, catalog, rules.RootElement, balance.RootElement);
        next.Gift.ExcludedSpeciesId = excludedSpeciesId;
    }

    public static async Task<SaveData?> LoadAsync(string v1Path, string v2Path, LegacyLookup lookup, CatalogPackage catalog, JsonSaveStore? store = null)
    {
        store ??= new JsonSaveStore(v2Path);
        // Load never falls back after an existing v2 failed its read/validation.
        var v2 = store.Load();
        if (v2 is not null)
        {
            ValidateCatalog(v2, catalog, v2Path);
            return v2;
        }
        var legacyRoot = Path.Combine(Path.GetDirectoryName(v1Path)!, "legacy-v1");
        if (Directory.Exists(legacyRoot) && Directory.EnumerateFiles(legacyRoot, "manifest.json", SearchOption.AllDirectories).Any())
            throw new SaveLoadException(v2Path, "найдена предыдущая миграция; автоматический импорт старого v1 запрещён");
        var v1 = new JsonSaveStore(v1Path).Load(out var bytes);
        if (v1 is null && Directory.Exists(legacyRoot))
            throw new SaveLoadException(v1Path, "активный v1 отсутствует, но найдены следы незавершённой миграции; новая игра запрещена");
        store.ObserveLegacyBeforeFirstWrite(v1Path, bytes);
        if (v1 is null) return null;
        SaveData migrated;
        try { migrated = Migrate(v1, lookup, catalog); }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or OverflowException)
        { throw new SaveLoadException(v1Path, ex.Message, ex); }
        var hash = Convert.ToHexString(SHA256.HashData(bytes!)).ToLowerInvariant();
        var backup = Path.Combine(legacyRoot, hash);
        if (Directory.Exists(backup)) backup += "-retry-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(backup);
        Preserve(Path.Combine(backup, "state.json"), bytes!);
        Preserve(Path.Combine(backup, LegacyLookup.PreservedLookupFileName), lookup.OriginalBytes);
        Preserve(Path.Combine(backup, "manifest.json"), System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        { SchemaVersion = 1, StateSha256 = hash, LookupSha256 = lookup.Sha256, MigrationId })));
        await new BackgroundSaveWriter(store).CommitAsync(migrated);
        return migrated;
    }

    public static void ValidateCatalog(SaveData data, CatalogPackage catalog, string path)
    {
        if (data.SchemaVersion != 2) throw new SaveLoadException(path, "ожидалась схема v2");
        var species = catalog.Species.ToDictionary(s => s.Id);
        foreach (var individual in data.Individuals.Concat(data.Gift.Gifts.Select(g => g.Individual))
            .Concat(data.Encounters.Where(e => e.Individual is not null).Select(e => e.Individual!))
            .Concat(data.Nest is null ? [] : new[] { data.Nest.Child })
            .Concat(data.HatchedNurseryResults.Values))
            if (!species.TryGetValue(individual.CurrentSpeciesId, out var current) ||
                current.FamilyRootId != individual.FamilyRootId || individual.ReachedSpeciesIds.Any(id => !species.ContainsKey(id)))
                throw new SaveLoadException(path, $"неверный вид/семья v2: {individual.Id}");
        if (data.DiscoveredSpeciesIds.Concat(data.ShinySpeciesIds).Concat(data.NormalSpeciesIds).Any(id => !species.ContainsKey(id)))
            throw new SaveLoadException(path, "неверный открытый вид v2");
        if (data.Encounters.Any(e => e.KnownSpeciesId is { } id && !catalog.CanIssueDirectly(id, false)))
            throw new SaveLoadException(path, "недоступный вид в сохранённой встрече");
    }

    public static void RestoreHistory(JsonSaveStore store, SaveHistoryEntry entry, CatalogPackage catalog, string dataRoot)
    {
        var bytes = store.History.ReadValidated(entry);
        var data = JsonSaveStore.Deserialize(bytes);
        if (data.SchemaVersion == 1)
        {
            if (!entry.IsLegacy) throw new SaveLoadException(entry.Path, "v1 требует сохранённый lookup миграции");
            var lookup = LegacyLookup.Load(dataRoot, preservedLookupPath: Path.Combine(Path.GetDirectoryName(entry.Path)!, LegacyLookup.PreservedLookupFileName));
            data = Migrate(data, lookup, catalog);
            bytes = System.Text.Encoding.UTF8.GetBytes(JsonSaveStore.Serialize(data));
        }
        ValidateCatalog(data, catalog, entry.Path);
        store.RestoreBytes(bytes, catalog);
    }

    private static void Preserve(string path, byte[] bytes)
    {
        try
        {
            var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            JsonSaveStore.WriteDurable(temp, bytes);
            File.Move(temp, path);
        }
        catch (IOException) when (File.Exists(path))
        {
            if (!File.ReadAllBytes(path).SequenceEqual(bytes)) throw new InvalidDataException($"Неизменяемый backup не совпадает: {path}");
        }
    }
}
