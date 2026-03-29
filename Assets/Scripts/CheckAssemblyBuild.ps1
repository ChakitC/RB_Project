param(
    [string] $ArtifactRoot
)

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
$assetsRoot = Get-NormalizedPath (Join-Path $projectRoot "Assets")
$csprojPath = Join-Path $projectRoot "Assembly-CSharp.csproj"

if ([string]::IsNullOrWhiteSpace($ArtifactRoot)) {
    $ArtifactRoot = $env:RB_ASSEMBLY_BUILD_ARTIFACT_ROOT
}

if ([string]::IsNullOrWhiteSpace($ArtifactRoot)) {
    $ArtifactRoot = Join-Path $projectRoot "..\BuildArtifacts"
}

$artifactRoot = Get-NormalizedPath $ArtifactRoot

if ($artifactRoot.StartsWith($assetsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Artifact root must be outside Assets so Unity does not import generated assemblies: $artifactRoot"
}

$buildRoot = Join-Path $artifactRoot "Assembly-CSharp"

$pollutingArtifactPaths = @(
    (Join-Path $PSScriptRoot "_buildbin"),
    (Join-Path $PSScriptRoot "_buildobj")
)
$pollutingArtifacts = @($pollutingArtifactPaths | Where-Object { Test-Path -LiteralPath $_ })

if ($pollutingArtifacts.Count -gt 0) {
    $artifactList = ($pollutingArtifacts | ForEach-Object { " - $_" }) -join [System.Environment]::NewLine
    throw "Found build artifact folders inside Assets/Scripts. Unity can import these DLLs back into the project and cause duplicate assembly errors. Move or delete them before building:`n$artifactList"
}

$baseIntermediatePath = Get-NormalizedPath (Join-Path $buildRoot "base")
$intermediatePath = Get-NormalizedPath (Join-Path $buildRoot "obj")
$outputPath = Get-NormalizedPath (Join-Path $buildRoot "bin")

New-Item -ItemType Directory -Force -Path $baseIntermediatePath | Out-Null
New-Item -ItemType Directory -Force -Path $intermediatePath | Out-Null
New-Item -ItemType Directory -Force -Path $outputPath | Out-Null

Write-Host "Artifact root: $artifactRoot"

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
