; CA-VAL-AIM (formerly ValTraining / ValTrainer) - Inno Setup 6 script.
; Built by tools\build-release.ps1, which passes everything below with /D... (see its header). By hand:
;   ISCC.exe /DAppVersion=1.0.0 /DAppFileVersion=1.0.0.0 /DSourceDir=<"Windows Installer" export folder with
;            LICENSE.txt and THIRD-PARTY-NOTICES.txt added> /DIconFile=<ValTrainer.ico> /DOutputDir=<dist> installer\ValTrainer.iss
; Optional: /DGitHubRepo=owner/repo (publisher / support / update links; omitted while it is the OWNER placeholder),
;           /DSIGN with /Ssigntool=... (signs Setup.exe and the uninstaller).
; All-users install by default (one UAC prompt): Program Files, shortcuts for every user. /CURRENTUSER keeps a per-user
; copy in %LOCALAPPDATA%\Programs, which is what in-app updates from 2.0.0 and older per-user installs use.
;
; Automatic updates (ValTrainerGodot\src\Core\Updater*.cs): the app downloads the next release's Setup.exe, checks its
; SHA-256 against the release's SHA256SUMS.txt and runs
;   Setup.exe /SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /SP- /CURRENTUSER|/ALLUSERS /RELAUNCH
;             [/RELAUNCHARGS=arg1|arg2|...] /LOG=<file>
; /RELAUNCH (silent installs only) starts CA-VAL-AIM again when Setup is done, with the '|'-separated arguments of
; /RELAUNCHARGS. install.ini next to CA-VAL-AIM.exe tells the app it was installed by this Setup (AppId and all-users or
; per-user mode); the app also checks that Windows' uninstall entry for that AppId points at its own folder.
; Setup and the uninstaller wait until CA-VAL-AIM.exe (or, on an update from 1.17.0 or older, ValTrainer.exe) has exited before changing anything (see PrepareToInstall).
; Interactive installs behave exactly as before.

#ifndef AppVersion
  #define AppVersion "0.0.0-dev"
#endif
#ifndef AppFileVersion
  #define AppFileVersion "0.0.0.0"
#endif
#ifndef SourceDir
  #error Pass /DSourceDir=<folder with CA-VAL-AIM.exe, CA-VAL-AIM.pck, data_ValTrainer_windows_x86_64, LICENSE.txt, THIRD-PARTY-NOTICES.txt>
#endif
#ifndef IconFile
  #define IconFile AddBackslash(SourcePath) + "ValTrainer.ico"
#endif
#ifndef OutputDir
  #define OutputDir AddBackslash(SourcePath) + "..\dist"
#endif
#ifndef GitHubRepo
  #define GitHubRepo "Pyromesis/CA-VAL-AIM"
#endif
#define HasRepo (Pos("OWNER", GitHubRepo) == 0 && Pos("/", GitHubRepo) > 0)

; Never change: identifies the installed app for upgrades and uninstall.
#define AppGuid "8BC1A72E-9D6C-4649-AE20-032F14D7A1F5"
#define AppExe "CA-VAL-AIM.exe"
; The executable's name up to 1.17.0. An update from those versions is started by the OLD app, so Setup must wait for
; this exe to exit too (see PrepareToInstall) and then removes it ([InstallDelete]).
#define LegacyExe "ValTrainer.exe"
; Release file prefix: still "ValTraining" so that 1.16.1-1.17.0 find the update (UpdateAssets.ReleasePrefix in the app).
#ifndef AssetPrefix
  #define AssetPrefix "ValTraining"
#endif

[Setup]
AppId={{{#AppGuid}}
AppName=CA-VAL-AIM
AppVersion={#AppVersion}
AppVerName=CA-VAL-AIM {#AppVersion}
AppPublisher=Pyromesis
AppCopyright=Copyright (C) 2026 Pyromesis and contributors. Formerly ValTraining, based on ValTrainer (https://github.com/idoraz1/ValTrainer); see NOTICE.txt. GPL-3.0-or-later.
#if HasRepo
AppPublisherURL=https://github.com/{#GitHubRepo}
AppSupportURL=https://github.com/{#GitHubRepo}/issues
AppUpdatesURL=https://github.com/{#GitHubRepo}/releases
#endif
AppComments=Free, fan-made VALORANT-style aim trainer. Not affiliated with Riot Games.
VersionInfoVersion={#AppFileVersion}
VersionInfoProductVersion={#AppFileVersion}
; Product version x.y.z.0 like CA-VAL-AIM.exe (SignPath Foundation: one product version for all signed files).
VersionInfoProductTextVersion={#AppFileVersion}
VersionInfoTextVersion={#AppVersion}
VersionInfoCompany=Pyromesis
VersionInfoDescription=CA-VAL-AIM Setup
VersionInfoProductName=CA-VAL-AIM
VersionInfoCopyright=Copyright (C) 2026 Pyromesis and contributors. GPL-3.0-or-later. See NOTICE.txt.

; All users by default: one UAC prompt, Program Files, shortcuts for every user. /CURRENTUSER (used by in-app updates
; from per-user installs) keeps a per-user copy in %LOCALAPPDATA%\Programs. Upgrades keep the previous folder
; (UsePreviousAppDir), but a folder under the user's %TEMP% or one that no longer exists is ignored (InitializeWizard).
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
DefaultDirName={autopf}\CA-VAL-AIM
DefaultGroupName=CA-VAL-AIM
DisableProgramGroupPage=yes
; The Start menu folder follows the rename (1.18.0) instead of reusing the old ValTraining folder.
UsePreviousGroup=no
UsePreviousAppDir=yes

; 64-bit Windows 10 1607+ (also runs on Windows 11 on ARM through x64 emulation).
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.14393

#if Ver >= EncodeVer(6, 6, 0)
WizardStyle=modern dynamic
#else
WizardStyle=modern
#endif
LicenseFile={#SourceDir}\LICENSE.txt
SetupIconFile={#IconFile}
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName=CA-VAL-AIM
ShowLanguageDialog=no

Compression=lzma2/max
SolidCompression=yes
LZMAUseSeparateProcess=yes
OutputDir={#OutputDir}
OutputBaseFilename={#AssetPrefix}-{#AppVersion}-Setup

; Close a running CA-VAL-AIM (Restart Manager) before replacing its files.
CloseApplications=yes
; One Setup at a time (e.g. an update started by the app and one started by hand).
SetupMutex=ValTrainerSetup{#AppGuid}
CloseApplicationsFilter=*.exe,*.dll,*.pck
RestartApplications=no
SetupLogging=yes

#ifdef SIGN
SignTool=signtool
SignedUninstaller=yes
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[InstallDelete]
; Upgrades: drop the previous version's .NET assemblies so no stale DLL survives next to the new ones.
Type: filesandordirs; Name: "{app}\data_ValTrainer_windows_x86_64"
; Rebrand: remove the shortcuts the old ValTrainer/Valtraining installer left in the Start menu.
Type: files; Name: "{autoprograms}\ValTrainer\*"
Type: dirifempty; Name: "{autoprograms}\ValTrainer"
Type: files; Name: "{autodesktop}\ValTrainer.lnk"
; Rename to CA-VAL-AIM (1.18.0): the old executable, its .pck and the ValTraining shortcuts.
Type: files; Name: "{app}\{#LegacyExe}"
Type: files; Name: "{app}\ValTrainer.pck"
Type: files; Name: "{app}\ValTrainer.exe.old"
Type: files; Name: "{autoprograms}\ValTraining.lnk"
Type: files; Name: "{autoprograms}\ValTraining (safe graphics).lnk"
Type: files; Name: "{autoprograms}\ValTraining\*"
Type: dirifempty; Name: "{autoprograms}\ValTraining"
Type: files; Name: "{autodesktop}\ValTraining.lnk"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[INI]
; Marker for the in-app updater (see the header); removed by the uninstaller.
Filename: "{app}\install.ini"; Section: "ValTrainer"; Key: "AppId"; String: "{#AppGuid}"; Flags: uninsdeletesection
Filename: "{app}\install.ini"; Section: "ValTrainer"; Key: "Mode"; String: "{code:InstallModeName}"; Flags: uninsdeletesection

[UninstallDelete]
Type: files; Name: "{app}\install.ini"

[Icons]
Name: "{group}\CA-VAL-AIM"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Comment: "Aim trainer with your own VALORANT settings"
Name: "{group}\CA-VAL-AIM (safe graphics)"; Filename: "{app}\{#AppExe}"; Parameters: "--rendering-method gl_compatibility"; WorkingDir: "{app}"; Comment: "Starts CA-VAL-AIM with the OpenGL Compatibility renderer, for graphics drivers that crash or show a black screen"
Name: "{autodesktop}\CA-VAL-AIM"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,CA-VAL-AIM}"; Flags: nowait postinstall skipifsilent runasoriginaluser
; In-app update (silent + /RELAUNCH): start the new version again, as the user (never elevated).
Filename: "{app}\{#AppExe}"; Parameters: "{code:RelaunchParams}"; WorkingDir: "{app}"; Flags: nowait runasoriginaluser; Check: ShouldRelaunch

[Code]
{ ---------- Semantic Versioning precedence (1.2.0 > 1.2.0-rc.1 > 1.2.0-beta.2 > 1.1.9) ---------- }

function IsDigits(const S: String): Boolean;
var
  I: Integer;
begin
  Result := Length(S) > 0;
  for I := 1 to Length(S) do
    if (S[I] < '0') or (S[I] > '9') then
    begin
      Result := False;
      Exit;
    end;
end;

{ Removes and returns the text before the first '.' of S (all of S when there is none). }
function CutPart(var S: String): String;
var
  P: Integer;
begin
  P := Pos('.', S);
  if P = 0 then
  begin
    Result := S;
    S := '';
  end
  else
  begin
    Result := Copy(S, 1, P - 1);
    S := Copy(S, P + 1, MaxInt);
  end;
end;

function Sgn(N: Integer): Integer;
begin
  if N < 0 then Result := -1
  else if N > 0 then Result := 1
  else Result := 0;
end;

{ Splits "v1.2.3-beta.1+build" into core "1.2.3" and prerelease "beta.1". }
procedure SplitVersion(V: String; var Core, Pre: String);
var
  P: Integer;
begin
  V := Trim(V);
  if (Length(V) > 0) and ((V[1] = 'v') or (V[1] = 'V')) then Delete(V, 1, 1);
  P := Pos('+', V);
  if P > 0 then V := Copy(V, 1, P - 1);
  P := Pos('-', V);
  if P > 0 then
  begin
    Core := Copy(V, 1, P - 1);
    Pre := Copy(V, P + 1, MaxInt);
  end
  else
  begin
    Core := V;
    Pre := '';
  end;
end;

function CompareVersions(const A, B: String): Integer;
var
  CA, PA, CB, PB, XA, XB: String;
  I: Integer;
begin
  SplitVersion(A, CA, PA);
  SplitVersion(B, CB, PB);
  for I := 1 to 3 do
  begin
    Result := Sgn(StrToIntDef(CutPart(CA), 0) - StrToIntDef(CutPart(CB), 0));
    if Result <> 0 then Exit;
  end;
  if (PA = '') and (PB = '') then begin Result := 0; Exit; end;
  if PA = '' then begin Result := 1; Exit; end;   { a release ranks above its prereleases }
  if PB = '' then begin Result := -1; Exit; end;
  while (PA <> '') or (PB <> '') do
  begin
    if PA = '' then begin Result := -1; Exit; end;
    if PB = '' then begin Result := 1; Exit; end;
    XA := CutPart(PA);
    XB := CutPart(PB);
    if IsDigits(XA) and IsDigits(XB) then Result := Sgn(StrToIntDef(XA, 0) - StrToIntDef(XB, 0))
    else if IsDigits(XA) then Result := -1
    else if IsDigits(XB) then Result := 1
    else Result := Sgn(CompareStr(XA, XB));
    if Result <> 0 then Exit;
  end;
  Result := 0;
end;

{ ---------- upgrade / downgrade ---------- }

function InstalledVersion(): String;
var
  Key: String;
begin
  Result := '';
  Key := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{' + '{#AppGuid}' + '}_is1';
  if RegQueryStringValue(HKCU, Key, 'DisplayVersion', Result) then Exit;
  if IsWin64 and RegQueryStringValue(HKLM64, Key, 'DisplayVersion', Result) then Exit;
  if RegQueryStringValue(HKLM, Key, 'DisplayVersion', Result) then Exit;
  Result := '';
end;

function InitializeSetup(): Boolean;
var
  Old: String;
begin
  Result := True;
  Old := InstalledVersion();
  { Same or older version installed: Setup simply installs over it (settings and stats live elsewhere and are kept). }
  if (Old <> '') and (CompareVersions(Old, '{#AppVersion}') > 0) then
    Result := SuppressibleMsgBox(
      'CA-VAL-AIM ' + Old + ' is installed, which is newer than this setup (' + '{#AppVersion}' + ').' + #13#10#13#10 +
      'Replace it with the older version ' + '{#AppVersion}' + '? Your settings and stats are kept.',
      mbConfirmation, MB_YESNO or MB_DEFBUTTON2, IDYES) = IDYES;
end;

{ ---------- install folder ---------- }

function PathStartsWith(const Path, Prefix: String): Boolean;
var
  P, Q: String;
begin
  P := Lowercase(Path);
  Q := Lowercase(Prefix);
  while (Length(Q) > 1) and (Q[Length(Q)] = '\') do
    Delete(Q, Length(Q), 1);
  Result := (Q <> '') and ((P = Q) or
    ((Length(P) > Length(Q)) and (Copy(P, 1, Length(Q)) = Q) and (P[Length(Q) + 1] = '\')));
end;

function IsUnderUserTemp(const Dir: String): Boolean;
begin
  Result := PathStartsWith(Dir, ExpandConstant('{%TEMP}')) or
            PathStartsWith(Dir, ExpandConstant('{%TMP}')) or
            PathStartsWith(Dir, ExpandConstant('{localappdata}\Temp'));
end;

function DirParamGiven(): Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(Copy(ParamStr(I), 1, 5), '/DIR=') = 0 then
    begin
      Result := True;
      Exit;
    end;
end;

procedure InitializeWizard();
var
  Prev: String;
begin
  { An explicit /DIR=... wins. Otherwise a previous folder that no longer exists or lives in the user's temp folder is
    not a valid default: a stale uninstall entry used to send new installs to %TEMP%. }
  if DirParamGiven() then Exit;
  Prev := WizardForm.DirEdit.Text;
  if (Prev = '') or (not DirExists(Prev)) or IsUnderUserTemp(Prev) then
    WizardForm.DirEdit.Text := ExpandConstant('{autopf}\CA-VAL-AIM');
end;

{ ---------- in-app updates ---------- }

function InstallModeName(Param: String): String;
begin
  if IsAdminInstallMode then Result := 'admin' else Result := 'user';
end;

{ True for a silent install started with /RELAUNCH (the app's updater): start ValTrainer again at the end. }
function ShouldRelaunch(): Boolean;
var
  I: Integer;
begin
  Result := False;
  if not WizardSilent then Exit;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), '/RELAUNCH') = 0 then
    begin
      Result := True;
      Exit;
    end;
end;

{ /RELAUNCHARGS=a|b|c -> a b c (each quoted when it contains a space): the arguments the app was started with. }
function RelaunchParams(Param: String): String;
var
  S, A: String;
  P: Integer;
begin
  Result := '';
  S := ExpandConstant('{param:RELAUNCHARGS|}');
  while S <> '' do
  begin
    P := Pos('|', S);
    if P = 0 then
    begin
      A := S;
      S := '';
    end
    else
    begin
      A := Copy(S, 1, P - 1);
      S := Copy(S, P + 1, MaxInt);
    end;
    if A <> '' then
    begin
      if Result <> '' then Result := Result + ' ';
      Result := Result + AddQuotes(A);
    end;
  end;
end;

{ ---------- wait until the app has exited ---------- }
{ The app quits right after starting an update, but shutting down can take a few seconds. Setup must not touch any file
  before then: [InstallDelete] would remove the .NET files the app hasn't loaded, a still-locked exe would
  then abort the install, and the rollback can't bring the deleted files back (1.4.1 -> 1.4.2 broke like that).
  Nothing is ever killed: Setup only waits (silent) or asks the player to close it (interactive). }

{ True while some process runs this exe (CA-VAL-AIM.exe or the legacy ValTrainer.exe): Windows refuses to open a running exe for writing. }
function ExeInUse(const Exe: String): Boolean;
var
  F: TFileStream;
begin
  Result := False;
  if not FileExists(Exe) then Exit;
  try
    F := TFileStream.Create(Exe, fmOpenReadWrite or fmShareExclusive);
    F.Free;
  except
    Result := True;
  end;
end;

{ Waits up to Seconds for Exe to be free; True when it is. }
function WaitForExit(const Exe: String; Seconds: Integer): Boolean;
var
  I: Integer;
begin
  for I := 1 to Seconds * 4 do
  begin
    if not ExeInUse(Exe) then
    begin
      Result := True;
      Exit;
    end;
    Sleep(250);
  end;
  Result := not ExeInUse(Exe);
end;

{ Silent: wait up to 90 s, then give up without changing anything. Interactive: ask until it's closed or cancelled. }
function WaitUntilClosed(const Exe: String; Silent: Boolean): Boolean;
begin
  if Silent then
  begin
    Result := WaitForExit(Exe, 90);
    if not Result then Log('CA-VAL-AIM is still running after 90 s: nothing was changed.');
    Exit;
  end;
  Result := WaitForExit(Exe, 3);
  while not Result do
  begin
    if MsgBox('CA-VAL-AIM is still running.' + #13#10#13#10 + 'Close it, then click Retry.',
              mbError, MB_RETRYCANCEL) <> IDRETRY then Exit;
    Result := WaitForExit(Exe, 3);
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  { Both names: an update from 1.17.0 or older is started by the old ValTrainer.exe. }
  if not WaitUntilClosed(ExpandConstant('{app}\{#AppExe}'), WizardSilent)
     or not WaitUntilClosed(ExpandConstant('{app}\{#LegacyExe}'), WizardSilent) then
    Result := 'CA-VAL-AIM is still running. Close it and run Setup again.';
end;

function InitializeUninstall(): Boolean;
begin
  Result := WaitUntilClosed(ExpandConstant('{app}\{#AppExe}'), UninstallSilent)
            and WaitUntilClosed(ExpandConstant('{app}\{#LegacyExe}'), UninstallSilent);
end;

{ ---------- uninstall: optionally remove the user's data ---------- }

var
  DeleteUserData: Boolean;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    { Default NO; a silent uninstall never deletes user data. }
    DeleteUserData := False;
    if not UninstallSilent then
      DeleteUserData := MsgBox(
        'Also delete your CA-VAL-AIM settings, stats and recordings?' + #13#10#13#10 +
        'This removes:' + #13#10 +
        '  ' + ExpandConstant('{userappdata}\ValTrainer') + #13#10 +
        '      settings, stats and recordings' + #13#10 +
        '  ' + ExpandConstant('{userappdata}\Godot\app_userdata\ValTrainer') + #13#10 +
        '      logs and shader cache' + #13#10 +
        '  ' + ExpandConstant('{localappdata}\data_ValTrainer_windows_x86_64') + #13#10 +
        '      runtime unpacked by the portable version' + #13#10#13#10 +
        'Choose No to keep them (for example to reinstall later).',
        mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
  end;
  if CurUninstallStep = usPostUninstall then
  begin
    { Updates the app downloaded (not user data): always removed. }
    DelTree(ExpandConstant('{localappdata}\ValTrainer\updates'), True, True, True);
    RemoveDir(ExpandConstant('{localappdata}\ValTrainer'));
  end;
  if (CurUninstallStep = usPostUninstall) and DeleteUserData then
  begin
    DelTree(ExpandConstant('{userappdata}\ValTrainer'), True, True, True);
    DelTree(ExpandConstant('{userappdata}\Godot\app_userdata\ValTrainer'), True, True, True);
    DelTree(ExpandConstant('{localappdata}\data_ValTrainer_windows_x86_64'), True, True, True);
  end;
end;
