using System.Windows;
using System.Windows.Controls;

namespace PokeCorner.App.Popup;

public partial class ConfirmationOverlay : System.Windows.Controls.UserControl
{
    public event Action<bool>? Completed;

    public ConfirmationOverlay() => InitializeComponent();

    public void Present(string title, string body, string confirmCaption)
    {
        TitleText.Text = title;
        BodyText.Text = body;
        ConfirmButton.Content = confirmCaption;
        Visibility = Visibility.Visible;
        UpdateLayout();
        CancelButton.Focus();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Completed?.Invoke(false);
    private void Confirm_Click(object sender, RoutedEventArgs e) => Completed?.Invoke(true);
}
