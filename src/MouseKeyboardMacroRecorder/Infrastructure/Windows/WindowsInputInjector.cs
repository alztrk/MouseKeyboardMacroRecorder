using System.ComponentModel;
using System.Runtime.InteropServices;
using MouseKeyboardMacroRecorder.Core.Application;
using MouseKeyboardMacroRecorder.Core.Domain;

namespace MouseKeyboardMacroRecorder.Infrastructure.Windows;

/// <summary>
/// Sends Windows input through SendInput and tracks keys/buttons for emergency release.
/// </summary>
public sealed class WindowsInputInjector : IInputInjector
{
    private readonly object _gate = new();
    private readonly HashSet<MouseButtonKind> _heldMouseButtons = new();
    private readonly HashSet<ushort> _heldKeys = new();
    private bool _disposed;

    public void MoveMouse(int x, int y)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            var left = WindowsNativeMethods.GetSystemMetrics(WindowsNativeMethods.SmXVirtualScreen);
            var top = WindowsNativeMethods.GetSystemMetrics(WindowsNativeMethods.SmYVirtualScreen);
            var width = Math.Max(1, WindowsNativeMethods.GetSystemMetrics(WindowsNativeMethods.SmCxVirtualScreen));
            var height = Math.Max(1, WindowsNativeMethods.GetSystemMetrics(WindowsNativeMethods.SmCyVirtualScreen));
            var normalizedX = Normalize(x, left, width);
            var normalizedY = Normalize(y, top, height);
            Send(new WindowsNativeMethods.Input
            {
                Type = WindowsNativeMethods.InputMouse,
                Data = new WindowsNativeMethods.InputUnion
                {
                    Mouse = new WindowsNativeMethods.MouseInput
                    {
                        X = normalizedX,
                        Y = normalizedY,
                        Flags = WindowsNativeMethods.MouseEventMove
                            | WindowsNativeMethods.MouseEventAbsolute
                            | WindowsNativeMethods.MouseEventVirtualDesk
                    }
                }
            });
        }
    }

    public void MouseButton(MouseButtonKind button, MouseButtonState state)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            var flags = (button, state) switch
            {
                (MouseButtonKind.Left, MouseButtonState.Down) => WindowsNativeMethods.MouseEventLeftDown,
                (MouseButtonKind.Left, MouseButtonState.Up) => WindowsNativeMethods.MouseEventLeftUp,
                (MouseButtonKind.Right, MouseButtonState.Down) => WindowsNativeMethods.MouseEventRightDown,
                (MouseButtonKind.Right, MouseButtonState.Up) => WindowsNativeMethods.MouseEventRightUp,
                (MouseButtonKind.Middle, MouseButtonState.Down) => WindowsNativeMethods.MouseEventMiddleDown,
                (MouseButtonKind.Middle, MouseButtonState.Up) => WindowsNativeMethods.MouseEventMiddleUp,
                _ => throw new ArgumentOutOfRangeException(nameof(button))
            };

            Send(new WindowsNativeMethods.Input
            {
                Type = WindowsNativeMethods.InputMouse,
                Data = new WindowsNativeMethods.InputUnion
                {
                    Mouse = new WindowsNativeMethods.MouseInput { Flags = flags }
                }
            });

            if (state == MouseButtonState.Down)
            {
                _heldMouseButtons.Add(button);
            }
            else
            {
                _heldMouseButtons.Remove(button);
            }
        }
    }

    public void MouseWheel(int delta)
    {
        if (delta == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), "Mouse wheel delta cannot be zero.");
        }

        lock (_gate)
        {
            ThrowIfDisposed();
            Send(new WindowsNativeMethods.Input
            {
                Type = WindowsNativeMethods.InputMouse,
                Data = new WindowsNativeMethods.InputUnion
                {
                    Mouse = new WindowsNativeMethods.MouseInput
                    {
                        MouseData = unchecked((uint)delta),
                        Flags = WindowsNativeMethods.MouseEventWheel
                    }
                }
            });
        }
    }

    public void Keyboard(ushort virtualKey, KeyboardKeyState state, bool isExtended)
    {
        if (virtualKey == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(virtualKey), "Virtual key cannot be zero.");
        }

        lock (_gate)
        {
            ThrowIfDisposed();
            var flags = state == KeyboardKeyState.Up
                ? WindowsNativeMethods.KeyboardEventKeyUp
                : 0;
            if (isExtended)
            {
                flags |= WindowsNativeMethods.KeyboardEventExtendedKey;
            }

            Send(new WindowsNativeMethods.Input
            {
                Type = WindowsNativeMethods.InputKeyboard,
                Data = new WindowsNativeMethods.InputUnion
                {
                    Keyboard = new WindowsNativeMethods.KeyboardInput
                    {
                        VirtualKey = virtualKey,
                        Flags = flags
                    }
                }
            });

            if (state == KeyboardKeyState.Down)
            {
                _heldKeys.Add(virtualKey);
            }
            else
            {
                _heldKeys.Remove(virtualKey);
            }
        }
    }

    public void ReleaseAll()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            ReleaseAllCore();
        }
    }

    private void ReleaseAllCore()
    {
        var mouseButtons = _heldMouseButtons.ToList();
        var keys = _heldKeys.ToList();
        _heldMouseButtons.Clear();
        _heldKeys.Clear();
        var errors = new List<Exception>();
        foreach (var button in mouseButtons)
        {
            try
            {
                SendMouseButtonRelease(button);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        foreach (var key in keys)
        {
            try
            {
                SendKeyboardRelease(key);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        if (errors.Count > 0)
        {
            throw new InputInjectionException("One or more held inputs could not be released.", new AggregateException(errors));
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                ReleaseAllCore();
            }
            finally
            {
                _disposed = true;
            }
        }
    }

    private static void SendMouseButtonRelease(MouseButtonKind button)
    {
        var flags = button switch
        {
            MouseButtonKind.Left => WindowsNativeMethods.MouseEventLeftUp,
            MouseButtonKind.Right => WindowsNativeMethods.MouseEventRightUp,
            MouseButtonKind.Middle => WindowsNativeMethods.MouseEventMiddleUp,
            _ => throw new ArgumentOutOfRangeException(nameof(button))
        };
        Send(new WindowsNativeMethods.Input
        {
            Type = WindowsNativeMethods.InputMouse,
            Data = new WindowsNativeMethods.InputUnion
            {
                Mouse = new WindowsNativeMethods.MouseInput { Flags = flags }
            }
        });
    }

    private static void SendKeyboardRelease(ushort virtualKey)
    {
        Send(new WindowsNativeMethods.Input
        {
            Type = WindowsNativeMethods.InputKeyboard,
            Data = new WindowsNativeMethods.InputUnion
            {
                Keyboard = new WindowsNativeMethods.KeyboardInput
                {
                    VirtualKey = virtualKey,
                    Flags = WindowsNativeMethods.KeyboardEventKeyUp
                }
            }
        });
    }

    private static void Send(WindowsNativeMethods.Input input)
    {
        var inputs = new[] { input };
        var sent = WindowsNativeMethods.SendInput(
            1,
            inputs,
            Marshal.SizeOf<WindowsNativeMethods.Input>());
        if (sent != 1)
        {
            throw new InputInjectionException(
                "Windows rejected the synthetic input.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }
    }

    private static int Normalize(int value, int origin, int extent)
    {
        var relative = Math.Clamp((long)value - origin, 0, Math.Max(0, (long)extent - 1));
        return (int)Math.Round(relative * 65535d / Math.Max(1, extent - 1));
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

public sealed class InputInjectionException : Exception
{
    public InputInjectionException(string message)
        : base(message)
    {
    }

    public InputInjectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
