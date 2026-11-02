using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Image = System.Windows.Controls.Image;
using Point = System.Windows.Point;

namespace PokeCorner.App.Popup;

/// <summary>One sketch on the diary margin, optionally with the trainer's note pinned to it.</summary>
public sealed record DiaryPiece(string Sketch, string? Note = null);

/// <summary>
/// The outer margin of a notebook page (Georgy 02.11): sketches scattered down the free space to the right of a
/// left-aligned column, each tilted and sized a little differently, notes pinned beside them. The scatter is fixed
/// per screen (seeded by its key), so nothing jumps between launches; more pieces appear as the margin grows.
/// </summary>
public sealed class DiaryMargin : Canvas
{
    private IReadOnlyList<DiaryPiece> _pieces = [];
    private string _key = "";

    public DiaryMargin()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        SizeChanged += (_, _) => Lay();
    }

    public void Show(string key, IReadOnlyList<DiaryPiece> pieces)
    {
        if (_key == key && ReferenceEquals(_pieces, pieces)) return;
        _key = key; _pieces = pieces;
        Lay();
    }

    private void Lay()
    {
        Children.Clear();
        double width = ActualWidth, height = ActualHeight;
        if (width < 96 || height < 80 || _pieces.Count == 0) return;
        var random = new Random(StableSeed(_key));
        var noteRoom = width >= 150;
        var baseSize = Math.Clamp(Math.Min(width * .55, height * .7), 60, 190); // a short band still gets one small sketch
        var y = 8 + random.NextDouble() * 24;
        foreach (var piece in _pieces)
        {
            var source = Load(piece.Sketch);
            if (source is null) continue;
            var size = baseSize * (.8 + random.NextDouble() * .4);
            var aspect = source.PixelHeight / (double)Math.Max(1, source.PixelWidth);
            var w = Math.Min(size, width - 12); var h = w * aspect;
            if (h > size * 1.6) { h = size * 1.6; w = h / aspect; }
            var note = noteRoom ? piece.Note : null;
            var noteHeight = note is null ? 0 : 64;
            if (y + h + noteHeight > height - 8) continue; // a tall sketch may not fit where a smaller next one still does
            var x = 6 + random.NextDouble() * Math.Max(0, width - w - 12);
            var image = new Image
            {
                Source = source, Width = w, Height = h, Opacity = .85, Stretch = Stretch.Uniform,
                RenderTransformOrigin = new Point(.5, .5),
                RenderTransform = new RotateTransform(random.NextDouble() * 28 - 14)
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            SetLeft(image, x); SetTop(image, y);
            Children.Add(image);
            y += h;
            if (note is not null)
            {
                var text = new TextBlock
                {
                    Text = note, MaxWidth = Math.Min(230, width - 16), TextAlignment = TextAlignment.Left,
                    RenderTransformOrigin = new Point(0, .5), RenderTransform = new RotateTransform(random.NextDouble() * 8 - 4)
                };
                if (TryFindResource("NdMarginNoteStyle") is Style style) text.Style = style;
                text.TextAlignment = TextAlignment.Left;
                // The note leans on the side of the sketch that has more room, so the column never looks mirrored.
                var noteX = x + w / 2 > width / 2 ? Math.Max(4, x - 40) : Math.Min(x + 20, Math.Max(4, width - text.MaxWidth - 4));
                SetLeft(text, noteX); SetTop(text, y + 2);
                Children.Add(text);
                y += noteHeight;
            }
            y += 16 + random.NextDouble() * 40;
        }
    }

    private static BitmapImage? Load(string sketch)
    {
        var uri = new Uri($"pack://application:,,,/PokeCorner;component/Assets/NightDiary/sketch-{sketch}.png");
        try { return System.Windows.Application.GetResourceStream(uri) is null ? null : new BitmapImage(uri); }
        catch (System.IO.IOException) { return null; } // a sketch not shipped yet simply leaves its place empty
    }

    // string.GetHashCode is randomised per process; the scatter must be the same on every launch.
    private static int StableSeed(string key) => key.Aggregate(17, (hash, c) => unchecked(hash * 31 + c));
}
