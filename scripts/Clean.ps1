<# Removes dist/ and every bin/ and obj/ under solution/. #>
[CmdletBinding()]
param()
. (Join-Path $PSScriptRoot 'Common.ps1')

if (Test-Path $DistDir) { Remove-Item $DistDir -Recurse -Force }
Get-ChildItem $SolutionDir -Directory -Recurse -Include bin, obj | Remove-Item -Recurse -Force
Write-Host "Cleaned dist/, bin/ and obj/."
