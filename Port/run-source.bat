@echo off
rem Optimal dev test loop: pull latest source, build incrementally, run.
rem Requires: .NET 10 SDK + VC++ redist (one-time installs, see PORT_PLAN).
cd /d "%~dp0"
git pull --ff-only
if errorlevel 1 ( echo git pull failed & pause & exit /b 1 )
dotnet run -c Release --project src\CosmicShore.Client
pause
