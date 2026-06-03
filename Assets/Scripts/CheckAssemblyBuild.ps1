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

function Resolve-ProjectRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $ProjectRoot
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or $Path.Contains('$(')) {
        return $Path
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $ProjectRoot $Path))
}

function Test-IsUnderAnyRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string[]] $Roots
    )

    foreach ($root in $Roots) {
        if ($Path.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

function Get-AssetRelativeParts {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $AssetsRoot
    )

    $relativePath = $Path.Substring($AssetsRoot.Length).TrimStart(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar
    )

    return @($relativePath -split "[\\/]")
}

function Test-IsEditorSource {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $AssetsRoot
    )

    $parts = @(Get-AssetRelativeParts -Path $Path -AssetsRoot $AssetsRoot)
    return $parts -contains "Editor"
}

function Test-IsFirstPassSource {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $AssetsRoot
    )

    $parts = @(Get-AssetRelativeParts -Path $Path -AssetsRoot $AssetsRoot)
    if ($parts.Count -eq 0) {
        return $false
    }

    $firstPassRoots = @(
        "Plugins",
        "Standard Assets",
        "Pro Standard Assets"
    )

    return $firstPassRoots -contains $parts[0]
}

function Get-AssemblyCSharpSourceFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string] $AssetsRoot
    )

    $assetsRootWithoutTrailingSeparator = $AssetsRoot.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar
    )

    $asmdefRoots = @(Get-ChildItem -LiteralPath $assetsRootWithoutTrailingSeparator -Filter "*.asmdef" -Recurse -File |
        ForEach-Object { Get-NormalizedPath $_.DirectoryName })

    $sourceFiles = @(Get-ChildItem -LiteralPath $assetsRootWithoutTrailingSeparator -Filter "*.cs" -Recurse -File |
        ForEach-Object { [System.IO.Path]::GetFullPath($_.FullName) } |
        Where-Object {
            -not (Test-IsEditorSource -Path $_ -AssetsRoot $AssetsRoot) -and
            -not (Test-IsFirstPassSource -Path $_ -AssetsRoot $AssetsRoot) -and
            -not (Test-IsUnderAnyRoot -Path $_ -Roots $asmdefRoots)
        } |
        Sort-Object)

    if ($sourceFiles.Count -eq 0) {
        throw "No Assembly-CSharp source files found under Assets."
    }

    return $sourceFiles
}

function Save-XmlDocument {
    param(
        [Parameter(Mandatory = $true)]
        [xml] $Document,

        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    $settings = [System.Xml.XmlWriterSettings]::new()
    $settings.Indent = $true
    $settings.Encoding = [System.Text.UTF8Encoding]::new($false)

    $writer = [System.Xml.XmlWriter]::Create($Path, $settings)
    try {
        $Document.Save($writer)
    }
    finally {
        $writer.Close()
    }
}

function New-ScannedAssemblyCSharpProject {
    param(
        [Parameter(Mandatory = $true)]
        [string] $BaseProjectPath,

        [Parameter(Mandatory = $true)]
        [string] $ProjectRoot,

        [Parameter(Mandatory = $true)]
        [string[]] $SourceFiles,

        [Parameter(Mandatory = $true)]
        [string] $OutputProjectPath
    )

    [xml] $project = Get-Content -LiteralPath $BaseProjectPath
    $namespaceManager = [System.Xml.XmlNamespaceManager]::new($project.NameTable)
    $namespaceManager.AddNamespace("msb", $project.Project.NamespaceURI)

    foreach ($projectReference in @($project.SelectNodes("//msb:ProjectReference", $namespaceManager))) {
        $projectReference.Include = Resolve-ProjectRelativePath -Path $projectReference.Include -ProjectRoot $ProjectRoot
    }

    foreach ($analyzer in @($project.SelectNodes("//msb:Analyzer", $namespaceManager))) {
        $analyzer.Include = Resolve-ProjectRelativePath -Path $analyzer.Include -ProjectRoot $ProjectRoot
    }

    foreach ($hintPath in @($project.SelectNodes("//msb:HintPath", $namespaceManager))) {
        $hintPath.InnerText = Resolve-ProjectRelativePath -Path $hintPath.InnerText -ProjectRoot $ProjectRoot
    }

    $baseDirectory = $project.SelectSingleNode("//msb:BaseDirectory", $namespaceManager)
    if ($null -ne $baseDirectory) {
        $baseDirectory.InnerText = $ProjectRoot
    }

    foreach ($compile in @($project.SelectNodes("//msb:Compile", $namespaceManager))) {
        [void] $compile.ParentNode.RemoveChild($compile)
    }

    foreach ($emptyItemGroup in @($project.SelectNodes("//msb:ItemGroup[not(*)]", $namespaceManager))) {
        [void] $emptyItemGroup.ParentNode.RemoveChild($emptyItemGroup)
    }

    $namespaceUri = $project.Project.NamespaceURI
    $compileItemGroup = $project.CreateElement("ItemGroup", $namespaceUri)

    foreach ($sourceFile in $SourceFiles) {
        $compile = $project.CreateElement("Compile", $namespaceUri)
        $include = $project.CreateAttribute("Include")
        $include.Value = $sourceFile
        [void] $compile.Attributes.Append($include)
        [void] $compileItemGroup.AppendChild($compile)
    }

    [void] $project.Project.AppendChild($compileItemGroup)
    Save-XmlDocument -Document $project -Path $OutputProjectPath
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

$sourceFiles = @(Get-AssemblyCSharpSourceFiles -AssetsRoot $assetsRoot)
$temporaryCsprojPath = Join-Path $buildRoot "Assembly-CSharp.scanned.csproj"
New-ScannedAssemblyCSharpProject `
    -BaseProjectPath $csprojPath `
    -ProjectRoot $projectRoot `
    -SourceFiles $sourceFiles `
    -OutputProjectPath $temporaryCsprojPath

Write-Host "Artifact root: $artifactRoot"
Write-Host "Temporary project: $temporaryCsprojPath"
Write-Host "Assembly-CSharp source files: $($sourceFiles.Count)"

$buildArgs = @(
    "build"
    $temporaryCsprojPath
    "-m:1"
    "-p:BaseIntermediateOutputPath=$baseIntermediatePath"
    "-p:IntermediateOutputPath=$intermediatePath"
    "-p:OutputPath=$outputPath"
    "-nologo"
)

Write-Host "dotnet $($buildArgs -join ' ')"
& dotnet @buildArgs
exit $LASTEXITCODE
