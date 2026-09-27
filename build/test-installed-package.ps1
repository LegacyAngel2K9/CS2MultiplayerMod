param(
    [string]$UserDataPath = (Join-Path $env:USERPROFILE 'AppData/LocalLow/Colossal Order/Cities Skylines II')
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $UserDataPath).Path
$installed = Join-Path $root 'Mods/CS2MPMod'
$expectedDll = [IO.Path]::GetFullPath((Join-Path $installed 'CS2MPMod.dll'))
# The game's asset database scans beyond Mods, including backup folders in user data.
$candidates = @(Get-ChildItem -LiteralPath $root -Filter 'CS2MPMod.dll' -File -Recurse)
if ($candidates.Count -ne 1 -or $candidates[0].FullName -ine $expectedDll) {
    throw 'Expected exactly one CS2MPMod.dll under user data, in Mods/CS2MPMod. Move backups outside the entire game user-data directory.'
}
$manifestPath = Join-Path $installed 'build-manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.mod -ne 'CS2MPMod' -or $manifest.files -isnot [array] -or $manifest.files.Count -eq 0) {
    throw 'Invalid installed package manifest.'
}
foreach ($entry in $manifest.files) {
    $path = [IO.Path]::GetFullPath((Join-Path $installed $entry.path))
    if (!$path.StartsWith([IO.Path]::GetFullPath($installed) + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) { throw 'Manifest path escapes the installed package.' }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256) {
        throw 'Installed package checksum mismatch.'
    }
}
Write-Output "PASS installed package: protocol $($manifest.protocol), $($manifest.files.Count) verified files, no duplicate CS2MPMod.dll under game user data. Runtime version still requires startup-log verification."
