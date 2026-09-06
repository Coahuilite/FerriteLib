param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Version,
    [switch]$AllowDirtyTree
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Produces the GitHub Release asset: the mod package a player installs, and nothing else.
#
# Deliberate omissions, each one a boundary this repo has already argued for:
#   * No NuGet package. The .nupkg is a compile-time convenience for our own repos; publishing it next
#     to the mod zip invites a third party to copy FerriteLib.UiKit.dll into their own package, which is
#     the single-carrier failure the whole version contract exists to detect.
#   * No game content, by construction: gate 5 proves the source tree has none, and this script stages
#     exactly the four things an assemblies-only mod is allowed to ship.
#   * No silent rebuild. Like the sibling repo's pack scripts, this stages what the build already
#     produced, so the artifact on the release page is provably the output of the build the gates
#     checked - not a later compilation of a tree nobody re-verified.

$root = [System.IO.Path]::GetFullPath($ProjectRoot)
$projectFile = Join-Path $root 'Source\FerriteLib.UiKit\FerriteLib.UiKit.csproj'
$payloadDir = Join-Path $root '1.6\Assemblies'
$payloadDll = Join-Path $payloadDir 'FerriteLib.UiKit.dll'
$githubDir = Join-Path $root 'dist\github'
$stageDir = Join-Path $githubDir 'FerriteLib'
$zipDir = $githubDir

# --- the tag must be a release or an rc, and only a tag -------------------------------------------
# Mirrors .github/workflows/release.yml: vBASE, or vBASE-rcN with N >= 1. The rc scheme is the trial
# mechanism (maintainer decision 2026-09-05): each iteration that goes to players gets the next
# number, and the final release must point at the same commit as the last rc. A tag that is neither
# form cannot be packed, so no mislabelled asset can reach the release page.
$tagPattern = '^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-rc([1-9]\d*))?$'
if ($Version -notmatch $tagPattern) {
    throw "Version must be vMAJOR.MINOR.PATCH or vMAJOR.MINOR.PATCH-rcN (N >= 1): $Version"
}
$baseVersion = "$($Matches[1]).$($Matches[2]).$($Matches[3])"   # what the DLL inside must report
# The rc suffix lives only in the artifact name and version.txt (via $Version below); the identity
# axes compare on the base, because the DLL's version resource does not carry the suffix.

# --- axis 1: the tag must name the build axis, or the page lies about its own artifact -------------
# pack-dev.ps1 derives the dev label from <VersionPrefix>; this derives the release label from the tag.
# If the two disagree, the zip is named after a version that was never built. FerriteLibVersionTests
# asserts the same agreement at gate time; this asserts it at the moment a number becomes public.
$prefixMatch = Select-String -LiteralPath $projectFile -Pattern '<VersionPrefix>(.*?)</VersionPrefix>'
if (-not $prefixMatch) { throw 'csproj carries no <VersionPrefix>; the build axis has no home.' }
$buildAxis = $prefixMatch.Matches[0].Groups[1].Value.Trim()
if ($buildAxis -ne $baseVersion) {
    throw "Release tag v$baseVersion does not match the build axis <VersionPrefix>$buildAxis. Bump the axes and rebuild, or retag; do not rename the artifact."
}

# --- axis 2: the shipped DLL must agree with the name on the box ----------------------------------
if (-not (Test-Path -LiteralPath $payloadDll -PathType Leaf)) {
    throw "No payload at $payloadDll. Build the Release configuration first (see .github/workflows/release.yml)."
}
$dll = Get-Item -LiteralPath $payloadDll
$versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($dll.FullName)
$informational = $versionInfo.ProductVersion
if ([string]::IsNullOrWhiteSpace($informational)) {
    throw 'The payload carries no ProductVersion; it cannot be attributed to a source commit.'
}
$infoBase = ($informational -replace '^\s*', '') -replace '[-+].*$', ''
if ($infoBase -ne $baseVersion) {
    throw "Payload's AssemblyInformationalVersion is '$informational' (base $infoBase) but the release tag says $baseVersion. The build is stale or the tag is wrong."
}
if ($informational -match '-dev') {
    throw "Payload is a dev build ('$informational'); a release asset must come from a Release build without the dev suffix."
}

# --- axis 3: clean tree, so the artifact is attributable to a commit ------------------------------
$commit = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or -not $commit) { throw 'Cannot resolve the current commit; refusing to publish an unattributable artifact.' }
$shortCommit = $commit.Substring(0, 12)
$status = @(& git -C $root status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'git status failed; cannot prove the tree is clean.' }
if ($status.Count -gt 0 -and -not $AllowDirtyTree) {
    throw ("Working tree is dirty ({0} path(s)); a published asset must come from the exact commit it names. Commit them or pass -AllowDirtyTree for a local rehearsal." -f $status.Count)
}

# --- axis 3b: the bytes must name the commit being released ---------------------------------------
# AssemblyInformationalVersion carries the source revision the SDK embedded at BUILD time, so it is the
# only thing inside the artifact that says what it was compiled from. On the release workflow the build
# runs on the tagged checkout, so this matches by construction; locally it catches the ordinary mistake
# of packing a DLL built before the commit you are about to tag - which is exactly what happened on
# 2026-09-05, where a mutation test left 1.6/Assemblies/ built from the previous commit. Skipped under
# -AllowDirtyTree, the rehearsal path, because there the tree is by definition not what gets published.
if (-not $AllowDirtyTree) {
    if ($informational -notlike "*$commit*") {
        throw "Payload was built from a different commit than HEAD: its version resource says '$informational', HEAD is $commit. Rebuild after committing, or pass -AllowDirtyTree for a local rehearsal."
    }
}

# --- gate 4: the source tree must still be content-free -------------------------------------------
# The gates prove this about the repository; the package is what a player receives, so the same named-path
# probe runs against the staging directory. A filtered enumeration would pass vacuously on an empty tree,
# which is the bug class this repo has already been burned by twice.
$forbiddenPayload = @('Defs', 'Patches', 'Languages', 'Sounds', 'Textures', 'ThingSets')

# --- stage, strip, zip ---------------------------------------------------------------------------
if (Test-Path -LiteralPath $stageDir) { Remove-Item -LiteralPath $stageDir -Recurse -Force }
$null = New-Item -ItemType Directory -Path (Join-Path $stageDir '1.6\Assemblies') -Force
$null = New-Item -ItemType Directory -Path (Join-Path $stageDir 'About') -Force
Copy-Item -LiteralPath (Join-Path $root 'About\About.xml') -Destination (Join-Path $stageDir 'About\About.xml') -Force
Copy-Item -LiteralPath (Join-Path $root 'LoadFolders.xml') -Destination (Join-Path $stageDir 'LoadFolders.xml') -Force
Copy-Item -LiteralPath $payloadDll -Destination (Join-Path $stageDir '1.6\Assemblies\FerriteLib.UiKit.dll') -Force

# MPL-2.0 section 3.2: a distributed Executable Form must say how to obtain the Source Code Form. The
# licence travels INSIDE the package, and it is copied after the stage directory was (re)created above,
# so no later step can delete it - the ordering bug this repo's own notes record as a near-miss.
$licenseSource = Join-Path $root 'LICENSE'
if (-not (Test-Path -LiteralPath $licenseSource -PathType Leaf)) {
    throw 'Missing LICENSE: the shipped assembly is MPL-2.0 covered and the package must carry the text.'
}
Copy-Item -LiteralPath $licenseSource -Destination (Join-Path $stageDir 'LICENSE') -Force

# Identity, so a player's installed copy can be traced to a commit without a game log.
# Use the full tag, not $baseVersion: a prerelease rehearsal build (v0.2.1-pre1) whose version.txt says
# "0.2.1" is indistinguishable from the real 0.2.1 on the player's disk, and that is exactly the window
# where early testers are being asked to tell us which build they hit.
$releaseLabel = $Version -replace '^v', ''
[System.IO.File]::WriteAllText(
    (Join-Path $stageDir 'version.txt'),
    "FerriteLib $releaseLabel`r`ncommit $shortCommit`r`n")

# Debug symbols are not part of a release payload.
$pdbs = @(Get-ChildItem -LiteralPath $stageDir -Recurse -File -Filter '*.pdb')
if ($pdbs.Count -gt 0) { $pdbs | Remove-Item -Force }

# --- assertions run LAST, on the final tree -------------------------------------------------------
# This ordering is the point: a content or licence check that runs against a tree that is still about
# to be rewritten proves nothing about the zip. See MEMORY.md, "Assertion order is part of an
# assertion".
foreach ($directory in $forbiddenPayload) {
    $probe = Join-Path $stageDir $directory
    if (Test-Path -LiteralPath $probe) {
        throw "Release package carries game content: $probe"
    }
}
$stagedAssemblies = @(Get-ChildItem -LiteralPath (Join-Path $stageDir '1.6\Assemblies') -File)
if ($stagedAssemblies.Count -ne 1 -or $stagedAssemblies[0].Name -ne 'FerriteLib.UiKit.dll') {
    throw ("Release payload must be exactly one assembly; found {0}." -f (($stagedAssemblies | ForEach-Object Name) -join ', '))
}
foreach ($required in @('LICENSE', 'LoadFolders.xml', 'version.txt', 'About\About.xml')) {
    if (-not (Test-Path -LiteralPath (Join-Path $stageDir $required) -PathType Leaf)) {
        throw "Release package is missing $required."
    }
}

# Shape: the zip contains a top-level "FerriteLib" folder, NOT its bare contents. pack-dev.ps1 uses
# the root-contents shape because a dev rehearsal copies into an already-existing Mods/FerriteLib;
# a release asset is unzipped straight into Mods/ by a player, so the folder has to be inside the
# archive or they get a Loose LoadFolders.xml in their Mods directory. Same choice the sibling
# repository makes on its release path (pack-github.ps1 stages the directory, not the glob).
# Deterministic timestamps: Compress-Archive stamps each entry with its file's mtime, and staging
# just rewrote those mtimes to wall-clock now - so two packs of the same commit differed by seconds
# and the printed SHA-256 described the build machine's clock, not the release. Normalize the whole
# stage tree to the tagged commit's author date: the artifact's timestamps then point at the source,
# and the digest becomes a pure function of the commit (verified: entry CRCs were already identical;
# mtimes were the only divergence).
$commitDate = [DateTime]::Parse((& git -C $root log -1 --format=%aI $commit)).ToUniversalTime()
# Order is load-bearing: on NTFS, rewriting a child's mtime bumps the parent directory's mtime to
# now. Enumerating top-down (the default) therefore re-dirties every directory right after pinning
# it - measured: file entries held the commit date while the 1.6/ directory entry still carried the
# wall clock. Files first, then directories deepest-first, so each directory is pinned after its
# last child.
Get-ChildItem -LiteralPath $githubDir -Recurse -Force |
    Sort-Object { [bool]$_.PSIsContainer }, @{ Expression = { $_.FullName.Length }; Descending = $true } |
    ForEach-Object { $_.LastWriteTime = $commitDate }
$zipPath = Join-Path $zipDir "FerriteLib-$Version.zip"
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
Compress-Archive -Path $stageDir -DestinationPath $zipPath -Force

# The digest printed here is what the release body must quote, and what a consumer's CI verifies when it
# links to this artifact instead of rebuilding it. One canonical copy: the page that owns the binary.
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
$size = (Get-Item -LiteralPath $zipPath).Length

Write-Host "[pack-release] staged  -> $stageDir"
Write-Host "[pack-release] asset   -> $zipPath"
Write-Host "[pack-release] commit  -> $shortCommit"
Write-Host "[pack-release] sha256  -> $hash"
Write-Host ("[pack-release] bytes   -> {0}" -f $size)
exit 0
