@echo off
rem Builds Amoebius as ONE self-contained .exe (no .NET needed to run it).
rem Needs the .NET 10 SDK (https://dot.net). Output: Port\dist\launcher\Amoebius.exe
cd /d "%~dp0"
dotnet publish src\CosmicShore.Launcher -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist\launcher
if errorlevel 1 ( echo. & echo Launcher build failed - see the messages above. & pause & exit /b 1 )
echo.
echo Done: dist\launcher\Amoebius.exe  - share this one file.
pause
