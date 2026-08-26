// src/PokeCorner.App/Popup/CatchView.xaml.cs
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PokeCorner.Core;

namespace PokeCorner.App.Popup;

public partial class CatchView : System.Windows.Controls.UserControl
{
    private readonly AppState _appState;
    private readonly Func<CatchReveal?> _tryPerformCatch;
    private readonly Func<int, Task<byte[]?>> _fetchSprite;
    private readonly Func<bool> _isCatchReady;
    private readonly Action _onStateChanged;

    /// Species id -> display name, resolved against the live species catalog by the caller.
    /// Replaces the retired hand-translated SpeciesNames dictionary; this view has no catalog
    /// of its own, so the lookup is injected rather than reached for.
    private readonly Func<int, string> _resolveSpeciesName;
    private DispatcherTimer? _liveRefreshTimer;

    public CatchView(AppState appState, Func<CatchReveal?> tryPerformCatch, Func<int, Task<byte[]?>> fetchSprite, Func<bool> isCatchReady, Action onStateChanged, Func<int, string> resolveSpeciesName)
    {
        InitializeComponent();
        _appState = appState;
        _tryPerformCatch = tryPerformCatch;
        _fetchSprite = fetchSprite;
        _isCatchReady = isCatchReady;
        _onStateChanged = onStateChanged;
        _resolveSpeciesName = resolveSpeciesName;
        Loaded += (_, _) => StartLiveRefresh();
        Unloaded += (_, _) => StopLiveRefresh();
        Refresh();
    }

    private void StartLiveRefresh()
    {
        _liveRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _liveRefreshTimer.Tick += (_, _) => Refresh();
        _liveRefreshTimer.Start();
    }

    private void StopLiveRefresh()
    {
        _liveRefreshTimer?.Stop();
        _liveRefreshTimer = null;
    }

    public void Refresh()
    {
        var progress = ProgressCalculator.Compute(_appState.Activity, _appState.Collection.GetActive(), _appState.Feeding);
        CatchProgressBar.Value = progress.CatchFraction;
        SetReady(_isCatchReady());
    }

    private void SetReady(bool ready)
    {
        CatchButton.IsEnabled = ready;
        StatusText.Text = ready ? "Готов новый покемон! Жми «Поймать!»." : "Копи активность за ПК — полоса поимки ещё не заполнилась.";
    }

    private async void CatchButton_Click(object sender, RoutedEventArgs e)
    {
        var reveal = _tryPerformCatch();
        if (reveal is null)
        {
            StatusText.Text = "Пока не готово — играй ещё.";
            return;
        }
        _onStateChanged();
        await PlayReveal(reveal);
    }

    public async Task PlayReveal(CatchReveal reveal)
    {
        SetReady(false);
        RevealSpriteImage.Opacity = 0;
        RevealNameText.Opacity = 0;
        RevealRarityText.Opacity = 0;

        var shake = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(900) };
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(-15, KeyTime.FromPercent(0.15)));
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(15, KeyTime.FromPercent(0.35)));
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(-15, KeyTime.FromPercent(0.55)));
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(15, KeyTime.FromPercent(0.75)));
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1.0)));

        var tcs = new TaskCompletionSource();
        shake.Completed += (_, _) => tcs.SetResult();
        PokeBallRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, shake);
        await tcs.Task;

        var spriteBytes = await _fetchSprite(reveal.Result.SpeciesId);
        if (spriteBytes is not null)
        {
            using var stream = new MemoryStream(spriteBytes);
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            RevealSpriteImage.Source = bitmap;
        }

        RevealNameText.Text = _resolveSpeciesName(reveal.Result.SpeciesId) + (reveal.Result.IsShiny ? " ✨" : "");
        RevealRarityText.Text = reveal.Rarity + (reveal.Result.WasDuplicateSpecies ? " · уже был в коллекции" : " · новый вид!");

        // The reveal block starts Collapsed (not merely Opacity=0) so the idle screen has no
        // ~140px hole between the pokéball and the catch button. It takes up layout space only
        // from here on, which reads as the reveal "arriving" rather than as a permanent gap.
        RevealSpriteImage.Visibility = Visibility.Visible;
        RevealNameText.Visibility = Visibility.Visible;
        RevealRarityText.Visibility = Visibility.Visible;

        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(400));
        RevealSpriteImage.BeginAnimation(OpacityProperty, fadeIn);
        RevealNameText.BeginAnimation(OpacityProperty, fadeIn);
        RevealRarityText.BeginAnimation(OpacityProperty, fadeIn);

        StatusText.Text = "Загляни в Коллекцию, чтобы посмотреть на нового покемона.";
    }
}
