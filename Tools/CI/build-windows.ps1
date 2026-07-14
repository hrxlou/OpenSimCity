#requires -Version 5.1
<#
.SYNOPSIS
    로컬 Windows에서 StandaloneWindows64 빌드를 만든다.
    CI(build-windows.yml)와 동일한 진입점(OpenSimCity.Editor.BuildScript.PerformWindowsBuild)을
    호출해 로컬 빌드와 CI 빌드가 같은 방식으로 재현되게 한다.

.EXAMPLE
    ./Tools/CI/build-windows.ps1
    ./Tools/CI/build-windows.ps1 -OutputPath D:\out\OpenSimCity.exe
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

$LogDir = Join-Path $RepoRoot 'TestResults'
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
$LogFile = Join-Path $LogDir 'build-windows.log'
$ResultFile = Join-Path $LogDir 'build-windows.result'
if (Test-Path $LogFile) { Remove-Item $LogFile -Force }
if (Test-Path $ResultFile) { Remove-Item $ResultFile -Force }

$unityArgs = @(
    '-batchmode'
    '-quit'
    '-nographics'
    '-projectPath', $RepoRoot
    '-buildTarget', 'Win64'
    '-executeMethod', 'OpenSimCity.Editor.BuildScript.PerformWindowsBuild'
    '-osc-buildPath', $OutputPath
    '-osc-buildResultPath', $ResultFile
    '-logFile', $LogFile
)

Write-Host "Building StandaloneWindows64 -> $OutputPath"
& $UnityEditor @unityArgs
$unityExitCode = $LASTEXITCODE

# Unity는 첫 프로세스가 내부적으로 재실행(relaunch)하며 원래 프로세스 핸들을
# 먼저 종료하는 경우가 있어, 이 프로세스의 종료 코드는 신뢰할 수 없다.
# 대신 BuildScript.cs가 BuildReport에서 만든 machine-readable 결과 파일을 기다린다.
# 로그 문구는 사람이 읽는 진단용이며 성공 판정 계약으로 사용하지 않는다.
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
if ($outcome -eq 'Succeeded' -and (Test-Path $OutputPath)) {
    Write-Host "Windows build succeeded: $OutputPath"
} else {
    Write-Error "Windows 빌드 실패 (result=$outcome, Unity exit code=$unityExitCode). 로그: $LogFile, 결과: $ResultFile"
    exit 1
}
