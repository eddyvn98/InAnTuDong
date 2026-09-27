[CmdletBinding()]
param(
    [string]$InstallRoot = (Join-Path $env:LOCALAPPDATA "Programs\PrintAI"),
    [switch]$NoShortcuts,
    [switch]$RemoveData
)

$ErrorActionPreference = "Stop"

function Remove-PrintAIShortcuts {
    $programs = [Environment]::GetFolderPath("Programs")
    $desktop = [Environment]::GetFolderPath("Desktop")

    foreach ($path in @(
        (Join-Path $programs "Print AI.lnk"),
        (Join-Path $desktop "Print AI.lnk")
    )) {
        Remove-Item $path -Force -ErrorAction SilentlyContinue
    }
}

$installFull = [IO.Path]::GetFullPath($InstallRoot).TrimEnd("\")

foreach ($path in @(
    $installFull,
    "$installFull.previous",
    "$installFull.new",
    "$installFull.rollback-swap"
)) {
    Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue
}

if (-not $NoShortcuts) {
    Remove-PrintAIShortcuts
}

if ($RemoveData) {
    $dataRoot = Join-Path $env:LOCALAPPDATA "PrintAI"
    Remove-Item $dataRoot -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Print AI application and local data removed."
}
else {
    Write-Host "Print AI application removed. Local history/recipes/work data were kept."
}
