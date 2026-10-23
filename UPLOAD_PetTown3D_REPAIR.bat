@echo off
setlocal EnableExtensions
set "PROJECT=C:\UnityProjects\PetTown3D"
set "REMOTE=https://github.com/upmppu-lab/PetTown3D.git"
cd /d "%PROJECT%" || (echo [ERROR] Project folder missing.& pause & exit /b 1)
if not exist ".git\" (echo [ERROR] Git repository missing.& pause & exit /b 1)
git rev-parse --show-toplevel
if errorlevel 1 (echo [ERROR] Invalid repository.& pause & exit /b 1)
git remote set-url origin "%REMOTE%"
if errorlevel 1 (echo [ERROR] Remote setup failed.& pause & exit /b 1)
if not exist ".gitignore" (echo [ERROR] Missing .gitignore. Stop for safety.& pause & exit /b 1)
findstr /I /C:"BACKUP/" ".gitignore" >nul || echo /BACKUP/>>".gitignore"
git add -A
if errorlevel 1 (echo [ERROR] Stage failed.& pause & exit /b 1)
git diff --cached --quiet
if errorlevel 1 (
  git commit -m "Save laptop Unity project" || (echo [ERROR] Commit failed.& pause & exit /b 1)
)
git fetch origin main || (echo [ERROR] Fetch failed.& pause & exit /b 1)
git merge-base --is-ancestor origin/main HEAD
if errorlevel 1 (
 echo.
 echo [STOP] GitHub contains changes not in this laptop.
 echo [SAFE] Local changes are committed. No overwrite or force push.
 echo Send this screen for next step.
 pause
 exit /b 2
)
git push -u origin HEAD:main || (echo [ERROR] Push failed.& pause & exit /b 1)
echo [SUCCESS] Uploaded safely.
pause
