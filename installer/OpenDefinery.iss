#define AppName "OpenDefinery for Revit"
#define AppVersion "0.1.0"
#define AppPublisher "Triple Zero Labs, LLC"
#define SrcRoot ".."

; Per-TFM Release output, mapped to the Revit versions each build supports:
;   net48            -> Revit 2022, 2023, 2024
;   net8.0-windows   -> Revit 2025, 2026
;   net10.0-windows  -> Revit 2027
#define Net48 "bin\Release\net48"
#define Net80 "bin\Release\net8.0-windows"
#define Net10 "bin\Release\net10.0-windows"

[Setup]
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppId={{B7E4D9A2-6C31-4F8E-9A5D-2E7C1F0B3A64}
DefaultDirName={userappdata}\Autodesk\Revit\Addins
; Uninstaller lives in a non-year folder so Revit never tries to load it
; (Revit only scans {userappdata}\Autodesk\Revit\Addins\<year>\ for .addin files).
UninstallFilesDir={userappdata}\Autodesk\Revit\Addins\OpenDefinery
; No admin rights - installs per-user to %AppData%
PrivilegesRequired=lowest
DisableDirPage=yes
OutputDir=..\dist
OutputBaseFilename=OpenDefinery for Revit-{#AppVersion}
Compression=lzma2
SolidCompression=yes

[Code]
{ True if the Revit addins folder for a given version exists on this machine. }
function RevitInstalled(Version: String): Boolean;
begin
  Result := DirExists(ExpandConstant('{userappdata}\Autodesk\Revit\Addins\') + Version);
end;

function Revit2022: Boolean; begin Result := RevitInstalled('2022'); end;
function Revit2023: Boolean; begin Result := RevitInstalled('2023'); end;
function Revit2024: Boolean; begin Result := RevitInstalled('2024'); end;
function Revit2025: Boolean; begin Result := RevitInstalled('2025'); end;
function Revit2026: Boolean; begin Result := RevitInstalled('2026'); end;
function Revit2027: Boolean; begin Result := RevitInstalled('2027'); end;

[InstallDelete]
; Clean each year's shared DLL folder before copying, so stale deps from an older
; version don't linger. Runs before [Files].
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2022\TripleZeroLabs"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2023\TripleZeroLabs"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2024\TripleZeroLabs"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2025\TripleZeroLabs"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2026\TripleZeroLabs"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2027\TripleZeroLabs"

; Family Editor was folded into the Parameter Manager add-in. Remove the retired
; manifest left by earlier installs - it sits outside TripleZeroLabs, so the wipe
; above doesn't catch it, and Revit errors on startup if a manifest points at a
; DLL that no longer ships.
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2022\OpenDefinery-FamEditor.addin"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2023\OpenDefinery-FamEditor.addin"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2024\OpenDefinery-FamEditor.addin"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2025\OpenDefinery-FamEditor.addin"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2026\OpenDefinery-FamEditor.addin"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2027\OpenDefinery-FamEditor.addin"

[Files]
; -- Revit 2022 (net48) ----------------------------------------------------------
Source: "{#SrcRoot}\OD-ParamManager\{#Net48}\*.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2022"; Flags: ignoreversion; Check: Revit2022
Source: "{#SrcRoot}\OD-ParamManager\{#Net48}\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2022\TripleZeroLabs"; Excludes: "*.addin"; Flags: ignoreversion recursesubdirs; Check: Revit2022

; -- Revit 2023 (net48) ----------------------------------------------------------
Source: "{#SrcRoot}\OD-ParamManager\{#Net48}\*.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2023"; Flags: ignoreversion; Check: Revit2023
Source: "{#SrcRoot}\OD-ParamManager\{#Net48}\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2023\TripleZeroLabs"; Excludes: "*.addin"; Flags: ignoreversion recursesubdirs; Check: Revit2023

; -- Revit 2024 (net48) ----------------------------------------------------------
Source: "{#SrcRoot}\OD-ParamManager\{#Net48}\*.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024"; Flags: ignoreversion; Check: Revit2024
Source: "{#SrcRoot}\OD-ParamManager\{#Net48}\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\TripleZeroLabs"; Excludes: "*.addin"; Flags: ignoreversion recursesubdirs; Check: Revit2024

; -- Revit 2025 (net8.0-windows) -------------------------------------------------
Source: "{#SrcRoot}\OD-ParamManager\{#Net80}\*.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025"; Flags: ignoreversion; Check: Revit2025
Source: "{#SrcRoot}\OD-ParamManager\{#Net80}\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\TripleZeroLabs"; Excludes: "*.addin"; Flags: ignoreversion recursesubdirs; Check: Revit2025

; -- Revit 2026 (net8.0-windows) -------------------------------------------------
Source: "{#SrcRoot}\OD-ParamManager\{#Net80}\*.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026"; Flags: ignoreversion; Check: Revit2026
Source: "{#SrcRoot}\OD-ParamManager\{#Net80}\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\TripleZeroLabs"; Excludes: "*.addin"; Flags: ignoreversion recursesubdirs; Check: Revit2026

; -- Revit 2027 (net10.0-windows) ------------------------------------------------
Source: "{#SrcRoot}\OD-ParamManager\{#Net10}\*.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2027"; Flags: ignoreversion; Check: Revit2027
Source: "{#SrcRoot}\OD-ParamManager\{#Net10}\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2027\TripleZeroLabs"; Excludes: "*.addin"; Flags: ignoreversion recursesubdirs; Check: Revit2027

[UninstallDelete]
; Remove the shared DLL folders and the .addin manifests for every year.
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2022\TripleZeroLabs"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2023\TripleZeroLabs"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2024\TripleZeroLabs"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2025\TripleZeroLabs"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2026\TripleZeroLabs"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2027\TripleZeroLabs"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2022\OpenDefinery-*.addin"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2023\OpenDefinery-*.addin"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2024\OpenDefinery-*.addin"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2025\OpenDefinery-*.addin"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2026\OpenDefinery-*.addin"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2027\OpenDefinery-*.addin"
; Uninstaller self-dir (only removed when empty).
Type: dirifempty; Name: "{userappdata}\Autodesk\Revit\Addins\OpenDefinery"
