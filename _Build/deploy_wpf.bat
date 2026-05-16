@echo off
setlocal

:: Configuration
set "ProjectName=OCC-ERP"
set "ProjectPath=..\src\OCC.WpfClient\OCC.WpfClient.csproj"
set "PublishDir=publish_wpf"
set "ReleaseDir=releases_wpf"
set "IconPath=..\src\OCC.WpfClient\Assets\Images\app.ico"

:: Extract version from .csproj file and trim whitespace
for /f "tokens=3 delims=><" %%a in ('findstr /i "<Version>" "%ProjectPath%"') do set VERSION=%%a
set VERSION=%VERSION: =%

echo ========================================================
echo [WPF] RELEASE BUILD AND PACKAGE AUTOMATION (v%VERSION%)
echo ========================================================

:: Ensure we are in the repository root
cd /d "%~dp0"

:: 1. Clean and Build
echo [BUILD] Compiling %ProjectName% (Self-Contained win-x64)...
if exist "%PublishDir%" rd /s /q "%PublishDir%"
dotnet publish "%ProjectPath%" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfContained=true -p:PublishReadyToRun=true -o "%PublishDir%" /p:Version=%VERSION%

if %errorlevel% neq 0 (
    echo [ERROR] Build failed. Deployment aborted.
    pause
    exit /b %errorlevel%
)

:: 2. Package with Velopack
echo [PACKAGE] Creating Velopack installer...
vpk pack -u "OCC-ERP" -p %PublishDir% -e "%ProjectName%.exe" --packTitle "OCC-ERP" --packAuthors "Origize63" -v %VERSION% --icon "%IconPath%" -o %ReleaseDir%

if %errorlevel% neq 0 (
    echo [ERROR] Packaging failed.
    pause
    exit /b %errorlevel%
)

echo [SUCCESS] WPF version %VERSION% packaged successfully!
echo Setup file can be found in the '%ReleaseDir%' folder.
pause
endlocal
