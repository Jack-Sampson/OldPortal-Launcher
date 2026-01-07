; OldPortal Launcher - InnoSetup Installer Script
; Your gateway to the worlds of Dereth

; Read version from the built executable (single source of truth)
#define MyAppVersion GetFileVersion("publish\win-x86\OPLauncher.exe")

[Setup]
; App Identity
AppName=OldPortal Launcher
AppVersion={#MyAppVersion}
AppPublisher=OldPortal
AppPublisherURL=https://oldportal.com
AppSupportURL=https://oldportal.com/community/category/help-support
AppUpdatesURL=https://oldportal.com/downloads
AppId={{8B9E5F1A-3D2C-4F8E-9A1B-7C6D5E4F3A2B}

; Installation Paths
DefaultDirName={autopf}\OldPortal Launcher
DefaultGroupName=OldPortal Launcher
UninstallDisplayName=OldPortal Launcher
UninstallDisplayIcon={app}\OPLauncher.exe

; Output
OutputDir=Releases
OutputBaseFilename=OPLauncher-Setup

; Branding & Appearance
SetupIconFile=Assets\oldportal-icon (256x256).ico
WizardImageFile=Assets\wizard-large.bmp
WizardSmallImageFile=Assets\wizard-small.bmp
WizardStyle=modern
WindowResizable=no
DisableWelcomePage=no

; Compression (LZMA2 Ultra - best compression)
Compression=lzma2/ultra64
SolidCompression=yes

; Permissions (User-level install, no admin required)
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Version Info
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany=OldPortal
VersionInfoDescription=OldPortal Launcher Setup
VersionInfoCopyright=Copyright (C) 2026 OldPortal

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
; Custom Welcome Screen
WelcomeLabel1=Welcome to OldPortal Launcher
WelcomeLabel2=Your gateway to the worlds of Dereth.%n%nThis wizard will guide you through the installation of OldPortal Launcher.%n%nOldPortal Launcher allows you to browse and connect to Asheron's Call emulator servers through the OldPortal platform.%n%nClick Next to continue.

; Custom Finish Screen
FinishedHeadingLabel=Completing OldPortal Launcher Setup
FinishedLabel=OldPortal Launcher has been successfully installed on your computer.%n%nYou can now browse worlds, manage credentials, and launch Asheron's Call servers through a unified interface.
ClickFinish=Click Finish to exit Setup.

; Directory Selection
SelectDirLabel3=Setup will install OldPortal Launcher into the following folder.
SelectDirBrowseLabel=To continue, click Next. If you would like to select a different folder, click Browse.

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"
Name: "startup"; Description: "Start OldPortal Launcher when Windows starts"; GroupDescription: "Startup options:"; Flags: unchecked

[Files]
; Main application files (all files from publish directory)
Source: "publish\win-x86\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start Menu shortcut
Name: "{autoprograms}\OldPortal Launcher"; Filename: "{app}\OPLauncher.exe"; IconFilename: "{app}\OPLauncher.exe"; Comment: "Launch OldPortal Launcher"

; Desktop shortcut (optional, user choice)
Name: "{autodesktop}\OldPortal Launcher"; Filename: "{app}\OPLauncher.exe"; IconFilename: "{app}\OPLauncher.exe"; Tasks: desktopicon; Comment: "Launch OldPortal Launcher"

[Run]
; Option to launch after installation completes
Filename: "{app}\OPLauncher.exe"; Description: "Launch OldPortal Launcher"; Flags: nowait postinstall skipifsilent

[Registry]
; Auto-start on Windows boot (optional, user choice)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "OldPortal Launcher"; ValueData: "{app}\OPLauncher.exe"; Flags: uninsdeletevalue; Tasks: startup

; Store installation path for future reference
Root: HKCU; Subkey: "Software\OldPortal\Launcher"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletevalue

[UninstallDelete]
; Clean up application data on uninstall (optional - user data is kept)
Type: filesandordirs; Name: "{app}"

[Code]
// Custom code for installer behavior (if needed in future)

function InitializeSetup(): Boolean;
begin
  Result := True;
end;
