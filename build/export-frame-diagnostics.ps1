param(
    [Parameter(Mandatory = $true)][string]$LogPath,
    [string]$OutputPath = (Join-Path $PSScriptRoot 'reports/frame-diagnostics.json')
)
$ErrorActionPreference = 'Stop'
$windows = [Collections.Generic.Queue[object]]::new()
$runs = @{}
$invalid = 0
$dropped = 0
$pattern = '\[perf\] (?:frameScope=(?:singleplayer|host|client) )?Frames/(\d+)s: (\d+) \((\d+)/s, mean (\d+) ms, worst (\d+) ms\) <=17ms:(\d+) <=33:(\d+) <=50:(\d+) <=100:(\d+) <=250:(\d+) <=500:(\d+) >500:(\d+)\s*$'
foreach ($line in [IO.File]::ReadLines((Resolve-Path -LiteralPath $LogPath).Path)) {
    if ($line -notmatch '\[perf\] (?:frameScope=\S+ )?Frames/') { continue }
    if ($line -notmatch $pattern) { $invalid++; continue }
    $values = [Collections.Generic.List[long]]::new()
    $valid = $true
    for ($i = 1; $i -le 12; $i++) {
        $number = 0L
        if (![long]::TryParse($Matches[$i], [ref]$number) -or $number -gt [int]::MaxValue) { $valid = $false; break }
        $values.Add($number)
    }
    if (!$valid) { $invalid++; continue }
    $sum = 0L
    for ($i = 5; $i -lt 12; $i++) { $sum += $values[$i] }
    if ($values[0] -eq 0 -or $values[1] -eq 0 -or $sum -ne $values[1] -or $values[4] -lt $values[3] -or
        $values[2] -ne [Math]::Floor($values[1] / $values[0])) { $invalid++; continue }
    $run = $null
    if ($line -match '(?:^| )run=([a-f0-9]{8})(?: |$)') {
        $id = $Matches[1]
        if (!$runs.ContainsKey($id)) {
            if ($runs.Count -ge 128) { $dropped++; continue }
            $runs[$id] = 'run-' + ($runs.Count + 1)
        }
        $run = $runs[$id]
    }
    $scope = if ($line -match '\[perf\] frameScope=(singleplayer|host|client) Frames/') { $Matches[1] } else { $null }
    $window = [ordered]@{
        frameScope = $scope;
        run = $run; durationSecondsRoundedDown = $values[0]; frames = $values[1];
        framesPerSecondRoundedDown = $values[2]; meanMsRoundedDown = $values[3]; worstMs = $values[4];
        bucketCounts = @($values[5], $values[6], $values[7], $values[8], $values[9], $values[10], $values[11])
    }
    if ($windows.Count -eq 20000) { $null = $windows.Dequeue(); $dropped++ }
    $windows.Enqueue($window)
}
$report = [ordered]@{
    schema = 1; windows = @($windows.ToArray()); rejectedWindows = $invalid; truncatedWindows = $dropped;
    bucketIntervalsMs = @('[0,17]', '(17,33]', '(33,50]', '(50,100]', '(100,250]', '(250,500]', '(500,infinity)');
    scope = 'Logged frame-interval windows, not isolated mod CPU cost or cross-machine latency. Buckets are disjoint, not cumulative. No exact quantiles inferred. Run aliases are local to this export. Scenario, save, package and completeness are not verified.'
}
$target = [IO.Path]::GetFullPath($OutputPath)
$null = New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force
[IO.File]::WriteAllText($target, ($report | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
Write-Output "Exported $($windows.Count) frame windows; rejected $invalid; omitted $dropped."
