param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [Parameter(Mandatory = $true)][string]$StageDir,
    [Parameter(Mandatory = $true)][string]$VersionLabel,
    [ValidateSet('dev', 'github', 'steam')][string]$BuildFlavor = 'dev',
    [string]$CommitLabel = 'unknown',
    [ValidateSet('RimWorld16', 'RimWorld17', 'Dual')][string]$Target = 'RimWorld16',
    [string]$BuildRoot = 'dist/build',
    # The github and steam channels publish to strangers, so their payload must not carry the dev
    # suffix and its base version must equal the label on the box. The dev channel skips this: a dev
    # rehearsal is by definition an unnamed build.
    [switch]$RequireReleaseIdentity,
    # Only the GitHub channel zips. A dev rehearsal and a Workshop upload are both directories, and
    # archiving them adds a timestamp to argue about that the artifact never needed.
    [switch]$CreateZip
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# The one staging engine behind all three channels (dev / github / steam). Thin packers own flavor
# identity; everything that decides what a package IS lives here. Two independent staging
# implementations had already drifted: the dev folder's version.txt carried no build/commit line, so a
# rehearsal could not say which configuration produced its DLL and that had to be read out of the
# assembly's metadata by hand. Both sibling repositories in this series use this shape.
#
# Order is load-bearing: prove the inputs before wiping anything, prove the result after the last
# write. An assertion that runs against a tree that is still about to be rewritten proves nothing
# about what ships.

$root = [System.IO.Path]::GetFullPath($ProjectRoot)
$projectFile = Join-Path $root 'Source\FerriteLib.UiKit\FerriteLib.UiKit.csproj'
$configuration = if ($BuildFlavor -eq 'dev') { 'Dev' } else { 'Release' }
$buildDirectory = [IO.Path]::GetFullPath($BuildRoot, $root)
$targets = if ($Target -eq 'Dual') { @('RimWorld16', 'RimWorld17') } else { @($Target) }
$payloads = @(foreach ($selected in $targets) {
    $game = if ($selected -eq 'RimWorld16') { '1.6' } else { '1.7' }
    $buildPath = if ($selected -eq 'RimWorld16') { "$configuration/FerriteLib.UiKit.dll" } else { "$configuration/RimWorld17/FerriteLib.UiKit.dll" }
    [pscustomobject]@{ Target = $selected; Game = $game; Source = Join-Path $buildDirectory $buildPath
        Relative = "$game/Assemblies/FerriteLib.UiKit.dll"; Metadata = $null; Hash = '' }
})
$aboutXml = Join-Path $root 'About\About.xml'
$loadFolders = Join-Path $root 'LoadFolders.xml'
$license = Join-Path $root 'LICENSE'
$deliveryDir = [System.IO.Path]::GetFullPath($StageDir, $root)
$distPrefix = [System.IO.Path]::GetFullPath((Join-Path $root 'dist')) + [IO.Path]::DirectorySeparatorChar
function Assert-DistributionPath([string]$Path) {
    if (-not ([IO.Path]::GetFullPath($Path)).StartsWith($distPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'StageDir must be a child of this repository''s dist directory.'
    }
}
Assert-DistributionPath $deliveryDir
# Candidate-then-commit (2026-09-22, adopted from the demo's packer): the copy and every assertion below
# run against a SCRATCH tree; the delivered folder is replaced only after all of them pass, so a refused
# pack leaves the previous package byte-identical instead of a half-written one.
$stageDir = Join-Path (Split-Path -Parent $deliveryDir) ('.staging-' + (Split-Path -Leaf $deliveryDir))
Assert-DistributionPath $stageDir

$expectedFiles = @(
    'About/About.xml',
    'LoadFolders.xml',
    'LICENSE',
    'version.txt'
) + @($payloads | ForEach-Object { $_.Relative })
# What an assemblies-only prerequisite mod must never carry. Named paths rather than a filtered
# enumeration: an empty enumeration would let a filter-based check pass vacuously, which is the bug
# class this repository has been burned by twice.
$forbiddenContent = @('Defs', 'Patches', 'Languages', 'Sounds', 'Textures', 'ThingSets')

# --- inputs, before anything is deleted ----------------------------------------------------------
foreach ($required in (@($projectFile, $aboutXml, $loadFolders, $license) + @($payloads | ForEach-Object { $_.Source }))) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Missing packaging input: $required"
    }
}
[xml] $loadMap = Get-Content -LiteralPath $loadFolders -Raw
[xml] $about = Get-Content -LiteralPath $aboutXml -Raw
foreach ($payload in $payloads) {
    $folders = @($loadMap.SelectNodes("/loadFolders/v$($payload.Game)/li") | ForEach-Object { $_.InnerText.Trim().Trim('/') })
    if ($payload.Game -notin $folders -or @($folders | Where-Object { $_ -notin @('', $payload.Game) }).Count -ne 0) {
        throw "Target $($payload.Target) requires only its own $($payload.Game) load root (and optional shared root)."
    }
    if ($payload.Game -notin @($about.SelectNodes('/ModMetaData/supportedVersions/li') | ForEach-Object { $_.InnerText.Trim() })) {
        throw "About.xml does not support the selected target $($payload.Game)."
    }
}
Import-Module (Join-Path $PSScriptRoot 'Release.Identity.psm1')
$releaseBase = ($VersionLabel -replace '^v', '') -replace '[-+].*$', ''
foreach ($payload in $payloads) {
    $metadata = Get-FerritePayloadMetadata -Path $payload.Source
    Assert-FerritePayloadTarget -Metadata $metadata -Target $payload.Target
    if ($metadata.configuration -ne $configuration) {
        throw "Channel '$BuildFlavor' requires a $configuration-configured payload; found '$($metadata.configuration)'."
    }
    $informational = $metadata.informationalVersion
    if ([string]::IsNullOrWhiteSpace($informational)) { throw 'The payload carries no source identity.' }
    if ($RequireReleaseIdentity) {
        if ($informational -match '-dev') {
            throw "Payload is a dev build ('$informational'); a published asset must come from a build without the dev suffix: dotnet build -c Release -p:VersionSuffix="
        }
        if (($informational -replace '[-+].*$', '') -ne $releaseBase) {
            throw "Payload identity '$informational' does not match '$VersionLabel'."
        }
    }
    $payload.Metadata = $metadata
    $payload.Hash = (Get-FileHash -LiteralPath $payload.Source -Algorithm SHA256).Hash
}
if (@($payloads | ForEach-Object { $_.Metadata.informationalVersion } | Select-Object -Unique).Count -ne 1) {
    throw 'Dual payloads must share the same actual informational/source identity.'
}

# --- stage from scratch --------------------------------------------------------------------------
if (Test-Path -LiteralPath $stageDir) { Remove-Item -LiteralPath $stageDir -Recurse -Force }
foreach ($payload in $payloads) { $null = New-Item -ItemType Directory -Path (Join-Path $stageDir "$($payload.Game)/Assemblies") -Force }
$null = New-Item -ItemType Directory -Path (Join-Path $stageDir 'About') -Force

# About.xml is copied as one file, never as the About directory: a locally generated
# About/PublishedFileId.txt is Workshop identity and must not ride into a GitHub or dev package.
# A single-target archive must not advertise the other version's absent payload.
$games = @($payloads | ForEach-Object { $_.Game })
foreach ($node in @($about.SelectNodes('/ModMetaData/supportedVersions/li'))) {
    if ($node.InnerText.Trim() -notin $games) { $null = $node.ParentNode.RemoveChild($node) }
}
foreach ($node in @($loadMap.DocumentElement.ChildNodes)) {
    if ($node.NodeType -eq 'Element' -and $node.Name -notin @($games | ForEach-Object { "v$_" })) { $null = $node.ParentNode.RemoveChild($node) }
}
$about.Save((Join-Path $stageDir 'About/About.xml'))
$loadMap.Save((Join-Path $stageDir 'LoadFolders.xml'))

# MPL-2.0 section 3.2: a distributed Executable Form must say how to obtain the Source Code Form, so
# the licence text travels inside the package rather than living only in the repository.
Copy-Item -LiteralPath $license -Destination (Join-Path $stageDir 'LICENSE') -Force
foreach ($payload in $payloads) { Copy-Item -LiteralPath $payload.Source -Destination (Join-Path $stageDir $payload.Relative) -Force }

# Package identity, written after the last structural copy so nothing can filter it out. It answers
# "how fresh is this folder" without opening the DLL: flavor, commit, and the source location MPL 3.2
# asks for. On the release runner the platform names the repository; locally the 2026-09-07 naming
# ruling is the source of truth.
$sourceUrl = if ($env:GITHUB_REPOSITORY) { "$($env:GITHUB_SERVER_URL)/$env:GITHUB_REPOSITORY" } else { 'https://github.com/Coahuilite/FerriteLib' }
$identityLines = @("FerriteLib $VersionLabel", "build=$BuildFlavor", "commit=$CommitLabel", "target=$Target")
foreach ($payload in $payloads) {
    $identityLines += "payload-$($payload.Game)-sha256=$($payload.Hash)", "framework-$($payload.Game)=$($payload.Metadata.framework)",
        "mvid-$($payload.Game)=$($payload.Metadata.mvid)", "identity-$($payload.Game)=$($payload.Metadata.informationalVersion)"
}
if ($payloads.Count -eq 1) { $identityLines += "payload-sha256=$($payloads[0].Hash)" }
$identityLines += "source $sourceUrl"
[System.IO.File]::WriteAllText(
    (Join-Path $stageDir 'version.txt'),
    ($identityLines -join "`r`n") + "`r`n")

# --- assertions on the finished tree -------------------------------------------------------------
foreach ($directory in $forbiddenContent) {
    $probe = Join-Path $stageDir $directory
    if (Test-Path -LiteralPath $probe) {
        throw "Staged package carries game content: $probe"
    }
}

$stagedFiles = @(Get-ChildItem -LiteralPath $stageDir -Recurse -File | ForEach-Object {
    $_.FullName.Substring($stageDir.Length + 1).Replace('\', '/').ToLowerInvariant()
})

foreach ($required in $expectedFiles) {
    if ($stagedFiles -notcontains $required.ToLowerInvariant()) {
        throw "Staged package is missing $required."
    }
}

# The set is closed, so a stray .pdb from the Dev build gate, a .gitkeep, or a repository-only file
# cannot ship inside an otherwise correct package. "We copied five things" is a claim; this is a proof.
$allowed = @($expectedFiles | ForEach-Object { $_.ToLowerInvariant() })
$unexpected = @($stagedFiles | Where-Object { $allowed -notcontains $_ })
if ($unexpected.Count -gt 0) {
    throw "Staged package carries files it must not: $($unexpected -join ', ')"
}

$assemblyCount = @($stagedFiles | Where-Object { $_ -like '*.dll' }).Count
if ($assemblyCount -ne $payloads.Count) {
    throw "The payload must contain exactly one assembly per selected target; found $assemblyCount."
}

foreach ($payload in $payloads) {
    if ((Get-FileHash -LiteralPath (Join-Path $stageDir $payload.Relative) -Algorithm SHA256).Hash -ne $payload.Hash) {
        throw 'Payload changed during staging; refusing the candidate.'
    }
}

# --- commit: every assertion above passed, so replace the delivered folder now ---------------------
if (Test-Path -LiteralPath $deliveryDir) { Remove-Item -LiteralPath $deliveryDir -Recurse -Force }
Move-Item -LiteralPath $stageDir -Destination $deliveryDir
$stageDir = $deliveryDir
Write-Host "[stage-package] delivered $deliveryDir"

Write-Host "[stage-package] flavor=$BuildFlavor label=$VersionLabel commit=$CommitLabel"
Write-Host "[stage-package] staged $($stagedFiles.Count) files -> $stageDir"

# --- optional archive, deterministic by construction ---------------------------------------------
# Compress-Archive stamps entries from the staged files' mtimes, which staging itself rewrites, so two
# packs of one commit produced different SHA-256s over identical content (measured 2026-09-07).
# Pinning the mtimes first does not fix it either: NTFS re-dirties a directory during the compressor's
# own walk. So the timestamps go into the archive directly and the digest becomes a pure function of
# the commit.
if ($CreateZip) {
    $commitDate = [DateTimeOffset]::Parse((& git -C $root log -1 --format=%aI)).ToUniversalTime()
    $zipPath = Join-Path (Split-Path -Parent $stageDir) "FerriteLib-$VersionLabel.zip"
    Assert-DistributionPath $zipPath
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        $entries = @(Get-ChildItem -LiteralPath $stageDir -Recurse -Force | ForEach-Object {
            $rel = $_.FullName.Substring($stageDir.Length + 1).Replace('\', '/')
            if ($_.PSIsContainer) { "$rel/" } else { $rel }
        } | Sort-Object)
        foreach ($rel in $entries) {
            $entry = $archive.CreateEntry("FerriteLib/$rel", [System.IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = $commitDate
            if (-not $rel.EndsWith('/')) {
                $in = [System.IO.File]::OpenRead((Join-Path $stageDir ($rel.Replace('/', '\'))))
                $out = $entry.Open()
                try { $in.CopyTo($out) } finally { $out.Dispose(); $in.Dispose() }
            }
        }
    }
    finally { $archive.Dispose() }

    # The digest printed here is what the release body quotes and what a consumer CI compares against.
    # Shape: the archive contains a top-level FerriteLib/, because the release page tells a player to
    # unzip it into their Mods folder and they must not end up with a loose LoadFolders.xml there. The
    # dev and steam folders are handed over as folders and installed by whoever wants them, which is
    # exactly why they are not archived.
    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
    Write-Host "[stage-package] asset  -> $zipPath"
    Write-Host "[stage-package] sha256 -> $hash"
    Write-Host ("[stage-package] bytes  -> {0}" -f (Get-Item -LiteralPath $zipPath).Length)
}
