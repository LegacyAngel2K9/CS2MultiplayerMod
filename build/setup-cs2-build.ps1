param(
    [string]$GameRoot
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    $candidates = @(
        [Environment]::GetEnvironmentVariable('CSII_INSTALLATIONPATH', 'Process'),
        [Environment]::GetEnvironmentVariable('CSII_INSTALLATIONPATH', 'User'),
        "${env:ProgramFiles(x86)}\Steam\steamapps\common\Cities Skylines II",
        "$env:ProgramFiles\Steam\steamapps\common\Cities Skylines II",
        "$env:ProgramFiles\WindowsApps"
    )
    $GameRoot = $candidates | Where-Object {
        ![string]::IsNullOrWhiteSpace($_) -and
            (Test-Path -LiteralPath (Join-Path $_ 'Cities2_Data\Managed\Game.dll'))
    } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($GameRoot) -or !(Test-Path $GameRoot)) {
    throw 'Cities: Skylines II was not found. Run this script with -GameRoot <path>.'
}

$managed = Join-Path $GameRoot 'Cities2_Data\Managed'
if (!(Test-Path (Join-Path $managed 'Game.dll'))) {
    throw "Game.dll was not found in '$managed'. Enable CS2 Modding in the game first."
}

$toolCandidates = @(
    [Environment]::GetEnvironmentVariable('CSII_TOOLPATH', 'Process'),
    [Environment]::GetEnvironmentVariable('CSII_TOOLPATH', 'User'),
    (Join-Path $GameRoot 'ModdingToolchain'),
    (Join-Path $GameRoot 'ModdingTools'),
    "$env:LOCALAPPDATA\Colossal Order\Cities Skylines II\ModdingToolchain",
    "$env:APPDATA\Colossal Order\Cities Skylines II\ModdingToolchain"
)
$tool = $toolCandidates | Where-Object {
    ![string]::IsNullOrWhiteSpace($_) -and
        (Test-Path -LiteralPath (Join-Path $_ 'Mod.props')) -and
        (Test-Path -LiteralPath (Join-Path $_ 'Mod.targets'))
} | Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($tool)) {
    throw 'Mod.props/Mod.targets were not found. Start Cities: Skylines II, open Options > Modding, enable modding, then run this script again.'
}

[Environment]::SetEnvironmentVariable('CSII_TOOLPATH', $tool, 'Process')
[Environment]::SetEnvironmentVariable('CSII_MANAGEDPATH', $managed, 'Process')
[Environment]::SetEnvironmentVariable('CSII_GAMEPATH', $GameRoot, 'Process')
try {
    [Environment]::SetEnvironmentVariable('CSII_TOOLPATH', $tool, 'User')
    [Environment]::SetEnvironmentVariable('CSII_MANAGEDPATH', $managed, 'User')
    [Environment]::SetEnvironmentVariable('CSII_GAMEPATH', $GameRoot, 'User')
} catch {
    Write-Warning 'User environment variables could not be written; use the printed values in the current shell or configure them manually.'
}

Write-Host "CSII_TOOLPATH=$tool"
Write-Host "CSII_MANAGEDPATH=$managed"
Write-Host 'Build environment configured. Restart Visual Studio or PowerShell before building.'
