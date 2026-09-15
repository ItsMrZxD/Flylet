<#
.SYNOPSIS
    Builds Flylet (Release x64), installs it on this PC under a dev identity, and starts it.

.DESCRIPTION
    Local installs use the package name MrzxD.Flylet.Dev ("Flylet (Dev)"). The Store identity in
    Package.appxmanifest (MrzxD.Flylet) won't start when registered from a build folder on a PC without
    a Store license, so it is only used for Store uploads. Needs Visual Studio 2026 and Developer Mode.

.PARAMETER NoBuild
    Install the existing build output without rebuilding.

.PARAMETER NoLaunch
    Install without starting the app.
#>
param(
    [switch]$NoBuild,
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$layout = Join-Path $repo 'Flylet.Package\bin\x64\Release'
$devName = 'MrzxD.Flylet.Dev'

# The dev build runs straight from the build output, so a running copy locks its files
Get-Process Flylet -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$layout\*" } | Stop-Process -Force

if (-not $NoBuild) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    $msbuild = & $vswhere -latest -prerelease -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\amd64\MSBuild.exe' | Select-Object -First 1
    if (-not $msbuild) { throw 'MSBuild not found. Install Visual Studio 2026 with the .NET desktop and C++ desktop workloads.' }

    # Shells opened before the .NET SDK was installed don't have it on PATH yet
    $dotnet = Join-Path $env:ProgramFiles 'dotnet'
    if ($env:PATH -notlike "*$dotnet*") { $env:PATH = "$dotnet;$env:PATH" }

    # Bundling would also build x86 and ARM64, which a local install doesn't need
    & $msbuild (Join-Path $repo 'Flylet.sln') /restore /m /nologo /v:minimal /p:Configuration=Release /p:Platform=x64 /p:AppxBundle=Never
    if ($LASTEXITCODE -ne 0) { throw "Build failed (exit code $LASTEXITCODE)." }
}

$manifestPath = Join-Path $layout 'AppxManifest.xml'
if (-not (Test-Path -LiteralPath $manifestPath)) { throw "No build output at $layout. Run without -NoBuild first." }

[xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
$manifest.Package.Identity.Name = $devName
$manifest.Package.Properties.DisplayName = 'Flylet (Dev)'
foreach ($node in $manifest.SelectNodes("//*[local-name()='VisualElements']")) { $node.SetAttribute('DisplayName', 'Flylet (Dev)') }
# Keep the dev build from starting with Windows
foreach ($node in $manifest.SelectNodes("//*[local-name()='StartupTask']")) { $node.SetAttribute('Enabled', 'false') }
$manifest.Save($manifestPath)

# The Appx cmdlets only load in Windows PowerShell 5.1
$result = powershell.exe -NoProfile -Command {
    param($path, $name)
    $ProgressPreference = 'SilentlyContinue'
    try {
        try { Add-AppxPackage -Register $path -ForceApplicationShutdown -ErrorAction Stop }
        catch { Get-AppxPackage -Name $name | Remove-AppxPackage; Add-AppxPackage -Register $path -ErrorAction Stop }
        'OK ' + (Get-AppxPackage -Name $name).PackageFamilyName
    }
    catch { 'FAILED ' + $_.Exception.Message }
} -args $manifestPath, $devName
if ($result -notlike 'OK *') { throw "Install failed: $($result -replace '^FAILED ', '')" }
$familyName = $result.Substring(3)
"Installed $devName ($familyName)"

if ($NoLaunch) { return }

# The Store version of ModernFlyouts replaces the same flyouts; close it so the two don't fight
Get-Process ModernFlyoutsHost -ErrorAction SilentlyContinue | Where-Object Path -like '*\WindowsApps\32669SamG.ModernFlyouts_*' | Stop-Process

Start-Process explorer.exe "shell:AppsFolder\$familyName!App"
$deadline = (Get-Date).AddSeconds(10)
while ((Get-Date) -lt $deadline -and -not (Get-Process Flylet -ErrorAction SilentlyContinue)) { Start-Sleep -Milliseconds 250 }
if (Get-Process Flylet -ErrorAction SilentlyContinue) { 'Flylet (Dev) is running.' }
else { Write-Warning 'Flylet (Dev) did not start within 10 seconds.' }
