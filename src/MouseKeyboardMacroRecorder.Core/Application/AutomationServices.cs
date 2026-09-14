using System.Diagnostics;
using MouseKeyboardMacroRecorder.Core.Domain;

namespace MouseKeyboardMacroRecorder.Core.Application;

/// <summary>
/// Records captured input into a validated macro document.
/// </summary>
public sealed class MacroRecorderService : IDisposable
{
    private readonly IInputCapture _capture;
    private readonly IScreenInfoProvider _screenInfo;
    private readonly object _gate = new();
    private readonly List<MacroAction> _actions = new();
    private readonly HashSet<MouseButtonKind> _heldMouseButtons = new();
    private readonly HashSet<ushort> _heldKeys = new();
    private IDisposable? _captureSubscription;
    private DateTimeOffset? _lastTimestampUtc;
    private bool _isRecording;

    public MacroRecorderService(IInputCapture capture, IScreenInfoProvider screenInfo)
    {
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _screenInfo = screenInfo ?? throw new ArgumentNullException(nameof(screenInfo));
    }

    public bool IsRecording
    {
        get
        {
            lock (_gate)
            {
                return _isRecording;
            }
        }
    }

    public int ActionCount
    {
        get
        {
            lock (_gate)
            {
                return _actions.Count;
            }
        }
    }

    public void Start(RecordingOptions options)
    {
        options.EnsureValid();
        lock (_gate)
        {
            if (_isRecording)
            {
                throw new InvalidOperationException("A recording is already active.");
            }

            _actions.Clear();
            _heldMouseButtons.Clear();
            _heldKeys.Clear();
            _lastTimestampUtc = null;
            _isRecording = true;
        }

        try
        {
            var subscription = _capture.Start(options, OnCapturedInput);
            lock (_gate)
            {
                if (!_isRecording)
                {
                    subscription.Dispose();
                    return;
                }

                _captureSubscription = subscription;
            }
        }
        catch
        {
            lock (_gate)
            {
                _isRecording = false;
                _lastTimestampUtc = null;
            }

            throw;
        }
    }

    public MacroDocument Stop(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        IDisposable? subscription;
        MacroAction[] actions;
        lock (_gate)
        {
            if (!_isRecording)
            {
                throw new InvalidOperationException("No recording is active.");
            }

            _isRecording = false;
            subscription = _captureSubscription;
            _captureSubscription = null;
            actions = _actions.ToArray();
            _heldMouseButtons.Clear();
            _heldKeys.Clear();
        }

        subscription?.Dispose();
        var document = new MacroDocument(
            name.Trim(),
            DateTimeOffset.UtcNow,
            _screenInfo.GetCurrent(),
            actions);
        MacroValidator.EnsureValid(document);
        return document;
    }

    public void Cancel()
    {
        IDisposable? subscription;
        lock (_gate)
        {
            if (!_isRecording)
            {
                return;
            }

            _isRecording = false;
            _lastTimestampUtc = null;
            _actions.Clear();
            _heldMouseButtons.Clear();
            _heldKeys.Clear();
            subscription = _captureSubscription;
            _captureSubscription = null;
        }

        subscription?.Dispose();
    }

    public void Dispose()
    {
        Cancel();
        _capture.Dispose();
    }

    private void OnCapturedInput(CapturedInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        lock (_gate)
        {
            if (!_isRecording)
            {
                return;
            }

            var afterMilliseconds = _lastTimestampUtc is null
                ? 0
                : (int)Math.Clamp(
                    Math.Round((input.TimestampUtc - _lastTimestampUtc.Value).TotalMilliseconds),
                    0,
                    int.MaxValue);
            if (input is CapturedKeyboard keyboard
                && !TrackKeyboardState(keyboard))
            {
                _lastTimestampUtc = input.TimestampUtc;
                return;
            }

            if (input is CapturedMouseButton mouseButton
                && !TrackMouseButtonState(mouseButton))
            {
                _lastTimestampUtc = input.TimestampUtc;
                return;
            }

            var action = ConvertToAction(input, afterMilliseconds);
            if (action is not null)
            {
                _actions.Add(action);
            }

            _lastTimestampUtc = input.TimestampUtc;
        }
    }

    private bool TrackMouseButtonState(CapturedMouseButton input)
    {
        return input.State == MouseButtonState.Down
            ? _heldMouseButtons.Add(input.Button)
            : _heldMouseButtons.Remove(input.Button);
    }

    private bool TrackKeyboardState(CapturedKeyboard input)
    {
        return input.State == KeyboardKeyState.Down
            ? _heldKeys.Add(input.VirtualKey)
            : _heldKeys.Remove(input.VirtualKey);
    }

    private static MacroAction? ConvertToAction(CapturedInput input, int afterMilliseconds)
    {
        return input switch
        {
            CapturedMouseMove mouseMove => new MouseMoveAction(afterMilliseconds, mouseMove.X, mouseMove.Y),
            CapturedMouseButton mouseButton => new MouseButtonAction(
                afterMilliseconds,
                mouseButton.X,
                mouseButton.Y,
                mouseButton.Button,
                mouseButton.State),
            CapturedMouseWheel mouseWheel => new MouseWheelAction(
                afterMilliseconds,
                mouseWheel.X,
                mouseWheel.Y,
                mouseWheel.Delta),
            CapturedKeyboard keyboard => new KeyboardAction(
                afterMilliseconds,
                keyboard.VirtualKey,
                keyboard.Key,
                keyboard.State,
                keyboard.IsExtended),
            _ => throw new InvalidOperationException("The input capture adapter returned an unknown event.")
        };
    }
}

/// <summary>
/// An async pause gate that can cancel a pending delay without losing its remaining duration.
/// </summary>
public sealed class PlaybackPauseGate : IDisposable
{
    private readonly object _gate = new();
    private TaskCompletionSource<bool> _resume = CompletedSource();
    private CancellationTokenSource _pauseCancellation = new();
    private bool _isPaused;

    public bool IsPaused
    {
        get
        {
            lock (_gate)
            {
                return _isPaused;
            }
        }
    }

    public CancellationToken PauseCancellationToken
    {
        get
        {
            lock (_gate)
            {
                return _pauseCancellation.Token;
            }
        }
    }

    public void Pause()
    {
        lock (_gate)
        {
            if (_isPaused)
            {
                return;
            }

            _isPaused = true;
            _resume = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pauseCancellation.Cancel();
        }
    }

    public void Resume()
    {
        lock (_gate)
        {
            if (!_isPaused)
            {
                return;
            }

            _isPaused = false;
            _pauseCancellation.Dispose();
            _pauseCancellation = new CancellationTokenSource();
            _resume.TrySetResult(true);
        }
    }

    public Task WaitIfPausedAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return _isPaused
                ? _resume.Task.WaitAsync(cancellationToken)
                : Task.CompletedTask;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _pauseCancellation.Dispose();
            _resume.TrySetCanceled();
        }
    }

    private static TaskCompletionSource<bool> CompletedSource()
    {
        var source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        source.SetResult(true);
        return source;
    }
}

/// <summary>
/// Replays a macro through the platform-independent injection boundary.
/// </summary>
public sealed class MacroPlaybackService
{
    private readonly IInputInjector _injector;
    private readonly IAsyncDelay _delay;

    public MacroPlaybackService(IInputInjector injector, IAsyncDelay delay)
    {
        _injector = injector ?? throw new ArgumentNullException(nameof(injector));
        _delay = delay ?? throw new ArgumentNullException(nameof(delay));
    }

    public event EventHandler<PlaybackProgressEventArgs>? ProgressChanged;

    public async Task PlayAsync(
        MacroDocument document,
        PlaybackOptions options,
        PlaybackPauseGate pauseGate,
        CancellationToken cancellationToken)
    {
        MacroValidator.EnsureValid(document);
        options.EnsureValid();
        ArgumentNullException.ThrowIfNull(pauseGate);

        var loops = options.RepeatMode == RepeatMode.UntilStopped
            ? int.MaxValue
            : options.RepeatMode == RepeatMode.FixedCount ? options.RepeatCount : 1;

        try
        {
            for (var loop = 0; loop < loops; loop++)
            {
                if (loop > 0)
                {
                    await DelayWithPauseAsync(
                        options.InterLoopDelayMilliseconds,
                        options.Speed,
                        pauseGate,
                        cancellationToken);
                }

                for (var actionIndex = 0; actionIndex < document.Actions.Count; actionIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var action = document.Actions[actionIndex];
                    await DelayWithPauseAsync(action.AfterMilliseconds, options.Speed, pauseGate, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    Apply(action);
                    ProgressChanged?.Invoke(
                        this,
                        new PlaybackProgressEventArgs(loop + 1, loops, actionIndex + 1, document.Actions.Count));
                }
            }
        }
        finally
        {
            _injector.ReleaseAll();
        }
    }

    private async Task DelayWithPauseAsync(
        int milliseconds,
        double speed,
        PlaybackPauseGate pauseGate,
        CancellationToken cancellationToken)
    {
        var remaining = TimeSpan.FromMilliseconds(milliseconds / speed);
        while (remaining > TimeSpan.Zero)
        {
            await pauseGate.WaitIfPausedAsync(cancellationToken);
            var pauseToken = pauseGate.PauseCancellationToken;
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                pauseToken);
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await _delay.DelayAsync(remaining, linkedCancellation.Token);
                return;
            }
            catch (OperationCanceledException) when (pauseGate.IsPaused && pauseToken.IsCancellationRequested)
            {
                remaining -= stopwatch.Elapsed;
            }
        }
    }

    private void Apply(MacroAction action)
    {
        switch (action)
        {
            case MouseMoveAction mouseMove:
                _injector.MoveMouse(mouseMove.X, mouseMove.Y);
                break;
            case MouseButtonAction mouseButton:
                _injector.MoveMouse(mouseButton.X, mouseButton.Y);
                _injector.MouseButton(mouseButton.Button, mouseButton.State);
                break;
            case MouseWheelAction mouseWheel:
                _injector.MoveMouse(mouseWheel.X, mouseWheel.Y);
                _injector.MouseWheel(mouseWheel.Delta);
                break;
            case KeyboardAction keyboard:
                _injector.Keyboard(keyboard.VirtualKey, keyboard.State, keyboard.IsExtended);
                break;
            default:
                throw new InvalidOperationException("The macro contains an unknown action type.");
        }
    }
}

/// <summary>
/// Progress information for UI status text and accessibility announcements.
/// </summary>
public sealed class PlaybackProgressEventArgs : EventArgs
{
    public PlaybackProgressEventArgs(int loop, int totalLoops, int action, int totalActions)
    {
        Loop = loop;
        TotalLoops = totalLoops;
        Action = action;
        TotalActions = totalActions;
    }

    public int Loop { get; }

    public int TotalLoops { get; }

    public int Action { get; }

    public int TotalActions { get; }
}

/// <summary>
/// Runs repeated mouse clicks with cancellation and guaranteed release.
/// </summary>
public sealed class AutoClickerService
{
    private readonly IInputInjector _injector;
    private readonly ICursorPositionProvider _cursor;
    private readonly IAsyncDelay _delay;

    public AutoClickerService(
        IInputInjector injector,
        ICursorPositionProvider cursor,
        IAsyncDelay delay)
    {
        _injector = injector ?? throw new ArgumentNullException(nameof(injector));
        _cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
        _delay = delay ?? throw new ArgumentNullException(nameof(delay));
    }

    public async Task RunAsync(AutoClickerOptions options, CancellationToken cancellationToken)
    {
        options.EnsureValid();
        var clicks = options.RepeatMode == RepeatMode.UntilStopped
            ? int.MaxValue
            : options.RepeatMode == RepeatMode.FixedCount ? options.RepeatCount : 1;

        try
        {
            for (var index = 0; index < clicks; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var position = options.PositionMode == ClickPositionMode.CurrentCursor
                    ? _cursor.GetPosition()
                    : (X: options.FixedX, Y: options.FixedY);
                _injector.MoveMouse(position.X, position.Y);
                _injector.MouseButton(options.Button, MouseButtonState.Down);
                _injector.MouseButton(options.Button, MouseButtonState.Up);

                if (index + 1 < clicks)
                {
                    await _delay.DelayAsync(
                        TimeSpan.FromMilliseconds(options.IntervalMilliseconds),
                        cancellationToken);
                }
            }
        }
        finally
        {
            _injector.ReleaseAll();
        }
    }
}

/// <summary>
/// Mutually exclusive runtime states for recording and automation.
/// </summary>
public enum AutomationState
{
    Idle,
    Recording,
    Playing,
    AutoClicking,
    Pausing,
    Stopping,
    Error
}

/// <summary>
/// Coordinates recorder, playback and auto-clicker operations.
/// </summary>
public sealed class AutomationCoordinator : IDisposable
{
    private readonly MacroRecorderService _recorder;
    private readonly MacroPlaybackService _playback;
    private readonly AutoClickerService _autoClicker;
    private readonly IInputInjector _injector;
    private readonly object _gate = new();
    private CancellationTokenSource? _activeCancellation;
    private PlaybackPauseGate? _pauseGate;
    private Task? _activeTask;
    private AutomationState _state = AutomationState.Idle;

    public AutomationCoordinator(
        MacroRecorderService recorder,
        MacroPlaybackService playback,
        AutoClickerService autoClicker,
        IInputInjector injector)
    {
        _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        _playback = playback ?? throw new ArgumentNullException(nameof(playback));
        _autoClicker = autoClicker ?? throw new ArgumentNullException(nameof(autoClicker));
        _injector = injector ?? throw new ArgumentNullException(nameof(injector));
    }

    public event EventHandler<AutomationStateChangedEventArgs>? StateChanged;

    public AutomationState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public int RecordedActionCount => _recorder.ActionCount;

    public void StartRecording(RecordingOptions options)
    {
        lock (_gate)
        {
            EnsureIdle();
            _recorder.Start(options);
            SetState(AutomationState.Recording);
        }
    }

    public MacroDocument StopRecording(string name)
    {
        lock (_gate)
        {
            if (_state != AutomationState.Recording)
            {
                throw new InvalidOperationException("No recording is active.");
            }

            SetState(AutomationState.Stopping);
        }

        try
        {
            var document = _recorder.Stop(name);
            SetState(AutomationState.Idle);
            return document;
        }
        catch
        {
            SetState(AutomationState.Error);
            throw;
        }
    }

    public void CancelRecording()
    {
        lock (_gate)
        {
            if (_state != AutomationState.Recording)
            {
                return;
            }

            SetState(AutomationState.Stopping);
        }

        _recorder.Cancel();
        SetState(AutomationState.Idle);
    }

    public Task PlayAsync(MacroDocument document, PlaybackOptions options)
    {
        lock (_gate)
        {
            EnsureIdle();
            var cancellation = new CancellationTokenSource();
            _activeCancellation = cancellation;
            var pauseGate = new PlaybackPauseGate();
            _pauseGate = pauseGate;
            SetState(AutomationState.Playing);
            var task = Task.Run(
                () => RunOperationAsync(
                    cancellationToken => _playback.PlayAsync(document, options, pauseGate, cancellationToken),
                    cancellation.Token),
                CancellationToken.None);
            _activeTask = task;
            return task;
        }
    }

    public Task RunAutoClickerAsync(AutoClickerOptions options)
    {
        lock (_gate)
        {
            EnsureIdle();
            var cancellation = new CancellationTokenSource();
            _activeCancellation = cancellation;
            SetState(AutomationState.AutoClicking);
            var task = Task.Run(
                () => RunOperationAsync(
                    cancellationToken => _autoClicker.RunAsync(options, cancellationToken),
                    cancellation.Token),
                CancellationToken.None);
            _activeTask = task;
            return task;
        }
    }

    public void PausePlayback()
    {
        lock (_gate)
        {
            if (_state != AutomationState.Playing || _pauseGate is null)
            {
                return;
            }

            _pauseGate.Pause();
            SetState(AutomationState.Pausing);
        }
    }

    public void ResumePlayback()
    {
        lock (_gate)
        {
            if (_state != AutomationState.Pausing || _pauseGate is null)
            {
                return;
            }

            _pauseGate.Resume();
            SetState(AutomationState.Playing);
        }
    }

    public async Task StopAsync()
    {
        Task? activeTask;
        lock (_gate)
        {
            if (_state == AutomationState.Idle)
            {
                return;
            }

            if (_state == AutomationState.Recording)
            {
                SetState(AutomationState.Stopping);
                activeTask = null;
            }
            else
            {
                SetState(AutomationState.Stopping);
                _activeCancellation?.Cancel();
                activeTask = _activeTask;
            }
        }

        if (activeTask is not null)
        {
            try
            {
                await activeTask;
            }
            catch (OperationCanceledException)
            {
                // Cancellation is the expected result of an explicit stop request.
            }
        }
        else
        {
            _recorder.Cancel();
        }

        _injector.ReleaseAll();
        SetState(AutomationState.Idle);
    }

    public void RequestStop()
    {
        lock (_gate)
        {
            if (_state == AutomationState.Idle)
            {
                return;
            }

            SetState(AutomationState.Stopping);
            _activeCancellation?.Cancel();
            if (_state == AutomationState.Stopping && _recorder.IsRecording)
            {
                _recorder.Cancel();
            }
        }

        _injector.ReleaseAll();
        SetState(AutomationState.Idle);
    }

    public void Dispose()
    {
        RequestStop();
        _pauseGate?.Dispose();
        _activeCancellation?.Dispose();
        _recorder.Dispose();
    }

    private async Task RunOperationAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        var failed = false;
        try
        {
            await operation(cancellationToken);
            SetState(AutomationState.Idle);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SetState(AutomationState.Idle);
        }
        catch
        {
            failed = true;
            SetState(AutomationState.Error);
            throw;
        }
        finally
        {
            lock (_gate)
            {
                if (_activeTask is not null && _activeTask.IsCompleted)
                {
                    _activeTask = null;
                }

                _activeCancellation?.Dispose();
                _activeCancellation = null;
                _pauseGate?.Dispose();
                _pauseGate = null;
            }

            if (failed)
            {
                SetState(AutomationState.Idle);
            }
        }
    }

    private void EnsureIdle()
    {
        if (_state != AutomationState.Idle)
        {
            throw new InvalidOperationException($"Cannot start a new operation while the application is {_state}.");
        }
    }

    private void SetState(AutomationState state)
    {
        AutomationState previous;
        lock (_gate)
        {
            previous = _state;
            _state = state;
        }

        if (previous != state)
        {
            StateChanged?.Invoke(this, new AutomationStateChangedEventArgs(previous, state));
        }
    }
}

/// <summary>
/// Runtime state transition information.
/// </summary>
public sealed class AutomationStateChangedEventArgs : EventArgs
{
    public AutomationStateChangedEventArgs(AutomationState previous, AutomationState current)
    {
        Previous = previous;
        Current = current;
    }

    public AutomationState Previous { get; }

    public AutomationState Current { get; }
}

/// <summary>
/// Default delay implementation used by the desktop application.
/// </summary>
public sealed class SystemAsyncDelay : IAsyncDelay
{
    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        return Task.Delay(duration, cancellationToken);
    }
}
