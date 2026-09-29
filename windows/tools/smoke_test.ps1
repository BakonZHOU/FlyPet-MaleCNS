# Run against a running FlyPet using its supported current-user local control API.
param(
    [switch]$IncludeExit,
    [string]$BuildDirectory = ''
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($BuildDirectory)) { $BuildDirectory = Join-Path (Split-Path $root -Parent) '.build/windows/windows-x64' }
$exe = Join-Path $BuildDirectory 'FlyPet.exe'
$out = Join-Path (Split-Path $root -Parent) '.build/smoke-test'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$reply = Join-Path $out 'command-reply.json'
$checks = [System.Collections.Generic.List[object]]::new()
function Command([string]$value) {
    $process = Start-Process -FilePath $exe -ArgumentList ('--command ' + $value + ' --output "' + $reply + '"') -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0) { throw "Command failed ($($process.ExitCode)): $value" }
    return Get-Content -LiteralPath $reply -Raw | ConvertFrom-Json
}
function Check([string]$name,[bool]$passed,$detail) {
    $checks.Add([pscustomobject]@{name=$name;passed=$passed;detail=$detail})
    if (!$passed) { throw "Check failed: $name" }
}
try {
    $s = Command 'show'
    Check 'native_windows_running' $s.visible $s
    $s = Command 'pause'
    Check 'pause' $s.paused $null
    $s = Command 'recall'
    $x = [int]$s.x; $y = [int]$s.y
    $s = Command "drop $x $y"
    Check 'drop_sugar' ($s.sugar -ge 1) $s.sugar
    $s = Command 'sugar-mode'
    Check 'sugar_tool' ($s.mode -eq 'Sugar') $null
    $s = Command 'swatter'
    Check 'swatter_tool' ($s.mode -eq 'Swatter') $null
    $s = Command 'normal'
    Check 'normal_tool' ($s.mode -eq 'Normal') $null
    for ($i=0; $i -lt 3; $i++) {
        $s = Command 'resume'
        Start-Sleep -Milliseconds 300
        $s = Command 'pause'
        $s = Command ('swat {0} {1}' -f [int]$s.x,[int]$s.y)
    }
    Check 'native_host_damage_and_death' $s.dead $s
    $s = Command 'hide'
    Check 'hide' (!$s.visible) $null
    $s = Command 'show'
    Check 'show_and_resume' ($s.visible -and !$s.paused) $null
    $s = Command 'clear'
    Check 'clear_food' ($s.sugar -eq 0) $null
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    while ($s.dead -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 600
        $s = Command 'status'
    }
    Check 'live_random_respawn' (!$s.dead -and $s.health -eq 100) $s
    $petProcess = Get-Process FlyPet | Select-Object -First 1
    $cpuStart = $petProcess.CPU
    Start-Sleep -Seconds 3
    $petProcess.Refresh()
    $cpuPerCore = ($petProcess.CPU - $cpuStart) / 3 * 100
    $s = Command 'status'
    Check 'live_realtime' ($s.realtime -gt 0.9 -and $s.fps -gt 45) ([pscustomobject]@{status=$s;oneCoreCpuPercent=$cpuPerCore;workingSetMB=$petProcess.WorkingSet64/1MB})
    if ($IncludeExit) {
        $s = Command 'exit'
        Start-Sleep -Milliseconds 400
        Check 'clean_shutdown' ($null -eq (Get-Process FlyPet -ErrorAction SilentlyContinue)) $null
    }
} finally {
    $checks | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $out 'integration.json') -Encoding utf8
}
$checks | Select-Object name,passed | Format-Table
