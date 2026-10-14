using PokeCorner.Core.Domain;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.App.Popup;

/// <param name="SpeciesId">The individual's permanently-fixed starter species.</param>
/// <param name="DisplaySpeciesId">The species id of the individual's CURRENT evolution
/// stage. This is what sprite thumbnails and Pokédex numbers must use, so an evolved
/// Pokémon's card shows Charizard rather than its baby Charmander form.</param>
public sealed record CollectionEntry(
    Guid IndividualId,
    int SpeciesId,
    int DisplaySpeciesId,
    string SpeciesName,
    /// Player-given name, or null. ALWAYS additive: every display site shows this next to
    /// <see cref="SpeciesName"/>, never in place of it.
    string? Nickname,
    bool IsShiny,
    bool IsActive,
    RarityTier Rarity,
    double? EvolutionFraction,
    EvolutionProgressState EvolutionState,
    DateTimeOffset CaughtAtUtc,
    Nature Nature,
    bool IsFavorite,
    /// PokeAPI type names for the CURRENT stage's species — one for most, two for some.
    /// Empty when the catalog in use predates type capture.
    IReadOnlyList<string> Types,
    /// The individual's whole evolution line, spoiler-masked (see EvolutionStagePreview).
    /// Empty for a single-stage species: there is no line to preview.
    IReadOnlyList<EvolutionStagePreview> EvolutionStages,
    /// True when this individual has not yet reached the final stage of its evolution chain
    /// (CurrentStage is 0-based, so the final stage is ChainLength - 1). Drives the Pokédex's
    /// "Не завершившие эволюцию" filter — a single-stage species (ChainLength == 1) is always
    /// already at its final stage, so it never matches.
    bool HasEvolutionsRemaining)
{
    public int Level { get; init; } = 1;
    public bool IsBusy { get; init; }
    public string? FirstTraitId { get; init; }
    public string? SecondTraitId { get; init; }
    public string? DescriptionRu { get; init; }
}

/// One node of the evolution-chain strip in the detail view.
///
/// SPOILER-SAFE BY CONSTRUCTION: for a stage the player's individual has not reached,
/// <see cref="SpeciesId"/> and <see cref="SpeciesName"/> are null and <see cref="Types"/> is
/// empty — the identity is not merely hidden by the template, it is never put into the model
/// at all, so there is nothing to leak through a tooltip, a binding or the visual tree.
public sealed record EvolutionStagePreview(
    int StageIndex,
    int? SpeciesId,
    string? SpeciesName,
    IReadOnlyList<string> Types,
    bool IsCurrent,
    /// True for the stage cosmetically shown on the sprite/thumbnail right now — CurrentStage
    /// unless DisplayStageOverride points somewhere earlier. Coexists with IsCurrent (real
    /// progress) rather than replacing it: the two differ exactly when an override is set.
    bool IsDisplayed)
{
    /// Reached stages are revealed; everything past the individual's current stage is a
    /// silhouette. Derived from the presence of the identity rather than stored separately, so
    /// the two can never contradict each other.
    public bool IsRevealed => SpeciesId is not null;
}

public sealed record PendingEggEntry(Guid EggId, RarityTier Tier, double HatchFraction, bool IsReady);

public sealed record OwnedIndividualEntry(Guid IndividualId, string? Nickname, int Level,
    bool IsShiny, bool IsBusy, bool IsActive, DateTimeOffset CaughtAtUtc);

public sealed record PokedexSpeciesEntry(int SpeciesId, string? DisplayName, string? DescriptionRu,
    IReadOnlyList<string> Types, string? Rarity, string RouteHint, bool IsDiscovered,
    bool HasSilhouette, bool HasEvolutionsRemaining, IReadOnlyList<OwnedIndividualEntry> OwnedIndividuals)
{
    public string Number => $"#{SpeciesId:D4}";
    public string AccessibleName => IsDiscovered ? $"{Number} {DisplayName}" : $"{Number} · {RouteHint}";
}

public sealed class CollectionViewModel
{
    private readonly PokemonCollection _collection;
    private readonly IDictionary<Guid, CareState> _careStates;
    private readonly CareModeService _careModeService;
    private readonly IReadOnlyList<SpeciesEvolutionChain> _chains;
    private readonly IReadOnlyList<PendingEgg> _pendingEggs;
    private readonly Func<IReadOnlySet<Guid>> _busyIndividualIds;
    private readonly IReadOnlyList<CatalogSpecies> _species;
    private readonly IReadOnlyDictionary<int, CatalogSpecies> _speciesById;
    private readonly Func<IReadOnlySet<int>> _discoveredSpeciesIds;
    private readonly IReadOnlyDictionary<int, string> _routeHints;
    private readonly Func<PokemonIndividual, int> _levelFor;

    public CollectionViewModel(PokemonCollection collection, IDictionary<Guid, CareState> careStates, CareModeService careModeService, IReadOnlyList<SpeciesEvolutionChain> chains, IReadOnlyList<PendingEgg> pendingEggs, Func<IReadOnlySet<Guid>>? busyIndividualIds = null,
        IReadOnlyList<CatalogSpecies>? species = null, Func<IReadOnlySet<int>>? discoveredSpeciesIds = null,
        IReadOnlyDictionary<int, string>? routeHints = null, Func<PokemonIndividual, int>? levelFor = null)
    {
        _collection = collection;
        _careStates = careStates;
        _careModeService = careModeService;
        _chains = chains;
        _pendingEggs = pendingEggs;
        _busyIndividualIds = busyIndividualIds ?? (() => new HashSet<Guid>());
        _species = species ?? [];
        _speciesById = _species.ToDictionary(s => s.Id);
        _discoveredSpeciesIds = discoveredSpeciesIds ?? (() => new HashSet<int>());
        _routeHints = routeHints ?? new Dictionary<int, string>();
        _levelFor = levelFor ?? (_ => 1);
    }

    public CollectionEntry? GetEntry(Guid id, DateTimeOffset nowUtc) =>
        GetEntries(nowUtc).FirstOrDefault(entry => entry.IndividualId == id);

    public Guid? ResolveDirectIndividual(int speciesId)
    {
        var ids = _collection.Individuals.Where(i => ResolveCurrentSpeciesId(i) == speciesId)
            .Select(i => i.Id).Take(2).ToArray();
        return ids.Length == 1 ? ids[0] : null;
    }

    public IReadOnlyList<PokedexSpeciesEntry> GetSpeciesEntries()
    {
        var discovered = _discoveredSpeciesIds().ToHashSet();
        foreach (var individual in _collection.Individuals)
        {
            discovered.Add(individual.SpeciesId);
            discovered.UnionWith(individual.ReachedSpeciesIds);
            discovered.Add(ResolveCurrentSpeciesId(individual));
        }
        var busy = _busyIndividualIds();
        var ownedBySpecies = _collection.Individuals.GroupBy(ResolveCurrentSpeciesId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        return _species.OrderBy(species => species.Id).Select(species =>
        {
            var open = discovered.Contains(species.Id);
            var current = ownedBySpecies.GetValueOrDefault(species.Id) ?? [];
            var owned = current
                .Select(i => new OwnedIndividualEntry(i.Id, i.Nickname, _levelFor(i), i.IsShiny,
                    busy.Contains(i.Id), i.Id == _collection.ActiveIndividualId, i.CaughtAtUtc)).ToList();
            return new PokedexSpeciesEntry(species.Id,
                open ? species.DisplayName : null,
                open ? species.DescriptionRu : null,
                open ? species.Types : [],
                open ? species.Rarity : null,
                _routeHints.GetValueOrDefault(species.Id) ?? "Местность пока неизвестна",
                open, species.NormalAvailable,
                open && current.Any(i => (int)i.CurrentStage < i.ChainLength - 1), owned);
        }).ToList();
    }

    public static bool MatchesSpeciesSearch(PokedexSpeciesEntry entry, string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        var needle = query.Trim();
        return entry.Number.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || entry.SpeciesId.ToString().Contains(needle, StringComparison.OrdinalIgnoreCase)
            || entry.RouteHint.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || (entry.DisplayName?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    public static IReadOnlyList<PokedexSpeciesEntry> FilterSpecies(IReadOnlyList<PokedexSpeciesEntry> entries,
        string? search, string? type, string? rarity, bool canEvolve, SpeciesSortOrder sort = SpeciesSortOrder.Number)
    {
        var filtered = entries.Where(e => MatchesSpeciesSearch(e, search))
            .Where(e => type is null || e.IsDiscovered && e.Types.Contains(type, StringComparer.OrdinalIgnoreCase))
            .Where(e => rarity is null || e.IsDiscovered && string.Equals(e.Rarity, rarity, StringComparison.OrdinalIgnoreCase))
            .Where(e => !canEvolve || e.IsDiscovered && e.HasEvolutionsRemaining);
        return sort switch
        {
            SpeciesSortOrder.Type => filtered.OrderBy(e => !e.IsDiscovered)
                .ThenBy(e => e.Types.FirstOrDefault(), StringComparer.OrdinalIgnoreCase).ThenBy(e => e.SpeciesId).ToList(),
            SpeciesSortOrder.CatchDate => filtered.OrderBy(e => !e.IsDiscovered)
                .ThenBy(e => e.OwnedIndividuals.Count == 0)
                .ThenByDescending(e => e.OwnedIndividuals.Select(i => i.CaughtAtUtc).DefaultIfEmpty().Max())
                .ThenBy(e => e.SpeciesId).ToList(),
            _ => filtered.OrderBy(e => e.SpeciesId).ToList()
        };
    }

    /// The grid's default order, in three bands: the individual on the desktop, then the
    /// favorites, then everything else — by dex number WITHIN each band, so the same species
    /// caught twice sits together.
    ///
    /// Dex number is the CURRENT stage's species id (what the card shows), so an evolved
    /// Charmander sorts where Charmeleon does. Filtering only narrows this list; it never reorders it.
    public IReadOnlyList<CollectionEntry> GetEntries(DateTimeOffset nowUtc) =>
        _collection.Individuals
            .OrderByDescending(i => i.Id == _collection.ActiveIndividualId)
            .ThenByDescending(i => i.IsFavorite)
            .ThenBy(ResolveDisplaySpeciesId)
            .Select(individual =>
        {
            var displaySpeciesId = ResolveDisplaySpeciesId(individual);
            var progress = ProgressCalculator.Compute(new Core.Activity.ActivityAccumulator(), individual, new FeedingInventory());
            return new CollectionEntry(
                individual.Id,
                individual.SpeciesId,
                displaySpeciesId,
                _speciesById.GetValueOrDefault(displaySpeciesId)?.DisplayName ?? _chains.ResolveSpeciesName(displaySpeciesId),
                individual.Nickname,
                individual.IsShiny,
                IsActive: individual.Id == _collection.ActiveIndividualId,
                Rarity: RarityForSpecies(displaySpeciesId, individual.EvolutionChainId),
                EvolutionFraction: progress.EvolutionFraction,
                EvolutionState: progress.EvolutionState,
                CaughtAtUtc: individual.CaughtAtUtc,
                Nature: individual.Nature,
                IsFavorite: individual.IsFavorite,
                Types: _speciesById.GetValueOrDefault(displaySpeciesId)?.Types ?? _chains.ResolveSpeciesTypes(displaySpeciesId),
                EvolutionStages: BuildEvolutionPreview(individual),
                HasEvolutionsRemaining: (int)individual.CurrentStage < individual.ChainLength - 1)
            {
                Level = _levelFor(individual),
                IsBusy = _busyIndividualIds().Contains(individual.Id),
                FirstTraitId = individual.FirstTraitId,
                SecondTraitId = individual.SecondTraitId,
                DescriptionRu = _speciesById.GetValueOrDefault(displaySpeciesId)?.DescriptionRu
            };
        }).ToList();

    /// Builds the spoiler-masked evolution strip for one individual: every stage its chain
    /// has, with the identity filled in only up to the stage this individual has reached.
    ///
    /// Returns EMPTY for a single-stage species (and for a chain missing from the catalog) —
    /// a one-node "chain" with no arrows would be a confusing thing to draw, so the section
    /// simply doesn't render.
    private IReadOnlyList<EvolutionStagePreview> BuildEvolutionPreview(PokemonIndividual individual)
    {
        var chain = _chains.FirstOrDefault(c => c.ChainId == individual.EvolutionChainId);
        var explicitSpecies = individual.CurrentSpeciesId > 0;
        if (!explicitSpecies && (chain is null || chain.ChainLength <= 1)) return Array.Empty<EvolutionStagePreview>();
        var stageCount = explicitSpecies ? Math.Max(individual.ReachedSpeciesIds.Count, chain?.ChainLength ?? 0) : chain!.ChainLength;
        if (stageCount <= 1) return Array.Empty<EvolutionStagePreview>();

        var reachedIndex = explicitSpecies ? individual.ReachedSpeciesIds.Count - 1 : (int)individual.CurrentStage;
        // Same clamp as ResolveDisplaySpeciesId: an out-of-range override (e.g. a hand-edited
        // save) falls back to the real current stage rather than trusting the stored value.
        var displayedIndex = individual.DisplayStageOverride is { } overrideIndex && overrideIndex >= 0 && overrideIndex <= reachedIndex
            ? overrideIndex
            : reachedIndex;
        var stages = new List<EvolutionStagePreview>(stageCount);

        for (var index = 0; index < stageCount; index++)
        {
            // Revealed only up to and including the stage actually reached. The bounds check
            // also covers a legacy chain whose ChainLength outruns its stage id list.
            var reachedSpecies = explicitSpecies ? individual.ReachedSpeciesIds : chain!.StageSpeciesIds;
            var isRevealed = index <= reachedIndex && index < reachedSpecies.Count;
            int? speciesId = isRevealed ? reachedSpecies[index] : null;

            stages.Add(new EvolutionStagePreview(
                StageIndex: index,
                SpeciesId: speciesId,
                SpeciesName: speciesId is null ? null : _speciesById.GetValueOrDefault(speciesId.Value)?.DisplayName ?? _chains.ResolveSpeciesName(speciesId.Value),
                Types: speciesId is null ? Array.Empty<string>() : _speciesById.GetValueOrDefault(speciesId.Value)?.Types ?? _chains.ResolveSpeciesTypes(speciesId.Value),
                IsCurrent: index == reachedIndex,
                IsDisplayed: index == displayedIndex));
        }

        return stages;
    }


    private int ResolveDisplaySpeciesId(PokemonIndividual individual) => individual.ResolveDisplaySpeciesId(_chains);

    private int ResolveCurrentSpeciesId(PokemonIndividual individual)
    {
        if (individual.CurrentSpeciesId > 0) return individual.CurrentSpeciesId;
        var chain = _chains.FirstOrDefault(c => c.ChainId == individual.EvolutionChainId);
        var stage = (int)individual.CurrentStage;
        return chain is not null && stage >= 0 && stage < chain.StageSpeciesIds.Count
            ? chain.StageSpeciesIds[stage] : individual.SpeciesId;
    }

    public IReadOnlyList<PendingEggEntry> GetPendingEggEntries(DateTimeOffset nowUtc) =>
        _pendingEggs.Select(egg =>
        {
            var startedAtUtc = egg.ReadyAtUtc - EggIncubationService.HatchDuration;
            var elapsedSeconds = (nowUtc - startedAtUtc).TotalSeconds;
            var fraction = Math.Clamp(elapsedSeconds / EggIncubationService.HatchDuration.TotalSeconds, 0.0, 1.0);
            return new PendingEggEntry(egg.Id, egg.Tier, fraction, nowUtc >= egg.ReadyAtUtc);
        }).ToList();

    private RarityTier RarityForChain(int evolutionChainId)
    {
        var chain = _chains.FirstOrDefault(c => c.ChainId == evolutionChainId);
        return chain is null ? RarityTier.Common : RarityClassifier.Classify(chain.CaptureRate);
    }

    private RarityTier RarityForSpecies(int speciesId, int evolutionChainId) =>
        Enum.TryParse<RarityTier>(_speciesById.GetValueOrDefault(speciesId)?.Rarity, true, out var tier)
            ? tier : RarityForChain(evolutionChainId);

    public bool TrySetActive(Guid id, DateTimeOffset nowUtc)
    {
        if (!_careStates.TryGetValue(id, out var careState))
        {
            careState = new CareState();
            _careStates[id] = careState;
        }
        _collection.SetActive(id);
        _careModeService.RegisterInteraction(careState, nowUtc);
        return true;
    }

    /// Flips the pin on one individual and reports its new state. Returns false when the id
    /// isn't in the collection (e.g. a stale card clicked after a release).
    public bool ToggleFavorite(Guid id)
    {
        var individual = _collection.Individuals.FirstOrDefault(i => i.Id == id);
        if (individual is null) return false;

        individual.IsFavorite = !individual.IsFavorite;
        return true;
    }

    /// Renames one individual. Sanitization (trim, empty => "no nickname", length clamp) lives
    /// in <see cref="PokemonIndividual.Nickname"/>'s setter, so passing "" or "   " here clears
    /// the nickname rather than storing a blank one. Returns false for an unknown id, matching
    /// <see cref="ToggleFavorite"/>.
    public bool SetNickname(Guid id, string? nickname)
    {
        var individual = _collection.Individuals.FirstOrDefault(i => i.Id == id);
        if (individual is null) return false;

        individual.Nickname = nickname;
        return true;
    }

    /// The Pokédex search box's rule, kept next to the entries it filters rather than in the
    /// view's code-behind so it can be tested without a WPF host. Case-insensitive substring
    /// against BOTH names — a player who nicknamed their Pikachu "Sparky" must be able to find
    /// it by either word — and an empty/whitespace query matches everything.
    public static bool MatchesSearch(CollectionEntry entry, string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        var needle = query.Trim();

        return entry.SpeciesName.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || (entry.Nickname?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// Re-orders an already-filtered entry list. Default leaves GetEntries' own banded order
    /// (active -> favorites -> rest, each by dex number) alone. Alphabetical sorts by species
    /// name (not the optional nickname — the one every entry always has). CatchDate is
    /// newest-first, matching "what did I just get" being the more common reason to sort by it.
    public static IReadOnlyList<CollectionEntry> ApplySort(IReadOnlyList<CollectionEntry> entries, PokedexSortOrder order) => order switch
    {
        PokedexSortOrder.Alphabetical => entries.OrderBy(e => e.SpeciesName, StringComparer.OrdinalIgnoreCase).ToList(),
        PokedexSortOrder.CatchDate => entries.OrderByDescending(e => e.CaughtAtUtc).ToList(),
        PokedexSortOrder.Number => entries.OrderBy(e => e.DisplaySpeciesId).ThenBy(e => e.CaughtAtUtc).ToList(),
        PokedexSortOrder.Type => entries.OrderBy(e => e.Types.FirstOrDefault(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.DisplaySpeciesId).ToList(),
        _ => entries
    };
}

/// The Pokédex grid's sort choice — see CollectionViewModel.ApplySort.
public enum PokedexSortOrder { Default, Alphabetical, CatchDate, Number, Type }
public enum SpeciesSortOrder { Number, CatchDate, Type }
