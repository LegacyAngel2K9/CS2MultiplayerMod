$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$root = Join-Path $repo ('build/reports/install-fixture-' + [Guid]::NewGuid().ToString('N'))
$mod = Join-Path $root 'Mods/CS2MPMod'
$null = New-Item -ItemType Directory -Path $mod -Force
$dll = Join-Path $mod 'CS2MPMod.dll'
[IO.File]::WriteAllBytes($dll, [byte[]]@(1,2,3))
$manifest = @{ mod='CS2MPMod'; protocol=65; files=@(@{path='CS2MPMod.dll'; sha256=(Get-FileHash -LiteralPath $dll).Hash}) }
[IO.File]::WriteAllText((Join-Path $mod 'build-manifest.json'), ($manifest | ConvertTo-Json -Depth 5))
$check = Join-Path $repo 'build/test-installed-package.ps1'
& $check -UserDataPath $root
[IO.File]::WriteAllBytes($dll, [byte[]]@(4,5,6))
$rejected = $false
try { & $check -UserDataPath $root } catch { $rejected = $true }
if (!$rejected) { throw 'Modified installed DLL was accepted.' }
[IO.File]::WriteAllBytes($dll, [byte[]]@(1,2,3))
$backup = Join-Path $root 'ModBackups/old'
$null = New-Item -ItemType Directory -Path $backup -Force
Copy-Item -LiteralPath $dll -Destination $backup
$rejected = $false
try { & $check -UserDataPath $root } catch { $rejected = $true }
if (!$rejected) { throw 'Backup duplicate under user data was accepted.' }
Write-Output 'PASS installed package checks reject changed files and backups outside Mods but inside game user data.'
