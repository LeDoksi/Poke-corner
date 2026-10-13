using System.Security.Cryptography;
using System.Text.Json;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.Core.Domain;

public sealed record ExpeditionRequest(string LocationId, string Difficulty, int DurationMinutes,
    IReadOnlyList<Guid> ParticipantIds, Guid OperationId, DateTimeOffset NowUtc);
public sealed record ExpeditionPreview(string PrimaryBerryId, int PrimaryBerries, int Coins, int XpPerParticipant,
    double EncounterChance, double ExtraChancePerCheck, DateTimeOffset ReadyAtUtc, IReadOnlyDictionary<Guid, int> XpByParticipant);
public sealed record ExpeditionAction(bool Success, string Message, bool Repeated = false, EncounterOpenResult? Encounter = null);

// Works only on a proposed SaveData. AppState.CommitAsync is the publication barrier for every mutation.
public sealed class ExpeditionService
{
    private sealed record Route(string Id, string Primary, string[] Additional, string[] BonusTypes, HashSet<int> SpeciesIds);
    private readonly CatalogPackage catalog;
    private readonly SaveData state;
    private readonly LevelProgression levels;
    private readonly TraitRules traits;
    private readonly JsonElement balance;
    private readonly Dictionary<string, Route> routes;
    private static readonly string[] Rarities = ["Common", "Uncommon", "Rare", "Legendary"];

    public ExpeditionService(CatalogPackage catalog, SaveData state)
    {
        this.catalog = catalog;
        this.state = state;
        levels = new LevelProgression(catalog.DataRoot);
        traits = new TraitRules(catalog.DataRoot);
        using var source = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.DataRoot, "final", "balance.json")));
        balance = source.RootElement.Clone();
        using var rules = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.DataRoot, "final", "catalog-rules.json")));
        routes = rules.RootElement.GetProperty("routes").EnumerateArray().Select(row => new Route(
            row.GetProperty("id").GetString()!, row.GetProperty("primary").GetString()!,
            row.GetProperty("additional").EnumerateArray().Select(x => x.GetString()!).ToArray(),
            row.GetProperty("bonusTypes").EnumerateArray().Select(x => x.GetString()!).ToArray(),
            row.GetProperty("speciesIds").EnumerateArray().Select(x => x.GetInt32()).ToHashSet()))
            .ToDictionary(x => x.Id);
        if (routes.Count != 8) throw new InvalidDataException("Invalid expedition routes.");
    }

    public static IReadOnlySet<Guid> BusyIndividualIds(SaveData state) =>
        (state.Expedition?.Status is "running" or "ready" ? state.Expedition.ParticipantIds : [])
        .Concat(state.Nest?.Status is "running" or "ready" ? state.Nest.ParticipantIds : [])
        .ToHashSet();

    public ExpeditionPreview Preview(ExpeditionRequest request)
    {
        var (route, members, difficulty) = ValidateRequest(request);
        var hours = request.DurationMinutes / 60m;
        var average = members.Average(x => levels.LevelForXp(x.TotalXp));
        var recommended = balance.GetProperty("recommendedLevels")[difficulty].GetInt32();
        var preparation = 1m - Math.Min(balance.GetProperty("maxUnderlevelPenalty").GetDecimal(),
            balance.GetProperty("underlevelPenaltyPerLevel").GetDecimal() * Math.Max(0m, recommended - (decimal)average));
        var matching = members.Count(p => catalog.Species.First(s => s.Id == p.CurrentSpeciesId).Types.Any(route.BonusTypes.Contains));
        var common = balance.GetProperty("levelBonusPerLevel").GetDecimal() * ((decimal)average - 1m)
            + balance.GetProperty("matchingTypeBonusPerMember").GetDecimal() * matching;
        var composition = balance.GetProperty("resourceCompositionByMembers")[members.Length - 1].GetDecimal();
        decimal ResourceRate(string trait, string table) =>
            balance.GetProperty(table)[difficulty].GetDecimal()
            * (1m + Math.Min(balance.GetProperty("resourceBonusCap").GetDecimal(), common + Trait(trait, members)))
            * preparation * composition;
        var primaryRate = Math.Max(balance.GetProperty("minimumPrimaryBerriesPerHour").GetDecimal(),
            ResourceRate("gatherer", "primaryBerriesPerHour"));
        var coinRate = ResourceRate("treasure-hunter", "coinsPerHour");
        var xpRate = balance.GetProperty("xpPerHour").GetDecimal() * (1m + Trait("mentor", members));
        var primary = Copy(state.BerryRemainders[route.Primary]).Settle(Scaled(primaryRate, request.DurationMinutes), ExpeditionRewards.Denominator);
        var coins = Copy(state.CoinRemainder).Settle(Scaled(coinRate, request.DurationMinutes), ExpeditionRewards.Denominator);
        var xpByParticipant = members.ToDictionary(p => p.Id, p => checked((int)Copy(p.XpRemainder)
            .Settle(Scaled(xpRate, request.DurationMinutes), ExpeditionRewards.Denominator)));
        var eventPreparation = 1m - Math.Min(balance.GetProperty("maxUnderlevelPenalty").GetDecimal(),
            balance.GetProperty("eventUnderlevelPenaltyPerLevel").GetDecimal() * Math.Max(0m, recommended - (decimal)average));
        var hourly = Math.Min(.95, (double)(balance.GetProperty("hourlyEncounterChance")[difficulty].GetDecimal() * (1m + common) * eventPreparation));
        var chance = members.Length == balance.GetProperty("eventTeamSize").GetInt32()
            ? Math.Min(.95, (1 - Math.Pow(1 - hourly, (double)hours)) * (1 + (double)Trait("tracker", members))) : 0;
        var extra = members.Length == 3 ? Math.Min(.95, (double)(balance.GetProperty("extraChancePerHalfHour")[difficulty].GetDecimal()
            * (1m + common + Trait("finder", members)) * eventPreparation)) : 0;
        var timeReduction = Math.Min(balance.GetProperty("maxExpeditionTimeReduction").GetDecimal(), Trait("terrain-expert", members));
        var ready = request.NowUtc.ToUniversalTime().AddTicks(checked((long)(TimeSpan.FromMinutes(request.DurationMinutes).Ticks * (1m - timeReduction))));
        return new(route.Primary, checked((int)primary), checked((int)coins), xpByParticipant[members[0].Id], chance, extra, ready, xpByParticipant);
    }

    public ExpeditionAction Start(ExpeditionRequest request)
    {
        if (state.Expedition?.Id == request.OperationId)
        {
            var prior = state.Expedition;
            if (prior.Status is "running" or "ready" && prior.LocationId == request.LocationId &&
                prior.Difficulty == request.Difficulty && prior.DurationMinutes == request.DurationMinutes &&
                prior.ParticipantIds.SequenceEqual(request.ParticipantIds)) return new(true, "Поход уже сохранён.", true);
            throw new InvalidOperationException("ID похода уже использован с другими параметрами.");
        }
        if (state.CompletedOperations.ContainsKey($"expedition:start:{request.OperationId}"))
            return new(true, "Поход уже был начат.", true);
        var preview = Preview(request);
        var (route, members, difficulty) = ValidateRequest(request);
        var random = Draw(request.OperationId, "expedition-start");
        var rewards = new ExpeditionRewards
        {
            PrimaryBerryId = route.Primary,
            PrimaryNumerator = ScaledPrimary(request, route, members, difficulty),
            CoinNumerator = ScaledCoins(request, route, members, difficulty),
            XpNumerator = ScaledXp(request, members),
            HasEncounter = random.NextDouble() < preview.EncounterChance
        };
        if (members.Length == 3)
        {
            var checks = request.DurationMinutes / balance.GetProperty("extraCheckMinutes").GetInt32();
            var loot = balance.GetProperty("extraLoot");
            var relicChance = Math.Min(.95, (double)(Trait("relic-finder", members)
                * (1m + CommonBonus(route, members)) * EventPreparation(difficulty, members)));
            for (var n = 0; n < checks; n++)
            {
                if (random.NextDouble() < preview.ExtraChancePerCheck)
                {
                    if (random.NextDouble() < loot.GetProperty("berry").GetProperty("probability").GetDouble())
                    {
                        var berry = route.Additional[random.Next(route.Additional.Length)];
                        rewards.ExtraBerries[berry] = checked(rewards.ExtraBerries.GetValueOrDefault(berry) + 1);
                    }
                    else rewards.ExtraCoins = checked(rewards.ExtraCoins + loot.GetProperty("coins").GetProperty("quantityByDifficulty")[difficulty].GetInt32());
                }
                if (random.NextDouble() < relicChance)
                {
                    var weights = balance.GetProperty("relicLootWeights");
                    var stone = weights.GetProperty("evolutionStone").GetInt32();
                    var charm = weights.GetProperty("offspringCharm").GetInt32();
                    var pick = random.Next(stone + charm + weights.GetProperty("luckyCharm").GetInt32());
                    var key = pick < stone ? "evolution-stone" : pick < stone + charm ? "offspring-charm" : "lucky-charm";
                    rewards.ExtraItems[key] = checked(rewards.ExtraItems.GetValueOrDefault(key) + 1);
                }
            }
        }
        state.Expedition = new ExpeditionState
        {
            Id = request.OperationId, Status = "running", LocationId = route.Id, Difficulty = request.Difficulty,
            ParticipantIds = request.ParticipantIds.ToList(), StartedAtUtc = request.NowUtc.ToUniversalTime(),
            ReadyAtUtc = preview.ReadyAtUtc, DurationMinutes = request.DurationMinutes,
            TraitTimeReduction = 1m - (decimal)(preview.ReadyAtUtc - request.NowUtc.ToUniversalTime()).Ticks / TimeSpan.FromMinutes(request.DurationMinutes).Ticks,
            CollectorBoost = TraitRules.Stack(members.SelectMany(p =>
                new[] { p.FirstTraitId, p.SecondTraitId }.Where(x => x == "collector")
                    .Select(_ => traits.Value("collector", levels.TraitTierForXp(p.TotalXp)) - 1m))),
            RarityBoost = 1m + TraitRules.Stack(members.SelectMany(p =>
                new[] { p.FirstTraitId, p.SecondTraitId }.Where(x => x == "rarity-expert")
                    .Select(_ => traits.Value("rarity-expert", levels.TraitTierForXp(p.TotalXp)) - 1m))),
            KnownSpeciesIdsAtStart = state.DiscoveredSpeciesIds.Concat(state.Individuals.SelectMany(p => p.ReachedSpeciesIds)).ToHashSet(),
            Rewards = rewards,
            Conditions = JsonSerializer.SerializeToElement(new { request.LocationId, request.Difficulty, request.DurationMinutes }),
            Outcomes = JsonSerializer.SerializeToElement(new { rewards.HasEncounter })
        };
        state.CompletedOperations[$"expedition:start:{request.OperationId}"] = request.OperationId;
        return new(true, "Поход начался.");
    }

    public ExpeditionAction Cancel(Guid id, DateTimeOffset now)
    {
        if (state.Expedition?.Id != id && state.CompletedOperations.ContainsKey($"expedition:cancel:{id}"))
            return new(true, "Поход уже отменён.", true);
        var trip = Active(id);
        if (trip.Status == "ready" || now.ToUniversalTime() >= trip.ReadyAtUtc)
            return new(false, "Поход уже завершён. Заберите находки.");
        state.Expedition = null;
        state.CompletedOperations[$"expedition:cancel:{id}"] = id;
        return new(true, "Поход отменён без наград.");
    }

    public ExpeditionAction Claim(Guid id, Guid operationId, DateTimeOffset? now = null)
    {
        if (operationId == Guid.Empty) throw new ArgumentException("Operation ID is required.", nameof(operationId));
        if (state.Expedition?.Id != id && state.CompletedOperations.TryGetValue($"expedition:claim:{id}", out var completed))
            return completed == operationId ? new(true, "Награды уже сохранены.", true, ReplayedEncounter(id))
                : new(false, "Награды этого похода уже получены.");
        var trip = state.Expedition ?? throw new InvalidOperationException("Поход не найден.");
        if (trip.Id != id) throw new InvalidOperationException("Поход не найден.");
        if (trip.Status == "claimed")
            return trip.ClaimOperationId == operationId
                ? new(true, "Награды уже сохранены.", true, ReplayedEncounter(id))
                : new(false, "Награды этого похода уже получены.");
        if (trip.Status != "ready" && (now ?? DateTimeOffset.UtcNow).ToUniversalTime() < trip.ReadyAtUtc)
            return new(false, "Поход ещё идёт.");
        var reward = trip.Rewards;
        state.Berries[reward.PrimaryBerryId] = checked(state.Berries[reward.PrimaryBerryId]
            + (int)state.BerryRemainders[reward.PrimaryBerryId].Settle(reward.PrimaryNumerator, ExpeditionRewards.Denominator));
        state.ShopCoins = checked(state.ShopCoins + (int)state.CoinRemainder.Settle(reward.CoinNumerator, ExpeditionRewards.Denominator) + reward.ExtraCoins);
        foreach (var (berry, count) in reward.ExtraBerries) state.Berries[berry] = checked(state.Berries[berry] + count);
        foreach (var (item, count) in reward.ExtraItems) state.Items[item] = checked(state.Items[item] + count);
        var gainedXp = false;
        foreach (var participantId in trip.ParticipantIds)
        {
            var participant = state.Individuals.Single(i => i.Id == participantId);
            if (participant.TotalXp == levels.MaxXp) continue;
            var xp = checked((int)participant.XpRemainder.Settle(reward.XpNumerator, ExpeditionRewards.Denominator));
            gainedXp |= levels.AddXp(participant, xp, Draw(id, participantId.ToString("N"))) > 0;
        }
        EncounterOpenResult? encounter = null;
        if (reward.HasEncounter)
        {
            var species = SelectSpecies(trip);
            var sealedBall = new EncounterService(catalog).Reserve(state, "expedition", id, species.Id);
            sealedBall.ReservedAtUtc = trip.ReadyAtUtc;
            encounter = new EncounterService(catalog).Open(state, sealedBall.Id);
        }
        trip.Status = "claimed";
        trip.ClaimOperationId = operationId;
        state.Metrics["claimed-expeditions"] = checked((state.Metrics.GetValueOrDefault("claimed-expeditions") ?? 0) + 1);
        state.Metrics[$"expedition:visited:{trip.LocationId}"] = 1;
        state.Metrics["visited-regions"] = routes.Keys.Count(route => state.Metrics.GetValueOrDefault($"expedition:visited:{route}") == 1);
        if (trip.Difficulty == "hard") state.Metrics[$"expedition:hard:{trip.LocationId}"] = 1;
        state.Metrics["hard-expedition-in-each-region"] = routes.Keys.Count(route => state.Metrics.GetValueOrDefault($"expedition:hard:{route}") == 1);
        if (gainedXp) state.Metrics["first-expedition-xp"] = 1;
        state.CompletedOperations[$"expedition:claim:{id}"] = operationId;
        return new(true, "Награды получены.", false, encounter);
    }

    public bool RefreshReady(DateTimeOffset now)
    {
        var trip = state.Expedition;
        if (trip?.Status != "running" || now.ToUniversalTime() < trip.ReadyAtUtc) return false;
        trip.Status = "ready";
        return true;
    }

    public bool ApplyActivityActions(long actions, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(actions);
        var trip = state.Expedition;
        if (trip?.Status != "running") return false;
        trip.ActivityActions = checked(trip.ActivityActions + actions);
        var activity = Math.Min(balance.GetProperty("activityAcceleration").GetProperty("maxReduction").GetDecimal(),
            trip.ActivityActions / balance.GetProperty("activityAcceleration").GetProperty("actionsPerStep").GetInt64()
            * balance.GetProperty("activityAcceleration").GetProperty("reductionPerStep").GetDecimal());
        var reduction = Math.Min(balance.GetProperty("maxExpeditionTimeReduction").GetDecimal(), trip.TraitTimeReduction + activity);
        var earlier = trip.StartedAtUtc.AddTicks(checked((long)(TimeSpan.FromMinutes(trip.DurationMinutes).Ticks * (1m - reduction))));
        if (earlier < trip.ReadyAtUtc) trip.ReadyAtUtc = earlier;
        RefreshReady(now);
        return true;
    }

    private (Route Route, PokemonIndividual[] Members, int Difficulty) ValidateRequest(ExpeditionRequest request)
    {
        if (state.SchemaVersion != 2 || request.OperationId == Guid.Empty) throw new InvalidOperationException("Требуется сохранение v2 и ID действия.");
        if (state.Expedition?.Status is "running" or "ready") throw new InvalidOperationException("Уже идёт поход.");
        if (!routes.TryGetValue(request.LocationId, out var route)) throw new InvalidOperationException("Местность не найдена.");
        var difficulty = Array.IndexOf(balance.GetProperty("difficultyOrder").EnumerateArray().Select(x => x.GetString()).ToArray(), request.Difficulty);
        if (difficulty < 0 || !balance.GetProperty("durationMinutes").EnumerateArray().Any(x => x.GetInt32() == request.DurationMinutes))
            throw new InvalidOperationException("Неверная сложность или длительность.");
        if (request.ParticipantIds is null || request.ParticipantIds.Count is < 1 or > 3 ||
            request.ParticipantIds.Contains(Guid.Empty) || request.ParticipantIds.Distinct().Count() != request.ParticipantIds.Count)
            throw new InvalidOperationException("Нужны 1–3 разных участника.");
        var busy = BusyIndividualIds(state);
        if (request.ParticipantIds.Any(busy.Contains)) throw new InvalidOperationException("Участник уже занят.");
        var members = request.ParticipantIds.Select(id => state.Individuals.SingleOrDefault(p => p.Id == id)
            ?? throw new InvalidOperationException("Участник не найден.")).ToArray();
        return (route, members, difficulty);
    }

    private ExpeditionState Active(Guid id) => state.Expedition is { } trip && trip.Id == id && trip.Status is "running" or "ready"
        ? trip : throw new InvalidOperationException("Активный поход не найден.");

    private decimal Trait(string id, PokemonIndividual[] members) => TraitRules.Stack(members.SelectMany(p =>
        new[] { p.FirstTraitId, p.SecondTraitId }.Where(x => x == id).Select(_ => traits.Value(id, levels.TraitTierForXp(p.TotalXp)))));

    private decimal CommonBonus(Route route, PokemonIndividual[] members) =>
        balance.GetProperty("levelBonusPerLevel").GetDecimal() * ((decimal)members.Average(x => levels.LevelForXp(x.TotalXp)) - 1m)
        + balance.GetProperty("matchingTypeBonusPerMember").GetDecimal() * members.Count(p =>
            catalog.Species.First(s => s.Id == p.CurrentSpeciesId).Types.Any(route.BonusTypes.Contains));

    private decimal EventPreparation(int difficulty, PokemonIndividual[] members) => 1m - Math.Min(
        balance.GetProperty("maxUnderlevelPenalty").GetDecimal(),
        balance.GetProperty("eventUnderlevelPenaltyPerLevel").GetDecimal()
        * Math.Max(0m, balance.GetProperty("recommendedLevels")[difficulty].GetDecimal()
            - (decimal)members.Average(x => levels.LevelForXp(x.TotalXp))));

    private decimal ResourcePreparation(int difficulty, PokemonIndividual[] members) => 1m - Math.Min(
        balance.GetProperty("maxUnderlevelPenalty").GetDecimal(),
        balance.GetProperty("underlevelPenaltyPerLevel").GetDecimal()
        * Math.Max(0m, balance.GetProperty("recommendedLevels")[difficulty].GetDecimal()
            - (decimal)members.Average(x => levels.LevelForXp(x.TotalXp))));

    private decimal ResourceRate(string trait, string table, Route route, PokemonIndividual[] members, int difficulty) =>
        balance.GetProperty(table)[difficulty].GetDecimal()
        * (1m + Math.Min(balance.GetProperty("resourceBonusCap").GetDecimal(), CommonBonus(route, members) + Trait(trait, members)))
        * ResourcePreparation(difficulty, members)
        * balance.GetProperty("resourceCompositionByMembers")[members.Length - 1].GetDecimal();

    private long ScaledPrimary(ExpeditionRequest request, Route route, PokemonIndividual[] members, int difficulty) =>
        Scaled(Math.Max(balance.GetProperty("minimumPrimaryBerriesPerHour").GetDecimal(),
            ResourceRate("gatherer", "primaryBerriesPerHour", route, members, difficulty)), request.DurationMinutes);
    private long ScaledCoins(ExpeditionRequest request, Route route, PokemonIndividual[] members, int difficulty) =>
        Scaled(ResourceRate("treasure-hunter", "coinsPerHour", route, members, difficulty), request.DurationMinutes);
    private long ScaledXp(ExpeditionRequest request, PokemonIndividual[] members) =>
        Scaled(balance.GetProperty("xpPerHour").GetDecimal() * (1m + Trait("mentor", members)), request.DurationMinutes);
    private static long Scaled(decimal hourly, int minutes) => checked((long)decimal.Round(hourly * 1_000_000_000m, 0) * minutes);
    private static RationalRemainder Copy(RationalRemainder value) => new() { Numerator = value.Numerator, Denominator = value.Denominator };
    private EncounterOpenResult? ReplayedEncounter(Guid id) => state.Encounters.FirstOrDefault(e => e.Id == id) is { Phase: "opened" } saved
        ? new EncounterOpenResult(saved.Individual!, saved.Rarity!.Value, saved.WasDuplicateSpecies, true) : null;
    private static Random Draw(Guid id, string salt) => new(BitConverter.ToInt32(SHA256.HashData(id.ToByteArray().Concat(System.Text.Encoding.UTF8.GetBytes(salt)).ToArray())));

    private CatalogSpecies SelectSpecies(ExpeditionState trip)
    {
        var route = routes[trip.LocationId];
        var difficulty = Array.IndexOf(balance.GetProperty("difficultyOrder").EnumerateArray().Select(x => x.GetString()).ToArray(), trip.Difficulty);
        var random = Draw(trip.Id, "expedition-species");
        var available = catalog.Species.Where(s => route.SpeciesIds.Contains(s.Id) && catalog.CanIssueDirectly(s.Id, false)).ToArray();
        var weights = balance.GetProperty("expeditionRarityWeights")[difficulty].EnumerateArray().Select(x => x.GetDouble()).ToArray();
        var rarityBoost = (double)trip.RarityBoost;
        weights[2] *= rarityBoost;
        weights[3] *= rarityBoost;
        var groups = Rarities.Select((name, i) => (Species: available.Where(s => s.Rarity == name).ToArray(), Weight: weights[i]))
            .Where(x => x.Species.Length > 0).ToArray();
        if (groups.Length == 0) throw new InvalidDataException("Маршрут не содержит доступных корней семей.");
        var roll = random.NextDouble() * groups.Sum(x => x.Weight);
        var group = groups.Last();
        foreach (var candidate in groups) { roll -= candidate.Weight; if (roll < 0) { group = candidate; break; } }
        var unseen = group.Species.Where(s => !trip.KnownSpeciesIdsAtStart.Contains(s.Id)).ToArray();
        var collector = trip.CollectorBoost;
        var novelWeight = 1d + (double)collector;
        var total = group.Species.Length + unseen.Length * (novelWeight - 1);
        var pick = random.NextDouble() * total;
        foreach (var species in group.Species)
        {
            pick -= unseen.Contains(species) ? novelWeight : 1;
            if (pick < 0) return species;
        }
        return group.Species[^1];
    }
}
