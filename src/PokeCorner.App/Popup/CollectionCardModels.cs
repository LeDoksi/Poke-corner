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
/// ImageSource, rarity as a Brush, states as Visibility — rather than through XAML
/// IValueConverters: sprite lookup needs a SpriteCache instance, which a XAML-constructed
/// converter can't have, and pre-resolving keeps the once-a-second refresh from re-running
/// converter logic for every visible card.
/// One OWNED INDIVIDUAL (two owned Pikachus are two cards), rendered as a Pokédex grid card
/// and — when clicked — as the contents of the detail overlay.
public sealed class PokedexCard
{
    public required Guid IndividualId { get; init; }

    /// The individual's CURRENT evolution stage — what the sprite, dex number and name all
    /// show, so an evolved Charmander's card reads Charizard.
    public required int SpeciesId { get; init; }
    public required string SpeciesName { get; init; }

    /// The player's own name for this individual, or null. Never replaces the species name: the
    /// two are shown together (see DisplayName / SpeciesSubtitle below).
    public string? Nickname { get; init; }

    /// The headline: the nickname when there is one, the species otherwise. Never leaves the
    /// species name unshown on its own — SpeciesSubtitle picks it up in the nicknamed case.
    public string DisplayName => string.IsNullOrWhiteSpace(Nickname) ? SpeciesName : Nickname!;

    /// The second, smaller line under the headline. Empty (and collapsed) when there is no
    /// nickname, because the headline is already the species name then and repeating it twice
    /// would read as a rendering bug.
    public string SpeciesSubtitle => string.IsNullOrWhiteSpace(Nickname) ? string.Empty : SpeciesName;

    public Visibility SpeciesSubtitleVisibility =>
        string.IsNullOrWhiteSpace(Nickname) ? Visibility.Collapsed : Visibility.Visible;

    public required string DexNumber { get; init; }
    public required string RarityName { get; init; }
    public required Brush RarityBrush { get; init; }

    /// The current stage's PokeAPI types, as coloured pills WITH their name text. Empty when
    /// the catalog in use predates type capture, in which case the row collapses entirely
    /// rather than leaving a gap.
    public required IReadOnlyList<TypeBadge> TypeBadges { get; init; }
    public Visibility TypeBadgesVisibility => TypeBadges.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    /// The spoiler-masked evolution line for the detail overlay's chain strip. Empty for a
    /// single-stage species, which collapses the whole section.
    public required IReadOnlyList<EvolutionStageCard> EvolutionStages { get; init; }
    /// null in both terminal states — see EvolutionProgressState. The card never renders this
    /// raw; EvolutionBarValue/EvolutionLabel below are what the template binds to, so a
    /// "no further evolution" individual can't leak a bare "100%".
    public required double? EvolutionFraction { get; init; }
    public required EvolutionProgressState EvolutionState { get; init; }

    public double EvolutionBarValue => EvolutionProgressDisplay.BarValue(EvolutionFraction, EvolutionState);
    public string EvolutionLabel => EvolutionProgressDisplay.Label(EvolutionFraction, EvolutionState);
    public string EvolutionToolTip => EvolutionProgressDisplay.ToolTip(EvolutionState);
    public double EvolutionBarOpacity => EvolutionProgressDisplay.BarOpacity(EvolutionState);

    /// Detail-overlay-only fields.
    public required string CaughtAtText { get; init; }
    public required string NatureText { get; init; }

    public required bool CanSelect { get; init; }
    public required bool IsShiny { get; init; }
    public required bool IsActive { get; init; }

    /// Static single frame — always assigned when the species' sprite is on disk, so a card
    /// shows real artwork immediately whether or not an animated sprite exists or has landed.
    public ImageSource? SpriteImage { get; init; }

    /// Every frame of the species' animated sprite, or null when it has none (or the GIF hasn't
    /// been fetched yet). Two or more frames make the card animate.
    public IReadOnlyList<ImageSource>? SpriteFrames { get; init; }

    public Visibility PlaceholderVisibility => SpriteImage is null ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShinyVisibility => IsShiny ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ActiveBadgeVisibility => IsActive ? Visibility.Visible : Visibility.Collapsed;

    /// Pinned-to-the-top flag.
    public required bool IsFavorite { get; init; }

    public Visibility FavoriteBadgeVisibility => IsFavorite ? Visibility.Visible : Visibility.Collapsed;
}

/// One node of the evolution-chain strip in the detail overlay.
///
/// A stage the player has not reached arrives here already stripped of its identity by
/// CollectionViewModel — this class only decides how the mystery LOOKS. It reuses the same
/// outline-pokéball silhouette the grid cards show for "sprite not cached yet", because the
/// two mean the same thing to the eye: something is there, you just can't see it yet.
public sealed class EvolutionStageCard
{
    /// Which stage of the chain this card is, 0-based — needed so the click handler knows
    /// which stage was picked without re-deriving it from position in the ItemsControl.
    public required int StageIndex { get; init; }

    public required bool IsRevealed { get; init; }

    /// The species name for a reached stage; "???" for one that is still a mystery.
    public required string NameText { get; init; }

    /// Always null for an unreached stage — the sprite is never even looked up, so there is
    /// nothing in the visual tree to peek at.
    public ImageSource? SpriteImage { get; init; }

    public required IReadOnlyList<TypeBadge> TypeBadges { get; init; }

    /// "You are here" — the stage this individual is currently on (real progress, never
    /// affected by the display override).
    public required bool IsCurrent { get; init; }

    /// The stage cosmetically shown on the sprite/card right now. Equal to IsCurrent unless a
    /// DisplayStageOverride is set to an earlier reached stage.
    public required bool IsDisplayed { get; init; }

    /// Every node but the last draws a "→" to its right.
    public required bool ShowArrow { get; init; }

    /// Shown when there is no artwork to draw: either the stage is a mystery, or it is a
    /// revealed stage whose sprite simply isn't in the on-disk cache yet. The name row
    /// underneath is what tells those two apart ("???" vs. the real name).
    public Visibility PlaceholderVisibility => SpriteImage is null ? Visibility.Visible : Visibility.Collapsed;

    /// The "?" glyph, which marks a mystery specifically — not merely an uncached sprite.
    public Visibility MysteryVisibility => IsRevealed ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ArrowVisibility => ShowArrow ? Visibility.Visible : Visibility.Collapsed;
    public Visibility CurrentBadgeVisibility => IsCurrent ? Visibility.Visible : Visibility.Collapsed;

    /// Only shown when it says something the "Сейчас" badge doesn't — i.e. this stage is the
    /// cosmetic display but NOT the individual's real current stage. When the two coincide
    /// (the default, no override set), showing both would just repeat the same badge twice.
    public Visibility DisplayedBadgeVisibility => IsDisplayed && !IsCurrent ? Visibility.Visible : Visibility.Collapsed;

    public Visibility TypeBadgesVisibility => TypeBadges.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    /// The current stage gets the gold ring the rest of the app already uses for "this is the
    /// active one". A stage picked as the cosmetic display (but not the real current one) gets
    /// a distinct blue ring — reusing RarityRareBrush rather than inventing a new theme color.
    /// Unreached stages fade back so the strip reads left-to-right as progress.
    public Brush BorderBrush => IsCurrent
        ? ThemeBrushes.Get("AccentGoldBrush")
        : IsDisplayed
            ? ThemeBrushes.Get("RarityRareBrush")
            : ThemeBrushes.Get("CardBorderBrush");

    public double Opacity => IsRevealed ? 1.0 : 0.55;

    public string ToolTip => IsRevealed
        ? (IsCurrent ? $"{NameText} — текущая форма этого покемона"
            : IsDisplayed ? $"{NameText} — сейчас отображается вместо текущей формы. Нажми, чтобы вернуть текущую форму"
            : $"{NameText} — нажми, чтобы отображать именно эту форму")
        : "Ещё не открыто — продолжай растить питомца";
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
