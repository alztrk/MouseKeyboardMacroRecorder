using System.Windows;

namespace MouseKeyboardMacroRecorder;

/// <summary>
/// Layout thresholds shared by the WPF markup and responsive code.
/// </summary>
public static class UiMetrics
{
    public const double DefaultWindowWidth = 900;
    public const double DefaultWindowHeight = 560;
    public const double MinimumWindowWidth = 760;
    public const double MinimumWindowHeight = 460;
    public const double HideShortcutHintWidth = 860;
    public const double CompactSurfaceWidth = 640;
    public const double MacroSettingsWidth = 240;
    public const double AutoClickerSettingsWidth = 224;

    public static GridLength MacroSettingsColumnWidth => new(MacroSettingsWidth);

    public static GridLength AutoClickerSettingsColumnWidth => new(AutoClickerSettingsWidth);
}
