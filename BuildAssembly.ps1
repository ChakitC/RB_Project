param(
    [string] $ArtifactRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptPath = Join-Path $PSScriptRoot "Assets\Scripts\CheckAssemblyBuild.ps1"

if (-not (Test-Path $scriptPath)) {
    throw "Build script not found: $scriptPath"
}

if ([string]::IsNullOrWhiteSpace($ArtifactRoot)) {
    & $scriptPath
}
else {
    & $scriptPath -ArtifactRoot $ArtifactRoot
}

exit $LASTEXITCODE
