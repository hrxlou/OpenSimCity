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
$TimeoutSeconds = 1800
if ($env:UNITY_TEST_TIMEOUT_SECONDS) {
    $parsedTimeout = 0
    if (-not [int]::TryParse($env:UNITY_TEST_TIMEOUT_SECONDS, [ref]$parsedTimeout) -or $parsedTimeout -le 0) {
        Write-Error "UNITY_TEST_TIMEOUT_SECONDS는 양의 정수여야 한다: $($env:UNITY_TEST_TIMEOUT_SECONDS)"
        exit 2
    }
    $TimeoutSeconds = $parsedTimeout
}

$BaselineGuard = Join-Path $PSScriptRoot 'validate-projectsettings-baseline.ps1'
if (-not (Test-Path $BaselineGuard)) {
    Write-Error "ProjectSettings baseline guard를 찾지 못했다: $BaselineGuard"
    exit 1
}
& $BaselineGuard

function Wait-ForUnityLockRelease {
    param(
        [int]$TimeoutSeconds = 60
    )

    # Unity's batchmode process can relaunch internally and exit its original
    # handle before the relaunched process finishes, so the results XML can
    # appear slightly before the project's lock is actually released. Running
    # the next test invocation ('all' mode) before that happens fails with
    # "Multiple Unity instances cannot open the same project."
    $lockPath = Join-Path $RepoRoot 'Temp\UnityLockfile'
    $elapsed = 0
    while ((Test-Path $lockPath) -and $elapsed -lt $TimeoutSeconds) {
        Start-Sleep -Seconds 1
        $elapsed += 1
    }

    if (Test-Path $lockPath) {
        Write-Error "Unity 프로젝트 lock이 ${TimeoutSeconds}초 안에 해제되지 않았다: $lockPath"
    }
}

# Unity가 결과 XML을 만들지 못하고 batchmode를 중단할 때 로그에 남기는 신호들.
# 이 경우 XML은 영영 나타나지 않으므로 타임아웃까지 기다리지 말고 즉시 실패해야 한다.
$FatalErrorSignatures = @(
    'Aborting batchmode due to fatal error',
    'Multiple Unity instances cannot open the same project',
    'It looks like another Unity instance is running',
    'No valid Unity Editor license found',
    'Failed to activate/update license',
    'Scripts have compiler errors'
)

function Find-UnityFatalError {
    param(
        [string]$LogPath
    )

    if (-not (Test-Path $LogPath)) {
        return $null
    }

    $content = Get-Content -Path $LogPath -Raw -ErrorAction SilentlyContinue
    if (-not $content) {
        return $null
    }

    foreach ($signature in $FatalErrorSignatures) {
        if ($content -like "*$signature*") {
            return $signature
        }
    }

    return $null
}

function Invoke-UnityTests {
    param(
        [string]$Platform,
        [string]$Slug
    )

    $resultsPath = Join-Path $ResultsDir "$Slug.xml"
    $logPath = Join-Path $ResultsDir "$Slug.log"
    if (Test-Path $resultsPath) { Remove-Item $resultsPath -Force }
    if (Test-Path $logPath) { Remove-Item $logPath -Force }

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
    # 단, Unity가 fatal error로 중단하면 XML은 영영 생기지 않으므로, 그 사이에
    # 로그의 fatal error 신호를 감지하면 타임아웃을 기다리지 않고 즉시 실패시킨다.
    $elapsed = 0
    while (-not (Test-Path $resultsPath) -and $elapsed -lt $TimeoutSeconds) {
        $fatalError = Find-UnityFatalError -LogPath $logPath
        if ($fatalError) {
            Write-Error "$Platform 테스트 실패: Unity가 fatal error로 중단했다: '$fatalError'. 로그: $logPath"
            exit 1
        }

        Start-Sleep -Seconds 2
        $elapsed += 2
    }

    if (-not (Test-Path $resultsPath)) {
        # 마지막으로 한 번 더 fatal error를 확인해 원인을 구체적으로 보고한다.
        $fatalError = Find-UnityFatalError -LogPath $logPath
        if ($fatalError) {
            Write-Error "$Platform 테스트 실패: Unity가 fatal error로 중단했다: '$fatalError'. 로그: $logPath"
        }
        else {
            Write-Error "$Platform 테스트 실패: ${TimeoutSeconds}초 내에 결과 파일이 생성되지 않았다. 로그: $logPath"
        }
        exit 1
    }

    try {
        [xml]$results = Get-Content -Path $resultsPath -Raw
    }
    catch {
        Write-Error "$Platform 테스트 실패: 결과 XML이 유효하지 않다. 로그: $logPath, 결과: $resultsPath"
        exit 1
    }

    $testResult = $results.'test-run'.result
    if ([string]::IsNullOrWhiteSpace($testResult)) {
        Write-Error "$Platform 테스트 실패: 결과 XML에 test-run result가 없다. 로그: $logPath, 결과: $resultsPath"
        exit 1
    }

    $total = 0
    $passed = 0
    $failed = 0
    $inconclusive = 0
    $skipped = 0
    $totalText = [string]$results.'test-run'.total
    if (-not [int]::TryParse($totalText, [ref]$total) -or $total -le 0 -or
        -not [int]::TryParse([string]$results.'test-run'.passed, [ref]$passed) -or $passed -le 0 -or
        -not [int]::TryParse([string]$results.'test-run'.failed, [ref]$failed) -or
        -not [int]::TryParse([string]$results.'test-run'.inconclusive, [ref]$inconclusive) -or
        -not [int]::TryParse([string]$results.'test-run'.skipped, [ref]$skipped)) {
        Write-Error "$Platform 테스트 실패: 결과 수치가 없거나 유효하지 않다. 로그: $logPath, 결과: $resultsPath"
        exit 1
    }

    if ($failed -ne 0 -or $inconclusive -ne 0 -or ($passed + $skipped) -ne $total -or
        $testResult -notin @('Passed', 'Skipped:Ignored')) {
        Write-Error "$Platform 테스트 실패 (result=$testResult, passed=$passed, failed=$failed, inconclusive=$inconclusive, skipped=$skipped). 로그: $logPath, 결과: $resultsPath"
        exit 1
    }

    Write-Host "$Platform tests passed ($passed passed, $skipped ignored, $total total)."
}

switch ($Mode) {
    'edit' { Invoke-UnityTests -Platform 'EditMode' -Slug 'editmode' }
    'play' { Invoke-UnityTests -Platform 'PlayMode' -Slug 'playmode' }
    'all' {
        Invoke-UnityTests -Platform 'EditMode' -Slug 'editmode'
        # PlayMode 실행 전에만 lock 해제를 기다린다. 마지막 실행 뒤에는
        # 대기가 불필요하고, lock이 늦게 풀리면 통과한 실행을 실패로 뒤집을 수 있다.
        Wait-ForUnityLockRelease
        Invoke-UnityTests -Platform 'PlayMode' -Slug 'playmode'
    }
}

Write-Host "Unity tests passed. Results: $ResultsDir"
