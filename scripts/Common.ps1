# Shared helpers, dot-sourced by every script. Not meant to be run directly.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:RepoRoot     = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$script:SolutionDir  = Join-Path $RepoRoot 'solution'
$script:SolutionFile = Join-Path $SolutionDir 'Installer.sln'
$script:CliProject   = Join-Path $SolutionDir 'Installer.Cli/Installer.Cli.csproj'
$script:DistDir      = Join-Path $RepoRoot 'dist'

function Invoke-Step {
    param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][scriptblock]$Action)
    Write-Host ""
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE" }
}

# Every version field, from solution/version.json, the Jenkins BUILD_NUMBER and the git commit:
# Prefix 0.1.0, File 0.1.0.12, Full 0.1.0-b12 (or 0.1.0-local), Informational 0.1.0-b12+a1b2c3d.
function Get-InstallerVersion {
    param([string]$BuildNumber = $env:BUILD_NUMBER)
    $json = Get-Content (Join-Path $SolutionDir 'version.json') -Raw | ConvertFrom-Json
    $prefix = '{0}.{1}.{2}' -f $json.major, $json.minor, $json.patch
    $isCi = -not [string]::IsNullOrWhiteSpace($BuildNumber)
    $build = if ($isCi) { [int]$BuildNumber } else { 0 }
    if ($build -lt 0 -or $build -gt 65535) { throw "Build number $build is outside 0..65535." }
    $suffix = if ($isCi) { "b$build" } else { 'local' }
    $sha = ''
    try { $sha = (& git -C $RepoRoot rev-parse --short=7 HEAD 2>$null) } catch { }
    if ($LASTEXITCODE -ne 0 -or -not $sha) { $sha = 'nogit' }
    $global:LASTEXITCODE = 0
    [PSCustomObject]@{
        Prefix        = $prefix
        Suffix        = $suffix
        Full          = "$prefix-$suffix"
        File          = "$prefix.$build"
        Informational = "$prefix-$suffix+$sha"
        IsCi          = $isCi
    }
}

function Get-VersionArgs {
    param([Parameter(Mandatory)]$Version)
    @(
        "-p:VersionPrefix=$($Version.Prefix)"
        "-p:VersionSuffix=$($Version.Suffix)"
        "-p:FileVersion=$($Version.File)"
        "-p:AssemblyVersion=$($Version.File)"
        "-p:InformationalVersion=$($Version.Informational)"
        "-p:ContinuousIntegrationBuild=$($Version.IsCi.ToString().ToLowerInvariant())"
    )
}
