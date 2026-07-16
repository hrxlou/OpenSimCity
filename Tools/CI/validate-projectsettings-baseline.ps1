#requires -Version 5.1

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$ExpectedQualityValues = @('0', '0', '0', '0', '2', '2')

function Get-IndexedFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $gitSpec = ":$RelativePath"
    $content = & git -C $RepoRoot show $gitSpec 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Git index does not contain $RelativePath."
    }

    return ($content -join [Environment]::NewLine)
}

function Get-IndexedPropertyValues {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Content,
        [Parameter(Mandatory = $true)]
        [string]$Property
    )

    $pattern = "(?m)^\s*{0}:\s*(\d+)\s*$" -f [regex]::Escape($Property)
    return @([regex]::Matches($Content, $pattern) | ForEach-Object { $_.Groups[1].Value })
}

$graphicsSettings = Get-IndexedFile -RelativePath 'ProjectSettings/GraphicsSettings.asset'
$qualitySettings = Get-IndexedFile -RelativePath 'ProjectSettings/QualitySettings.asset'

$graphicsValues = @(Get-IndexedPropertyValues -Content $graphicsSettings -Property 'm_LightsUseColorTemperature')
if ($graphicsValues.Count -ne 1) {
    throw "Expected exactly one m_LightsUseColorTemperature entry in the Git index, found $($graphicsValues.Count)."
}
if ($graphicsValues[0] -ne '0') {
    throw "GraphicsSettings.asset in the Git index has m_LightsUseColorTemperature=$($graphicsValues[0]); expected 0."
}

$qualityValues = @(Get-IndexedPropertyValues -Content $qualitySettings -Property 'antiAliasing')
if ($qualityValues.Count -ne $ExpectedQualityValues.Count) {
    throw "Expected $($ExpectedQualityValues.Count) antiAliasing entries in the Git index, found $($qualityValues.Count)."
}

for ($index = 0; $index -lt $ExpectedQualityValues.Count; $index++) {
    if ($qualityValues[$index] -ne $ExpectedQualityValues[$index]) {
        throw "QualitySettings.asset in the Git index has antiAliasing values [$($qualityValues -join ', ')]; expected [$($ExpectedQualityValues -join ', ')] (first mismatch at index $index)."
    }
}

Write-Host 'Committed ProjectSettings baseline passed (Git index; unstaged Unity drift ignored).'
