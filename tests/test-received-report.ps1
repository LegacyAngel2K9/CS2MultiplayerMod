$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$folder = Join-Path $repo 'build/reports'
$null = New-Item -ItemType Directory -Path $folder -Force
$path = Join-Path $folder 'received-fixture.report'
$stream = [IO.File]::Create($path)
$writer = [IO.BinaryWriter]::new($stream)
try {
    $writer.Write([byte]17); $writer.Write([int]32)
    $writer.Write([Text.Encoding]::UTF8.GetBytes('aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'))
    $writer.Write([byte]0); $writer.Write([long]3); $writer.Write([long]0); $writer.Write([int]1)
    $writer.Write([int]2); $writer.Write([int]1)
    foreach ($value in @(10, 8, 9, 50)) { $writer.Write([long]$value) }
} finally { $writer.Dispose(); $stream.Dispose() }
$report = (& (Join-Path $repo 'build/read-received-report.ps1') -InputPath $path) | ConvertFrom-Json
if ($report.epoch -ne 3 -or $report.peers.Count -ne 1 -or $report.peers[0].oldestPendingSequence -ne 9) {
    throw 'Stored report could not be decoded.'
}
$bytes = [IO.File]::ReadAllBytes($path)
[IO.File]::WriteAllBytes($path, $bytes + [byte]1)
try {
    $null = & (Join-Path $repo 'build/read-received-report.ps1') -InputPath $path
    throw 'Unexpectedly accepted trailing bytes.'
} catch { if ($_.Exception.Message -ne 'Trailing diagnostic data.') { throw } }
Write-Output 'PASS received report reader and strict framing.'
