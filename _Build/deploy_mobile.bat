@echo off
setlocal

:: Configuration
set "ProjectName=OCC-Mobile"
set "ProjectPath=..\src\OCC.Mobile.Android\OCC.Mobile.Android.csproj"
set "CommonPath=..\src\OCC.Mobile\OCC.Mobile.csproj"
set "ReleaseDir=releases_mobile"
set "CONFIG=Debug"

:: Extract version and application version from the Android project file and trim whitespace
for /f "tokens=3 delims=><" %%a in ('findstr /i "<Version>" "%ProjectPath%"') do set VERSION=%%a
set VERSION=%VERSION: =%
for /f "tokens=3 delims=><" %%a in ('findstr /i "<ApplicationVersion>" "%ProjectPath%"') do set APP_VERSION=%%a
set APP_VERSION=%APP_VERSION: =%

echo ========================================================
echo [MOBILE] ANDROID APK BUILD AUTOMATION (v%VERSION% / Build %APP_VERSION%)
echo ========================================================

:: Ensure we are in the script directory
cd /d "%~dp0"

:: 1. Clean and Build
echo [BUILD] Compiling %ProjectName% (Debug ARM64 - For Testing)...
if exist "bin_mobile" rd /s /q "bin_mobile"

:: Note: Building in DEBUG mode ensures the APK is auto-signed with a 
:: debug key so it can be installed on your tablet for testing.
:: We remove a single RID to build a universal APK that supports all tablet types.
dotnet publish "%ProjectPath%" -c %CONFIG% -f net10.0-android -p:AndroidPackageFormat=apk -p:AndroidCreatePackagePerAbi=false --self-contained true -o "bin_mobile" /p:Version=%VERSION% /p:ApplicationVersion=%APP_VERSION% /p:ApplicationDisplayVersion=%VERSION%

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
for /r "bin_mobile" %%f in (*-Signed.apk) do (
    copy "%%f" "%ReleaseDir%\OCC-Mobile-v%VERSION%.apk" /Y
    set "APK_FOUND=true"
)

if "%APK_FOUND%"=="false" (
    echo [ERROR] Could not find the generated APK file in bin_mobile.
    pause
    exit /b 1
)

echo [SUCCESS] Android APK v%VERSION% built successfully!
echo File: %~dp0%ReleaseDir%\OCC-Mobile-v%VERSION%.apk
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
