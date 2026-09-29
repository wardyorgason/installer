<#
.SYNOPSIS
  Publishes the builder zip in dist/ (from scripts/Publish.ps1, or copied from an installer-build Jenkins run) as a
  GitHub release, through the REST API only: no gh CLI.
.DESCRIPTION
  Reads dist/release.json, checks the zip against its recorded SHA-256, then creates the release v<version> with its
  tag on the commit the zip was built from (GitHub creates the tag; nothing is pushed), and uploads the zip and its
  .sha256 file. An existing release with the same tag is completed if assets are missing and refused if it already
  has them, so a failed run can be retried and a published release is never overwritten.

  The token comes from GITHUB_TOKEN: a fine-grained token for the repository with Contents: Read and write.
.EXAMPLE
  $env:GITHUB_TOKEN = '<token>'; pwsh scripts/Publish-Release.ps1 -Repository wardyorgason/installer
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[\w.-]+/[\w.-]+$')][string]$Repository,
    [switch]$Prerelease,
    [string]$ApiUrl = 'https://api.github.com'
)
. (Join-Path $PSScriptRoot 'Common.ps1')

$token = $env:GITHUB_TOKEN
if ([string]::IsNullOrWhiteSpace($token)) { throw 'GITHUB_TOKEN is not set (a token with Contents: Read and write on the repository).' }

$releaseFile = Join-Path $DistDir 'release.json'
if (-not (Test-Path $releaseFile)) { throw "dist/release.json is missing; run scripts/Publish.ps1 or copy the installer-build artifacts first." }
$release = Get-Content $releaseFile -Raw | ConvertFrom-Json
if (-not $release.commit) { throw 'dist/release.json has no commit: the zip was built outside a git checkout and cannot be tagged.' }

$zipPath = Join-Path $DistDir $release.asset
$shaPath = "$zipPath.sha256"
if (-not (Test-Path $zipPath)) { throw "dist/$($release.asset) is missing." }
$actual = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actual -ne $release.sha256) { throw "dist/$($release.asset) has SHA-256 $actual, but release.json records $($release.sha256)." }

$tag = "v$($release.version)"
$headers = @{
    Authorization          = "Bearer $token"
    Accept                 = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
}

function Invoke-GitHub {
    param([string]$Method, [string]$Uri, $Body, [string]$InFile, [string]$ContentType = 'application/json')
    $arguments = @{ Method = $Method; Uri = $Uri; Headers = $headers; ContentType = $ContentType }
    if ($null -ne $Body) { $arguments.Body = ($Body | ConvertTo-Json -Depth 5) }
    if ($InFile) { $arguments.InFile = $InFile }
    Invoke-RestMethod @arguments
}

$existing = $null
try {
    $existing = Invoke-GitHub -Method Get -Uri "$ApiUrl/repos/$Repository/releases/tags/$tag"
} catch {
    if ($_.Exception.Response.StatusCode.value__ -ne 404) { throw }
}

if ($existing) {
    Write-Host "Release $tag already exists: $($existing.html_url)"
    $gh = $existing
} else {
    Write-Host "Creating release $tag on $($release.commit) in $Repository"
    $notes = @"
Installer builder $($release.informationalVersion), built from $($release.commit).

Unpack ``$($release.asset)`` on a macOS or Linux build host with the .NET 10 runtime and run ``dotnet Installer.Cli.dll build installer.json``.

SHA-256: ``$($release.sha256)``
"@
    $gh = Invoke-GitHub -Method Post -Uri "$ApiUrl/repos/$Repository/releases" -Body @{
        tag_name         = $tag
        target_commitish = $release.commit
        name             = "Installer $($release.version)"
        body             = $notes
        draft            = $false
        prerelease       = [bool]$Prerelease
    }
}

$uploadBase = $gh.upload_url -replace '\{.*\}$', ''
$present = @($gh.assets | ForEach-Object { $_.name })
$uploaded = 0
foreach ($file in @($zipPath, $shaPath)) {
    $name = Split-Path $file -Leaf
    if ($present -contains $name) { continue }
    $type = if ($name.EndsWith('.zip')) { 'application/zip' } else { 'text/plain' }
    Write-Host "==> Upload $name" -ForegroundColor Cyan
    Invoke-GitHub -Method Post -Uri "$($uploadBase)?name=$([uri]::EscapeDataString($name))" -InFile $file -ContentType $type | Out-Null
    $uploaded++
}

if ($existing -and $uploaded -eq 0) {
    throw "Release $tag is already published with its assets; bump solution/version.json or release a newer build."
}

Set-Content -Path (Join-Path $DistDir 'release-url.txt') -Value $gh.html_url -NoNewline
Write-Host "Published $($gh.html_url)" -ForegroundColor Green
