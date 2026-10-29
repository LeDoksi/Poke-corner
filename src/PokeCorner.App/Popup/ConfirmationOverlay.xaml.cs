using System.Windows;
using System.Windows.Controls;

namespace PokeCorner.App.Popup;

public partial class ConfirmationOverlay : System.Windows.Controls.UserControl
{
    public event Action<bool>? Completed;

    private bool _nightDiary;

    public ConfirmationOverlay() => InitializeComponent();

    public void UseNightDiaryTheme()
    {
        _nightDiary = true;
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
        Foreground = (System.Windows.Media.Brush)FindResource("NdTextBrush");
        Background = (System.Windows.Media.Brush)FindResource("NdScrimBrush");
        ConfirmationCard.Background = (System.Windows.Media.Brush)FindResource("NdSurfaceBrush");
        ConfirmationCard.BorderBrush = (System.Windows.Media.Brush)FindResource("NdBorderBrush");
        ConfirmationCard.BorderThickness = new Thickness(1);
        CancelButton.Style = (Style)FindResource("NdDialogButtonStyle");
        Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)] = (Style)FindResource("NdScrollBarStyle");
    }

    public void Present(string title, string body, string confirmCaption, bool destructive = false)
    {
        TitleText.Text = title;
        BodyText.Text = body;
        ConfirmButton.Content = confirmCaption;
        CancelButton.Content = destructive ? "Оставить" : "Отмена";
        ConfirmButton.Style = (Style)FindResource(_nightDiary
            ? destructive ? "NdDialogDangerButtonStyle" : "NdDialogPrimaryButtonStyle"
            : destructive ? "UiDangerButtonStyle" : "UiPrimaryButtonStyle");
        Visibility = Visibility.Visible;
        UpdateLayout();
        CancelButton.Focus();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Completed?.Invoke(false);
    private void Confirm_Click(object sender, RoutedEventArgs e) => Completed?.Invoke(true);
}
