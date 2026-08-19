namespace PokeCorner.Core.Domain;

public sealed class FeedingInventory
{
    public int Treats { get; set; }

    /// <summary>
    /// Leftover activity units from the last <see cref="FeedingService.EarnTreatsFromActivity"/> call
    /// that weren't enough to earn a whole treat. Carried forward so repeated small-unit calls
    /// (e.g. every 5 seconds) don't truncate the fractional progress away each time.
    /// </summary>
    public long RemainderUnits { get; set; }
}
