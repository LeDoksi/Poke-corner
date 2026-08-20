using System.Drawing;
using System.Drawing.Imaging;

namespace PokeCorner.Core.PokeApi;

public static class GifFrameDecoder
{
    public static IReadOnlyList<byte[]> DecodeFramesAsPng(byte[] gifBytes)
    {
        using var sourceStream = new MemoryStream(gifBytes);
        using var image = Image.FromStream(sourceStream);
        var dimension = new FrameDimension(image.FrameDimensionsList[0]);
        var frameCount = image.GetFrameCount(dimension);

        var frames = new List<byte[]>(frameCount);
        for (var i = 0; i < frameCount; i++)
        {
            image.SelectActiveFrame(dimension, i);
            using var frameStream = new MemoryStream();
            using var frameBitmap = new Bitmap(image);
            frameBitmap.Save(frameStream, ImageFormat.Png);
            frames.Add(frameStream.ToArray());
        }
        return frames;
    }
}
