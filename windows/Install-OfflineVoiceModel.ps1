param(
    [string]$Destination = (Join-Path $env:LOCALAPPDATA 'FlyPet\models')
)

$ErrorActionPreference = 'Stop'
$url = 'https://alphacephei.com/vosk/models/vosk-model-small-cn-0.22.zip'
$model = Join-Path $Destination 'vosk-model-small-cn-0.22'
if (Test-Path -LiteralPath (Join-Path $model 'am\final.mdl')) {
    Write-Host "语音包已安装：$model"
    exit 0
}
New-Item -ItemType Directory -Force -Path $Destination | Out-Null
$zip = Join-Path $env:TEMP 'FlyPet-vosk-model-small-cn-0.22.zip'
try {
    Write-Host '正在下载离线中文语音包（约 42 MB）…'
    Invoke-WebRequest -Uri $url -OutFile $zip
    Expand-Archive -LiteralPath $zip -DestinationPath $Destination -Force
    if (-not (Test-Path -LiteralPath (Join-Path $model 'am\final.mdl'))) { throw '语音包解压后结构不完整。' }
    Write-Host "安装完成：$model"
} finally {
    Remove-Item -LiteralPath $zip -Force -ErrorAction SilentlyContinue
}
