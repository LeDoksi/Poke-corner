using PokeCorner.Core.Persistence;
namespace PokeCorner.App;

// Application-session lifetime, deliberately independent of menu/view lifetimes and save schema.
internal sealed class ExpeditionReadyNotifications
{
    private readonly HashSet<Guid> _announced = [];
    internal void ObserveCommitted(SaveData snapshot, Action<string, string> notify)
    {
        // A past ReadyAtUtc is enough for a badge, never for a toast before the ready commit.
        if (snapshot.Expedition is not { Status: "ready" } trip || _announced.Contains(trip.Id)) return;
        notify("PokéCorner", "Команда вернулась из экспедиции. Заберите находки в разделе «Экспедиции».");
        _announced.Add(trip.Id);
    }
}
