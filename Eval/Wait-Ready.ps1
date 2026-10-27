# Waits up to -Seconds for a run's Editor to open its control channel. Prints Ready, NotReady (call again) or
# Exited. Bounded so a caller can wait in the foreground, one call at a time.
param(
    [Parameter(Mandatory = $true)][string]$RunDir,
    [int]$Seconds = 540
)

$ErrorActionPreference = 'Stop'
$run = Get-Content -Raw (Join-Path $RunDir 'run.json') | ConvertFrom-Json
$deadline = (Get-Date).AddSeconds($Seconds)
while ($true) {
    $editor = Get-Process -Id $run.pid -ErrorAction SilentlyContinue
    if (-not $editor -or $editor.ProcessName -ne $run.editor) { 'Exited'; exit 1 }
    try {
        Invoke-RestMethod -Uri "http://127.0.0.1:$($run.port)/status" -TimeoutSec 5 | Out-Null
        'Ready'
        exit 0
    }
    catch {
        if ((Get-Date) -gt $deadline) { 'NotReady'; exit 2 }
        Start-Sleep -Seconds 5
    }
}
