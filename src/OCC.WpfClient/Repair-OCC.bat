@echo off
title OCC-ERP Repair Utility
color 0B
echo ========================================================
echo               OCC-ERP REPAIR UTILITY
echo ========================================================
echo.
echo 1. Terminating any hanging processes...
taskkill /F /IM OCC-ERP.exe 2>nul
taskkill /F /IM Update.exe 2>nul

echo 2. Clearing update package cache ^& temporary files...
set "APP_DIR=%LOCALAPPDATA%\OCC-ERP"

if exist "%APP_DIR%\packages" (
    echo    - Removing package cache...
    rd /s /q "%APP_DIR%\packages" 2>nul
)

if exist "%APP_DIR%\temp" (
    echo    - Removing temporary files...
    rd /s /q "%APP_DIR%\temp" 2>nul
)

if exist "%LOCALAPPDATA%\Temp\Velopack" (
    echo    - Removing Velopack temporary files...
    rd /s /q "%LOCALAPPDATA%\Temp\Velopack" 2>nul
)

echo.
echo 3. Repair complete! Restarting OCC-ERP...
echo.

if exist "%APP_DIR%\Update.exe" (
    start "" "%APP_DIR%\Update.exe" --processStart "OCC-ERP.exe"
) else if exist "%APP_DIR%\current\OCC-ERP.exe" (
    start "" "%APP_DIR%\current\OCC-ERP.exe"
) else if exist "OCC-ERP.exe" (
    start "" "OCC-ERP.exe"
) else (
    echo [ERROR] OCC-ERP executable not found.
    pause
)
