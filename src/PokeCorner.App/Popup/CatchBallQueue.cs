using PokeCorner.Core.Domain;

namespace PokeCorner.App.Popup;

/// <summary>Only closed, player-ready balls; gift results stay hidden until Open commits.</summary>
public static class CatchBallQueue
{
    public static int GiftReadyCount(IReadOnlyList<EncounterState> encounters) =>
        encounters.Count(e => e.Source == "gift" && (e.Phase is "sealed" or "resolved"));

    public static int ReadyCount(IReadOnlyList<EncounterState> encounters, int activityReadyCount) =>
        (int)Math.Min(int.MaxValue, (long)activityReadyCount + GiftReadyCount(encounters));

    public static EncounterState? NextReady(IReadOnlyList<EncounterState> encounters) =>
        encounters.FirstOrDefault(e => e.Source == "gift" && (e.Phase is "sealed" or "resolved"))
        ?? encounters.FirstOrDefault(e => e.Source == "activity" && e.Phase == "sealed");
}
