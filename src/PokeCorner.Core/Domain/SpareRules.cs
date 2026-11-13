namespace PokeCorner.Core.Domain;

/// D-42 (Georgy 11.11): the Pokédex filter «Лишние дубли». The goal of the game is a full collection, so the
/// filter may only name a Pokémon whose release loses nothing:
/// - a copy is compared with the same species; since D-50 shiny is a look, so a shiny and a normal one are duplicates;
/// - the best copy always stays (favourite or active first, then level, care, the older one);
/// - while the family still has forms to open by growing, one more copy stays for every such form;
/// - a favourite or the active pet is never offered, whatever the count;
/// - D-53 (Georgy 13.11): a younger stage is spare too when a grown relative is in the collection (a Geodude
///   next to a Golem, whether that Golem grew from a Geodude or was met grown), as long as growing it would open nothing new. Its page and its looks stay open.
public static class SpareRules
{
    public sealed record Copy(Guid Id, int SpeciesId, bool IsShiny, bool Protected, int Level, long CarePoints,
        DateTimeOffset CaughtAtUtc, int UnopenedAhead)
    {
        /// Every species this pet has been, its current one included. Empty when the caller does not know.
        public IReadOnlyCollection<int> Reached { get; init; } = [];
        /// Every species this pet could still grow into. Empty when the caller does not know.
        public IReadOnlyCollection<int> Ahead { get; init; } = [];
    }

    public static IReadOnlySet<Guid> Spare(IEnumerable<Copy> copies)
    {
        var spare = new HashSet<Guid>();
        var all = copies as IReadOnlyCollection<Copy> ?? copies.ToArray();
        foreach (var group in all.GroupBy(c => c.SpeciesId))
        {
            var ordered = group.OrderByDescending(c => c.Protected).ThenByDescending(c => c.Level)
                .ThenByDescending(c => c.CarePoints).ThenBy(c => c.CaughtAtUtc).ThenBy(c => c.Id).ToArray();
            var keep = 1 + Math.Max(0, ordered.Max(c => c.UnopenedAhead));
            foreach (var copy in ordered.Skip(keep))
                if (!copy.Protected) spare.Add(copy.Id);
        }

        foreach (var copy in all)
            if (!copy.Protected && copy.UnopenedAhead <= 0 && !spare.Contains(copy.Id)
                && all.Any(other => other.Id != copy.Id && other.SpeciesId != copy.SpeciesId
                    && (other.Reached.Contains(copy.SpeciesId) || copy.Ahead.Contains(other.SpeciesId))))
                spare.Add(copy.Id);

        return spare;
    }
}
