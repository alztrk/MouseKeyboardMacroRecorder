# Contributing

Thank you for contributing to Mouse Keyboard Macro Recorder.

## Before opening a change

- Check existing issues and discussions for overlapping work.
- Keep changes focused and explain user-visible behavior.
- Do not include recorded personal input, credentials, tokens, screenshots containing private data, or generated build artifacts.
- Add or update tests for observable behavior.
- Update the relevant documentation when a file format, permission, safety, or user workflow changes.

## Local checks

```text
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
```

All checks must pass before a pull request is considered ready. If a Windows-only check cannot run in your environment, state that clearly in the pull request.

## Pull requests

Describe:

- The user problem and the observable behavior changed.
- The files and layers affected.
- Safety, permission, privacy, or compatibility implications.
- Tests run and any checks that could not be run.
