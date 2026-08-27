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
/// Deliberately kept clear of the rarity tier colours (#9AA5D1 grey-blue, #4CD787 mint,
/// #5B9DFF blue, #FFB020 gold) so the two badge systems don't read as one at a glance: water
/// is a deeper blue than RarityRare, grass a leafier green than RarityUncommon, and electric a
/// truer yellow than RarityLegendary's orange-gold.
public static class TypeDisplay
{
    private static readonly IReadOnlyDictionary<string, Brush> Brushes = new Dictionary<string, Brush>(StringComparer.OrdinalIgnoreCase)
    {
        ["normal"] = Frozen("#A8A47E"),
        ["fire"] = Frozen("#F0803C"),
        ["water"] = Frozen("#3B82E8"),
        ["electric"] = Frozen("#EFD023"),
        ["grass"] = Frozen("#62B14E"),
        ["ice"] = Frozen("#6FD3D8"),
        ["fighting"] = Frozen("#C0392B"),
        ["poison"] = Frozen("#9B59B6"),
        ["ground"] = Frozen("#D6A94E"),
        ["flying"] = Frozen("#9C8FE8"),
        ["psychic"] = Frozen("#F0568F"),
        ["bug"] = Frozen("#8CAF3E"),
        ["rock"] = Frozen("#B08D57"),
        ["ghost"] = Frozen("#6A5B9E"),
        ["dragon"] = Frozen("#6C4CE0"),
        ["dark"] = Frozen("#5A5266"),
        ["steel"] = Frozen("#8E9BA8"),
        ["fairy"] = Frozen("#EE9BC0")
    };

    /// Unknown type name (PokeAPI adding a 19th, or a corrupted cache entry) falls back to a
    /// neutral grey rather than throwing — a badge with the right text in the wrong colour is
    /// still useful, a crash is not.
    private static readonly Brush UnknownBrush = Frozen("#7C8497");

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
