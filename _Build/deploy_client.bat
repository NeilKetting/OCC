@echo off
setlocal

:: Extract version from .csproj file and trim whitespace
for /f "tokens=2 delims=<> " %%a in ('findstr /i "<Version>" "..\OCC.Client\OCC.Client.Desktop\OCC.Client.Desktop.csproj"') do set VERSION=%%a
set VERSION=%VERSION: =%

echo ========================================================
echo [CLIENT] RELEASE BUILD AND PACKAGE AUTOMATION (v%VERSION%)
echo ========================================================

:: Ensure we are in the repository root
cd /d "%~dp0"

:: 1. Clean and Build
echo [BUILD] Compiling OCC.Client Desktop (Self-Contained win-x64)...
dotnet publish "..\OCC.Client\OCC.Client.Desktop\OCC.Client.Desktop.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfContained=true -o publish

if %errorlevel% neq 0 (
    echo [ERROR] Build failed. Deployment aborted.
    pause
    exit /b %errorlevel%
)

:: 2. Package with Velopack
echo [PACKAGE] Creating Velopack installer...
:: Adding --icon to fix branding and ensuring packId matches your old script
vpk pack -u "OCC.Beta" --packTitle "Orange Circle Construction" --packAuthors "Origize63" -v %VERSION% -p publish -e "OCC.Client.Desktop.exe" -o releases --icon "..\OCC.Client\OCC.Client.Desktop\Assets\app.ico"

if %errorlevel% neq 0 (
    echo [ERROR] Packaging failed.
    pause
    exit /b %errorlevel%
)

echo [SUCCESS] Client version %VERSION% packaged successfully!
echo Setup file can be found in the 'releases' folder.
pause
endlocal
