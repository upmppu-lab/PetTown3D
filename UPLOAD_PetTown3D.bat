@echo off
setlocal EnableExtensions DisableDelayedExpansion
chcp 65001 >nul
set "PROJECT=C:\UnityProjects\PetTown3D"
set "REMOTE=https://github.com/upmppu-lab/PetTown3D.git"
set "BRANCH=main"
title PetTown3D - GitHub Upload

echo ===============================================
echo  PetTown3D - GitHub Upload
echo ===============================================
echo.
where git >nul 2>&1
if errorlevel 1 (
  echo [ERROR] Git is not installed or not in PATH.
  goto :fail
)
git lfs version >nul 2>&1
if errorlevel 1 (
  echo [ERROR] Git LFS is required. Install Git for Windows with LFS.
  goto :fail
)
if not exist "%PROJECT%\Assets\" (
  echo [ERROR] Unity Assets folder missing: "%PROJECT%"
  goto :fail
)
if not exist "%PROJECT%\Packages\manifest.json" (
  echo [ERROR] Unity Packages\manifest.json missing.
  goto :fail
)
if not exist "%PROJECT%\ProjectSettings\" (
  echo [ERROR] Unity ProjectSettings folder missing.
  goto :fail
)
cd /d "%PROJECT%" || goto :fail

rem Ensure this script never operates on a different repository nested in the project.
if exist ".git\" (
  for /f "delims=" %%R in ('git rev-parse --show-toplevel 2^>nul') do set "REPO_ROOT=%%R"
  if /I not "%REPO_ROOT%"=="%PROJECT%" (
    echo [ERROR] Repository root differs from project folder: "%REPO_ROOT%"
    goto :fail
  )
) else (
  echo [INFO] Initializing Git repository...
  git init -b %BRANCH% || goto :fail
)

if not exist ".gitignore" (
  echo [INFO] Creating Unity .gitignore...
  > ".gitignore" (
    echo /[Ll]ibrary/
    echo /[Tt]emp/
    echo /[Oo]bj/
    echo /[Bb]uild/
    echo /[Bb]uilds/
    echo /[Ll]ogs/
    echo /[Uu]ser[Ss]ettings/
    echo /[Mm]emoryCaptures/
    echo /[Rr]ecordings/
    echo /.vs/
    echo /.idea/
    echo /.gradle/
    echo /ExportedObj/
    echo /[Aa]ssets/Plugins/Editor/JetBrains*/
    echo *.csproj
    echo *.unityproj
    echo *.sln
    echo *.slnx
    echo *.suo
    echo *.user
    echo *.userprefs
    echo *.tmp
    echo *.log
    echo *.pdb
    echo *.mdb
    echo *.blend1
    echo *.blend1.meta
    echo *.apk
    echo *.aab
    echo *.unitypackage
    echo *.unitypackage.meta
    echo .DS_Store
    echo Thumbs.db
  )
)

rem Track heavy binary asset formats with LFS. Unity .meta and scene files remain regular Git files.
git lfs install --local || goto :fail
for %%E in (fbx glb gltf blend psd psb tga exr tif tiff wav mp3 ogg mp4 mov) do (
  git lfs track "*.%%E" >nul || goto :fail
)

rem Protect against pushing to the wrong remote.
for /f "delims=" %%U in ('git remote get-url origin 2^>nul') do set "CURRENT_REMOTE=%%U"
if defined CURRENT_REMOTE (
  if /I not "%CURRENT_REMOTE%"=="%REMOTE%" (
    echo [ERROR] Existing origin points elsewhere: %CURRENT_REMOTE%
    echo [ERROR] No remote was changed. Check manually.
    goto :fail
  )
) else (
  git remote add origin "%REMOTE%" || goto :fail
)

rem Check for remote commits before first upload. Never overwrite remote history.
echo [INFO] Checking GitHub repository...
git ls-remote --exit-code --heads origin %BRANCH% >nul 2>&1
if not errorlevel 1 (
  git fetch origin %BRANCH% || goto :fail
  git rev-parse --verify HEAD >nul 2>&1
  if errorlevel 1 (
    echo [STOP] GitHub main already has commits, but this local repository is new.
    echo [STOP] No upload attempted. Contact me with this message for safe merging.
    goto :fail
  )
  git merge-base HEAD origin/%BRANCH% >nul 2>&1
  if errorlevel 1 (
    echo [STOP] Local and remote histories are unrelated. No upload attempted.
    goto :fail
  )
  git merge-base --is-ancestor origin/%BRANCH% HEAD
  if errorlevel 1 (
    echo [STOP] Remote has changes not in this folder.
    echo [STOP] Run DOWNLOAD safely before uploading. No force push.
    goto :fail
  )
)

rem Stage additions, edits, and deletions.
echo [INFO] Staging changed project files...
git add -A || goto :fail

rem Refuse staged non-LFS files >= 100 MiB (GitHub rejects these).
powershell -NoProfile -ExecutionPolicy Bypass -Command "$paths = git diff --cached --name-only --diff-filter=ACMR -z; $items = ($paths -join '') -split [char]0; foreach ($p in $items) { if (-not $p) { continue }; if ((Test-Path -LiteralPath $p -PathType Leaf) -and (Get-Item -LiteralPath $p).Length -ge 100MB) { $a = git check-attr filter -- $p; if ($a -notmatch 'filter: lfs') { Write-Host ('[ERROR] File over 100MB without LFS: ' + $p); exit 1 } } }"
if errorlevel 1 goto :fail

git diff --cached --quiet
if errorlevel 1 (
  echo [INFO] Committing changes...
  git commit -m "PetTown3D update %DATE% %TIME%" || goto :fail
) else (
  echo [INFO] No new changes to commit.
)

rem Ensure local main is the branch being published.
for /f "delims=" %%B in ('git branch --show-current') do set "CURRENT_BRANCH=%%B"
if /I not "%CURRENT_BRANCH%"=="%BRANCH%" (
  echo [STOP] Current branch is "%CURRENT_BRANCH%", expected "%BRANCH%".
  goto :fail
)

echo [INFO] Pushing to GitHub...
git push -u origin %BRANCH% || goto :fail
echo.
echo [SUCCESS] Upload complete: %REMOTE%
echo [INFO] Check GitHub to confirm files are visible.
pause
exit /b 0
:fail
echo.
echo [FAILED OR STOPPED] No force push or destructive reset was performed.
echo Copy the error message or screenshot and send it to me.
pause
exit /b 1
