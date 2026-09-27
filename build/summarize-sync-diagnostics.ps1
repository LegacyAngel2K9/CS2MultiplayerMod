param(
    [Parameter(Mandatory = $true)][string]$InputPath,
    [string]$OutputPath = (Join-Path $PSScriptRoot 'reports/sync-summary.json')
)
$ErrorActionPreference = 'Stop'
$sourceFile = Get-Item -LiteralPath $InputPath
if ($sourceFile.Length -gt 20MB) { throw 'Diagnostic export exceeds the 20 MiB analysis limit.' }
$source = Get-Content -LiteralPath $InputPath -Raw | ConvertFrom-Json
function Test-NonnegativeInteger($Value) {
    return ($Value -is [int] -or $Value -is [long]) -and $Value -ge 0
}
if ($source -isnot [pscustomobject] -or !(Test-NonnegativeInteger $source.schema) -or $source.schema -ne 1 -or
    $source.records -isnot [array] -or $source.records.Count -gt 20000 -or
    !(Test-NonnegativeInteger $source.truncatedRecords)) { throw 'Unsupported diagnostic export.' }
# Validate the entire input before writing any output. Never coerce strings, booleans or
# fractional measurements into plausible integer timings, or leak arbitrary session text.
foreach ($record in $source.records) {
    if ($record -isnot [pscustomobject] -or $record.session -isnot [string] -or
        $record.session -cnotmatch '^session-[1-9][0-9]*$') { throw 'Invalid diagnostic record identity.' }
    foreach ($field in @('epoch','sequence','player','command','elapsedLocalMs','received',
        'completedObserved','pending','oldestPendingMs','oldestPendingSequence','queueMs','applyMs','evicted')) {
        if ($null -ne $record.$field -and !(Test-NonnegativeInteger $record.$field)) {
            throw "Invalid nonnegative integer diagnostic field: $field."
        }
    }
}
$latest = @{}
$evicted = 0L
$peakPending = $null
$queueSampleCount = 0
$queueSamplesWithoutEpoch = 0
$summaries = @{}
$epochSummaries = @{}
foreach ($record in $source.records) {
    if ($null -ne $record.evicted) { $evicted = [Math]::Max($evicted, [long]$record.evicted) }
    if ($null -ne $record.pending) {
        $peakPending = if ($null -eq $peakPending) { [long]$record.pending } else { [Math]::Max($peakPending, [long]$record.pending) }
        $queueSampleCount++
        if ($null -eq $record.epoch) { $queueSamplesWithoutEpoch++ }
    }
    if ($null -ne $record.pending -and $null -ne $record.player -and $null -ne $record.session) {
        $peerKey = '{0}:{1}' -f $record.session, $record.player
        $previousPeak = if ($summaries.ContainsKey($peerKey)) { $summaries[$peerKey].peak } else { 0L }
        $summaries[$peerKey] = @{ latest = $record; peak = [Math]::Max($previousPeak, [long]$record.pending) }
        if ($null -ne $record.epoch) {
            $epochKey = '{0}:{1}:{2}' -f $record.session, $record.epoch, $record.player
            $previousPeak = if ($epochSummaries.ContainsKey($epochKey)) { $epochSummaries[$epochKey].peak } else { 0L }
            $epochSummaries[$epochKey] = @{ latest = $record; peak = [Math]::Max($previousPeak, [long]$record.pending) }
        }
    }
    if ($null -eq $record.sequence -or $null -eq $record.epoch -or $null -eq $record.player -or
        $record.stage -notin @('received','decoded','applying','armed','submitted','waiting-identity',
            'commit-unverified','retry','completed','rejected')) { continue }
    $key = '{0}:{1}:{2}:{3}' -f $record.session, $record.epoch, $record.sequence, $record.player
    # Repeated completion observations must not multiply the latency sample count.
    if (!$latest.ContainsKey($key) -or $latest[$key].stage -ne 'completed') { $latest[$key] = $record }
}
function Quantiles($Values) {
    $sorted = @($Values | Sort-Object)
    if ($sorted.Count -eq 0) { return @{ count = 0; p50 = $null; p95 = $null; p99 = $null } }
    return @{ count = $sorted.Count;
        p50 = $sorted[[int][Math]::Ceiling($sorted.Count * 0.50) - 1];
        p95 = $sorted[[int][Math]::Ceiling($sorted.Count * 0.95) - 1];
        p99 = $sorted[[int][Math]::Ceiling($sorted.Count * 0.99) - 1] }
}
$completed = @($latest.Values | Where-Object stage -eq 'completed')
$queue = @($completed | Where-Object { $null -ne $_.queueMs -and $_.queueMs -ge 0 } | ForEach-Object { [long]$_.queueMs })
$apply = @($completed | Where-Object { $null -ne $_.applyMs -and $_.applyMs -ge 0 } | ForEach-Object { [long]$_.applyMs })
$counts = [ordered]@{}
foreach ($group in ($latest.Values | Group-Object stage | Sort-Object Name)) { $counts[$group.Name] = $group.Count }
$peerGroups = @{}
foreach ($operation in $latest.Values) {
    $peerKey = '{0}:{1}' -f $operation.session, $operation.player
    if (!$peerGroups.ContainsKey($peerKey)) { $peerGroups[$peerKey] = [Collections.Generic.List[object]]::new() }
    $peerGroups[$peerKey].Add($operation)
}
foreach ($peerKey in $summaries.Keys) {
    if (!$peerGroups.ContainsKey($peerKey)) { $peerGroups[$peerKey] = [Collections.Generic.List[object]]::new() }
}
$peers = @(foreach ($peerKey in ($peerGroups.Keys | Sort-Object)) {
    $operations = @($peerGroups[$peerKey].ToArray())
    $sample = if ($summaries.ContainsKey($peerKey)) { $summaries[$peerKey].latest } else { $null }
    $identity = if ($operations.Count -gt 0) { $operations[0] } else { $sample }
    $finished = @($operations | Where-Object stage -eq 'completed')
    $queueSamples = @($finished | Where-Object { $null -ne $_.queueMs -and $_.queueMs -ge 0 } | ForEach-Object { [long]$_.queueMs })
    $applySamples = @($finished | Where-Object { $null -ne $_.applyMs -and $_.applyMs -ge 0 } | ForEach-Object { [long]$_.applyMs })
    [ordered]@{
        session = $identity.session; player = $identity.player;
        hasSummarySamples = $null -ne $sample;
        lastSampledPending = if ($null -ne $sample) { $sample.pending } else { $null };
        peakSampledPending = if ($null -ne $sample) { $summaries[$peerKey].peak } else { $null };
        lastSampledOldestPendingMs = if ($null -ne $sample) { $sample.oldestPendingMs } else { $null };
        lastSampledOldestPendingSequence = if ($null -ne $sample) { $sample.oldestPendingSequence } else { $null };
        uniqueObservedOperations = $operations.Count; completedObserved = $finished.Count;
        incompleteObserved = @($operations | Where-Object { $_.stage -notin @('completed', 'rejected') }).Count;
        rejectedObserved = @($operations | Where-Object stage -eq 'rejected').Count;
        queueMs = (Quantiles $queueSamples); applyMs = (Quantiles $applySamples);
        completedWithoutQueueTiming = $finished.Count - $queueSamples.Count;
        completedWithoutApplyTiming = $finished.Count - $applySamples.Count
    }
})
$qualityWarnings = [Collections.Generic.List[string]]::new()
$safeReasons = @(& (Join-Path $PSScriptRoot 'diagnostic-reason-codes.ps1'))
$reasonOutcomes = @(foreach ($group in ($latest.Values | Group-Object session, epoch, player, stage)) {
    foreach ($reasonGroup in ($group.Group | Group-Object {
        if ($safeReasons -contains $_.reasonCode) { $_.reasonCode } else { 'unknown' }
    })) {
        $identity = $reasonGroup.Group[0]
        [ordered]@{
            session = $identity.session; epoch = $identity.epoch; player = $identity.player;
            stage = $identity.stage; reasonCode = $reasonGroup.Name;
            uniqueObservedOperations = $reasonGroup.Count
        }
    }
})
if ($queueSampleCount -eq 0) { $qualityWarnings.Add('no-queue-summary-samples') }
if ($queueSamplesWithoutEpoch -gt 0) { $qualityWarnings.Add('queue-summary-samples-missing-epoch') }
$epochs = @(foreach ($epochGroup in ($latest.Values | Group-Object session, epoch)) {
    $observed = @($epochGroup.Group)
    $finished = @($observed | Where-Object stage -eq 'completed')
    $knownQueue = @($finished | Where-Object { $null -ne $_.queueMs -and $_.queueMs -ge 0 } | ForEach-Object { [long]$_.queueMs })
    $knownApply = @($finished | Where-Object { $null -ne $_.applyMs -and $_.applyMs -ge 0 } | ForEach-Object { [long]$_.applyMs })
    [ordered]@{
        session = $observed[0].session; epoch = $observed[0].epoch;
        uniqueObservedOperations = $observed.Count; completedObserved = $finished.Count;
        incompleteObserved = @($observed | Where-Object { $_.stage -notin @('completed','rejected') }).Count;
        rejectedObserved = @($observed | Where-Object stage -eq 'rejected').Count;
        queueMs = (Quantiles $knownQueue); applyMs = (Quantiles $knownApply);
        completedWithoutQueueTiming = $finished.Count - $knownQueue.Count;
        completedWithoutApplyTiming = $finished.Count - $knownApply.Count
    }
})
if ($epochs.Count -gt 1) { $qualityWarnings.Add('aggregate-timings-pool-multiple-session-epochs') }
if ($latest.Count -eq 0) { $qualityWarnings.Add('no-individual-operation-records') }
if ($completed.Count -eq 0) { $qualityWarnings.Add('no-completed-operation-samples') }
if ($source.truncatedRecords -gt 0) { $qualityWarnings.Add('export-truncated') }
if ($evicted -gt 0) { $qualityWarnings.Add('diagnostic-window-evicted-records') }
if ($completed.Count -gt $queue.Count) { $qualityWarnings.Add('completed-operations-missing-queue-timing') }
if ($completed.Count -gt $apply.Count) { $qualityWarnings.Add('completed-operations-missing-apply-timing') }
if (@($latest.Values | Where-Object { $_.stage -notin @('completed','rejected') }).Count -gt 0) {
    $qualityWarnings.Add('unfinished-operations-excluded-from-quantiles')
}
if (@($peers | Where-Object { !$_.hasSummarySamples }).Count -gt 0) {
    $qualityWarnings.Add('some-origins-without-queue-summary-samples')
}
$report = [ordered]@{
    schema = 1; uniqueObservedOperations = $latest.Count; completedObserved = $completed.Count;
    statuses = $counts; queueMs = (Quantiles $queue); applyMs = (Quantiles $apply);
    completedWithoutQueueTiming = $completed.Count - $queue.Count;
    completedWithoutApplyTiming = $completed.Count - $apply.Count;
    peakSampledPendingPerPeer = $peakPending; maxObservedEvictions = $evicted;
    truncatedExportRecords = $source.truncatedRecords;
    peers = $peers;
    epochs = $epochs;
    reasonOutcomes = $reasonOutcomes;
    reasonScope = 'Latest observed state per unique operation, not retry-event counts or confirmed resync causes. Missing or unapproved reason codes are unknown. Completed operations may have successful outcome reasons.';
    epochQueues = @(foreach ($key in ($epochSummaries.Keys | Sort-Object)) {
        $window = $epochSummaries[$key]
        $sample = $window.latest
        [ordered]@{
            session = $sample.session; epoch = $sample.epoch; player = $sample.player;
            lastSampledPending = $sample.pending; peakSampledPending = $window.peak;
            lastSampledReceived = $sample.received;
            lastSampledCompletedObserved = $sample.completedObserved;
            lastSampledOldestPendingMs = $sample.oldestPendingMs;
            lastSampledOldestPendingSequence = $sample.oldestPendingSequence
        }
    });
    epochGrouping = 'Separate local session/epoch timing windows; epoch changes are not proof of successful resync. Summary-only records without epoch are not assigned to a window. These are not cross-machine identities.';
    dataQuality = [ordered]@{
        queueSummarySamples = $queueSampleCount;
        queueSummarySamplesWithoutEpoch = $queueSamplesWithoutEpoch;
        coverage = 'partial-observation-window'; warnings = @($qualityWarnings.ToArray());
        completenessVerified = $false;
        interpretation = 'No warnings does not prove a complete capture or a successful multiplayer baseline. Unfinished operations are excluded from timing quantiles and may bias them downward.'
    };
    peerGrouping = 'player is the command origin, not the receiving machine. Each session is local to this export; session aliases from different exports must not be merged as shared identities.';
    scope = 'Local observations in the exported window, not all gameplay operations. Quantiles use known completed samples only; pending work is excluded, not assigned zero latency. Peak is sampled, not an exact maximum. No cross-machine latency, frame cost or bandwidth inferred.'
}
$target = [IO.Path]::GetFullPath($OutputPath)
$null = New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force
[IO.File]::WriteAllText($target, ($report | ConvertTo-Json -Depth 7), [Text.UTF8Encoding]::new($false))
Write-Output "Summarized $($latest.Count) observed operations, $($completed.Count) completed."
