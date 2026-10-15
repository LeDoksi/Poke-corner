using System.Text.Json;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.Core.Domain;

public sealed class AchievementService
{
    private readonly CatalogPackage catalog;
    private readonly JsonElement rewards;
    private readonly LevelProgression levels;
    private readonly int[] reachable;
    private readonly int[] legends;

    public IReadOnlyList<int> ReachableSpeciesIds => reachable;
    public IReadOnlyList<int> ReachableLegendaryIds => legends;

    public AchievementService(CatalogPackage catalog)
    {
        this.catalog = catalog;
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.DataRoot, "final", "achievement-rewards.json")));
        rewards = document.RootElement.Clone();
        levels = new LevelProgression(catalog.DataRoot);
        using var rules = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.DataRoot, "final", "catalog-rules.json")));
        var species = catalog.Species.ToDictionary(s => s.Id);
        var found = catalog.Species.Where(s => catalog.CanIssueDirectly(s.Id, false)).Select(s => s.Id).ToHashSet();
        var edges = rules.RootElement.GetProperty("evolutionEdges").EnumerateArray()
            .Where(e => e.GetProperty("normalAvailable").GetBoolean())
            .Select(e => (From: e.GetProperty("from").GetInt32(), To: e.GetProperty("to").GetInt32())).ToArray();
        bool changed;
        do
        {
            changed = false;
            foreach (var edge in edges)
                if (found.Contains(edge.From) && species.TryGetValue(edge.To, out var target)
                    && catalog.CanObtain(edge.To, false) && species[edge.From].FamilyRootId == target.FamilyRootId)
                    changed |= found.Add(edge.To);
        } while (changed);
        reachable = found.Order().ToArray();
        legends = reachable.Where(id => species[id].Rarity == "Legendary").ToArray();
        var tiers = rewards.GetProperty("chainTiers").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        var goals = rewards.GetProperty("chainGoals").EnumerateArray().ToArray();
        var specials = rewards.GetProperty("specials").EnumerateObject().ToArray();
        if (tiers.Length != 4 || goals.Length != 40 || specials.Length != 16 ||
            goals.Sum(g => tiers[int.Parse(g.GetProperty("awardId").GetString()![^1..]) - 1]) + specials.Sum(s => s.Value.GetInt32()) != 865)
            throw new InvalidDataException("Achievement reward catalog is inconsistent.");
    }

    // Call only on the proposed save snapshot, before the confirmed commit publishes it.
    public void Evaluate(SaveData snapshot)
    {
        if (snapshot.SchemaVersion != 2) return;
        AchievementMetrics.Refresh(snapshot);
        foreach (var goal in rewards.GetProperty("chainGoals").EnumerateArray())
        {
            var id = goal.GetProperty("awardId").GetString()!;
            if (snapshot.AwardLedger.ContainsKey(id)) continue;
            var condition = goal.GetProperty("condition").GetString()!;
            var minimum = goal.GetProperty("count").GetInt32();
            var value = GoalValue(snapshot, goal, condition);
            if (value is null || value < (condition == "all-normal-species-in-award-revision" ? reachable.Length : minimum)) continue;
            var tier = int.Parse(id[^1..]) - 1;
            Award(snapshot, id, rewards.GetProperty("chainTiers")[tier].GetInt32(),
                condition == "all-normal-species-in-award-revision" ? reachable : []);
        }
        foreach (var special in rewards.GetProperty("specials").EnumerateObject())
        {
            var id = special.Name;
            if (snapshot.AwardLedger.ContainsKey(id) || !SpecialEarned(snapshot, id)) continue;
            Award(snapshot, id, special.Value.GetInt32(), id == "trophy-special-all-legends" ? legends : []);
        }
    }

    private long? GoalValue(SaveData state, JsonElement goal, string condition) => condition switch
    {
        "discovered-species" => state.DiscoveredSpeciesIds.Count,
        "all-normal-species-in-award-revision" => reachable.Length > 0 && reachable.All(state.DiscoveredSpeciesIds.Contains) ? reachable.Length : 0,
        "owned-individuals" => state.Individuals.Count,
        "successful-catches" or "claimed-expeditions" or "visited-regions" or "hard-expedition-in-each-region"
            or "first-expedition-xp" or "first-feeding" or "first-care-evolution-ready"
            or "distinct-cared-individuals" or "distinct-hatched-root-species" => AchievementMetrics.Get(state, condition),
        "individuals-at-required-level" => state.Individuals.Count(i => levels.LevelForXp(i.TotalXp) >= goal.GetProperty("minimumLevel").GetInt32()),
        "distinct-species-transitions" => state.EvolutionTransitions.Count,
        "distinct-shiny-species" => state.ShinySpeciesIds.Count,
        _ => throw new InvalidDataException("Unknown achievement condition: " + condition)
    };

    private bool SpecialEarned(SaveData state, string id) => id switch
    {
        "trophy-special-nick" => state.Individuals.Any(i => i.Nickname is not null),
        "trophy-special-favorite" => state.Individuals.Any(i => i.IsFavorite),
        "trophy-special-all-berries" => SaveMigration.BerryIds.All(b => state.Metrics.GetValueOrDefault("berry-obtained:" + b) == 1),
        "trophy-special-all-legends" => legends.Length > 0 && legends.All(state.DiscoveredSpeciesIds.Contains),
        "trophy-special-second-trait" => state.Individuals.Any(i => i.SecondTraitId is not null &&
            levels.LevelForXp(i.TotalXp) >= rewards.GetProperty("specialThresholds").GetProperty(id).GetProperty("level").GetInt32()),
        "trophy-special-rehome" => state.RehomingResults.Count > 0,
        "trophy-special-xp-potion" => state.CompletedOperations.Keys.Any(k => k.StartsWith("potion:", StringComparison.Ordinal)),
        "trophy-special-type-team" or "trophy-special-bonus-find" or "trophy-special-nursery-expedition"
            or "trophy-special-family-expedition" or "trophy-special-evolved-expedition"
            or "trophy-special-berry-evolution" or "trophy-special-branch-choice"
            or "trophy-special-offspring-choice" or "trophy-special-retrain"
            => state.Metrics.GetValueOrDefault("special:" + id) == 1,
        _ => throw new InvalidDataException("Unknown special achievement: " + id)
    };

    private void Award(SaveData state, string id, int coins, IReadOnlyList<int> revisionSpecies)
    {
        int total;
        try { total = checked(state.ShopCoins + coins); }
        catch (OverflowException ex) { throw new InvalidDataException("Achievement reward overflows the coin balance.", ex); }
        state.AwardLedger.Add(id, new AwardEntry
        {
            AwardedAtUtc = DateTimeOffset.UtcNow, CoinsGranted = coins, CatalogRevision = catalog.CatalogRevision,
            AvailableSpeciesIds = revisionSpecies.ToList()
        });
        state.ShopCoins = total;
        var chain = id.Length > 2 && id[^2] == '-' && id[^1] is >= '1' and <= '4' ? id[..^1] : null;
        var occupied = chain is null ? -1 : state.TrophyShelfIds.FindIndex(existing =>
            existing is not null && existing.Length == id.Length && existing.StartsWith(chain, StringComparison.Ordinal));
        if (occupied >= 0) state.TrophyShelfIds[occupied] = id;
        else
        {
            var vacant = state.TrophyShelfIds.FindIndex(existing => existing is null);
            if (vacant >= 0) state.TrophyShelfIds[vacant] = id;
            else if (state.TrophyShelfIds.Count < 3) state.TrophyShelfIds.Add(id);
        }
    }
}
