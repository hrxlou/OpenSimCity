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
if ($LASTEXITCODE -ne 0) {
    Write-Error "Windows 빌드 실패 (exit $LASTEXITCODE). 로그: $LogFile"
    exit $LASTEXITCODE
}

Write-Host "Windows build succeeded: $OutputPath"
