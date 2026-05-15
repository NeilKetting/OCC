@echo off
setlocal

:: Configuration
set "ProjectName=OCC-Mobile"
set "ProjectPath=..\src\OCC.Mobile.Android\OCC.Mobile.Android.csproj"
set "CommonPath=..\src\OCC.Mobile\OCC.Mobile.csproj"
set "ReleaseDir=releases_mobile"

:: Extract version from .csproj file and trim whitespace
for /f "tokens=3 delims=><" %%a in ('findstr /i "<Version>" "%CommonPath%"') do set VERSION=%%a
set VERSION=%VERSION: =%

echo ========================================================
echo [MOBILE] ANDROID APK BUILD AUTOMATION (v%VERSION%)
echo ========================================================

:: Ensure we are in the script directory
cd /d "%~dp0"

:: 1. Clean and Build
echo [BUILD] Compiling %ProjectName% (Release ARM64)...
if exist "bin_mobile" rd /s /q "bin_mobile"

:: Note: We target ARM64 as it's standard for modern tablets.
:: We use dotnet publish to trigger the Android packaging task.
dotnet publish "%ProjectPath%" -c Release -f net10.0-android -p:AndroidPackageFormat=apk -p:RuntimeIdentifier=android-arm64 --self-contained true -o "bin_mobile" /p:Version=%VERSION%

if %errorlevel% neq 0 (
    echo [ERROR] Build failed. Deployment aborted.
    pause
    exit /b %errorlevel%
)

:: 2. Locate and Move APK
echo [PREPARE] Moving APK to release folder...
if not exist "%ReleaseDir%" mkdir "%ReleaseDir%"

:: Search for the generated APK in the output directory
:: Usually it picks up the signed or unsigned APK.
set "APK_FOUND=false"
for /r "bin_mobile" %%f in (*.apk) do (
    copy "%%f" "%ReleaseDir%\OCC-Mobile-v%VERSION%.apk" /Y
    set "APK_FOUND=true"
)

if "%APK_FOUND%"=="false" (
    echo [ERROR] Could not find the generated APK file in bin_mobile.
    pause
    exit /b 1
)

echo [SUCCESS] Android APK v%VERSION% built successfully!
echo File can be found in the '%ReleaseDir%' folder: OCC-Mobile-v%VERSION%.apk
echo.
echo ========================================================
echo NEXT STEPS FOR REMOTE UPDATES:
echo 1. Go to GitHub: https://github.com/NeilKetting/OCC.Mobile
echo 2. Create a new Release with Tag: v%VERSION%
echo 3. Upload 'OCC-Mobile-v%VERSION%.apk' as an asset.
echo 4. Tablets will detect the update on next app start!
echo ========================================================
echo.
pause
endlocal
