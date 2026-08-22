using System.Windows;

namespace Hs80.App;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string detail, string hint)
    {
        InitializeComponent();
        TitleText.Text = title;
        DetailText.Text = detail;
        HintText.Text = hint;
    }

    private void OnRetry(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
