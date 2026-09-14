using System.Text.Json;
using MouseKeyboardMacroRecorder.Core.Domain;

namespace MouseKeyboardMacroRecorder.Core.Infrastructure.Persistence;

/// <summary>
/// Serializes and parses the versioned .macro.json contract.
/// </summary>
public static class MacroJsonSerializer
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    public static string Serialize(MacroDocument document)
    {
        MacroValidator.EnsureValid(document);
        var payload = new
        {
            format = document.Format,
            version = document.Version,
            name = document.Name,
            created_at = document.CreatedAtUtc,
            screen = new
            {
                width = document.Screen.Width,
                height = document.Screen.Height,
                dpi_scale = document.Screen.DpiScale
            },
            actions = document.Actions.Select(SerializeAction).ToArray()
        };
        return JsonSerializer.Serialize(payload, WriteOptions);
    }

    public static MacroDocument Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new MacroFormatException("The macro file is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new MacroFormatException("The macro root must be a JSON object.");
            }
            var format = ReadString(root, "format");
            var version = ReadInt32(root, "version");
            var name = ReadString(root, "name");
            var createdAt = ReadDateTimeOffset(root, "created_at");
            var screenElement = ReadObject(root, "screen");
            var screen = new ScreenInfo(
                ReadInt32(screenElement, "width"),
                ReadInt32(screenElement, "height"),
                ReadDouble(screenElement, "dpi_scale"));
            var actionsElement = ReadArray(root, "actions");
            var actions = actionsElement.EnumerateArray().Select(DeserializeAction).ToArray();
            var result = new MacroDocument(name, createdAt, screen, actions, format, version);
            MacroValidator.EnsureValid(result);
            return result;
        }
        catch (MacroFormatException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new MacroFormatException("The macro file is not valid JSON.", exception);
        }
    }

    private static object SerializeAction(MacroAction action)
    {
        return action switch
        {
            MouseMoveAction mouseMove => new
            {
                type = "mouse_move",
                after_ms = mouseMove.AfterMilliseconds,
                x = mouseMove.X,
                y = mouseMove.Y
            },
            MouseButtonAction mouseButton => new
            {
                type = "mouse_button",
                after_ms = mouseButton.AfterMilliseconds,
                x = mouseButton.X,
                y = mouseButton.Y,
                button = mouseButton.Button.ToString().ToLowerInvariant(),
                state = mouseButton.State.ToString().ToLowerInvariant()
            },
            MouseWheelAction mouseWheel => new
            {
                type = "mouse_wheel",
                after_ms = mouseWheel.AfterMilliseconds,
                x = mouseWheel.X,
                y = mouseWheel.Y,
                delta = mouseWheel.Delta
            },
            KeyboardAction keyboard => new
            {
                type = "keyboard",
                after_ms = keyboard.AfterMilliseconds,
                key = keyboard.Key,
                virtual_key = keyboard.VirtualKey,
                state = keyboard.State.ToString().ToLowerInvariant(),
                extended = keyboard.IsExtended
            },
            _ => throw new MacroFormatException("The macro contains an unsupported action type.")
        };
    }

    private static MacroAction DeserializeAction(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new MacroFormatException("Each macro action must be a JSON object.");
        }

        var type = ReadString(element, "type");
        var afterMilliseconds = ReadInt32(element, "after_ms");
        return type switch
        {
            "mouse_move" => new MouseMoveAction(
                afterMilliseconds,
                ReadInt32(element, "x"),
                ReadInt32(element, "y")),
            "mouse_button" => new MouseButtonAction(
                afterMilliseconds,
                ReadInt32(element, "x"),
                ReadInt32(element, "y"),
                ParseMouseButton(ReadString(element, "button")),
                ParseMouseButtonState(ReadString(element, "state"))),
            "mouse_wheel" => new MouseWheelAction(
                afterMilliseconds,
                ReadInt32(element, "x"),
                ReadInt32(element, "y"),
                ReadInt32(element, "delta")),
            "keyboard" => new KeyboardAction(
                afterMilliseconds,
                ReadUInt16(element, "virtual_key"),
                ReadString(element, "key"),
                ParseKeyboardState(ReadString(element, "state")),
                ReadBoolean(element, "extended")),
            _ => throw new MacroFormatException($"Action type '{type}' is not supported.")
        };
    }

    private static JsonElement ReadObject(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            throw new MacroFormatException($"The '{propertyName}' object is required.");
        }

        return value;
    }

    private static JsonElement ReadArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new MacroFormatException($"The '{propertyName}' array is required.");
        }

        return value;
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new MacroFormatException($"The '{propertyName}' text value is required.");
        }

        return value.GetString() ?? string.Empty;
    }

    private static int ReadInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || !value.TryGetInt32(out var result))
        {
            throw new MacroFormatException($"The '{propertyName}' integer value is invalid.");
        }

        return result;
    }

    private static ushort ReadUInt16(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || !value.TryGetUInt16(out var result))
        {
            throw new MacroFormatException($"The '{propertyName}' key code is invalid.");
        }

        return result;
    }

    private static double ReadDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || !value.TryGetDouble(out var result))
        {
            throw new MacroFormatException($"The '{propertyName}' number value is invalid.");
        }

        return result;
    }

    private static bool ReadBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
        {
            throw new MacroFormatException($"The '{propertyName}' boolean value is invalid.");
        }

        return value.GetBoolean();
    }

    private static DateTimeOffset ReadDateTimeOffset(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.String
            || !value.TryGetDateTimeOffset(out var result))
        {
            throw new MacroFormatException($"The '{propertyName}' timestamp is invalid.");
        }

        return result;
    }

    private static MouseButtonKind ParseMouseButton(string value)
    {
        return value switch
        {
            "left" => MouseButtonKind.Left,
            "right" => MouseButtonKind.Right,
            "middle" => MouseButtonKind.Middle,
            _ => throw new MacroFormatException($"Mouse button '{value}' is not supported.")
        };
    }

    private static MouseButtonState ParseMouseButtonState(string value)
    {
        return value switch
        {
            "down" => MouseButtonState.Down,
            "up" => MouseButtonState.Up,
            _ => throw new MacroFormatException($"Mouse button state '{value}' is not supported.")
        };
    }

    private static KeyboardKeyState ParseKeyboardState(string value)
    {
        return value switch
        {
            "down" => KeyboardKeyState.Down,
            "up" => KeyboardKeyState.Up,
            _ => throw new MacroFormatException($"Keyboard state '{value}' is not supported.")
        };
    }
}

/// <summary>
/// Indicates that a macro file does not satisfy the versioned contract.
/// </summary>
public sealed class MacroFormatException : Exception
{
    public MacroFormatException(string message)
        : base(message)
    {
    }

    public MacroFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
