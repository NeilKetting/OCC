@echo off
setlocal

:: Ensure we are running from the script's own directory
cd /d "%~dp0"

echo ========================================================
echo [SERVER] PULLING LATEST API CODE - OCC.BETA
echo ========================================================

:: 1. Find the repository root by looking for .git folder
:find_root
if exist ".git" goto found_root
if "%cd%"=="%cd:~0,3%" goto not_found
cd ..
goto find_root

:found_root
echo [INFO] Repository root found at: %cd%

:: 2. Pull latest code
echo [DEPLOY] Verifying Remote URL...
git remote get-url origin | findstr /i "OCC.Beta" >nul
if %errorlevel% neq 0 (
    echo [WARN] Remote 'origin' is not pointing to OCC.Beta. Updating...
    git remote set-url origin https://github.com/NeilKetting/OCC.Beta.git
    echo [INFO] Remote updated to OCC.Beta.
)

echo [DEPLOY] Pulling latest code from origin master...
:: Stash local changes (like appsettings.json creds) to allow pull
git stash push OCC.API/appsettings.json -m "Deployment auto-stash"
git pull origin master --no-edit

:: Try to restore stashed changes
git stash pop
if %errorlevel% neq 0 (
    echo [WARN] Conflict detected in appsettings.json. Forcing production version...
    git checkout --ours OCC.API/appsettings.json
    git stash drop
)

if %errorlevel% neq 0 (
    echo [ERROR] Git pull failed.
    pause
    exit /b %errorlevel%
)

echo [SUCCESS] Code updated successfully.
goto end

:not_found
echo [ERROR] Could not find the repository root (no .git folder found in any parent directories).
echo Checked up to: %cd%
pause
exit /b 1

:end
echo.
echo To deploy the API, run update_main.bat or publish manually.
pause
endlocal
