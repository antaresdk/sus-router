# Install git hooks for sus-router (one-time setup)
# Run from repo root: .\scripts~\install-hooks.ps1
# Folder is `scripts~` so Unity AssetDatabase ignores git-hook tooling.

$ErrorActionPreference = "Stop"
$repoRoot = git rev-parse --show-toplevel 2>$null
if (-not $repoRoot) {
    Write-Host "ERROR: Not inside a git repo" -ForegroundColor Red
    exit 1
}

$hooksDir = "$repoRoot\.git\hooks"
$scriptDir = Join-Path $repoRoot "scripts~"

if (-not (Test-Path $scriptDir)) {
    Write-Host "ERROR: scripts~/ directory not found in $repoRoot" -ForegroundColor Red
    exit 1
}

function Install-Hook {
    param($hookName, $scriptName)

    $hookSource = Join-Path $scriptDir $scriptName
    $hookDest   = Join-Path $hooksDir $hookName

    if (-not (Test-Path $hookSource)) {
        Write-Host "ERROR: $scriptName not found in scripts~/" -ForegroundColor Red
        exit 1
    }

    $wrapper = @"
#!/bin/sh
# Git $hookName hook — runs $scriptName from scripts~/
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$hookSource"
exit `$?
"@

    Set-Content -Path $hookDest -Value $wrapper -Encoding ASCII -NoNewline

    if ($IsLinux -or $IsMacOS) {
        chmod +x $hookDest
    }
}

Install-Hook "pre-commit" "pre-commit.ps1"

# pre-push is bash (HARD version bump) — copy as-is for Git for Windows
$prePushSrc = Join-Path $scriptDir "pre-push"
$prePushDst = Join-Path $hooksDir "pre-push"
if (-not (Test-Path $prePushSrc)) {
    Write-Host "ERROR: scripts~/pre-push not found" -ForegroundColor Red
    exit 1
}
$text = [System.IO.File]::ReadAllText($prePushSrc) -replace "`r`n", "`n" -replace "`r", "`n"
[System.IO.File]::WriteAllText($prePushDst, $text, (New-Object System.Text.UTF8Encoding $false))

Write-Host ""
Write-Host "Git hooks installed:" -ForegroundColor Green
Write-Host "  pre-commit  ->  scripts~/pre-commit.ps1  (auto-generate .meta)" -ForegroundColor Green
Write-Host "  pre-push    ->  scripts~/pre-push       (HARD version bump: new > old)" -ForegroundColor Green
Write-Host ""
Write-Host "To uninstall: rm .git/hooks/pre-commit .git/hooks/pre-push" -ForegroundColor DarkGray
