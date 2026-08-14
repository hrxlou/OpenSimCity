#requires -Version 5.1
<#
.SYNOPSIS
    Windows에서 P9 release profile 기반 StandaloneWindows64 빌드를 만든다.
    BuildScript가 build result와 release evidence JSON을 기록한다.
#>
[CmdletBinding()]
param(
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$VersionLine = Select-String -Path (Join-Path $RepoRoot 'ProjectSettings\ProjectVersion.txt') -Pattern 'm_EditorVersion:' | Select-Object -First 1
if (-not $VersionLine) {
    Write-Error 'ProjectVersion.txt에서 Unity 버전을 읽지 못했다.'
    exit 1
}
$UnityVersion = ($VersionLine.Line -split '\s+')[1]

$DefaultUnity = "C:\Program Files\Unity\Hub\Editor\$UnityVersion\Editor\Unity.exe"
$UnityEditor = if ($env:UNITY_EDITOR) { $env:UNITY_EDITOR } else { $DefaultUnity }

if (-not (Test-Path $UnityEditor)) {
    Write-Error "Unity Editor를 찾지 못했다: $UnityEditor`nUNITY_EDITOR 환경변수에 실행 파일 경로를 지정한다."
    exit 1
}

if (-not $OutputPath) {
    $OutputPath = Join-Path $RepoRoot 'build\StandaloneWindows64\OpenSimCity.exe'
}

$SourceCommit = (& git -C $RepoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or -not $SourceCommit) {
    Write-Error 'release build source commit을 읽지 못했다.'
    exit 1
}
$StatusRows = @(& git -C $RepoRoot status --porcelain --untracked-files=all)
if ($LASTEXITCODE -ne 0) {
    Write-Error 'git clean-checkout 상태를 읽지 못했다.'
    exit 1
}
$CleanCheckout = if ($StatusRows.Count -eq 0) { '1' } else { '0' }

$LogDir = Join-Path $RepoRoot 'TestResults'
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
$LogFile = Join-Path $LogDir 'build-windows.log'
$ResultFile = Join-Path $LogDir 'build-windows.result'
$EvidenceFile = Join-Path $LogDir 'build-windows.release-evidence.json'
if (Test-Path $LogFile) { Remove-Item $LogFile -Force }
if (Test-Path $ResultFile) { Remove-Item $ResultFile -Force }
if (Test-Path $EvidenceFile) { Remove-Item $EvidenceFile -Force }

$unityArgs = @(
    '-batchmode'
    '-quit'
    '-nographics'
    '-projectPath', $RepoRoot
    '-buildTarget', 'Win64'
    '-executeMethod', 'OpenSimCity.Editor.BuildScript.PerformWindowsBuild'
    '-osc-buildPath', $OutputPath
    '-osc-buildResultPath', $ResultFile
    '-osc-buildEvidencePath', $EvidenceFile
    '-osc-sourceCommit', $SourceCommit
    '-osc-cleanCheckout', $CleanCheckout
    '-logFile', $LogFile
)

Write-Host "Building StandaloneWindows64 -> $OutputPath"
Write-Host "Release source=$SourceCommit cleanCheckout=$CleanCheckout"
& $UnityEditor @unityArgs
$unityExitCode = $LASTEXITCODE

$timeoutSeconds = 1800
$elapsed = 0
while (-not (Test-Path $ResultFile) -and $elapsed -lt $timeoutSeconds) {
    Start-Sleep -Seconds 2
    $elapsed += 2
}

if (-not (Test-Path $ResultFile)) {
    Write-Error "Windows 빌드 결과 파일이 ${timeoutSeconds}초 안에 생성되지 않았다 (Unity exit code=$unityExitCode). 로그: $LogFile"
    exit 1
}

$outcome = (Get-Content -Path $ResultFile -TotalCount 1 -ErrorAction Stop).Trim()
if ($outcome -eq 'Succeeded' -and (Test-Path $OutputPath) -and (Test-Path $EvidenceFile)) {
    Write-Host "Windows build succeeded: $OutputPath"
    Write-Host "Release evidence: $EvidenceFile"
} else {
    Write-Error "Windows 빌드 실패 (result=$outcome, Unity exit code=$unityExitCode). 로그: $LogFile, 결과: $ResultFile, evidence: $EvidenceFile"
    exit 1
}
