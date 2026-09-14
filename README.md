# Mouse Keyboard Macro Recorder

Open-source Windows desktop application for recording, editing, and replaying mouse and keyboard actions, with a built-in auto-clicker.

The project is designed for local, transparent automation. Macro files remain under the user's control, the application does not require an online account, and telemetry is not part of the product scope.

## Current capabilities

- Record mouse movement, left/right/middle button presses, dragging, wheel input, and keyboard key-down/key-up events.
- Review, reorder, and remove recorded actions before exporting a macro.
- Replay a saved macro once, for a fixed number of loops, or until stopped.
- Adjust playback speed and the delay between loops.
- Configure function-key hotkeys for record, play, and stop, with conflict reporting.
- Run a separate auto-clicker with left, right, or middle button, interval, repeat, and current/fixed cursor position controls.
- Save and load portable versioned JSON macro files.
- Stop active automation immediately with the stop hotkey or stop button, releasing any held input.
- Keep the selected theme and runtime preferences across launches.

## Scope and safety

This application sends input to the local desktop only after the user explicitly starts recording, playback, or auto-clicking. It is not a password manager, screen recorder, remote-control service, or anti-cheat bypass. Users must review a macro before running it and should not record passwords, authentication codes, or other sensitive input.

The first target platform is Windows 10 and later on x64 Windows. The publish script also accepts `win-arm64` for ARM64 Windows builds. Cross-platform support is not promised until the input backend and permission model have been designed and tested for each platform.

## Implementation status

The first usable desktop workflow is implemented. The C# solution contains a platform-independent core, a Windows native adapter, the WPF command surface, and deterministic service tests. Recording uses low-level Windows hooks, playback and auto-clicking use `SendInput`, and all active input is released during cancellation or shutdown. Product identity, runtime limits, user-data paths, defaults, and publish settings are centralized so the UI and infrastructure do not silently drift apart.

The application has no web server, account, telemetry, or network dependency. Macro files are explicit `.macro.json` files selected by the user through import and export dialogs. The selected theme is saved locally at `%LOCALAPPDATA%\MouseKeyboardMacroRecorder\settings.json`; other preferences are stored at `%LOCALAPPDATA%\MouseKeyboardMacroRecorder\preferences.json`.

Windows may reject synthetic input when a target application runs at a higher elevation. The application reports that failure instead of silently claiming that the action was sent. Recorded coordinates are virtual-desktop coordinates and include screen metadata so a macro can be reviewed before it is replayed on a changed display layout.

See [docs/roadmap.md](docs/roadmap.md) for the remaining release-hardening work.

## Development setup

Requirements:

- Windows 10 or later
- .NET 8 SDK
- Visual Studio 2022 or another .NET 8-compatible editor

Restore and build the solution:

```text
dotnet restore
dotnet build --configuration Release
```

Run the configured checks:

```text
dotnet test --configuration Release
```

The test project covers JSON round trips, malformed input, recording timing, cancellation release, auto-click repeat counts, and atomic macro persistence. The repeatable distribution command is:

```text
pwsh ./scripts/publish.ps1 -Target All -RuntimeIdentifier win-x64
```

This produces a self-contained portable folder under `outputs/portable/win-x64`, an Inno Setup installer under `outputs/installer`, and a release manifest with SHA-256 information. The installer source lives in `packaging/MouseKeyboardMacroRecorder.iss`. Install Inno Setup 7 before requesting an installer build, or pass `-InnoSetupCompiler` with the path to `ISCC.exe`.

Brand assets are stored in `src/MouseKeyboardMacroRecorder/Assets`. The project ships light and dark SVG wordmarks, mark-only variants, and a multi-resolution `favicon.ico` used by the WPF window and Windows executable. Generated portable and installer outputs are ignored by Git; source packaging definitions remain reviewable.

## Repository layout

```text
docs/                         Product and technical documentation
scripts/                      Maintainer and packaging helpers
packaging/                    Installer source files
src/MouseKeyboardMacroRecorder/
                              WPF application and native Windows adapters
src/MouseKeyboardMacroRecorder.Core/
                              Domain, application services, and persistence
tests/                        Automated core tests
```

## License

The project is released under the [MIT License](LICENSE).
