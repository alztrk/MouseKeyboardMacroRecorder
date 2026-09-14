using System.IO;

namespace MouseKeyboardMacroRecorder.Core.Application;

/// <summary>
/// Stable product identity and user-data locations shared by persistence and presentation.
/// </summary>
public static class ProductInfo
{
    public const string Identifier = "MouseKeyboardMacroRecorder";
    public const string DisplayName = "Mouse Keyboard Macro Recorder";
    public const string ThemeSettingsFileName = "settings.json";
    public const string UserPreferencesFileName = "preferences.json";
    public const string MacroDirectoryName = "Macros";
    public const string MacroFileDialogFilter = "Macro files (*.macro.json)|*.macro.json";
    public const string RecordedMacroBaseName = "Recorded macro";

    public static string GetLocalDataDirectory()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException("The Windows local application data directory is unavailable.");
        }

        return Path.Combine(localApplicationData, Identifier);
    }

    public static string GetLocalDataPath(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (!string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
        {
            throw new ArgumentException("The application data file name must not contain a directory.", nameof(fileName));
        }

        return Path.Combine(GetLocalDataDirectory(), fileName);
    }

    public static string GetMacroDirectory()
    {
        return Path.Combine(GetLocalDataDirectory(), MacroDirectoryName);
    }
}
