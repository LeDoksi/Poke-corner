using PokeCorner.Core.Persistence;

namespace PokeCorner.Core.Domain;

public sealed class NurseryState : TimedActivity
{
    public required PokemonIndividual Child { get; set; }
    public bool ShinySupported { get; set; }
    public ShinyDraw ShinyDraw { get; set; }
    public int? SelectedSpeciesId { get; set; }
    public Guid? HatchOperationId { get; set; }
}
