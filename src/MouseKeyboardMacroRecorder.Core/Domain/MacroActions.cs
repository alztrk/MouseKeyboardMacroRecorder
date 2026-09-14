namespace MouseKeyboardMacroRecorder.Core.Domain;

/// <summary>
/// The mouse buttons supported by the macro format and input services.
/// </summary>
public enum MouseButtonKind
{
    Left,
    Right,
    Middle
}

/// <summary>
/// The state of a mouse button event.
/// </summary>
public enum MouseButtonState
{
    Down,
    Up
}

/// <summary>
/// The state of a keyboard key event.
/// </summary>
public enum KeyboardKeyState
{
    Down,
    Up
}

/// <summary>
/// The finite action types that can be stored in a macro.
/// </summary>
public enum MacroActionKind
{
    MouseMove,
    MouseButton,
    MouseWheel,
    Keyboard
}

/// <summary>
/// An immutable action with a delay from the previous action.
/// </summary>
public abstract record MacroAction(int AfterMilliseconds)
{
    public abstract MacroActionKind Kind { get; }
}

/// <summary>
/// Moves the cursor to a screen coordinate.
/// </summary>
public sealed record MouseMoveAction(
    int AfterMilliseconds,
    int X,
    int Y) : MacroAction(AfterMilliseconds)
{
    public override MacroActionKind Kind => MacroActionKind.MouseMove;
}

/// <summary>
/// Presses or releases a mouse button at a screen coordinate.
/// </summary>
public sealed record MouseButtonAction(
    int AfterMilliseconds,
    int X,
    int Y,
    MouseButtonKind Button,
    MouseButtonState State) : MacroAction(AfterMilliseconds)
{
    public override MacroActionKind Kind => MacroActionKind.MouseButton;
}

/// <summary>
/// Scrolls the mouse wheel at a screen coordinate.
/// </summary>
public sealed record MouseWheelAction(
    int AfterMilliseconds,
    int X,
    int Y,
    int Delta) : MacroAction(AfterMilliseconds)
{
    public override MacroActionKind Kind => MacroActionKind.MouseWheel;
}

/// <summary>
/// Presses or releases a Windows virtual key.
/// </summary>
public sealed record KeyboardAction(
    int AfterMilliseconds,
    ushort VirtualKey,
    string Key,
    KeyboardKeyState State,
    bool IsExtended) : MacroAction(AfterMilliseconds)
{
    public override MacroActionKind Kind => MacroActionKind.Keyboard;
}
