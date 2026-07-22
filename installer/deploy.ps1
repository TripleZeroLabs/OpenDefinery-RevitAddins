#Requires -Version 5.1
<#
.SYNOPSIS
    Bumps the version, builds Release, signs the OpenDefinery DLLs, compiles the
    Inno Setup installer, then signs the installer EXE.
.DESCRIPTION
    End-to-end release flow (modeled on the Amzn-Gateway pipeline):
    1. Prompts for (or accepts) the version to deploy.
    2. Writes that version into <Version> in every OpenDefinery csproj and into the
       AppVersion #define in installer\OpenDefinery.iss.
    3. Builds the solution in Release (net48 + net8.0-windows + net10.0-windows).
    4. Signs the OpenDefinery assemblies in the Release output directories.
    5. Compiles installer\OpenDefinery.iss with ISCC.exe into dist\.
    6. Signs the resulting installer EXE.

    Requires AZURE_CLIENT_ID, AZURE_CLIENT_SECRET, and AZURE_TENANT_ID in the
    environment (Azure Trusted Signing), Inno Setup 6, and the
    Microsoft.Trusted.Signing.Client package (restored via `dotnet restore`).
.PARAMETER Version
    The version to deploy (semver, e.g. 1.2.0 or 1.2.0-beta.1). Prompts if omitted.
.PARAMETER SkipBuild
    Skip the dotnet build step (re-sign / re-package existing Release binaries).
.EXAMPLE
    PS> .\deploy.ps1
    Prompts for version, then runs the full pipeline.
.EXAMPLE
    PS> .\deploy.ps1 -Version 1.2.0-beta.1
#>

param(
    [string]$Version,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

# --- Verify required env vars ---------------------------------------------------
foreach ($var in 'AZURE_CLIENT_ID', 'AZURE_CLIENT_SECRET', 'AZURE_TENANT_ID') {
    if (-not [Environment]::GetEnvironmentVariable($var)) {
        Write-Error "Missing required environment variable: $var"
        exit 1
    }
}

# --- Resolve version ------------------------------------------------------------
# Default to the current installer version (from OpenDefinery.iss) so the user can
# just press Enter to re-use it.
$defaultVersion = $null
$issForDefault = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) 'OpenDefinery.iss'
if (Test-Path $issForDefault) {
    if ([System.IO.File]::ReadAllText($issForDefault) -match '(?m)^#define\s+AppVersion\s+"([^"]+)"') {
        $defaultVersion = $matches[1]
    }
}

if (-not $Version -and -not $SkipBuild) {
    if ($defaultVersion) {
        $entered = Read-Host "Version to deploy [$defaultVersion]"
        $Version = if ([string]::IsNullOrWhiteSpace($entered)) { $defaultVersion } else { $entered.Trim() }
    } else {
        $Version = Read-Host "Version to deploy (e.g. 1.2.0-beta.1)"
    }
    if (-not $Version) {
        Write-Error "Version is required."
        exit 1
    }
}

if ($Version) {
    # Permissive semver: MAJOR.MINOR.PATCH plus optional -prerelease and +build
    if ($Version -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z.\-]+)?(\+[0-9A-Za-z.\-]+)?$') {
        Write-Error "Invalid version: '$Version'. Expected semver, e.g. 1.2.3 or 1.2.3-beta.4"
        exit 1
    }
}

if ($Version -and $SkipBuild) {
    Write-Warning "-Version was provided with -SkipBuild. Version files will be rewritten but binaries are NOT rebuilt -- make sure the existing binaries match $Version."
}

# --- Paths ----------------------------------------------------------------------
$scriptDir    = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot     = Resolve-Path (Join-Path $scriptDir '..')
$slnFile      = Join-Path $repoRoot 'OpenDefinery-RevitAddins.sln'
$issFile      = Join-Path $scriptDir 'OpenDefinery.iss'
$metadataJson = Join-Path $repoRoot 'signing-metadata.json'

# Locate ISCC.exe (Inno Setup). Override with ISCC_PATH; otherwise probe common locations.
$iscc = $env:ISCC_PATH
if (-not $iscc) {
    $iscc = @(
        "$env:ProgramFiles\Inno Setup 7\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $iscc) {
    Write-Error "Inno Setup (ISCC.exe) not found. Set ISCC_PATH, or install Inno Setup 6/7."
    exit 1
}

# --- Locate signtool.exe --------------------------------------------------------
$signTool = $env:SIGNTOOL_PATH
if (-not $signTool) {
    $kitsRoot = (Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows Kits\Installed Roots' `
                    -ErrorAction SilentlyContinue).KitsRoot10
    if ($kitsRoot) {
        $signTool = Get-ChildItem "$kitsRoot\bin" -Filter 'signtool.exe' -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -like '*x64*' } |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1 -ExpandProperty FullName
    }
}
if (-not $signTool) { $signTool = 'signtool.exe' }

# --- Locate Azure Trusted Signing DLIB ------------------------------------------
$nugetRoot = $env:NUGET_PACKAGES
if (-not $nugetRoot) { $nugetRoot = "$env:USERPROFILE\.nuget\packages" }
$dlib = Join-Path $nugetRoot 'microsoft.trusted.signing.client\1.0.60\bin\x64\Azure.CodeSigning.Dlib.dll'
if (-not (Test-Path $dlib)) {
    Write-Error "Azure.CodeSigning.Dlib.dll not found at: $dlib`nRun 'dotnet restore' to download it."
    exit 1
}

function Invoke-Sign {
    param([string[]]$Files)
    if ($Files.Count -eq 0) { return }
    & $signTool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 `
        /dlib $dlib /dmdf $metadataJson @Files
    if ($LASTEXITCODE -ne 0) {
        Write-Error "signtool.exe failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
}

# --- Version-bump helpers -------------------------------------------------------
function Update-CsprojVersion {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Version
    )

    $content = [System.IO.File]::ReadAllText($Path)

    $oldVersion = $null
    if ($content -match '(?m)^\s*<Version>([^<]+)</Version>') {
        $oldVersion = $matches[1]
    }

    if ($oldVersion) {
        # Update existing element in place -- preserve indentation captured in $1.
        $newContent = $content -replace '(?m)^(\s*)<Version>[^<]*</Version>', `
            ('${1}<Version>' + $Version + '</Version>')
    } else {
        # Insert a fresh <Version> line after the first <AssemblyName>...</AssemblyName>.
        $newContent = $content -replace '(?m)^(\s*)(<AssemblyName>[^<]+</AssemblyName>)', `
            ('${1}${2}' + "`r`n" + '${1}<Version>' + $Version + '</Version>')
    }

    [System.IO.File]::WriteAllText($Path, $newContent)

    $name = [System.IO.Path]::GetFileName($Path)
    if ($oldVersion) {
        Write-Host "  $name : $oldVersion -> $Version" -ForegroundColor Green
    } else {
        Write-Host "  $name : (added <Version>) -> $Version" -ForegroundColor Green
    }
}

function Update-IssVersion {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Version
    )

    $content = [System.IO.File]::ReadAllText($Path)

    $oldVersion = $null
    if ($content -match '(?m)^#define\s+AppVersion\s+"([^"]+)"') {
        $oldVersion = $matches[1]
    }

    $newContent = $content -replace '(?m)^(#define\s+AppVersion\s+")[^"]+(")', `
        ('${1}' + $Version + '${2}')

    [System.IO.File]::WriteAllText($Path, $newContent)

    $name = [System.IO.Path]::GetFileName($Path)
    if ($oldVersion) {
        Write-Host "  $name : $oldVersion -> $Version" -ForegroundColor Green
    } else {
        Write-Warning "  $name : no #define AppVersion found -- file unchanged."
    }
}

# --- Step 1: Stamp the version into csprojs + ISS -------------------------------
if ($Version) {
    Write-Host "`nStamping version $Version..." -ForegroundColor Cyan

    $csprojs = @(
        'OpenDefinery\OpenDefinery.csproj',
        'OpenDefinery.Theme\OpenDefinery.Theme.csproj',
        'OD-ParamManager\OD-ParamManager.csproj',
        'OD-FamEditor\OD-FamEditor.csproj'
    )
    foreach ($rel in $csprojs) {
        $full = Join-Path $repoRoot $rel
        if (-not (Test-Path $full)) {
            Write-Error "csproj not found: $rel"
            exit 1
        }
        Update-CsprojVersion -Path $full -Version $Version
    }

    Update-IssVersion -Path $issFile -Version $Version
}

# --- Step 2: Build solution -----------------------------------------------------
# DeployToRevit=false so the build doesn't also copy into local Revit Addins folders.
if (-not $SkipBuild) {
    Write-Host "`nBuilding Release configuration (net48 + net8.0-windows + net10.0-windows)..." -ForegroundColor Cyan
    & dotnet build $slnFile -c Release -p:DeployToRevit=false
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
} else {
    Write-Host "`nSkipping build (using existing Release binaries)..." -ForegroundColor Yellow
}

# --- Step 3: Sign our own assemblies in the Release output ----------------------
# The installer packages these, so they must be signed before ISCC runs. Only our
# assemblies are signed; third-party DLLs keep their original signatures.
$tfms     = @('net48', 'net8.0-windows', 'net10.0-windows')
$ourDlls  = @(
    @{ Proj = 'OpenDefinery';        Dll = 'OpenDefinery.dll' },
    @{ Proj = 'OpenDefinery.Theme';  Dll = 'OpenDefinery.Theme.dll' },
    @{ Proj = 'OD-ParamManager';     Dll = 'OpenDefinery-ParamManager.dll' },
    @{ Proj = 'OD-FamEditor';        Dll = 'OpenDefinery-FamEditor.dll' }
)

$dllsToSign = @()
foreach ($item in $ourDlls) {
    foreach ($tfm in $tfms) {
        $full = Join-Path $repoRoot ("{0}\bin\Release\{1}\{2}" -f $item.Proj, $tfm, $item.Dll)
        if (-not (Test-Path $full)) {
            Write-Error "Missing build artifact: $full`nBuild Release first (or drop -SkipBuild)."
            exit 1
        }
        $dllsToSign += $full
    }
}

Write-Host "`nSigning $($dllsToSign.Count) assembly file(s) in Release output..." -ForegroundColor Cyan
$dllsToSign | ForEach-Object { Write-Host "  $_" }
Invoke-Sign -Files $dllsToSign

# --- Step 4: Compile installer --------------------------------------------------
Write-Host "`nCompiling installer..." -ForegroundColor Cyan
& $iscc $issFile
if ($LASTEXITCODE -ne 0) {
    Write-Error "ISCC.exe failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# --- Step 5: Sign the installer EXE --------------------------------------------
# Brief pause so Windows Defender finishes scanning the new EXE before we sign it.
Start-Sleep -Seconds 3

$distDir = Join-Path $repoRoot 'dist'
if (-not (Test-Path $distDir)) {
    Write-Error "dist\ folder not found. Check OutputDir in OpenDefinery.iss."
    exit 1
}

$exeFile = Get-ChildItem $distDir -Filter '*.exe' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $exeFile) {
    Write-Error "No .exe found in $distDir"
    exit 1
}

Write-Host "`nSigning installer EXE: $exeFile" -ForegroundColor Cyan
Invoke-Sign -Files @($exeFile)

if ($Version) {
    Write-Host "`nDone. Signed installer for version ${Version}: $exeFile" -ForegroundColor Green
} else {
    Write-Host "`nDone. Signed installer: $exeFile" -ForegroundColor Green
}
