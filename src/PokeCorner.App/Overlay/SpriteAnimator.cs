using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace PokeCorner.App.Overlay;

public sealed class SpriteAnimator
{
    private readonly Image _target;
    private readonly FrameSequencer _sequencer;
    private readonly IReadOnlyList<ImageSource> _frames;
    private readonly DispatcherTimer _timer;
    private readonly System.Diagnostics.Stopwatch _stopwatch = new();

    public SpriteAnimator(Image target, FrameSequencer sequencer, IReadOnlyList<ImageSource> frames)
    {
        _target = target;
        _sequencer = sequencer;
        _frames = frames;
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => Tick();
    }

    public void Start()
    {
        _stopwatch.Restart();
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        _stopwatch.Stop();
    }

    private void Tick()
    {
        var frameIndex = _sequencer.FrameIndexAt(_stopwatch.Elapsed);
        _target.Source = _frames[frameIndex];
    }
}
