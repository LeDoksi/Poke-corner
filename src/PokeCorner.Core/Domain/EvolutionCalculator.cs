namespace PokeCorner.Core.Domain;

public static class EvolutionThresholds
{
    // Expressed relative to ActivityAccumulator.CatchCheckIntervalUnits (22_500) on purpose:
    // per live feedback the first evolution must cost noticeably MORE activity than a catch,
    // and at the old 37_500 it landed at ~1.7 catches — evolutions outpaced catches. ~3.1 and
    // ~12.4 catches keep the original 4x Stage2:Stage3 ratio.
    public const int Stage2Threshold = 70_000;
    public const int Stage3Threshold = 280_000;
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
