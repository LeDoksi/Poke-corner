using PokeCorner.Core.Domain;

namespace PokeCorner.App.Popup;

/// <summary>Closed player-ready encounters; gift and activity identities stay hidden until Open commits.</summary>
public static class CatchBallQueue
{
    public static int GiftReadyCount(IReadOnlyList<EncounterState> encounters) =>
        encounters.Count(e => e.Source == "gift" && (e.Phase is "sealed" or "resolved"));

    public static bool IsReady(EncounterState encounter) => encounter.Source switch
    {
        "gift" => encounter.Phase is "sealed" or "resolved",
        "activity" => encounter.Phase == "sealed",
        "expedition" => encounter is { Phase: "resolved", KnownSpeciesId: not null, Individual: not null },
        _ => false
    };

    public static int ReadyCount(IReadOnlyList<EncounterState> encounters, int activityReadyCount) =>
        (int)Math.Min(int.MaxValue, (long)activityReadyCount + GiftReadyCount(encounters) + encounters.Count(e => e.Source == "expedition" && IsReady(e)));

    public static EncounterState? NextReady(IReadOnlyList<EncounterState> encounters) =>
        encounters.FirstOrDefault(e => e.Source == "gift" && (e.Phase is "sealed" or "resolved"))
        ?? encounters.FirstOrDefault(e => e.Source == "activity" && e.Phase == "sealed")
        ?? encounters.FirstOrDefault(e => e.Source == "expedition" && IsReady(e));
}
