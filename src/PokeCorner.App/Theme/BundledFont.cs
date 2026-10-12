using System.IO;

namespace PokeCorner.App.Theme;

public static class BundledFont
{
    public static System.Windows.Media.FontFamily Family { get; } = new(
        new Uri(Path.Combine(AppContext.BaseDirectory, "Data", "fonts") + Path.DirectorySeparatorChar),
        "./#Nunito");
}
