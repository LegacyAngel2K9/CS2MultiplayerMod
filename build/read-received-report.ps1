param([Parameter(Mandatory = $true)][string]$InputPath)
$ErrorActionPreference = 'Stop'
if ((Get-Item -LiteralPath $InputPath).Length -gt 2048) { throw 'Report exceeds size limit.' }
$stream = [IO.File]::OpenRead((Resolve-Path -LiteralPath $InputPath).Path)
$reader = [IO.BinaryReader]::new($stream)
try {
    if ($reader.ReadByte() -ne 17 -or $reader.ReadInt32() -ne 32) { throw 'Not a diagnostic report.' }
    $id = [Text.Encoding]::UTF8.GetString($reader.ReadBytes(32))
    $parsedId = [Guid]::Empty
    if (![Guid]::TryParseExact($id, 'N', [ref]$parsedId) -or $parsedId -eq [Guid]::Empty) { throw 'Invalid report ID.' }
    if ($reader.ReadByte() -ne 0) { throw 'Acknowledgements contain no diagnostic data.' }
    $epoch = $reader.ReadInt64(); $evicted = $reader.ReadInt64(); $count = $reader.ReadInt32()
    if ($epoch -lt 0 -or $evicted -lt 0 -or $count -lt 0 -or $count -gt 32) { throw 'Invalid diagnostic header.' }
    $rows = @(for ($i = 0; $i -lt $count; $i++) {
        $row = [ordered]@{ player = $reader.ReadInt32(); pending = $reader.ReadInt32();
            received = $reader.ReadInt64(); completedObserved = $reader.ReadInt64();
            oldestPendingSequence = $reader.ReadInt64(); oldestPendingMs = $reader.ReadInt64() }
        if (@($row.Values | Where-Object { $_ -lt 0 }).Count -gt 0 -or $row.pending -gt 2048) { throw 'Invalid diagnostic row.' }
        $row
    })
    if ($stream.Position -ne $stream.Length) { throw 'Trailing diagnostic data.' }
    [ordered]@{ schema = 1; reportId = $id; epoch = $epoch; evicted = $evicted; peers = $rows;
        scope = 'Client-reported numeric window; not host-verified gameplay. No raw log, save, endpoint, player name, latency or complete-session baseline.' } | ConvertTo-Json -Depth 5
} finally { $reader.Dispose(); $stream.Dispose() }
