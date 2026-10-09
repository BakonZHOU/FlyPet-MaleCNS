param(
    [string]$Destination = (Join-Path $env:LOCALAPPDATA 'FlyPet\models')
)

$ErrorActionPreference = 'Stop'
$url = 'https://alphacephei.com/vosk/models/vosk-model-small-cn-0.22.zip'
$model = Join-Path $Destination 'vosk-model-small-cn-0.22'
if (Test-Path -LiteralPath (Join-Path $model 'am\final.mdl')) {
    Write-Host "Offline voice model is already installed: $model"
    exit 0
}
New-Item -ItemType Directory -Force -Path $Destination | Out-Null
$zip = Join-Path $env:TEMP 'FlyPet-vosk-model-small-cn-0.22.zip'
try {
    Write-Host 'Downloading offline Chinese voice model (about 42 MB)...'
    Invoke-WebRequest -Uri $url -OutFile $zip
    Expand-Archive -LiteralPath $zip -DestinationPath $Destination -Force
    if (-not (Test-Path -LiteralPath (Join-Path $model 'am\final.mdl'))) { throw 'Voice model archive has an unexpected structure.' }
    Write-Host "Installation complete: $model"
} finally {
    Remove-Item -LiteralPath $zip -Force -ErrorAction SilentlyContinue
}
