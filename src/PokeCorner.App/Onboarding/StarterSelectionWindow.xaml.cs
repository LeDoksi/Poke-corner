using PokeCorner.App.Imaging;
using System.Windows;
using PokeCorner.App.Overlay;
using PokeCorner.App.Popup;
using PokeCorner.Core.Domain;

namespace PokeCorner.App.Onboarding;

public partial class StarterSelectionWindow : Window
{
    private readonly IReadOnlyList<SpeciesEvolutionChain> _candidates;
    private readonly Func<int, Task<byte[]?>> _fetchSprite;

    /// Animated-GIF frames for one species, or null when it has no animated sprite. Optional
    /// so the theme/layout tests can keep constructing this window with the static fetch
    /// alone; a null delegate simply leaves every candidate on its single static frame,
    /// which is also the runtime fallback for a species PokeAPI has no animation for.
    private readonly Func<int, Task<IReadOnlyList<byte[]>?>>? _fetchAnimatedFrames;

    /// Same idle cadence the desktop overlay and the collection hero animate at, so the
    /// starter the player picks moves at the speed they just watched it move at.
    private static readonly TimeSpan FrameDuration = TimeSpan.FromMilliseconds(150);

    /// One animator per candidate card. Held so they can all be stopped when the window
    /// closes — each owns a DispatcherTimer that would otherwise keep ticking (and keep
    /// every decoded frame alive) after the dialog is gone.
    private readonly List<SpriteAnimator> _animators = new();
    private bool _closed;

    public SpeciesEvolutionChain? Chosen { get; private set; }

    public StarterSelectionWindow(
        IReadOnlyList<SpeciesEvolutionChain> candidates,
        Func<int, Task<byte[]?>> fetchSprite,
        Func<int, Task<IReadOnlyList<byte[]>?>>? fetchAnimatedFrames = null)
    {
        InitializeComponent();
        _candidates = candidates;
        _fetchSprite = fetchSprite;
        _fetchAnimatedFrames = fetchAnimatedFrames;

        if (_candidates.Count < 2) Candidate2Panel.Visibility = Visibility.Collapsed;
        if (_candidates.Count < 3) Candidate3Panel.Visibility = Visibility.Collapsed;

        ShowRarityTags();

        Loaded += async (_, _) => await LoadSpritesAsync();
        Closed += (_, _) =>
        {
            _closed = true;
            foreach (var animator in _animators) animator.Stop();
            _animators.Clear();
        };
    }

    /// Rarity comes straight off the candidate chain's CaptureRate through the same
    /// classifier and display helpers the Pokédex and roster cards use, so the three
    /// surfaces can never disagree about what "Редкий" means or what colour it is.
    /// Set synchronously in the constructor — unlike the sprites there is nothing to await.
    private void ShowRarityTags()
    {
        var pills = new[] { Candidate1RarityPill, Candidate2RarityPill, Candidate3RarityPill };
        var dots = new[] { Candidate1RarityDot, Candidate2RarityDot, Candidate3RarityDot };
        var labels = new[] { Candidate1RarityText, Candidate2RarityText, Candidate3RarityText };

        for (var i = 0; i < _candidates.Count && i < 3; i++)
        {
            var tier = RarityClassifier.Classify(_candidates[i].CaptureRate);
            var brush = RarityDisplay.GetBrush(tier);

            pills[i].BorderBrush = brush;
            dots[i].Fill = brush;
            labels[i].Text = RarityDisplay.GetName(tier);
            labels[i].Foreground = brush;
        }
    }

    private async Task LoadSpritesAsync()
    {
        var images = new[] { Candidate1Image, Candidate2Image, Candidate3Image };
        var names = new[] { Candidate1Name, Candidate2Name, Candidate3Name };

        for (var i = 0; i < _candidates.Count && i < 3; i++)
        {
            var speciesId = _candidates[i].StarterSpeciesId;
            names[i].Text = _candidates.ResolveSpeciesName(speciesId);

            var frames = _fetchAnimatedFrames is null ? null : await _fetchAnimatedFrames(speciesId);
            if (_closed) return;
            if (frames is null || frames.Count == 0)
            {
                var bytes = await _fetchSprite(speciesId);
                if (_closed) return;
                if (bytes is not null) images[i].Source = SpriteImages.Decode(bytes);
                continue;
            }
            var decoded = frames.Select(SpriteImages.Decode).ToList();
            images[i].Source = decoded[0];
            if (decoded.Count < 2) continue;

            var animator = new SpriteAnimator(images[i], new FrameSequencer(decoded.Count, FrameDuration), decoded);
            animator.Start();
            _animators.Add(animator);
        }
    }

    private void Candidate1Button_Click(object sender, RoutedEventArgs e) => Choose(0);
    private void Candidate2Button_Click(object sender, RoutedEventArgs e) => Choose(1);
    private void Candidate3Button_Click(object sender, RoutedEventArgs e) => Choose(2);

    private void Choose(int index)
    {
        if (index >= _candidates.Count) return;
        Chosen = _candidates[index];
        DialogResult = true;
        Close();
    }

}
