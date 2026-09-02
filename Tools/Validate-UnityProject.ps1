[CmdletBinding()]
param(
    [string]$UnityEditorPath,
    [switch]$RunTests,
    [switch]$ResolveOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
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

Write-Host "Project: $projectRoot"
Write-Host "Unity:   $unityPath"

if ($ResolveOnly) {
    return
}

$lockFile = Join-Path $projectRoot 'Temp\UnityLockfile'
if (Test-Path -LiteralPath $lockFile) {
    throw 'This project is open in Unity. Close that Editor before running headless validation.'
}

$logsDirectory = Join-Path $projectRoot 'Logs'
New-Item -ItemType Directory -Path $logsDirectory -Force *> $null

$compileLog = Join-Path $logsDirectory 'Validation-Compile.log'
$compileArguments = @(
    '-batchmode',
    '-quit',
    '-projectPath', $projectRoot,
    '-logFile', $compileLog
)

Write-Host 'Running Unity import and compilation validation...'
& $unityPath @compileArguments
$compileExitCode = $LASTEXITCODE

$compileFailure = Select-String -LiteralPath $compileLog -Pattern @(
    'error CS\d{4}',
    'Scripts have compiler errors',
    'Compilation failed',
    'Aborting batchmode due to failure'
) -Quiet

if ($compileExitCode -ne 0 -or $compileFailure) {
    throw "Unity compilation validation failed. See $compileLog"
}

Write-Host 'Unity import and compilation validation passed.'

if (-not $RunTests) {
    return
}

$testLog = Join-Path $logsDirectory 'Validation-EditMode.log'
$testResults = Join-Path $logsDirectory 'EditModeResults.xml'
$testArguments = @(
    '-batchmode',
    '-projectPath', $projectRoot,
    '-runTests',
    '-testPlatform', 'EditMode',
    '-testResults', $testResults,
    '-logFile', $testLog
)

Write-Host 'Running EditMode tests...'
& $unityPath @testArguments

if ($LASTEXITCODE -ne 0) {
    throw "EditMode tests failed. See $testLog and $testResults"
}

Write-Host "EditMode tests passed. Results: $testResults"

