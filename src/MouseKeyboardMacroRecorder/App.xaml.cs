using MouseKeyboardMacroRecorder.Core.Application;
using System.IO;

namespace MouseKeyboardMacroRecorder;

/// <summary>
/// WPF application entry point.
/// </summary>
public partial class App : System.Windows.Application, IDisposable
{
    private Mutex? _singleInstanceMutex;

    /// <summary>
    /// Loads persisted application preferences before the main window is created.
    /// </summary>
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(
            initiallyOwned: true,
            name: $"Local\\{ProductInfo.Identifier}",
            createdNew: out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

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

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        Dispose();
        base.OnExit(e);
    }

    public void Dispose()
    {
        var mutex = Interlocked.Exchange(ref _singleInstanceMutex, null);
        if (mutex is null)
        {
            return;
        }

        mutex.ReleaseMutex();
        mutex.Dispose();
        GC.SuppressFinalize(this);
    }
}
