# Changelog

All notable changes to this project will be documented here.

## Unreleased

- Added the C#/.NET 8 WPF implementation for the local Windows desktop workflow.
- Added typed mouse, keyboard, wheel, timing, repeat, and screen metadata models with validation.
- Added version 1 `.macro.json` serialization and same-directory atomic persistence.
- Added low-level Windows mouse/keyboard capture with injected-event filtering.
- Added `SendInput` playback and auto-clicking with cancellation and guaranteed input release.
- Added recording toggles, action inspection, action reorder/removal, import/export, and playback pause.
- Added configurable F8/F9/Esc-style global hotkeys with native conflict reporting.
- Added persistent runtime preferences, inter-loop delay, light/dark theme persistence, and a focused Hotkeys dialog.
- Added eight deterministic core tests covering format, timing, cancellation, repeat counts, preference normalization, and file persistence.
- Centralized product identity, local-data paths, automation limits, defaults, hotkey validation, and responsive layout thresholds.
- Added repeatable self-contained portable publishing, release manifests, and an open-source Inno Setup installer definition.
