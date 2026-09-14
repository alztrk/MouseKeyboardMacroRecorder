# .NET development

## Toolchain

- Target framework: `net8.0-windows`
- UI framework: WPF
- Language: C# with nullable reference types enabled
- Build system: .NET SDK and MSBuild
- Test runner: `dotnet test`

## Layering

The application will keep domain types independent from WPF and Win32. Windows hooks, input injection, global hotkeys, filesystem persistence, and timers belong in infrastructure adapters. Application services coordinate those adapters and expose explicit cancellation and failure results to the WPF presentation layer.

## Local commands

```text
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release
dotnet format --verify-no-changes
pwsh ./scripts/publish.ps1 -Target Portable -RuntimeIdentifier win-x64
```

`dotnet format` is part of the source validation. The publish script is the supported path for reproducible self-contained output. Installer builds additionally require Inno Setup and use the same portable folder as their input.

## Windows API boundary

Interop declarations must be small, documented, and isolated. The application must not expose raw Win32 handles or P/Invoke details to the domain model. Every native subscription must have a matching disposal path, and every injected key or mouse button must have a safe release path on cancellation and application shutdown.
