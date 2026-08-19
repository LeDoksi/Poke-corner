namespace PokeCorner.Core.Domain;

public sealed class PokemonCollection
{
    private readonly List<PokemonIndividual> _individuals = new();

    public IReadOnlyList<PokemonIndividual> Individuals => _individuals;
    public Guid? ActiveIndividualId { get; private set; }

    public void Add(PokemonIndividual individual)
    {
        _individuals.Add(individual);
        ActiveIndividualId ??= individual.Id;
    }

    public bool Release(Guid id)
    {
        var removed = _individuals.RemoveAll(i => i.Id == id) > 0;
        if (removed && ActiveIndividualId == id)
        {
            ActiveIndividualId = _individuals.Count > 0 ? _individuals[0].Id : null;
        }
        return removed;
    }

    public void SetActive(Guid id)
    {
        if (!_individuals.Any(i => i.Id == id))
        {
            throw new InvalidOperationException($"Unknown individual id: {id}");
        }
        ActiveIndividualId = id;
    }

    /// Leaves the desktop empty without removing anyone from the collection — used when
    /// a Hardcore-mode individual runs away from neglect (spec: "никто не выходит
    /// автоматически"; the App plan's composition root calls this, then the user
    /// picks a replacement manually via the collection popup, or waits out the cooldown).
    public void ClearActive() => ActiveIndividualId = null;

    public PokemonIndividual? GetActive() =>
        _individuals.FirstOrDefault(i => i.Id == ActiveIndividualId);

    public bool OwnsSpeciesChain(int evolutionChainId) =>
        _individuals.Any(i => i.EvolutionChainId == evolutionChainId);
}
