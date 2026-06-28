# Copies the latest SprinkSnap Revit add-in build output into the Revit 2027 add-ins folder.
# Run after building SprinkSnap.Revit (Debug or Release). Close Revit before deploying.

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [string]$RevitAddinsDir = "$env:APPDATA\Autodesk\Revit\Addins\2027"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$buildDir = Join-Path $repoRoot "SprinkSnap.Revit\bin\$Configuration\net10.0-windows"
$addinManifest = Join-Path $repoRoot "SprinkSnap.Addin\SprinkSnapAI.addin"

if (-not (Test-Path $buildDir)) {
    throw "Build output not found: $buildDir. Build SprinkSnap.Revit first (Configuration=$Configuration)."
}

if (-not (Test-Path (Join-Path $buildDir "SprinkSnap.Revit.dll"))) {
    throw "SprinkSnap.Revit.dll missing in $buildDir. Rebuild the solution on Windows with Revit 2027 API available."
}

if (-not (Test-Path (Join-Path $buildDir "SprinkSnap.UI.dll"))) {
    throw "SprinkSnap.UI.dll missing in $buildDir. Logo assets are embedded in SprinkSnap.UI.dll — rebuild SprinkSnap.UI before deploying."
}

New-Item -ItemType Directory -Force -Path $RevitAddinsDir | Out-Null

Get-ChildItem -Path $buildDir -Filter "*.dll" | Copy-Item -Destination $RevitAddinsDir -Force
if (Test-Path (Join-Path $buildDir "Data")) {
    Copy-Item -Path (Join-Path $buildDir "Data") -Destination $RevitAddinsDir -Recurse -Force
}

Copy-Item -Path $addinManifest -Destination $RevitAddinsDir -Force

$uiDll = Get-Item (Join-Path $RevitAddinsDir "SprinkSnap.UI.dll")
Write-Host "Deployed SprinkSnap add-in to $RevitAddinsDir"
Write-Host "SprinkSnap.UI.dll size: $($uiDll.Length) bytes, updated: $($uiDll.LastWriteTime)"
Write-Host "Restart Revit completely to refresh ribbon icons and the dockable pane logo."
