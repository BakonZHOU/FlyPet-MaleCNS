param(
    [ValidateSet('win-x64','win-arm64')]
    [string]$Runtime = 'win-x64',
    [string]$Output = ''
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($Output)) { $Output = Join-Path $repo ".build/windows/$Runtime" }
Push-Location $PSScriptRoot
try {
    dotnet publish FlyPet.csproj -c Release -r $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -o $Output --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    Copy-Item -LiteralPath (Join-Path $repo 'README.md') -Destination (Join-Path $Output '使用说明.md') -Force
    New-Item -ItemType Directory -Path (Join-Path $Output 'Assets') -Force | Out-Null
    Copy-Item -LiteralPath 'Assets/cockroach-preview.png' -Destination (Join-Path $Output 'Assets/cockroach-preview.png') -Force
    Copy-Item -LiteralPath THIRD_PARTY.md -Destination (Join-Path $Output 'THIRD_PARTY.md') -Force
    Copy-Item -LiteralPath licenses -Destination $Output -Recurse -Force
    Write-Host "Ready: $Output/FlyPet.exe ($Runtime)"
} finally { Pop-Location }
