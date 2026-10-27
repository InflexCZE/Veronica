# Ends an eval run: kills its Editor and copies the Editor's logs into the run folder.
param(
    [Parameter(Mandatory = $true)][string]$RunDir
)

$ErrorActionPreference = 'Stop'
$run = Get-Content -Raw (Join-Path $RunDir 'run.json') | ConvertFrom-Json

$editor = Get-Process -Id $run.pid -ErrorAction SilentlyContinue
# The pid may have been reused after the Editor exited on its own.
if ($editor -and $editor.ProcessName -eq $run.editor) {
    $editor | Stop-Process -Force
    $editor | Wait-Process -Timeout 60 -ErrorAction SilentlyContinue
}

$logs = Join-Path $env:APPDATA 'VRAGE3.DevEditor\Temp\Logs'
$target = Join-Path $RunDir 'logs'
New-Item -ItemType Directory -Force $target | Out-Null
Get-ChildItem $logs -Filter "*_$($run.pid)*.log" -ErrorAction SilentlyContinue | Copy-Item -Destination $target
"Stopped Editor $($run.pid); logs in $target"
