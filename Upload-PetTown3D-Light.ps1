[CmdletBinding()]
param(
    [string]$ProjectPath,
    [ValidateRange(1,20)][int]$MaxFileMB = 5,
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'
$branch = 'source-lite'
$expectedRemote = 'https://github.com/upmppu-lab/PetTown3D.git'
$utf8 = New-Object System.Text.UTF8Encoding($false)
[Console]::OutputEncoding = $utf8
$OutputEncoding = $utf8
if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'ProjectSettings/ProjectVersion.txt')) {
        $ProjectPath = $PSScriptRoot
    } else { $ProjectPath = Join-Path $PSScriptRoot 'PetTown3D' }
}

function Run-Git {
    param([string[]]$Arguments)
    & git @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Git failed (exit $LASTEXITCODE): $($Arguments -join ' ')" }
}
function Write-Utf8([string]$Path, [string]$Text) {
    [IO.File]::WriteAllText($Path, $Text, $utf8)
}

try {
    Get-Command git -ErrorAction Stop | Out-Null
    $project = (Resolve-Path -LiteralPath $ProjectPath).Path.TrimEnd('\','/')
    if (!(Test-Path -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt'))) {
        throw 'This is not a Unity project.'
    }
    $sourceArgs = @('-c', "safe.directory=$($project.Replace('\','/'))", '-C', $project)
    $remoteOutput = @(& git @sourceArgs config --get remote.origin.url)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read source repository origin.' }
    $remote = ($remoteOutput -join '').Trim()
    if ($remote -ne $expectedRemote -and $remote -ne 'git@github.com:upmppu-lab/PetTown3D.git') {
        throw "Unexpected GitHub repository: $remote. Check the script before uploading."
    }

    # Explicit allowlist: no third-party asset-pack folders, textures, models,
    # audio, animation payloads, archives, credentials, caches or local saves.
    $assetExtensions = @('.cs','.shader','.hlsl','.cginc','.compute','.shadergraph',
        '.shadersubgraph','.asmdef','.asmref','.unity','.prefab','.mat','.asset',
        '.controller','.overridecontroller','.uxml','.uss','.inputactions')
    $selected = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    $omitted = New-Object 'System.Collections.Generic.List[object]'
    $limit = [long]$MaxFileMB * 1MB
    $roots = @('Assets/PetTown3D','ProjectSettings','Packages','docs')
    foreach ($root in $roots) {
        $folder = Join-Path $project $root
        if (!(Test-Path -LiteralPath $folder)) { continue }
        foreach ($file in Get-ChildItem -LiteralPath $folder -File -Recurse) {
            $relative = $file.FullName.Substring($project.Length + 1).Replace('\','/')
            if ($file.Extension -eq '.meta') { continue }
            $reason = $null
            if ($relative -match '(?i)(backup|\.bak(?:_|\.|$)|/Captures/|/Screenshots/)' -or
                $relative -match '(?i)(secret|credential|keystore|\.env(?:\.|$)|private.?key)') {
                $reason = 'backup-or-private-file'
            } elseif ($file.Length -gt $limit) { $reason = 'over-size-limit' }
            elseif ($root -eq 'Assets/PetTown3D' -and $assetExtensions -notcontains $file.Extension.ToLowerInvariant()) {
                $reason = 'asset-payload-excluded'
            } elseif ($root -eq 'Packages' -and $relative -notin @('Packages/manifest.json','Packages/packages-lock.json')) {
                $reason = 'embedded-package-excluded'
            } elseif ($root -eq 'ProjectSettings' -and $file.Extension -notin @('.asset','.json','.txt')) {
                $reason = 'non-setting-file'
            } elseif ($root -eq 'docs' -and $file.Extension -notin @('.md','.txt')) {
                $reason = 'non-text-document'
            }
            if ($reason) {
                $omitted.Add([pscustomobject]@{path=$relative;bytes=$file.Length;reason=$reason})
                continue
            }
            [void]$selected.Add($relative)
            if (Test-Path -LiteralPath ($file.FullName + '.meta')) {
                if ((Get-Item -LiteralPath ($file.FullName + '.meta')).Length -le $limit) {
                    [void]$selected.Add($relative + '.meta')
                }
            }
            $parent = Split-Path -Parent $file.FullName
            while ($parent.Length -gt $project.Length) {
                $parentMeta = $parent + '.meta'
                if (Test-Path -LiteralPath $parentMeta) {
                    if ((Get-Item -LiteralPath $parentMeta).Length -le $limit) {
                        [void]$selected.Add($parentMeta.Substring($project.Length + 1).Replace('\','/'))
                    }
                }
                $parent = Split-Path -Parent $parent
            }
        }
    }
    if (!$selected.Contains('Packages/manifest.json')) { throw 'Packages/manifest.json is missing.' }
    foreach ($required in @('ProjectSettings/ProjectVersion.txt','Assets/PetTown3D/Scripts/PetTownGame.cs')) {
        if (!$selected.Contains($required)) { throw "Required source file missing: $required" }
    }

    $bytes = [long]0
    foreach ($relative in $selected) {
        $file = Join-Path $project $relative
        $bytes += (Get-Item -LiteralPath $file).Length
        # Fail closed on recognizable credentials; never print matched secrets.
        $text = [IO.File]::ReadAllText($file)
        if ($text -match '(ghp_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|-----BEGIN (RSA |OPENSSH |EC )?PRIVATE KEY-----|AKIA[A-Z0-9]{16})') {
            throw "Possible credential detected in $relative. Upload stopped."
        }
    }
    $reportDirectory = Join-Path $PSScriptRoot 'PetTown3D_UploadReports'
    New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
    $reportPath = Join-Path $reportDirectory 'latest-plan.json'
    $excludedAssetRoots = @(Get-ChildItem -LiteralPath (Join-Path $project 'Assets') -Directory |
        Where-Object Name -ne 'PetTown3D' | Select-Object -ExpandProperty Name)
    $plan = [ordered]@{project=$project;remote=$remote;branch=$branch;fileLimitMB=$MaxFileMB;
        fileCount=$selected.Count;totalMiB=[math]::Round($bytes/1MB,2);
        included=@($selected | Sort-Object);excludedAssetFolders=$excludedAssetRoots;excluded=@($omitted.ToArray())}
    Write-Utf8 $reportPath ($plan | ConvertTo-Json -Depth 5)
    Write-Host "Project: $project"
    Write-Host "Destination: $remote [$branch]"
    Write-Host "Selected: $($selected.Count) files, $([math]::Round($bytes/1MB,2)) MiB"
    Write-Host "Report: $reportPath"
    if ($DryRun) { Write-Host 'DRY RUN complete. No network, commit or upload performed.'; exit 0 }

    # A unique scratch checkout isolates all Git mutations from the source project.
    $scratch = Join-Path ([IO.Path]::GetTempPath()) ('PetTown3D-SourceUpload-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $scratch | Out-Null
    Write-Host "Upload working folder: $scratch"
    $gitArgs = @('-c',"safe.directory=$($scratch.Replace('\','/'))",'-C',$scratch)
    Run-Git ($gitArgs + @('init','--quiet'))
    Run-Git ($gitArgs + @('remote','add','origin',$remote))
    $existing = @(& git @gitArgs ls-remote --heads origin "refs/heads/$branch")
    if ($LASTEXITCODE -ne 0) { throw 'GitHub connection failed. Check sign-in and network.' }
    if ($existing.Count -gt 0) {
        Run-Git ($gitArgs + @('fetch','--depth','1','origin',"refs/heads/${branch}:refs/remotes/origin/$branch"))
        Run-Git ($gitArgs + @('checkout','--quiet','-b',$branch,"origin/$branch"))
        # Only files tracked in the scratch source-lite checkout are removed.
        Run-Git ($gitArgs + @('rm','-r','--quiet','--ignore-unmatch','--','.'))
    } else { Run-Git ($gitArgs + @('checkout','--orphan',$branch)) }

    foreach ($relative in $selected) {
        $destination = Join-Path $scratch $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $project $relative) -Destination $destination
        if ((Get-Item -LiteralPath $destination).Length -gt $limit) { throw "File grew during export: $relative" }
    }
    Write-Utf8 (Join-Path $scratch '.gitignore') "/Library/`n/Temp/`n/Logs/`n/Builds/`n/UserSettings/`n"
    Write-Utf8 (Join-Path $scratch '.gitattributes') "* text=auto`n"
    Write-Utf8 (Join-Path $scratch 'SOURCE_ONLY_README.md') @"
# PetTown3D source-only snapshot

This branch contains code, scenes, prefabs, materials and Unity settings.
Models, textures, fonts, audio, animation payloads and external asset packs are excluded.
Files larger than $MaxFileMB MiB are excluded. This is NOT a complete playable project.
Restore matching assets and their original .meta files from the full project before running.
Do not merge asset deletions from this branch into the full-project main branch.
Review UPLOAD_MANIFEST.json for the exact included files and excluded asset folders.

The upload script uses a separate checkout and never changes the source project's index,
working files, branch, stash, or history. Uploads are normal fast-forward pushes, never force pushes.
"@
    Write-Utf8 (Join-Path $scratch 'UPLOAD_MANIFEST.json') ($plan | ConvertTo-Json -Depth 5)
    foreach ($helper in @('UPLOAD_PetTown3D_LIGHT.bat','Upload-PetTown3D-Light.ps1','UPLOAD_PetTown3D_LIGHT_KO.md')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $helper) -Destination (Join-Path $scratch $helper)
    }
    Run-Git ($gitArgs + @('add','-A'))
    & git @gitArgs diff --cached --quiet
    $diffCode = $LASTEXITCODE
    if ($diffCode -eq 0) { Write-Host 'No changes. GitHub is already up to date.'; exit 0 }
    if ($diffCode -ne 1) { throw 'Cannot inspect staged changes.' }
    Run-Git ($gitArgs + @('-c','user.name=upmppu-lab','-c','user.email=upmppu@gmail.com',
        'commit','--quiet','-m',('chore: source-only snapshot ' + (Get-Date -Format 'yyyy-MM-dd HH:mm'))))
    Run-Git ($gitArgs + @('push','--set-upstream','origin',"HEAD:refs/heads/$branch"))
    Write-Host "SUCCESS: https://github.com/upmppu-lab/PetTown3D/tree/$branch"
    Write-Host "Scratch checkout retained at: $scratch"
} catch {
    Write-Host ("FAILED: " + $_.Exception.Message) -ForegroundColor Red
    Write-Host 'Source project was not modified. No force-push was used.'
    exit 1
}
