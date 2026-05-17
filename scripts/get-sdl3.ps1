<#
.SYNOPSIS
    Downloads the SDL3 native binary for Windows and places it in the
    runtimes/<rid>/native/ layout TerminalNinja.Skia's MSBuild targets expect.

.DESCRIPTION
    TerminalNinja.Skia drives windowing via SDL3 P/Invoke. The native library must be
    on the dynamic loader's search path at runtime. This script downloads the official
    SDL3 release zip from libsdl-org/SDL on GitHub, extracts SDL3.dll for the requested
    runtime identifier, and places it under <output>/runtimes/<rid>/native/.

    The MSBuild targets shipped in the TerminalNinja.Skia NuGet package will then copy
    it to your app's bin/ folder during build.

.PARAMETER OutputDirectory
    Where to write the runtimes/ layout. Defaults to the current directory.

.PARAMETER Rid
    Runtime identifier. Supported: win-x64, win-x86, win-arm64. Defaults to win-x64.

.PARAMETER Version
    SDL3 version to download. Defaults to 3.2.20 (latest stable as of writing).

.EXAMPLE
    ./scripts/get-sdl3.ps1
    # Downloads SDL3.dll for win-x64 into ./runtimes/win-x64/native/SDL3.dll

.EXAMPLE
    ./scripts/get-sdl3.ps1 -OutputDirectory ../MyApp -Rid win-arm64
    # Downloads the ARM64 build into ../MyApp/runtimes/win-arm64/native/SDL3.dll
#>

[CmdletBinding()]
param(
    [string]$OutputDirectory = ".",
    [ValidateSet("win-x64", "win-x86", "win-arm64")]
    [string]$Rid = "win-x64",
    [string]$Version = "3.2.20"
)

$ErrorActionPreference = "Stop"

# SDL3 release zips ship as SDL3-<version>-win32-x64.zip / -x86.zip / -arm64.zip.
$archSuffix = switch ($Rid) {
    "win-x64"   { "x64" }
    "win-x86"   { "x86" }
    "win-arm64" { "arm64" }
}

$zipName = "SDL3-$Version-win32-$archSuffix.zip"
$url = "https://github.com/libsdl-org/SDL/releases/download/release-$Version/$zipName"
$nativeDir = Join-Path $OutputDirectory "runtimes\$Rid\native"
$dllPath = Join-Path $nativeDir "SDL3.dll"

if (Test-Path $dllPath) {
    Write-Host "SDL3.dll already exists at $dllPath. Delete to re-download." -ForegroundColor Yellow
    return
}

Write-Host "Downloading $url ..." -ForegroundColor Cyan
$tempZip = Join-Path ([System.IO.Path]::GetTempPath()) $zipName
Invoke-WebRequest -Uri $url -OutFile $tempZip

Write-Host "Extracting SDL3.dll into $nativeDir ..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $nativeDir | Out-Null
$tempExtract = Join-Path ([System.IO.Path]::GetTempPath()) "sdl3-$Version-$archSuffix"
Expand-Archive -Path $tempZip -DestinationPath $tempExtract -Force

$dll = Get-ChildItem -Path $tempExtract -Filter "SDL3.dll" -Recurse | Select-Object -First 1
if ($null -eq $dll) {
    throw "SDL3.dll not found in the extracted archive. The release layout may have changed; check https://github.com/libsdl-org/SDL/releases/tag/release-$Version"
}

Copy-Item $dll.FullName $dllPath -Force
Remove-Item $tempZip -Force
Remove-Item $tempExtract -Recurse -Force

Write-Host "Done. SDL3.dll is at $dllPath" -ForegroundColor Green
Write-Host "TerminalNinja.Skia's MSBuild targets will pick it up on the next build."
