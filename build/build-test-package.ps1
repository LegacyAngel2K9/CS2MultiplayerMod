param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$stageRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'staging'))
$stageMods = Join-Path $stageRoot 'Mods'
$modFolder = Join-Path $stageMods 'CS2MPMod'
$packageRoot = Join-Path $PSScriptRoot 'packages'
$logPath = Join-Path $PSScriptRoot 'validation-build.log'

# Mod.targets clears its deployment directory. Constrain that target to this repo.
if (!$modFolder.StartsWith($repoRoot + [IO.Path]::DirectorySeparatorChar,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The staging target must stay inside the repository.'
}

$originalUserData = [Environment]::GetEnvironmentVariable('CSII_USERDATAPATH', 'Process')
Push-Location $repoRoot
try {
    & (Join-Path $PSScriptRoot 'test-build-environment.ps1')
    $manifestManagedPath = [Environment]::GetEnvironmentVariable('CSII_MANAGEDPATH', 'Process')
    if ([string]::IsNullOrWhiteSpace($manifestManagedPath)) {
        $manifestManagedPath = [Environment]::GetEnvironmentVariable('CSII_MANAGEDPATH', 'User')
    }
    & dotnet run --project tests/CS2MPMod.Core.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Core regression checks failed; no package was built.' }
    & (Join-Path $repoRoot 'tests/test-diagnostic-summary.ps1')
    & (Join-Path $repoRoot 'tests/test-received-report.ps1')
    & (Join-Path $repoRoot 'tests/test-frame-export.ps1')
    & (Join-Path $repoRoot 'tests/test-installed-package.ps1')

    $null = New-Item -ItemType Directory -Path $stageMods -Force
    # Both C# deployment and webpack must use the same staging root. The installed
    # mod and the user's persistent toolchain variables are left untouched.
    $env:CSII_USERDATAPATH = $stageRoot
    Push-Location (Join-Path $repoRoot 'CS2MPMod/UI')
    try {
        & npm ci --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) { throw 'Locked UI dependency installation failed.' }
    } finally { Pop-Location }
    & dotnet build CS2MPMod.sln -c $Configuration --disable-build-servers "-p:LocalModsPath=$stageMods" 2>&1 |
        Tee-Object -FilePath $logPath
    if ($LASTEXITCODE -ne 0) { throw "Full mod build failed. See $logPath" }

    foreach ($file in @('CS2MPMod.dll', 'CS2MPMod.Steam.dll', 'CS2MPMod.mjs')) {
        $required = Join-Path $modFolder $file
        if (!(Test-Path -LiteralPath $required) -or (Get-Item -LiteralPath $required).Length -eq 0) {
            throw "Incomplete mod package: missing $file"
        }
    }
    Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE') -Destination (Join-Path $modFolder 'LICENSE')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'TEST-PACKAGE.md') -Destination (Join-Path $modFolder 'TEST-PACKAGE.md')

    $protocolSource = Get-Content -LiteralPath 'CS2MPMod/Core/Protocol/ProtocolConstants.cs' -Raw
    $protocolMatch = [regex]::Match($protocolSource, 'ProtocolVersion\s*=\s*(\d+)')
    if (!$protocolMatch.Success) { throw 'Cannot determine package protocol version.' }
    $protocol = [int]$protocolMatch.Groups[1].Value
    $files = @(Get-ChildItem -LiteralPath $modFolder -File -Recurse |
        Where-Object { $_.Name -ne 'build-manifest.json' } |
        Sort-Object FullName | ForEach-Object {
            [ordered]@{
                path = $_.FullName.Substring($modFolder.Length + 1).Replace('\', '/')
                bytes = $_.Length
                sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
            }
        })
    $manifest = [ordered]@{
        mod = 'CS2MPMod'
        protocol = $protocol
        configuration = $Configuration
        builtUtc = [DateTime]::UtcNow.ToString('o')
        coreRegressionChecks = 'passed'
        multiplayerInGameVerified = $false
        gameAssemblySha256 = (Get-FileHash -LiteralPath (Join-Path $manifestManagedPath 'Game.dll') -Algorithm SHA256).Hash
        files = $files
    } | ConvertTo-Json -Depth 5
    [IO.File]::WriteAllText((Join-Path $modFolder 'build-manifest.json'), $manifest,
        [Text.UTF8Encoding]::new($false))
    $null = New-Item -ItemType Directory -Path $packageRoot -Force
    $packagePath = Join-Path $packageRoot "CS2MPMod-protocol$protocol-test.zip"
    Compress-Archive -LiteralPath $modFolder -DestinationPath $packagePath -Force
    Write-Output "Verified local test package: $packagePath"
    Write-Output 'Not installed or published. Host and clients must use the same complete package.'
}
finally {
    [Environment]::SetEnvironmentVariable('CSII_USERDATAPATH', $originalUserData, 'Process')
    Pop-Location
}
