using System.Drawing;
using System.Drawing.Imaging;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.Core.Tests.PokeApi;

[System.Runtime.Versioning.SupportedOSPlatform("windows6.1")]
public class GifFrameDecoderTests
{
    private static byte[] MakeSingleFrameGif()
    {
        using var stream = new MemoryStream();
        using var frame0 = new Bitmap(4, 4);
        frame0.Save(stream, ImageFormat.Gif);
        return stream.ToArray();
    }

    [Fact]
    public void DecodeFramesAsPng_SingleFrameGif_ReturnsOneFrame()
    {
        var gifBytes = MakeSingleFrameGif();

        var frames = GifFrameDecoder.DecodeFramesAsPng(gifBytes);

        Assert.Single(frames);
        Assert.All(frames, f => Assert.True(f.Length > 0));
    }
}
