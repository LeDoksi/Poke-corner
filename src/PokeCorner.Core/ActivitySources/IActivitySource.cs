namespace PokeCorner.Core.ActivitySources;

public interface IActivitySource
{
    long ReadNewUnitsSinceLastCall();
}
