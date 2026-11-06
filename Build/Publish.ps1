# Builds one release (Docs/Tech/Distribution.md) into bin\dist\<version>\:
#   Veronica.exe  self-contained single file
#   Veronica.zip  Runtime\ (plugin, WebQwen.exe single file + native DLLs + manifest.json), Skills\, Wiki\
# Creates no release anywhere; upload both files as the assets of release v<version>.
param(
    [Parameter(Mandatory)][string]$Version
)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must be X.Y.Z, got '$Version'." }

$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "bin\dist\$Version"
$stage = Join-Path $out 'stage'
if (Test-Path $out) { Remove-Item -Recurse -Force $out }
New-Item -ItemType Directory -Force $stage | Out-Null

# The native projects need Visual Studio's MSBuild; the dotnet CLI cannot build them.
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -prerelease -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'Visual Studio MSBuild not found.' }

function Invoke-Checked([string]$what, [scriptblock]$command) {
    & $command
    if ($LASTEXITCODE -ne 0) { throw "$what failed." }
}

$common = @('-nologo', '-v:minimal', '-p:Configuration=Release', "-p:Version=$Version")

Invoke-Checked 'Native build' {
    & $msbuild (Join-Path $root 'Veronica.sln') -restore -m @common '-p:Platform=x64' '-t:WebQwen_Dawn;WebQwen_Native'
}

$runtime = Join-Path $stage 'Runtime'
Invoke-Checked 'Plugin build' {
    & $msbuild (Join-Path $root 'Source\Veronica\Veronica.csproj') -restore @common "-p:OutDir=$(Join-Path $stage 'plugin')\"
}
Get-ChildItem (Join-Path $stage 'plugin') -Filter *.dll | Copy-Item -Destination (New-Item -ItemType Directory -Force $runtime)

$webqwen = Join-Path $stage 'webqwen'
Invoke-Checked 'WebQwen publish' {
    & $msbuild (Join-Path $root 'Source\WebQwen\WebQwen.csproj') -restore -t:Publish @common '-p:Platform=x64' `
        '-p:RuntimeIdentifier=win-x64' '-p:SelfContained=false' '-p:PublishSingleFile=true' "-p:PublishDir=$webqwen\"
}
foreach ($name in 'WebQwen.exe', 'wq.dll', 'webgpu_dawn.dll', 'manifest.json') {
    Copy-Item (Join-Path $webqwen $name) $runtime
}

Copy-Item -Recurse (Join-Path $root 'Source\Veronica\Skills') (Join-Path $stage 'Skills')
Copy-Item -Recurse (Join-Path $root 'Source\Veronica\Wiki') (Join-Path $stage 'Wiki')

Invoke-Checked 'Veronica.exe publish' {
    dotnet publish (Join-Path $root 'Source\Veronica.Setup\Veronica.Setup.csproj') --nologo -v minimal -c Release `
        -p:Platform=x64 -r win-x64 --self-contained true -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
        "-p:Version=$Version" -o (Join-Path $stage 'setup')
}
Copy-Item (Join-Path $stage 'setup\Veronica.exe') $out

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = Join-Path $out 'Veronica.zip'
$archive = [IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    foreach ($folder in 'Runtime', 'Skills', 'Wiki') {
        $base = Join-Path $stage $folder
        Get-ChildItem -Recurse -File $base | ForEach-Object {
            $entry = $folder + '/' + $_.FullName.Substring($base.Length + 1).Replace('\', '/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $entry, 'Optimal') | Out-Null
        }
    }
}
finally {
    $archive.Dispose()
}

Remove-Item -Recurse -Force $stage
Get-ChildItem $out | ForEach-Object { '{0,-14} {1,8:N1} MB' -f $_.Name, ($_.Length / 1MB) }
