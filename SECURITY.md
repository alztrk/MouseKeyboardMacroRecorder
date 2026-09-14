# Security policy

## Scope

This project controls local mouse and keyboard input. A malformed macro, unsafe playback implementation, or unexpected global-hotkey behavior can cause unintended local actions.

## Reporting a vulnerability

Do not open a public issue for a security-sensitive report. Until a dedicated security contact is published, report the issue privately to the project maintainers through the repository's private security reporting channel.

Include:

- A clear description of the impact.
- Reproduction steps that do not expose credentials or personal data.
- Affected version or commit.
- Any suggested mitigation.

Do not attach real macro recordings if they may contain passwords, authentication codes, private documents, or personal information.

## Security principles

- Macro files are untrusted input and must be validated before execution.
- The application must not execute arbitrary shell commands from a macro file.
- The application must not upload macro contents or desktop input.
- Emergency stop must remain available while automation is active.
- Errors must not expose secrets or raw sensitive input in logs.
