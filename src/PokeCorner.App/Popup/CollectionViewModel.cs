using PokeCorner.Core.Domain;

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
    bool IsAvailableToActivate,
    DateTimeOffset? AvailableAgainAtUtc,
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
    bool HasEvolutionsRemaining);

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

public sealed class CollectionViewModel
{
    private readonly PokemonCollection _collection;
    private readonly IDictionary<Guid, CareState> _careStates;
    private readonly CareModeService _careModeService;
    private readonly CareMode _careMode;
    private readonly IReadOnlyList<SpeciesEvolutionChain> _chains;
    private readonly IReadOnlyList<PendingEgg> _pendingEggs;

    public CollectionViewModel(PokemonCollection collection, IDictionary<Guid, CareState> careStates, CareModeService careModeService, CareMode careMode, IReadOnlyList<SpeciesEvolutionChain> chains, IReadOnlyList<PendingEgg> pendingEggs)
    {
        _collection = collection;
        _careStates = careStates;
        _careModeService = careModeService;
        _careMode = careMode;
        _chains = chains;
        _pendingEggs = pendingEggs;
    }

    /// The grid's default order, in three bands: the individual currently on the desktop, then
    /// the favorites, then everything else — with dex number ordering WITHIN each band. Catch
    /// order used to be the tie-breaker, which made a large collection unbrowsable: the same
    /// species caught twice sat at opposite ends of the list.
    ///
    /// Dex number here is the CURRENT stage's species id (what the card actually shows), not
    /// the fixed starter species, so an evolved Charmander sorts where Charmeleon does.
    /// Filtering (rarity pills, the search box) only narrows this list; it never reorders it.
    public IReadOnlyList<CollectionEntry> GetEntries(DateTimeOffset nowUtc) =>
        _collection.Individuals
            .OrderByDescending(i => i.Id == _collection.ActiveIndividualId)
            .ThenByDescending(i => i.IsFavorite)
            .ThenBy(ResolveDisplaySpeciesId)
            .Select(individual =>
        {
            var careState = _careStates.TryGetValue(individual.Id, out var state) ? state : new CareState();
            var isAvailable = _careMode != CareMode.Hardcore || _careModeService.IsAvailableToPlaceOnDesktop(careState, nowUtc);
            var displaySpeciesId = ResolveDisplaySpeciesId(individual);
            var progress = ProgressCalculator.Compute(new Core.Activity.ActivityAccumulator(), individual, new FeedingInventory());
            return new CollectionEntry(
                individual.Id,
                individual.SpeciesId,
                displaySpeciesId,
                _chains.ResolveSpeciesName(displaySpeciesId),
                individual.Nickname,
                individual.IsShiny,
                IsActive: individual.Id == _collection.ActiveIndividualId,
                IsAvailableToActivate: isAvailable,
                AvailableAgainAtUtc: isAvailable ? null : careState.RanAwayUntilUtc,
                Rarity: RarityForChain(individual.EvolutionChainId),
                EvolutionFraction: progress.EvolutionFraction,
                EvolutionState: progress.EvolutionState,
                CaughtAtUtc: individual.CaughtAtUtc,
                Nature: individual.Nature,
                IsFavorite: individual.IsFavorite,
                Types: _chains.ResolveSpeciesTypes(displaySpeciesId),
                EvolutionStages: BuildEvolutionPreview(individual),
                HasEvolutionsRemaining: (int)individual.CurrentStage < individual.ChainLength - 1);
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
        if (chain is null || chain.ChainLength <= 1) return Array.Empty<EvolutionStagePreview>();

        var reachedIndex = (int)individual.CurrentStage;
        // Same clamp as ResolveDisplaySpeciesId: an out-of-range override (e.g. a hand-edited
        // save) falls back to the real current stage rather than trusting the stored value.
        var displayedIndex = individual.DisplayStageOverride is { } overrideIndex && overrideIndex >= 0 && overrideIndex <= reachedIndex
            ? overrideIndex
            : reachedIndex;
        var stages = new List<EvolutionStagePreview>(chain.ChainLength);

        for (var index = 0; index < chain.ChainLength; index++)
        {
            // Revealed only up to and including the stage actually reached. The bounds check
            // also covers a legacy chain whose ChainLength outruns its stage id list.
            var isRevealed = index <= reachedIndex && index < chain.StageSpeciesIds.Count;
            int? speciesId = isRevealed ? chain.StageSpeciesIds[index] : null;

            stages.Add(new EvolutionStagePreview(
                StageIndex: index,
                SpeciesId: speciesId,
                SpeciesName: speciesId is null ? null : _chains.ResolveSpeciesName(speciesId.Value),
                Types: speciesId is null ? Array.Empty<string>() : _chains.ResolveSpeciesTypes(speciesId.Value),
                IsCurrent: index == reachedIndex,
                IsDisplayed: index == displayedIndex));
        }

        return stages;
    }

    // (GetPokedexEntries lived here: one deduped entry per distinct DISPLAY species, feeding
    // a Pokédex grid that sat above a separate roster list of the same Pokémon. The two lists
    // were merged into one per-individual grid, so GetEntries above is now the only source
    // the collection screen reads and the deduped projection had no remaining caller. The
    // "Открыто видов: N" completion stat is still distinct-species — the view derives it with
    // a Distinct() over the same entries rather than a second pass over the collection.)

    private int ResolveDisplaySpeciesId(PokemonIndividual individual) => individual.ResolveDisplaySpeciesId(_chains);

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

    public bool TrySetActive(Guid id, DateTimeOffset nowUtc)
    {
        if (!_careStates.TryGetValue(id, out var careState))
        {
            careState = new CareState();
            _careStates[id] = careState;
        }
        var isAvailable = _careMode != CareMode.Hardcore || _careModeService.IsAvailableToPlaceOnDesktop(careState, nowUtc);
        if (!isAvailable) return false;

        _collection.SetActive(id);
        _careModeService.RegisterInteraction(careState, nowUtc);
        return true;
    }

    public bool Release(Guid id) => _collection.Release(id);

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
        _ => entries
    };
}

/// The Pokédex grid's sort choice — see CollectionViewModel.ApplySort.
public enum PokedexSortOrder { Default, Alphabetical, CatchDate }
