@echo off
rem Optimal dev test loop: pull latest source, build incrementally, run.
rem Requires: .NET 10 SDK + VC++ redist (one-time installs, see PORT_PLAN).
rem Runs the REAL game (CosmicShore.Player) from this checkout's Assets/.
cd /d "%~dp0"
git pull --ff-only
if errorlevel 1 ( echo git pull failed & pause & exit /b 1 )
if not exist .native\win-x64\fmodstudio.dll python tools\fetch_native.py --platform win-x64
dotnet run -c Release --project src\CosmicShore.Player
pause
