# Builds Veronica and opens the Editor on Assets\VeronicaDevMod with Veronica loaded.
# Paths come from Directory.Build.local.props (see Directory.Build.local.props.template).
param(
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$project = Join-Path $PSScriptRoot 'Source\Veronica\Veronica.csproj'

if (-not $NoBuild) {
    dotnet build $project -c Debug --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'Veronica build failed.' }
}

$json = dotnet msbuild $project -nologo -getProperty:EditorExe -getProperty:EditorLaunchArgs -getProperty:GameDir
if ($LASTEXITCODE -ne 0) { throw 'Reading Editor paths from MSBuild failed.' }
$props = ($json | Out-String | ConvertFrom-Json).Properties

if (-not (Test-Path $props.EditorExe)) { throw "Editor not found: $($props.EditorExe)" }

Start-Process -FilePath $props.EditorExe -ArgumentList $props.EditorLaunchArgs -WorkingDirectory $props.GameDir
