@echo off
rem One-click test loop: pull the newest build from the branch, extract, play.
rem One-time setup: git clone the repo, switch to claude/quirky-cannon-sk8a02,
rem then keep double-clicking this file.
cd /d "%~dp0"
echo Pulling latest build...
git pull --ff-only
if errorlevel 1 ( echo git pull failed - check connection/auth & pause & exit /b 1 )
powershell -NoProfile -Command "Expand-Archive -Force 'dist\SkimRace-Windows.zip' 'dist\SkimRace-latest'"
cd dist\SkimRace-latest
SkimRace.exe
pause
