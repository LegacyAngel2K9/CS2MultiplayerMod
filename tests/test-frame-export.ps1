$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$root = Join-Path $repo 'build/reports'
$null = New-Item -ItemType Directory -Path $root -Force
$inputFile = Join-Path $root 'frame-fixture.log'
$outputFile = Join-Path $root 'frame-fixture.json'
$valid = 'run=aaaaaaaa PrivateUser C:\private 192.0.2.1 [perf] Frames/30s: 211 (7/s, mean 142 ms, worst 310 ms) <=17ms:0 <=33:0 <=50:0 <=100:22 <=250:185 <=500:4 >500:0'
[IO.File]::WriteAllLines($inputFile, @($valid, $valid.Replace('aaaaaaaa','bbbbbbbb'),
    $valid.Replace('<=100:22','<=100:23'), $valid.Replace('Frames/30s','Frames/0s'),
    $valid.Replace('211 (','999999999999999999999999 ('), 'unrelated private text'))
& (Join-Path $repo 'build/export-frame-diagnostics.ps1') -LogPath $inputFile -OutputPath $outputFile
$json = Get-Content -LiteralPath $outputFile -Raw
$report = $json | ConvertFrom-Json
if ($report.windows.Count -ne 2 -or $report.rejectedWindows -ne 3 -or
    $report.windows[0].run -ne 'run-1' -or $report.windows[1].run -ne 'run-2' -or
    $report.windows[0].bucketCounts[4] -ne 185 -or $report.windows[0].frames -ne 211) {
    throw 'Frame export misread histogram or accepted invalid windows.'
}
foreach ($private in @('PrivateUser','C:\private','192.0.2.1','aaaaaaaa','bbbbbbbb')) {
    if ($json.Contains($private)) { throw 'Frame export leaked private input.' }
}
[IO.File]::WriteAllText($inputFile, '')
& (Join-Path $repo 'build/export-frame-diagnostics.ps1') -LogPath $inputFile -OutputPath $outputFile
if (@((Get-Content -LiteralPath $outputFile -Raw | ConvertFrom-Json).windows).Count -ne 0) {
    throw 'Empty log invented frame measurements.'
}
Write-Output 'PASS frame histogram export, invalid windows, private-data filtering and empty input.'

[IO.File]::WriteAllLines($inputFile, @($valid,
    $valid.Replace('[perf] Frames/', '[perf] frameScope=singleplayer Frames/'),
    $valid.Replace('[perf] Frames/', '[perf] frameScope=host Frames/'),
    $valid.Replace('[perf] Frames/', '[perf] frameScope=client Frames/'),
    $valid.Replace('[perf] Frames/', '[perf] frameScope=PrivateUser Frames/')))
& (Join-Path $repo 'build/export-frame-diagnostics.ps1') -LogPath $inputFile -OutputPath $outputFile
$scoped = Get-Content -LiteralPath $outputFile -Raw | ConvertFrom-Json
if ($scoped.windows.Count -ne 4 -or $scoped.rejectedWindows -ne 1 -or
    $null -ne $scoped.windows[0].frameScope -or $scoped.windows[1].frameScope -ne 'singleplayer' -or
    $scoped.windows[2].frameScope -ne 'host' -or $scoped.windows[3].frameScope -ne 'client') {
    throw 'Frame role classification lost or inferred an unknown role.'
}
Write-Output 'PASS singleplayer/host/client scope and unknown legacy scope.'
