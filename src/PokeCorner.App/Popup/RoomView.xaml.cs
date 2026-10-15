using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using UserControl = System.Windows.Controls.UserControl;
using Image = System.Windows.Controls.Image;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PokeCorner.App.Imaging;
using PokeCorner.App.Overlay;
using PokeCorner.Core.Persistence;

namespace PokeCorner.App.Popup;

public sealed record RoomAward(string Id, string File, string MaskFile, double Width, double Height, double FootX, double FootY);

public sealed class RoomArt
{
    private const string RelativeRoot = "art-production/last-attempt/final-assets";
    public string Root { get; }
    public string Background { get; }
    public string Frontlip { get; }
    public (double X, double Y, double Width, double Height) FrontlipRect { get; }
    public IReadOnlyList<(double X, double Y)> Anchors { get; }
    public IReadOnlyDictionary<string, RoomAward> Awards { get; }
    public IReadOnlyList<string> Cards { get; }

    private RoomArt(string root, string background, string frontlip, (double X, double Y, double Width, double Height) frontlipRect,
        IReadOnlyList<(double X, double Y)> anchors,
        IReadOnlyDictionary<string, RoomAward> awards, IReadOnlyList<string> cards)
    {
        Root = root;
        Background = background;
        Frontlip = frontlip;
        FrontlipRect = frontlipRect;
        Anchors = anchors;
        Awards = awards;
        Cards = cards;
    }

    public static RoomArt Load(string dataRoot)
    {
        var root = Path.Combine(dataRoot, RelativeRoot);
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "manifest.json")));
        var manifest = document.RootElement;
        var room = manifest.GetProperty("room");
        var dimensions = room.GetProperty("dimensions");
        if (dimensions[0].GetInt32() != 1448 || dimensions[1].GetInt32() != 582)
            throw new InvalidDataException("Неверный размер комнаты.");
        var anchors = manifest.GetProperty("shelf").GetProperty("anchors").EnumerateArray()
            .Select(point => (point[0].GetDouble(), point[1].GetDouble())).ToArray();
        if (anchors.Length != 3) throw new InvalidDataException("Ожидалось три места полки.");
        var awards = manifest.GetProperty("trophies").EnumerateArray().ToDictionary(
            item => item.GetProperty("id").GetString()!,
            item =>
            {
                var size = item.GetProperty("logicalSize");
                var foot = item.GetProperty("footAnchor");
                return new RoomAward(item.GetProperty("id").GetString()!, item.GetProperty("file").GetString()!,
                    item.GetProperty("maskFile").GetString()!, size[0].GetDouble(), size[1].GetDouble(),
                    foot[0].GetDouble(), foot[1].GetDouble());
            });
        var cards = manifest.GetProperty("albumCards").EnumerateArray().Select(item => item.GetString()!).ToArray();
        if (awards.Count != 56 || cards.Length != 26 || cards.Any(id => !awards.ContainsKey(id)))
            throw new InvalidDataException("Неверный набор наград комнаты.");
        var lip = manifest.GetProperty("frontlip");
        var rect = lip.GetProperty("rect");
        return new RoomArt(root, room.GetProperty("file").GetString()!,
            lip.GetProperty("file").GetString()!,
            (rect[0].GetDouble(), rect[1].GetDouble(),
                rect[2].GetDouble() - rect[0].GetDouble(), rect[3].GetDouble() - rect[1].GetDouble()),
            anchors, awards, cards);
    }

    public string PathFor(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    public string? LatestEarned(SaveData save, string cardId)
    {
        if (!Awards.TryGetValue(cardId, out var first)) return null;
        if (first.Id.StartsWith("trophy-special-", StringComparison.Ordinal))
            return save.AwardLedger.ContainsKey(cardId) ? cardId : null;
        var prefix = cardId[..^1];
        return Enumerable.Range(1, 4).Reverse().Select(tier => prefix + tier)
            .FirstOrDefault(save.AwardLedger.ContainsKey);
    }

    public (double X, double Y) Position(RoomAward award, int slot) =>
        (Anchors[slot].X - award.Width * award.FootX, Anchors[slot].Y - award.Height * award.FootY);
}

public partial class RoomView : UserControl
{
    private readonly RoomArt _art;
    private readonly Func<SaveData> _snapshot;
    private readonly Func<IReadOnlyList<byte[]>?> _spriteFrames;
    private readonly Action _openAchievements;
    private readonly Func<int, string> _speciesName;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private SpriteAnimator? _animator;
    private string _sceneSignature = "";
    private string _spriteSignature = "";

    public RoomView(RoomArt art, Func<SaveData> snapshot, Func<IReadOnlyList<byte[]>?> spriteFrames, Action openAchievements,
        Func<int, string>? speciesName = null)
    {
        _art = art;
        _snapshot = snapshot;
        _spriteFrames = spriteFrames;
        _openAchievements = openAchievements;
        _speciesName = speciesName ?? (id => $"№{id:D4}");
        InitializeComponent();
        RoomBackground.Source = LoadImage(art.PathFor(art.Background));
        ShelfFrontlip.Source = LoadImage(art.PathFor(art.Frontlip));
        Canvas.SetLeft(ShelfFrontlip, art.FrontlipRect.X);
        Canvas.SetTop(ShelfFrontlip, art.FrontlipRect.Y);
        ShelfFrontlip.Width = art.FrontlipRect.Width;
        ShelfFrontlip.Height = art.FrontlipRect.Height;
        _timer.Tick += (_, _) => Refresh();
        Loaded += (_, _) => { _timer.Start(); Refresh(); _animator?.Start(); };
        Unloaded += (_, _) => { _timer.Stop(); _animator?.Stop(); _animator = null; _spriteSignature = ""; };
        Refresh();
    }

    internal static BitmapImage LoadImage(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(Path.GetFullPath(path));
        image.EndInit();
        image.Freeze();
        return image;
    }

    public void Refresh()
    {
        var data = _snapshot();
        var signature = string.Join("|", data.TrophyShelfIds);
        if (signature != _sceneSignature)
        {
            _sceneSignature = signature;
            AwardLayer.Children.Clear();
            for (var slot = 0; slot < Math.Min(3, data.TrophyShelfIds.Count); slot++)
            {
                var id = data.TrophyShelfIds[slot];
                if (id is null || !data.AwardLedger.ContainsKey(id) || !_art.Awards.TryGetValue(id, out var award)) continue;
                var image = new Image { Source = LoadImage(_art.PathFor(award.File)),
                    Width = award.Width, Height = award.Height, Stretch = Stretch.Fill,
                    ToolTip = "Награда " + id };
                var position = _art.Position(award, slot);
                Canvas.SetLeft(image, position.X);
                Canvas.SetTop(image, position.Y);
                AwardLayer.Children.Add(image);
            }
        }
        var active = data.Individuals.FirstOrDefault(p => p.Id == data.ActiveIndividualId);
        var size = data.Settings.PetSizePixels;
        PetImage.Width = PetImage.Height = size;
        Canvas.SetLeft(PetImage, 800 - size / 2.0);
        Canvas.SetTop(PetImage, 524 - size);
        PetCaption.Text = active is null ? "Выберите спутника в Покедексе." :
            (active.Nickname is null ? _speciesName(active.CurrentSpeciesId) :
                active.Nickname + " · " + _speciesName(active.CurrentSpeciesId)) +
            $" · №{active.CurrentSpeciesId:D4}" + (active.IsShiny ? " · shiny" : "");
        var frames = _spriteFrames();
        var spriteSignature = active is null || frames is null || frames.Count == 0 ? "" :
            $"{active.Id}:{active.CurrentSpeciesId}:{active.IsShiny}:{frames.Count}:{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(frames)}";
        if (spriteSignature == _spriteSignature) return;
        _spriteSignature = spriteSignature;
        _animator?.Stop();
        _animator = null;
        if (spriteSignature.Length == 0) { PetImage.Source = null; return; }
        var decoded = frames!.Select(SpriteImages.Decode).ToList();
        PetImage.Source = decoded[0];
        if (decoded.Count == 1) return;
        _animator = new SpriteAnimator(PetImage, new FrameSequencer(decoded.Count, TimeSpan.FromMilliseconds(150)), decoded);
        if (IsLoaded) _animator.Start();
    }

    private void AchievementsButton_Click(object sender, RoutedEventArgs e) => _openAchievements();
}
