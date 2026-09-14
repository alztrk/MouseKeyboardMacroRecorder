using System.Text.Json;
using System.Text.Json.Serialization;
using MouseKeyboardMacroRecorder.Core.Application;
using MouseKeyboardMacroRecorder.Core.Domain;

namespace MouseKeyboardMacroRecorder.Core.Infrastructure.Persistence;

/// <summary>
/// Settings persisted separately from the existing theme file.
/// </summary>
public sealed class UserPreferences
{
    public bool CaptureMouse { get; set; } = AutomationDefaults.CaptureMouse;

    public bool CaptureKeyboard { get; set; } = AutomationDefaults.CaptureKeyboard;

    public double PlaybackSpeed { get; set; } = AutomationDefaults.PlaybackSpeed;

    public RepeatMode PlaybackRepeatMode { get; set; } = AutomationDefaults.PlaybackRepeatMode;

    public int PlaybackRepeatCount { get; set; } = AutomationDefaults.PlaybackRepeatCount;

    public int PlaybackInterLoopDelayMilliseconds { get; set; } = AutomationDefaults.PlaybackInterLoopDelayMilliseconds;

    public uint RecordHotkeyVirtualKey { get; set; } = HotkeyDefaults.RecordVirtualKey;

    public HotkeyModifiers RecordHotkeyModifiers { get; set; } = HotkeyModifiers.None;

    public uint PlayHotkeyVirtualKey { get; set; } = HotkeyDefaults.PlayVirtualKey;

    public HotkeyModifiers PlayHotkeyModifiers { get; set; } = HotkeyModifiers.None;

    public uint StopHotkeyVirtualKey { get; set; } = HotkeyDefaults.StopVirtualKey;

    public HotkeyModifiers StopHotkeyModifiers { get; set; } = HotkeyModifiers.None;

    public MouseButtonKind AutoClickButton { get; set; } = AutomationDefaults.AutoClickButton;

    public int AutoClickIntervalMilliseconds { get; set; } = AutomationDefaults.AutoClickIntervalMilliseconds;

    public RepeatMode AutoClickRepeatMode { get; set; } = AutomationDefaults.AutoClickRepeatMode;

    public int AutoClickRepeatCount { get; set; } = AutomationDefaults.AutoClickRepeatCount;

    public ClickPositionMode AutoClickPositionMode { get; set; } = AutomationDefaults.AutoClickPositionMode;

    public int AutoClickFixedX { get; set; }

    public int AutoClickFixedY { get; set; }

    public string LastSurface { get; set; } = AutomationDefaults.LastSurface;
}

/// <summary>
/// Repairs invalid or out-of-range persisted values before they reach the UI or automation services.
/// </summary>
public static class UserPreferencesNormalizer
{
    public static void Normalize(UserPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        if (!double.IsFinite(preferences.PlaybackSpeed)
            || preferences.PlaybackSpeed <= 0
            || preferences.PlaybackSpeed > AutomationLimits.MaximumPlaybackSpeed)
        {
            preferences.PlaybackSpeed = AutomationDefaults.PlaybackSpeed;
        }

        if (!Enum.IsDefined(preferences.PlaybackRepeatMode))
        {
            preferences.PlaybackRepeatMode = AutomationDefaults.PlaybackRepeatMode;
        }

        if (preferences.PlaybackRepeatCount is < 1 or > AutomationLimits.MaximumPlaybackRepeatCount)
        {
            preferences.PlaybackRepeatCount = AutomationDefaults.PlaybackRepeatCount;
        }

        if (preferences.PlaybackInterLoopDelayMilliseconds is < 0 or > AutomationLimits.MaximumActionDelayMilliseconds)
        {
            preferences.PlaybackInterLoopDelayMilliseconds = AutomationDefaults.PlaybackInterLoopDelayMilliseconds;
        }

        if (!Enum.IsDefined(preferences.AutoClickButton))
        {
            preferences.AutoClickButton = AutomationDefaults.AutoClickButton;
        }

        if (preferences.AutoClickIntervalMilliseconds is < AutomationLimits.MinimumAutoClickIntervalMilliseconds
            or > AutomationLimits.MaximumAutoClickIntervalMilliseconds)
        {
            preferences.AutoClickIntervalMilliseconds = AutomationDefaults.AutoClickIntervalMilliseconds;
        }

        if (!Enum.IsDefined(preferences.AutoClickRepeatMode))
        {
            preferences.AutoClickRepeatMode = AutomationDefaults.AutoClickRepeatMode;
        }

        if (preferences.AutoClickRepeatCount is < 1 or > AutomationLimits.MaximumAutoClickRepeatCount)
        {
            preferences.AutoClickRepeatCount = AutomationDefaults.AutoClickRepeatCount;
        }

        if (!Enum.IsDefined(preferences.AutoClickPositionMode))
        {
            preferences.AutoClickPositionMode = AutomationDefaults.AutoClickPositionMode;
        }

        if (!WorkspaceSurfaceNames.IsSupported(preferences.LastSurface))
        {
            preferences.LastSurface = AutomationDefaults.LastSurface;
        }

        if (!HotkeyDefaults.IsSupported(preferences.RecordHotkeyVirtualKey)
            || !HotkeyDefaults.IsSupportedModifiers(preferences.RecordHotkeyModifiers))
        {
            preferences.RecordHotkeyVirtualKey = HotkeyDefaults.RecordVirtualKey;
            preferences.RecordHotkeyModifiers = HotkeyModifiers.None;
        }

        if (!HotkeyDefaults.IsSupported(preferences.PlayHotkeyVirtualKey)
            || !HotkeyDefaults.IsSupportedModifiers(preferences.PlayHotkeyModifiers))
        {
            preferences.PlayHotkeyVirtualKey = HotkeyDefaults.PlayVirtualKey;
            preferences.PlayHotkeyModifiers = HotkeyModifiers.None;
        }

        if (!HotkeyDefaults.IsSupported(preferences.StopHotkeyVirtualKey)
            || !HotkeyDefaults.IsSupportedModifiers(preferences.StopHotkeyModifiers))
        {
            preferences.StopHotkeyVirtualKey = HotkeyDefaults.StopVirtualKey;
            preferences.StopHotkeyModifiers = HotkeyModifiers.None;
        }

        var recordBinding = new HotkeyBinding(
            HotkeyCommand.Record,
            preferences.RecordHotkeyModifiers,
            preferences.RecordHotkeyVirtualKey);
        var playBinding = new HotkeyBinding(
            HotkeyCommand.Play,
            preferences.PlayHotkeyModifiers,
            preferences.PlayHotkeyVirtualKey);
        var stopBinding = new HotkeyBinding(
            HotkeyCommand.Stop,
            preferences.StopHotkeyModifiers,
            preferences.StopHotkeyVirtualKey);
        if (recordBinding.Modifiers == playBinding.Modifiers && recordBinding.VirtualKey == playBinding.VirtualKey
            || recordBinding.Modifiers == stopBinding.Modifiers && recordBinding.VirtualKey == stopBinding.VirtualKey
            || playBinding.Modifiers == stopBinding.Modifiers && playBinding.VirtualKey == stopBinding.VirtualKey)
        {
            preferences.RecordHotkeyVirtualKey = HotkeyDefaults.RecordVirtualKey;
            preferences.RecordHotkeyModifiers = HotkeyModifiers.None;
            preferences.PlayHotkeyVirtualKey = HotkeyDefaults.PlayVirtualKey;
            preferences.PlayHotkeyModifiers = HotkeyModifiers.None;
            preferences.StopHotkeyVirtualKey = HotkeyDefaults.StopVirtualKey;
            preferences.StopHotkeyModifiers = HotkeyModifiers.None;
        }
    }
}

/// <summary>
/// Stores non-theme application preferences in the local application data directory.
/// </summary>
public sealed class UserPreferencesStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static UserPreferences Load()
    {
        try
        {
            var path = GetPath();
            var json = File.ReadAllText(path);
            var preferences = JsonSerializer.Deserialize<UserPreferences>(json, Options)
                ?? throw new PreferencesPersistenceException("The preferences file is empty.");
            UserPreferencesNormalizer.Normalize(preferences);
            return preferences;
        }
        catch (FileNotFoundException)
        {
            return new UserPreferences();
        }
        catch (DirectoryNotFoundException)
        {
            return new UserPreferences();
        }
        catch (JsonException exception)
        {
            throw new PreferencesPersistenceException("The preferences file is invalid.", exception);
        }
        catch (IOException exception)
        {
            throw new PreferencesPersistenceException("The preferences file could not be read.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new PreferencesPersistenceException("The preferences file could not be read because access was denied.", exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new PreferencesPersistenceException("The preferences storage location is unavailable.", exception);
        }
    }

    public static void Save(UserPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        UserPreferencesNormalizer.Normalize(preferences);
        try
        {
            var path = GetPath();
            var json = JsonSerializer.Serialize(preferences, Options);
            AtomicFileWriter.WriteUtf8(path, json);
        }
        catch (IOException exception)
        {
            throw new PreferencesPersistenceException("The preferences could not be saved.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new PreferencesPersistenceException("The preferences could not be saved because access was denied.", exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new PreferencesPersistenceException("The preferences storage location is unavailable.", exception);
        }
    }

    public static string GetPath()
    {
        return ProductInfo.GetLocalDataPath(ProductInfo.UserPreferencesFileName);
    }
}

/// <summary>
/// Indicates that preferences could not be read or written.
/// </summary>
public sealed class PreferencesPersistenceException : Exception
{
    public PreferencesPersistenceException(string message)
        : base(message)
    {
    }

    public PreferencesPersistenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
