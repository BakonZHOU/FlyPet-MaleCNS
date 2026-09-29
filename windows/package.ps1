param(
    [ValidateSet('windows-x64','windows-arm64')]
    [string]$Target = 'windows-x64',
    [string]$Version = '1.1.4'
)
$ErrorActionPreference = 'Stop'
$runtime = if ($Target -eq 'windows-arm64') { 'win-arm64' } else { 'win-x64' }
$repo = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repo ".build/windows/$Target"
$release = Join-Path $repo 'releases/latest'
& (Join-Path $PSScriptRoot 'build.ps1') -Runtime $runtime -Output $output
if ($LASTEXITCODE -ne 0) { throw "Packaging failed: $Target" }
New-Item -ItemType Directory -Path $release -Force | Out-Null
$expanded = Join-Path $release $Target
if (Test-Path -LiteralPath $expanded) { Remove-Item -LiteralPath $expanded -Recurse -Force }
Copy-Item -LiteralPath $output -Destination $expanded -Recurse -Force
$archive = Join-Path $release "FlyPet-$Version-$Target.zip"
if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
Compress-Archive -Path (Join-Path $expanded '*') -DestinationPath $archive -Force
Write-Host "Ready: $expanded/FlyPet.exe and $archive"
