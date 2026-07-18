# pre-commit hook — auto-generate missing .meta files
# Scans staged files/dirs and generates .meta with deterministic GUIDs (MD5 of path).
#
# Never generate folder .meta for Unity-hidden "~" paths (Documentation~, Samples~, …)
# — those spam the Editor console ("meta exists but folder can't be found").

$ErrorActionPreference = "SilentlyContinue"

function Test-UnityHiddenTildePath {
    param([string]$RelativePath)
    return ($RelativePath -match '(^|/)[^/]+~(/|$)') -or ($RelativePath -match '~$')
}

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$staged = git diff --cached --name-only 2>$null
if (-not $staged) { exit 0 }

$allFiles = git ls-files 2>$null
$knownMetas = @{}
foreach ($f in $allFiles) {
    if ($f -match '\.meta$') {
        $knownMetas[$f -replace '\.meta$',''] = $true
    }
}

function New-DeterministicGuid {
    param($relativePath)
    $normalized = $relativePath.Replace('\','/').TrimStart('/')
    $bytes = [System.Security.Cryptography.MD5]::Create().ComputeHash(
        [System.Text.Encoding]::UTF8.GetBytes($normalized)
    )
    return [Guid]::new($bytes).ToString("N")
}

$folderMeta = @'
fileFormatVersion: 2
guid: {0}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
'@

$asmdefMeta = @'
fileFormatVersion: 2
guid: {0}
AssemblyDefinitionImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
'@

$fileMeta = @'
fileFormatVersion: 2
guid: {0}
'@

function Get-MetaTemplate {
    param($path)
    if ($path -match '\.asmdef$') { return $asmdefMeta }
    return $fileMeta
}

function Write-And-Stage {
    param($path, $content)
    $dir = Split-Path $path -Parent
    if ($dir -and -not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    [System.IO.File]::WriteAllText($path, $content, [System.Text.Encoding]::ASCII)
    git add $path 2>$null
    Write-Host "  [meta] auto-generated: $path" -ForegroundColor DarkGray
}

# 1. Staged files need .meta
foreach ($f in $staged) {
    $f = $f.Replace('\','/')
    if ($f -match '\.meta$' -or $f -match '^\.git' -or $f -eq '.gitignore') { continue }
    if (Test-UnityHiddenTildePath $f) { continue }
    if (-not $knownMetas.ContainsKey($f)) {
        $metaPath = "$f.meta"
        $guid = New-DeterministicGuid $f
        $template = Get-MetaTemplate $f
        Write-And-Stage $metaPath ($template -f $guid)
        $knownMetas[$f] = $true
    }
}

# 2. Parent directories need .meta
foreach ($f in $staged) {
    $f = $f.Replace('\','/')
    if ($f -match '\.meta$') { continue }
    $dir = Split-Path $f -Parent
    while ($dir -and $dir -ne '.') {
        $dir = $dir.Replace('\','/')
        if (Test-UnityHiddenTildePath $dir) {
            $dir = Split-Path $dir -Parent
            continue
        }
        if (-not $knownMetas.ContainsKey($dir)) {
            $metaPath = "$dir.meta"
            $guid = New-DeterministicGuid $dir
            Write-And-Stage $metaPath ($folderMeta -f $guid)
            $knownMetas[$dir] = $true
        }
        $dir = Split-Path $dir -Parent
    }
}

exit 0
