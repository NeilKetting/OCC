@echo off
title Uninstall OCC-ERP
color 0C
echo ========================================================
echo               UNINSTALL OCC-ERP
echo ========================================================
echo.
echo Are you sure you want to uninstall OCC-ERP?
echo Press Ctrl+C to cancel, or
pause

echo Terminating running processes...
taskkill /F /IM OCC-ERP.exe 2>nul

set "APP_DIR=%LOCALAPPDATA%\OCC-ERP"
if exist "%APP_DIR%\Update.exe" (
    start "" "%APP_DIR%\Update.exe" --uninstall
) else (
    echo Uninstaller not found. Please uninstall OCC-ERP via Windows Settings ^> Apps.
    pause
)
