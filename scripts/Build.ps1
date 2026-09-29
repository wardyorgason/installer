<#
.SYNOPSIS
  Restores and builds the whole solution with a stamped version.
.EXAMPLE
  pwsh scripts/Build.ps1
  pwsh scripts/Build.ps1 -BuildNumber $env:BUILD_NUMBER -Configuration Release
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$BuildNumber = $env:BUILD_NUMBER
)
. (Join-Path $PSScriptRoot 'Common.ps1')

$version = Get-InstallerVersion -BuildNumber $BuildNumber
Write-Host "Version $($version.Informational) (file $($version.File))"

Invoke-Step 'Restore' { dotnet restore $SolutionFile --nologo }
Invoke-Step "Build ($Configuration)" {
    dotnet build $SolutionFile --nologo --no-restore -c $Configuration @(Get-VersionArgs $version)
}

New-Item -ItemType Directory -Force $DistDir | Out-Null
Set-Content -Path (Join-Path $DistDir 'version.txt') -Value $version.Informational -NoNewline
Write-Host "Wrote dist/version.txt = $($version.Informational)"
