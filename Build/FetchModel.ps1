# Puts one model from the vendored WebQwen manifest into .deps\models\<model_id>\:
# the files of its pinned Hugging Face revision plus its library as webgpu.wasm.
# Every file is verified against the hashes Hugging Face lists for that revision
# (the library against the manifest), whether it is downloaded or copied from a
# local folder (-From for the weights folder, -LibFrom for the library file).
param(
    [string]$ModelId = 'Ternary-Bonsai-2-27B-MLC',
    [string]$From,
    [string]$LibFrom
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$root = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content -Raw (Join-Path $root 'Source\WebQwen.Native\Engine\manifest.json') | ConvertFrom-Json
$record = $manifest.models | Where-Object { $_.model_id -eq $ModelId }
if (-not $record) { throw "the vendored manifest has no model $ModelId" }

if ($record.url -notmatch '^https://huggingface\.co/([^/]+/[^/]+)/resolve/([0-9a-f]{40})/(.*?)/?$') {
    throw "unexpected model url $($record.url)"
}
$repo = $Matches[1]; $revision = $Matches[2]; $subdir = $Matches[3]
$target = Join-Path $root ".deps\models\$ModelId"
New-Item -ItemType Directory -Force $target | Out-Null

function Get-Sha256([string]$path) {
    (Get-FileHash -Algorithm SHA256 $path).Hash.ToLowerInvariant()
}

# Hugging Face lists small (non-LFS) files by their git blob id.
function Get-GitBlobSha1([string]$path) {
    $bytes = [IO.File]::ReadAllBytes($path)
    $header = [Text.Encoding]::ASCII.GetBytes("blob $($bytes.Length)`0")
    $sha1 = [Security.Cryptography.SHA1]::Create()
    $null = $sha1.TransformBlock($header, 0, $header.Length, $null, 0)
    $null = $sha1.TransformFinalBlock($bytes, 0, $bytes.Length)
    -join ($sha1.Hash | ForEach-Object { $_.ToString('x2') })
}

function Test-File($path, $entry) {
    if (-not (Test-Path $path)) { return $false }
    if ((Get-Item $path).Length -ne $entry.size) { return $false }
    if ($entry.lfs) { return (Get-Sha256 $path) -eq $entry.lfs.oid }
    (Get-GitBlobSha1 $path) -eq $entry.oid
}

$listUrl = "https://huggingface.co/api/models/$repo/tree/$revision/$subdir`?recursive=true"
$listing = Invoke-RestMethod -UseBasicParsing -Uri $listUrl
$entries = @($listing | Where-Object { $_.type -eq 'file' })
if ($entries.Count -ge 1000) { throw "the listing of $listUrl may be paginated; not handled" }
# The library comes from its own pinned revision.
$entries = @($entries | Where-Object { $_.path -ne "$subdir/webgpu.wasm" })
Write-Host "$ModelId : $($entries.Count) files at $repo@$revision/$subdir"

foreach ($entry in $entries) {
    $name = $entry.path.Substring($subdir.Length + 1)
    $dest = Join-Path $target $name
    if (Test-File $dest $entry) { continue }
    New-Item -ItemType Directory -Force (Split-Path -Parent $dest) | Out-Null
    if ($From) {
        Copy-Item -Force (Join-Path $From $name) "$dest.part"
    } else {
        Write-Host "  download $name"
        Invoke-WebRequest -UseBasicParsing -Uri "$($record.url.TrimEnd('/'))/$name" -OutFile "$dest.part"
    }
    if (-not (Test-File "$dest.part" $entry)) {
        Remove-Item "$dest.part"
        throw "$name does not match $repo@$revision"
    }
    Move-Item -Force "$dest.part" $dest
}

$lib = Join-Path $target 'webgpu.wasm'
if (-not ((Test-Path $lib) -and (Get-Sha256 $lib) -eq $record.lib.sha256)) {
    if ($LibFrom) {
        Copy-Item -Force $LibFrom "$lib.part"
    } else {
        Write-Host "  download webgpu.wasm"
        Invoke-WebRequest -UseBasicParsing -Uri $record.lib.url -OutFile "$lib.part"
    }
    if ((Get-Sha256 "$lib.part") -ne $record.lib.sha256) {
        Remove-Item "$lib.part"
        throw "the library does not match the manifest sha256 $($record.lib.sha256)"
    }
    Move-Item -Force "$lib.part" $lib
}
Write-Host "$ModelId : verified in $target"
