$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repo 'build/reports/export-regression.json'
& (Join-Path $repo 'build/export-sync-diagnostics.ps1') -LogPath (Join-Path $PSScriptRoot 'fixtures/diagnostic-export.log') -OutputPath $output
$json = Get-Content -LiteralPath $output -Raw
$parsed = $json | ConvertFrom-Json
if ($parsed.records.Count -ne 5) { throw 'Wrong record count.' }
if ($parsed.records[3].stage -ne 'waiting-identity' -or
    $parsed.records[4].stage -ne 'commit-unverified' -or
    $parsed.records[4].reasonCode -ne 'route-update-state-mismatch') { throw 'Route state or safe reason code lost.' }
if ($parsed.records[0].session -ne 'session-1' -or $parsed.records[1].stage -ne 'completed') { throw 'Missing diagnostic correlation.' }
if ($parsed.records[1].queueMs -ne 10 -or $parsed.records[1].applyMs -ne 30) { throw 'Queue/apply timing lost in export.' }
if ($parsed.records[1].captureResult -ne 'unknown' -or $parsed.records[1].applyResult -ne 'verified-or-equivalent') { throw 'Contract results lost in export.' }
foreach ($private in @('Alice','PrivateUser','192.0.2.1','76561198000000000','PrivateAsset','aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa')) {
    if ($json.Contains($private)) { throw 'Private input leaked into export.' }
}
Write-Output 'PASS allowlist export preserves diagnostics without raw private data.'
