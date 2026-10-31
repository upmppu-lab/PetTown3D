@echo off
setlocal
chcp 65001 >nul
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Upload-PetTown3D-Light.ps1" %*
set "UPLOAD_RESULT=%ERRORLEVEL%"
echo.
if not "%UPLOAD_RESULT%"=="0" echo Upload failed. Read the error above. Nothing was force-pushed.
pause
exit /b %UPLOAD_RESULT%
