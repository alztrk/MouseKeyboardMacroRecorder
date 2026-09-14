using System.ComponentModel;
using System.Runtime.InteropServices;
using MouseKeyboardMacroRecorder.Core.Application;

namespace MouseKeyboardMacroRecorder.Infrastructure.Windows;

/// <summary>
/// Registers global hotkeys on a dedicated message thread.
/// </summary>
public sealed class WindowsGlobalHotkeyService : IGlobalHotkeyService
{
    private readonly object _gate = new();
    private HotkeyRegistration? _registration;
    private bool _disposed;

    public event EventHandler<HotkeyPressedEventArgs>? Pressed;

    public IDisposable Register(IReadOnlyList<HotkeyBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        if (bindings.Count == 0)
        {
            throw new ArgumentException("At least one global hotkey is required.", nameof(bindings));
        }

        var normalized = bindings.ToArray();
        ValidateBindings(normalized);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_registration is not null)
            {
                throw new InvalidOperationException("Global hotkeys are already registered.");
            }

            var registration = new HotkeyRegistration(normalized, RaisePressed);
            _registration = registration;
            try
            {
                registration.Start();
                return new RegistrationSubscription(this, registration);
            }
            catch
            {
                _registration = null;
                registration.Dispose();
                throw;
            }
        }
    }

    public void Dispose()
    {
        HotkeyRegistration? registration;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            registration = _registration;
            _registration = null;
        }

        registration?.Dispose();
    }

    private void Stop(HotkeyRegistration registration)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_registration, registration))
            {
                _registration = null;
            }
        }

        registration.Dispose();
    }

    private void RaisePressed(HotkeyCommand command)
    {
        Pressed?.Invoke(this, new HotkeyPressedEventArgs(command));
    }

    private static void ValidateBindings(IEnumerable<HotkeyBinding> bindings)
    {
        var commands = new HashSet<HotkeyCommand>();
        foreach (var binding in bindings)
        {
            if (!commands.Add(binding.Command))
            {
                throw new ArgumentException("Each hotkey command may only be registered once.");
            }

            if (binding.VirtualKey == 0)
            {
                throw new ArgumentException("A hotkey virtual key cannot be zero.");
            }

            if (((uint)binding.Modifiers & ~0x000Fu) != 0)
            {
                throw new ArgumentException("The hotkey contains unsupported modifiers.");
            }
        }
    }

    private sealed class RegistrationSubscription : IDisposable
    {
        private readonly WindowsGlobalHotkeyService _owner;
        private HotkeyRegistration? _registration;

        public RegistrationSubscription(WindowsGlobalHotkeyService owner, HotkeyRegistration registration)
        {
            _owner = owner;
            _registration = registration;
        }

        public void Dispose()
        {
            var registration = Interlocked.Exchange(ref _registration, null);
            if (registration is not null)
            {
                _owner.Stop(registration);
            }
        }
    }

    private sealed class HotkeyRegistration : IDisposable
    {
        private readonly IReadOnlyList<HotkeyBinding> _bindings;
        private readonly Action<HotkeyCommand> _onPressed;
        private readonly ManualResetEventSlim _ready = new(false);
        private readonly Dictionary<int, HotkeyCommand> _commandsById = new();
        private readonly List<int> _registeredIds = new();
        private readonly object _gate = new();
        private Thread? _thread;
        private uint _threadId;
        private Exception? _startupException;
        private bool _stopRequested;
        private bool _disposed;

        public HotkeyRegistration(
            IReadOnlyList<HotkeyBinding> bindings,
            Action<HotkeyCommand> onPressed)
        {
            _bindings = bindings;
            _onPressed = onPressed;
        }

        public void Start()
        {
            _thread = new Thread(MessageLoop)
            {
                IsBackground = true,
                Name = "MouseKeyboardMacroRecorder.GlobalHotkeys"
            };
            _thread.Start();
            if (!_ready.Wait(TimeSpan.FromSeconds(5)))
            {
                Dispose();
                throw new TimeoutException("Global hotkeys could not be registered within five seconds.");
            }

            if (_startupException is not null)
            {
                throw new GlobalHotkeyException("One or more global hotkeys are unavailable.", _startupException);
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
                WindowsNativeMethods.PeekMessage(
                    out _,
                    IntPtr.Zero,
                    0,
                    0,
                    0);

                for (var index = 0; index < _bindings.Count; index++)
                {
                    var binding = _bindings[index];
                    var id = index + 1;
                    var modifiers = (uint)binding.Modifiers | WindowsNativeMethods.ModNoRepeat;
                    if (!WindowsNativeMethods.RegisterHotKey(IntPtr.Zero, id, modifiers, binding.VirtualKey))
                    {
                        var error = Marshal.GetLastWin32Error();
                        throw new Win32Exception(error, $"Hotkey {HotkeyFormatting.Describe(binding)} is unavailable.");
                    }

                    _commandsById[id] = binding.Command;
                    _registeredIds.Add(id);
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

                    if (message.MessageId == WindowsNativeMethods.WmHotkey
                        && _commandsById.TryGetValue(unchecked((int)message.WParam.ToUInt64()), out var command))
                    {
                        _onPressed(command);
                    }
                }
            }
            catch (Exception exception)
            {
                _startupException = exception;
                _ready.Set();
            }
            finally
            {
                foreach (var id in _registeredIds)
                {
                    WindowsNativeMethods.UnregisterHotKey(IntPtr.Zero, id);
                }

                _registeredIds.Clear();
                _commandsById.Clear();
            }
        }
    }
}

internal static class HotkeyFormatting
{
    public static string Describe(HotkeyBinding binding)
    {
        var modifierText = new List<string>();
        if (binding.Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            modifierText.Add("Ctrl");
        }

        if (binding.Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            modifierText.Add("Alt");
        }

        if (binding.Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            modifierText.Add("Shift");
        }

        if (binding.Modifiers.HasFlag(HotkeyModifiers.Windows))
        {
            modifierText.Add("Win");
        }

        modifierText.Add(WindowsKeyNames.GetName((ushort)binding.VirtualKey, 0, false));
        return string.Join("+", modifierText);
    }
}

public sealed class GlobalHotkeyException : Exception
{
    public GlobalHotkeyException(string message)
        : base(message)
    {
    }

    public GlobalHotkeyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
