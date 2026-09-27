; PC Manager installer (Inno Setup 6). See docs/specs/07-installer.md for the full spec and the
; "Contract with first-run setup" this script implements together with
; PCManager.Core.Components.IRegistryReader.GetInstallerHandledComponentIds.
;
; Built by CI only (.github/workflows/ci.yml, .github/workflows/release.yml) - never run locally
; on a real PC. CI invokes it as:
;   ISCC installer\PCManager.iss /DMyAppVersion=<Version from Directory.Build.props> ^
;     /DPublishDir=<path to the published single-file build> /O<output dir>

#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\publish"
#endif

#define MyAppName "PC Manager"
#define MyAppPublisher "PC Manager"
#define MyAppExeName "PCManager.exe"
#define MyAppMutex "PCManagerAppMutex"
; Fixed forever - identifies upgrades/uninstalls across versions. Do not regenerate.
#define MyAppId "{A9E642BD-1DA1-421E-81D3-4416ED5C45F3}"

[Setup]
; Doubled leading brace is deliberate: ISPP expands {#MyAppId} to the macro's own value, which
; already includes its enclosing braces ("{A9E642BD-...}"), and Inno's [Setup] value parser then
; collapses the resulting "{{" into a single literal "{" - this is the standard idiom Inno
; Setup's own script wizard generates for AppId.
AppId={{#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\PC Manager
DefaultGroupName=PC Manager
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=..\dist
OutputBaseFilename=PCManager-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
; PawnIO's driver install (and AnyDesk's per-machine install) both need admin anyway - see
; docs/specs/01b-components.md.
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\src\PCManager.App\Assets\AppIcon.ico
; Matches App.AppMutexName (src/PCManager.App/App.xaml.cs) - lets Setup detect a running PC
; Manager and ask the user to close it before install OR uninstall proceeds, without needing the
; heavier Restart Manager-based CloseApplications feature.
AppMutex={#MyAppMutex}
WizardStyle=modern
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
; Per-machine install, so a per-user HKCU Run key or Startup-folder shortcut for the installing
; user only would be wrong for a shared PC. A Common Startup folder shortcut ({commonstartup})
; starts PC Manager for every account that signs in, still shows up in Task Manager's Startup
; tab (unlike an HKLM Run key, which is invisible there and harder for a family member to
; discover/disable), and is removed automatically by the uninstaller like any other shortcut -
; no extra [Code] needed to clean up an HKLM Run value on uninstall.
Name: "startupicon"; Description: "Start PC Manager when I sign in"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

; One checkbox per optional third-party component (installed via winget in the [Code] section
; below, not as installer-bundled files). Default checked/unchecked state is set in code
; (InitializeWizard) since [Components] itself has no per-item "unchecked" flag.
[Components]
Name: "anydesk"; Description: "Remote help from family (AnyDesk)"
Name: "openrgb"; Description: "RGB lighting control (OpenRGB)"
Name: "pawnio"; Description: "Fan control and temperature sensors (PawnIO driver) - installs a signed hardware driver"

[Files]
Source: "{#PublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\PC Manager"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall PC Manager"; Filename: "{uninstallexe}"
Name: "{autodesktop}\PC Manager"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{commonstartup}\PC Manager"; Filename: "{app}\{#MyAppExeName}"; Tasks: startupicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch PC Manager"; Flags: nowait postinstall skipifsilent

[Code]
const
  { winget's own hex exit codes for "the package/an equivalent version is already installed",
    expressed as the signed 32-bit integers Pascal Script compares Exec's ResultCode against.
    Mirrors PCManager.Core.Processes.WingetExitCodes - keep in sync with that file. }
  WINGET_PACKAGE_ALREADY_INSTALLED = -1978335135; { 0x8A150061 }
  WINGET_UPDATE_NOT_APPLICABLE     = -1978335189; { 0x8A15002B }
  WINGET_INSTALL_ALREADY_INSTALLED = -1978334963; { 0x8A15010D }

  InstallerRegistryKey = 'Software\PC Manager\Installer';
  InstallerRegistryValue = 'Components';

var
  ComponentProgressPage: TOutputProgressWizardPage;

procedure InitializeWizard();
begin
  ComponentProgressPage := CreateOutputProgressPage(
    'Setting up optional components',
    'Please wait while Setup installs the components you selected via winget.');

  { [Components] has no per-item default-unchecked flag, so the desired defaults - AnyDesk on,
    OpenRGB and PawnIO off - are applied here once, right after the wizard form is built. Index
    order matches the [Components] section above (0 = anydesk, 1 = openrgb, 2 = pawnio). }
  WizardForm.ComponentsList.Checked[1] := False;
  WizardForm.ComponentsList.Checked[2] := False;
end;

{ True if winget.exe can be located at all (via cmd.exe /C "where", so it also picks up the
  per-user App Execution Alias winget is normally published as). }
function IsWingetAvailable(): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('cmd.exe', '/C where winget.exe >nul 2>nul', '', SW_HIDE,
    ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

{ True if ExitCode means the component ended up installed even though this particular winget
  call did not install anything new (0 = a plain successful install). }
function IsAlreadyInstalledOrSuccess(ExitCode: Integer): Boolean;
begin
  Result := (ExitCode = 0) or
    (ExitCode = WINGET_PACKAGE_ALREADY_INSTALLED) or
    (ExitCode = WINGET_UPDATE_NOT_APPLICABLE) or
    (ExitCode = WINGET_INSTALL_ALREADY_INSTALLED);
end;

function RunWingetInstall(const WingetId: string; var ExitCode: Integer): Boolean;
var
  Params: string;
begin
  Params := '/C winget.exe install --id ' + WingetId +
    ' --exact --silent --accept-package-agreements --accept-source-agreements --disable-interactivity';
  Result := Exec('cmd.exe', Params, '', SW_HIDE, ewWaitUntilTerminated, ExitCode);
  if not Result then
    ExitCode := -1;
end;

{ Runs winget for one ticked component and records its id as "attempted" regardless of outcome -
  per docs/specs/07-installer.md's "Contract with first-run setup", the registry marker lists
  every component the installer ran winget for, not only the ones that succeeded. First-run setup
  always re-detects the real state itself; the marker only steers its default checkbox state. }
procedure TryInstallComponent(const DisplayName, WingetId, ComponentId: string;
  AttemptedIds: TStringList);
var
  ExitCode: Integer;
  Started: Boolean;
begin
  ComponentProgressPage.SetText('Installing ' + DisplayName + '...', '');
  ComponentProgressPage.Show;
  try
    Started := RunWingetInstall(WingetId, ExitCode);
    AttemptedIds.Add(ComponentId);

    if not Started then
      Log('PCManager.iss: could not start winget for ' + WingetId + '.')
    else if IsAlreadyInstalledOrSuccess(ExitCode) then
      Log('PCManager.iss: ' + DisplayName + ' installed (or already installed); winget exit code ' + IntToStr(ExitCode) + '.')
    else
      Log('PCManager.iss: winget install for ' + WingetId + ' returned exit code ' + IntToStr(ExitCode) +
        '; PC Manager''s first-run setup can retry this later.');
  finally
    ComponentProgressPage.Hide;
  end;
end;

function IndexOfToken(List: TStringList; const Token: string): Integer;
var
  I: Integer;
begin
  Result := -1;
  for I := 0 to List.Count - 1 do
    if List[I] = Token then
    begin
      Result := I;
      Exit;
    end;
end;

procedure AddUniqueToken(List: TStringList; Token: string);
begin
  Token := Trim(Token);
  if (Token <> '') and (IndexOfToken(List, Token) < 0) then
    List.Add(Token);
end;

{ Splits a comma-separated list into unique, trimmed tokens appended to List. Hand-rolled (rather
  than TStringList.CommaText/Delimiter) to avoid any quoting surprises - component ids are plain
  lowercase tokens with no commas or spaces of their own. }
procedure SplitCsvInto(CsvText: string; List: TStringList);
var
  CommaPos: Integer;
begin
  CsvText := CsvText + ',';
  while Length(CsvText) > 0 do
  begin
    CommaPos := Pos(',', CsvText);
    if CommaPos = 0 then
      Break;
    AddUniqueToken(List, Copy(CsvText, 1, CommaPos - 1));
    CsvText := Copy(CsvText, CommaPos + 1, Length(CsvText));
  end;
end;

{ Writes HKLM\Software\PC Manager\Installer\Components, merging with whatever is already there
  (e.g. a repair install) rather than overwriting it - see the installer contract in
  docs/specs/07-installer.md. }
procedure MergeInstallerHandledComponents(AttemptedIds: TStringList);
var
  Existing, Merged: string;
  MergedList: TStringList;
  I: Integer;
begin
  if AttemptedIds.Count = 0 then
    Exit;

  MergedList := TStringList.Create;
  try
    if RegQueryStringValue(HKLM, InstallerRegistryKey, InstallerRegistryValue, Existing) then
      SplitCsvInto(Existing, MergedList);

    for I := 0 to AttemptedIds.Count - 1 do
      AddUniqueToken(MergedList, AttemptedIds[I]);

    Merged := '';
    for I := 0 to MergedList.Count - 1 do
    begin
      if I > 0 then
        Merged := Merged + ',';
      Merged := Merged + MergedList[I];
    end;

    { RegWriteStringValue creates Software\PC Manager\Installer if it does not already exist. }
    RegWriteStringValue(HKLM, InstallerRegistryKey, InstallerRegistryValue, Merged);
  finally
    MergedList.Free;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  AttemptedIds: TStringList;
begin
  if CurStep <> ssPostInstall then
    Exit;

  if not IsWingetAvailable() then
  begin
    if IsComponentSelected('anydesk') or IsComponentSelected('openrgb') or IsComponentSelected('pawnio') then
      SuppressibleMsgBox(
        'Winget was not found on this PC, so the optional components you selected were not ' +
        'installed. You can install them later from PC Manager''s "Set up optional features".',
        mbInformation, MB_OK, IDOK);
    Exit;
  end;

  AttemptedIds := TStringList.Create;
  try
    if IsComponentSelected('anydesk') then
      TryInstallComponent('AnyDesk (remote help)', 'AnyDesk.AnyDesk', 'anydesk', AttemptedIds);
    if IsComponentSelected('openrgb') then
      TryInstallComponent('OpenRGB (RGB lighting)', 'OpenRGB.OpenRGB', 'openrgb', AttemptedIds);
    if IsComponentSelected('pawnio') then
      TryInstallComponent('PawnIO driver (fan control)', 'namazso.PawnIO', 'pawnio', AttemptedIds);

    MergeInstallerHandledComponents(AttemptedIds);
  finally
    AttemptedIds.Free;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: string;
begin
  if CurUninstallStep <> usPostUninstall then
    Exit;

  { Not required by the installer contract (a stale marker is harmless - see
    docs/specs/07-installer.md), but nothing reads it after uninstall either, so remove it to
    leave the registry clean for a future reinstall. }
  RegDeleteValue(HKLM, InstallerRegistryKey, InstallerRegistryValue);

  { Per-machine uninstall runs elevated as whichever account launched it - normally the same
    signed-in user (UAC keeps the same user token), but it does not have to be, e.g. a different
    administrator account. {userappdata} here always resolves to the account actually running
    this uninstaller, so only offer to delete that account's data; another account's PC Manager
    data (if any) is left untouched rather than risk deleting the wrong profile's files. }
  DataDir := ExpandConstant('{userappdata}') + '\PCManager';
  if DirExists(DataDir) then
  begin
    if SuppressibleMsgBox(
      'Delete PC Manager settings and logs for the current Windows account?' + #13#10 +
      DataDir + #13#10#13#10 +
      'This only affects the account you are using now - if another Windows ' +
      'account on this PC used PC Manager, its data is left in place.',
      mbConfirmation, MB_YESNO, IDNO) = IDYES then
      DelTree(DataDir, True, True, True);
  end;

  SuppressibleMsgBox(
    'PC Manager has been uninstalled. AnyDesk, OpenRGB and the PawnIO driver are separate ' +
    'applications and were not removed - uninstall them individually from Windows Settings if ' +
    'you no longer need them.',
    mbInformation, MB_OK, IDOK);
end;
