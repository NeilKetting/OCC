@echo off
setlocal

REM Ensure we are running from the script's own directory
cd /d "%~dp0"

REM Check for Administrative privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] This script MUST be run as Administrator!
    pause
    exit /b 1
)

echo ========================================================
echo [DEPLOY] MAIN DEPLOYMENT AND SYNC AUTOMATION
echo ========================================================
echo.

REM 1. Optional Database Sync
set /p SYNC="Sync Live DB to Main 1:1 before deployment? (Y/N): "
if /i "%SYNC%"=="Y" (
    echo [SYNC] Starting Database Synchronization...
    REM Use full path to the sql file to avoid "Invalid filename" errors
    if exist "%~dp0SQL\sync_main_db.sql" (
        sqlcmd -b -S "OCOR\OCC_SQL" -i "%~dp0SQL\sync_main_db.sql"
        if %errorlevel% neq 0 (
            echo [ERROR] Database Sync failed. 
            pause
            exit /b %errorlevel%
        )
        echo [SYNC] Database Synchronization Successful.
    ) else (
        echo [ERROR] SQL\sync_main_db.sql not found. Skipping sync.
    )
)

REM 2. Stop IIS
echo [DEPLOY] Stopping IIS Site and AppPool...
%windir%\system32\inetsrv\appcmd stop site /site.name:"OCC_API"
%windir%\system32\inetsrv\appcmd stop apppool /apppool.name:"OCC_API"
if %errorlevel% neq 0 echo [INFO] IIS already stopped or unavailable.

echo Waiting for process to release locks...
timeout /t 5 /nobreak

echo [DEPLOY] Updating code from MASTER...
git fetch origin master
git reset --hard origin/master
if %errorlevel% neq 0 (
    echo [ERROR] Git update failed!
    pause
    exit /b %errorlevel%
)

REM 4. Publish
echo [DEPLOY] Publishing to Live Folder...
if exist "..\src\OCC.API\OCC.API.csproj" (
    dotnet publish "..\src\OCC.API\OCC.API.csproj" -c Release -o "C:\inetpub\wwwroot\OCC-API" --nologo
    if %errorlevel% neq 0 (
        echo [ERROR] Publish failed!
        pause
    )
) else (
    echo [ERROR] ..\src\OCC.API\OCC.API.csproj NOT FOUND!
    pause
)

REM 5. Restart IIS
echo [DEPLOY] Starting IIS AppPool and Site...
%windir%\system32\inetsrv\appcmd start apppool /apppool.name:"OCC_API"
%windir%\system32\inetsrv\appcmd start site /site.name:"OCC_API"

echo [DEPLOY] Update Complete!
timeout /t 30
endlocal
