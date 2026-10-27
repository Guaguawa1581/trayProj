<#
.SYNOPSIS
  Build, test and package BackendTray.

.DESCRIPTION
  1. Run unit tests (skip with -SkipTests)
  2. Publish every component (framework-dependent, win-x64) to artifacts\publish
  3. Copy the Vue build output (frontend\dist) to artifacts\publish\wwwroot
  4. Compile the Inno Setup installer to artifacts\installer (skip with -SkipInstaller)

  The version comes from <Version> in Directory.Build.props.
  The same Setup.exe is used for first install and for updates.

.EXAMPLE
  .\build\build.ps1
  .\build\build.ps1 -SkipInstaller
#>
[CmdletBinding()]
param(
    [switch]$SkipTests,
    [switch]$SkipInstaller,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$publishDir = Join-Path $root 'artifacts\publish'
$version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version | Select-Object -First 1

function Invoke-Dotnet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($args -join ' ') failed (exit code $LASTEXITCODE)" }
}

function Publish-Project([string]$project, [string]$destination) {
    Write-Host "==> Publish $project" -ForegroundColor Cyan
    Invoke-Dotnet publish (Join-Path $root $project) -c $Configuration -r win-x64 --self-contained false -o (Join-Path $publishDir $destination) -nologo
}

Write-Host "BackendTray $version ($Configuration)" -ForegroundColor Green

if (-not $SkipTests) {
    Write-Host '==> Test' -ForegroundColor Cyan
    Invoke-Dotnet test (Join-Path $root 'BackendTray.slnx') -c $Configuration -nologo
}

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

Publish-Project 'src\BackendTray.Service\BackendTray.Service.csproj' 'service'
Publish-Project 'src\BackendTray.Manager\BackendTray.Manager.csproj' 'manager'
Publish-Project 'src\BackendTray.Gateway\BackendTray.Gateway.csproj' 'gateway'

# Each API under src\Servers is published to servers\<folder>; the service finds it through server.json.
Get-ChildItem (Join-Path $root 'src\Servers') -Directory | ForEach-Object {
    $project = Get-ChildItem $_.FullName -Filter *.csproj | Select-Object -First 1
    if ($project) { Publish-Project $project.FullName.Substring($root.Length + 1) "servers\$($_.Name)" }
}

Write-Host '==> Frontend' -ForegroundColor Cyan
$webRoot = New-Item (Join-Path $publishDir 'wwwroot') -ItemType Directory -Force
$frontend = Get-ChildItem (Join-Path $root 'frontend\dist') -Force | Where-Object Name -ne '.gitkeep'
if ($frontend) {
    $frontend | Copy-Item -Destination $webRoot.FullName -Recurse -Force
} else {
    Write-Warning 'frontend\dist is empty. The gateway will show a placeholder page.'
}

if (-not $SkipInstaller) {
    Write-Host '==> Installer' -ForegroundColor Cyan
    $iscc = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) { throw 'ISCC.exe not found. Install Inno Setup 6: winget install JRSoftware.InnoSetup' }

    & $iscc "/DAppVersion=$version" (Join-Path $root 'build\installer\BackendTray.iss')
    if ($LASTEXITCODE -ne 0) { throw "ISCC failed (exit code $LASTEXITCODE)" }
    Write-Host "Installer: $(Join-Path $root "artifacts\installer\BackendTray-Setup-$version.exe")" -ForegroundColor Green
}
