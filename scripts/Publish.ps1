<#
.SYNOPSIS
  Publishes Installer.Cli as a portable framework-dependent build and zips it into dist/.
.DESCRIPTION
  The zip runs on any macOS or Linux (glibc, x64/arm64) build host with the .NET 10 runtime:
    dotnet <unpacked>/Installer.Cli.dll build installer.json
.OUTPUTS
  dist/Installer-<version>.zip, dist/Installer-<version>.zip.sha256 (sha256sum format), dist/version.txt, and
  dist/release.json (version, commit, asset name and SHA-256), which scripts/Publish-Release.ps1 reads.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$BuildNumber = $env:BUILD_NUMBER
)
. (Join-Path $PSScriptRoot 'Common.ps1')

$version = Get-InstallerVersion -BuildNumber $BuildNumber
$publishDir = Join-Path $DistDir 'publish/installer'
$zipPath = Join-Path $DistDir "Installer-$($version.Full).zip"
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
New-Item -ItemType Directory -Force $DistDir | Out-Null

Invoke-Step "Publish Installer.Cli ($($version.Informational))" {
    dotnet publish $CliProject --nologo -c $Configuration --no-self-contained -o $publishDir @(Get-VersionArgs $version)
}
# SkiaSharp ships native libraries for many runtimes (musl, riscv64, loongarch64, x86, …); the builder supports macOS
# and glibc Linux on x64/arm64 hosts, so everything else is dead weight (over 100 MB).
$supportedRuntimes = @('osx', 'linux-x64', 'linux-arm64')
Get-ChildItem (Join-Path $publishDir 'runtimes') -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -notin $supportedRuntimes } |
    Remove-Item -Recurse -Force
# License and third-party notices travel with the binaries (MIT/BSD/FreeType/libpng/DNG terms require it). SkiaSharp's
# notice file covers its native libraries; it is copied unchanged from the NuGet package that supplied them.
Copy-Item (Join-Path $RepoRoot 'LICENSE'), (Join-Path $RepoRoot 'THIRD-PARTY-NOTICES.md') $publishDir
[xml]$packages = Get-Content (Join-Path $SolutionDir 'Directory.Packages.props')
$skiaVersion = ($packages.Project.ItemGroup.PackageVersion | Where-Object Include -eq 'SkiaSharp.NativeAssets.Linux.NoDependencies').Version
$nugetRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $HOME '.nuget/packages' }
$skiaNotices = Join-Path $nugetRoot "skiasharp.nativeassets.linux.nodependencies/$skiaVersion/THIRD-PARTY-NOTICES.txt"
if (-not (Test-Path $skiaNotices)) { throw "SkiaSharp's notice file was not found at $skiaNotices (restore first)." }
Copy-Item $skiaNotices (Join-Path $publishDir 'SkiaSharp-THIRD-PARTY-NOTICES.txt')

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -CompressionLevel Optimal
Set-Content -Path (Join-Path $DistDir 'version.txt') -Value $version.Informational -NoNewline

$zipName = Split-Path $zipPath -Leaf
$sha256 = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Path "$zipPath.sha256" -Value "$sha256  $zipName" -NoNewline
$commit = ''
try { $commit = (& git -C $RepoRoot rev-parse HEAD 2>$null) } catch { }
if ($LASTEXITCODE -ne 0 -or -not $commit) { $commit = '' }
$global:LASTEXITCODE = 0
[ordered]@{
    version              = $version.Full
    informationalVersion = $version.Informational
    commit               = $commit
    asset                = $zipName
    sha256               = $sha256
} | ConvertTo-Json | Set-Content -Path (Join-Path $DistDir 'release.json')
$size = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host "Wrote dist/$(Split-Path $zipPath -Leaf) ($size MB)" -ForegroundColor Green
