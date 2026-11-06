# Starts one eval run (Docs/Tech/Eval.md): fresh Editor on a clean dev mod, recording into a new run folder.
# Prints RunDir, Port, Pid and McpPort as soon as the Editor is started; Wait-Ready.ps1 waits for it to load.
param(
    [Parameter(Mandatory = $true)][string]$Task,
    [string]$LlamaUrl = 'http://localhost:8080/v1',
    [string]$Model = '',
    [string]$Effort = '',
    [int]$Port = 47811,
    [int]$McpPort = 47812
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$devMod = 'Assets/VeronicaDevMod'
$taskFile = Join-Path $PSScriptRoot "Tasks\$Task.md"
$fixture = Join-Path $PSScriptRoot "Tasks\$Task"
$project = Join-Path $repo 'Source\Veronica\Veronica.csproj'

if (-not (Test-Path $taskFile)) { throw "No task file: $taskFile" }

$json = dotnet msbuild $project -nologo -getProperty:EditorExe -getProperty:EditorLaunchArgs -getProperty:GameDir
if ($LASTEXITCODE -ne 0) { throw 'Reading Editor paths from MSBuild failed.' }
$props = ($json | Out-String | ConvertFrom-Json).Properties
if (-not (Test-Path $props.EditorExe)) { throw "Editor not found: $($props.EditorExe)" }

$editorName = [IO.Path]::GetFileNameWithoutExtension($props.EditorExe)
$running = @(Get-Process -Name $editorName -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    $running | Stop-Process -Force
    $running | Wait-Process -Timeout 60 -ErrorAction SilentlyContinue
}

Push-Location $repo
try {
    git restore --source=HEAD --staged --worktree -- $devMod
    if ($LASTEXITCODE -ne 0) { throw 'Resetting the dev mod failed.' }
    git clean -fdxq -- $devMod
    if ($LASTEXITCODE -ne 0) { throw 'Cleaning the dev mod failed.' }
    if (Test-Path $fixture) {
        Copy-Item -Path (Join-Path $fixture '*') -Exclude 'make_fixture.py' -Destination (Join-Path $repo $devMod) -Recurse -Force
        # Generated files stay out of git; their metas are in the fixture, so the Editor starts without its metafile prompt.
        $maker = Join-Path $fixture 'make_fixture.py'
        if (Test-Path $maker) {
            python -I $maker (Join-Path $repo $devMod)
            if ($LASTEXITCODE -ne 0) { throw 'Generating the fixture failed.' }
        }
    }

    dotnet build $project -c Debug --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'Veronica build failed.' }

    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $runDir = Join-Path $repo ".tmp\eval\runs\$stamp-$Task"
    New-Item -ItemType Directory -Force $runDir | Out-Null

    # The context under test: tracked changes and new files in Source, against the commit.
    $patch = Join-Path $runDir 'context.patch'
    # Windows PowerShell turns redirected native stderr (git's line-ending warnings) into errors, which Stop would throw.
    $ErrorActionPreference = 'Continue'
    git diff HEAD --output=$patch -- Source 2>$null
    $part = Join-Path $runDir 'context.part'
    foreach ($file in @(git ls-files --others --exclude-standard -- Source)) {
        git diff --no-index --output=$part -- /dev/null $file 2>$null
        [IO.File]::AppendAllText($patch, [IO.File]::ReadAllText($part))
    }
    $ErrorActionPreference = 'Stop'
    Remove-Item $part -ErrorAction SilentlyContinue

    $commit = (git rev-parse HEAD).Trim()
    $dirty = @(git status --porcelain -- Source).Count -gt 0
}
finally {
    Pop-Location
}

if ($Model -eq '') {
    # curl.exe, not Invoke-RestMethod: Windows PowerShell has no TLS 1.3. --insecure like the plugin's
    # provider, since locally hosted servers typically use self-signed certificates.
    $models = curl.exe -s --insecure --max-time 10 "$($LlamaUrl.TrimEnd('/'))/models"
    if ($LASTEXITCODE -ne 0) { throw "The model server at $LlamaUrl does not answer (curl exit code $LASTEXITCODE)." }
    $Model = ($models | Out-String | ConvertFrom-Json).data[0].id
    if (-not $Model) { throw "The model server at $LlamaUrl lists no models." }
}

$env:VERONICA_PROVIDER = 'openai'
$env:VERONICA_OPENAI_URL = $LlamaUrl
$env:VERONICA_OPENAI_MODEL = $Model
$env:VERONICA_OPENAI_EFFORT = $Effort
$env:VERONICA_CONTROL_PORT = "$Port"
$env:VERONICA_RUN_DIR = $runDir
# The fork plays the user's Editor steps (moving a dummy) by calling the tools through the MCP endpoint.
$env:VERONICA_MCP = '1'
$env:VERONICA_MCP_PORT = "$McpPort"

$editor = Start-Process -FilePath $props.EditorExe -ArgumentList $props.EditorLaunchArgs -WorkingDirectory $props.GameDir -PassThru
# Windows PowerShell reports no ExitCode unless the handle was opened while the process ran.
$null = $editor.Handle

$runJson = [ordered]@{
    task    = $Task
    commit  = $commit
    dirty   = $dirty
    model   = $Model
    effort  = $Effort
    started = (Get-Date).ToString('o')
    editor  = $editorName
    pid     = $editor.Id
    port    = $Port
    mcpPort = $McpPort
} | ConvertTo-Json
[IO.File]::WriteAllText((Join-Path $runDir 'run.json'), $runJson, (New-Object Text.UTF8Encoding $false))

"RunDir=$runDir"
"Port=$Port"
"Pid=$($editor.Id)"
"McpPort=$McpPort"
