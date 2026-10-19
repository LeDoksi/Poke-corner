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
using PokeCorner.Core.Domain;
using PokeCorner.Core.PokeApi;
using PokeCorner.Core.Activity;

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

    public string DataRoot => Path.GetFullPath(Path.Combine(Root, "..", "..", ".."));

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
    private readonly CatalogPackage? _catalog;
    private readonly LevelProgression? _levels;
    private readonly TraitRules? _careTraits;
    private readonly Action? _pet;
    private readonly Action? _feed;
    private readonly Action<Guid>? _openIndividual;
    private readonly Action? _openPokedex;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private SpriteAnimator? _animator;
    private string _sceneSignature = "";
    private string _spriteSignature = "";
    private (int Width, int Height, int Left, int Right, int Bottom) _spriteBounds;

    public RoomView(RoomArt art, Func<SaveData> snapshot, Func<IReadOnlyList<byte[]>?> spriteFrames, Action openAchievements,
        Func<int, string>? speciesName = null, CatalogPackage? catalog = null,
        Action? pet = null, Action? feed = null, Action<Guid>? openIndividual = null, Action? openPokedex = null)
    {
        _art = art;
        _snapshot = snapshot;
        _spriteFrames = spriteFrames;
        _openAchievements = openAchievements;
        _speciesName = speciesName ?? (id => $"№{id:D4}");
        _catalog = catalog;
        _levels = catalog is null ? null : new LevelProgression(catalog.DataRoot);
        _careTraits = catalog is null ? null : new TraitRules(catalog.DataRoot);
        _pet = pet;
        _feed = feed;
        _openIndividual = openIndividual;
        _openPokedex = openPokedex;
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
                    ToolTip = "Заработанная награда" };
                var position = _art.Position(award, slot);
                Canvas.SetLeft(image, position.X);
                Canvas.SetTop(image, position.Y);
                AwardLayer.Children.Add(image);
            }
        }
        var active = data.Individuals.FirstOrDefault(p => p.Id == data.ActiveIndividualId);
        PetCaption.Text = active is null ? "Выберите спутника в Покедексе." :
            (active.Nickname is null ? _speciesName(active.CurrentSpeciesId) :
                active.Nickname + " · " + _speciesName(active.CurrentSpeciesId)) +
            $" · №{active.CurrentSpeciesId:D4}" + (active.IsShiny ? " · shiny" : "");
        var species = active is null ? null : _catalog?.Species.FirstOrDefault(s => s.Id == active.CurrentSpeciesId);
        if (active is not null)
        {
            var level = _levels?.LevelForXp(active.TotalXp);
            PetCaption.Text += (species is null ? "" : " · " + string.Join(" · ", species.Types.Select(TypeDisplay.GetLabel)) +
                " · " + (Enum.TryParse<RarityTier>(species.Rarity, out var tier) ? RarityDisplay.GetName(tier) : species.Rarity)) +
                (level is null ? "" : $" · Ур. {level}");
            var required = _catalog is null ? EvolutionCalculator.RequiredCareForNext(active.CurrentStage)
                : EvolutionCalculator.RequiredCareForNext(active, _careTraits!, _levels!, _catalog);
            CareText.Text = active.ChainLength <= 1 ? EvolutionProgressDisplay.NeverEvolvesLabel :
                (int)active.CurrentStage >= active.ChainLength - 1 ? EvolutionProgressDisplay.FinalFormLabel :
                active.AccumulatedCarePoints >= required
                    ? "Уход накоплен · условия эволюции в карточке" : "Проводите время вместе и кормите питомца";
            var previous = active.CurrentStage != EvolutionStage.Stage2 ? 0 : _catalog is null ? EvolutionThresholds.Stage2Threshold
                : EvolutionCalculator.RequiredCareForNext(active, _careTraits!, _levels!, _catalog, EvolutionStage.Stage1);
            CareBar.Value = active.ChainLength <= 1 ? 0 : (int)active.CurrentStage >= active.ChainLength - 1 ? 1 :
                Math.Clamp((double)(active.AccumulatedCarePoints - previous) /
                    (required - previous), 0, 1);
            XpText.Text = level is null ? $"{active.TotalXp} XP" : level >= 40 ? $"Ур. {level} · максимум опыта" :
                $"Ур. {level} · {active.TotalXp} / {_levels!.XpForLevel(level.Value + 1)} XP";
        }
        else
        {
            CareText.Text = "Выберите спутника";
            CareBar.Value = 0;
            XpText.Text = "Выберите питомца в Покедексе";
        }
        XpText.Text += data.PendingCatchCount > 0 ? $" · Покеболов: {data.PendingCatchCount}" : " · За активность";
        CatchBar.Value = Math.Clamp((double)data.CatchAccumulatorUnits / ActivityAccumulator.CatchCheckIntervalUnits, 0, 1);
        TreatBar.Value = Math.Clamp((double)data.FeedingRemainderUnits / FeedingService.ActivityUnitsPerTreatEarned, 0, 1);
        TreatText.Text = $"{data.FeedingTreats} в запасе · новые за активность";
        PetButton.IsEnabled = active is not null && _pet is not null;
        FeedButton.IsEnabled = active is not null && data.FeedingTreats > 0 && _feed is not null;
        DetailButton.Content = active is null ? "Открыть Покедекс" : "Карточка питомца";
        DetailButton.IsEnabled = active is null ? _openPokedex is not null : _openIndividual is not null;
        var frames = active is null ? null : _spriteFrames();
        MissingSpriteText.Visibility = active is not null && (frames is null || frames.Count == 0)
            ? Visibility.Visible : Visibility.Collapsed;
        var spriteSignature = active is null || frames is null || frames.Count == 0 ? "" :
            $"{active.Id}:{active.CurrentSpeciesId}:{active.IsShiny}:{frames.Count}:{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(frames)}";
        if (spriteSignature != _spriteSignature)
        {
            _spriteSignature = spriteSignature;
            _animator?.Stop();
            _animator = null;
            if (spriteSignature.Length == 0)
            {
                PetImage.Source = null;
                _spriteBounds = default;
            }
            else
            {
                var decoded = frames!.Select(SpriteImages.Decode).ToList();
                _spriteBounds = MeasureSpriteBounds(decoded);
                PetImage.Source = decoded[0];
                if (decoded.Count > 1)
                {
                    _animator = new SpriteAnimator(PetImage, new FrameSequencer(decoded.Count, TimeSpan.FromMilliseconds(150)), decoded);
                    if (IsLoaded) _animator.Start();
                }
            }
        }
        PositionPet();
    }

    private void PositionPet()
    {
        // Accepted revision3 keeps the visible foot at (800,524), independent of desktop size.
        const double scale = 5.07;
        PetImage.Width = _spriteBounds.Width * scale;
        PetImage.Height = _spriteBounds.Height * scale;
        Canvas.SetLeft(PetImage, 800 - (_spriteBounds.Left + _spriteBounds.Right) * scale / 2);
        Canvas.SetTop(PetImage, 524 - _spriteBounds.Bottom * scale);
    }

    private static (int Width, int Height, int Left, int Right, int Bottom) MeasureSpriteBounds(IReadOnlyList<ImageSource> frames)
    {
        var first = (BitmapSource)frames[0];
        var left = first.PixelWidth;
        var right = 0;
        var bottom = 0;
        foreach (var frame in frames)
        {
            var source = (BitmapSource)frame;
            if (source.PixelWidth != first.PixelWidth || source.PixelHeight != first.PixelHeight)
                throw new InvalidDataException("Animated sprite frames have inconsistent dimensions.");
            var bitmap = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            var stride = bitmap.PixelWidth * 4;
            var pixels = new byte[stride * bitmap.PixelHeight];
            bitmap.CopyPixels(pixels, stride, 0);
            for (var y = 0; y < bitmap.PixelHeight; y++)
                for (var x = 0; x < bitmap.PixelWidth; x++)
                    if (pixels[y * stride + x * 4 + 3] != 0)
                    {
                        left = Math.Min(left, x);
                        right = Math.Max(right, x + 1);
                        bottom = Math.Max(bottom, y + 1);
                    }
        }
        if (right == 0) { left = 0; right = first.PixelWidth; bottom = first.PixelHeight; }
        return (first.PixelWidth, first.PixelHeight, left, right, bottom);
    }

    private void PetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshot().ActiveIndividualId is not null) _pet?.Invoke();
        Refresh();
    }

    private void FeedButton_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshot().ActiveIndividualId is not null) _feed?.Invoke();
        Refresh();
    }

    private void DetailButton_Click(object sender, RoutedEventArgs e)
    {
        var data = _snapshot();
        var active = data.Individuals.FirstOrDefault(p => p.Id == data.ActiveIndividualId);
        if (active is null) _openPokedex?.Invoke();
        else _openIndividual?.Invoke(active.Id);
    }

    private void AchievementsButton_Click(object sender, RoutedEventArgs e) => _openAchievements();
}
