using System.ComponentModel;
using System.Runtime.InteropServices;
using MouseKeyboardMacroRecorder.Core.Application;
using MouseKeyboardMacroRecorder.Core.Domain;

namespace MouseKeyboardMacroRecorder.Infrastructure.Windows;

/// <summary>
/// Captures low-level Windows mouse and keyboard messages on a dedicated message thread.
/// </summary>
public sealed class WindowsInputCapture : IInputCapture
{
    private readonly object _gate = new();
    private CaptureSession? _session;
    private bool _disposed;

    public event EventHandler<InputCaptureErrorEventArgs>? Error;

    public IDisposable Start(RecordingOptions options, Action<CapturedInput> onInput)
    {
        options.EnsureValid();
        ArgumentNullException.ThrowIfNull(onInput);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_session is not null)
            {
                throw new InvalidOperationException("A Windows input capture session is already active.");
            }

            var session = new CaptureSession(options, onInput, HandleError);
            _session = session;
            try
            {
                session.Start();
                return new CaptureSubscription(this, session);
            }
            catch
            {
                _session = null;
                session.Dispose();
                throw;
            }
        }
    }

    public void Dispose()
    {
        CaptureSession? session;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            session = _session;
            _session = null;
        }

        session?.Dispose();
    }

    private void Stop(CaptureSession session)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_session, session))
            {
                _session = null;
            }
        }

        session.Dispose();
    }

    private void HandleError(Exception exception)
    {
        Error?.Invoke(this, new InputCaptureErrorEventArgs(exception));
    }

    private sealed class CaptureSubscription : IDisposable
    {
        private readonly WindowsInputCapture _owner;
        private CaptureSession? _session;

        public CaptureSubscription(WindowsInputCapture owner, CaptureSession session)
        {
            _owner = owner;
            _session = session;
        }

        public void Dispose()
        {
            var session = Interlocked.Exchange(ref _session, null);
            if (session is not null)
            {
                _owner.Stop(session);
            }
        }
    }

    private sealed class CaptureSession : IDisposable
    {
        private readonly RecordingOptions _options;
        private readonly Action<CapturedInput> _onInput;
        private readonly Action<Exception> _onError;
        private readonly ManualResetEventSlim _ready = new(false);
        private readonly object _gate = new();
        private readonly WindowsNativeMethods.HookProcedure _keyboardProcedure;
        private readonly WindowsNativeMethods.HookProcedure _mouseProcedure;
        private Thread? _thread;
        private uint _threadId;
        private IntPtr _keyboardHook;
        private IntPtr _mouseHook;
        private Exception? _startupException;
        private bool _stopRequested;
        private bool _disposed;

        public CaptureSession(
            RecordingOptions options,
            Action<CapturedInput> onInput,
            Action<Exception> onError)
        {
            _options = options;
            _onInput = onInput;
            _onError = onError;
            _keyboardProcedure = KeyboardHook;
            _mouseProcedure = MouseHook;
        }

        public void Start()
        {
            _thread = new Thread(MessageLoop)
            {
                IsBackground = true,
                Name = "MouseKeyboardMacroRecorder.InputCapture"
            };
            _thread.Start();
            if (!_ready.Wait(TimeSpan.FromSeconds(5)))
            {
                Dispose();
                throw new TimeoutException("Windows input capture could not start within five seconds.");
            }

            if (_startupException is not null)
            {
                throw new InputCaptureException("Windows input capture could not start.", _startupException);
            }
        }

        public void Dispose()
        {
            Thread? thread;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _stopRequested = true;
                thread = _thread;
            }

            if (_threadId != 0)
            {
                WindowsNativeMethods.PostThreadMessage(_threadId, WindowsNativeMethods.WmQuit, UIntPtr.Zero, IntPtr.Zero);
            }

            if (thread is not null && thread != Thread.CurrentThread)
            {
                thread.Join(TimeSpan.FromSeconds(2));
            }

            _ready.Dispose();
        }

        private void MessageLoop()
        {
            _threadId = WindowsNativeMethods.GetCurrentThreadId();
            try
            {
                var moduleHandle = WindowsNativeMethods.GetModuleHandle(null);
                if (_options.CaptureKeyboard)
                {
                    _keyboardHook = WindowsNativeMethods.SetWindowsHookEx(
                        WindowsNativeMethods.WhKeyboardLl,
                        _keyboardProcedure,
                        moduleHandle,
                        0);
                    if (_keyboardHook == IntPtr.Zero)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "The keyboard hook could not be installed.");
                    }
                }

                if (_options.CaptureMouse)
                {
                    _mouseHook = WindowsNativeMethods.SetWindowsHookEx(
                        WindowsNativeMethods.WhMouseLl,
                        _mouseProcedure,
                        moduleHandle,
                        0);
                    if (_mouseHook == IntPtr.Zero)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "The mouse hook could not be installed.");
                    }
                }

                _ready.Set();
                while (!_stopRequested)
                {
                    var result = WindowsNativeMethods.GetMessage(
                        out var message,
                        IntPtr.Zero,
                        0,
                        0);
                    if (result <= 0)
                    {
                        break;
                    }

                    WindowsNativeMethods.TranslateMessage(ref message);
                    WindowsNativeMethods.DispatchMessage(ref message);
                }
            }
            catch (Exception exception)
            {
                _startupException = exception;
                _ready.Set();
                _onError(exception);
            }
            finally
            {
                if (_keyboardHook != IntPtr.Zero)
                {
                    WindowsNativeMethods.UnhookWindowsHookEx(_keyboardHook);
                    _keyboardHook = IntPtr.Zero;
                }

                if (_mouseHook != IntPtr.Zero)
                {
                    WindowsNativeMethods.UnhookWindowsHookEx(_mouseHook);
                    _mouseHook = IntPtr.Zero;
                }
            }
        }

        private IntPtr KeyboardHook(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0 && _options.CaptureKeyboard)
            {
                try
                {
                    var message = unchecked((uint)wParam.ToInt64());
                    var data = Marshal.PtrToStructure<WindowsNativeMethods.KeyboardHookStruct>(lParam);
                    var isInjected = (data.Flags & WindowsNativeMethods.LlkhfInjected) != 0
                        || (data.Flags & WindowsNativeMethods.LlkhfLowerIl) != 0;
                    if (!(_options.IgnoreInjectedEvents && isInjected)
                        && message is (WindowsNativeMethods.WmKeyDown
                            or WindowsNativeMethods.WmSysKeyDown
                            or WindowsNativeMethods.WmKeyUp
                            or WindowsNativeMethods.WmSysKeyUp))
                    {
                        var isDown = message is WindowsNativeMethods.WmKeyDown or WindowsNativeMethods.WmSysKeyDown;
                        var virtualKey = checked((ushort)data.VirtualKey);
                        _onInput(new CapturedKeyboard(
                            DateTimeOffset.UtcNow,
                            virtualKey,
                            WindowsKeyNames.GetName(virtualKey, data.ScanCode, (data.Flags & WindowsNativeMethods.LlkhfExtended) != 0),
                            isDown ? KeyboardKeyState.Down : KeyboardKeyState.Up,
                            (data.Flags & WindowsNativeMethods.LlkhfExtended) != 0));
                    }
                }
                catch (Exception exception)
                {
                    _onError(exception);
                }
            }

            return WindowsNativeMethods.CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        }

        private IntPtr MouseHook(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0)
            {
                try
                {
                    var message = unchecked((uint)wParam.ToInt64());
                    var data = Marshal.PtrToStructure<WindowsNativeMethods.MouseHookStruct>(lParam);
                    var isInjected = (data.Flags & WindowsNativeMethods.LlmhfInjected) != 0;
                    if (!(_options.IgnoreInjectedEvents && isInjected))
                    {
                        var timestamp = DateTimeOffset.UtcNow;
                        if (_options.CaptureMouse && message == WindowsNativeMethods.WmMouseMove)
                        {
                            _onInput(new CapturedMouseMove(timestamp, data.Point.X, data.Point.Y));
                        }
                        else if (_options.CaptureMouse && TryGetMouseButton(message, out var button, out var state))
                        {
                            _onInput(new CapturedMouseButton(timestamp, data.Point.X, data.Point.Y, button, state));
                        }
                        else if (_options.CaptureMouse && message == WindowsNativeMethods.WmMouseWheel)
                        {
                            var delta = unchecked((short)(data.MouseData >> 16));
                            if (delta != 0)
                            {
                                _onInput(new CapturedMouseWheel(timestamp, data.Point.X, data.Point.Y, delta));
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    _onError(exception);
                }
            }

            return WindowsNativeMethods.CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
        }

        private static bool TryGetMouseButton(
            uint message,
            out MouseButtonKind button,
            out MouseButtonState state)
        {
            switch (message)
            {
                case WindowsNativeMethods.WmLButtonDown:
                    button = MouseButtonKind.Left;
                    state = MouseButtonState.Down;
                    return true;
                case WindowsNativeMethods.WmLButtonUp:
                    button = MouseButtonKind.Left;
                    state = MouseButtonState.Up;
                    return true;
                case WindowsNativeMethods.WmRButtonDown:
                    button = MouseButtonKind.Right;
                    state = MouseButtonState.Down;
                    return true;
                case WindowsNativeMethods.WmRButtonUp:
                    button = MouseButtonKind.Right;
                    state = MouseButtonState.Up;
                    return true;
                case WindowsNativeMethods.WmMButtonDown:
                    button = MouseButtonKind.Middle;
                    state = MouseButtonState.Down;
                    return true;
                case WindowsNativeMethods.WmMButtonUp:
                    button = MouseButtonKind.Middle;
                    state = MouseButtonState.Up;
                    return true;
                default:
                    button = default;
                    state = default;
                    return false;
            }
        }
    }
}

public sealed class InputCaptureErrorEventArgs : EventArgs
{
    public InputCaptureErrorEventArgs(Exception exception)
    {
        Exception = exception;
    }

    public Exception Exception { get; }
}

public sealed class InputCaptureException : Exception
{
    public InputCaptureException(string message)
        : base(message)
    {
    }

    public InputCaptureException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
