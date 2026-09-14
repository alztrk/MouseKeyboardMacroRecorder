[CmdletBinding()]
param(
    [ValidateSet("Portable", "Installer", "All")]
    [string]$Target = "All",
    [ValidateSet("win-x64", "win-arm64")]
    [string]$RuntimeIdentifier = "win-x64",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$OutputRoot,
    [string]$InnoSetupCompiler
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repositoryRoot "src\MouseKeyboardMacroRecorder\MouseKeyboardMacroRecorder.csproj"
$installerScriptPath = Join-Path $repositoryRoot "packaging\MouseKeyboardMacroRecorder.iss"
$resolvedOutputRoot = if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    Join-Path $repositoryRoot "release"
} else {
    [System.IO.Path]::GetFullPath($OutputRoot)
}
$portableOutputPath = $resolvedOutputRoot
$installerOutputPath = Join-Path $resolvedOutputRoot "installer"

function Invoke-Dotnet {
    param([string[]]$Arguments)

    & dotnet @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed with exit code $LASTEXITCODE."
    }
}

function Get-ProjectProperty {
    param([string]$PropertyName)

    $propertyOutput = & dotnet msbuild $projectPath "-getProperty:$PropertyName" -nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Could not read MSBuild property '$PropertyName'."
    }

    $propertyText = ($propertyOutput -join [Environment]::NewLine).Trim()
    if ([string]::IsNullOrWhiteSpace($propertyText)) {
        throw "MSBuild property '$PropertyName' is empty."
    }

    if ($propertyText.StartsWith("{")) {
        try {
            $propertyDocument = $propertyText | ConvertFrom-Json
        }
        catch {
            throw "MSBuild property '$PropertyName' did not return valid JSON."
        }

        $property = $propertyDocument.Properties.PSObject.Properties[$PropertyName]
        if ($null -eq $property -or [string]::IsNullOrWhiteSpace([string]$property.Value)) {
            throw "MSBuild property '$PropertyName' is empty."
        }

        return ([string]$property.Value).Trim()
    }

    return $propertyText
}

function Publish-Portable {
    New-Item -ItemType Directory -Path $portableOutputPath -Force | Out-Null
    Invoke-Dotnet @(
        "publish",
        $projectPath,
        "--configuration", $Configuration,
        "--runtime", $RuntimeIdentifier,
        "--self-contained", "true",
        "--output", $portableOutputPath,
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:PublishTrimmed=false",
        "-p:GenerateDocumentationFile=false",
        "-p:DebugType=None",
        "-p:DebugSymbols=false"
    )
}

function Find-InnoSetupCompiler {
    if (-not [string]::IsNullOrWhiteSpace($InnoSetupCompiler)) {
        $explicitPath = (Resolve-Path $InnoSetupCompiler -ErrorAction Stop).Path
        if (-not (Test-Path -LiteralPath $explicitPath -PathType Leaf)) {
            throw "The specified Inno Setup compiler does not exist: $explicitPath"
        }

        return $explicitPath
    }

    $command = Get-Command iscc -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $command -and (Test-Path -LiteralPath $command.Source -PathType Leaf)) {
        return $command.Source
    }

    $uninstallRoots = @(
        "Registry::HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall",
        "Registry::HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Uninstall",
        "Registry::HKEY_LOCAL_MACHINE\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    )

    foreach ($uninstallRoot in $uninstallRoots) {
        if (-not (Test-Path -LiteralPath $uninstallRoot)) {
            continue
        }

        $entries = Get-ChildItem -LiteralPath $uninstallRoot -ErrorAction SilentlyContinue
        foreach ($entry in $entries) {
            $metadata = Get-ItemProperty -LiteralPath $entry.PSPath -ErrorAction SilentlyContinue
            if ($null -eq $metadata -or [string]$metadata.DisplayName -notmatch '^Inno Setup\b') {
                continue
            }

            if ([string]::IsNullOrWhiteSpace([string]$metadata.InstallLocation)) {
                continue
            }

            $installLocation = [Environment]::ExpandEnvironmentVariables([string]$metadata.InstallLocation)
            $candidatePath = Join-Path $installLocation "ISCC.exe"
            if (Test-Path -LiteralPath $candidatePath -PathType Leaf) {
                return (Resolve-Path -LiteralPath $candidatePath).Path
            }
        }
    }

    throw "Inno Setup compiler was not found on PATH or in the Windows uninstall registry. Install Inno Setup or pass -InnoSetupCompiler <path to ISCC.exe>."
}

function Build-Installer {
    if (-not (Test-Path -LiteralPath $portableOutputPath -PathType Container)) {
        $null = Publish-Portable
    }

    $compilerPath = Find-InnoSetupCompiler
    $version = Get-ProjectProperty "Version"
    $productName = Get-ProjectProperty "Product"
    New-Item -ItemType Directory -Path $installerOutputPath -Force | Out-Null

    & $compilerPath "/Qp" "/DAppName=$productName" "/DAppVersion=$version" "/DPortableDir=$portableOutputPath" "/DInstallerOutput=$installerOutputPath" $installerScriptPath | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup failed with exit code $LASTEXITCODE."
    }

    $installerPath = Join-Path $installerOutputPath "MouseKeyboardMacroRecorder-Setup-$version.exe"
    if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
        throw "Inno Setup completed without producing the expected installer: $installerPath"
    }

    return $installerPath
}

$portableRequested = $Target -in @("Portable", "All")
$installerRequested = $Target -in @("Installer", "All")
if ($portableRequested) {
    Publish-Portable
}

$installerPath = $null
if ($installerRequested) {
    $installerPath = Build-Installer
}

$portableExecutablePath = Join-Path $portableOutputPath "MouseKeyboardMacroRecorder.exe"
if (-not (Test-Path -LiteralPath $portableExecutablePath -PathType Leaf)) {
    throw "Portable publish completed without an executable: $portableExecutablePath"
}

$portableHash = Get-FileHash -LiteralPath $portableExecutablePath -Algorithm SHA256
$installerHash = if ($null -eq $installerPath) {
    $null
} else {
    Get-FileHash -LiteralPath $installerPath -Algorithm SHA256
}
$projectVersion = Get-ProjectProperty "Version"
$manifestInstallerPath = if ($null -eq $installerPath) {
    $null
} else {
    "installer/MouseKeyboardMacroRecorder-Setup-$projectVersion.exe"
}
$manifest = [ordered]@{
    product = Get-ProjectProperty "Product"
    version = $projectVersion
    runtime = $RuntimeIdentifier
    configuration = $Configuration
    portable_executable = "MouseKeyboardMacroRecorder.exe"
    portable_sha256 = $portableHash.Hash
    installer = $manifestInstallerPath
    installer_sha256 = if ($null -eq $installerHash) { $null } else { $installerHash.Hash }
}
$manifestPath = Join-Path $repositoryRoot "release-manifest-$RuntimeIdentifier.json"
$manifest | ConvertTo-Json | Set-Content -LiteralPath $manifestPath -Encoding utf8

[pscustomobject]@{
    PortableDirectory = $portableOutputPath
    PortableExecutable = $portableExecutablePath
    PortableSha256 = $portableHash.Hash
    Installer = $installerPath
    InstallerSha256 = if ($null -eq $installerHash) { $null } else { $installerHash.Hash }
    Manifest = $manifestPath
}
