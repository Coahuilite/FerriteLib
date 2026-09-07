param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Version,
    # Local rehearsal only. The release workflow never passes it, so a published asset is always
    # attributable to a commit; without the flag this is exactly the CI path.
    [switch]$AllowDirtyTree
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
$payloadDll = Join-Path $root '1.6\Assemblies\FerriteLib.UiKit.dll'
$stageDir = Join-Path $root 'dist\github\FerriteLib'

# --- 1. the tag shape: vBASE, or vBASE-rcN with N >= 1 ------------------------------------------
# The rc scheme is the trial mechanism (maintainer decision 2026-09-05): each iteration that reaches
# players takes the next number, and the final release must point at the same commit as the last rc. A
# tag in no legal form cannot be packed, so a mislabelled asset cannot reach the release page at all.
$tagPattern = '^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-rc([1-9]\d*))?$'
if ($Version -notmatch $tagPattern) {
    throw "Version must be vMAJOR.MINOR.PATCH or vMAJOR.MINOR.PATCH-rcN (N >= 1): $Version"
}
$baseVersion = "$($Matches[1]).$($Matches[2]).$($Matches[3])"

# --- 2. the build axis has to name the same number ------------------------------------------------
# pack-dev derives its label from <VersionPrefix> and this derives the release label from the tag; if
# the two disagree the zip is named after a version that was never built. FerriteLibVersionTests
# asserts the same agreement at gate time, and the payload-vs-label check inside stage-package.ps1
# asserts the DLL agrees too. What this line adds is the moment a number becomes public.
$prefixMatch = Select-String -LiteralPath $projectFile -Pattern '<VersionPrefix>(.*?)</VersionPrefix>'
if (-not $prefixMatch) { throw 'csproj carries no <VersionPrefix>; the build axis has no home.' }
$buildAxis = $prefixMatch.Matches[0].Groups[1].Value.Trim()
if ($buildAxis -ne $baseVersion) {
    throw "Release tag v$baseVersion does not match the build axis <VersionPrefix>$buildAxis. Bump the axes and rebuild, or retag; do not rename the artifact."
}

# --- 3. provenance: a published asset must name a tree someone can check out ---------------------
$commit = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or -not $commit) { throw 'Cannot resolve the current commit; refusing to publish an unattributable artifact.' }

$status = @(& git -C $root status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'git status failed; cannot prove the tree is clean.' }
if ($status.Count -gt 0 -and -not $AllowDirtyTree) {
    throw ("Working tree is dirty ({0} path(s)); a published asset must come from the exact commit it names. Commit them or pass -AllowDirtyTree for a local rehearsal." -f $status.Count)
}

if (-not (Test-Path -LiteralPath $payloadDll -PathType Leaf)) {
    throw "No payload at $payloadDll. Build the Release configuration first (see .github/workflows/release.yml)."
}

# AssemblyInformationalVersion carries the revision the SDK embedded at build time, so it is the only
# thing inside the artifact that says what it was compiled from. On the release runner the build runs on
# the tagged checkout and this matches by construction; locally it catches the ordinary mistake of
# packing a DLL built before the commit being tagged, which happened on 2026-09-05 when a mutation test
# left 1.6/Assemblies/ built from the previous commit. Skipped on the rehearsal path, where the tree is
# by definition not what gets published.
if (-not $AllowDirtyTree) {
    $informational = ([System.Diagnostics.FileVersionInfo]::GetVersionInfo($payloadDll)).ProductVersion
    if ($informational -notlike "*$commit*") {
        throw "Payload was built from a different commit than HEAD: its version resource says '$informational', HEAD is $commit. Rebuild after committing, or pass -AllowDirtyTree for a local rehearsal."
    }
}

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
