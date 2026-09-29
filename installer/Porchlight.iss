; Porchlight installer (Inno Setup 6). See docs/specs/07-installer.md for the full spec and the
; "Contract with first-run setup" this script implements together with
; Porchlight.Core.Components.IRegistryReader.GetInstallerHandledComponentIds.
;
; Built by CI only (.github/workflows/ci.yml, .github/workflows/release.yml) - never run locally
; on a real PC. CI invokes it as:
;   ISCC installer\Porchlight.iss /DMyAppVersion=<Version from Directory.Build.props> ^
;     /DPublishDir=<path to the published single-file build> /O<output dir>

#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\publish"
#endif

#define MyAppName "Porchlight"
#define MyAppPublisher "El1rans"
#define MyAppExeName "Porchlight.exe"
#define MyAppMutex "PorchlightAppMutex"
; Pre-rebrand mutex name (docs/specs/08-rebrand-porchlight.md) - listed alongside MyAppMutex in
; AppMutex below so Setup still detects, and offers to close, a still-running old "PC Manager"
; build when upgrading it in place.
#define MyLegacyAppMutex "PCManagerAppMutex"
; Fixed forever - identifies upgrades/uninstalls across versions. Do not regenerate. Kept
; unchanged from the "PC Manager" installer so installing Porchlight over it is treated as an
; in-place upgrade (one entry in Apps & features) rather than a second, separate install.
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
AppPublisherURL=https://github.com/El1rans/porchlight
AppSupportURL=https://github.com/El1rans/porchlight
; Sets the compiled Setup.exe's own FileVersion/ProductVersion resource, which is otherwise blank.
VersionInfoVersion={#MyAppVersion}
; Explicit (Inno defaults this to AppName already) so it's obvious this must keep matching
; Directory.Build.props's <Product> and docs/signing/*.xml's product-name="Porchlight" metadata
; restriction - SignPath Foundation requires artifact metadata restrictions.
VersionInfoProductName={#MyAppName}
DefaultDirName={autopf}\Porchlight
; Forces the install dir to DefaultDirName above rather than reusing a previous install's chosen
; directory - an upgrade from "PC Manager" (installed to {autopf}\PC Manager) must move to
; {autopf}\Porchlight, not silently keep installing into the old folder name.
UsePreviousAppDir=no
DefaultGroupName=Porchlight
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=..\dist
OutputBaseFilename=Porchlight-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
; PawnIO's driver install (and AnyDesk's per-machine install) both need admin anyway - see
; docs/specs/01b-components.md.
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\assets\brand\porchlight.ico
WizardImageFile=..\assets\brand\wizard-100.bmp,..\assets\brand\wizard-200.bmp
WizardSmallImageFile=..\assets\brand\wizard-small-100.bmp,..\assets\brand\wizard-small-200.bmp
; Matches App.AppMutexName/App.GlobalAppMutexName (src/Porchlight.App/App.xaml.cs). Two names
; because Porchlight can autostart in ANY signed-in user's session via {commonstartup}: a plain
; (implicitly "Local\") named mutex is only visible within its own session, so a "Global\" one is
; also needed for Setup - running in whichever session launched it, possibly a different one - to
; detect an instance running in another user's session. This is independent of CloseApplications
; below (Restart Manager-based file-lock detection, which Inno enables by default regardless of
; AppMutex) - set explicitly here so both mechanisms are clearly intentional, not one instead of
; the other.
; Also lists the pre-rebrand mutex names (MyLegacyAppMutex) so upgrading over a still-running old
; "PC Manager" build is detected too - see docs/specs/08-rebrand-porchlight.md.
AppMutex={#MyAppMutex},Global\{#MyAppMutex},{#MyLegacyAppMutex},Global\{#MyLegacyAppMutex}
CloseApplications=yes
WizardStyle=modern
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Types]
; The only type; "iscustom" hides the Setup-type combo box entirely, leaving just the plain
; [Components] checkbox list below - see the [Components] comment for why this section exists.
Name: "custom"; Description: "Custom"; Flags: iscustom

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
; Per-machine install, so a per-user HKCU Run key or Startup-folder shortcut for the installing
; user only would be wrong for a shared PC. A Common Startup folder shortcut ({commonstartup})
; starts Porchlight for every account that signs in, still shows up in Task Manager's Startup
; tab (unlike an HKLM Run key, which is invisible there and harder for a family member to
; discover/disable), and is removed automatically by the uninstaller like any other shortcut -
; no extra [Code] needed to clean up an HKLM Run value on uninstall.
Name: "startupicon"; Description: "Start Porchlight when anyone signs in to this PC"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

; One checkbox per optional third-party component (installed via winget in the [Code] section
; below, not as installer-bundled files). Without a [Types] section, Inno implicitly adds
; "Full/Compact/Custom installation" types and - since no component would belong to any of them -
; a fresh install's default type ("Full") would have every component unticked, and a silent
; install with no /COMPONENTS= override would install none of them. The single "custom" type
; above avoids that: "anydesk" belongs to it (so it is ticked by default); "openrgb"/"pawnio"
; belong to no type, so they default to unticked but remain independently toggleable, matching
; docs/specs/07-installer.md. An unattended install can override the selection with
; /COMPONENTS="anydesk,openrgb,pawnio" (see README.md's "Install" section).
[Components]
Name: "anydesk"; Description: "Remote help from family (AnyDesk)"; Types: custom
Name: "openrgb"; Description: "RGB lighting control (OpenRGB)"
Name: "pawnio"; Description: "Fan control and temperature sensors (PawnIO driver) - installs a signed hardware driver"

; Cleans up the pre-rebrand "PC Manager" install this upgrades in place (same AppId, but
; UsePreviousAppDir=no moves the files to a new folder - see docs/specs/08-rebrand-porchlight.md).
; Inno Setup processes [InstallDelete] early, before [Files] copies anything - but that order
; does not matter here either way, since the paths below are the fixed old "PC Manager" locations,
; never {app} (which is always the new "Porchlight" directory/shortcuts, a different literal path
; on every install, upgrade or fresh). Every entry is a no-op if its path does not exist, so a
; fresh (non-upgrade) install - where none of this ever existed - is unaffected.
[InstallDelete]
Type: files; Name: "{autopf}\PC Manager\PCManager.exe"
; The old build's own uninstaller (Inno Setup always names it unins000.exe/.dat) also has to go,
; or it - not the app exe above - is the one file left in {autopf}\PC Manager, and the
; dirifempty just below would then never fire (a non-empty directory is left behind forever).
Type: files; Name: "{autopf}\PC Manager\unins000.exe"
Type: files; Name: "{autopf}\PC Manager\unins000.dat"
Type: dirifempty; Name: "{autopf}\PC Manager"
; {commonprograms}\PC Manager is the old DefaultGroupName's Start Menu folder (a literal path, not
; {group} - {group} may already have been rewritten to the new "Porchlight" group by the time this
; runs, so it cannot be relied on to still mean the old folder).
Type: files; Name: "{commonprograms}\PC Manager\PC Manager.lnk"
Type: files; Name: "{commonprograms}\PC Manager\Uninstall PC Manager.lnk"
Type: dirifempty; Name: "{commonprograms}\PC Manager"
Type: files; Name: "{autodesktop}\PC Manager.lnk"
Type: files; Name: "{commonstartup}\PC Manager.lnk"

[Files]
Source: "{#PublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Porchlight"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall Porchlight"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Porchlight"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{commonstartup}\Porchlight"; Filename: "{app}\{#MyAppExeName}"; Tasks: startupicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Porchlight"; Flags: nowait postinstall skipifsilent

; Removes the "start when I sign in" scheduled task Porchlight can register itself
; (docs/specs/24-start-at-login.md). The task name must match LoginLaunchTaskXml.TaskName. Setup
; already runs elevated (PrivilegesRequired=admin), which deleting a HighestAvailable task needs;
; a missing task just makes schtasks exit non-zero, which is harmless. Independent of the optional
; {commonstartup} shortcut above, which is removed by Inno itself.
[UninstallRun]
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""Porchlight"" /F"; Flags: runhidden; RunOnceId: "DeletePorchlightSignInTask"

[Code]
const
  { winget's own hex exit codes for "the package/an equivalent version is already installed",
    expressed as the signed 32-bit integers Pascal Script compares Exec's ResultCode against.
    Mirrors Porchlight.Core.Processes.WingetExitCodes - keep in sync with that file. }
  WINGET_PACKAGE_ALREADY_INSTALLED = -1978335135; { 0x8A150061 }
  WINGET_UPDATE_NOT_APPLICABLE     = -1978335189; { 0x8A15002B }
  WINGET_INSTALL_ALREADY_INSTALLED = -1978334963; { 0x8A15010D }

  { ERROR_TIMEOUT (Win32), returned by RunWingetInstall itself (not by winget) when the process
    does not exit within WINGET_INSTALL_TIMEOUT_MS - see RunWingetInstall. }
  ERROR_TIMEOUT_EXIT_CODE = 1460;
  WINGET_INSTALL_TIMEOUT_MS = 600000; { 10 minutes. }

  InstallerRegistryKey = 'Software\Porchlight\Installer';
  InstallerParentRegistryKey = 'Software\Porchlight';
  InstallerRegistryValue = 'Components';

  { Pre-rebrand key (docs/specs/08-rebrand-porchlight.md) - this installer never writes here (see
    MergeInstallerHandledComponents, which only reads it as a fallback so an upgrade that skipped
    a repair install still gets the "already installer-handled" hint), but uninstall still cleans
    it up if present, for the same reason it cleans up InstallerRegistryKey. }
  LegacyInstallerRegistryKey = 'Software\PC Manager\Installer';
  LegacyInstallerParentRegistryKey = 'Software\PC Manager';

var
  ComponentProgressPage: TOutputProgressWizardPage;

procedure InitializeWizard();
begin
  ComponentProgressPage := CreateOutputProgressPage(
    'Setting up optional components',
    'Please wait while Setup installs the components you selected via winget.');
end;

{ True if winget.exe can be located at all (via cmd.exe /C "where", so it also picks up the
  per-user App Execution Alias winget is normally published as). }
function IsWingetAvailable(): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec(ExpandConstant('{sys}\cmd.exe'), '/C where winget.exe >nul 2>nul', '', SW_HIDE,
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

{ Wraps a string in single quotes for use as a PowerShell string literal, doubling any embedded
  single quote (PowerShell's own escaping rule) - defense in depth, since every caller here only
  ever passes a plain winget package id with no quotes or spaces of its own. Built via Chr(39)
  rather than Pascal's doubled-quote literal syntax to keep the quote-counting unambiguous. }
function PsQuote(const S: string): string;
var
  QuoteChar: string;
  Doubled: string;
  Ch: string;
  I: Integer;
begin
  { Everything kept as "string" (via Copy, rather than indexing S[I] as a Char) so every
    comparison and concatenation below is string-to-string - unambiguous regardless of how
    strictly this Pascal Script implementation distinguishes Char from a 1-character string. }
  QuoteChar := Chr(39);
  Doubled := '';
  for I := 1 to Length(S) do
  begin
    Ch := Copy(S, I, 1);
    if Ch = QuoteChar then
      Doubled := Doubled + QuoteChar + QuoteChar
    else
      Doubled := Doubled + Ch;
  end;
  Result := QuoteChar + Doubled + QuoteChar;
end;

{ Runs "winget install --id <WingetId> ..." through PowerShell's Start-Process -PassThru so the
  wait can be bounded: a hung winget (or a hung child installer, e.g. a driver with a stuck UI
  prompt) must not hang the rest of Setup forever. On timeout the process is killed and ExitCode
  is set to ERROR_TIMEOUT_EXIT_CODE, which IsAlreadyInstalledOrSuccess correctly treats as neither
  success nor "cancelled by user", so it falls through to the same "failed, first-run setup can
  retry later" handling as any other non-zero winget exit code. }
function RunWingetInstall(const WingetId: string; var ExitCode: Integer): Boolean;
var
  ArgumentList: string;
  PsCommand: string;
  Params: string;
begin
  ArgumentList :=
    PsQuote('install') + ',' + PsQuote('--id') + ',' + PsQuote(WingetId) + ',' +
    PsQuote('--exact') + ',' + PsQuote('--source') + ',' + PsQuote('winget') + ',' +
    PsQuote('--silent') + ',' + PsQuote('--accept-package-agreements') + ',' +
    PsQuote('--accept-source-agreements') + ',' + PsQuote('--disable-interactivity');

  PsCommand :=
    { Prefer the per-user App Execution Alias by absolute path; fall back to the bare name (as
      before) if this account has no such alias. }
    '$w = Join-Path $env:LOCALAPPDATA ''Microsoft\WindowsApps\winget.exe''; ' +
    'if (-not (Test-Path -LiteralPath $w)) { $w = ''winget.exe'' }; ' +
    '$p = Start-Process -FilePath $w -ArgumentList ' + ArgumentList +
    ' -PassThru -WindowStyle Hidden; ' +
    'if (-not $p.WaitForExit(' + IntToStr(WINGET_INSTALL_TIMEOUT_MS) + ')) { ' +
    'try { $p.Kill() } catch {}; exit ' + IntToStr(ERROR_TIMEOUT_EXIT_CODE) + ' } ' +
    'else { exit $p.ExitCode }';

  Params := '-NoProfile -ExecutionPolicy Bypass -Command "' + PsCommand + '"';

  Result := Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Params, '', SW_HIDE, ewWaitUntilTerminated, ExitCode);
  if not Result then
    ExitCode := -1;
end;

{ Runs winget for one ticked component. Per docs/specs/07-installer.md's "Contract with first-run
  setup", the registry marker lists every component the installer actually ran winget for -
  regardless of whether that winget call went on to succeed - so the id is added whenever the
  process itself started, even if winget's own exit code was a failure. It is deliberately NOT
  added when RunWingetInstall could not even start the process (e.g. PowerShell itself missing or
  blocked by policy), since in that case Setup never attempted anything for this component and
  first-run setup should still default it to ticked. First-run setup always re-detects the real
  state itself either way; the marker only steers its default checkbox state. }
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

    if not Started then
      Log('Porchlight.iss: could not start winget for ' + WingetId + '.')
    else
    begin
      AttemptedIds.Add(ComponentId);
      if IsAlreadyInstalledOrSuccess(ExitCode) then
        Log('Porchlight.iss: ' + DisplayName + ' installed (or already installed); winget exit code ' + IntToStr(ExitCode) + '.')
      else
        Log('Porchlight.iss: winget install for ' + WingetId + ' returned exit code ' + IntToStr(ExitCode) +
          '; Porchlight''s first-run setup can retry this later.');
    end;
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

{ Writes HKLM\Software\Porchlight\Installer\Components, merging with whatever is already there
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

    { Also folds in the legacy key's value, if any (e.g. a repair install over a machine that was
      never actually launched since upgrading from "PC Manager", so nothing ever wrote the new
      key) - this installer only ever writes InstallerRegistryKey from here on, but preserves
      whatever the old one already recorded rather than silently dropping it. }
    if RegQueryStringValue(HKLM, LegacyInstallerRegistryKey, InstallerRegistryValue, Existing) then
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

    { RegWriteStringValue creates Software\Porchlight\Installer if it does not already exist. }
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
    if WizardIsComponentSelected('anydesk') or WizardIsComponentSelected('openrgb') or WizardIsComponentSelected('pawnio') then
      SuppressibleMsgBox(
        'Winget was not found on this PC, so the optional components you selected were not ' +
        'installed. You can install them later from Porchlight''s "Set up optional features".',
        mbInformation, MB_OK, IDOK);
    Exit;
  end;

  AttemptedIds := TStringList.Create;
  try
    if WizardIsComponentSelected('anydesk') then
      TryInstallComponent('AnyDesk (remote help)', 'AnyDesk.AnyDesk', 'anydesk', AttemptedIds);
    if WizardIsComponentSelected('openrgb') then
      TryInstallComponent('OpenRGB (RGB lighting)', 'OpenRGB.OpenRGB', 'openrgb', AttemptedIds);
    if WizardIsComponentSelected('pawnio') then
      TryInstallComponent('PawnIO driver (fan control)', 'namazso.PawnIO', 'pawnio', AttemptedIds);

    MergeInstallerHandledComponents(AttemptedIds);
  finally
    AttemptedIds.Free;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir, LegacyDataDir, Message: string;
  HasData, HasLegacyData: Boolean;
begin
  if CurUninstallStep <> usPostUninstall then
    Exit;

  { Not required by the installer contract (a stale marker is harmless - see
    docs/specs/07-installer.md), but nothing reads it after uninstall either, so remove it to
    leave the registry clean for a future reinstall. Also removes the two key levels Porchlight's
    installer owns if they end up empty (RegDeleteKeyIfEmpty is a no-op if anything else is still
    under them). }
  RegDeleteValue(HKLM, InstallerRegistryKey, InstallerRegistryValue);
  RegDeleteKeyIfEmpty(HKLM, InstallerRegistryKey);
  RegDeleteKeyIfEmpty(HKLM, InstallerParentRegistryKey);

  { Same cleanup for the pre-rebrand key (docs/specs/08-rebrand-porchlight.md) - this installer
    never writes it (see MergeInstallerHandledComponents), but an upgrade from "PC Manager" can
    still leave it behind, so uninstall cleans it up here too rather than leaving stale
    Software\PC Manager registry keys around forever. }
  RegDeleteValue(HKLM, LegacyInstallerRegistryKey, InstallerRegistryValue);
  RegDeleteKeyIfEmpty(HKLM, LegacyInstallerRegistryKey);
  RegDeleteKeyIfEmpty(HKLM, LegacyInstallerParentRegistryKey);

  { Per-machine uninstall runs elevated as whichever account launched it - normally the same
    signed-in user (UAC keeps the same user token), but it does not have to be, e.g. a different
    administrator account. The userappdata constant below always resolves to the account
    actually running this uninstaller, so only offer to delete that account's data; another
    account's Porchlight data (if any) is left untouched rather than risk deleting the wrong
    profile's files. }
  DataDir := ExpandConstant('{userappdata}') + '\Porchlight';
  LegacyDataDir := ExpandConstant('{userappdata}') + '\PCManager';
  HasData := DirExists(DataDir);
  HasLegacyData := DirExists(LegacyDataDir);

  if HasData or HasLegacyData then
  begin
    { Lists whichever of the two folders actually exist, rather than always naming just DataDir:
      the legacy folder (docs/specs/08-rebrand-porchlight.md) exists on every machine upgraded
      from "PC Manager" that has since been launched - AppDataMigrator only ever copies from it,
      never deletes it - so both paths are the common case, not an edge case. }
    Message := 'Delete Porchlight settings and logs for the current Windows account?' + #13#10;
    if HasData then
      Message := Message + DataDir + #13#10;
    if HasLegacyData then
      Message := Message + LegacyDataDir + #13#10;
    Message := Message + #13#10 +
      'This only affects the account you are using now - if another Windows ' +
      'account on this PC used Porchlight, its data is left in place.';

    if SuppressibleMsgBox(Message, mbConfirmation, MB_YESNO, IDNO) = IDYES then
    begin
      if HasData then
        DelTree(DataDir, True, True, True);
      { Deleting the legacy folder here is covered by the same single Yes/No prompt above (which
        now lists it explicitly when present), since it is the same "delete my Porchlight data"
        choice - see the Message comment above for why this is the common case, not the rare one. }
      if HasLegacyData then
        DelTree(LegacyDataDir, True, True, True);
    end;
  end;

  SuppressibleMsgBox(
    'Porchlight has been uninstalled. AnyDesk, OpenRGB and the PawnIO driver are separate ' +
    'applications and were not removed - uninstall them individually from Windows Settings if ' +
    'you no longer need them.',
    mbInformation, MB_OK, IDOK);
end;
