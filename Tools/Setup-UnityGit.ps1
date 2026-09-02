[CmdletBinding()]
param(
    [string]$UnityEditorPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$gitRoot = (& git -C $projectRoot rev-parse --show-toplevel 2>$null).Trim()

if (-not $gitRoot) {
    throw "Not a Git repository: $projectRoot"
}

$projectVersionFile = Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt'
$versionLine = Select-String -LiteralPath $projectVersionFile -Pattern '^m_EditorVersion:\s*(.+)$'

if (-not $versionLine) {
    throw "Cannot read the Unity version from $projectVersionFile"
}

$unityVersion = $versionLine.Matches[0].Groups[1].Value.Trim()
$candidates = [System.Collections.Generic.List[string]]::new()

if ($UnityEditorPath) {
    $candidates.Add($UnityEditorPath)
}

if ($env:UNITY_EDITOR_PATH) {
    $candidates.Add($env:UNITY_EDITOR_PATH)
}

$candidates.Add((Join-Path (Split-Path $projectRoot -Parent) "$unityVersion\Editor\Unity.exe"))
$candidates.Add((Join-Path $env:ProgramFiles "Unity\Hub\Editor\$unityVersion\Editor\Unity.exe"))

$unityPath = $candidates |
    Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) } |
    Select-Object -First 1

if (-not $unityPath) {
    throw "Unity $unityVersion was not found. Pass -UnityEditorPath or set UNITY_EDITOR_PATH."
}

$yamlMergePath = Join-Path (Split-Path $unityPath -Parent) 'Data\Tools\UnityYAMLMerge.exe'

if (-not (Test-Path -LiteralPath $yamlMergePath -PathType Leaf)) {
    throw "UnityYAMLMerge.exe was not found next to the selected Unity Editor: $yamlMergePath"
}

& git lfs version *> $null
if ($LASTEXITCODE -ne 0) {
    throw 'Git LFS is required but is not installed or not available on PATH.'
}

& git -C $gitRoot lfs install --local
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to initialize Git LFS for this repository.'
}

$portableMergePath = $yamlMergePath.Replace('\', '/')
$mergeDriver = "'$portableMergePath' merge -p %O %B %A %A"

& git -C $gitRoot config --local merge.unityyamlmerge.name 'Unity Smart Merge'
& git -C $gitRoot config --local merge.unityyamlmerge.driver $mergeDriver
& git -C $gitRoot config --local merge.unityyamlmerge.recursive binary
& git -C $gitRoot config --local core.quotepath false

if ($LASTEXITCODE -ne 0) {
    throw 'Failed to configure Unity Smart Merge.'
}

$configuredDriver = & git -C $gitRoot config --local --get merge.unityyamlmerge.driver

Write-Host "Repository: $gitRoot"
Write-Host "Unity:      $unityPath"
Write-Host "SmartMerge: $configuredDriver"
Write-Host 'Git LFS, Unity Smart Merge, and readable Unicode paths are configured for this repository.'
