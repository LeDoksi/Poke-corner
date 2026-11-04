using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Image = System.Windows.Controls.Image;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace PokeCorner.App.Overlay;

/// <summary>
/// D-30: one frame player for every pet reaction, on the desktop and in the corner. Sheets are 4×4 frames of
/// 64 px (docs/design-v2/reactions-v1, cleaned to an opaque palette; the third sulk redrawn by Claude 04.11).
/// Feeding and petting play once. A sulk is a state: enter once, hold frames 5–12 for as long as it lasts,
/// leave through its exit frames only after attention. The whole sheet is never looped, so a sulk never
/// fades by itself. Only one cloud at a time: a sulk leaves before feeding or petting plays.
/// </summary>
public sealed class ReactionPlayer
{
    public const int FrameSize = 64;

    private sealed record Clip(int[] Frames, int[] Durations);
    private sealed record Mood(Clip Enter, Clip Hold, Clip Exit, int Rest);

    private static readonly Clip Feeding = new(Range(16), [90, 90, 180, 100, 70, 110, 70, 120, 130, 100, 160, 240, 130, 110, 80, 60]);
    private static readonly Clip Petting = new(Range(16), [65, 75, 100, 110, 110, 100, 120, 140, 160, 140, 120, 120, 100, 90, 75, 60]);
    private static readonly Clip Enter = new([0, 1, 2, 3], [100, 120, 150, 180]);
    private static readonly Clip Exit = new([12, 13, 14, 15], [100, 100, 90, 70]);
    private static readonly Mood[] Moods =
    [
        new(Enter, new([4, 5, 6, 7, 8, 9, 10, 4], [5200, 220, 220, 220, 220, 220, 220, 480]), Exit, 4),
        new(Enter, new([4, 5, 6, 7, 8, 9, 10, 11], [250, 250, 250, 250, 250, 250, 250, 250]), Exit, 4),
        new(Enter, new([4, 5, 6, 7, 8, 9, 10, 4], [3200, 130, 150, 160, 160, 160, 180, 360]), Exit, 4)
    ];
    /// Frame of the feeding clip on which the crunch starts (its second bite is inside the sound file).
    public const int FeedingSoundFrame = 4;

    private static readonly Dictionary<string, BitmapSource[]> Sheets = [];

    private readonly Image _target;
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render);
    private readonly Queue<(string Sheet, int Frame, int Ms)> _steps = new();
    private Action? _then;
    private string? _once;          // the once-clip playing now
    private string? _pendingOnce;   // at most one waiting repeat; more clicks do not pile up effects
    private int _moodTarget;
    private int _moodShown;

    /// Raised when a frame is shown: (sheet id, frame index). The app starts the crunch from it.
    public event Action<string, int>? FrameShown;
    /// Raised whenever the cloud appears or disappears, so a container can re-anchor or hide other badges.
    public event Action? VisibilityChanged;

    public ReactionPlayer(Image target)
    {
        _target = target;
        _target.IsHitTestVisible = false;
        RenderOptions.SetBitmapScalingMode(_target, BitmapScalingMode.NearestNeighbor);
        _target.Visibility = Visibility.Collapsed;
        _timer.Tick += (_, _) => Next();
    }

    public int MoodStage => _moodTarget;
    public bool IsShowing => _target.Visibility == Visibility.Visible;
    /// The kind of overlay on screen now, for anchoring: "petting" or "cloud" (feeding and sulks share one anchor).
    public string AnchorKind => _once == "petting" ? "petting" : "cloud";

    private static bool Animate => SystemParameters.ClientAreaAnimation;

    public void PlayOnce(string id)
    {
        if (id is not ("feeding" or "petting")) return;
        if (_once is not null) { _pendingOnce = id; return; }
        if (_moodShown > 0 && Animate)
        {
            // The sulk leaves through its own exit frames first; clicks meanwhile wait as the one pending repeat.
            var sheet = $"neglect-{_moodShown}";
            var exit = Moods[_moodShown - 1].Exit;
            _moodShown = 0;
            _once = id;
            Run(sheet, exit, () => StartOnce(id));
            return;
        }
        _moodShown = 0;
        StartOnce(id);
    }

    /// 0 = content; 1–3 = the three sulk stages.
    public void SetMood(int stage)
    {
        stage = Math.Clamp(stage, 0, Moods.Length);
        if (stage == _moodTarget) return;
        _moodTarget = stage;
        _target.Tag = stage;
        if (_once is null) Reconcile();
    }

    private void StartOnce(string id)
    {
        _once = id;
        var clip = id == "feeding" ? Feeding : Petting;
        if (Animate) Run(id, clip, FinishOnce);
        else
        {
            // Reduced motion: one still frame that tells the story (the heart), the sound still plays.
            FrameShown?.Invoke(id, FeedingSoundFrame);
            Run(id, new([id == "feeding" ? 11 : 8], [900]), FinishOnce, raiseFrames: false);
        }
    }

    private void FinishOnce()
    {
        _once = null;
        if (_pendingOnce is { } next) { _pendingOnce = null; StartOnce(next); return; }
        Hide();
        Reconcile();
    }

    private void Reconcile()
    {
        if (_moodShown == _moodTarget) return;
        if (_moodShown > 0)
        {
            var leaving = Moods[_moodShown - 1];
            var sheet = $"neglect-{_moodShown}";
            _moodShown = 0;
            if (Animate) { Run(sheet, leaving.Exit, () => { Hide(); Reconcile(); }); return; }
            Hide();
        }
        if (_moodTarget == 0) { Hide(); return; }
        var mood = Moods[_moodTarget - 1];
        var id = $"neglect-{_moodTarget}";
        _moodShown = _moodTarget;
        if (!Animate) { _timer.Stop(); _steps.Clear(); Show(id, mood.Rest); return; } // a still cloud
        Run(id, mood.Enter, () => Hold(id, mood));
    }

    private void Hold(string id, Mood mood) => Run(id, mood.Hold, () => Hold(id, mood));

    private bool _raiseFrames = true;
    private void Run(string sheet, Clip clip, Action then, bool raiseFrames = true)
    {
        _timer.Stop();
        _steps.Clear();
        for (var i = 0; i < clip.Frames.Length; i++) _steps.Enqueue((sheet, clip.Frames[i], clip.Durations[i]));
        _then = then;
        _raiseFrames = raiseFrames;
        Next();
    }

    private void Next()
    {
        _timer.Stop();
        if (_steps.Count == 0) { var then = _then; _then = null; then?.Invoke(); return; }
        var (sheet, frame, ms) = _steps.Dequeue();
        Show(sheet, frame);
        if (_raiseFrames) FrameShown?.Invoke(sheet, frame);
        _timer.Interval = TimeSpan.FromMilliseconds(ms);
        _timer.Start();
    }

    private void Show(string sheet, int frame)
    {
        var frames = Load(sheet);
        if (frames.Length == 0) { Hide(); return; }
        _target.Source = frames[frame];
        if (_target.Visibility != Visibility.Visible) { _target.Visibility = Visibility.Visible; VisibilityChanged?.Invoke(); }
    }

    private void Hide()
    {
        _timer.Stop();
        _steps.Clear();
        if (_target.Visibility == Visibility.Collapsed) return;
        _target.Visibility = Visibility.Collapsed;
        _target.Source = null;
        VisibilityChanged?.Invoke();
    }

    public void Stop()
    {
        _once = null; _pendingOnce = null; _moodShown = 0;
        Hide();
    }

    private static BitmapSource[] Load(string sheet)
    {
        if (Sheets.TryGetValue(sheet, out var cached)) return cached;
        BitmapSource[] frames;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri($"pack://application:,,,/PokeCorner;component/Assets/Reactions/{sheet}.png");
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            frames = Enumerable.Range(0, 16).Select(i =>
            {
                var crop = new CroppedBitmap(image, new Int32Rect(i % 4 * FrameSize, i / 4 * FrameSize, FrameSize, FrameSize));
                crop.Freeze();
                return (BitmapSource)crop;
            }).ToArray();
        }
        catch (Exception ex) when (ex is System.IO.IOException or NotSupportedException or UriFormatException) { frames = []; }
        Sheets[sheet] = frames;
        return frames;
    }

    private static int[] Range(int count) => Enumerable.Range(0, count).ToArray();

    /// <summary>
    /// Where the 64-px frame goes, by the pack's universal rule (no per-species head or mouth): the top-right
    /// corner of the pet's visible silhouette. Feeding and sulks put frame point (20,48) 2 px right of it and 3 px
    /// down; petting puts (16,48) 2 px right and 18 px down. No room on the right: the overlay alone is mirrored to
    /// the top-left corner. No room on top: it slides down. <paramref name="unit"/> is one frame pixel in container units.
    /// </summary>
    public static (Point TopLeft, bool Mirrored) Place(Rect silhouette, Size container, double unit, string kind) =>
        Place(silhouette, new Rect(container), unit, kind);

    /// `allowed` is the part of the container that is actually visible (for the desktop pet: the window
    /// clipped to its monitor's work area). Right of the silhouette first, mirrored to the left when the
    /// right has no room; never clamped back over the pet.
    public static (Point TopLeft, bool Mirrored) Place(Rect silhouette, Rect allowed, double unit, string kind)
    {
        var (ax, dy) = kind == "petting" ? (16, 18) : (20, 3);
        const int ay = 48, dx = 2;
        var size = FrameSize * unit;
        var right = silhouette.Right + (dx - ax) * unit;
        var left = silhouette.Left - (dx + FrameSize - ax) * unit;
        var mirrored = right + size > allowed.Right && left >= allowed.Left;
        var x = mirrored ? left : right;
        x = Math.Clamp(x, allowed.Left, Math.Max(allowed.Left, allowed.Right - size));
        var top = Math.Clamp(silhouette.Top + (dy - ay) * unit, allowed.Top, Math.Max(allowed.Top, allowed.Bottom - size));
        return (new Point(x, top), mirrored);
    }

    /// The visible pixels of all idle frames together, in source pixels: one stable box, not one per frame.
    public static Int32Rect SilhouetteOf(IReadOnlyList<ImageSource> frames)
    {
        int left = int.MaxValue, top = int.MaxValue, right = 0, bottom = 0, width = 0, height = 0;
        foreach (var frame in frames.OfType<BitmapSource>())
        {
            var bitmap = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            width = bitmap.PixelWidth; height = bitmap.PixelHeight;
            var stride = width * 4;
            var pixels = new byte[stride * height];
            bitmap.CopyPixels(pixels, stride, 0);
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                    if (pixels[y * stride + x * 4 + 3] > 16)
                    {
                        left = Math.Min(left, x); top = Math.Min(top, y);
                        right = Math.Max(right, x + 1); bottom = Math.Max(bottom, y + 1);
                    }
        }
        return right == 0 ? new Int32Rect(0, 0, width, height) : new Int32Rect(left, top, right - left, bottom - top);
    }
}
