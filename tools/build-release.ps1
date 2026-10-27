<#
.SYNOPSIS
  Builds a CA-VAL-AIM release: the installer, the portable zip and their SHA-256 sums.

.DESCRIPTION
  Output (in -OutDir, default dist\ in the repo root):
    ValTraining-<version>-Setup.exe      Inno Setup installer (all users, one UAC prompt, Program Files)
    ValTraining-<version>-Portable.zip   the portable folder (CA-VAL-AIM.exe + data folder + maps) with LICENSE.txt, THIRD-PARTY-NOTICES.txt and README.txt
    SHA256SUMS.txt                      "<sha256>  <file>" lines (sha256sum -c compatible)

  Stages (-Stage). Without -Stage the script runs all three in one go, as it always did:
    app        1. Version from ValTrainerGodot\project.godot (tools\version.ps1).
               2. Copies the Godot project to a private work folder - the tracked tree is never modified - adds the
                  repo's CHANGELOG.md (the in-game "What's new" reads res://CHANGELOG.md) and stamps the Windows
                  file/product version (x.y.z.0) into the copy's export presets (product name: CA-VAL-AIM).
               3. Imports assets, builds the C# project, exports the "Windows Installer" preset (folder: exe + pck +
                  .NET data folder) and the "Windows Desktop" preset (one self-contained exe).
               4. THIRD-PARTY-NOTICES.txt: CA-VAL-AIM (GPL-3.0), Godot and its third-party components (printed by the
                  exported game itself, so they match the shipped engine), the .NET runtime's license and notices,
                  CC0 asset credits.
               5. Optional local code signing of both CA-VAL-AIM.exe files (signtool, see below).
               Writes the app folder (-AppDir, default <OutDir>\app; in the one-shot build a temp folder):
                 portable\   CA-VAL-AIM.exe (self-contained), LICENSE.txt, THIRD-PARTY-NOTICES.txt
                 installer\  CA-VAL-AIM.exe, CA-VAL-AIM.pck, data_ValTrainer_windows_x86_64\, LICENSE.txt,
                             THIRD-PARTY-NOTICES.txt
    package    The portable zip and the installer (Inno Setup 6) from the app folder (-AppDir), which may have been
               signed in between (the release workflow signs both CA-VAL-AIM.exe files with SignPath). Checks that
               both exes carry this version.
    checksums  SHA256SUMS.txt over the final Setup.exe and Portable.zip in -OutDir (run it after Setup.exe is signed).

  Works locally and on CI (GitHub windows-latest): pass -Godot / -Iscc or set $env:GODOT. Needs the .NET 8 SDK and
  the Godot 4.7.2 .NET export templates (app stage) and Inno Setup 6 (package stage). Never commits anything; exits
  non-zero on any failure. Godot runs only while this script holds the machine-wide Godot slot (the named mutex
  "Global\ValTrainerGodotSlot", shared with tools\godot-run.ps1, gshot.ps1 and selftest.ps1): one Godot at a time.

  Pass -SkipPortable to ship the installer only: it skips the "Windows Desktop" single-exe export, the portable zip
  and its checksum line. SHA256SUMS.txt then covers just Setup.exe (what the in-app updater expects on releases that
  don't publish a portable zip).

  Local code signing (optional): set SIGNTOOL_CERT_THUMBPRINT (SHA-1 thumbprint of a code-signing certificate in the
  Windows certificate store) and optionally SIGNTOOL_PATH (signtool.exe) and SIGNTOOL_TIMESTAMP_URL. Then the app stage
  signs both CA-VAL-AIM.exe files and the package stage signs Setup.exe and the uninstaller. Without it, signing is
  skipped. (Release builds are signed by SignPath in .github\workflows\release.yml instead; see CONTRIBUTING.md.)

.PARAMETER Stage
  all (default), app, package or checksums. See the description.
.PARAMETER Godot
  Godot 4.7.2 .NET console exe. Default: $env:GODOT, else tools\godot\Godot_v4.7.2-stable_mono_win64\..._console.exe.
.PARAMETER OutDir
  Output folder (relative paths are relative to the repo root). Default: dist.
.PARAMETER AppDir
  App folder written by the app stage and read by the package stage (relative paths are relative to the repo root).
  Default: <OutDir>\app for -Stage app / package; a folder inside the work folder for the one-shot build.
.PARAMETER Iscc
  Inno Setup 6 compiler. Default: tools\innosetup\ISCC.exe, then Program Files, then PATH.
.PARAMETER SkipInstaller
  Only build the portable zip (no Inno Setup needed).
.PARAMETER WorkDir
  Private build folder. Default: $env:RUNNER_TEMP or %TEMP%, \ValTrainer-build. Deleted afterwards unless -KeepWork.
.PARAMETER KeepWork
  Keep the work folder (later builds then only re-copy changed files and skip most of the asset import).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\build-release.ps1
.EXAMPLE
  pwsh tools\build-release.ps1 -Godot C:\godot\Godot_v4.7.2-stable_mono_win64_console.exe -Iscc "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
.EXAMPLE
  tools\build-release.ps1 -Stage app          # dist\app\portable + dist\app\installer
  # ... sign dist\app\portable\CA-VAL-AIM.exe and dist\app\installer\CA-VAL-AIM.exe ...
  tools\build-release.ps1 -Stage package      # dist\ValTraining-<version>-Portable.zip + -Setup.exe from dist\app
  # ... sign dist\ValTraining-<version>-Setup.exe ...
  tools\build-release.ps1 -Stage checksums    # dist\SHA256SUMS.txt
#>
[CmdletBinding()]
param(
  [ValidateSet('all', 'app', 'package', 'checksums')][string]$Stage = 'all',
  [string]$Godot = "",
  [string]$OutDir = "dist",
  [string]$AppDir = "",
  [string]$Iscc = "",
  [switch]$SkipInstaller,
  [switch]$SkipPortable,
  # Escape hatch for local experiments only: every real release ships all 13 maps, and without them the build fails
  # instead of quietly producing an installer that falls back to the traced blockouts.
  [switch]$AllowNoMaps,
  [string]$WorkDir = "",
  [switch]$KeepWork
)

Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$Repo = Split-Path -Parent $PSScriptRoot
# Product name (exe version resource, installer, notices) and executable name since 1.18.0 (formerly ValTraining /
# ValTrainer.exe). The release files keep the ValTraining- prefix: 1.16.1-1.17.0 only download files with that prefix, so a
# CA-VAL-AIM-... file would break their automatic update (see UpdateAssets.ReleasePrefix in the app).
$ProductName = 'CA-VAL-AIM'
$ExeName = 'CA-VAL-AIM.exe'
$PckName = 'CA-VAL-AIM.pck'
$AssetPrefix = 'ValTraining'
$Proj = Join-Path $Repo 'ValTrainerGodot'
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$DoApp = $Stage -eq 'all' -or $Stage -eq 'app'
$DoPackage = $Stage -eq 'all' -or $Stage -eq 'package'
$DoChecksums = $Stage -eq 'all' -or $Stage -eq 'checksums'

function Step([string]$msg) { Write-Host ("[{0,5:0}s] {1}" -f $sw.Elapsed.TotalSeconds, $msg) -ForegroundColor Cyan }
function Fail([string]$msg) { throw $msg }

# ---------------------------------------------------------------- the map set
# Every release ships the 13 extracted maps. They live outside the project (dist-maps\maps: ~2.3 GB, git-ignored)
# because the repository does not carry the extracted Riot geometry; the installer copies that folder next to the
# executable, where MapAssets finds maps\<key>\collision.json and the GLBs. A build without them is not a smaller
# release, it is a broken one (every map falls back to the traced blockout), so the build stops unless -AllowNoMaps.
$MapKeys = @('abyss', 'ascent', 'bind', 'breeze', 'corrode', 'fracture', 'haven', 'icebox', 'lotus', 'pearl',
             'split', 'summit', 'sunset')

function Assert-MapSet([string]$dir, [string]$what) {
  if (-not (Test-Path -LiteralPath $dir)) { Fail "$what is missing: $dir" }
  $missing = @()
  foreach ($key in $MapKeys) {
    $mapDir = Join-Path $dir $key
    if (-not (Test-Path -LiteralPath (Join-Path $mapDir 'collision.json'))) { $missing += $key; continue }
    $glb = Get-ChildItem -LiteralPath $mapDir -Filter *.glb -File -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $glb) { $missing += "$key (no GLBs)" }
  }
  if ($missing.Count -gt 0) { Fail "$what is incomplete (missing: $($missing -join ', '))" }
  $bytes = (Get-ChildItem -LiteralPath $dir -Recurse -File | Measure-Object Length -Sum).Sum
  if ($bytes -lt 1GB) { Fail "$what is suspiciously small: $([math]::Round($bytes / 1MB)) MB for 13 maps" }
  Write-Host ("Maps OK: 13/13, {0:N0} MB in {1}" -f ($bytes / 1MB), $dir) -ForegroundColor Green
}

function Invoke-Native([string]$exe, [string[]]$arguments, [string]$what) {
  & $exe @arguments
  if ($LASTEXITCODE -ne 0) { Fail "$what failed (exit code $LASTEXITCODE)" }
}

function Write-Utf8([string]$path, [string]$text, [bool]$bom = $false) {
  [System.IO.File]::WriteAllText($path, $text, (New-Object System.Text.UTF8Encoding($bom)))
}

function Resolve-RepoPath([string]$path) {
  if ([System.IO.Path]::IsPathRooted($path)) { return $path }
  return (Join-Path $Repo $path)
}

# The machine-wide Godot slot: only one Godot process at a time on this PC (several at once froze it). Shared with
# tools\godot-run.ps1, gshot.ps1 and selftest.ps1. Also taken on CI, where nothing else competes for it.
function Enter-GodotSlot {
  $slot = New-Object System.Threading.Mutex($false, 'Global\ValTrainerGodotSlot')
  $waited = [System.Diagnostics.Stopwatch]::StartNew()
  try {
    if (-not $slot.WaitOne(0)) {
      Write-Host "Waiting for the Godot slot (another Godot run is using it)..."
      [void]$slot.WaitOne()
    }
  } catch [System.Threading.AbandonedMutexException] { }   # the previous holder died: the slot is ours
  if ($waited.Elapsed.TotalSeconds -gt 2) { Write-Host ("Waited {0:0} s for the Godot slot" -f $waited.Elapsed.TotalSeconds) }
  return $slot
}

function Exit-GodotSlot($slot) {
  if ($slot) {
    try { $slot.ReleaseMutex() } catch { }
    $slot.Dispose()
  }
}

# Multi-size .ico (16-256 px) from the 256 px app icon: BMP entries up to 48 px, PNG entries above (Vista+ format).
function New-Icon([string]$png, [string]$ico) {
  Add-Type -AssemblyName System.Drawing
  $src = [System.Drawing.Image]::FromFile($png)
  try {
    $images = New-Object System.Collections.Generic.List[byte[]]
    $sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
    foreach ($s in $sizes) {
      $bmp = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
      $g = [System.Drawing.Graphics]::FromImage($bmp)
      $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
      $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
      $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
      $g.DrawImage($src, 0, 0, $s, $s)
      $g.Dispose()
      $ms = New-Object System.IO.MemoryStream
      if ($s -ge 64) {
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
      } else {
        $bw = New-Object System.IO.BinaryWriter($ms)
        $maskRow = [int]([math]::Ceiling($s / 32.0) * 4)
        $bw.Write([int]40); $bw.Write([int]$s); $bw.Write([int]($s * 2)); $bw.Write([int16]1); $bw.Write([int16]32)
        $bw.Write([int]0); $bw.Write([int]($s * $s * 4 + $maskRow * $s)); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0)
        $rect = New-Object System.Drawing.Rectangle(0, 0, $s, $s)
        $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $row = New-Object byte[] ($s * 4)
        for ($y = $s - 1; $y -ge 0; $y--) {   # bottom-up BGRA rows
          [System.Runtime.InteropServices.Marshal]::Copy([IntPtr]($data.Scan0.ToInt64() + $y * $data.Stride), $row, 0, $s * 4)
          $bw.Write($row)
        }
        $bmp.UnlockBits($data)
        $bw.Write((New-Object byte[] ($maskRow * $s)))   # AND mask unused (alpha channel)
        $bw.Flush()
      }
      $images.Add($ms.ToArray())
      $bmp.Dispose()
    }
    $out = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter($out)
    $w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
      $s = $sizes[$i]; $dim = if ($s -ge 256) { 0 } else { $s }
      $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
      $w.Write([int16]1); $w.Write([int16]32); $w.Write([int]$images[$i].Length); $w.Write([int]$offset)
      $offset += $images[$i].Length
    }
    foreach ($img in $images) { $w.Write($img) }
    $w.Flush()
    [System.IO.File]::WriteAllBytes($ico, $out.ToArray())
  } finally { $src.Dispose() }
}

function Find-SignTool {
  if ($env:SIGNTOOL_PATH) { return $env:SIGNTOOL_PATH }
  $kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
  if (Test-Path $kits) {
    $st = Get-ChildItem $kits -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
      Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1
    if ($st) { return $st.FullName }
  }
  $cmd = Get-Command signtool.exe -ErrorAction SilentlyContinue
  if ($cmd) { return $cmd.Source }
  Fail "SIGNTOOL_CERT_THUMBPRINT is set but signtool.exe wasn't found (set SIGNTOOL_PATH)"
}

function Get-TimestampUrl { if ($env:SIGNTOOL_TIMESTAMP_URL) { $env:SIGNTOOL_TIMESTAMP_URL } else { 'http://timestamp.digicert.com' } }

# Both exes must carry this build's version and product name, also after signing elsewhere.
function Assert-ExeMetadata([string]$exe) {
  $vi = (Get-Item -LiteralPath $exe).VersionInfo
  if ($vi.FileVersion -ne $FileVersion) { Fail "$exe has file version '$($vi.FileVersion)', expected $FileVersion" }
  if ($vi.ProductVersion -ne $FileVersion) { Fail "$exe has product version '$($vi.ProductVersion)', expected $FileVersion" }
  if ($vi.ProductName -ne $ProductName) { Fail "$exe has product name '$($vi.ProductName)', expected $ProductName" }
}

function Get-SignatureText([string]$file) {
  $sig = Get-AuthenticodeSignature -LiteralPath $file
  if ($sig.Status -eq 'NotSigned') { return 'not signed' }
  $who = if ($sig.SignerCertificate) { $sig.SignerCertificate.GetNameInfo([System.Security.Cryptography.X509Certificates.X509NameType]::SimpleName, $false) } else { '?' }
  return "signed by $who ($($sig.Status))"
}

# ==================================================================== app stage
function Invoke-AppStage {
  $stage = Join-Path $work 'project'
  $outInst = Join-Path $AppDir 'installer'
  $outPort = Join-Path $AppDir 'portable'
  foreach ($d in @($outInst, $outPort, (Join-Path $work 'appdata'))) {
    if (Test-Path -LiteralPath $d) { Remove-Item -LiteralPath $d -Recurse -Force }
  }
  New-Item -ItemType Directory -Force $stage, $outInst, $outPort | Out-Null

  # ---------------------------------------------------------------- the map set (checked before anything expensive)
  $mapsSrc = Join-Path $Repo 'dist-maps\maps'
  if (Test-Path -LiteralPath $mapsSrc) {
    Assert-MapSet $mapsSrc 'the source map set (dist-maps\maps)'
  } elseif ($AllowNoMaps) {
    Write-Warning "Real maps: $mapsSrc not found (-AllowNoMaps): this build uses the traced blockouts."
  } else {
    Fail ("The real map set is missing: $mapsSrc. Every release ships all 13 maps, so the build stops here; " +
          "restore dist-maps\maps (see tools\mapgen\README.md) or pass -AllowNoMaps for a local experiment.")
  }

  # ---------------------------------------------------------------- private copy of the project
  Step "Copying the project to $stage"
  # .godot\imported is copied too (saves re-importing every asset); .godot\mono (build output) is rebuilt.
  & robocopy $Proj $stage /MIR /XD (Join-Path $Proj '.godot\mono') (Join-Path $stage '.godot\mono') /NFL /NDL /NJH /NJS /NP | Out-Null
  if ($LASTEXITCODE -ge 8) { Fail "robocopy failed (exit code $LASTEXITCODE)" }
  $global:LASTEXITCODE = 0

  $changelog = Join-Path $Repo 'CHANGELOG.md'
  $stageChangelog = Join-Path $stage 'CHANGELOG.md'
  if (Test-Path -LiteralPath $changelog) { Copy-Item -LiteralPath $changelog $stageChangelog -Force }
  else {
    Write-Warning "CHANGELOG.md not found in the repo root: the in-game ""What's new"" panel will be skipped"
    if (Test-Path -LiteralPath $stageChangelog) { Remove-Item -LiteralPath $stageChangelog -Force }
  }

  # Windows file/product version: Godot falls back to config/version when these are empty, but only for purely
  # numeric versions (1.1.0-beta.1 would become 1.0.0.0), so stamp x.y.z.0 into the private copy's presets.
  # The product name comes from the presets too; it must be $ProductName (SignPath checks it before signing).
  $presets = Join-Path $stage 'export_presets.cfg'
  $p = [System.IO.File]::ReadAllText($presets)
  $p = [regex]::Replace($p, '(?m)^application/(file_version|product_version)=".*"', ('application/$1="' + $FileVersion + '"'))
  $p = [regex]::Replace($p, '(?m)^application/product_name=".*"', ('application/product_name="' + $ProductName + '"'))
  Write-Utf8 $presets $p

  # ---------------------------------------------------------------- import, build, export (in the Godot slot)
  $instExe = Join-Path $outInst $ExeName
  $portExe = Join-Path $outPort $ExeName
  $dataDir = Join-Path $outInst 'data_ValTrainer_windows_x86_64'
  $engineNotices = Join-Path $work 'licenses-from-game.txt'
  $slot = Enter-GodotSlot
  try {
    $godotVer = (& $Godot --version 2>$null | Select-Object -Last 1)
    if ("$godotVer" -notmatch 'mono') { Fail "$Godot is not a Godot .NET (mono) build ('$godotVer')" }
    Write-Host "Godot $godotVer"

    Step "Importing assets"
    Invoke-Native $Godot @('--headless', '--path', $stage, '--import') 'Godot asset import'

    Step "Building C#"
    Invoke-Native 'dotnet' @('build', (Join-Path $stage 'ValTrainer.csproj'), '-c', 'Debug', '-nologo', '-v', 'q') 'dotnet build'

    Step "Exporting ""Windows Installer"" -> $outInst"
    Invoke-Native $Godot @('--headless', '--path', $stage, '--export-release', 'Windows Installer', $instExe) 'Godot export (Windows Installer)'
    foreach ($f in @($instExe, (Join-Path $outInst $PckName), $dataDir)) {
      if (-not (Test-Path -LiteralPath $f)) { Fail "export output missing: $f" }
    }

    if ($SkipPortable) {
      Step "Skipping the portable single-exe export (-SkipPortable)"
      Assert-ExeMetadata $instExe
    } else {
      Step "Exporting ""Windows Desktop"" (single exe) -> $outPort"
      Invoke-Native $Godot @('--headless', '--path', $stage, '--export-release', 'Windows Desktop', $portExe) 'Godot export (Windows Desktop)'
      if (-not (Test-Path -LiteralPath $portExe) -or (Get-Item -LiteralPath $portExe).Length -lt 50MB) { Fail "portable export missing or too small: $portExe" }
      foreach ($exe in @($instExe, $portExe)) { Assert-ExeMetadata $exe }
    }

    # ---------------------------------------------------------------- real map assets
    # Copied next to the executable, where MapAssets looks for maps/<key>/collision.json and the GLBs; the installer
    # ships its whole source folder. The portable folder only gets them when it is actually exported.
    if (Test-Path -LiteralPath $mapsSrc) {
      $staging = @($outInst)
      if (-not $SkipPortable) { $staging += $outPort }
      Step "Copying real maps -> $($staging.Count) export folder(s)"
      foreach ($dir in $staging) {
        $dst = Join-Path $dir 'maps'
        if (Test-Path -LiteralPath $dst) { Remove-Item -LiteralPath $dst -Recurse -Force }
        Copy-Item -LiteralPath $mapsSrc -Destination $dst -Recurse -Force
        Assert-MapSet $dst "the staged maps in $dst"
      }
    }

    # ---------------------------------------------------------------- notices
    Step "Writing THIRD-PARTY-NOTICES.txt"
    if (Test-Path -LiteralPath $engineNotices) { Remove-Item -LiteralPath $engineNotices -Force }
    # The exported game prints its own notices (Engine.GetLicenseText / GetCopyrightInfo / GetLicenseInfo).
    # --dev + --data-dir: never touches the real settings; --log-file keeps the user's Godot log folder clean.
    $argLine = "--headless --log-file `"$(Join-Path $work 'print-licenses.log')`" -- --dev --data-dir `"$(Join-Path $work 'appdata')`" --print-licenses `"$engineNotices`""
    $proc = Start-Process -FilePath $instExe -ArgumentList $argLine -Wait -PassThru -WindowStyle Hidden
    if ($proc.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $engineNotices) -or (Get-Item -LiteralPath $engineNotices).Length -lt 20KB) {
      Fail "the exported game couldn't write its license notices (exit code $($proc.ExitCode)); see $(Join-Path $work 'print-licenses.log')"
    }
  } finally { Exit-GodotSlot $slot }

  # .NET runtime notices from the runtime pack the export was published with.
  $deps = Get-ChildItem -LiteralPath $dataDir -Recurse -Filter 'ValTrainer.deps.json' | Select-Object -First 1
  if (-not $deps) { Fail "ValTrainer.deps.json not found in $dataDir" }
  $m = [regex]::Match([System.IO.File]::ReadAllText($deps.FullName), 'runtimepack\.Microsoft\.NETCore\.App\.Runtime\.win-x64/([0-9][^"]*)"')
  if (-not $m.Success) { Fail "couldn't find the .NET runtime pack version in $($deps.FullName)" }
  $netVer = $m.Groups[1].Value
  $nuget = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
  $pack = Join-Path $nuget "microsoft.netcore.app.runtime.win-x64\$netVer"
  $netNotices = Get-ChildItem -LiteralPath $pack -Filter 'THIRD-PARTY-NOTICES.TXT' -ErrorAction SilentlyContinue | Select-Object -First 1
  if (-not $netNotices) { Fail ".NET runtime notices not found in $pack" }

  $sep = '=' * 64
  $notices = New-Object System.Text.StringBuilder
  [void]$notices.Append("$ProductName $Version - third-party notices`r`n")
  [void]$notices.Append("Generated by tools\build-release.ps1. Also shown in the game: Settings > About > Licenses.`r`n`r`n")
  [void]$notices.Append("$ProductName (formerly ValTraining / ValTrainer) is free software under the GNU General Public License v3 (LICENSE.txt).`r`n")
  [void]$notices.Append("It is a modified version of ValTrainer (https://github.com/idoraz1/ValTrainer), copyright (C) 2026 ValTrainer`r`n")
  [void]$notices.Append("contributors, also under GPL-3.0-or-later. The original copyright notice is in NOTICE.txt.`r`n`r`n")
  [void]$notices.Append("It is built with the Godot Engine (MIT) and runs on the .NET runtime (MIT); their licenses and the notices of the`r`n")
  [void]$notices.Append("components they include follow, then the credits for the CC0 models, textures and sounds.`r`n`r`n")
  [void]$notices.Append([System.IO.File]::ReadAllText($engineNotices).TrimEnd())
  [void]$notices.Append("`r`n`r`n$sep`r`n.NET RUNTIME $netVer - THIRD-PARTY NOTICES`r`n$sep`r`n`r`n")
  [void]$notices.Append(([System.IO.File]::ReadAllText($netNotices.FullName) -replace "`r?`n", "`r`n").TrimEnd())
  [void]$notices.Append("`r`n`r`n$sep`r`nINSTALLER`r`n$sep`r`n`r`n")
  [void]$notices.Append("The Windows installer is made with Inno Setup (https://jrsoftware.org/isinfo.php).`r`n")
  [void]$notices.Append("Copyright (C) 1997-2026 Jordan Russell. Portions Copyright (C) 2000-2026 Martijn Laan.`r`n")
  $noticesText = $notices.ToString()
  $licenseText = [System.IO.File]::ReadAllText($license) -replace "`r?`n", "`r`n"
  $noticeText = [System.IO.File]::ReadAllText((Join-Path $Repo 'NOTICE')) -replace "`r?`n", "`r`n"

  foreach ($dir in @($outInst, $outPort)) {
    Write-Utf8 (Join-Path $dir 'THIRD-PARTY-NOTICES.txt') $noticesText $true
    Write-Utf8 (Join-Path $dir 'LICENSE.txt') $licenseText $true
    Write-Utf8 (Join-Path $dir 'NOTICE.txt') $noticeText $true
  }

  # ---------------------------------------------------------------- optional local signing
  if ($env:SIGNTOOL_CERT_THUMBPRINT) {
    $signtool = Find-SignTool
    $ts = Get-TimestampUrl
    Step "Signing $ExeName (installer and portable builds)"
    foreach ($exe in @($instExe, $portExe)) {
      Invoke-Native $signtool @('sign', '/sha1', $env:SIGNTOOL_CERT_THUMBPRINT, '/fd', 'sha256', '/tr', $ts, '/td', 'sha256', '/d', $ProductName, $exe) "signtool ($exe)"
    }
  } else {
    Write-Host "Code signing: skipped (SIGNTOOL_CERT_THUMBPRINT not set)"
  }
}

# ==================================================================== package stage
function Invoke-PackageStage {
  $outInst = Join-Path $AppDir 'installer'
  $outPort = Join-Path $AppDir 'portable'
  $portExe = Join-Path $outPort $ExeName
  $instExe = Join-Path $outInst $ExeName
  if ($SkipPortable -and $SkipInstaller) { Fail "-SkipPortable and -SkipInstaller together would package nothing" }
  $need = @()
  if (-not $SkipPortable) {
    $need += @($portExe, (Join-Path $outPort 'LICENSE.txt'), (Join-Path $outPort 'NOTICE.txt'),
      (Join-Path $outPort 'THIRD-PARTY-NOTICES.txt'),
      (Join-Path $outPort 'data_ValTrainer_windows_x86_64\ValTrainer.dll'))
  }
  if (-not $SkipInstaller) {
    $need += @($instExe, (Join-Path $outInst $PckName), (Join-Path $outInst 'data_ValTrainer_windows_x86_64'),
      (Join-Path $outInst 'data_ValTrainer_windows_x86_64\ValTrainer.dll'),
      (Join-Path $outInst 'LICENSE.txt'), (Join-Path $outInst 'NOTICE.txt'),
      (Join-Path $outInst 'THIRD-PARTY-NOTICES.txt'))
  }
  foreach ($f in $need) {
    if (-not (Test-Path -LiteralPath $f)) { Fail "app folder incomplete: $f is missing (run -Stage app first, or pass -AppDir)" }
  }
  $exes = @()
  if (-not $SkipPortable) { $exes += $portExe }
  if (-not $SkipInstaller) { $exes += $instExe }
  foreach ($exe in $exes) {
    Assert-ExeMetadata $exe
    Write-Host ("{0}: {1}" -f $exe, (Get-SignatureText $exe))
  }

  # ---------------------------------------------------------------- portable zip
  if ($SkipPortable) {
    Write-Host "Portable zip: skipped (-SkipPortable)"
  } else {
  $zipName = "$AssetPrefix-$Version-Portable.zip"
  $zipPath = Join-Path $OutDir $zipName
  Step "Packing $zipName"
  $zipDir = Join-Path $work 'zip'
  if (Test-Path -LiteralPath $zipDir) { Remove-Item -LiteralPath $zipDir -Recurse -Force }
  $zipRoot = Join-Path $zipDir "$ProductName-$Version"
  New-Item -ItemType Directory -Force $zipRoot | Out-Null
  Copy-Item -Path (Join-Path $outPort '*') -Destination $zipRoot -Recurse -Force
  $readme = @"
CA-VAL-AIM $Version - portable version
======================================

Free, fan-made VALORANT-style aim trainer. Not affiliated with or endorsed by Riot Games.

Run CA-VAL-AIM.exe - no installation needed. It reads your VALORANT settings (sensitivity, crosshair,
keybinds, display) from %LOCALAPPDATA%\VALORANT\Saved\Config and never changes them.

Files it creates:
  %APPDATA%\ValTrainer                              settings, stats and recordings
  %APPDATA%\Godot\app_userdata\ValTrainer           logs (logs\godot.log) and shader cache
  %LOCALAPPDATA%\data_ValTrainer_windows_x86_64     .NET runtime, unpacked on first launch
  %LOCALAPPDATA%\ValTrainer\updates                 automatic updates while they download (removed after updating)
To remove CA-VAL-AIM completely, delete this folder and those four.

Updates: the UPDATE button in the menu checks GitHub on demand (also when automatic checks are off), downloads the
new release, verifies it against SHA256SUMS.txt and replaces CA-VAL-AIM.exe in this folder when you restart it (the
old exe is kept as CA-VAL-AIM.exe.old until the next start). With automatic updates on, CA-VAL-AIM also checks at
startup and every six hours; turn that off in Settings > About.

Black screen or crash at startup? Start it with the OpenGL renderer:
  CA-VAL-AIM.exe --rendering-method gl_compatibility

The installer version ($AssetPrefix-$Version-Setup.exe) adds Start menu shortcuts and an uninstaller.
License: GNU GPL v3 (LICENSE.txt). Upstream copyright notice: NOTICE.txt. Third-party licenses: THIRD-PARTY-NOTICES.txt.
"@
  Write-Utf8 (Join-Path $zipRoot 'README.txt') ($readme -replace "`r?`n", "`r`n") $true
  if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  [System.IO.Compression.ZipFile]::CreateFromDirectory($zipRoot, $zipPath, [System.IO.Compression.CompressionLevel]::Optimal, $true)
  }

  # ---------------------------------------------------------------- installer
  $setupPath = Join-Path $OutDir "$AssetPrefix-$Version-Setup.exe"
  if (-not $SkipInstaller) {
    Step "Compiling $(Split-Path -Leaf $setupPath) (Inno Setup)"
    $signArgs = @()
    if ($env:SIGNTOOL_CERT_THUMBPRINT) {
      $signtool = Find-SignTool
      $ts = Get-TimestampUrl
      # Inno Setup signs Setup.exe and the uninstaller with this command ($q = quote, $f = file).
      $signArgs = @('/DSIGN', ('/Ssigntool=$q' + $signtool + '$q sign /sha1 ' + $env:SIGNTOOL_CERT_THUMBPRINT + ' /fd sha256 /tr ' + $ts + ' /td sha256 /d $q' + $ProductName + ' Setup$q $f'))
    }
    $ico = Join-Path $work 'ValTrainer.ico'
    New-Icon (Join-Path $Proj 'icon.png') $ico
    $repoMatch = [regex]::Match([System.IO.File]::ReadAllText((Join-Path $Proj 'src\Core\AppInfo.cs')), 'GitHubRepo\s*=\s*"([^"]+)"')
    $ghRepo = if ($repoMatch.Success) { $repoMatch.Groups[1].Value } else { 'Pyromesis/CA-VAL-AIM' }
    if (Test-Path -LiteralPath $setupPath) { Remove-Item -LiteralPath $setupPath -Force }
    $isccArgs = @('/Qp', "/DAppVersion=$Version", "/DAppFileVersion=$FileVersion", "/DSourceDir=$outInst", "/DIconFile=$ico",
      "/DOutputDir=$OutDir", "/DGitHubRepo=$ghRepo", "/DAssetPrefix=$AssetPrefix") + $(if ($AllowNoMaps) { @('/DAllowNoMaps') } else { @() }) + $signArgs + @((Join-Path $Repo 'installer\ValTrainer.iss'))
    Invoke-Native $Iscc $isccArgs 'Inno Setup'
    if (-not (Test-Path -LiteralPath $setupPath)) { Fail "installer not produced: $setupPath" }
  } else {
    Write-Host "Installer: skipped (-SkipInstaller)"
  }
}

# ==================================================================== checksums stage
function Invoke-ChecksumsStage {
  $files = @((Join-Path $OutDir "$AssetPrefix-$Version-Setup.exe"), (Join-Path $OutDir "$AssetPrefix-$Version-Portable.zip"))
  if ($Stage -eq 'checksums' -and -not (Test-Path -LiteralPath $files[0])) {
    Fail "$($files[0]) not found (run -Stage package first)"
  }
  Step "Writing SHA256SUMS.txt"
  $lines = foreach ($f in $files) {
    if (Test-Path -LiteralPath $f) { '{0}  {1}' -f (Get-FileHash -LiteralPath $f -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path -Leaf $f) }
  }
  Write-Utf8 (Join-Path $OutDir 'SHA256SUMS.txt') ((@($lines) -join "`n") + "`n")
}

$work = $null
try {
  # ---------------------------------------------------------------- version & tools
  $global:LASTEXITCODE = 0
  $Version = (& (Join-Path $PSScriptRoot 'version.ps1')) | Select-Object -Last 1
  if ($LASTEXITCODE -ne 0 -or -not $Version) { Fail "couldn't read the version (tools\version.ps1)" }
  $Version = $Version.Trim()
  $numeric = ($Version -split '[-+]')[0]
  $FileVersion = "$numeric.0"          # Windows needs 4 numeric parts; prerelease tags can't go in there
  Write-Host ""
  Write-Host "$ProductName $Version  (file version $FileVersion)  stage: $Stage" -ForegroundColor Green

  if ($DoApp) {
    if (-not $Godot) { $Godot = $env:GODOT }
    if (-not $Godot) { $Godot = Join-Path $Repo 'tools\godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe' }
    if (-not (Test-Path -LiteralPath $Godot)) { Fail "Godot not found: $Godot (pass -Godot or set `$env:GODOT)" }
  }

  if ($DoPackage -and -not $SkipInstaller) {
    if (-not $Iscc) {
      $candidates = @(
        (Join-Path $Repo 'tools\innosetup\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'))
      $Iscc = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
      if (-not $Iscc) { $c = Get-Command ISCC.exe -ErrorAction SilentlyContinue; if ($c) { $Iscc = $c.Source } }
    }
    if (-not $Iscc -or -not (Test-Path -LiteralPath $Iscc)) { Fail "Inno Setup 6 (ISCC.exe) not found: pass -Iscc, install Inno Setup 6, or use -SkipInstaller" }
    Write-Host "Inno Setup: $Iscc"
  }

  $license = Join-Path $Repo 'LICENSE'
  $notice = Join-Path $Repo 'NOTICE'
  if ($DoApp -and -not (Test-Path -LiteralPath $license)) { Fail "LICENSE not found in the repo root" }
  if ($DoApp -and -not (Test-Path -LiteralPath $notice)) { Fail "NOTICE not found in the repo root" }

  $OutDir = Resolve-RepoPath $OutDir
  New-Item -ItemType Directory -Force $OutDir | Out-Null
  $OutDir = (Resolve-Path -LiteralPath $OutDir).Path

  if (-not $WorkDir) {
    $tmp = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { [System.IO.Path]::GetTempPath() }
    $WorkDir = Join-Path $tmp 'ValTrainer-build'
  }
  $work = $WorkDir
  if ($DoApp -or $DoPackage) { New-Item -ItemType Directory -Force $work | Out-Null }

  # The app folder: inside the work folder for the one-shot build (dist\ gets only the release files, as before),
  # <OutDir>\app between separate stages.
  if (-not $AppDir) { $AppDir = if ($Stage -eq 'all') { Join-Path $work 'app' } else { Join-Path $OutDir 'app' } }
  $AppDir = Resolve-RepoPath $AppDir
  if ($DoApp -or $DoPackage) { Write-Host "App folder: $AppDir" }

  if ($DoApp) { Invoke-AppStage }
  if ($DoPackage) { Invoke-PackageStage }
  if ($DoChecksums) { Invoke-ChecksumsStage }

  Write-Host ""
  Write-Host ("$ProductName {0} ({1}) built in {2:0} s:" -f $Version, $Stage, $sw.Elapsed.TotalSeconds) -ForegroundColor Green
  $show = @()
  if ($Stage -eq 'app') {
    $show = @((Join-Path $AppDir ('portable\' + $ExeName)), (Join-Path $AppDir ('installer\' + $ExeName)))
  } else {
    $show = @((Join-Path $OutDir "$AssetPrefix-$Version-Setup.exe"), (Join-Path $OutDir "$AssetPrefix-$Version-Portable.zip"))
    if ($DoChecksums) { $show += (Join-Path $OutDir 'SHA256SUMS.txt') }
  }
  foreach ($f in $show) {
    if (Test-Path -LiteralPath $f) { Write-Host ("  {0,-48} {1,10:N1} MB" -f $f, ((Get-Item -LiteralPath $f).Length / 1MB)) }
  }
  $exitCode = 0
} catch {
  Write-Host ""
  Write-Host "build-release.ps1 FAILED: $($_.Exception.Message)" -ForegroundColor Red
  if ($_.InvocationInfo) { Write-Host $_.InvocationInfo.PositionMessage -ForegroundColor DarkGray }
  $exitCode = 1
} finally {
  if ($work -and -not $KeepWork -and (Test-Path -LiteralPath $work)) {
    if ($DoApp) {
      Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
    } elseif ($DoPackage) {
      # Only what this stage made: a project copy kept by "-Stage app -KeepWork" stays.
      foreach ($d in @((Join-Path $work 'zip'), (Join-Path $work 'ValTrainer.ico'))) {
        if (Test-Path -LiteralPath $d) { Remove-Item -LiteralPath $d -Recurse -Force -ErrorAction SilentlyContinue }
      }
      if (-not (Get-ChildItem -LiteralPath $work -Force -ErrorAction SilentlyContinue)) { Remove-Item -LiteralPath $work -Force -ErrorAction SilentlyContinue }
    }
  }
}
exit $exitCode
