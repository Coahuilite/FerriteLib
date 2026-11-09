param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Version,
    # Local rehearsal only. The release workflow never passes it, so a published asset is always
    # attributable to a commit; without the flag this is exactly the CI path.
    [switch]$AllowDirtyTree,
    [string]$StageDir = 'dist/github/FerriteLib'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# GitHub channel: the mod package a player installs, and nothing else. Staging, the content probe, the
# licence copy, version.txt and the deterministic archive all belong to stage-package.ps1, which the
# dev and steam channels run through too — so what is published is the same tree the Workshop folder
# gets, minus the archive.
#
# What stays here is the identity of a release, in the order it can still be fixed:
#   1. the tag must be a release or an rc, and only a tag;
#   2. the build axis must name that version, or the page lies about its own artifact;
#   3. the tree must be clean and the payload must have been built from that commit.
#
# Deliberately absent: no NuGet package. The .nupkg is a compile-time convenience for repositories on
# this machine; publishing it beside the mod zip invites a third party to copy FerriteLib.UiKit.dll into
# their own package, which is the single-carrier failure the whole version contract exists to detect.

$root = [System.IO.Path]::GetFullPath($ProjectRoot)
$projectFile = Join-Path $root 'Source\FerriteLib.UiKit\FerriteLib.UiKit.csproj'
$payloadDll = Join-Path $root 'dist\build\Release\FerriteLib.UiKit.dll'
$stageDir = [IO.Path]::GetFullPath($StageDir, $root)

Import-Module (Join-Path $PSScriptRoot 'Release.Identity.psm1')
$releaseVersion = Get-FerriteReleaseVersion -Version $Version
Assert-FerriteReleaseAxis -ProjectFile $projectFile -BaseVersion $releaseVersion.Base
$commit = Get-FerriteReleaseCommit -Root $root -PayloadDll $payloadDll -Channel github -AllowDirtyTree:$AllowDirtyTree

$stageArgs = @{
    ProjectRoot            = $root
    StageDir               = $stageDir
    VersionLabel           = $Version
    BuildFlavor            = 'github'
    CommitLabel            = $commit.Substring(0, 12)
    RequireReleaseIdentity = $true
    CreateZip              = $true
}
& (Join-Path $PSScriptRoot 'stage-package.ps1') @stageArgs
if ($LASTEXITCODE -ne 0) { throw "stage-package failed with exit code $LASTEXITCODE." }

# The digest the staging step prints is what the release body quotes and what a consumer's CI verifies
# against, so it has exactly one canonical copy: the page that owns the binary.
Write-Host "[pack-release] commit -> $($commit.Substring(0, 12))"
