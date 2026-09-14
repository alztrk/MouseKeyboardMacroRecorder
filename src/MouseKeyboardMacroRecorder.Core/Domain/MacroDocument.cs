namespace MouseKeyboardMacroRecorder.Core.Domain;

/// <summary>
/// Screen metadata captured when a macro is recorded.
/// </summary>
public sealed record ScreenInfo(int Width, int Height, double DpiScale);

/// <summary>
/// A versioned, portable macro document.
/// </summary>
public sealed record MacroDocument
{
    public const string RequiredFormat = "mouse-keyboard-macro";
    public const int CurrentVersion = 1;

    public MacroDocument(
        string name,
        DateTimeOffset createdAtUtc,
        ScreenInfo screen,
        IEnumerable<MacroAction> actions,
        string format = RequiredFormat,
        int version = CurrentVersion)
    {
        Name = name;
        CreatedAtUtc = createdAtUtc;
        Screen = screen ?? throw new ArgumentNullException(nameof(screen));
        Actions = (actions ?? throw new ArgumentNullException(nameof(actions))).ToArray();
        Format = format;
        Version = version;
    }

    public string Format { get; init; }

    public int Version { get; init; }

    public string Name { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public ScreenInfo Screen { get; init; }

    public IReadOnlyList<MacroAction> Actions { get; init; }
}

/// <summary>
/// Provides deterministic validation for macro documents.
/// </summary>
public static class MacroValidator
{
    public static IReadOnlyList<string> Validate(MacroDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var errors = new List<string>();
        if (!string.Equals(document.Format, MacroDocument.RequiredFormat, StringComparison.Ordinal))
        {
            errors.Add("The macro format is not supported.");
        }

        if (document.Version != MacroDocument.CurrentVersion)
        {
            errors.Add($"Macro version {document.Version} is not supported.");
        }

        if (string.IsNullOrWhiteSpace(document.Name))
        {
            errors.Add("A macro name is required.");
        }
        else if (document.Name.Length > 200)
        {
            errors.Add("The macro name cannot exceed 200 characters.");
        }

        if (document.Screen.Width <= 0 || document.Screen.Height <= 0)
        {
            errors.Add("Screen dimensions must be positive.");
        }

        if (double.IsNaN(document.Screen.DpiScale)
            || double.IsInfinity(document.Screen.DpiScale)
            || document.Screen.DpiScale <= 0
            || document.Screen.DpiScale > 8)
        {
            errors.Add("Screen DPI scale must be between 0 and 8.");
        }

        if (document.Actions.Count > AutomationLimits.MaximumMacroActions)
        {
            errors.Add($"A macro cannot contain more than {AutomationLimits.MaximumMacroActions} actions.");
        }

        var heldMouseButtons = new HashSet<MouseButtonKind>();
        var heldKeys = new HashSet<ushort>();
        for (var index = 0; index < document.Actions.Count; index++)
        {
            var action = document.Actions[index];
            if (action is null)
            {
                errors.Add($"Action {index + 1} is missing.");
                continue;
            }

            if (action.AfterMilliseconds < 0 || action.AfterMilliseconds > AutomationLimits.MaximumActionDelayMilliseconds)
            {
                errors.Add($"Action {index + 1} has an invalid delay.");
            }

            switch (action)
            {
                case MouseButtonAction mouseButton:
                    ValidateMouseButton(mouseButton, index, heldMouseButtons, errors);
                    break;
                case MouseWheelAction mouseWheel when mouseWheel.Delta == 0:
                    errors.Add($"Action {index + 1} has an empty wheel delta.");
                    break;
                case KeyboardAction keyboard when keyboard.VirtualKey == 0 || string.IsNullOrWhiteSpace(keyboard.Key):
                    errors.Add($"Action {index + 1} has an invalid keyboard key.");
                    break;
                case not MouseMoveAction and not MouseButtonAction and not MouseWheelAction and not KeyboardAction:
                    errors.Add($"Action {index + 1} has an unknown type.");
                    break;
            }

            if (action is KeyboardAction validKeyboard && validKeyboard.VirtualKey != 0 && !string.IsNullOrWhiteSpace(validKeyboard.Key))
            {
                ValidateKeyboard(validKeyboard, index, heldKeys, errors);
            }
        }

        if (heldMouseButtons.Count > 0)
        {
            errors.Add("The macro ends with a mouse button held down.");
        }

        if (heldKeys.Count > 0)
        {
            errors.Add("The macro ends with a keyboard key held down.");
        }

        return errors;
    }

    public static void EnsureValid(MacroDocument document)
    {
        var errors = Validate(document);
        if (errors.Count > 0)
        {
            throw new MacroValidationException(errors);
        }
    }

    private static void ValidateMouseButton(
        MouseButtonAction action,
        int index,
        HashSet<MouseButtonKind> heldButtons,
        List<string> errors)
    {
        if (action.State == MouseButtonState.Down && !heldButtons.Add(action.Button))
        {
            errors.Add($"Action {index + 1} presses an already held mouse button.");
        }

        if (action.State == MouseButtonState.Up && !heldButtons.Remove(action.Button))
        {
            errors.Add($"Action {index + 1} releases a mouse button that is not held.");
        }
    }

    private static void ValidateKeyboard(
        KeyboardAction action,
        int index,
        HashSet<ushort> heldKeys,
        List<string> errors)
    {
        if (action.State == KeyboardKeyState.Down && !heldKeys.Add(action.VirtualKey))
        {
            errors.Add($"Action {index + 1} presses an already held keyboard key.");
        }

        if (action.State == KeyboardKeyState.Up && !heldKeys.Remove(action.VirtualKey))
        {
            errors.Add($"Action {index + 1} releases a keyboard key that is not held.");
        }
    }
}

/// <summary>
/// Describes one or more invalid macro document fields.
/// </summary>
public sealed class MacroValidationException : Exception
{
    public MacroValidationException(IReadOnlyList<string> errors)
        : base(string.Join(" ", errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
