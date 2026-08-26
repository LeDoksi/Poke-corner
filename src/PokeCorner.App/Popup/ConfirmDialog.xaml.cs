namespace PokeCorner.App.Popup;

/// Themed replacement for MessageBox — see the comment in ConfirmDialog.xaml for why the
/// stock one is not usable here.
public partial class ConfirmDialog : System.Windows.Window
{
    public ConfirmDialog(string title, string body, string confirmCaption)
    {
        InitializeComponent();
        TitleText.Text = title;
        BodyText.Text = body;
        ConfirmButton.Content = confirmCaption;
    }

    /// Returns true only if the player pressed the confirm button. Closing the window, Esc
    /// and Cancel all fall through to false, so a caller chaining two of these gets
    /// "confirmed twice" semantics from a plain `Ask(...) && Ask(...)`.
    public static bool Ask(string title, string body, string confirmCaption, System.Windows.Window? owner = null)
    {
        var dialog = new ConfirmDialog(title, body, confirmCaption);
        if (owner is not null)
        {
            dialog.Owner = owner;
            dialog.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
        }
        return dialog.ShowDialog() == true;
    }

    private void ConfirmButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
