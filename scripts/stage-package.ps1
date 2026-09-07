param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [Parameter(Mandatory = $true)][string]$StageDir,
    [Parameter(Mandatory = $true)][string]$VersionLabel,
    [ValidateSet('dev', 'github', 'steam')][string]$BuildFlavor = 'dev',
    [string]$CommitLabel = 'unknown',
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
$payloadDll = Join-Path $root '1.6\Assemblies\FerriteLib.UiKit.dll'
$aboutXml = Join-Path $root 'About\About.xml'
$loadFolders = Join-Path $root 'LoadFolders.xml'
$license = Join-Path $root 'LICENSE'
$stageDir = [System.IO.Path]::GetFullPath($StageDir)

$dllRelative = '1.6/Assemblies/FerriteLib.UiKit.dll'
$expectedFiles = @(
    'About/About.xml',
    'LoadFolders.xml',
    'LICENSE',
    'version.txt',
    $dllRelative
)
# What an assemblies-only prerequisite mod must never carry. Named paths rather than a filtered
# enumeration: an empty enumeration would let a filter-based check pass vacuously, which is the bug
# class this repository has been burned by twice.
$forbiddenContent = @('Defs', 'Patches', 'Languages', 'Sounds', 'Textures', 'ThingSets')

# --- inputs, before anything is deleted ----------------------------------------------------------
foreach ($required in @($projectFile, $payloadDll, $aboutXml, $loadFolders, $license)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Missing packaging input: $required"
    }
}

$releaseBase = ($VersionLabel -replace '^v', '') -replace '[-+].*$', ''

if ($RequireReleaseIdentity) {
    $informational = ([System.Diagnostics.FileVersionInfo]::GetVersionInfo($payloadDll)).ProductVersion
    if ([string]::IsNullOrWhiteSpace($informational)) {
        throw 'The payload carries no ProductVersion; it cannot be attributed to a source commit.'
    }

    if ($informational -match '-dev') {
        throw "Payload is a dev build ('$informational'); a published asset must come from a build without the dev suffix: dotnet build -c Release -p:VersionSuffix="
    }

    $infoBase = $informational -replace '[-+].*$', ''
    if ($infoBase -ne $releaseBase) {
        throw "Payload identity is '$informational' (base $infoBase) but this channel is packing '$VersionLabel' (base $releaseBase). The build is stale or the label is wrong."
    }
}

# --- stage from scratch --------------------------------------------------------------------------
if (Test-Path -LiteralPath $stageDir) { Remove-Item -LiteralPath $stageDir -Recurse -Force }
$null = New-Item -ItemType Directory -Path (Join-Path $stageDir '1.6\Assemblies') -Force
$null = New-Item -ItemType Directory -Path (Join-Path $stageDir 'About') -Force

# About.xml is copied as one file, never as the About directory: a locally generated
# About/PublishedFileId.txt is Workshop identity and must not ride into a GitHub or dev package.
Copy-Item -LiteralPath $aboutXml -Destination (Join-Path $stageDir 'About\About.xml') -Force
Copy-Item -LiteralPath $loadFolders -Destination (Join-Path $stageDir 'LoadFolders.xml') -Force

# MPL-2.0 section 3.2: a distributed Executable Form must say how to obtain the Source Code Form, so
# the licence text travels inside the package rather than living only in the repository.
Copy-Item -LiteralPath $license -Destination (Join-Path $stageDir 'LICENSE') -Force
Copy-Item -LiteralPath $payloadDll -Destination (Join-Path $stageDir '1.6\Assemblies\FerriteLib.UiKit.dll') -Force

# Package identity, written after the last structural copy so nothing can filter it out. It answers
# "how fresh is this folder" without opening the DLL: flavor, commit, and the source location MPL 3.2
# asks for. On the release runner the platform names the repository; locally the 2026-09-07 naming
# ruling is the source of truth.
$sourceUrl = if ($env:GITHUB_REPOSITORY) { "$($env:GITHUB_SERVER_URL)/$env:GITHUB_REPOSITORY" } else { 'https://github.com/Coahuilite/FerriteLib' }
[System.IO.File]::WriteAllText(
    (Join-Path $stageDir 'version.txt'),
    "FerriteLib $VersionLabel`r`nbuild=$BuildFlavor`r`ncommit=$CommitLabel`r`nsource $sourceUrl`r`n")

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
if ($assemblyCount -ne 1) {
    throw "The payload must be exactly one assembly; found $assemblyCount."
}

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
