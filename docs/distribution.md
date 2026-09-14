# Distribution

The project has one source of truth for runtime packaging: `scripts/publish.ps1`.

## Portable build

```powershell
pwsh ./scripts/publish.ps1 -Target Portable -RuntimeIdentifier win-x64
```

The result is a self-contained release folder at `release`. The single-file executable contains the .NET runtime and application icon, so no `Assets` folder or runtime DLLs are required beside it. It does not write an installer registry entry or require administrator privileges.

The script accepts `win-arm64` for a self-contained ARM64 build:

```powershell
pwsh ./scripts/publish.ps1 -Target Portable -RuntimeIdentifier win-arm64
```

## Installer build

The installer uses the reviewable Inno Setup definition at `packaging/MouseKeyboardMacroRecorder.iss`. Install the 64-bit Inno Setup compiler. The publish script discovers `ISCC.exe` from `PATH` or the Windows uninstall registry; pass `-InnoSetupCompiler <path to ISCC.exe>` only when automatic discovery is not suitable:

```powershell
pwsh ./scripts/publish.ps1 `
    -Target All `
    -RuntimeIdentifier win-x64
```

The installer is per-user by default and installs to `%LOCALAPPDATA%\Programs\Mouse Keyboard Macro Recorder`. It creates a Start Menu shortcut and offers an unchecked desktop shortcut. It does not remove application preferences or macro files during uninstall.

## Release evidence

Every publish writes `outputs/release-manifest-<runtime>.json` with the resolved MSBuild version, runtime, relative artifact paths, and SHA-256 values for the portable executable and, when requested, the installer. Signing is intentionally a separate release step because it requires a project-owned certificate and timestamping policy.
