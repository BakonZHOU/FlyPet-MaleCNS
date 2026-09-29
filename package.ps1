param(
    [ValidateSet('windows-x64','windows-arm64')]
    [string]$Target = 'windows-x64'
)
$ErrorActionPreference = 'Stop'
$runtime = if ($Target -eq 'windows-arm64') { 'win-arm64' } else { 'win-x64' }
$output = Join-Path $PSScriptRoot "dist/$Target"
& (Join-Path $PSScriptRoot 'build.ps1') -Runtime $runtime -Output $output
if ($LASTEXITCODE -ne 0) { throw "Packaging failed: $Target" }
