# Shared tag/build/source identity for the GitHub and Steam channels.
# Staging still owns measured configuration, payload identity and the closed file set.
function Get-FerriteReleaseVersion {
    param([string] $Version)
    if ($Version -notmatch '^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-rc([1-9]\d*))?$') {
        throw "Version must be vMAJOR.MINOR.PATCH or vMAJOR.MINOR.PATCH-rcN (N >= 1): $Version"
    }
    [pscustomobject]@{ Base = "$($Matches[1]).$($Matches[2]).$($Matches[3])"; Rc = $Matches[4] }
}

function Get-FerriteBuildAxis {
    param([string] $ProjectFile)
    [xml] $project = Get-Content -LiteralPath $ProjectFile -Raw
    $prefix = @($project.SelectNodes('/Project/PropertyGroup/VersionPrefix'))
    if ($prefix.Count -ne 1) { throw "Expected exactly one <VersionPrefix>, found $($prefix.Count)." }
    $prefix[0].InnerText.Trim()
}

function Assert-FerriteReleaseAxis {
    param([string] $ProjectFile, [string] $BaseVersion)
    $buildAxis = Get-FerriteBuildAxis -ProjectFile $ProjectFile
    if ($buildAxis -ne $BaseVersion) {
        throw "Release tag v$BaseVersion does not match the build axis <VersionPrefix>$buildAxis. Bump the axes and rebuild, or retag; do not rename the artifact."
    }
}

function Get-FerriteReleaseCommit {
    param([string] $Root, [string] $PayloadDll, [ValidateSet('github','steam')][string] $Channel, [switch] $AllowDirtyTree)
    $commitOutput = @(& git -C $Root rev-parse HEAD)
    if ($LASTEXITCODE -ne 0 -or $commitOutput.Count -ne 1 -or -not $commitOutput[0]) {
        throw 'Cannot resolve the current commit; refusing to publish an unattributable artifact.'
    }
    $commit = ([string]$commitOutput[0]).Trim()
    $status = @(& git -C $Root status --porcelain)
    if ($LASTEXITCODE -ne 0) { throw 'git status failed; cannot prove the tree is clean.' }
    if ($status.Count -gt 0 -and ($Channel -eq 'steam' -or -not $AllowDirtyTree)) {
        if ($Channel -eq 'steam') { throw ("Working tree is dirty ({0} path(s)); commit before staging for Steam." -f $status.Count) }
        throw ("Working tree is dirty ({0} path(s)); a published asset must come from the exact commit it names. Commit them or pass -AllowDirtyTree for a local rehearsal." -f $status.Count)
    }
    if (-not (Test-Path -LiteralPath $PayloadDll -PathType Leaf)) {
        throw "No payload at $PayloadDll. Build the Release configuration first (see .github/workflows/release.yml)."
    }
    # Only GitHub's explicit local rehearsal may omit the source-commit check.
    if ($Channel -eq 'steam' -or -not $AllowDirtyTree) {
        $informational = [Diagnostics.FileVersionInfo]::GetVersionInfo($PayloadDll).ProductVersion
        if ($informational -notlike "*$commit*") {
            throw "Payload was built from a different commit than HEAD: its version resource says '$informational', HEAD is $commit. Rebuild after committing."
        }
    }
    $commit
}
Export-ModuleMember -Function Get-FerriteReleaseVersion, Get-FerriteBuildAxis, Assert-FerriteReleaseAxis, Get-FerriteReleaseCommit
