#requires -Version 5.1
<#
.SYNOPSIS
    Windows에서 Unity EditMode/PlayMode 테스트를 실행한다.
    Tools/CI/run-unity-tests.sh(macOS/Linux)와 동일한 인터페이스를 제공해
    두 기기에서 같은 명령으로 테스트할 수 있게 한다.

.EXAMPLE
    ./Tools/CI/run-unity-tests.ps1 edit
    ./Tools/CI/run-unity-tests.ps1 play
    ./Tools/CI/run-unity-tests.ps1 all
#>
[CmdletBinding()]
param(
    [ValidateSet('edit', 'play', 'all')]
    [string]$Mode = 'all'
)

$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$VersionLine = Select-String -Path (Join-Path $RepoRoot 'ProjectSettings\ProjectVersion.txt') -Pattern 'm_EditorVersion:' | Select-Object -First 1
if (-not $VersionLine) {
    Write-Error 'ProjectVersion.txt에서 Unity 버전을 읽지 못했다.'
    exit 1
}
$UnityVersion = ($VersionLine.Line -split '\s+')[1]

# UNITY_EDITOR 환경변수로 재정의할 수 있고, 없으면 Unity Hub 기본 경로를 사용한다.
$DefaultUnity = "C:\Program Files\Unity\Hub\Editor\$UnityVersion\Editor\Unity.exe"
$UnityEditor = if ($env:UNITY_EDITOR) { $env:UNITY_EDITOR } else { $DefaultUnity }

if (-not (Test-Path $UnityEditor)) {
    Write-Error "Unity Editor를 찾지 못했다: $UnityEditor`nUNITY_EDITOR 환경변수에 실행 파일 경로를 지정한다."
    exit 1
}

$ResultsDir = Join-Path $RepoRoot 'TestResults'
New-Item -ItemType Directory -Force -Path $ResultsDir | Out-Null

function Invoke-UnityTests {
    param(
        [string]$Platform,
        [string]$Slug
    )

    $unityArgs = @(
        '-batchmode'
        '-nographics'
        '-projectPath', $RepoRoot
        '-runTests'
        '-testPlatform', $Platform
        '-testResults', (Join-Path $ResultsDir "$Slug.xml")
        '-logFile', (Join-Path $ResultsDir "$Slug.log")
    )

    Write-Host "Running $Platform tests..."
    & $UnityEditor @unityArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "$Platform 테스트 실패 (exit $LASTEXITCODE). 로그: $ResultsDir\$Slug.log"
        exit $LASTEXITCODE
    }
}

switch ($Mode) {
    'edit' { Invoke-UnityTests -Platform 'EditMode' -Slug 'editmode' }
    'play' { Invoke-UnityTests -Platform 'PlayMode' -Slug 'playmode' }
    'all' {
        Invoke-UnityTests -Platform 'EditMode' -Slug 'editmode'
        Invoke-UnityTests -Platform 'PlayMode' -Slug 'playmode'
    }
}

Write-Host "Unity tests passed. Results: $ResultsDir"
