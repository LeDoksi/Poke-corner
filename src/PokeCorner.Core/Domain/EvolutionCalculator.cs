namespace PokeCorner.Core.Domain;

public static class EvolutionThresholds
{
    // Recalibrated ×7.5 alongside CatchCheckIntervalUnits — see that constant's comment.
    public const int Stage2Threshold = 37_500;
    public const int Stage3Threshold = 150_000;
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
