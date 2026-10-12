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
/// Dark enough for type labels on the paper card, while keeping each type distinct.
public static class TypeDisplay
{
    private static readonly IReadOnlyDictionary<string, Brush> Brushes = new Dictionary<string, Brush>(StringComparer.OrdinalIgnoreCase)
    {
        ["normal"] = Frozen("#5C5A43"),
        ["fire"] = Frozen("#9B3F17"),
        ["water"] = Frozen("#25548E"),
        ["electric"] = Frozen("#6B5600"),
        ["grass"] = Frozen("#2D662E"),
        ["ice"] = Frozen("#176A70"),
        ["fighting"] = Frozen("#8A3028"),
        ["poison"] = Frozen("#683B82"),
        ["ground"] = Frozen("#76511B"),
        ["flying"] = Frozen("#51478D"),
        ["psychic"] = Frozen("#99325E"),
        ["bug"] = Frozen("#526520"),
        ["rock"] = Frozen("#675132"),
        ["ghost"] = Frozen("#4B427A"),
        ["dragon"] = Frozen("#4935A0"),
        ["dark"] = Frozen("#41404A"),
        ["steel"] = Frozen("#4A5D69"),
        ["fairy"] = Frozen("#934A72")
    };

    /// Unknown type name (PokeAPI adding a 19th, or a corrupted cache entry) falls back to a
    /// neutral grey rather than throwing — a badge with the right text in the wrong colour is
    /// still useful, a crash is not.
    private static readonly Brush UnknownBrush = Frozen("#505B67");

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

    /// "electric" -> "Electric". PokeAPI's type names are single lowercase words, so this is
    /// just capitalization — kept as a method so the one-word assumption has a place to change.
    public static string GetLabel(string apiTypeName)
    {
        if (string.IsNullOrWhiteSpace(apiTypeName)) return string.Empty;
        return char.ToUpperInvariant(apiTypeName[0]) + apiTypeName[1..].ToLowerInvariant();
    }

    public static TypeBadge ToBadge(string apiTypeName) =>
        new(GetLabel(apiTypeName), GetBrush(apiTypeName));

    public static IReadOnlyList<TypeBadge> ToBadges(IReadOnlyList<string>? apiTypeNames) =>
        apiTypeNames is null
            ? Array.Empty<TypeBadge>()
            : apiTypeNames.Where(name => !string.IsNullOrWhiteSpace(name)).Select(ToBadge).ToList();
}
