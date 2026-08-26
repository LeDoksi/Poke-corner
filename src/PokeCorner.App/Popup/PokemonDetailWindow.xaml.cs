using System.Windows;
using PokeCorner.App.Overlay;

namespace PokeCorner.App.Popup;

/// The detail/actions view a Pokédex card opens into — see PokemonDetailWindow.xaml for why
/// the actions live here rather than inline on every card.
///
/// Deliberately dumb: it renders the PokedexCard it is handed and calls back into
/// CollectionView for anything that mutates state, so Выбрать/Отпустить/избранное go through
/// exactly the same CollectionViewModel calls the roster's inline buttons used to make.
public partial class PokemonDetailWindow : System.Windows.Window
{
    private readonly PokedexCard _card;
    private readonly Action<Guid> _onSelect;
    private readonly Action<Guid> _onRelease;
    private readonly Action<Guid> _onToggleFavorite;

    /// Local mirror of the pin so the star can flip immediately on click. The owning view
    /// rebuilds its grid from the model, but this window is holding an immutable snapshot
    /// card — re-reading it would show the stale value.
    private bool _isFavorite;

    private SpriteAnimator? _animator;

    /// Same idle cadence as the desktop overlay, the collection hero and the grid cards.
    private static readonly TimeSpan FrameDuration = TimeSpan.FromMilliseconds(150);

    public PokemonDetailWindow(PokedexCard card, Action<Guid> onSelect, Action<Guid> onRelease, Action<Guid> onToggleFavorite)
    {
        InitializeComponent();
        _card = card;
        _onSelect = onSelect;
        _onRelease = onRelease;
        _onToggleFavorite = onToggleFavorite;
        _isFavorite = card.IsFavorite;

        DexNumberText.Text = card.DexNumber;
        SpeciesNameText.Text = card.SpeciesName;
        NatureText.Text = card.NatureText;
        CaughtAtText.Text = card.CaughtAtText;
        EvolutionBar.Value = card.EvolutionFraction;

        RarityPill.BorderBrush = card.RarityBrush;
        RarityDot.Fill = card.RarityBrush;
        RarityText.Text = card.RarityName;
        RarityText.Foreground = card.RarityBrush;

        ShinyBadge.Visibility = card.ShinyVisibility;
        ActiveBadge.Visibility = card.ActiveBadgeVisibility;
        UnavailableBadge.Visibility = card.UnavailableBadgeVisibility;
        UnavailableBadge.ToolTip = card.AvailabilityToolTip;

        // Exactly the roster's rule: an already-active pet, or one that has run away in
        // Hardcore, can't be selected.
        SelectButton.IsEnabled = card.CanSelect;
        if (!card.CanSelect && card.IsActive)
        {
            SelectButton.ToolTip = "Этот покемон уже на рабочем столе";
        }
        else if (!card.CanSelect)
        {
            SelectButton.ToolTip = card.AvailabilityToolTip;
        }

        ApplyFavoriteVisuals();
        ShowSprite(card);

        Closed += (_, _) =>
        {
            _animator?.Stop();
            _animator = null;
        };
    }

    private void ShowSprite(PokedexCard card)
    {
        var frames = card.SpriteFrames;
        var hasStatic = card.SpriteImage is not null;

        SpritePlaceholder.Visibility = hasStatic || frames is { Count: > 0 }
            ? Visibility.Collapsed
            : Visibility.Visible;

        // Assigned up front so the image is never blank for the animator's first tick, and so
        // a species with no animated sprite needs no timer at all.
        SpriteImage.Source = frames is { Count: > 0 } ? frames[0] : card.SpriteImage;

        if (frames is null || frames.Count < 2) return;

        _animator = new SpriteAnimator(SpriteImage, new FrameSequencer(frames.Count, FrameDuration), frames);
        _animator.Start();
    }

    private void ApplyFavoriteVisuals()
    {
        FavoriteStar.Fill = _isFavorite
            ? ThemeBrushes.Get("AccentGoldBrush")
            : System.Windows.Media.Brushes.Transparent;
        FavoriteStar.Stroke = _isFavorite
            ? ThemeBrushes.Get("AccentGoldBrush")
            : ThemeBrushes.Get("MutedTextBrush");
        FavoriteButton.ToolTip = _isFavorite
            ? "Убрать из избранного"
            : "В избранное — закрепить в начале списка";
        FavoriteBadge.Visibility = _isFavorite ? Visibility.Visible : Visibility.Collapsed;
    }

    /// Stays open, unlike the other two: pinning is a small reversible tweak, and closing the
    /// window under the player after it would make a second thought cost another two clicks.
    private void FavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        _onToggleFavorite(_card.IndividualId);
        _isFavorite = !_isFavorite;
        ApplyFavoriteVisuals();
    }

    /// Both of these close: they change which cards exist (release) or which one is marked
    /// active (select), so the snapshot this window is rendering is stale the moment they run.
    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        _onSelect(_card.IndividualId);
        Close();
    }

    private void ReleaseButton_Click(object sender, RoutedEventArgs e)
    {
        // Releasing is permanent and the button sits one click from a card the player opened
        // just to look at, so it gets the same themed confirmation the save-reset flow uses.
        var confirmed = ConfirmDialog.Ask(
            "Отпустить покемона?",
            $"{_card.SpeciesName} будет навсегда удалён из коллекции. Это нельзя отменить.",
            "Отпустить", this);
        if (!confirmed) return;

        _onRelease(_card.IndividualId);
        Close();
    }
}
