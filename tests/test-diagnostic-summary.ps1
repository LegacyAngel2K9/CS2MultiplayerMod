$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'test-diagnostic-export.ps1')
$inputFile = Join-Path $repo 'build/reports/export-regression.json'
$outputFile = Join-Path $repo 'build/reports/summary-regression.json'
& (Join-Path $repo 'build/summarize-sync-diagnostics.ps1') -InputPath $inputFile -OutputPath $outputFile
$report = Get-Content -LiteralPath $outputFile -Raw | ConvertFrom-Json
if ($report.uniqueObservedOperations -ne 3 -or $report.completedObserved -ne 1 -or
    $report.queueMs.p95 -ne 10 -or $report.applyMs.p99 -ne 30 -or
    $report.statuses.'waiting-identity' -ne 1 -or $report.statuses.'commit-unverified' -ne 1) {
    throw 'Diagnostic summary miscounted or misclassified incomplete operations.'
}
# Empty input must produce unavailable quantiles, not a fictitious zero-latency result.
$duplicateInput = Get-Content -LiteralPath $inputFile -Raw | ConvertFrom-Json
$duplicateInput.records += $duplicateInput.records[1]
$duplicateFile = Join-Path $repo 'build/reports/duplicate-diagnostics.json'
[IO.File]::WriteAllText($duplicateFile, ($duplicateInput | ConvertTo-Json -Depth 5))
& (Join-Path $repo 'build/summarize-sync-diagnostics.ps1') -InputPath $duplicateFile -OutputPath $outputFile
$duplicate = Get-Content -LiteralPath $outputFile -Raw | ConvertFrom-Json
if ($duplicate.completedObserved -ne 1 -or $duplicate.queueMs.count -ne 1) {
    throw 'Duplicate completion inflated sample count.'
}
# Empty input must produce unavailable quantiles, not a fictitious zero-latency result.
$emptyFile = Join-Path $repo 'build/reports/empty-diagnostics.json'
[IO.File]::WriteAllText($emptyFile, '{"schema":1,"truncatedRecords":0,"records":[]}')
& (Join-Path $repo 'build/summarize-sync-diagnostics.ps1') -InputPath $emptyFile -OutputPath $outputFile
$empty = Get-Content -LiteralPath $outputFile -Raw | ConvertFrom-Json
if ($empty.completedObserved -ne 0 -or $null -ne $empty.queueMs.p95 -or $null -ne $empty.applyMs.p99) {
    throw 'Empty report invented timing samples.'
}
Write-Output 'PASS local timing summary and unknown/empty sample handling.'

$multiFile = Join-Path $repo 'build/reports/multi-peer-diagnostics.json'
$multi = @{ schema = 1; truncatedRecords = 0; records = @(
    @{ session='session-1'; epoch=1; sequence=1; player=2; stage='completed'; queueMs=10; applyMs=20 },
    @{ session='session-1'; epoch=1; sequence=2; player=3; stage='completed'; queueMs=1000; applyMs=2000 },
    @{ session='session-1'; epoch=1; sequence=3; player=3; stage='waiting-identity' },
    @{ session='session-2'; epoch=1; sequence=1; player=2; stage='completed' }
) }
[IO.File]::WriteAllText($multiFile, ($multi | ConvertTo-Json -Depth 5))
& (Join-Path $repo 'build/summarize-sync-diagnostics.ps1') -InputPath $multiFile -OutputPath $outputFile
$multiReport = Get-Content -LiteralPath $outputFile -Raw | ConvertFrom-Json
$fast = @($multiReport.peers | Where-Object { $_.session -eq 'session-1' -and $_.player -eq 2 })
$slow = @($multiReport.peers | Where-Object { $_.session -eq 'session-1' -and $_.player -eq 3 })
$unknown = @($multiReport.peers | Where-Object session -eq 'session-2')
if ($multiReport.peers.Count -ne 3 -or $fast[0].applyMs.p95 -ne 20 -or
    $slow[0].applyMs.p95 -ne 2000 -or $slow[0].incompleteObserved -ne 1 -or
    $unknown[0].completedWithoutApplyTiming -ne 1 -or $null -ne $unknown[0].applyMs.p95) {
    throw 'Peer/session grouping merged latency or invented missing timing.'
}
Write-Output 'PASS per-peer latency, session isolation and missing timing.'

$multi.records += @(
    @{ session='session-1'; player=4; pending=9; oldestPendingMs=900; oldestPendingSequence=8 },
    @{ session='session-1'; player=4; pending=2; oldestPendingMs=1200; oldestPendingSequence=8 }
)
[IO.File]::WriteAllText($multiFile, ($multi | ConvertTo-Json -Depth 5))
& (Join-Path $repo 'build/summarize-sync-diagnostics.ps1') -InputPath $multiFile -OutputPath $outputFile
$sampledReport = Get-Content -LiteralPath $outputFile -Raw | ConvertFrom-Json
$sampled = @($sampledReport.peers | Where-Object player -eq 4)
$unsampled = @($sampledReport.peers | Where-Object { $_.session -eq 'session-1' -and $_.player -eq 2 })
if ($sampled.Count -ne 1 -or $sampled[0].uniqueObservedOperations -ne 0 -or
    $sampled[0].lastSampledPending -ne 2 -or $sampled[0].peakSampledPending -ne 9 -or
    $sampled[0].lastSampledOldestPendingMs -ne 1200 -or
    $sampled[0].lastSampledOldestPendingSequence -ne 8 -or
    $null -ne $sampled[0].applyMs.p95 -or $unsampled[0].hasSummarySamples -or
    $null -ne $unsampled[0].lastSampledPending) {
    throw 'Summary-only peer disappeared or sampled/current/unknown state was confused.'
}
Write-Output 'PASS summary-only peers, sampled backlog peaks and unavailable samples.'

$multi.truncatedRecords = 12
$multi.records += @{ session='session-1'; player=4; pending=2; evicted=7 }
[IO.File]::WriteAllText($multiFile, ($multi | ConvertTo-Json -Depth 5))
& (Join-Path $repo 'build/summarize-sync-diagnostics.ps1') -InputPath $multiFile -OutputPath $outputFile
$quality = (Get-Content -LiteralPath $outputFile -Raw | ConvertFrom-Json).dataQuality
foreach ($expected in @('export-truncated','diagnostic-window-evicted-records',
    'completed-operations-missing-apply-timing','unfinished-operations-excluded-from-quantiles')) {
    if ($quality.warnings -notcontains $expected) { throw "Missing data-quality warning: $expected" }
}
if ($quality.completenessVerified) { throw 'Partial observations were certified complete.' }
& (Join-Path $repo 'build/summarize-sync-diagnostics.ps1') -InputPath $emptyFile -OutputPath $outputFile
$emptyQuality = (Get-Content -LiteralPath $outputFile -Raw | ConvertFrom-Json).dataQuality
if ($emptyQuality.warnings -notcontains 'no-completed-operation-samples' -or
    $emptyQuality.warnings -notcontains 'no-individual-operation-records') { throw 'Empty sample warning missing.' }
Write-Output 'PASS explicit truncation, missing-timing, incomplete-work and empty-data warnings.'

$epochFixture = @{ schema=1; truncatedRecords=0; records=@(
    @{ session='session-1'; epoch=1; sequence=1; player=2; stage='completed'; queueMs=10; applyMs=20 },
    @{ session='session-1'; epoch=2; sequence=1; player=2; stage='completed'; queueMs=100; applyMs=200 },
    @{ session='session-1'; epoch=2; sequence=2; player=2; stage='retry' },
    @{ session='session-2'; epoch=1; sequence=1; player=2; stage='completed' }
) }
[IO.File]::WriteAllText($multiFile, ($epochFixture | ConvertTo-Json -Depth 5))
& (Join-Path $repo 'build/summarize-sync-diagnostics.ps1') -InputPath $multiFile -OutputPath $outputFile
$epochReport = Get-Content -LiteralPath $outputFile -Raw | ConvertFrom-Json
$before = @($epochReport.epochs | Where-Object { $_.session -eq 'session-1' -and $_.epoch -eq 1 })
$after = @($epochReport.epochs | Where-Object { $_.session -eq 'session-1' -and $_.epoch -eq 2 })
$missing = @($epochReport.epochs | Where-Object session -eq 'session-2')
if ($epochReport.epochs.Count -ne 3 -or $before.Count -ne 1 -or $after.Count -ne 1 -or
    $before[0].applyMs.p95 -ne 20 -or $after[0].applyMs.p95 -ne 200 -or
    $after[0].incompleteObserved -ne 1 -or $missing[0].completedWithoutApplyTiming -ne 1 -or
    $null -ne $missing[0].applyMs.p95 -or
    $epochReport.dataQuality.warnings -notcontains 'aggregate-timings-pool-multiple-session-epochs') {
    throw 'Epoch timing windows were merged or missing timing was invented.'
}
& (Join-Path $repo 'build/summarize-sync-diagnostics.ps1') -InputPath $emptyFile -OutputPath $outputFile
if (@((Get-Content -LiteralPath $outputFile -Raw | ConvertFrom-Json).epochs).Count -ne 0) {
    throw 'Empty input invented an epoch window.'
}
Write-Output 'PASS separate session/epoch timing windows and pooled-data warning.'

$epochFixture.records += @(
    @{ session='session-1'; epoch=1; player=2; pending=8; received=10; completedObserved=2; oldestPendingSequence=3; oldestPendingMs=900 },
    @{ session='session-1'; epoch=2; player=2; pending=1; received=2; completedObserved=1; oldestPendingSequence=2; oldestPendingMs=50 },
    @{ session='session-1'; epoch=1; player=2; pending=3; received=10; completedObserved=7; oldestPendingSequence=8; oldestPendingMs=1200 },
    @{ session='session-2'; epoch=1; player=2; pending=4 },
    @{ session='session-1'; player=2; pending=99 }
)
[IO.File]::WriteAllText($multiFile, ($epochFixture | ConvertTo-Json -Depth 5))
& (Join-Path $repo 'build/summarize-sync-diagnostics.ps1') -InputPath $multiFile -OutputPath $outputFile
$queueReport = Get-Content -LiteralPath $outputFile -Raw | ConvertFrom-Json
$oldQueue = @($queueReport.epochQueues | Where-Object { $_.session -eq 'session-1' -and $_.epoch -eq 1 })
$newQueue = @($queueReport.epochQueues | Where-Object { $_.session -eq 'session-1' -and $_.epoch -eq 2 })
$otherQueue = @($queueReport.epochQueues | Where-Object session -eq 'session-2')
if ($queueReport.epochQueues.Count -ne 3 -or $oldQueue.Count -ne 1 -or $newQueue.Count -ne 1 -or
    $oldQueue[0].peakSampledPending -ne 8 -or $oldQueue[0].lastSampledPending -ne 3 -or
    $oldQueue[0].lastSampledOldestPendingSequence -ne 8 -or $oldQueue[0].lastSampledOldestPendingMs -ne 1200 -or
    $oldQueue[0].lastSampledCompletedObserved -ne 7 -or $newQueue[0].peakSampledPending -ne 1 -or
    $newQueue[0].lastSampledReceived -ne 2 -or $null -ne $otherQueue[0].lastSampledOldestPendingMs) {
    throw 'Epoch queue samples were merged, lost, or assigned fictitious values.'
}
& (Join-Path $repo 'build/summarize-sync-diagnostics.ps1') -InputPath $emptyFile -OutputPath $outputFile
if (@((Get-Content -LiteralPath $outputFile -Raw | ConvertFrom-Json).epochQueues).Count -ne 0) {
    throw 'Empty input invented queue samples.'
}
Write-Output 'PASS epoch queue isolation, unknown epoch exclusion and missing sample preservation.'

if ($queueReport.dataQuality.queueSummarySamples -ne 5 -or
    $queueReport.dataQuality.queueSummarySamplesWithoutEpoch -ne 1 -or
    $queueReport.dataQuality.warnings -notcontains 'queue-summary-samples-missing-epoch') {
    throw 'Legacy queue samples without epoch were silently excluded.'
}
$emptyQueues = Get-Content -LiteralPath $outputFile -Raw | ConvertFrom-Json
if ($null -ne $emptyQueues.peakSampledPendingPerPeer -or
    $emptyQueues.dataQuality.queueSummarySamples -ne 0 -or
    $emptyQueues.dataQuality.warnings -notcontains 'no-queue-summary-samples') {
    throw 'Missing queue samples were reported as an empty queue.'
}
$zeroFixture = @{ schema=1; truncatedRecords=0; records=@(
    @{ session='session-1'; epoch=1; player=2; pending=0 }
) }
[IO.File]::WriteAllText($multiFile, ($zeroFixture | ConvertTo-Json -Depth 5))
& (Join-Path $repo 'build/summarize-sync-diagnostics.ps1') -InputPath $multiFile -OutputPath $outputFile
$zeroQueue = Get-Content -LiteralPath $outputFile -Raw | ConvertFrom-Json
if ($null -eq $zeroQueue.peakSampledPendingPerPeer -or $zeroQueue.peakSampledPendingPerPeer -ne 0 -or
    $zeroQueue.dataQuality.queueSummarySamples -ne 1 -or
    $zeroQueue.dataQuality.warnings -contains 'no-queue-summary-samples' -or
    $zeroQueue.dataQuality.warnings -contains 'queue-summary-samples-missing-epoch') {
    throw 'An observed empty queue was confused with missing observations.'
}
Write-Output 'PASS unknown queue peak, observed zero and explicit missing-epoch coverage.'

$reasonFixture = @{ schema=1; truncatedRecords=0; records=@(
    @{ session='session-1'; epoch=1; sequence=1; player=2; stage='retry'; reasonCode='native-net-commit-lost' },
    @{ session='session-1'; epoch=1; sequence=1; player=2; stage='retry'; reasonCode='native-net-commit-lost' },
    @{ session='session-1'; epoch=1; sequence=2; player=2; stage='retry'; reasonCode='route-commit-lost' },
    @{ session='session-1'; epoch=1; sequence=2; player=2; stage='completed'; reasonCode='route-update-graph-committed' },
    @{ session='session-1'; epoch=2; sequence=1; player=2; stage='rejected'; reasonCode='PrivateUser C:\\private\\file' },
    @{ session='session-2'; epoch=1; sequence=1; player=2; stage='retry' }
) }
[IO.File]::WriteAllText($multiFile, ($reasonFixture | ConvertTo-Json -Depth 5))
& (Join-Path $repo 'build/summarize-sync-diagnostics.ps1') -InputPath $multiFile -OutputPath $outputFile
$reasonJson = Get-Content -LiteralPath $outputFile -Raw
$reasons = ($reasonJson | ConvertFrom-Json).reasonOutcomes
$retryReason = @($reasons | Where-Object reasonCode -eq 'native-net-commit-lost')
$successReason = @($reasons | Where-Object reasonCode -eq 'route-update-graph-committed')
if ($reasons.Count -ne 4 -or $retryReason.Count -ne 1 -or $retryReason[0].uniqueObservedOperations -ne 1 -or
    $successReason.Count -ne 1 -or $successReason[0].stage -ne 'completed' -or
    @($reasons | Where-Object reasonCode -eq 'route-commit-lost').Count -ne 0 -or
    @($reasons | Where-Object reasonCode -eq 'unknown').Count -ne 2 -or $reasonJson.Contains('PrivateUser')) {
    throw 'Reason outcomes leaked text, duplicated retries, or retained a superseded failure.'
}
Write-Output 'PASS allowlisted reason outcomes, deduplication, completion and epoch/session isolation.'

# Invalid input must fail without replacing a previously generated report.
$preservedOutput = Get-Content -LiteralPath $outputFile -Raw
$invalidInputs = @(
    '{"schema":"1","truncatedRecords":0,"records":[]}',
    '{"schema":1,"truncatedRecords":0}',
    '{"schema":1,"truncatedRecords":0,"records":{}}',
    '{"schema":1,"truncatedRecords":-1,"records":[]}',
    '{"schema":1,"truncatedRecords":0,"records":[null]}',
    '{"schema":1,"truncatedRecords":0,"records":[{"session":"PrivateUser"}]}',
    '{"schema":1,"truncatedRecords":0,"records":[{"session":"session-1","queueMs":-1}]}',
    '{"schema":1,"truncatedRecords":0,"records":[{"session":"session-1","applyMs":1.5}]}',
    '{"schema":1,"truncatedRecords":0,"records":[{"session":"session-1","pending":"2"}]}',
    '{"schema":1,"truncatedRecords":0,"records":[{"session":"session-1","epoch":true}]}'
)
foreach ($invalid in $invalidInputs) {
    [IO.File]::WriteAllText($multiFile, $invalid)
    $rejected = $false
    try { & (Join-Path $repo 'build/summarize-sync-diagnostics.ps1') -InputPath $multiFile -OutputPath $outputFile }
    catch { $rejected = $true }
    if (!$rejected -or (Get-Content -LiteralPath $outputFile -Raw) -cne $preservedOutput) {
        throw 'Malformed diagnostics were accepted or replaced an existing report.'
    }
}
Write-Output 'PASS invalid shape, private identity, numeric types and existing-output preservation.'
