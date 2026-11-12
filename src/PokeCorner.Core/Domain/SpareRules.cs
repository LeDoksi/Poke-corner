namespace PokeCorner.Core.Domain;

/// D-42 (Georgy 11.11): the Pokédex filter «Лишние дубли». The goal of the game is a full collection, so the
/// filter may only name a Pokémon whose release loses nothing:
/// - a copy is compared only with the same species in the same form (a shiny and a normal one are not duplicates);
/// - the best copy always stays (favourite or active first, then level, care, the older one);
/// - while the family still has forms to open by growing, one more copy stays for every such form;
/// - a favourite or the active pet is never offered, whatever the count.
public static class SpareRules
{
    public sealed record Copy(Guid Id, int SpeciesId, bool IsShiny, bool Protected, int Level, long CarePoints,
        DateTimeOffset CaughtAtUtc, int UnopenedAhead);

    public static IReadOnlySet<Guid> Spare(IEnumerable<Copy> copies)
    {
        var spare = new HashSet<Guid>();
        foreach (var group in copies.GroupBy(c => (c.SpeciesId, c.IsShiny)))
        {
            var ordered = group.OrderByDescending(c => c.Protected).ThenByDescending(c => c.Level)
                .ThenByDescending(c => c.CarePoints).ThenBy(c => c.CaughtAtUtc).ThenBy(c => c.Id).ToArray();
            var keep = 1 + Math.Max(0, ordered.Max(c => c.UnopenedAhead));
            foreach (var copy in ordered.Skip(keep))
                if (!copy.Protected) spare.Add(copy.Id);
        }

        return spare;
    }
}
