param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    # Stage only: build the payload and lay out the installable mod folder, but write no archive.
    # A rehearsal you drop straight into Mods/ for an in-game pass does not need a zip around it,
    # and skipping the compressor also skips the whole timestamp-in-the-archive problem that
    # pack-release.ps1 had to solve (see MEMORY). The nupkg goes too: it is a zip container, and a
    # consumer reference package is not what a directory rehearsal is for.
    [switch]$StageOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Produces both FerriteLib artifacts from one build, in the order that keeps them honest:
#   1. the mod payload folder (dist/dev/FerriteLib/...), which is what a player installs, and
#   2. the NuGet package (artifacts/package/...), which is what a consumer mod compiles against.
#
# They come out of the SAME build of the SAME csproj. That is deliberate: if the packaged reference
# assembly were built separately from the shipped payload, a consumer could compile against one and
# bind to the other, and the version contract would be checking a number nobody can tie to a DLL.

$root = [System.IO.Path]::GetFullPath($ProjectRoot)
$projectFile = Join-Path $root 'Source\FerriteLib.UiKit\FerriteLib.UiKit.csproj'
$distDir = Join-Path $root 'dist\dev'
$stageDir = Join-Path $distDir 'FerriteLib'
$payloadDir = Join-Path $root '1.6\Assemblies'

# --no-incremental is load-bearing, not belt-and-braces: Dev and Release share one OutputPath
# (1.6/Assemblies/), so an incremental Release build can declare itself up to date against obj\Release
# while the file actually sitting at the payload path was written by the Dev gate. Measured on
# 2026-09-07: right after a full verify-local -PackDev run, 1.6/Assemblies/FerriteLib.UiKit.dll was a
# Dev-configuration assembly of 98,304 bytes, while a forced Release rebuild produces 91,136. Copying
# whatever is there would stage the wrong payload under a label that still reads dev+sha, so nothing
# downstream could tell. Rebuilding is safe to force: same-path and clone-path Release builds of one
# commit hash identically (see MEMORY).
& dotnet build $projectFile -c Release --no-incremental --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "Release build failed." }

if (-not (Test-Path -LiteralPath (Join-Path $payloadDir 'FerriteLib.UiKit.dll') -PathType Leaf)) {
    throw "Build produced no payload at $payloadDir"
}

# --- mod package -------------------------------------------------------------
if (Test-Path -LiteralPath $stageDir) { Remove-Item -LiteralPath $stageDir -Recurse -Force }
$null = New-Item -ItemType Directory -Path (Join-Path $stageDir '1.6\Assemblies') -Force
$null = New-Item -ItemType Directory -Path (Join-Path $stageDir 'About') -Force
Copy-Item -LiteralPath (Join-Path $root 'About\About.xml') -Destination (Join-Path $stageDir 'About\About.xml') -Force
Copy-Item -LiteralPath (Join-Path $root 'LoadFolders.xml') -Destination (Join-Path $stageDir 'LoadFolders.xml') -Force
Copy-Item -LiteralPath (Join-Path $payloadDir 'FerriteLib.UiKit.dll') -Destination (Join-Path $stageDir '1.6\Assemblies\FerriteLib.UiKit.dll') -Force

# MPL-2.0 section 3.2: a distributed Executable Form must say how to obtain the Source Code Form, so
# the licence text travels inside the mod package rather than living only in the repository.
$licenseSource = Join-Path $root 'LICENSE'
if (-not (Test-Path -LiteralPath $licenseSource -PathType Leaf)) {
    throw "Missing LICENSE: the carrier package is MPL-2.0 covered and must ship the licence text."
}
Copy-Item -LiteralPath $licenseSource -Destination (Join-Path $stageDir 'LICENSE') -Force

$label = (Select-String -LiteralPath $projectFile -Pattern '<VersionPrefix>(.*?)</VersionPrefix>').Matches[0].Groups[1].Value
$suffixNode = (Select-String -LiteralPath $projectFile -Pattern '<VersionSuffix>(.*?)</VersionSuffix>')
if ($suffixNode) { $label = "$label-$($suffixNode.Matches[0].Groups[1].Value)" }
# The source pointer is MPL-2.0 3.2's requirement on an Executable Form; see pack-release.ps1 for the
# full note. Dev packages are local rehearsals, but they ship the same shape as the release asset.
[System.IO.File]::WriteAllText((Join-Path $stageDir 'version.txt'), "FerriteLib $label`r`nsource https://github.com/Coahuilite/FerriteLib`r`n")

$pdbs = @(Get-ChildItem -LiteralPath $stageDir -Recurse -File -Filter '*.pdb')
if ($pdbs.Count -gt 0) { $pdbs | Remove-Item -Force }

if ($StageOnly) {
    Write-Host "[pack-dev] staged mod folder -> $stageDir"
    Write-Host '[pack-dev] -StageOnly: no zip, no nupkg (directory rehearsal only)'
}
else {
    $zipPath = Join-Path $distDir "FerriteLib-dev-v$label.zip"
    if (Test-Path -LiteralPath $zipPath -PathType Leaf) { Remove-Item -LiteralPath $zipPath -Force }
    Compress-Archive -Path (Join-Path $stageDir '*') -DestinationPath $zipPath
    Write-Host "[pack-dev] staged mod package -> $stageDir"
    Write-Host "[pack-dev] zip -> $zipPath"

    # --- consumer reference package ---------------------------------------------
    & dotnet pack $projectFile -c Release --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed." }
    Write-Host "[pack-dev] nupkg -> $root\artifacts\package (not published anywhere; see MEMORY for why a feed is on hold)"
}
