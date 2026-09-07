param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Version
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Steam channel: the directory a Workshop uploader reads, and the only channel whose artifact is never
# archived. The item on disk must be the same bytes the GitHub release page published, so this shares
# every identity rule with that channel and differs in exactly two ways: a clean tree is mandatory, and
# nothing is zipped.
#
# Running this is not publishing. Uploading stays gated on the maintainer's Workshop decision and on the
# player-facing preview image (`TODO.md` §5); until then this only stages a folder nobody sees.

$root = [System.IO.Path]::GetFullPath($ProjectRoot)
$projectFile = Join-Path $root 'Source\FerriteLib.UiKit\FerriteLib.UiKit.csproj'
$payloadDll = Join-Path $root '1.6\Assemblies\FerriteLib.UiKit.dll'
$stageDir = Join-Path $root 'dist\steam\FerriteLib'

# Same tag grammar as the GitHub channel, deliberately: a Workshop item and a release page carrying
# different version shapes of one library is how a "which one do I install" question gets asked.
$tagPattern = '^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-rc([1-9]\d*))?$'
if ($Version -notmatch $tagPattern) {
    throw "Version must be vMAJOR.MINOR.PATCH or vMAJOR.MINOR.PATCH-rcN (N >= 1): $Version"
}
$baseVersion = "$($Matches[1]).$($Matches[2]).$($Matches[3])"

# The build axis must name the same version this folder claims. Read from the csproj rather than
# About.xml, because a Release build is what carries the number into the DLL's version resource.
$prefixMatch = Select-String -LiteralPath $projectFile -Pattern '<VersionPrefix>(.*?)</VersionPrefix>'
if (-not $prefixMatch) { throw 'csproj carries no <VersionPrefix>; the build axis has no home.' }
$buildAxis = $prefixMatch.Matches[0].Groups[1].Value.Trim()
if ($buildAxis -ne $baseVersion) {
    throw "v$baseVersion does not match the build axis <VersionPrefix>$buildAxis. Bump the axes and rebuild, or retag; do not rename the artifact."
}

# Clean tree, with no escape hatch: Steam is the last step of a release, and an item built from
# uncommitted work carries a commit nobody can check out. Rehearsing a build is pack-dev's job, and it
# writes a different folder.
$commit = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or -not $commit) { throw 'Cannot resolve the current commit; refusing to publish an unattributable item.' }

$status = @(& git -C $root status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'git status failed; cannot prove the tree is clean.' }
if ($status.Count -gt 0) {
    throw ("Working tree is dirty ({0} path(s)); commit before staging for Steam." -f $status.Count)
}

# The payload has to be the commit being uploaded, built without the dev suffix. The base-version and
# dev-suffix halves live in the staging engine; this one is about provenance rather than identity, so
# it stays here.
if (-not (Test-Path -LiteralPath $payloadDll -PathType Leaf)) {
    throw "No payload at $payloadDll. Build it first: dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release -p:VersionSuffix="
}

$informational = ([System.Diagnostics.FileVersionInfo]::GetVersionInfo($payloadDll)).ProductVersion
if ($informational -notlike "*$commit*") {
    throw "Payload was built from a different commit than HEAD: its version resource says '$informational', HEAD is $commit. Rebuild after committing."
}

$stageArgs = @{
    ProjectRoot            = $root
    StageDir              = $stageDir
    VersionLabel           = $Version
    BuildFlavor            = 'steam'
    CommitLabel            = $commit.Substring(0, 12)
    RequireReleaseIdentity = $true
}
& (Join-Path $PSScriptRoot 'stage-package.ps1') @stageArgs
if ($LASTEXITCODE -ne 0) { throw "stage-package failed with exit code $LASTEXITCODE." }

# The uploader needs a Workshop identity and this folder deliberately carries none:
# About/PublishedFileId.txt is gitignored, and stage-package.ps1 never copies the About directory, so
# no GitHub or dev artifact can pick it up either. The upload step writes it into
# dist/steam/FerriteLib/About/ locally, after this script has run.
$dllHash = (Get-FileHash -LiteralPath $payloadDll -Algorithm SHA256).Hash
Write-Host "[pack-steam] folder          -> $stageDir"
Write-Host "[pack-steam] commit          -> $($commit.Substring(0, 12))"
Write-Host "[pack-steam] payload sha256  -> $dllHash"
Write-Host '[pack-steam] upload the folder as-is; PublishedFileId.txt is created locally at upload time.'
