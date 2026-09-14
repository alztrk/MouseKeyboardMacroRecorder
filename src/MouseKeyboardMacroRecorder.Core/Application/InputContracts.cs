using MouseKeyboardMacroRecorder.Core.Domain;

namespace MouseKeyboardMacroRecorder.Core.Application;

/// <summary>
/// A captured mouse movement from the operating-system input boundary.
/// </summary>
public sealed record CapturedMouseMove(DateTimeOffset TimestampUtc, int X, int Y) : CapturedInput(TimestampUtc);

/// <summary>
/// A captured mouse button event from the operating-system input boundary.
/// </summary>
public sealed record CapturedMouseButton(
    DateTimeOffset TimestampUtc,
    int X,
    int Y,
    MouseButtonKind Button,
    MouseButtonState State) : CapturedInput(TimestampUtc);

/// <summary>
/// A captured mouse wheel event from the operating-system input boundary.
/// </summary>
public sealed record CapturedMouseWheel(
    DateTimeOffset TimestampUtc,
    int X,
    int Y,
    int Delta) : CapturedInput(TimestampUtc);

/// <summary>
/// A captured keyboard event from the operating-system input boundary.
/// </summary>
public sealed record CapturedKeyboard(
    DateTimeOffset TimestampUtc,
    ushort VirtualKey,
    string Key,
    KeyboardKeyState State,
    bool IsExtended) : CapturedInput(TimestampUtc);

/// <summary>
/// Base type for operating-system input events.
/// </summary>
public abstract record CapturedInput(DateTimeOffset TimestampUtc);

/// <summary>
/// Describes the screen at record time.
/// </summary>
public interface IScreenInfoProvider
{
    ScreenInfo GetCurrent();
}

/// <summary>
/// Returns the current cursor position in virtual-screen coordinates.
/// </summary>
public interface ICursorPositionProvider
{
    (int X, int Y) GetPosition();
}

/// <summary>
/// Captures low-level local input without exposing platform types to the application layer.
/// </summary>
public interface IInputCapture : IDisposable
{
    IDisposable Start(RecordingOptions options, Action<CapturedInput> onInput);
}

/// <summary>
/// Sends mouse and keyboard input and tracks inputs that must be released on cancellation.
/// </summary>
public interface IInputInjector : IDisposable
{
    void MoveMouse(int x, int y);

    void MouseButton(MouseButtonKind button, MouseButtonState state);

    void MouseWheel(int delta);

    void Keyboard(ushort virtualKey, KeyboardKeyState state, bool isExtended);

    void ReleaseAll();
}

/// <summary>
/// Provides a replaceable delay primitive for deterministic service tests.
/// </summary>
public interface IAsyncDelay
{
    Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken);
}

/// <summary>
/// A global hotkey command understood by the presentation layer.
/// </summary>
public enum HotkeyCommand
{
    Record,
    Play,
    Stop
}

/// <summary>
/// Modifier flags used by RegisterHotKey.
/// </summary>
[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008
}

/// <summary>
/// A global hotkey registration.
/// </summary>
public sealed record HotkeyBinding(HotkeyCommand Command, HotkeyModifiers Modifiers, uint VirtualKey);

/// <summary>
/// Global hotkey events raised by the infrastructure adapter.
/// </summary>
public sealed class HotkeyPressedEventArgs : EventArgs
{
    public HotkeyPressedEventArgs(HotkeyCommand command)
    {
        Command = command;
    }

    public HotkeyCommand Command { get; }
}

/// <summary>
/// Registers global hotkeys without exposing window handles to the UI.
/// </summary>
public interface IGlobalHotkeyService : IDisposable
{
    event EventHandler<HotkeyPressedEventArgs>? Pressed;

    IDisposable Register(IReadOnlyList<HotkeyBinding> bindings);
}
