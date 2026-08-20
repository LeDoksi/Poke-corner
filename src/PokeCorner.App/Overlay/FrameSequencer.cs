namespace PokeCorner.App.Overlay;

public sealed class FrameSequencer
{
    private readonly int _frameCount;
    private readonly TimeSpan _frameDuration;

    public FrameSequencer(int frameCount, TimeSpan frameDuration)
    {
        if (frameCount <= 0) throw new ArgumentOutOfRangeException(nameof(frameCount));
        _frameCount = frameCount;
        _frameDuration = frameDuration;
    }

    public int FrameIndexAt(TimeSpan elapsed)
    {
        var cycleDurationMs = _frameDuration.TotalMilliseconds * _frameCount;
        var wrappedMs = elapsed.TotalMilliseconds % cycleDurationMs;
        return (int)(wrappedMs / _frameDuration.TotalMilliseconds);
    }
}
