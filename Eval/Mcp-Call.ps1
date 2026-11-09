# Calls one Veronica tool through the Editor's MCP endpoint (Docs/Tech/McpEndpoint.md) and prints its result.
# The fork uses it to play the user's Editor steps, e.g. moving a dummy the model asked the user to position.
# The endpoint keeps no session, so one tools/call request is all it takes.
param(
    [Parameter(Mandatory = $true)][string]$Tool,
    [string]$ArgsJson = '{}',
    [string]$ArgsFile,
    [int]$Port = 47812
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8

# Windows PowerShell drops the double quotes of a native argument, so JSON passed from bash or cmd arrives broken; a file keeps it intact.
if ($ArgsFile) { $ArgsJson = [IO.File]::ReadAllText((Resolve-Path $ArgsFile)) }
$arguments = $ArgsJson | ConvertFrom-Json
$request = [ordered]@{
    jsonrpc = '2.0'
    id      = 1
    method  = 'tools/call'
    params  = [ordered]@{ name = $Tool; arguments = $arguments }
} | ConvertTo-Json -Depth 20 -Compress

$response = Invoke-WebRequest -UseBasicParsing -Method Post -Uri "http://127.0.0.1:$Port/mcp" `
    -ContentType 'application/json' -Headers @{ Accept = 'application/json, text/event-stream' } `
    -Body ([Text.Encoding]::UTF8.GetBytes($request)) -TimeoutSec 120
$message = [Text.Encoding]::UTF8.GetString($response.RawContentStream.ToArray()) | ConvertFrom-Json

if ($message.error) { throw "MCP error $($message.error.code): $($message.error.message)" }
($message.result.content | ForEach-Object { $_.text }) -join "`n"
if ($message.result.isError) { exit 1 }
