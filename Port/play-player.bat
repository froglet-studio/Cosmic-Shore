@echo off
rem One-click test of the REAL game running without Unity (CosmicShore.Player).
rem One-time setup: git clone https://github.com/froglet-studio/Cosmic-Shore.git
rem                 git switch cece/focused-planck-cj46y3
rem then double-click this file. No .NET SDK needed: the build is self-contained.
rem The build reads scenes/prefabs/art/audio straight from this checkout's Assets/,
rem so it must be extracted INSIDE the clone (this script does that).
cd /d "%~dp0"
echo Pulling latest build...
git pull --ff-only
if errorlevel 1 ( echo git pull failed - check connection/auth & pause & exit /b 1 )
if exist dist\Player-latest rmdir /s /q dist\Player-latest
powershell -NoProfile -Command "Expand-Archive -Force 'dist\CosmicShore-Player-Windows.zip' 'dist\Player-latest'"
if errorlevel 1 ( echo extract failed & pause & exit /b 1 )
cd dist\Player-latest
CosmicShore.exe
pause
