namespace PokeCorner.Core.Domain;

public sealed record SpeciesEvolutionChain(
    int ChainId,
    int StarterSpeciesId,
    int CaptureRate,
    int ChainLength,
    IReadOnlyList<int> StageSpeciesIds);

public sealed record CatchResult(int ChainId, int SpeciesId, Nature Nature, bool IsShiny, bool WasDuplicateSpecies, int ChainLength);
