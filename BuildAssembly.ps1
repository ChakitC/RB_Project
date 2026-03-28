Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptPath = Join-Path $PSScriptRoot "Assets\Scripts\CheckAssemblyBuild.ps1"

if (-not (Test-Path $scriptPath)) {
    throw "Build script not found: $scriptPath"
}

& $scriptPath
exit $LASTEXITCODE
