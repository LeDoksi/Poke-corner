using System.IO;
using PokeCorner.App.Overlay;
using Xunit;

namespace PokeCorner.App.Tests.Overlay;

public class ZOrderLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "PokeCornerZOrderLogTests_" + Guid.NewGuid());
    private string LogPath => Path.Combine(_dir, "zorder.log");
    private static void Inline(Action write) => write();

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Append_SameStateTwice_WritesOnce()
    {
        var log = new ZOrderLog(LogPath, Inline);
        var now = new DateTime(2026, 10, 2, 9, 0, 0);

        log.Append("topmost=True covered=[]", "fg=Notepad(notepad)", now);
        log.Append("topmost=True covered=[]", "fg=Chrome(chrome)", now.AddSeconds(1)); // only the detail changed
        log.Append("topmost=False covered=[Chrome(chrome)]", "fg=Chrome(chrome)", now.AddSeconds(2));

        var lines = File.ReadAllLines(LogPath);
        Assert.Equal(2, lines.Length);
        Assert.Equal("2026-10-02 09:00:00.000 topmost=True covered=[] fg=Notepad(notepad)", lines[0]);
        Assert.StartsWith("2026-10-02 09:00:02.000 topmost=False", lines[1]);
    }

    [Fact]
    public void Append_FileOver1Mb_RestartsFile()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(LogPath, new string('x', (int)ZOrderLog.MaxBytes + 1));
        var log = new ZOrderLog(LogPath, Inline);

        log.Append("topmost=True covered=[]", "fg=x", new DateTime(2026, 10, 2));

        Assert.Single(File.ReadAllLines(LogPath));
    }

    /// Disk work is banned on the growth tick's thread (it owns the low-level input hooks;
    /// stalling it freezes the cursor system-wide), so the write must go through the runner,
    /// not happen inline in Append.
    [Fact]
    public void Append_WritesOnlyThroughTheRunner()
    {
        Action? pending = null;
        var log = new ZOrderLog(LogPath, runWrite: write => pending = write);

        log.Append("topmost=True covered=[]", "fg=x", new DateTime(2026, 10, 2));

        Assert.False(File.Exists(LogPath));
        Assert.NotNull(pending);
        pending!();
        Assert.Single(File.ReadAllLines(LogPath));
    }

    [Fact]
    public void Append_DirectoryMissing_CreatesIt()
    {
        new ZOrderLog(LogPath, Inline).Append("s", "d", new DateTime(2026, 10, 2));

        Assert.True(File.Exists(LogPath));
    }
}
