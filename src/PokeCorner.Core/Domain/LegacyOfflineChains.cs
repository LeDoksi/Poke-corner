namespace PokeCorner.Core.Domain;

/// <summary>
/// Rescue data for individuals caught before the bundled catalog was unified with PokeAPI's
/// real numbering.
///
/// The old hand-authored offline starter pool invented its own <c>ChainId</c> values, and
/// most of them collided with COMPLETELY UNRELATED real PokeAPI evolution chains — verified
/// live against pokeapi.co:
///
///   old id 4   -> really Caterpie/Metapod/Butterfree   (not Charmander's line)
///   old id 7   -> really Rattata/Raticate              (not Squirtle's line)
///   old id 10  -> really Pichu/Pikachu/Raichu, 3 stages (the old entry was a 2-stage Pikachu)
///   old id 152 -> really Meditite/Medicham             (not Chikorita's line)
///   old id 155 -> really Minun, single-stage           (not Cyndaquil's line)
///   old id 158 -> really Budew/Roselia/Roserade        (not Totodile's line)
///   old ids 10001/10002 -> not real chain ids at all; they resolve to nothing
///
/// Now that the bundled catalog IS the real PokeAPI data, an individual still carrying one of
/// those old ids would resolve to the wrong species (the app's own maintainer watched their
/// Pikachu start displaying as "Покемон #172"/Pichu). <see cref="Migration"/> maps each old
/// individual onto a reserved id in the 900000 range that can never collide with PokeAPI, and
/// the chains below reproduce that individual's ORIGINAL shape exactly — same stage count,
/// same evolution targets. Deliberately NOT retrofitted with baby pre-evolutions: an
/// individual caught as a stage-0-of-2 Pikachu stays a 2-stage Pikachu -> Raichu.
///
/// These chains are lookup-only. They are merged into
/// <c>SpeciesCatalog.LoadForLookup()</c> (display species, rarity) but deliberately NOT into
/// <c>LoadCachedOrFallback()</c>, which is the catch/hatch roll pool — rolling from them
/// would let a player catch a second, duplicate Bulbasaur line under a legacy id.
/// </summary>
public static class LegacyOfflineChains
{
    public const int FirstLegacyChainId = 900001;

    public static IReadOnlyList<SpeciesEvolutionChain> All { get; } = new List<SpeciesEvolutionChain>
    {
        new(900001, 1, 45, 3, new[] { 1, 2, 3 }, new[] { "Bulbasaur", "Ivysaur", "Venusaur" }),
        new(900002, 4, 45, 3, new[] { 4, 5, 6 }, new[] { "Charmander", "Charmeleon", "Charizard" }),
        new(900003, 7, 45, 3, new[] { 7, 8, 9 }, new[] { "Squirtle", "Wartortle", "Blastoise" }),
        new(900004, 25, 190, 2, new[] { 25, 26 }, new[] { "Pikachu", "Raichu" }),
        new(900005, 152, 45, 3, new[] { 152, 153, 154 }, new[] { "Chikorita", "Bayleef", "Meganium" }),
        new(900006, 155, 45, 3, new[] { 155, 156, 157 }, new[] { "Cyndaquil", "Quilava", "Typhlosion" }),
        new(900007, 158, 45, 3, new[] { 158, 159, 160 }, new[] { "Totodile", "Croconaw", "Feraligatr" }),
        new(900008, 10, 255, 3, new[] { 10, 11, 12 }, new[] { "Caterpie", "Metapod", "Butterfree" }),
        new(900009, 16, 255, 3, new[] { 16, 17, 18 }, new[] { "Pidgey", "Pidgeotto", "Pidgeot" })
    };

    /// <summary>
    /// The exact (SpeciesId, ChainLength, EvolutionChainId) triples the old offline pool could
    /// produce, mapped to their replacement legacy chain id. Matching on the full triple — not
    /// on the chain id alone — is what keeps this surgical: an individual legitimately caught
    /// from the real catalog on chain 4 (Caterpie) has SpeciesId 10, not 4, so it is left
    /// completely alone.
    /// </summary>
    public static IReadOnlyDictionary<(int SpeciesId, int ChainLength, int EvolutionChainId), int> Migration { get; } =
        new Dictionary<(int, int, int), int>
        {
            [(1, 3, 1)] = 900001,
            [(4, 3, 4)] = 900002,
            [(7, 3, 7)] = 900003,
            [(25, 2, 10)] = 900004,
            [(152, 3, 152)] = 900005,
            [(155, 3, 155)] = 900006,
            [(158, 3, 158)] = 900007,
            [(10, 3, 10001)] = 900008,
            [(16, 3, 10002)] = 900009
        };

    /// <summary>
    /// Remaps one individual's chain id if — and only if — its full triple matches a known old
    /// offline-pool entry. Returns true when something was remapped.
    ///
    /// Idempotent by construction: after a remap the individual's chain id is in the 900000
    /// range, which no triple in <see cref="Migration"/> contains, so a second pass is a no-op.
    /// </summary>
    public static bool TryMigrate(PokemonIndividual individual)
    {
        var key = (individual.SpeciesId, individual.ChainLength, individual.EvolutionChainId);
        if (!Migration.TryGetValue(key, out var legacyChainId)) return false;

        individual.RemapEvolutionChainId(legacyChainId);
        return true;
    }
}
