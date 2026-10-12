namespace PokeCorner.Core.Persistence;

/// Moves the save file's disk write off the calling thread. The autosave runs on the growth
/// tick, whose thread owns the low-level input hooks: a disk stall there would freeze the mouse
/// cursor system-wide. Post/Flush/CommitAsync must be called from one thread (the UI thread).
public sealed class BackgroundSaveWriter
{
    private readonly JsonSaveStore _store;
    private readonly Action<Action> _runWrite;
    private readonly object _writeLock = new();
    private bool _publicationPending;
    private volatile Exception? _failure;
    public event Action<Exception>? SaveFailed;
    public bool HasFailed => _failure is not null;

    private long _lastPostedSequence;
    private long _lastWrittenSequence;
    private readonly SortedDictionary<long, (string Json, TaskCompletionSource Completion)> _commits = new();

    /// runWrite defaults to the thread pool; tests pass a runner they control.
    public BackgroundSaveWriter(JsonSaveStore store, Action<Action>? runWrite = null)
    {
        _store = store;
        _runWrite = runWrite ?? (write => Task.Run(write));
    }

    /// Serializes now — the state objects keep changing on the UI thread — and writes later.
    public void Post(SaveData data)
    {
        if (_publicationPending || HasFailed) return;
        var json = JsonSaveStore.Serialize(data);
        var sequence = ++_lastPostedSequence;
        _runWrite(() =>
        {
            try
            {
                Write(sequence, json);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                // Failure is latched in Write; stale callbacks cannot retry it.
            }
        });
    }

    /// Synchronous write for exit and the crash handler.
    public void Flush(SaveData data)
    {
        if (!_publicationPending) Write(++_lastPostedSequence, JsonSaveStore.Serialize(data));
    }

    public async Task CommitAndPublishAsync(SaveData data, Action publish)
    {
        if (_publicationPending) throw new InvalidOperationException("A publication is already pending.");
        _publicationPending = true;
        try
        {
            await CommitAsync(data);
            publish();
        }
        finally { _publicationPending = false; }
    }


    /// Captures now; acknowledges only after this exact snapshot reaches disk. Later Post/Flush
    /// writes drain earlier commits first even when their thread-pool callbacks run first.
    public Task CommitAsync(SaveData data)
    {
        var json = JsonSaveStore.Serialize(data);
        var sequence = ++_lastPostedSequence;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_writeLock)
        {
            if (_lastWrittenSequence == long.MaxValue)
            {
                completion.SetCanceled();
                return completion.Task;
            }
            _commits.Add(sequence, (json, completion));
        }
        try { _runWrite(() => Write(sequence, json, commitCallback: true)); }
        catch (Exception ex)
        {
            lock (_writeLock) _commits.Remove(sequence);
            completion.TrySetException(ex);
        }
        return completion.Task;
    }

    /// Deletes the save file and drops every write still queued, permanently: after this the
    /// writer never writes again. Recoverable history remains on disk.
    public void Delete()
    {
        lock (_writeLock)
        {
            _lastWrittenSequence = long.MaxValue;
            foreach (var commit in _commits.Values) commit.Completion.TrySetCanceled();
            _commits.Clear();
            _store.Delete();
        }
    }

    public void Stop()
    {
        lock (_writeLock)
        {
            _lastWrittenSequence = long.MaxValue;
            foreach (var commit in _commits.Values) commit.Completion.TrySetCanceled();
            _commits.Clear();
        }
    }

    private void Fail(Exception ex)
    {
        _failure = ex;
        foreach (var commit in _commits.Values) commit.Completion.TrySetException(ex);
        _commits.Clear();
        SaveFailed?.Invoke(ex);
    }

    /// A snapshot older than the one already on disk is dropped, so a queued autosave can never
    /// land on top of a newer one or of the exit flush.
    private void Write(long sequence, string json, bool commitCallback = false)
    {
        lock (_writeLock)
        {
            if (_failure is not null)
            {
                if (_commits.Remove(sequence, out var failed)) failed.Completion.TrySetException(_failure);
                if (!commitCallback) throw new IOException("Запись остановлена после ошибки сохранения.", _failure);
                return;
            }
            if (commitCallback && !_commits.ContainsKey(sequence)) return;
            var isCommit = _commits.ContainsKey(sequence);
            foreach (var pending in _commits.Where(pair => pair.Key <= sequence).ToArray())
            {
                _commits.Remove(pending.Key);
                try
                {
                    _store.SaveSerialized(pending.Value.Json);
                    _lastWrittenSequence = pending.Key;
                    pending.Value.Completion.TrySetResult();
                }
                catch (Exception ex)
                {
                    pending.Value.Completion.TrySetException(ex);
                    Fail(ex);
                    if (!commitCallback) throw;
                    return;
                }
            }
            if (isCommit) return;
            if (sequence <= _lastWrittenSequence) return;
            try { _store.SaveSerialized(json); _lastWrittenSequence = sequence; }
            catch (Exception ex) { Fail(ex); throw; }
        }
    }
}
