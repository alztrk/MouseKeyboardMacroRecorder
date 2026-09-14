using MouseKeyboardMacroRecorder.Core.Application;
using MouseKeyboardMacroRecorder.Core.Domain;
using MouseKeyboardMacroRecorder.Core.Infrastructure.Persistence;
using Xunit;

namespace MouseKeyboardMacroRecorder.Core.Tests;

public sealed class CoreBehaviorTests
{
    [Fact]
    public void MacroJsonRoundTripsAllSupportedActionTypes()
    {
        var createdAt = new DateTimeOffset(2026, 9, 13, 12, 30, 0, TimeSpan.Zero);
        var actions = new MacroAction[]
        {
            new MouseMoveAction(0, 120, 240),
            new MouseButtonAction(15, 120, 240, MouseButtonKind.Left, MouseButtonState.Down),
            new MouseButtonAction(20, 120, 240, MouseButtonKind.Left, MouseButtonState.Up),
            new MouseWheelAction(30, 120, 240, -120),
            new KeyboardAction(40, 0x41, "A", KeyboardKeyState.Down, false),
            new KeyboardAction(50, 0x41, "A", KeyboardKeyState.Up, false)
        };
        var original = new MacroDocument("Example", createdAt, new ScreenInfo(1920, 1080, 1.25), actions);

        var json = MacroJsonSerializer.Serialize(original);
        var roundTripped = MacroJsonSerializer.Deserialize(json);

        Assert.Equal(original.Format, roundTripped.Format);
        Assert.Equal(original.Version, roundTripped.Version);
        Assert.Equal(original.Name, roundTripped.Name);
        Assert.Equal(original.CreatedAtUtc, roundTripped.CreatedAtUtc);
        Assert.Equal(original.Screen, roundTripped.Screen);
        Assert.Equal(original.Actions, roundTripped.Actions);
    }

    [Fact]
    public void ValidatorRejectsMacrosThatEndWithHeldInput()
    {
        var document = new MacroDocument(
            "Incomplete",
            DateTimeOffset.UtcNow,
            new ScreenInfo(1920, 1080, 1),
            new MacroAction[]
            {
                new KeyboardAction(0, 0x41, "A", KeyboardKeyState.Down, false)
            });

        var errors = MacroValidator.Validate(document);

        Assert.Contains(errors, error => error.Contains("keyboard key held", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DeserializerRejectsNonObjectRootsWithAFormatError()
    {
        var exception = Assert.Throws<MacroFormatException>(() => MacroJsonSerializer.Deserialize("[]"));

        Assert.Contains("root", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecorderPreservesEventOrderAndElapsedTime()
    {
        var capture = new FakeCapture();
        var recorder = new MacroRecorderService(capture, new FakeScreenInfo());
        var start = new DateTimeOffset(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

        recorder.Start(new RecordingOptions(CaptureMouse: true, CaptureKeyboard: true));
        capture.Emit(new CapturedMouseMove(start, 10, 20));
        capture.Emit(new CapturedMouseButton(start.AddMilliseconds(25), 10, 20, MouseButtonKind.Left, MouseButtonState.Down));
        capture.Emit(new CapturedMouseButton(start.AddMilliseconds(45), 10, 20, MouseButtonKind.Left, MouseButtonState.Up));
        var document = recorder.Stop("Recorded");

        Assert.Equal(3, document.Actions.Count);
        Assert.Equal(0, document.Actions[0].AfterMilliseconds);
        Assert.Equal(25, document.Actions[1].AfterMilliseconds);
        Assert.Equal(20, document.Actions[2].AfterMilliseconds);
        recorder.Dispose();
    }

    [Fact]
    public async Task PlaybackReleasesInputsWhenARunIsCancelled()
    {
        var injector = new FakeInjector();
        var delay = new BlockingDelay();
        var playback = new MacroPlaybackService(injector, delay);
        using var pauseGate = new PlaybackPauseGate();
        using var cancellation = new CancellationTokenSource();
        var document = new MacroDocument(
            "Cancelable",
            DateTimeOffset.UtcNow,
            new ScreenInfo(1920, 1080, 1),
            new MacroAction[]
            {
                new MouseMoveAction(1, 100, 100),
                new KeyboardAction(0, 0x41, "A", KeyboardKeyState.Down, false),
                new KeyboardAction(0, 0x41, "A", KeyboardKeyState.Up, false)
            });

        var playTask = playback.PlayAsync(document, new PlaybackOptions(1, RepeatMode.UntilStopped, 1), pauseGate, cancellation.Token);
        await delay.Started;
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await playTask);

        Assert.Contains("release-all", injector.Events);
    }

    [Fact]
    public async Task AutoClickerRunsTheRequestedFixedNumberOfClicks()
    {
        var injector = new FakeInjector();
        var clicker = new AutoClickerService(injector, new FakeCursor(300, 400), new CompletedDelay());

        await clicker.RunAsync(
            new AutoClickerOptions(
                MouseButtonKind.Right,
                25,
                RepeatMode.FixedCount,
                3,
                ClickPositionMode.FixedPosition,
                50,
                60),
            CancellationToken.None);

        Assert.Equal(3, injector.Events.Count(entry => entry == "right-down"));
        Assert.Equal(3, injector.Events.Count(entry => entry == "right-up"));
        Assert.Equal(3, injector.Events.Count(entry => entry == "move:50,60"));
        Assert.Contains("release-all", injector.Events);
    }

    [Fact]
    public void MacroFileStoreUsesTheVersionedExtensionAndRoundTripsAFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MouseKeyboardMacroRecorder.Tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "sample.macro.json");
        var document = new MacroDocument(
            "Sample",
            DateTimeOffset.UtcNow,
            new ScreenInfo(1920, 1080, 1),
            Array.Empty<MacroAction>());

        try
        {
            MacroFileStore.Save(path, document);
            var loaded = MacroFileStore.Load(path);

            Assert.Equal(document.Name, loaded.Name);
            Assert.Equal(document.Actions, loaded.Actions);
            Assert.Empty(Directory.GetFiles(directory, ".*.tmp", SearchOption.TopDirectoryOnly));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void PreferencesNormalizerRepairsInvalidPersistedValues()
    {
        var preferences = new UserPreferences
        {
            PlaybackSpeed = double.NaN,
            PlaybackRepeatMode = (RepeatMode)99,
            PlaybackRepeatCount = 0,
            PlaybackInterLoopDelayMilliseconds = -1,
            AutoClickButton = (MouseButtonKind)99,
            AutoClickIntervalMilliseconds = 1,
            AutoClickRepeatMode = (RepeatMode)99,
            AutoClickRepeatCount = 0,
            AutoClickPositionMode = (ClickPositionMode)99,
            LastSurface = "unknown",
            RecordHotkeyVirtualKey = HotkeyDefaults.PlayVirtualKey,
            PlayHotkeyVirtualKey = HotkeyDefaults.PlayVirtualKey,
            StopHotkeyVirtualKey = 0
        };

        UserPreferencesNormalizer.Normalize(preferences);

        Assert.Equal(AutomationDefaults.PlaybackSpeed, preferences.PlaybackSpeed);
        Assert.Equal(AutomationDefaults.PlaybackRepeatMode, preferences.PlaybackRepeatMode);
        Assert.Equal(AutomationDefaults.PlaybackRepeatCount, preferences.PlaybackRepeatCount);
        Assert.Equal(AutomationDefaults.PlaybackInterLoopDelayMilliseconds, preferences.PlaybackInterLoopDelayMilliseconds);
        Assert.Equal(AutomationDefaults.AutoClickButton, preferences.AutoClickButton);
        Assert.Equal(AutomationDefaults.AutoClickIntervalMilliseconds, preferences.AutoClickIntervalMilliseconds);
        Assert.Equal(AutomationDefaults.AutoClickRepeatMode, preferences.AutoClickRepeatMode);
        Assert.Equal(AutomationDefaults.AutoClickRepeatCount, preferences.AutoClickRepeatCount);
        Assert.Equal(AutomationDefaults.AutoClickPositionMode, preferences.AutoClickPositionMode);
        Assert.Equal(AutomationDefaults.LastSurface, preferences.LastSurface);
        Assert.Equal(HotkeyDefaults.RecordVirtualKey, preferences.RecordHotkeyVirtualKey);
        Assert.Equal(HotkeyDefaults.PlayVirtualKey, preferences.PlayHotkeyVirtualKey);
        Assert.Equal(HotkeyDefaults.StopVirtualKey, preferences.StopHotkeyVirtualKey);
    }

    private sealed class FakeCapture : IInputCapture
    {
        private Action<CapturedInput>? _onInput;

        public IDisposable Start(RecordingOptions options, Action<CapturedInput> onInput)
        {
            _onInput = onInput;
            return new DelegateDisposable(() => _onInput = null);
        }

        public void Emit(CapturedInput input)
        {
            _onInput?.Invoke(input);
        }

        public void Dispose()
        {
            _onInput = null;
        }
    }

    private sealed class FakeScreenInfo : IScreenInfoProvider
    {
        public ScreenInfo GetCurrent() => new(1920, 1080, 1);
    }

    private sealed class FakeCursor : ICursorPositionProvider
    {
        private readonly int _x;
        private readonly int _y;

        public FakeCursor(int x, int y)
        {
            _x = x;
            _y = y;
        }

        public (int X, int Y) GetPosition() => (_x, _y);
    }

    private sealed class FakeInjector : IInputInjector
    {
        public List<string> Events { get; } = new();

        public void MoveMouse(int x, int y) => Events.Add($"move:{x},{y}");

        public void MouseButton(MouseButtonKind button, MouseButtonState state) => Events.Add($"{button.ToString().ToLowerInvariant()}-{state.ToString().ToLowerInvariant()}");

        public void MouseWheel(int delta) => Events.Add($"wheel:{delta}");

        public void Keyboard(ushort virtualKey, KeyboardKeyState state, bool isExtended) => Events.Add($"key:{virtualKey:X2}-{state.ToString().ToLowerInvariant()}");

        public void ReleaseAll() => Events.Add("release-all");

        public void Dispose()
        {
        }
    }

    private sealed class CompletedDelay : IAsyncDelay
    {
        public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class BlockingDelay : IAsyncDelay
    {
        private readonly TaskCompletionSource<bool> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        public async Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            _started.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    private sealed class DelegateDisposable : IDisposable
    {
        private Action? _dispose;

        public DelegateDisposable(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _dispose, null)?.Invoke();
        }
    }
}
