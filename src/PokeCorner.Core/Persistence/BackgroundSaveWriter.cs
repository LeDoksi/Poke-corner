namespace PokeCorner.Core.Persistence;

/// Moves the save file's disk write off the calling thread. The autosave runs on the growth
/// tick, whose thread owns the low-level input hooks: a disk stall there would freeze the mouse
/// cursor system-wide. Post/Flush must be called from one thread (the UI thread).
public sealed class BackgroundSaveWriter
{
    private readonly JsonSaveStore _store;
    private readonly Action<Action> _runWrite;
    private readonly object _writeLock = new();
    private long _lastPostedSequence;
    private long _lastWrittenSequence;

    /// runWrite defaults to the thread pool; tests pass a runner they control.
    public BackgroundSaveWriter(JsonSaveStore store, Action<Action>? runWrite = null)
    {
        _store = store;
        _runWrite = runWrite ?? (write => Task.Run(write));
    }

    /// Serializes now — the state objects keep changing on the UI thread — and writes later.
    public void Post(SaveData data)
    {
        var json = JsonSaveStore.Serialize(data);
        var sequence = ++_lastPostedSequence;
        _runWrite(() =>
        {
            try
            {
                Write(sequence, json);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The next autosave, five seconds later, tries again.
            }
        });
    }

    /// Synchronous write for exit and the crash handler.
    public void Flush(SaveData data) => Write(++_lastPostedSequence, JsonSaveStore.Serialize(data));

    /// Deletes the save file and drops every write still queued, permanently: after this the
    /// writer never writes again (the app is about to restart from a clean slate).
    public void Delete()
    {
        lock (_writeLock)
        {
            _lastWrittenSequence = long.MaxValue;
            _store.Delete();
        }
    }

    /// A snapshot older than the one already on disk is dropped, so a queued autosave can never
    /// land on top of a newer one or of the exit flush.
    private void Write(long sequence, string json)
    {
        lock (_writeLock)
        {
            if (sequence <= _lastWrittenSequence) return;
            _store.SaveSerialized(json);
            _lastWrittenSequence = sequence;
        }
    }
}
