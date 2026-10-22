using System.Windows.Media;

// Aliased because UseWindowsForms (needed for TrayIconService's NotifyIcon) pulls
// System.Drawing into scope project-wide, making a bare "Brush"/"Color" ambiguous.
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace PokeCorner.App.Popup;

/// One rendered type badge: the label the player reads and the colour it is drawn in.
///
/// The label is always present and always shown. A colour-only swatch was considered and
/// rejected — rarity can get away with a bare dot because there are four tiers a player learns
/// in a session, but there are 18 types and no one infers "Rock" from a shade of tan.
public sealed record TypeBadge(string Label, Brush Brush);

/// Display data for PokeAPI's 18 types: a presentable label and a distinguishable colour per
/// type name.
///
/// Colours live here as frozen brushes rather than as 18 more entries in PokedexTheme.xaml.
/// The theme dictionary holds tokens the app's own visual language is built from (surfaces,
/// text, the four rarity tiers); this is a fixed external vocabulary that maps 1:1 onto
/// PokeAPI's own type list and changes only if PokeAPI adds a type.
///
/// Light enough for type labels on the Night Diary card, while keeping each type distinct.
public static class TypeDisplay
{
    private static readonly IReadOnlyDictionary<string, Brush> Brushes = new Dictionary<string, Brush>(StringComparer.OrdinalIgnoreCase)
    {
        ["normal"] = Frozen("#C9C4A6"),
        ["fire"] = Frozen("#FF9E6E"),
        ["water"] = Frozen("#86B5F2"),
        ["electric"] = Frozen("#F2D35E"),
        ["grass"] = Frozen("#8ED68F"),
        ["ice"] = Frozen("#7FD8DE"),
        ["fighting"] = Frozen("#F08E85"),
        ["poison"] = Frozen("#CF9BEB"),
        ["ground"] = Frozen("#E0B27A"),
        ["flying"] = Frozen("#AFA6F0"),
        ["psychic"] = Frozen("#F48DB8"),
        ["bug"] = Frozen("#C2D872"),
        ["rock"] = Frozen("#D2B48C"),
        ["ghost"] = Frozen("#A99BDE"),
        ["dragon"] = Frozen("#9F8CF5"),
        ["dark"] = Frozen("#B3B0C2"),
        ["steel"] = Frozen("#A9C1CF"),
        ["fairy"] = Frozen("#F2A6CF")
    };

    /// Unknown type name (PokeAPI adding a 19th, or a corrupted cache entry) falls back to a
    /// neutral grey rather than throwing — a badge with the right text in the wrong colour is
    /// still useful, a crash is not.
    private static readonly Brush UnknownBrush = Frozen("#B0B8C2");

    private static Brush Frozen(string hex)
    {
        // Fully qualified: UseWindowsForms (for TrayIconService's NotifyIcon) makes a bare
        // ColorConverter ambiguous with System.Drawing.ColorConverter project-wide.
        var brush = new SolidColorBrush((Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
        brush.Freeze(); // shared across every card; frozen brushes are cross-thread safe and cheap
        return brush;
    }

    public static Brush GetBrush(string apiTypeName) =>
        Brushes.TryGetValue(apiTypeName ?? string.Empty, out var brush) ? brush : UnknownBrush;

    /// Canonical API ids stay in data; labels match the Russian Pokédex controls.
    public static string GetLabel(string apiTypeName)
    {
        if (string.IsNullOrWhiteSpace(apiTypeName)) return string.Empty;
        return apiTypeName.ToLowerInvariant() switch
        {
            "normal" => "Нормальный", "fire" => "Огонь", "water" => "Вода",
            "electric" => "Электро", "grass" => "Трава", "ice" => "Лёд",
            "fighting" => "Боевой", "poison" => "Яд", "ground" => "Земля",
            "flying" => "Летающий", "psychic" => "Психический", "bug" => "Жук",
            "rock" => "Камень", "ghost" => "Призрак", "dragon" => "Дракон",
            "dark" => "Тьма", "steel" => "Сталь", "fairy" => "Фея",
            _ => char.ToUpperInvariant(apiTypeName[0]) + apiTypeName[1..].ToLowerInvariant()
        };
    }

    public static TypeBadge ToBadge(string apiTypeName) =>
        new(GetLabel(apiTypeName), GetBrush(apiTypeName));

    public static IReadOnlyList<TypeBadge> ToBadges(IReadOnlyList<string>? apiTypeNames) =>
        apiTypeNames is null
            ? Array.Empty<TypeBadge>()
            : apiTypeNames.Where(name => !string.IsNullOrWhiteSpace(name)).Select(ToBadge).ToList();
}
