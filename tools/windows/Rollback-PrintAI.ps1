[CmdletBinding()]
param(
    [string]$InstallRoot = (Join-Path $env:LOCALAPPDATA "Programs\PrintAI"),
    [switch]$NoShortcuts,
    [switch]$NoLaunch
)

$ErrorActionPreference = "Stop"

function Set-PrintAIShortcuts {
    param([string]$Executable)

    $shell = New-Object -ComObject WScript.Shell
    $programs = [Environment]::GetFolderPath("Programs")
    $desktop = [Environment]::GetFolderPath("Desktop")

    foreach ($path in @(
        (Join-Path $programs "Print AI.lnk"),
        (Join-Path $desktop "Print AI.lnk")
    )) {
        $shortcut = $shell.CreateShortcut($path)
        $shortcut.TargetPath = $Executable
        $shortcut.WorkingDirectory = Split-Path $Executable
        $shortcut.Description = "Print AI"
        $shortcut.Save()
    }
}

$installFull = [IO.Path]::GetFullPath($InstallRoot).TrimEnd("\")
$previous = "$installFull.previous"
$swap = "$installFull.rollback-swap"

if (-not (Test-Path $previous)) {
    throw "No previous Print AI installation is available for rollback."
}

if (-not (Test-Path (Join-Path $previous "PrintAI.exe"))) {
    throw "Previous installation is incomplete; PrintAI.exe is missing."
}

Remove-Item $swap -Recurse -Force -ErrorAction SilentlyContinue

if (Test-Path $installFull) {
    Move-Item $installFull $swap
}

try {
    Move-Item $previous $installFull

    if (Test-Path $swap) {
        Move-Item $swap $previous
    }
}
catch {
    if (-not (Test-Path $installFull) -and (Test-Path $swap)) {
        Move-Item $swap $installFull
    }

    throw
}

$installedExe = Join-Path $installFull "PrintAI.exe"

if (-not $NoShortcuts) {
    Set-PrintAIShortcuts -Executable $installedExe
}

Write-Host "Print AI rolled back. Current install: $installFull"

if (-not $NoLaunch) {
    Start-Process $installedExe
}
