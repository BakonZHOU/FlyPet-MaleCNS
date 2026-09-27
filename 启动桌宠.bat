@echo off
cd /d "%~dp0"
if exist "dist\FlyPet.exe" (
    start "" "dist\FlyPet.exe"
) else (
    echo Build missing. Run build.ps1 first.
    pause
)
