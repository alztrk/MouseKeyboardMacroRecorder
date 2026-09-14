using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using MouseKeyboardMacroRecorder.Core.Application;
using Color = System.Windows.Media.Color;
using WpfApplication = System.Windows.Application;

namespace MouseKeyboardMacroRecorder;

/// <summary>
/// Applies and persists the application's visual theme.
/// </summary>
public static class ThemeManager
{
    /// <summary>
    /// Gets a value indicating whether the dark theme is active.
    /// </summary>
    public static bool IsDarkTheme { get; private set; }

    /// <summary>
    /// Loads the saved theme and applies its resource colors.
    /// </summary>
    public static void LoadSavedTheme()
    {
        IsDarkTheme = ReadSavedTheme();
        ApplyResources();
    }

    /// <summary>
    /// Switches the active theme and persists the new selection.
    /// </summary>
    public static void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        ApplyResources();
        SaveTheme();
    }

    private static void ApplyResources()
    {
        if (IsDarkTheme)
        {
            SetBrush("CanvasBrush", Color.FromRgb(21, 26, 32));
            SetBrush("SurfaceBrush", Color.FromRgb(29, 36, 44));
            SetBrush("BorderBrush", Color.FromRgb(47, 57, 68));
            SetBrush("InkBrush", Color.FromRgb(241, 244, 247));
            SetBrush("MutedInkBrush", Color.FromRgb(177, 188, 201));
            SetBrush("SubtleInkBrush", Color.FromRgb(133, 147, 164));
            SetBrush("SelectAccentBrush", Color.FromRgb(227, 154, 110));
            SetBrush("SidebarBrush", Color.FromRgb(16, 21, 27));
            SetBrush("SidebarSelectedBrush", Color.FromRgb(38, 48, 60));
            SetBrush("SidebarMutedBrush", Color.FromRgb(178, 188, 201));
            SetBrush("AccentSoftBrush", Color.FromRgb(66, 42, 31));
            SetBrush("SuccessBrush", Color.FromRgb(77, 177, 137));
            SetBrush("SidebarHoverBrush", Color.FromRgb(38, 48, 60));
            SetBrush("PrimaryHoverBrush", Color.FromRgb(169, 84, 37));
            SetBrush("SecondaryHoverBrush", Color.FromRgb(42, 51, 61));
            SetBrush("SecondaryHoverBorderBrush", Color.FromRgb(67, 79, 93));
            SetBrush("WindowHoverBrush", Color.FromRgb(48, 59, 72));
            SetBrush("SelectHoverBrush", Color.FromRgb(66, 42, 31));
            SetBrush("SelectSelectedBrush", Color.FromRgb(82, 47, 31));
            SetBrush("SelectArrowSurfaceBrush", Color.FromRgb(53, 39, 32));
            SetBrush("SelectOpenBrush", Color.FromRgb(34, 42, 51));
            SetBrush("SelectHoverBorderBrush", Color.FromRgb(72, 84, 99));
            SetBrush("PopupBorderBrush", Color.FromRgb(58, 69, 82));
            SetBrush("DisabledSurfaceBrush", Color.FromRgb(38, 46, 56));
            SetBrush("DisabledBorderBrush", Color.FromRgb(53, 63, 75));
            SetBrush("DisabledInkBrush", Color.FromRgb(133, 147, 164));
            SetBrush("WindowCloseHoverBrush", Color.FromRgb(165, 65, 65));
            SetBrush("FocusRingBrush", Color.FromRgb(217, 149, 110));
            SetBrush("ErrorBrush", Color.FromRgb(226, 125, 125));
            SetBrush("ErrorSoftBrush", Color.FromRgb(61, 35, 38));
            SetBrush("ListRowBrush", Color.FromRgb(29, 36, 44));
            SetBrush("ListRowHoverBrush", Color.FromRgb(38, 47, 57));
            SetBrush("ListRowSelectedBrush", Color.FromRgb(66, 42, 31));
            WpfApplication.Current.Resources["PopupShadowColor"] = Color.FromRgb(0, 0, 0);
            return;
        }

        SetBrush("CanvasBrush", Color.FromRgb(245, 247, 250));
        SetBrush("SurfaceBrush", Color.FromRgb(255, 255, 255));
        SetBrush("BorderBrush", Color.FromRgb(231, 236, 241));
        SetBrush("InkBrush", Color.FromRgb(25, 33, 43));
        SetBrush("MutedInkBrush", Color.FromRgb(102, 115, 132));
        SetBrush("SubtleInkBrush", Color.FromRgb(106, 119, 136));
        SetBrush("SelectAccentBrush", Color.FromRgb(197, 106, 50));
        SetBrush("SidebarBrush", Color.FromRgb(23, 32, 43));
        SetBrush("SidebarSelectedBrush", Color.FromRgb(36, 49, 64));
        SetBrush("SidebarMutedBrush", Color.FromRgb(169, 180, 194));
        SetBrush("AccentSoftBrush", Color.FromRgb(255, 240, 231));
        SetBrush("SuccessBrush", Color.FromRgb(46, 139, 103));
        SetBrush("SidebarHoverBrush", Color.FromRgb(36, 49, 64));
        SetBrush("PrimaryHoverBrush", Color.FromRgb(169, 84, 37));
        SetBrush("SecondaryHoverBrush", Color.FromRgb(240, 243, 246));
        SetBrush("SecondaryHoverBorderBrush", Color.FromRgb(213, 222, 231));
        SetBrush("WindowHoverBrush", Color.FromRgb(233, 237, 242));
        SetBrush("SelectHoverBrush", Color.FromRgb(255, 242, 233));
        SetBrush("SelectSelectedBrush", Color.FromRgb(255, 240, 231));
        SetBrush("SelectArrowSurfaceBrush", Color.FromRgb(255, 247, 242));
        SetBrush("SelectOpenBrush", Color.FromRgb(255, 252, 250));
        SetBrush("SelectHoverBorderBrush", Color.FromRgb(204, 214, 224));
        SetBrush("PopupBorderBrush", Color.FromRgb(226, 231, 237));
        SetBrush("DisabledSurfaceBrush", Color.FromRgb(238, 241, 244));
        SetBrush("DisabledBorderBrush", Color.FromRgb(229, 234, 240));
        SetBrush("DisabledInkBrush", Color.FromRgb(138, 150, 166));
        SetBrush("WindowCloseHoverBrush", Color.FromRgb(201, 76, 76));
        SetBrush("FocusRingBrush", Color.FromRgb(197, 106, 50));
        SetBrush("ErrorBrush", Color.FromRgb(182, 75, 75));
        SetBrush("ErrorSoftBrush", Color.FromRgb(255, 240, 240));
        SetBrush("ListRowBrush", Color.FromRgb(255, 255, 255));
        SetBrush("ListRowHoverBrush", Color.FromRgb(255, 248, 244));
        SetBrush("ListRowSelectedBrush", Color.FromRgb(255, 240, 231));
        WpfApplication.Current.Resources["PopupShadowColor"] = Color.FromRgb(25, 33, 43);
    }

    private static void SetBrush(string key, Color color)
    {
        WpfApplication.Current.Resources[key] = new SolidColorBrush(color);
    }

    private static bool ReadSavedTheme()
    {
        try
        {
            var json = File.ReadAllText(ProductInfo.GetLocalDataPath(ProductInfo.ThemeSettingsFileName));
            var settings = JsonSerializer.Deserialize<ThemeSettings>(json);
            return string.Equals(settings?.Theme, "dark", StringComparison.OrdinalIgnoreCase);
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void SaveTheme()
    {
        try
        {
            var settingsPath = ProductInfo.GetLocalDataPath(ProductInfo.ThemeSettingsFileName);
            var directory = Path.GetDirectoryName(settingsPath);
            if (directory is null)
            {
                return;
            }

            Directory.CreateDirectory(directory);
            var json = JsonSerializer.Serialize(new ThemeSettings(IsDarkTheme ? "dark" : "light"));
            File.WriteAllText(settingsPath, json);
        }
        catch (IOException)
        {
            System.Diagnostics.Debug.WriteLine("The selected theme could not be persisted.");
        }
        catch (UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine("The selected theme could not be persisted.");
        }
        catch (InvalidOperationException)
        {
            System.Diagnostics.Debug.WriteLine("The selected theme could not be persisted.");
        }
    }

    private sealed record ThemeSettings(string Theme);
}
