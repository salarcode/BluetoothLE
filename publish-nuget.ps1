<#
.SYNOPSIS
    Build, pack, and publish Salar.BluetoothLE and Salar.BluetoothLE.Maui to NuGet.

.DESCRIPTION
    This script:
      1. Restores and builds both library projects in Release configuration.
      2. Runs the test suite (skippable with -SkipTests).
      3. Packs both projects into .nupkg files.
      4. Pushes the packages to nuget.org.

    The NuGet API key is never required on the command line.
    Resolution order:
      a) -ApiKey parameter (useful for non-interactive automation)
      b) NUGET_API_KEY environment variable (recommended for CI/CD)
      c) Interactive prompt (masked input — key is not echoed or stored)

.PARAMETER Version
    Override the package version (e.g. 1.2.3). When omitted the version
    declared in the .csproj files is used.

.PARAMETER ApiKey
    NuGet API key. Prefer setting the NUGET_API_KEY environment variable
    instead of passing this parameter, so the key is never visible in your
    shell history.

.PARAMETER OutputDir
    Directory where .nupkg files are written. Defaults to 'artifacts/nuget'
    under the repository root.

.PARAMETER SkipTests
    Skip the test run.

.PARAMETER SkipPush
    Build and pack only; do not push to nuget.org. Useful for a dry run.

.EXAMPLE
    # Interactive — prompts for the API key
    .\publish-nuget.ps1

.EXAMPLE
    # CI/CD — key comes from an environment variable
    $env:NUGET_API_KEY = '<secret>'
    .\publish-nuget.ps1 -Version 1.2.3

.EXAMPLE
    # Dry run: build and pack without pushing
    .\publish-nuget.ps1 -SkipPush
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $Version   = '',
    [string] $ApiKey    = '',
    [string] $OutputDir = '',
    [switch] $SkipTests,
    [switch] $SkipPush
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Paths
# ---------------------------------------------------------------------------
$repoRoot = $PSScriptRoot
$coreProj = Join-Path $repoRoot 'src/Salar.BluetoothLE/Salar.BluetoothLE.csproj'
$mauiProj = Join-Path $repoRoot 'src/Salar.BluetoothLE.Maui/Salar.BluetoothLE.Maui.csproj'

if (-not $OutputDir) {
    $OutputDir = Join-Path $repoRoot 'artifacts/nuget'
}

# ---------------------------------------------------------------------------
# Helper
# ---------------------------------------------------------------------------
function Invoke-Cmd {
    param([string]$Description, [scriptblock]$Cmd)
    Write-Host ""
    Write-Host "==> $Description" -ForegroundColor Cyan
    & $Cmd
    if ($LASTEXITCODE -ne 0) {
        Write-Error "$Description failed with exit code $LASTEXITCODE."
    }
}

# ---------------------------------------------------------------------------
# Version override (optional)
# ---------------------------------------------------------------------------
$versionArgs = @()
if ($Version) {
    $versionArgs = @("/p:Version=$Version")
    Write-Host "Using version override: $Version" -ForegroundColor Yellow
}

# ---------------------------------------------------------------------------
# Clean output directory
# ---------------------------------------------------------------------------
if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# ---------------------------------------------------------------------------
# Restore
# ---------------------------------------------------------------------------
Invoke-Cmd 'Restore packages' {
    dotnet restore $coreProj
}
Invoke-Cmd 'Restore packages (Maui)' {
    dotnet restore $mauiProj
}

# ---------------------------------------------------------------------------
# Build
# ---------------------------------------------------------------------------
Invoke-Cmd 'Build Salar.BluetoothLE (Release)' {
    dotnet build $coreProj --configuration Release --no-restore @versionArgs
}
Invoke-Cmd 'Build Salar.BluetoothLE.Maui (Release)' {
    dotnet build $mauiProj --configuration Release --no-restore @versionArgs
}

# ---------------------------------------------------------------------------
# Test
# ---------------------------------------------------------------------------
if (-not $SkipTests) {
    $testProj = Join-Path $repoRoot 'tests/Salar.BluetoothLE.Tests/Salar.BluetoothLE.Tests.csproj'
    if (Test-Path $testProj) {
        Invoke-Cmd 'Run tests (net10.0-windows)' {
            dotnet test $testProj --configuration Release --framework net10.0-windows10.0.19041.0
        }
    }
    else {
        Write-Host "No test project found at '$testProj' — skipping tests." -ForegroundColor Yellow
    }
}

# ---------------------------------------------------------------------------
# Pack
# ---------------------------------------------------------------------------
Invoke-Cmd 'Pack Salar.BluetoothLE' {
    dotnet pack $coreProj --configuration Release --no-build --output $OutputDir @versionArgs
}
Invoke-Cmd 'Pack Salar.BluetoothLE.Maui' {
    dotnet pack $mauiProj --configuration Release --no-build --output $OutputDir @versionArgs
}

Write-Host ""
Write-Host "Packages written to: $OutputDir" -ForegroundColor Green
Get-ChildItem -Path $OutputDir -Filter '*.nupkg' | ForEach-Object {
    Write-Host "  $($_.Name)" -ForegroundColor Green
}

if ($SkipPush) {
    Write-Host ""
    Write-Host "-SkipPush specified — packages were not published." -ForegroundColor Yellow
    return
}

# ---------------------------------------------------------------------------
# Resolve API key — never required on the command line
# ---------------------------------------------------------------------------
if (-not $ApiKey) {
    if ($env:NUGET_API_KEY) {
        $ApiKey = $env:NUGET_API_KEY
        Write-Host ""
        Write-Host "Using API key from NUGET_API_KEY environment variable." -ForegroundColor DarkGray
    }
    else {
        Write-Host ""
        # Read-Host masks the input so the key is not visible or logged.
        $secureKey = Read-Host 'Enter your nuget.org API key' -AsSecureString
        if ($secureKey.Length -eq 0) {
            Write-Error 'No API key provided. Aborting push.'
        }
        $ApiKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR(
            [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
        )
    }
}

# ---------------------------------------------------------------------------
# Push
# ---------------------------------------------------------------------------
$nugetSource = 'https://api.nuget.org/v3/index.json'
$packages    = Get-ChildItem -Path $OutputDir -Filter '*.nupkg'

if (-not $packages) {
    Write-Error "No .nupkg files found in '$OutputDir'."
}

foreach ($pkg in $packages) {
    Invoke-Cmd "Push $($pkg.Name)" {
        dotnet nuget push $pkg.FullName `
            --api-key $ApiKey `
            --source $nugetSource `
            --skip-duplicate
    }
}

Write-Host ""
Write-Host "All packages published successfully." -ForegroundColor Green
