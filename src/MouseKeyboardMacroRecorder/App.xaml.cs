using MouseKeyboardMacroRecorder.Core.Application;
using System.IO;

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
            var details = args.Exception.ToString();
            System.Diagnostics.Debug.WriteLine(details);
            try
            {
                var directory = ProductInfo.GetLocalDataDirectory();
                Directory.CreateDirectory(directory);
                File.AppendAllText(
                    Path.Combine(directory, "errors.log"),
                    $"[{DateTime.Now:O}] UI dispatcher exception{Environment.NewLine}{details}{Environment.NewLine}{Environment.NewLine}");
            }
            catch (Exception loggingException)
            {
                System.Diagnostics.Debug.WriteLine(loggingException);
            }

            System.Windows.MessageBox.Show(
                $"The operation could not be completed.{Environment.NewLine}{Environment.NewLine}{args.Exception.Message}",
                ProductInfo.DisplayName,
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        };

        ThemeManager.LoadSavedTheme();
        base.OnStartup(e);
    }
}
