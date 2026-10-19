using System.IO;
using System.Text.Json;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.App.Popup;

// Presentation-only index. Service Preview remains the reward/eligibility authority.
internal sealed class ExpeditionTeamSelector
{
    private readonly CatalogPackage? _catalog;
    private readonly Dictionary<int, CatalogSpecies> _species;
    private readonly Dictionary<string, string[]> _routeTypes = [];
    private readonly LevelProgression? _levels;
    private readonly int[] _recommended = [1, 10, 25];
    internal int SecondTraitLevel { get; } = 10;
    internal static readonly string[] TraitIds = ["gatherer", "treasure-hunter", "tracker", "finder", "mentor", "collector", "relic-finder", "terrain-expert", "fast-grower", "rarity-expert", "caring-parent"];
    internal ExpeditionTeamSelector(CatalogPackage? catalog)
    {
        _catalog = catalog; _species = catalog?.Species.ToDictionary(s => s.Id) ?? [];
        if (catalog is null) return;
        _levels = new LevelProgression(catalog.DataRoot);
        using var rules = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.DataRoot, "final", "catalog-rules.json")));
        foreach (var route in rules.RootElement.GetProperty("routes").EnumerateArray())
            _routeTypes[route.GetProperty("id").GetString()!] = route.GetProperty("bonusTypes").EnumerateArray().Select(t => t.GetString()!).ToArray();
        using var balance = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(catalog.DataRoot, "final", "balance.json")));
        _recommended = balance.RootElement.GetProperty("recommendedLevels").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        SecondTraitLevel = balance.RootElement.GetProperty("secondTraitLevel").GetInt32();
    }
    internal string Name(PokemonIndividual pet) => _species.GetValueOrDefault(pet.CurrentSpeciesId)?.DisplayName ?? $"№{pet.CurrentSpeciesId:D4}";
    internal int Level(PokemonIndividual pet) => _levels?.LevelForXp(pet.TotalXp) ?? 1;
    internal string[] Types(PokemonIndividual pet) => _species.GetValueOrDefault(pet.CurrentSpeciesId)?.Types.ToArray() ?? [];
    internal int Recommended(string difficulty) => _recommended[difficulty == "hard" ? 2 : difficulty == "normal" ? 1 : 0];
    internal bool Matches(PokemonIndividual pet, string route) => Types(pet).Intersect(_routeTypes.GetValueOrDefault(route) ?? []).Any();
    internal string RouteHint(string route, string difficulty) => "Подходят типы: " + string.Join(", ", (_routeTypes.GetValueOrDefault(route) ?? []).Select(TypeName)) + $" · рекомендуемый уровень {Recommended(difficulty)}";
    internal string? Unavailable(PokemonIndividual pet, SaveData state)
    {
        if (state.ActiveIndividualId == pet.Id) return "На рабочем столе";
        if (state.Expedition?.Status is "running" or "ready" && state.Expedition.ParticipantIds.Contains(pet.Id)) return "В походе";
        if (state.Nest?.Status is "running" or "ready" && state.Nest.ParticipantIds.Contains(pet.Id)) return "В питомнике";
        if (_catalog is not null && !_catalog.CanObtain(pet.CurrentSpeciesId, pet.IsShiny)) return "Изображение этой формы пока недоступно";
        return null;
    }
    internal string Traits(PokemonIndividual pet) => string.Join(" · ", new[] { pet.FirstTraitId, Level(pet) >= SecondTraitLevel ? pet.SecondTraitId : null }
        .Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => CollectionView.TraitLabel(t) + " — " + CollectionView.TraitEffect(t)))
        + (Level(pet) < SecondTraitLevel ? $" · Вторая особенность с уровня {SecondTraitLevel}" : "");
    internal IReadOnlyList<PokemonIndividual> Query(SaveData data, string route, string difficulty, string search,
        string? type, string? trait, bool available, bool matchesRoute, int sort)
    {
        var q = search.Trim(); var busy = ExpeditionService.BusyIndividualIds(data);
        bool Available(PokemonIndividual p) => p.Id != data.ActiveIndividualId && !busy.Contains(p.Id) && (_catalog?.CanObtain(p.CurrentSpeciesId, p.IsShiny) ?? true);
        var rows = data.Individuals.Where(p => (!available || Available(p)) && (!matchesRoute || Matches(p, route)) &&
            (string.IsNullOrEmpty(type) || Types(p).Contains(type)) &&
            (string.IsNullOrEmpty(trait) || p.FirstTraitId == trait || (Level(p) >= SecondTraitLevel && p.SecondTraitId == trait)) &&
            (q.Length == 0 || Name(p).Contains(q, StringComparison.CurrentCultureIgnoreCase) ||
             (p.Nickname?.Contains(q, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
             p.CurrentSpeciesId.ToString().Contains(q.TrimStart('#', '№', '0'))));
        return (sort == 1 ? rows.OrderByDescending(Level).ThenBy(p => p.CurrentSpeciesId) :
            sort == 2 ? rows.OrderBy(p => p.CurrentSpeciesId).ThenBy(p => p.CaughtAtUtc) :
            rows.OrderByDescending(Available).ThenByDescending(p => Matches(p, route)).ThenByDescending(p => Level(p) >= Recommended(difficulty))
                .ThenByDescending(Level).ThenBy(p => p.CurrentSpeciesId)).ToArray();
    }
    internal static string TypeName(string type) => type switch {
        "normal" => "Обычный", "fire" => "Огненный", "water" => "Водный", "grass" => "Травяной", "electric" => "Электрический",
        "ice" => "Ледяной", "fighting" => "Боевой", "poison" => "Ядовитый", "ground" => "Земляной", "flying" => "Летающий",
        "psychic" => "Психический", "bug" => "Насекомый", "rock" => "Каменный", "ghost" => "Призрачный", "dragon" => "Драконий",
        "dark" => "Тёмный", "steel" => "Стальной", "fairy" => "Волшебный", _ => type };
}
