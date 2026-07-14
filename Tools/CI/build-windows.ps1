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
if (Test-Path $LogFile) { Remove-Item $LogFile -Force }

$unityArgs = @(
    '-batchmode'
    '-quit'
    '-nographics'
    '-projectPath', $RepoRoot
    '-buildTarget', 'Win64'
    '-executeMethod', 'OpenSimCity.Editor.BuildScript.PerformWindowsBuild'
    '-osc-buildPath', $OutputPath
    '-logFile', $LogFile
)

Write-Host "Building StandaloneWindows64 -> $OutputPath"
& $UnityEditor @unityArgs

# Unity는 첫 프로세스가 내부적으로 재실행(relaunch)하며 원래 프로세스 핸들을
# 먼저 종료하는 경우가 있어, 이 프로세스의 종료 코드는 신뢰할 수 없다.
# 대신 BuildScript.cs가 로그에 남기는 성공/실패 마커가 나타날 때까지 기다린다.
$timeoutSeconds = 1800
$elapsed = 0
$successMarker = 'Windows build 성공:'
$failureMarker = 'Windows build 실패:'
$outcome = $null
while ($elapsed -lt $timeoutSeconds) {
    if (Test-Path $LogFile) {
        $content = Get-Content $LogFile -Raw -ErrorAction SilentlyContinue
        if ($content -match [regex]::Escape($successMarker)) { $outcome = 'success'; break }
        if ($content -match [regex]::Escape($failureMarker)) { $outcome = 'failure'; break }
    }
    Start-Sleep -Seconds 2
    $elapsed += 2
}

if ($outcome -eq 'success' -and (Test-Path $OutputPath)) {
    Write-Host "Windows build succeeded: $OutputPath"
} else {
    Write-Error "Windows 빌드 실패 또는 시간 초과. 로그: $LogFile"
    exit 1
}
