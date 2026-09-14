# Architecture

## Product boundary

Mouse Keyboard Macro Recorder is a local Windows desktop application with four explicit layers:

1. **Domain**: immutable macro actions, timing, validation, and playback configuration.
2. **Infrastructure**: native Windows input capture, input injection, file persistence, and global hotkeys.
3. **Application services**: recording sessions, playback orchestration, auto-clicking, cancellation, and safety release.
4. **Presentation**: WPF windows, forms, action lists, status feedback, and keyboard-accessible controls.

The domain layer does not import WPF, Windows APIs, timers, or filesystem code. This keeps macro validation and playback behavior deterministic and testable without a desktop session.

## Runtime states

The coordinator models mutually exclusive active modes:

- `idle`
- `recording`
- `playing`
- `auto_clicking`
- `pausing`
- `stopping`
- `error`

Only one input-producing mode may be active at a time. Stop requests are idempotent. When a stop or failure occurs, the application releases any keys or mouse buttons that it pressed and returns to `idle`; the presentation layer keeps the failure message visible until the next successful action.

## Native input boundary

Native Windows hooks and input injection are behind narrow interfaces. The rest of the application depends on those interfaces rather than on a specific hook library.

The current Windows adapter provides:

- low-level mouse movement, left/right/middle button down/up, wheel, and keyboard down/up events;
- filtering of `SendInput`-injected events during recording;
- virtual-desktop coordinate normalization using the current monitor bounds and physical screen metadata;
- an actionable input-injection error when Windows UIPI or another native restriction rejects an event;
- dedicated message threads for input hooks and global hotkeys;
- configurable function-key assignments in the Hotkeys dialog, with a clear conflict error when registration fails.

The recorder ignores keyboard auto-repeat down events and unmatched release events so ordinary typing produces a balanced macro. A low-level hook callback never lets an exception cross the unmanaged boundary.

## Playback and safety

`MacroPlaybackService` delays each action according to its `after_ms` value and the selected speed. A pause gate suspends the remaining delay without losing it. The coordinator owns cancellation for playback and auto-clicking. Both services call `ReleaseAll` in `finally`, and the Windows injector separately tracks every synthetic key/button press so an emergency stop can release it again.

## Persistence

Macro files use versioned UTF-8 JSON with the `.macro.json` suffix. Writes use a temporary file in the same directory, flush it, and replace the destination atomically. Invalid or incompatible files are rejected with a user-facing explanation; they are not silently reset or treated as an empty macro.

Theme and runtime preferences are stored separately below `%LOCALAPPDATA%\MouseKeyboardMacroRecorder` so a macro file never contains account data, credentials, or application settings.

## Privacy and platform limits

No network service is required. The application does not collect telemetry or upload macro contents. Keyboard capture has an explicit toggle in Quick settings, and recording is refused when both mouse and keyboard capture are disabled.

Coordinates are captured in virtual-screen pixels. The injector reads the current virtual desktop bounds on every move, which supports negative monitor origins and common multi-monitor layouts. A changed layout or DPI can still make a recorded coordinate target a different visual element, so users should review a macro before replaying it.

Windows may reject input sent to a higher-elevation target. That failure is reported and the operation is stopped; the application does not attempt to bypass Windows security boundaries.
