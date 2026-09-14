using MouseKeyboardMacroRecorder.Core.Application;

namespace MouseKeyboardMacroRecorder.Core.Domain;

/// <summary>
/// Safety and resource limits shared by every automation entry point.
/// </summary>
public static class AutomationLimits
{
    public const int MaximumMacroActions = 100_000;
    public const int MaximumActionDelayMilliseconds = 24 * 60 * 60 * 1000;
    public const double MaximumPlaybackSpeed = 100;
    public const int MaximumPlaybackRepeatCount = 100_000;
    public const int MinimumAutoClickIntervalMilliseconds = 10;
    public const int MaximumAutoClickIntervalMilliseconds = 24 * 60 * 60 * 1000;
    public const int MaximumAutoClickRepeatCount = 1_000_000;
}

/// <summary>
/// Default values used when no persisted preference exists or a preference is invalid.
/// </summary>
public static class AutomationDefaults
{
    public const bool CaptureMouse = true;
    public const bool CaptureKeyboard = true;
    public const double PlaybackSpeed = 1;
    public const RepeatMode PlaybackRepeatMode = RepeatMode.Once;
    public const int PlaybackRepeatCount = 1;
    public const int PlaybackInterLoopDelayMilliseconds = 0;
    public const MouseButtonKind AutoClickButton = MouseButtonKind.Left;
    public const int AutoClickIntervalMilliseconds = 100;
    public const RepeatMode AutoClickRepeatMode = RepeatMode.UntilStopped;
    public const int AutoClickRepeatCount = 1;
    public const ClickPositionMode AutoClickPositionMode = ClickPositionMode.CurrentCursor;
    public const string LastSurface = WorkspaceSurfaceNames.Macros;
}

/// <summary>
/// Default global hotkeys and validation helpers.
/// </summary>
public static class HotkeyDefaults
{
    public const uint RecordVirtualKey = 0x77;
    public const uint PlayVirtualKey = 0x78;
    public const uint F10VirtualKey = 0x79;
    public const uint F11VirtualKey = 0x7A;
    public const uint F12VirtualKey = 0x7B;
    public const uint StopVirtualKey = 0x1B;

    public static IReadOnlyList<uint> RecordVirtualKeys { get; } = Array.AsReadOnly(new uint[]
    {
        RecordVirtualKey,
        F10VirtualKey,
        F11VirtualKey
    });

    public static IReadOnlyList<uint> PlayVirtualKeys { get; } = Array.AsReadOnly(new uint[]
    {
        PlayVirtualKey,
        F10VirtualKey,
        F12VirtualKey
    });

    public static IReadOnlyList<uint> StopVirtualKeys { get; } = Array.AsReadOnly(new uint[]
    {
        StopVirtualKey,
        F11VirtualKey,
        F12VirtualKey
    });

    public static bool IsSupported(uint virtualKey)
    {
        return virtualKey is >= 1 and <= 0xFE;
    }

    public static bool IsSupportedModifiers(HotkeyModifiers modifiers)
    {
        return ((uint)modifiers & ~0x000Fu) == 0;
    }

    public static string GetDisplayName(uint virtualKey)
    {
        return virtualKey switch
        {
            StopVirtualKey => "Esc",
            >= 0x70 and <= 0x87 => $"F{virtualKey - 0x6F}",
            _ => $"VK 0x{virtualKey:X2}"
        };
    }
}

/// <summary>
/// Stable values used to restore the last active command surface.
/// </summary>
public static class WorkspaceSurfaceNames
{
    public const string Macros = "macros";
    public const string AutoClicker = "auto";

    public static bool IsSupported(string? value)
    {
        return string.Equals(value, Macros, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, AutoClicker, StringComparison.OrdinalIgnoreCase);
    }
}
