using System.Globalization;
using System.Windows.Data;
using PokeCorner.Core.Domain;

namespace PokeCorner.App.Popup;

public sealed class RarityToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            RarityTier.Common => "RarityCommonBrush",
            RarityTier.Uncommon => "RarityUncommonBrush",
            RarityTier.Rare => "RarityRareBrush",
            RarityTier.Legendary => "RarityLegendaryBrush",
            _ => "RarityCommonBrush"
        };
        // Fully qualified: the project has UseWindowsForms enabled for TrayIconService's
        // NotifyIcon, which makes the unqualified "Application" type ambiguous between
        // System.Windows.Application and System.Windows.Forms.Application.
        return System.Windows.Application.Current.Resources[key];
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
