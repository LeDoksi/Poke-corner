using System.IO;
using System.Windows;
using PokeCorner.App.Popup;
using PokeCorner.Core.Domain;

namespace PokeCorner.App.Onboarding;

public partial class StarterSelectionWindow : Window
{
    private readonly IReadOnlyList<SpeciesEvolutionChain> _candidates;
    private readonly Func<int, Task<byte[]?>> _fetchSprite;

    public SpeciesEvolutionChain? Chosen { get; private set; }

    public StarterSelectionWindow(IReadOnlyList<SpeciesEvolutionChain> candidates, Func<int, Task<byte[]?>> fetchSprite)
    {
        InitializeComponent();
        _candidates = candidates;
        _fetchSprite = fetchSprite;

        if (_candidates.Count < 2) Candidate2Panel.Visibility = Visibility.Collapsed;
        if (_candidates.Count < 3) Candidate3Panel.Visibility = Visibility.Collapsed;

        Loaded += async (_, _) => await LoadSpritesAsync();
    }

    private async Task LoadSpritesAsync()
    {
        var images = new[] { Candidate1Image, Candidate2Image, Candidate3Image };
        var names = new[] { Candidate1Name, Candidate2Name, Candidate3Name };

        for (var i = 0; i < _candidates.Count && i < 3; i++)
        {
            names[i].Text = _candidates.ResolveSpeciesName(_candidates[i].StarterSpeciesId);
            var bytes = await _fetchSprite(_candidates[i].StarterSpeciesId);
            if (bytes is not null) images[i].Source = DecodePng(bytes);
        }
    }

    private void Candidate1Button_Click(object sender, RoutedEventArgs e) => Choose(0);
    private void Candidate2Button_Click(object sender, RoutedEventArgs e) => Choose(1);
    private void Candidate3Button_Click(object sender, RoutedEventArgs e) => Choose(2);

    private void Choose(int index)
    {
        if (index >= _candidates.Count) return;
        Chosen = _candidates[index];
        DialogResult = true;
        Close();
    }

    private static System.Windows.Media.ImageSource DecodePng(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
