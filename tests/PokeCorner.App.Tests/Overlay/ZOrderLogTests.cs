using System.IO;
using PokeCorner.App.Overlay;
using Xunit;

namespace PokeCorner.App.Tests.Overlay;

public class ZOrderLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "PokeCornerZOrderLogTests_" + Guid.NewGuid());
    private string LogPath => Path.Combine(_dir, "zorder.log");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Append_SameStateTwice_WritesOnce()
    {
        var log = new ZOrderLog(LogPath);
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
        var log = new ZOrderLog(LogPath);

        log.Append("topmost=True covered=[]", "fg=x", new DateTime(2026, 10, 2));

        Assert.Single(File.ReadAllLines(LogPath));
    }

    [Fact]
    public void Append_DirectoryMissing_CreatesIt()
    {
        new ZOrderLog(LogPath).Append("s", "d", new DateTime(2026, 10, 2));

        Assert.True(File.Exists(LogPath));
    }
}
