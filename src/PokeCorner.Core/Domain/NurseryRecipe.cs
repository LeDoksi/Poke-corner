using System.Collections.ObjectModel;

namespace PokeCorner.Core.Domain;

public sealed record NurseryOutcome(int SpeciesId, string Rarity, int Weight, bool ShinyAvailable);

public sealed class NurseryRecipe
{
    public int FirstParentId { get; }
    public int SecondParentId { get; }
    public IReadOnlyList<NurseryOutcome> Outcomes { get; }
    public IReadOnlyList<NurseryOutcome> AvailableOutcomes { get; }
    public IReadOnlyDictionary<string, int> BaseBerries { get; }
    public IReadOnlyDictionary<string, int> SelectedSurcharge { get; }
    public bool NeedsThemeReview { get; }
    public int? Hours { get; }
    public bool IsAvailable => Hours.HasValue;
    public bool SelectionUseful => AvailableOutcomes.Count > 1;

    internal NurseryRecipe(int firstParentId, int secondParentId, IReadOnlyList<NurseryOutcome> outcomes,
        IReadOnlyList<NurseryOutcome> availableOutcomes, IReadOnlyDictionary<string, int> baseBerries,
        IReadOnlyDictionary<string, int> selectedSurcharge, bool needsThemeReview, int? hours)
    {
        FirstParentId = firstParentId;
        SecondParentId = secondParentId;
        Outcomes = Array.AsReadOnly(outcomes.ToArray());
        AvailableOutcomes = Array.AsReadOnly(availableOutcomes.ToArray());
        BaseBerries = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(baseBerries));
        SelectedSurcharge = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(selectedSurcharge));
        NeedsThemeReview = needsThemeReview;
        Hours = hours;
    }
}
