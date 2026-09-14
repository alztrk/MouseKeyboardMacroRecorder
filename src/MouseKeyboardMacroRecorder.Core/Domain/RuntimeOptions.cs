namespace MouseKeyboardMacroRecorder.Core.Domain;

/// <summary>
/// The playback repeat behavior shared by playback and auto-clicker flows.
/// </summary>
public enum RepeatMode
{
    Once,
    FixedCount,
    UntilStopped
}

/// <summary>
/// The coordinate source used by the auto-clicker.
/// </summary>
public enum ClickPositionMode
{
    CurrentCursor,
    FixedPosition
}

/// <summary>
/// Playback configuration validated at the service boundary.
/// </summary>
public sealed record PlaybackOptions(
    double Speed,
    RepeatMode RepeatMode,
    int RepeatCount,
    int InterLoopDelayMilliseconds = 0)
{
    public void EnsureValid()
    {
        if (double.IsNaN(Speed) || double.IsInfinity(Speed) || Speed <= 0 || Speed > AutomationLimits.MaximumPlaybackSpeed)
        {
            throw new ArgumentOutOfRangeException(nameof(Speed), $"Playback speed must be greater than 0 and no more than {AutomationLimits.MaximumPlaybackSpeed}.");
        }

        if (RepeatMode == RepeatMode.FixedCount && (RepeatCount <= 0 || RepeatCount > AutomationLimits.MaximumPlaybackRepeatCount))
        {
            throw new ArgumentOutOfRangeException(nameof(RepeatCount), $"Playback repeat count must be between 1 and {AutomationLimits.MaximumPlaybackRepeatCount}.");
        }

        if (InterLoopDelayMilliseconds < 0 || InterLoopDelayMilliseconds > AutomationLimits.MaximumActionDelayMilliseconds)
        {
            throw new ArgumentOutOfRangeException(nameof(InterLoopDelayMilliseconds), $"Inter-loop delay must be between 0 and {AutomationLimits.MaximumActionDelayMilliseconds} milliseconds.");
        }
    }
}

/// <summary>
/// Auto-clicker configuration validated at the service boundary.
/// </summary>
public sealed record AutoClickerOptions(
    MouseButtonKind Button,
    int IntervalMilliseconds,
    RepeatMode RepeatMode,
    int RepeatCount,
    ClickPositionMode PositionMode,
    int FixedX,
    int FixedY)
{
    public void EnsureValid()
    {
        if (IntervalMilliseconds < AutomationLimits.MinimumAutoClickIntervalMilliseconds
            || IntervalMilliseconds > AutomationLimits.MaximumAutoClickIntervalMilliseconds)
        {
            throw new ArgumentOutOfRangeException(nameof(IntervalMilliseconds), $"Interval must be between {AutomationLimits.MinimumAutoClickIntervalMilliseconds} and {AutomationLimits.MaximumAutoClickIntervalMilliseconds} milliseconds.");
        }

        if (RepeatMode == RepeatMode.FixedCount && (RepeatCount <= 0 || RepeatCount > AutomationLimits.MaximumAutoClickRepeatCount))
        {
            throw new ArgumentOutOfRangeException(nameof(RepeatCount), $"Click repeat count must be between 1 and {AutomationLimits.MaximumAutoClickRepeatCount}.");
        }
    }
}

/// <summary>
/// Options that control which local input streams are recorded.
/// </summary>
public sealed record RecordingOptions(bool CaptureMouse, bool CaptureKeyboard, bool IgnoreInjectedEvents = true)
{
    public void EnsureValid()
    {
        if (!CaptureMouse && !CaptureKeyboard)
        {
            throw new ArgumentException("At least one input stream must be enabled.", nameof(RecordingOptions));
        }
    }
}
