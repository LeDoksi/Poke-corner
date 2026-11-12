namespace PokeCorner.App.Theme;

/// Night Diary 2.0 (D-38): Onest for text, Unbounded for titles and numbers, Caveat for margin notes.
/// All three ship inside the assembly (Assets/Fonts), so they resolve the same way in the app and in tests.
public static class BundledFont
{
    private static readonly Uri Base = MakeBase();

    public static System.Windows.Media.FontFamily Family { get; } = new(Base, "./#Onest, Segoe UI");
    public static System.Windows.Media.FontFamily Display { get; } = new(Base, "./#Unbounded, Segoe UI");

    private static Uri MakeBase()
    {
        // Touching PackUriHelper registers the pack:// scheme when no Application has done it yet.
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
        return new Uri("pack://application:,,,/PokeCorner;component/Assets/Fonts/", UriKind.Absolute);
    }
}
