namespace PokeCorner.App.Tests;

public class SingleInstanceTests
{
    private static string UniqueName() => @"Local\PokeCorner.Tests." + Guid.NewGuid().ToString("N");

    /// Mutex ownership is per thread, so "another instance" is simulated by a thread that
    /// acquires and keeps holding until told to let go.
    private static (Thread thread, ManualResetEventSlim release) HoldOnAnotherThread(string name)
    {
        var acquired = new ManualResetEventSlim();
        var release = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            using var held = SingleInstance.TryAcquire(name, TimeSpan.Zero)!;
            acquired.Set();
            release.Wait();
        });
        thread.Start();
        Assert.True(acquired.Wait(TimeSpan.FromSeconds(5)));
        return (thread, release);
    }

    [Fact]
    public void TryAcquire_WhileHeldElsewhere_ReturnsNull()
    {
        var name = UniqueName();
        var (thread, release) = HoldOnAnotherThread(name);

        Assert.Null(SingleInstance.TryAcquire(name, TimeSpan.Zero));

        release.Set();
        thread.Join();
    }

    [Fact]
    public void TryAcquire_AfterRelease_Succeeds()
    {
        var name = UniqueName();
        var (thread, release) = HoldOnAnotherThread(name);
        release.Set();
        thread.Join();

        using var instance = SingleInstance.TryAcquire(name, TimeSpan.Zero);
        Assert.NotNull(instance);
    }

    /// The restart after "reset save data" launches the new process while the old one is still
    /// shutting down — it must wait for the old one instead of giving up immediately.
    [Fact]
    public void TryAcquire_WithWait_SucceedsOnceTheHolderLetsGo()
    {
        var name = UniqueName();
        var (thread, release) = HoldOnAnotherThread(name);
        _ = Task.Delay(300).ContinueWith(_ => release.Set());

        using var instance = SingleInstance.TryAcquire(name, TimeSpan.FromSeconds(5));

        Assert.NotNull(instance);
        thread.Join();
    }

    [Fact]
    public void SignalRunningInstance_RaisesActivationRequestedInTheOwner()
    {
        var name = UniqueName();
        using var owner = SingleInstance.TryAcquire(name, TimeSpan.Zero)!;
        var raised = new ManualResetEventSlim();
        owner.ActivationRequested += raised.Set;

        SingleInstance.SignalRunningInstance(name);

        Assert.True(raised.Wait(TimeSpan.FromSeconds(5)));
    }
}
