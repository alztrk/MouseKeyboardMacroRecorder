using System.Windows;

namespace MouseKeyboardMacroRecorder;

public partial class ErrorDialog : Window
{
    public ErrorDialog(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
