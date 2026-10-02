using System.IO;

namespace PokeCorner.App.Overlay;

/// Diagnostic trail for POKE-9 ("the pet ends up under other windows, no visible pattern").
/// Written once a second from the growth tick, but a line lands only when the pet's z-order
/// STATE changes — the detail (what was in the foreground at that moment) rides along without
/// being part of that comparison, so ordinary window switching doesn't flood the file.
public sealed class ZOrderLog
{
    /// Past this the file is simply started over: it is a "what happened lately" trail for a
    /// user to send in, not an archive.
    public const long MaxBytes = 1_048_576;

    private readonly string _path;
    private readonly Action<Action> _runWrite;
    private readonly object _fileLock = new();
    private string? _lastState;

    /// runWrite defaults to the thread pool: Append is called from the growth tick, whose
    /// thread owns the low-level input hooks, and disk work there stalls the cursor
    /// system-wide. Tests pass an inline runner.
    public ZOrderLog(string path, Action<Action>? runWrite = null)
    {
        _path = path;
        _runWrite = runWrite ?? (write => Task.Run(write));
    }

    /// Call from one thread only (the tick): the state comparison is not synchronized.
    public void Append(string state, string detail, DateTime now)
    {
        if (state == _lastState) return;
        _lastState = state;

        var line = $"{now:yyyy-MM-dd HH:mm:ss.fff} {state} {detail}{Environment.NewLine}";
        _runWrite(() => Write(line));
    }

    private void Write(string line)
    {
        lock (_fileLock) // two pool writes in flight must not trip over each other's file handle
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                var info = new FileInfo(_path);
                if (info.Exists && info.Length > MaxBytes) info.Delete();
                File.AppendAllText(_path, line);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Diagnostics must never cost the user anything; a missed line is fine.
            }
        }
    }
}
