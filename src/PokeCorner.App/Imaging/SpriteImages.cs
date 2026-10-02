using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PokeCorner.App.Imaging;

public static class SpriteImages
{
    /// Decodes PNG bytes into a frozen bitmap. Frozen is what makes the result safe to build
    /// on a worker thread and hand to the UI thread.
    public static ImageSource Decode(byte[] png)
    {
        using var stream = new MemoryStream(png);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad; // read everything now, so the stream can be disposed
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
