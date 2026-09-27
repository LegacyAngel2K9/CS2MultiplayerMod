param(
    [Parameter(Mandatory = $true)][string]$LogPath,
    [string]$OutputPath = (Join-Path $PSScriptRoot 'reports/sync-diagnostics.json')
)
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $LogPath -PathType Leaf)) { throw 'Flight log not found.' }
# An allowlist export, not a best-effort regex replacement of arbitrary log text.
# No raw text, names, addresses, prefab labels, machine paths or free-form reasons leave this filter.
$records = [Collections.Generic.Queue[object]]::new()
$dropped = 0
$sessions = @{}
$safeReasons = @(& (Join-Path $PSScriptRoot 'diagnostic-reason-codes.ps1'))
foreach ($line in [IO.File]::ReadLines((Resolve-Path -LiteralPath $LogPath).Path)) {
    if ($line -notmatch '\[pipeline\] operation(?:-summary)?(?: schema=1)? session=([a-f0-9]{32}) ') { continue }
    $sourceSession = $Matches[1]
    if (!$sessions.ContainsKey($sourceSession)) {
        if ($sessions.Count -ge 128) { $dropped++; continue }
        $sessions[$sourceSession] = 'session-' + ($sessions.Count + 1)
    }
    $record = [ordered]@{ session = $sessions[$sourceSession] }
    foreach ($field in @('epoch','sequence','player','command','operation','elapsedLocalMs',
        'received','completedObserved','pending','oldestPendingMs','oldestPendingSequence','queueMs','applyMs','evicted')) {
        if ($line -match ('(?:^| )' + $field + '=(-?\d+)(?: |$)')) {
            $number = 0L
            if ([long]::TryParse($Matches[1], [ref]$number)) { $record[$field] = $number }
        }
    }
    if ($line -match ' stage=(received|decoded|applying|armed|submitted|waiting-identity|commit-unverified|retry|completed|rejected|captured)(?: |$)') {
        $record['stage'] = $Matches[1]
    }
    if ($line -match ' reason=([a-z-]+)(?: |$)' -and $safeReasons -contains $Matches[1]) {
        $record['reasonCode'] = $Matches[1]
    }
    if ($line -match ' captureResult=(encoded|unknown)(?: |$)') { $record['captureResult'] = $Matches[1] }
    if ($line -match ' applyResult=(unknown|verified-or-equivalent|received|decoded|applying|armed|submitted|waiting-identity|commit-unverified|retry|completed|rejected)(?: |$)') {
        $record['applyResult'] = $Matches[1]
    }
    if ($records.Count -eq 20000) { $null = $records.Dequeue(); $dropped++ }
    $records.Enqueue($record)
}
$fullOutput = [IO.Path]::GetFullPath($OutputPath)
$null = New-Item -ItemType Directory -Path (Split-Path $fullOutput -Parent) -Force
$result = [ordered]@{ schema = 1; truncatedRecords = $dropped; records = @($records.ToArray());
    note = 'Local receive/apply observations only. completedObserved is not AppliedThrough; uninstrumented domains remain pending. No cross-machine latency inferred.' }
[IO.File]::WriteAllText($fullOutput, ($result | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
Write-Output "Exported $($records.Count) allowlisted records; omitted $dropped over budget."
