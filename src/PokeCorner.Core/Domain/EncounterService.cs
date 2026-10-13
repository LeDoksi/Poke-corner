using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.Core.Domain;

public sealed class EncounterService
{
    private readonly CatalogPackage _catalog;
    private readonly SpeciesEvolutionChain[] _roots;
    private readonly TraitRules _traits;
    private readonly int[] _rarityWeights;
    private readonly double _newChance;

    public IReadOnlyList<int> DirectRootIds => _roots.Select(c => c.StarterSpeciesId).ToArray();

    public EncounterService(CatalogPackage catalog)
    {
        _catalog = catalog;
        _traits = new TraitRules(catalog.DataRoot);
        using var rules = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.DataRoot, "final", "catalog-rules.json")));
        var edges = rules.RootElement.GetProperty("evolutionEdges").EnumerateArray()
            .Select(e => (from: e.GetProperty("from").GetInt32(), to: e.GetProperty("to").GetInt32()))
            .ToLookup(e => e.from, e => e.to);
        int Length(int id, HashSet<int> path)
        {
            if (!path.Add(id)) throw new InvalidDataException("Cyclic evolution path in canonical catalog.");
            var length = 1 + edges[id].Select(next => Length(next, path)).DefaultIfEmpty(0).Max();
            path.Remove(id);
            if (length is < 1 or > 3) throw new InvalidDataException("Invalid evolution length in canonical catalog.");
            return length;
        }
        _roots = catalog.Species.Where(s => catalog.CanIssueDirectly(s.Id, false))
            .Select(s => new SpeciesEvolutionChain(s.EvolutionChainId, s.Id, s.Rarity switch
            {
                "Common" => 150, "Uncommon" => 70, "Rare" => 30, "Legendary" => 1,
                _ => throw new InvalidDataException($"Unknown encounter rarity for #{s.Id}.")
            }, Length(s.Id, new HashSet<int>()), [s.Id], [s.DisplayName])).ToArray();
        if (_roots.Length == 0) throw new InvalidDataException("No directly issuable encounter roots.");
        using var balance = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.DataRoot, "final", "balance.json")));
        _rarityWeights = balance.RootElement.GetProperty("activityRarityWeights").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        _newChance = balance.RootElement.GetProperty("activityNewChance").GetDouble();
    }

    public EncounterState Reserve(SaveData state, string source, Guid operationId, int? knownSpeciesId = null)
    {
        var encounter = Reserve(state.Encounters, source, operationId, knownSpeciesId);
        if (source == "activity" && encounter.Phase == "sealed" && state.PendingCatchCount < state.Encounters.Count(e => e.Source == "activity" && e.Phase == "sealed"))
            state.PendingCatchCount = checked(state.PendingCatchCount + 1);
        return encounter;
    }

    public EncounterState Reserve(List<EncounterState> queue, string source, Guid operationId, int? knownSpeciesId = null)
    {
        if (operationId == Guid.Empty) throw new ArgumentException("Operation ID is required.", nameof(operationId));
        if (source is not ("activity" or "expedition" or "gift")) throw new ArgumentException("Unknown encounter source.", nameof(source));
        if (source == "activity" && knownSpeciesId is not null) throw new ArgumentException("Activity balls must stay sealed.", nameof(knownSpeciesId));
        if (knownSpeciesId is { } id && !_catalog.CanIssueDirectly(id, false)) throw new InvalidDataException("Encounter requires an available full-family root.");
        var existing = queue.FirstOrDefault(e => e.Id == operationId);
        if (existing is not null)
        {
            if (existing.Source != source || existing.KnownSpeciesId != knownSpeciesId) throw new InvalidOperationException("Operation ID belongs to another encounter.");
            return existing;
        }
        var encounter = new EncounterState { Id = operationId, Source = source, ReservedAtUtc = DateTimeOffset.UtcNow, KnownSpeciesId = knownSpeciesId };
        queue.Add(encounter);
        return encounter;
    }

    public EncounterState ReserveGift(SaveData state, Guid operationId, Guid giftIndividualId)
    {
        var existing = state.Encounters.FirstOrDefault(e => e.Id == operationId);
        if (existing is not null)
        {
            if (existing.Source != "gift" || existing.GiftIndividualId != giftIndividualId)
                throw new InvalidOperationException("Operation ID belongs to another gift.");
            return existing;
        }
        var gift = state.Gift.Gifts.SingleOrDefault(g => g.Individual.Id == giftIndividualId)
            ?? throw new InvalidOperationException("Reserved gift was not found.");
        if (gift.Status != "reserved" || !_catalog.CanIssueDirectly(gift.Individual.SpeciesId, gift.Individual.IsShiny))
            throw new InvalidDataException("Gift is not eligible for direct issue.");
        var encounter = Reserve(state, "gift", operationId);
        if (encounter.GiftIndividualId is not null && encounter.GiftIndividualId != giftIndividualId)
            throw new InvalidOperationException("Operation ID belongs to another gift.");
        encounter.GiftIndividualId = giftIndividualId;
        return encounter;
    }

    // Mutate only a proposed SaveData snapshot. Caller commits it before publishing the result.
    public EncounterOpenResult Open(SaveData state, Guid encounterId)
    {
        var encounter = state.Encounters.FirstOrDefault(e => e.Id == encounterId)
            ?? throw new InvalidOperationException("Encounter was not reserved.");
        if (encounter.Phase == "opened")
        {
            var saved = encounter.Individual ?? throw new InvalidDataException("Opened encounter has no result.");
            return new EncounterOpenResult(saved, encounter.Rarity!.Value, encounter.WasDuplicateSpecies, true);
        }
        if (encounter.Phase != "sealed") throw new InvalidDataException("Unknown encounter phase.");
        if (encounter.Source == "gift" && encounter.GiftIndividualId is null ||
            encounter.Source == "expedition" && encounter.KnownSpeciesId is null)
            throw new InvalidDataException("Encounter outcome is not reserved by its source.");
        var seed = BinaryPrimitives.ReadInt32LittleEndian(SHA256.HashData(encounter.Id.ToByteArray()));
        var random = new Random(seed);
        var roller = new CatchRoller(random);
        var discovered = state.DiscoveredSpeciesIds.Concat(state.Individuals.SelectMany(i => i.ReachedSpeciesIds)).ToHashSet();
        double Multiplier(int _)
        {
            return new ShopService().TryConsumeLuckyCharm(state, true) ? ShinyRules.CharmMultiplier : 1;
        }
        CatchResult rolled;
        PokemonIndividual? reservedGift = null;
        if (encounter.GiftIndividualId is { } giftId)
        {
            var gift = state.Gift.Gifts.SingleOrDefault(g => g.Individual.Id == giftId && g.Status == "reserved")
                ?? throw new InvalidDataException("Gift reservation is unavailable.");
            reservedGift = gift.Individual;
            var chain = _roots.FirstOrDefault(c => c.StarterSpeciesId == reservedGift.SpeciesId)
                ?? throw new InvalidDataException("Gift root is unavailable.");
            if (!_catalog.CanIssueDirectly(reservedGift.SpeciesId, reservedGift.IsShiny))
                throw new InvalidDataException("Gift variant is unavailable.");
            rolled = new CatchResult(chain.ChainId, reservedGift.SpeciesId, reservedGift.Nature, reservedGift.IsShiny,
                discovered.Contains(reservedGift.SpeciesId), chain.ChainLength);
            gift.Status = "opened";
        }
        else if (encounter.KnownSpeciesId is { } knownId)
        {
            var chain = _roots.FirstOrDefault(c => c.StarterSpeciesId == knownId)
                ?? throw new InvalidDataException("Known encounter root is unavailable.");
            var canShiny = _catalog.CanIssueDirectly(knownId, true);
            rolled = roller.RollKnownEncounter(chain, discovered.Contains(knownId), canShiny, canShiny ? Multiplier(knownId) : 1);
        }
        else
        {
            rolled = roller.RollActivity(_roots, discovered, state.ActivityNewGuaranteed, _rarityWeights, _newChance,
                id => _catalog.CanIssueDirectly(id, true), Multiplier);
        }
        var allowedTraits = _traits.Allowed(rolled.SpeciesId, rolled.IsShiny, _catalog);
        var individual = reservedGift ?? new PokemonIndividual
        {
            Id = encounter.Id, SpeciesId = rolled.SpeciesId, EvolutionChainId = rolled.ChainId,
            ChainLength = rolled.ChainLength, Nature = rolled.Nature, IsShiny = rolled.IsShiny,
            CaughtAtUtc = encounter.ReservedAtUtc, AcquisitionSource = encounter.Source,
            FirstTraitId = allowedTraits[random.Next(allowedTraits.Count)]
        };
        if (reservedGift is null)
        {
            individual.CurrentSpeciesId = rolled.SpeciesId;
            individual.FamilyRootId = rolled.SpeciesId;
            individual.ReachedSpeciesIds = [rolled.SpeciesId];
        }
        if (state.Individuals.Any(i => i.Id == individual.Id)) throw new InvalidDataException("Encounter individual ID is already in use.");
        state.Individuals.Add(individual);
        state.DiscoveredSpeciesIds.Add(individual.SpeciesId);
        if (individual.IsShiny) state.ShinySpeciesIds.Add(individual.SpeciesId);
        if (state.ActiveIndividualId is null && state.Individuals.Count == 1) state.ActiveIndividualId = individual.Id;
        if (encounter.Source == "activity")
        {
            state.PendingCatchCount = Math.Max(0, state.PendingCatchCount - 1);
            state.ActivityNewGuaranteed = rolled.WasDuplicateSpecies && _roots.Any(c => !state.DiscoveredSpeciesIds.Contains(c.StarterSpeciesId));
        }
        encounter.Individual = individual;
        encounter.Rarity = RarityClassifier.Classify(_roots.First(c => c.StarterSpeciesId == rolled.SpeciesId).CaptureRate);
        encounter.WasDuplicateSpecies = rolled.WasDuplicateSpecies;
        encounter.Phase = "opened";
        return new EncounterOpenResult(individual, encounter.Rarity.Value, rolled.WasDuplicateSpecies, false);
    }
}
