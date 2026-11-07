param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Version,
    [string]$StageDir = 'dist/steam/FerriteLib'
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
$payloadDll = Join-Path $root 'dist\build\Release\FerriteLib.UiKit.dll'
$stageDir = [IO.Path]::GetFullPath($StageDir, $root)

Import-Module (Join-Path $PSScriptRoot 'Release.Identity.psm1')
$releaseVersion = Get-FerriteReleaseVersion -Version $Version
Assert-FerriteReleaseAxis -ProjectFile $projectFile -BaseVersion $releaseVersion.Base
$commit = Get-FerriteReleaseCommit -Root $root -PayloadDll $payloadDll -Channel steam

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
