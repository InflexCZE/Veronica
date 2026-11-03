# Fetches the run's Editor validation once processing has settled (at most -Seconds), saves it as
# validation-<Label>.json in the run folder and prints one summary line plus every error.
# Exit 0: no errors. 1: errors on the mod only. 2: errors outside the mod (vanilla): the Editor is unreliable.
# 3: the control channel did not answer.
param(
    [Parameter(Mandatory = $true)][string]$RunDir,
    [string]$Label = 'end',
    [int]$Seconds = 120
)

$ErrorActionPreference = 'Stop'
$run = Get-Content -Raw (Join-Path $RunDir 'run.json') | ConvertFrom-Json
try {
    $raw = Invoke-WebRequest -UseBasicParsing -Uri "http://127.0.0.1:$($run.port)/validation?wait=$Seconds" -TimeoutSec ($Seconds + 60)
}
catch {
    "NoAnswer: $($_.Exception.Message)"
    exit 3
}
$path = Join-Path $RunDir "validation-$Label.json"
[System.IO.File]::WriteAllText($path, $raw.Content)
$v = $raw.Content | ConvertFrom-Json
"idle=$($v.idle) errors=$($v.errors) (mod $($v.mod_errors), other $($v.other_errors)) warnings=$($v.warnings) -> $path"
foreach ($i in $v.issues | Where-Object { $_.severity -eq 'Error' }) {
    "ERROR [$(if ($i.mod) { 'mod' } else { $i.project })] $($i.item) $($i.field): $($i.message)"
}
if ($v.other_errors -gt 0) { exit 2 }
if ($v.mod_errors -gt 0) { exit 1 }
exit 0
