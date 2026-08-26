using System.Windows;
using System.Windows.Media;
using PokeCorner.Core.Domain;

// Aliased because UseWindowsForms (needed for TrayIconService's NotifyIcon) pulls
// System.Drawing into scope project-wide, making a bare "Brush" ambiguous.
using Brush = System.Windows.Media.Brush;

namespace PokeCorner.App.Popup;

/// UI-only projections of CollectionViewModel's records.
///
/// Everything the card DataTemplates bind to is pre-resolved here — the sprite as a decoded
/// ImageSource, rarity as a Brush, states as Visibility — rather than resolved through XAML
/// IValueConverters. Two reasons: sprite lookup needs a SpriteCache instance, which a
/// parameterless XAML-constructed converter can't have; and pre-resolving keeps the
/// once-per-second refresh loop from re-running converter logic for every visible card.
/// One OWNED INDIVIDUAL, rendered as a Pokédex grid card and — when that card is clicked —
/// as the contents of PokemonDetailWindow.
///
/// This is the merge of what used to be two types. The home tab carried a "Мои покемоны"
/// roster (one row per individual, with an inline Выбрать/Отпустить/star action row) and the
/// Pokédex tab carried a species grid (one card per distinct species), which meant the same
/// name, rarity and sprite were rendered twice in two parallel lists. There is now a single
/// per-individual grid — two owned Pikachus are two cards — and the actions moved off the
/// card into the detail popup. So this one model carries both the grid-facing fields and the
/// individual's identity, catch date and nature that only the popup shows.
public sealed class PokedexCard
{
    public required Guid IndividualId { get; init; }

    /// The individual's CURRENT evolution stage — what the sprite, dex number and name all
    /// show, so an evolved Charmander's card reads Charizard.
    public required int SpeciesId { get; init; }
    public required string SpeciesName { get; init; }
    public required string DexNumber { get; init; }
    public required string RarityName { get; init; }
    public required Brush RarityBrush { get; init; }
    public required double EvolutionFraction { get; init; }

    /// Popup-only detail: the roster rows used to show these inline and the species grid
    /// dropped them entirely, which is half of why the two lists felt redundant-yet-incomplete.
    public required string CaughtAtText { get; init; }
    public required string NatureText { get; init; }

    public required bool CanSelect { get; init; }
    public required string? AvailabilityToolTip { get; init; }
    public required bool IsShiny { get; init; }
    public required bool IsActive { get; init; }
    public required bool IsAvailableToActivate { get; init; }

    /// Static single frame — always assigned when the species' sprite is on disk, so a card
    /// shows real artwork immediately whether or not an animated sprite exists or has landed.
    public ImageSource? SpriteImage { get; init; }

    /// Every frame of the species' animated sprite, or null when it has none (or the GIF
    /// hasn't been fetched yet). Two or more frames is what makes a card actually animate;
    /// the cards used to render frame 0 forever while the desktop pet moved.
    public IReadOnlyList<ImageSource>? SpriteFrames { get; init; }

    public Visibility PlaceholderVisibility => SpriteImage is null ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShinyVisibility => IsShiny ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ActiveBadgeVisibility => IsActive ? Visibility.Visible : Visibility.Collapsed;
    public Visibility UnavailableBadgeVisibility => IsAvailableToActivate ? Visibility.Collapsed : Visibility.Visible;

    /// Pinned-to-the-top flag. The star glyph is a single Path drawn with both a Fill and a
    /// Stroke, so one template covers both states: gold-on-gold when pinned, hollow with a
    /// muted outline when not — no second glyph and no visibility swapping.
    public required bool IsFavorite { get; init; }

    public Visibility FavoriteBadgeVisibility => IsFavorite ? Visibility.Visible : Visibility.Collapsed;

    public Brush FavoriteFill => IsFavorite
        ? ThemeBrushes.Get("AccentGoldBrush")
        : System.Windows.Media.Brushes.Transparent;

    public Brush FavoriteStroke => IsFavorite
        ? ThemeBrushes.Get("AccentGoldBrush")
        : ThemeBrushes.Get("MutedTextBrush");

    public string FavoriteToolTip => IsFavorite
        ? "Убрать из избранного"
        : "В избранное — закрепить в начале списка";
}

public sealed class PendingEggCard
{
    public required Guid EggId { get; init; }
    public required string TierName { get; init; }
    public required Brush TierBrush { get; init; }
    public required double HatchFraction { get; init; }
    public required bool IsReady { get; init; }
    public required string StatusText { get; init; }
}

/// Theme-dictionary brush lookup by resource key.
///
/// Fully qualified as System.Windows.Application, not a bare Application — the project has
/// UseWindowsForms enabled for TrayIconService's NotifyIcon, which makes the unqualified name
/// ambiguous with System.Windows.Forms.Application.
internal static class ThemeBrushes
{
    public static Brush Get(string key) => (Brush)System.Windows.Application.Current.Resources[key];
}

/// Russian display names for the rarity tiers, plus their theme brush. Kept next to the
/// card models rather than in RarityToBrushConverter because these are card-composition
/// concerns, and the converter stays in place for any XAML that still binds through it.
public static class RarityDisplay
{
    public static string GetName(RarityTier tier) => tier switch
    {
        RarityTier.Common => "Обычный",
        RarityTier.Uncommon => "Необычный",
        RarityTier.Rare => "Редкий",
        RarityTier.Legendary => "Легендарный",
        _ => "Обычный"
    };

    public static Brush GetBrush(RarityTier tier)
    {
        var key = tier switch
        {
            RarityTier.Common => "RarityCommonBrush",
            RarityTier.Uncommon => "RarityUncommonBrush",
            RarityTier.Rare => "RarityRareBrush",
            RarityTier.Legendary => "RarityLegendaryBrush",
            _ => "RarityCommonBrush"
        };
        // Fully qualified: the project has UseWindowsForms enabled for TrayIconService's
        // NotifyIcon, which makes the unqualified "Application" type ambiguous between
        // System.Windows.Application and System.Windows.Forms.Application.
        return (Brush)System.Windows.Application.Current.Resources[key];
    }
}
