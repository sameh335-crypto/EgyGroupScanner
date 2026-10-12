; Inno Setup script - Egy Group Scanner installer (AnyCPU or x86)
#define MyAppName "Egy Group Scanner"
#define MyAppExe "EgyGroupScanner.exe"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#ifndef MyArch
  #define MyArch "x86"
#endif

[Setup]
AppId={{B7C1E3A2-5D4F-4E8B-9A61-3F2D8C7E1A90}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Egy Group
AppPublisherURL=https://egygroups.com
DefaultDirName={autopf}\Egy Group Scanner
DefaultGroupName=Egy Group Scanner
DisableProgramGroupPage=yes
OutputDir=dist\installer
OutputBaseFilename=EgyGroupScanner-Setup-{#MyArch}
SetupIconFile=icon.ico
UninstallDisplayIcon={app}\{#MyAppExe}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
#if MyArch != "x86"
; AnyCPU build: installs as 64-bit on 64-bit Windows, as 32-bit on 32-bit Windows
ArchitecturesInstallIn64BitMode=x64
#endif

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"

[Files]
Source: "bin\Release\net48\{#MyAppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
function IsDotNet48Installed: Boolean;
var
  Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full',
    'Release', Release) and (Release >= 528040);
end;

function InitializeSetup: Boolean;
var
  ErrCode: Integer;
begin
  Result := True;
  if not IsDotNet48Installed then
  begin
    if MsgBox('.NET Framework 4.8 is required (Windows 7 needs SP1 first).' + #13#10 +
              'Open the download page now?', mbConfirmation, MB_YESNO) = IDYES then
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet-framework/net48',
        '', '', SW_SHOWNORMAL, ewNoWait, ErrCode);
    Result := False;
  end;
end;
