param(
    [ValidateSet('windows-x64','windows-arm64')]
    [string]$Target = 'windows-x64',
    [string]$Version = '1.2.1'
)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'windows/package.ps1') -Target $Target -Version $Version
