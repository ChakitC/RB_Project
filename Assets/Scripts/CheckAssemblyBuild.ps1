Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-NormalizedPath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if (-not $fullPath.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $fullPath += [System.IO.Path]::DirectorySeparatorChar
    }

    return $fullPath
}

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$csprojPath = Join-Path $projectRoot "Assembly-CSharp.csproj"
$buildRoot = Join-Path $projectRoot "Temp\CodexBuild\Assembly-CSharp"

$baseIntermediatePath = Get-NormalizedPath (Join-Path $buildRoot "base")
$intermediatePath = Get-NormalizedPath (Join-Path $buildRoot "obj")
$outputPath = Get-NormalizedPath (Join-Path $buildRoot "bin")

New-Item -ItemType Directory -Force -Path $baseIntermediatePath | Out-Null
New-Item -ItemType Directory -Force -Path $intermediatePath | Out-Null
New-Item -ItemType Directory -Force -Path $outputPath | Out-Null

$buildArgs = @(
    "build"
    $csprojPath
    "-m:1"
    "-p:BaseIntermediateOutputPath=$baseIntermediatePath"
    "-p:IntermediateOutputPath=$intermediatePath"
    "-p:OutputPath=$outputPath"
    "-nologo"
)

Write-Host "dotnet $($buildArgs -join ' ')"
& dotnet @buildArgs
exit $LASTEXITCODE
