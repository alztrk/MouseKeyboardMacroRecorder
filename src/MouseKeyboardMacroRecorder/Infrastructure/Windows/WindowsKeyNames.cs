using System.Text;

namespace MouseKeyboardMacroRecorder.Infrastructure.Windows;

internal static class WindowsKeyNames
{
    private static readonly Dictionary<ushort, string> WellKnownNames =
        new Dictionary<ushort, string>
        {
            [0x08] = "Backspace",
            [0x09] = "Tab",
            [0x0D] = "Enter",
            [0x10] = "Shift",
            [0x11] = "Ctrl",
            [0x12] = "Alt",
            [0x13] = "Pause",
            [0x14] = "Caps Lock",
            [0x1B] = "Esc",
            [0x20] = "Space",
            [0x21] = "Page Up",
            [0x22] = "Page Down",
            [0x23] = "End",
            [0x24] = "Home",
            [0x25] = "Left Arrow",
            [0x26] = "Up Arrow",
            [0x27] = "Right Arrow",
            [0x28] = "Down Arrow",
            [0x2D] = "Insert",
            [0x2E] = "Delete",
            [0x5B] = "Left Win",
            [0x5C] = "Right Win",
            [0x5D] = "Apps",
            [0x6A] = "Numpad *",
            [0x6B] = "Numpad +",
            [0x6D] = "Numpad -",
            [0x6E] = "Numpad .",
            [0x6F] = "Numpad /",
            [0x90] = "Num Lock",
            [0x91] = "Scroll Lock",
            [0xA0] = "Left Shift",
            [0xA1] = "Right Shift",
            [0xA2] = "Left Ctrl",
            [0xA3] = "Right Ctrl",
            [0xA4] = "Left Alt",
            [0xA5] = "Right Alt",
            [0xBA] = ";",
            [0xBB] = "=",
            [0xBC] = ",",
            [0xBD] = "-",
            [0xBE] = ".",
            [0xBF] = "/",
            [0xC0] = "`",
            [0xDB] = "[",
            [0xDC] = "\\",
            [0xDD] = "]",
            [0xDE] = "'"
        };

    public static string GetName(ushort virtualKey, uint scanCode, bool isExtended)
    {
        if (WellKnownNames.TryGetValue(virtualKey, out var knownName))
        {
            return knownName;
        }

        if (virtualKey is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A)
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= 0x70 and <= 0x87)
        {
            return $"F{virtualKey - 0x6F}";
        }

        var name = new StringBuilder(64);
        var lParam = unchecked((int)(scanCode << 16));
        if (isExtended)
        {
            lParam |= 1 << 24;
        }

        if (WindowsNativeMethods.GetKeyNameText(lParam, name, name.Capacity) > 0)
        {
            return name.ToString();
        }

        return $"VK 0x{virtualKey:X2}";
    }
}
