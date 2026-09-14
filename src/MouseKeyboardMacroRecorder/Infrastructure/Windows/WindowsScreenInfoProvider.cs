using MouseKeyboardMacroRecorder.Core.Application;
using MouseKeyboardMacroRecorder.Core.Domain;

namespace MouseKeyboardMacroRecorder.Infrastructure.Windows;

public sealed class WindowsScreenInfoProvider : IScreenInfoProvider
{
    public ScreenInfo GetCurrent()
    {
        var width = Math.Max(1, WindowsNativeMethods.GetSystemMetrics(WindowsNativeMethods.SmCxVirtualScreen));
        var height = Math.Max(1, WindowsNativeMethods.GetSystemMetrics(WindowsNativeMethods.SmCyVirtualScreen));
        var dpi = WindowsNativeMethods.GetDpiForSystem();
        var dpiScale = dpi == 0 ? 1d : dpi / 96d;
        return new ScreenInfo(width, height, dpiScale);
    }
}

public sealed class WindowsCursorPositionProvider : ICursorPositionProvider
{
    public (int X, int Y) GetPosition()
    {
        if (!WindowsNativeMethods.GetCursorPos(out var point))
        {
            throw new InvalidOperationException("The current cursor position could not be read.");
        }

        return (point.X, point.Y);
    }
}
