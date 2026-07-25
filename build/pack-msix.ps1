<#
.SYNOPSIS
    Builds the Microsoft Store MSIX bundle for Pix2d without a Windows Application
    Packaging Project (.wapproj).

.DESCRIPTION
    Replaces the msbuild/.wapproj packaging path with plain `dotnet publish` + the
    Windows App Development CLI (`winapp package`, https://aka.ms/winappcli):

        publish win-x64   -> staging/x64   (self-contained, single-file)
        publish win-arm64 -> staging/arm64
        winapp package staging/x64 staging/arm64 -> Pix2d_<ver>_x64_arm64.msixbundle

    The package payload is deliberately kept identical in shape to what the .wapproj
    produced (self-contained single-file exe + native side-by-side libs), with two
    fixes: debug symbols (*.pdb) and linker imports (*.lib) are stripped, which the
    .wapproj shipped inside the Store package.

    Store submissions do not need a signature (Microsoft re-signs). Pass -CertPath to
    sign for local sideload testing.

.EXAMPLE
    # Store bundle, version taken from Directory.Build.props
    ./build/pack-msix.ps1

.EXAMPLE
    # Single-arch, signed with a dev cert, for local install testing
    ./build/pack-msix.ps1 -Architectures x64 -GenerateCert
#>
[CmdletBinding()]
param(
    # 1-4 part app version. Defaults to Pix2dVersion from Sources/Directory.Build.props.
    # Normalized to the 4-part Identity/@Version the Store requires (revision must be 0).
    [string] $Version,

    [string] $Configuration = 'Release',

    [ValidateSet('x64', 'arm64')]
    [string[]] $Architectures = @('x64', 'arm64'),

    [string] $OutputDir,
    [string] $StagingDir,

    # Sign the output (sideload testing only — omit for Store submissions).
    [string] $CertPath,
    [string] $CertPassword = 'password',
    [switch] $GenerateCert,

    # Reuse an existing staging layout instead of re-publishing.
    [switch] $SkipPublish,

    # Keep *.pdb / *.lib in the package (adds ~100 MB; the .wapproj used to do this).
    [switch] $IncludeSymbols,

    # The .wapproj rewrote every TargetDeviceFamily in the manifest from its own
    # TargetPlatformMinVersion / TargetPlatformVersion. The checked-in manifest still
    # carries the pre-rewrite placeholders (MaxVersionTested="10.0.0.0"), which the
    # Store rejects, so the same rewrite is reproduced here.
    [string] $TargetPlatformMinVersion = '10.0.17763.0',
    [string] $MaxVersionTested = '10.0.26100.0'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# winapp CLI phones home unless opted out; keep builds quiet and offline-friendly.
$env:WINAPP_CLI_TELEMETRY_OPTOUT = '1'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$desktopProj = Join-Path $repoRoot 'Sources\Heads\Pix2d.Desktop\Pix2d.Desktop.csproj'
$propsFile = Join-Path $repoRoot 'Sources\Directory.Build.props'

# Manifest + Store assets still live in the .wapproj folder so that project keeps
# working as a fallback. They are plain files — nothing here invokes msbuild.
$packagingDir = Join-Path $repoRoot 'Sources\Heads\Pix2d.Desktop.Wap'
$manifestSource = Join-Path $packagingDir 'Package.appxmanifest'
$imagesSource = Join-Path $packagingDir 'Images'

if (-not $OutputDir) { $OutputDir = Join-Path $repoRoot 'artifacts\msix' }
if (-not $StagingDir) { $StagingDir = Join-Path $repoRoot 'artifacts\msix-staging' }

foreach ($required in @($desktopProj, $propsFile, $manifestSource, $imagesSource)) {
    if (-not (Test-Path $required)) { throw "Required path not found: $required" }
}

if (-not (Get-Command winapp -ErrorAction SilentlyContinue)) {
    throw "winapp CLI not found. Install it with: winget install --id Microsoft.WinAppCli --source winget"
}

# ---------------------------------------------------------------- version handling

if (-not $Version) {
    [xml]$props = Get-Content $propsFile
    $Version = ($props.Project.PropertyGroup.Pix2dVersion | Where-Object { $_ }) -as [string]
    if (-not $Version) { throw "Could not read Pix2dVersion from $propsFile" }
    Write-Host "Version not supplied; using Pix2dVersion = $Version" -ForegroundColor DarkGray
}

$parts = @(0, 0, 0, 0)
$given = ([string]$Version).Trim().Split('.')
for ($i = 0; $i -lt [Math]::Min($given.Count, 4); $i++) {
    $parsed = 0
    if (-not [int]::TryParse($given[$i], [ref]$parsed)) { throw "Invalid version '$Version'" }
    $parts[$i] = $parsed
}
if ($parts[3] -ne 0) {
    # Partner Center rejects packages whose Identity revision is non-zero.
    Write-Warning "Identity revision must be 0 for Store submissions; forcing $($parts[3]) -> 0"
    $parts[3] = 0
}
$packageVersion = $parts -join '.'                       # 3.11.2.0  -> Identity/@Version
$assemblyVersion = ($parts[0..2]) -join '.'              # 3.11.2    -> -p:Version

Write-Host ""
Write-Host "Pix2d MSIX packaging" -ForegroundColor Cyan
Write-Host "  package version : $packageVersion"
Write-Host "  assembly version: $assemblyVersion"
Write-Host "  configuration   : $Configuration"
Write-Host "  architectures   : $($Architectures -join ', ')"
Write-Host "  staging         : $StagingDir"
Write-Host "  output          : $OutputDir"
Write-Host ""

# ------------------------------------------------------------------ manifest patch

function New-PackageManifest {
    param([string] $Destination)

    [xml]$manifest = New-Object System.Xml.XmlDocument
    $manifest.PreserveWhitespace = $true
    $manifest.Load($manifestSource)

    $identity = $manifest.SelectSingleNode("//*[local-name()='Package']/*[local-name()='Identity']")
    if (-not $identity) { throw "No <Identity> element in $manifestSource" }
    $identity.SetAttribute('Version', $packageVersion)
    # ProcessorArchitecture is intentionally not set: winapp stamps it per bundle slice
    # from each input folder's detected architecture. Verify-Bundle asserts it landed.

    # The .wapproj resolved these MSBuild tokens for us; do it explicitly instead.
    $app = $manifest.SelectSingleNode("//*[local-name()='Applications']/*[local-name()='Application']")
    if (-not $app) { throw "No <Application> element in $manifestSource" }
    $app.SetAttribute('Executable', 'Pix2d.exe')
    $app.SetAttribute('EntryPoint', 'Windows.FullTrustApplication')

    # Same rewrite the .wapproj applied from TargetPlatformMinVersion/TargetPlatformVersion.
    $families = $manifest.SelectNodes("//*[local-name()='Dependencies']/*[local-name()='TargetDeviceFamily']")
    foreach ($family in $families) {
        $family.SetAttribute('MinVersion', $TargetPlatformMinVersion)
        $family.SetAttribute('MaxVersionTested', $MaxVersionTested)
    }

    $writer = New-Object System.Xml.XmlTextWriter($Destination, (New-Object System.Text.UTF8Encoding($false)))
    try {
        $writer.Formatting = [System.Xml.Formatting]::Indented
        $manifest.Save($writer)
    }
    finally { $writer.Close() }
}

# ----------------------------------------------------------------- publish + stage

$stagePaths = @()

foreach ($arch in $Architectures) {
    $rid = "win-$arch"
    $stage = Join-Path $StagingDir $arch
    $stagePaths += $stage

    if (-not $SkipPublish) {
        if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
        New-Item -ItemType Directory -Path $stage -Force | Out-Null

        Write-Host "==> dotnet publish $rid" -ForegroundColor Yellow
        # --self-contained + PublishSingleFile match what the .wapproj's internal
        # "msixpublish" step produced, so the Store payload does not change shape.
        # (Sources/Heads/Pix2d.Desktop/Pix2d.Desktop.csproj sets SelfContained=false
        # for the portable/zip builds, so it must be overridden here.)
        $publishArgs = @(
            'publish', $desktopProj,
            '-c', $Configuration,
            '-r', $rid,
            '--self-contained', 'true',
            '-p:PublishSingleFile=true',
            "-p:Version=$assemblyVersion",
            '-p:WarningLevel=0',
            '-o', $stage
        )
        if ($env:SENTRY_DSN) { $publishArgs += "-p:SentryDsn=$($env:SENTRY_DSN)" }

        & dotnet @publishArgs
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $rid (exit $LASTEXITCODE)" }
    }
    elseif (-not (Test-Path $stage)) {
        throw "-SkipPublish was passed but $stage does not exist"
    }

    if (-not $IncludeSymbols) {
        # The .wapproj shipped ~100 MB of .pdb (libSkiaSharp.pdb alone is 80 MB) plus
        # unused import libraries inside the Store package. Drop them.
        $junk = Get-ChildItem $stage -Recurse -File -Include '*.pdb', '*.lib'
        if ($junk) {
            $freed = [math]::Round(($junk | Measure-Object Length -Sum).Sum / 1MB, 1)
            $junk | Remove-Item -Force
            Write-Host "    stripped $($junk.Count) symbol/import files ($freed MB)" -ForegroundColor DarkGray
        }
    }

    Copy-Item $imagesSource (Join-Path $stage 'Images') -Recurse -Force

    # A manifest left inside the input folder would be packed as an ordinary payload
    # file on top of being used as the manifest, so it lives one level up instead.
    Get-ChildItem $stage -File -Filter 'Package.appxmanifest' | Remove-Item -Force

    $payload = Get-ChildItem $stage -Recurse -File
    $size = [math]::Round(($payload | Measure-Object Length -Sum).Sum / 1MB, 1)
    Write-Host "    staged $($payload.Count) files, $size MB -> $stage" -ForegroundColor DarkGray
}

# -------------------------------------------------------------------------- package

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$isBundle = $stagePaths.Count -gt 1
$extension = if ($isBundle) { 'msixbundle' } else { 'msix' }
$outputName = "Pix2d_${packageVersion}_$($Architectures -join '_').$extension"
$outputPath = Join-Path $OutputDir $outputName
if (Test-Path $outputPath) { Remove-Item $outputPath -Force }

$manifestPath = Join-Path $StagingDir 'Package.appxmanifest'
New-PackageManifest -Destination $manifestPath

$packArgs = @('package') + $stagePaths + @('--manifest', $manifestPath, '--output', $outputPath, '--verbose')
if ($GenerateCert) { $packArgs += '--generate-cert' }
if ($CertPath) { $packArgs += @('--cert', $CertPath, '--cert-password', $CertPassword) }

Write-Host ""
Write-Host "==> winapp $($packArgs -join ' ')" -ForegroundColor Yellow
& winapp @packArgs
if ($LASTEXITCODE -ne 0) { throw "winapp package failed (exit $LASTEXITCODE)" }
if (-not (Test-Path $outputPath)) { throw "winapp package reported success but $outputPath is missing" }

# --------------------------------------------------------------------- verification

# Unpack what was actually produced and assert the properties Partner Center checks,
# rather than trusting the packer's exit code.
function Test-Package {
    param([string] $Path, [bool] $Bundle)

    $makeappx = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin\*\x64\makeappx.exe' -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $makeappx) {
        Write-Warning "makeappx.exe not found; skipping package verification"
        return
    }

    $work = Join-Path ([IO.Path]::GetTempPath()) ("pix2d-verify-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
    New-Item -ItemType Directory -Path $work -Force | Out-Null

    try {
        $slices = @()
        if ($Bundle) {
            & $makeappx.FullName unbundle /p $Path /d $work /o *>$null
            if ($LASTEXITCODE -ne 0) { throw "makeappx unbundle failed for $Path" }
            $slices = Get-ChildItem $work -Filter '*.msix' -File
        }
        else {
            $slices = @(Get-Item $Path)
        }
        if (-not $slices) { throw "No package slices found inside $Path" }

        $problems = @()
        $seenArchitectures = @()

        foreach ($slice in $slices) {
            $sliceDir = Join-Path $work ($slice.BaseName + '-x')
            & $makeappx.FullName unpack /p $slice.FullName /d $sliceDir /o *>$null
            if ($LASTEXITCODE -ne 0) { throw "makeappx unpack failed for $($slice.Name)" }

            [xml]$m = Get-Content (Join-Path $sliceDir 'AppxManifest.xml')
            $identity = $m.SelectSingleNode("//*[local-name()='Identity']")
            $app = $m.SelectSingleNode("//*[local-name()='Application']")
            $arch = $identity.GetAttribute('ProcessorArchitecture')
            $seenArchitectures += $arch

            Write-Host ("  {0,-8} version={1} exe={2}" -f $arch, $identity.GetAttribute('Version'), $app.GetAttribute('Executable'))

            if ($identity.GetAttribute('Version') -ne $packageVersion) {
                $problems += "$($slice.Name): Identity version is '$($identity.GetAttribute('Version'))', expected '$packageVersion'"
            }
            if (-not $arch) {
                $problems += "$($slice.Name): Identity/@ProcessorArchitecture is empty"
            }
            if ($app.GetAttribute('EntryPoint') -ne 'Windows.FullTrustApplication') {
                $problems += "$($slice.Name): unexpected EntryPoint '$($app.GetAttribute('EntryPoint'))'"
            }

            $exe = $app.GetAttribute('Executable')
            if (-not (Test-Path (Join-Path $sliceDir $exe))) {
                $problems += "$($slice.Name): manifest points at '$exe' but it is not in the payload"
            }
            if (-not (Test-Path (Join-Path $sliceDir 'resources.pri'))) {
                $problems += "$($slice.Name): resources.pri missing (scale-qualified assets would not resolve)"
            }

            # x-generate is a build-time token; it must not survive into the package.
            $rawManifest = Get-Content (Join-Path $sliceDir 'AppxManifest.xml') -Raw
            if ($rawManifest -match 'x-generate' -or $rawManifest -match '\$target') {
                $problems += "$($slice.Name): unresolved manifest placeholder (x-generate / `$target...)"
            }

            foreach ($family in $m.SelectNodes("//*[local-name()='TargetDeviceFamily']")) {
                $min = [Version]$family.GetAttribute('MinVersion')
                if ($min -lt [Version]'10.0.17763.0') {
                    $problems += "$($slice.Name): $($family.GetAttribute('Name')) MinVersion $min is below 10.0.17763.0 (.msix extension requires 1809+)"
                }
            }
        }

        foreach ($expected in $Architectures) {
            if ($seenArchitectures -notcontains $expected) {
                $problems += "bundle is missing the $expected slice (found: $($seenArchitectures -join ', '))"
            }
        }

        if ($problems) {
            Write-Host ""
            foreach ($p in $problems) { Write-Host "  FAIL $p" -ForegroundColor Red }
            throw "Package verification failed with $($problems.Count) problem(s)"
        }
        Write-Host "  all checks passed" -ForegroundColor Green
    }
    finally {
        Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host ""
Write-Host "==> verifying package" -ForegroundColor Yellow
Test-Package -Path $outputPath -Bundle $isBundle

$outputSize = [math]::Round((Get-Item $outputPath).Length / 1MB, 1)
Write-Host ""
Write-Host "Done: $outputPath ($outputSize MB)" -ForegroundColor Green
if (-not $CertPath -and -not $GenerateCert) {
    Write-Host "Unsigned — this is what Partner Center expects (Microsoft signs on submission)." -ForegroundColor DarkGray
}
