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
        [string]$Slug,
        [int]$TimeoutSeconds = 1800
    )

    $resultsPath = Join-Path $ResultsDir "$Slug.xml"
    $logPath = Join-Path $ResultsDir "$Slug.log"
    if (Test-Path $resultsPath) { Remove-Item $resultsPath -Force }

    $unityArgs = @(
        '-batchmode'
        '-nographics'
        '-projectPath', $RepoRoot
        '-runTests'
        '-testPlatform', $Platform
        '-testResults', $resultsPath
        '-logFile', $logPath
    )

    Write-Host "Running $Platform tests..."
    & $UnityEditor @unityArgs

    # Unity는 첫 프로세스가 내부적으로 재실행(relaunch)하며 원래 프로세스 핸들을
    # 먼저 종료하는 경우가 있어, 이 프로세스의 종료 코드는 신뢰할 수 없다.
    # 대신 실제 NUnit 결과 XML이 나타날 때까지 기다려 그 결과를 판정 기준으로 쓴다.
    $elapsed = 0
    while (-not (Test-Path $resultsPath) -and $elapsed -lt $TimeoutSeconds) {
        Start-Sleep -Seconds 2
        $elapsed += 2
    }

    if (-not (Test-Path $resultsPath)) {
        Write-Error "$Platform 테스트 실패: ${TimeoutSeconds}초 내에 결과 파일이 생성되지 않았다. 로그: $logPath"
        exit 1
    }

    [xml]$results = Get-Content $resultsPath
    $testResult = $results.'test-run'.result
    if ($testResult -ne 'Passed') {
        Write-Error "$Platform 테스트 실패 (result=$testResult). 로그: $logPath, 결과: $resultsPath"
        exit 1
    }

    Write-Host "$Platform tests passed ($($results.'test-run'.passed)/$($results.'test-run'.total))."
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
