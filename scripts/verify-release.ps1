param(
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Tag,
    [string]$Repo = "Coahuilite/ferritelib",
    [string]$AssetPrefix = 'FerriteLib',
    [string]$ZipLocal
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Post-publish verification of one release, as the platform actually holds it.
#
# Why this exists as a script rather than a checklist: GitHub does NOT derive prerelease from the tag
# name. `prerelease` is a boolean on the release object that defaults to false, so an rc tag whose
# release was created by any path that forgot the flag - a manual web-UI click, a different tool, a
# hand call to the REST API - silently becomes the repository's STABLE "Latest" release, and
# /releases/latest (the resolution source for consumer CI) will serve it to everyone as the real one.
# release.yml sets the flag, but a pipeline that verifies its own inputs and not its own outputs has
# the last step open-loop. This closes it: one command, run after any publish, exit 0 only when the
# platform state matches what the tag promises.
#
# Every fact asserted here was confirmed against github/site-policy docs and the live
# Coahuilite/SqueakyRatkin data: prerelease=true survives on v0.1.0-rc1 and v0.3.2-pre1 because the
# workflow set it; /releases/latest skips prereleases by created_at; assets carry a server-computed
# `digest` ("sha256:<hex>") that matched a local sha256sum of the same zip byte-for-byte.
#
# Requires the gh CLI, authenticated for the repo (public repos read fine without a token, with one
# a private repo under development also works).

try { $null = Get-Command gh -CommandType Application -ErrorAction Stop }
catch { throw "gh CLI is required (see AGENTS.md tooling rule)." }

$failures = @()
function Add-Failure([string]$m) { $script:failures += $m; Write-Host "FAIL  $m" -ForegroundColor Red }
function Add-Pass([string]$m) { Write-Host "ok    $m" }

# The naming contract, restated from release.yml: bare vBASE is stable, vBASE-rcN is a trial.
$isRcShape = $Tag -match '^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-rc([1-9]\d*))?$'
if (-not $isRcShape) { throw "Tag '$Tag' is not vMAJOR.MINOR.PATCH or vMAJOR.MINOR.PATCH-rcN; it should never have reached a release." }
$expectPrerelease = $Tag -match '-rc'

# 1. The release object exists and is published (not draft).
$relJson = & gh api "repos/$Repo/releases/tags/$Tag" --jq '.' 2>$null
if ($LASTEXITCODE -ne 0 -or -not $relJson) {
    throw "No GitHub Release exists for tag $Tag on $Repo (a bare git tag is not a release; players find releases, not tags)."
}
$rel = $relJson | ConvertFrom-Json
if ($rel.draft) { Add-Failure "$Tag is still a DRAFT release; nobody but you can see it." } else { Add-Pass "release exists and is published" }

# 2. The prerelease flag matches the tag name. This is THE setting GitHub never infers.
if ([bool]$rel.prerelease -ne $expectPrerelease) {
    $want = if ($expectPrerelease) { "true (tag is an rc)" } else { "false (bare release tag)" }
    Add-Failure "prerelease flag is $($rel.prerelease), expected $want - GitHub does not derive it from the tag name; an rc flagged stable becomes the repo's 'Latest'."
} else { Add-Pass "prerelease flag = $($rel.prerelease), matching the tag" }

# 3. Exactly the intended asset, and its server digest matches the local zip when given.
$expectedAsset = "$AssetPrefix-$Tag.zip"
$assets = @($rel.assets)
if ($assets.Count -ne 1) {
    Add-Failure ("expected exactly 1 release asset ($expectedAsset); found {0}: {1}" -f $assets.Count, (($assets | ForEach-Object name) -join ', '))
} else {
    if ($assets[0].name -ne $expectedAsset) { Add-Failure "asset is named '$($assets[0].name)', expected '$expectedAsset'" }
    else { Add-Pass "single asset $expectedAsset ($($assets[0].size) bytes, $($assets[0].download_count) downloads)" }
    if ($ZipLocal) {
        if (-not (Test-Path -LiteralPath $ZipLocal -PathType Leaf)) { throw "ZipLocal not found: $ZipLocal" }
        $local = (Get-FileHash -LiteralPath $ZipLocal -Algorithm SHA256).Hash.ToLowerInvariant()
        $remote = "$($assets[0].digest)".Replace('sha256:', '')
        if (-not $remote) { Add-Failure "asset has no server digest yet; re-run shortly (GitHub computes it on upload completion)" }
        elseif ($remote -ne $local) { Add-Failure "digest mismatch: uploaded zip != local zip`n        remote $remote`n        local  $local" }
        else { Add-Pass "server digest == local zip SHA-256 ($local)" }
    }
}

# 4. Latest-pointer sanity: an rc must not be what /releases/latest hands out.
$latestJson = & gh api "repos/$Repo/releases/latest" --jq '.' 2>$null
if ($LASTEXITCODE -eq 0 -and $latestJson) {
    $latest = $latestJson | ConvertFrom-Json
    if ($latest.prerelease) { Add-Failure "/releases/latest returned a prerelease ($($latest.tag_name)); a stable release was mis-flagged somewhere" }
    elseif ($expectPrerelease -and $latest.tag_name -eq $Tag) { Add-Failure "this rc is reported as the repo's latest stable release" }
    else { Add-Pass "latest = $($latest.tag_name) (stable)" }
} else {
    Add-Pass "no stable release exists yet (/releases/latest is 404) - correct during the rc-only window"
}

# 5. No dangling tags: every version tag should have a release. A tag pushed without its workflow
#    run (cancelled mid-queue, permissions failure) is a silent hole in the rc chain, and the next
#    rc gate counts it, so check it explicitly rather than trusting run history.
$tagNames = @(& gh api "repos/$Repo/tags?per_page=100" --jq '.[].name' 2>$null)
$relNames = @(& gh api "repos/$Repo/releases?per_page=100" --jq '.[].tag_name' 2>$null)
$dangling = @($tagNames | Where-Object { $_ -like 'v*' -and $_ -notin $relNames })
if ($dangling.Count -gt 0) { Add-Failure ("tag(s) with no release: {0} - re-run the release workflow for each" -f ($dangling -join ', ')) }
else { Add-Pass "every v* tag has a release" }

if ($failures.Count -gt 0) {
    Write-Host "`n$($failures.Count) problem(s) on $Repo $Tag." -ForegroundColor Red
    exit 1
}
Write-Host "`n$Repo $Tag verified against the platform."
exit 0
