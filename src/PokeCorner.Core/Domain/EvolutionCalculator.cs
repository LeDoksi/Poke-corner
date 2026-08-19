namespace PokeCorner.Core.Domain;

public static class EvolutionThresholds
{
    public const int Stage2Threshold = 5_000;
    public const int Stage3Threshold = 20_000;
}

public static class EvolutionCalculator
{
    public static EvolutionStage StageForPoints(int accumulatedCarePoints, int chainLength)
    {
        if (chainLength >= 3 && accumulatedCarePoints >= EvolutionThresholds.Stage3Threshold)
        {
            return EvolutionStage.Stage3;
        }
        if (chainLength >= 2 && accumulatedCarePoints >= EvolutionThresholds.Stage2Threshold)
        {
            return EvolutionStage.Stage2;
        }
        return EvolutionStage.Stage1;
    }
}
