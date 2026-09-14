# Roadmap

## 0.1 Foundation

- [x] Define typed action and playback models.
- [x] Validate and serialize versioned macro files.
- [x] Add deterministic unit tests for timing, ordering, and malformed input.
- [x] Create the WPF application shell and explicit idle/recording/playing states.

## 0.2 Windows input

- [x] Implement mouse and keyboard capture behind interfaces.
- [x] Implement safe mouse and keyboard injection.
- [x] Add emergency stop and guaranteed release of held inputs.
- [x] Add configurable function-key hotkeys with conflict reporting.

## 0.3 Recorder and player

- [x] Add a recording workflow with mouse and keyboard capture toggles.
- [x] Add action list inspection and basic editing.
- [x] Add playback speed, loop count, inter-loop delay, pause, and cancellation.
- [x] Add save/load dialogs and atomic persistence.

## 0.4 Auto-clicker

- [x] Add independent auto-clicker mode.
- [x] Support left, right, and middle buttons.
- [x] Support interval, repeat count, and current/fixed cursor position.
- [x] Reuse the same safety state machine and emergency stop.

## 0.5 Release hardening

- [ ] Test across supported Windows versions, DPI settings, multiple monitors, and elevated targets.
- [x] Add self-contained `win-x64` publishing guidance.
- [x] Add repeatable portable publishing with runtime selection and SHA-256 manifest output.
- [x] Add a reviewable open-source installer definition that packages the portable output.
- [x] Add accessible focus, error-state, cancellation, and recovery behavior to the first desktop workflow.
- [ ] Add signed-release guidance when a release certificate is available.

Features such as screen image recognition, scripting, remote control, credential handling, stealth behavior, and anti-cheat bypass are outside the initial scope.
