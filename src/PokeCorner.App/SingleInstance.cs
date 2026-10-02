namespace PokeCorner.App;

/// One running copy per Windows session — two copies would autosave the same state.json,
/// each overwriting the other's progress. A second launch signals the running copy to show
/// itself and exits.
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activation;
    private readonly RegisteredWaitHandle _activationWait;

    /// Raised on a thread-pool thread when another launch asked this copy to show itself.
    public event Action? ActivationRequested;

    private SingleInstance(Mutex mutex, string name)
    {
        _mutex = mutex;
        _activation = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationName(name));
        _activationWait = ThreadPool.RegisterWaitForSingleObject(
            _activation, (_, _) => ActivationRequested?.Invoke(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    /// Null when another copy still holds the name after `wait`. Must be disposed on the same
    /// thread that acquired it — mutex ownership is per thread.
    public static SingleInstance? TryAcquire(string name, TimeSpan wait)
    {
        var mutex = new Mutex(false, name);
        bool owned;
        try
        {
            owned = mutex.WaitOne(wait);
        }
        catch (AbandonedMutexException)
        {
            owned = true; // the previous copy crashed without releasing; it's ours now
        }

        if (owned) return new SingleInstance(mutex, name);
        mutex.Dispose();
        return null;
    }

    public static void SignalRunningInstance(string name)
    {
        if (EventWaitHandle.TryOpenExisting(ActivationName(name), out var activation))
        {
            // Windows only lets the foreground process bring another one forward. This launch
            // is the foreground one (the user just started it), so it hands that right over
            // before signalling; without it the running copy's menu opens behind other windows.
            AllowSetForegroundWindow(AsfwAny);
            using (activation) activation.Set();
        }
    }

    private const int AsfwAny = -1;
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(int processId);

    private static string ActivationName(string name) => name + ".Activate";

    public void Dispose()
    {
        _activationWait.Unregister(null);
        _activation.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
