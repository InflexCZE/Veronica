# Downloads the pinned prebuilt dependencies listed in deps.json into .deps\<name>\.
# Archives are cached in .deps\dl\ and verified by SHA-256; a dependency is
# re-extracted only when its pinned archives change.
param([switch]$Force)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$root = Split-Path -Parent $PSScriptRoot
$depsDir = Join-Path $root '.deps'
$dlDir = Join-Path $depsDir 'dl'
$tar = Join-Path $env:SystemRoot 'System32\tar.exe'
New-Item -ItemType Directory -Force $dlDir | Out-Null

function Get-Sha256([string]$path) {
    (Get-FileHash -Algorithm SHA256 $path).Hash.ToLowerInvariant()
}

function Get-Archive($archive) {
    $file = Join-Path $dlDir ([IO.Path]::GetFileName(([Uri]$archive.url).AbsolutePath))
    if ((Test-Path $file) -and (Get-Sha256 $file) -eq $archive.sha256) { return $file }
    Write-Host "  download $($archive.url)"
    Invoke-WebRequest -UseBasicParsing -Uri $archive.url -OutFile "$file.part"
    $actual = Get-Sha256 "$file.part"
    if ($actual -ne $archive.sha256) {
        Remove-Item "$file.part"
        throw "SHA-256 mismatch for $($archive.url): expected $($archive.sha256), got $actual"
    }
    Move-Item -Force "$file.part" $file
    $file
}

$deps = Get-Content -Raw (Join-Path $PSScriptRoot 'deps.json') | ConvertFrom-Json
foreach ($prop in $deps.PSObject.Properties) {
    $name = $prop.Name
    $dep = $prop.Value
    $target = Join-Path $depsDir $name
    $stampFile = Join-Path $target '.stamp'
    $stamp = (@($dep.version) + @($dep.archives | ForEach-Object { $_.sha256 })) -join "`n"
    if (-not $Force -and (Test-Path $stampFile) -and (Get-Content -Raw $stampFile).Trim() -eq $stamp) {
        Write-Host "$name $($dep.version): up to date"
        continue
    }
    Write-Host "$name $($dep.version)"
    if (Test-Path $target) { Remove-Item -Recurse -Force $target }
    foreach ($archive in $dep.archives) {
        $file = Get-Archive $archive
        $into = Join-Path $target $archive.into
        New-Item -ItemType Directory -Force $into | Out-Null
        $tarArgs = @('-xf', $file, '-C', $into)
        if ($archive.strip) { $tarArgs += "--strip-components=$($archive.strip)" }
        & $tar @tarArgs
        if ($LASTEXITCODE -ne 0) { throw "extracting $file failed" }
    }
    Set-Content -Encoding ASCII $stampFile $stamp
}
