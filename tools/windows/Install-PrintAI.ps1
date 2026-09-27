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

$sourceRoot = $PSScriptRoot
$sourceExe = Join-Path $sourceRoot "PrintAI.exe"

if (-not (Test-Path $sourceExe)) {
    throw "PrintAI.exe was not found beside the installer."
}

$sourceFull = [IO.Path]::GetFullPath($sourceRoot).TrimEnd("\")
$installFull = [IO.Path]::GetFullPath($InstallRoot).TrimEnd("\")

if ($sourceFull.Equals($installFull, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Run Install-PrintAI.ps1 from an extracted release package, not from the install directory."
}

$parent = Split-Path $installFull
$staging = "$installFull.new"
$previous = "$installFull.previous"

New-Item -ItemType Directory -Force -Path $parent | Out-Null
Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $staging | Out-Null

try {
    Copy-Item -Path (Join-Path $sourceRoot "*") -Destination $staging -Recurse -Force

    foreach ($required in @("PrintAI.exe", "ui\index.html", "BUILD.txt")) {
        if (-not (Test-Path (Join-Path $staging $required))) {
            throw "Staged install is missing $required."
        }
    }

    Remove-Item $previous -Recurse -Force -ErrorAction SilentlyContinue

    if (Test-Path $installFull) {
        Move-Item $installFull $previous
    }

    try {
        Move-Item $staging $installFull
    }
    catch {
        if (Test-Path $previous -and -not (Test-Path $installFull)) {
            Move-Item $previous $installFull
        }

        throw
    }
}
catch {
    Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
    throw
}

$installedExe = Join-Path $installFull "PrintAI.exe"

if (-not $NoShortcuts) {
    Set-PrintAIShortcuts -Executable $installedExe
}

Write-Host "Print AI installed to: $installFull"

if (-not $NoLaunch) {
    Start-Process $installedExe
}
