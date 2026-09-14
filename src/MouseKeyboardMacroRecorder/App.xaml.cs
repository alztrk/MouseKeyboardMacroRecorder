using MouseKeyboardMacroRecorder.Core.Application;

namespace MouseKeyboardMacroRecorder;

/// <summary>
/// WPF application entry point.
/// </summary>
public partial class App : System.Windows.Application
{
    /// <summary>
    /// Loads persisted application preferences before the main window is created.
    /// </summary>
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            System.Diagnostics.Debug.WriteLine(args.Exception);
            System.Windows.MessageBox.Show(
                "The operation could not be completed. The application is still running.",
                ProductInfo.DisplayName,
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        };

        ThemeManager.LoadSavedTheme();
        base.OnStartup(e);
    }
}
