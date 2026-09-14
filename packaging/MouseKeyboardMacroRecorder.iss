#ifndef AppName
#define AppName "Mouse Keyboard Macro Recorder"
#endif

#ifndef AppVersion
#define AppVersion "0.1.0"
#endif

#ifndef PortableDir
#define PortableDir "..\release"
#endif

#ifndef InstallerOutput
#define InstallerOutput "..\release\installer"
#endif

#define AppExecutable "MouseKeyboardMacroRecorder.exe"

[Setup]
AppId={{5D2BC86F-46B3-46BD-9E0D-7C5F3B1EF2D1}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Mouse Keyboard Macro Recorder contributors
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#InstallerOutput}
OutputBaseFilename=MouseKeyboardMacroRecorder-Setup-{#AppVersion}
SetupIconFile=..\src\MouseKeyboardMacroRecorder\Assets\favicon.ico
UninstallDisplayIcon={app}\{#AppExecutable}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} installer
ChangesAssociations=no

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#PortableDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExecutable}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExecutable}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExecutable}"; Description: "Launch {#AppName}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent unchecked
