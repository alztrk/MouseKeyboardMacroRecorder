# Maintainer scripts

This directory contains repeatable packaging and release helpers. Scripts should be deterministic, document their inputs, preserve exit codes, and never print secrets or recorded user input.

## Publish

From the repository root:

```powershell
pwsh ./scripts/publish.ps1 -Target All -RuntimeIdentifier win-x64
```

Targets:

- `Portable`: produces a single-file self-contained executable under `release`.
- `Installer`: packages the existing release folder with Inno Setup.
- `All`: produces both artifacts from the same publish.

The script discovers `ISCC.exe` from `PATH` or the Windows uninstall registry. Use `-InnoSetupCompiler <path to ISCC.exe>` only when automatic discovery is not suitable. The script reads the version and product name from MSBuild, so the installer filename and metadata do not need a second version constant.
