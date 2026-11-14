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
function Get-FerritePayloadMetadata {
    param([Parameter(Mandatory = $true)][string] $Path)
    $shell = Join-Path $PSHOME 'pwsh.exe'
    if (-not (Test-Path -LiteralPath $shell -PathType Leaf)) { $shell = 'pwsh' }
    $output = @(& $shell -NoProfile -NonInteractive -File (Join-Path $PSScriptRoot 'read-carrier-metadata.ps1') -Path $Path)
    if ($LASTEXITCODE -ne 0) { throw "Reading carrier metadata failed (exit $LASTEXITCODE)." }
    ($output -join [Environment]::NewLine) | ConvertFrom-Json
}

function Assert-FerritePayloadTarget {
    param([Parameter(Mandatory = $true)] $Metadata,
        [ValidateSet('RimWorld16', 'RimWorld17')][string] $Target)
    if ($Metadata.name -ne 'FerriteLib.UiKit') { throw 'The payload must be the FerriteLib.UiKit assembly.' }
    if ($Metadata.informationalVersion -notmatch '\+[0-9a-fA-F]{40}($|[-.])') {
        throw 'The carrier must retain its committed source identity in InformationalVersion.'
    }
    $framework = if ($Target -eq 'RimWorld16') { '.NETFramework,Version=v4.7.2' } else { '.NETCoreApp,Version=v10.0' }
    if ($Metadata.framework -ne $framework) {
        throw "Target $Target requires $framework; payload reports '$($Metadata.framework)'."
    }
    # The compiler omits unused references: RimWorld/Waylith are real build inputs but need not
    # survive in a carrier which only uses the Verse/UnityStubs surface.
    $required = if ($Target -eq 'RimWorld16') { @('Assembly-CSharp') } else { @('Assembly-CSharp', 'UnityStubs') }
    foreach ($reference in $required) {
        if ($reference -notin $Metadata.references) { throw "Target $Target is missing its game reference '$reference'." }
    }
    if ($Target -eq 'RimWorld16' -and 'UnityStubs' -in $Metadata.references) { throw 'The 1.6 payload cannot use the 1.7 UnityStubs backend.' }
}

Export-ModuleMember -Function Get-FerriteReleaseVersion, Get-FerriteBuildAxis, Assert-FerriteReleaseAxis, Get-FerriteReleaseCommit, Get-FerritePayloadMetadata, Assert-FerritePayloadTarget
