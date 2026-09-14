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
        ThemeManager.LoadSavedTheme();
        base.OnStartup(e);
    }
}
