[CmdletBinding()]
param(
    [string]$OutputDirectory = "Build/Windows",
    [switch]$PackageOnly,
    [string]$ArchivePath = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$editorPath = $env:UNITY_EDITOR_PATH
$modulePath = if ($editorPath) { Join-Path (Split-Path $editorPath -Parent) "Data\PlaybackEngines\WindowsStandaloneSupport" } else { "" }

if (-not $PackageOnly) {
    if (-not $editorPath -or -not (Test-Path -LiteralPath $editorPath -PathType Leaf)) {
        throw "Unity Editor 6000.0.64f1 was not found. Set UNITY_EDITOR_PATH to the full path of Unity.exe on this Windows runner."
    }
    if (-not (Test-Path -LiteralPath $modulePath -PathType Container)) {
        throw "Unity's Windows Standalone module is missing at '$modulePath'. Install the Windows Build Support module for Unity 6000.0.64f1."
    }
    if ($env:UNITY_LICENSE) {
        $licenseDirectory = Join-Path $env:ProgramData "Unity"
        New-Item -ItemType Directory -Force -Path $licenseDirectory | Out-Null
        [System.IO.File]::WriteAllText((Join-Path $licenseDirectory "Unity_lic.ulf"), $env:UNITY_LICENSE)
    }

    $outputPath = Join-Path $projectRoot $OutputDirectory
    New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
    $logPath = Join-Path $env:TEMP ("frutiger-unity-build-" + [guid]::NewGuid().ToString("N") + ".log")
    $previousLocation = Get-Location
    try {
        Set-Location $projectRoot
        & $editorPath -batchmode -nographics -quit -projectPath $projectRoot -executeMethod FrutigerBadminton.Editor.WindowsBuild.BuildWindows -logFile $logPath
        if ($LASTEXITCODE -ne 0) {
            throw "Unity batch build failed with exit code $LASTEXITCODE. See '$logPath' on the runner for diagnostics."
        }
    }
    finally {
        Set-Location $previousLocation
    }
}

$playerDirectory = Join-Path (Join-Path $projectRoot $OutputDirectory) "FrutigerBadminton_Data"
$playerExecutable = Join-Path (Join-Path $projectRoot $OutputDirectory) "FrutigerBadminton.exe"
if (-not (Test-Path -LiteralPath $playerExecutable -PathType Leaf) -or -not (Test-Path -LiteralPath $playerDirectory -PathType Container)) {
    throw "The complete Windows player folder was not produced (expected FrutigerBadminton.exe and FrutigerBadminton_Data)."
}

if (-not $ArchivePath) {
    $branch = if ($env:GITHUB_REF_NAME) { $env:GITHUB_REF_NAME -replace '[^A-Za-z0-9._-]', '-' } else { "local" }
    $sha = if ($env:GITHUB_SHA) { $env:GITHUB_SHA } else { "local" }
    $ArchivePath = Join-Path $projectRoot "artifacts\windows-$branch-$sha.zip"
}
$archiveFullPath = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $ArchivePath))
New-Item -ItemType Directory -Force -Path (Split-Path $archiveFullPath -Parent) | Out-Null
if (Test-Path -LiteralPath $archiveFullPath) { Remove-Item -LiteralPath $archiveFullPath -Force }
Compress-Archive -Path (Join-Path (Join-Path $projectRoot $OutputDirectory) "*") -DestinationPath $archiveFullPath -CompressionLevel Optimal
if ((Get-Item -LiteralPath $archiveFullPath).Length -le 0) { throw "The Windows player archive is empty." }
Write-Output "Portable Windows player archive created: $archiveFullPath"
