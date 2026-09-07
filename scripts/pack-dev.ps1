param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    # The rehearsal is a folder, not an archive: drop it into Mods/ and run the game. -Zip exists for
    # the cases where a folder actually has to travel as one file.
    [switch]$Zip,
    # The NuGet package is a compile-time convenience for repositories on this machine; consumers bind
    # to the payload by sibling path, and a feed is deliberately on hold (MEMORY). So it is opt-in.
    [switch]$Nupkg
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Dev channel: the thinnest thing that can be installed. No identity gate, no archive, no package
# feed — those belong to the channels that publish. All the staging rules live in stage-package.ps1;
# this file decides only what a dev artifact is called and where it goes.

$root = [System.IO.Path]::GetFullPath($ProjectRoot)
$projectFile = Join-Path $root 'Source\FerriteLib.UiKit\FerriteLib.UiKit.csproj'
$stageDir = Join-Path $root 'dist\dev\FerriteLib'

# Build first, and force it: Dev and Release share one OutputPath (1.6/Assemblies/), so an incremental
# Release build can report itself up to date against obj\Release while the file at the payload path was
# written by the Dev gate. Measured 2026-09-07: the payload was a Dev-configuration assembly of 98,304
# bytes after a full verify-local run, where a forced Release rebuild of the same commit is 91,136.
# Copying whatever sits there would stage the wrong bytes under a label that still reads dev+sha, so
# nothing downstream could tell. Determinism makes forcing the rebuild cheap (MEMORY).
& dotnet build $projectFile -c Release --no-incremental --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }

# The label comes from the build axis, exactly like the artifact name in pack-release: one source for
# the number, or the folder and the DLL inside it can disagree.
$prefixMatch = Select-String -LiteralPath $projectFile -Pattern '<VersionPrefix>(.*?)</VersionPrefix>'
if (-not $prefixMatch) { throw 'csproj carries no <VersionPrefix>; the build axis has no home.' }
$label = $prefixMatch.Matches[0].Groups[1].Value.Trim() + '-dev'

$shortCommit = (& git -C $root rev-parse --short=12 HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or -not $shortCommit) { throw 'Cannot resolve the current commit for the dev label.' }

# A dev folder built from uncommitted work is normal; it just has to say so. The suffix is the reason
# an in-game report can be traced to a tree that no longer exists.
$dirty = @(& git -C $root status --porcelain --untracked-files=normal)
if ($LASTEXITCODE -ne 0) { throw 'git status failed; cannot label the dev artifact.' }
if ($dirty.Count -gt 0) { $shortCommit += '-dirty' }

$stageArgs = @{
    ProjectRoot  = $root
    StageDir     = $stageDir
    VersionLabel = $label
    BuildFlavor  = 'dev'
    CommitLabel  = $shortCommit
}
if ($Zip) { $stageArgs.CreateZip = $true }

& (Join-Path $PSScriptRoot 'stage-package.ps1') @stageArgs
if ($LASTEXITCODE -ne 0) { throw "stage-package failed with exit code $LASTEXITCODE." }

Write-Host "[pack-dev] install by copying the folder into Mods/: $stageDir"

if ($Nupkg) {
    & dotnet pack $projectFile -c Release --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'dotnet pack failed.' }
    Write-Host "[pack-dev] nupkg -> $root\artifacts\package (not published anywhere; see MEMORY for why a feed is on hold)"
}
