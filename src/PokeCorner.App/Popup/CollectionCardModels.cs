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
public sealed class CollectionCard
{
    public required Guid IndividualId { get; init; }
    public required string SpeciesName { get; init; }
    public required string DexNumber { get; init; }
    public required string MetaLine { get; init; }
    public required string RarityName { get; init; }
    public required Brush RarityBrush { get; init; }
    public required double EvolutionFraction { get; init; }
    public required bool CanSelect { get; init; }
    public required string? AvailabilityToolTip { get; init; }
    public ImageSource? SpriteImage { get; init; }

    public Visibility PlaceholderVisibility => SpriteImage is null ? Visibility.Visible : Visibility.Collapsed;
    public required Visibility ShinyVisibility { get; init; }
    public required Visibility ActiveBadgeVisibility { get; init; }
    public required Visibility UnavailableBadgeVisibility { get; init; }
}

public sealed class PokedexCard
{
    public required int SpeciesId { get; init; }
    public required string SpeciesName { get; init; }
    public required string DexNumber { get; init; }
    public required string RarityName { get; init; }
    public required Brush RarityBrush { get; init; }
    public ImageSource? SpriteImage { get; init; }

    public Visibility PlaceholderVisibility => SpriteImage is null ? Visibility.Visible : Visibility.Collapsed;
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
