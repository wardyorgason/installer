<#
.SYNOPSIS
  Runs every UnitTests.* project and writes JUnit XML files to dist/test-results for Jenkins.
.NOTES
  Tests that need real external tools carry [Category("Integration")]; tests that run the whole app on the samples
  carry [Category("EndToEnd")]. Both are excluded unless -IncludeIntegration / -IncludeEndToEnd. Either kind calls
  Assert.Ignore when the host lacks what it needs (a Mac, a signing identity, makensis, Docker).
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$IncludeIntegration,
    [switch]$IncludeEndToEnd,
    [switch]$NoBuild
)
. (Join-Path $PSScriptRoot 'Common.ps1')

$resultsDir = Join-Path $DistDir 'test-results'
New-Item -ItemType Directory -Force $resultsDir | Out-Null
Get-ChildItem $resultsDir -Filter *.xml -ErrorAction SilentlyContinue | Remove-Item -Force

$projects = Get-ChildItem $SolutionDir -Directory -Filter 'UnitTests.*' | ForEach-Object { Join-Path $_.FullName "$($_.Name).csproj" }

$failed = @()
foreach ($project in $projects) {
    $name = [IO.Path]::GetFileNameWithoutExtension($project)
    Write-Host ""
    Write-Host "==> Test $name" -ForegroundColor Cyan
    # --blame-hang aborts a stuck test after the timeout instead of freezing the pipeline.
    $testArgs = @('test', $project, '--nologo', '-c', $Configuration, '--logger', "junit;LogFilePath=$resultsDir/$name.xml;MethodFormat=Class;FailureBodyFormat=Verbose", '--blame-hang', '--blame-hang-timeout', '15m')
    if ($NoBuild) { $testArgs += '--no-build' }
    $exclude = @()
    if (-not $IncludeIntegration) { $exclude += 'TestCategory!=Integration' }
    if (-not $IncludeEndToEnd) { $exclude += 'TestCategory!=EndToEnd' }
    if ($exclude.Count -gt 0) { $testArgs += @('--filter', ($exclude -join '&')) }
    & dotnet @testArgs
    if ($LASTEXITCODE -ne 0) { $failed += $name }
}

if ($failed.Count -gt 0) { throw "Test projects failed: $($failed -join ', ')" }
Write-Host ""
Write-Host "All $($projects.Count) test projects passed. JUnit XML in dist/test-results." -ForegroundColor Green
