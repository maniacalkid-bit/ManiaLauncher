; ============================================================
;  Mania Launcher — Inno Setup script
;  Owner: maniacalkid · License: MIT
;  Build:  ISCC.exe ManiaLauncher.iss
;  Expects publish output at ..\publish\portable
; ============================================================

#define MyAppName        "Mania Launcher"
#define MyAppVersion     "1.1.0"
#define MyAppPublisher   "maniacalkid"
#define MyAppURL         "https://github.com/maniacalkid-bit/ManiaLauncher"
#define MyAppExeName     "ManiaLauncher.exe"
#define PublishDir       "..\publish\portable"

[Setup]
AppId={{8F1D9A42-6C3E-4B7A-9E25-D0C4A7B31F86}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
AppCopyright=Copyright (c) 2026 {#MyAppPublisher}
LicenseFile=..\LICENSE
DefaultDirName={autopf}\ManiaLauncher
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\dist
OutputBaseFilename=ManiaLauncher-{#MyAppVersion}-Setup-win-x64
SetupIconFile=..\assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Per-user install requires no admin rights.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DisableProgramGroupPage=auto

[Languages]
Name: "english";  MessagesFile: "compiler:Default.isl"
Name: "russian";  MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}";  Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
