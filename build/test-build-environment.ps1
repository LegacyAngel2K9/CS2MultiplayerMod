param([switch]$CoreOnly)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
function Read-Cs2Path([string]$Name) {
    $value = [Environment]::GetEnvironmentVariable($Name, 'Process')
    if ([string]::IsNullOrWhiteSpace($value)) {
        $value = [Environment]::GetEnvironmentVariable($Name, 'User')
    }
    if ([string]::IsNullOrWhiteSpace($value)) { throw "Missing $Name; run build/setup-cs2-build.ps1." }
    return $value
}
function Require-File([string]$Path) {
    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Required file missing: $Path" }
}
Push-Location $repo
try {
    $null = Get-Command dotnet -ErrorAction Stop
    $sdk = (& dotnet --version)
    if ($LASTEXITCODE -ne 0) { throw 'SDK from global.json is unavailable.' }
    $requiredSdk = (Get-Content global.json -Raw | ConvertFrom-Json).sdk.version
    if (($sdk -split '\.')[0] -ne ($requiredSdk -split '\.')[0] -or $sdk -match '-') {
        throw "Expected stable SDK family $requiredSdk, found $sdk."
    }
    if ($CoreOnly) { Write-Output "Core preflight OK: SDK $sdk"; return }
    $managed = Read-Cs2Path 'CSII_MANAGEDPATH'
    $tool = Read-Cs2Path 'CSII_TOOLPATH'
    foreach ($name in @('Game.dll', 'Unity.Entities.dll', 'UnityEngine.CoreModule.dll')) {
        Require-File (Join-Path $managed $name)
    }
    foreach ($name in @('Mod.props', 'Mod.targets')) { Require-File (Join-Path $tool $name) }
    $null = Get-Command node -ErrorAction Stop
    $null = Get-Command npm -ErrorAction Stop
    $nodeVersion = & node --version
    if ($LASTEXITCODE -ne 0 -or [int]($nodeVersion.TrimStart('v').Split('.')[0]) -lt 18) {
        throw 'Node.js >=18 is required.'
    }
    foreach ($name in @('package.json', 'package-lock.json', 'webpack.config.js', 'tools/css-presence.js')) {
        Require-File (Join-Path $repo "CS2MPMod/UI/$name")
    }
    & node -e 'const p=require("./CS2MPMod/UI/package.json"),l=require("./CS2MPMod/UI/package-lock.json"); if(l.lockfileVersion<2)process.exit(1); for(const k of ["dependencies","devDependencies"])if(JSON.stringify(p[k])!==JSON.stringify(l.packages[""][k]))process.exit(2);'
    if ($LASTEXITCODE -ne 0) { throw 'UI lockfile is unsupported or differs from package.json.' }
    Write-Output "Full preflight OK: SDK $sdk, Node $nodeVersion; toolchain and UI inputs present."
} finally { Pop-Location }
