@echo off
rem Builds the Android app (an .apk you can install on a phone), the way Unity's
rem Build Settings > Android > Build does: only what the game ships goes in.
rem Double-click it from inside the clone. Needs the .NET 10 SDK (https://dot.net).
rem The first run also installs the .NET Android workload and the Android SDK (a few GB, once).
rem Output: Builds\Android\CosmicShore.apk
cd /d "%~dp0\.."
dotnet run --project Port\src\CosmicShore.Build -c Release -- android %*
if errorlevel 1 ( echo. & echo Android build failed - see the messages above. & pause & exit /b 1 )
echo.
echo Done: Builds\Android\CosmicShore.apk
echo To install: plug in the phone with USB debugging on, then run
echo   adb install -r Builds\Android\CosmicShore.apk
pause
