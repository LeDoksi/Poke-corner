using PokeCorner.Core.Persistence;

namespace PokeCorner.Core.Tests.Persistence;

public class BackgroundSaveWriterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "PokeCornerSaveWriterTests_" + Guid.NewGuid());
    private string SavePath => Path.Combine(_dir, "state.json");
    private readonly List<Action> _pending = new();

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private BackgroundSaveWriter MakeWriter() => new(new JsonSaveStore(SavePath), runWrite: _pending.Add);

    private static SaveData Snapshot(int coins) => new() { Individuals = new(), Settings = new AppSettings(), ShopCoins = coins };

    private int CoinsOnDisk() => new JsonSaveStore(SavePath).Load()!.ShopCoins;

    /// The autosave is posted from the growth tick, whose thread owns the low-level input hooks;
    /// a disk stall there freezes the cursor system-wide, so the write must not happen inline.
    [Fact]
    public void Post_WritesThroughTheRunner_NotInline()
    {
        MakeWriter().Post(Snapshot(1));

        Assert.False(File.Exists(SavePath));
        Assert.Single(_pending);
        _pending[0]();
        Assert.Equal(1, CoinsOnDisk());
    }

    [Fact]
    public void Post_OlderSnapshotRunLast_DoesNotOverwriteNewer()
    {
        var writer = MakeWriter();
        writer.Post(Snapshot(1));
        writer.Post(Snapshot(2));

        _pending[1]();
        _pending[0]();

        Assert.Equal(2, CoinsOnDisk());
    }

    /// Exit flushes synchronously; an autosave still queued from a moment earlier must not land
    /// on top of it afterwards.
    [Fact]
    public void Flush_AfterPendingPost_LeavesFlushedData()
    {
        var writer = MakeWriter();
        writer.Post(Snapshot(1));

        writer.Flush(Snapshot(2));
        _pending[0]();

        Assert.Equal(2, CoinsOnDisk());
    }

    /// Post serializes immediately: the live objects keep changing on the UI thread while the
    /// write waits in the pool, and the file must hold the state as of the Post call.
    [Fact]
    public void Post_SnapshotsAtCallTime()
    {
        var writer = MakeWriter();
        var settings = new AppSettings { PetSizePixels = 64 };
        writer.Post(new SaveData { Individuals = new(), Settings = settings });

        settings.PetSizePixels = 200;
        _pending[0]();

        Assert.Equal(64, new JsonSaveStore(SavePath).Load()!.Settings.PetSizePixels);
    }

    /// "Reset save data" deletes the file; an autosave queued a moment earlier must not write
    /// it straight back, or the relaunched app finds the old collection.
    [Fact]
    public void Delete_CancelsQueuedWrites()
    {
        var writer = MakeWriter();
        writer.Flush(Snapshot(1));
        writer.Post(Snapshot(2));

        writer.Delete();
        _pending[0]();

        Assert.False(File.Exists(SavePath));
    }
}
