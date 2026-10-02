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
    private string? _lastState;

    public ZOrderLog(string path) => _path = path;

    public void Append(string state, string detail, DateTime now)
    {
        if (state == _lastState) return;
        _lastState = state;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var info = new FileInfo(_path);
            if (info.Exists && info.Length > MaxBytes) info.Delete();
            File.AppendAllText(_path, $"{now:yyyy-MM-dd HH:mm:ss.fff} {state} {detail}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Diagnostics must never cost the user anything; a missed line is fine.
        }
    }
}
