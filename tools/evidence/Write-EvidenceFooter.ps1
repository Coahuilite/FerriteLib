<#
.SYNOPSIS
    Writes the M6/M11 evidence footer for a FerriteLib run, with the artifact line DERIVED from the configuration
    that was actually triggered.

.DESCRIPTION
    Why this file exists (T31): the FL chain and harness logs were bound to a commit only by the reader's
    inference from file timestamps, because the logs carried no identity of their own. A log that cannot say
    which bytes produced it is a narrative, not evidence.

    The SHAPE is the consumer repository's tools/evidence/Write-EvidenceFooter.ps1, deliberately: two
    repositories writing two different footers would be the same drift this project already paid for once with
    text rulers. The field set is the same; what is added here is what a FerriteLib run has and a consumer run
    does not - the two carriers (the frozen root payload and the paired dev package, each recorded as a hash
    AND an mtime pair, because equal bytes can be rewritten) and this repository's gate lines.

    The artifact line is DERIVED from -Configuration, never hard-coded, so a log cannot describe a build the
    run did not trigger. -Configuration Both is the chain's honest value: verify-local.ps1 builds and runs both
    configurations, and pretending it is one of them would be the defect this derivation exists to prevent.

    The convention for the artifact line follows FL tools/mutation/Invoke-Mutation.ps1 (the K3 rule): the path
    is derived from the mutated file for a mutation, and from the run's configuration otherwise.

.PARAMETER LogPath
    The log file to append to. Must exist; a missing log is refused rather than created, so a footer can never
    describe a run whose output nobody kept.

.PARAMETER ToStdout
    Print the footer instead of appending it. Used by verify-local.ps1 so a redirected chain log receives it in
    stream order, with no second writer and no second shape.

.PARAMETER Configuration
    The configuration the run used (Release, Dev, or Both for the chain). The artifact line derives from it.

.PARAMETER Subject
    What the log is: the command, the script, or the lane that produced it.

.PARAMETER Command
    The command line that produced the log.

.PARAMETER ExitCode
    The process exit code.

.PARAMETER Gate
    Zero or more "name=result" lines for the gates or lanes this run reported, verbatim from the run.

.PARAMETER PinPath
    Repository-relative files to fingerprint: the files that hold the assertions the run relied on.

.EXAMPLE
    pwsh -NoProfile -File tools/evidence/Write-EvidenceFooter.ps1 -LogPath dist/dev-work/chain.log `
        -Configuration Both -Subject 'scripts/verify-local.ps1 -NoRestore' `
        -Command 'pwsh -NoProfile -File scripts/verify-local.ps1 -NoRestore' -ExitCode 0
#>
param(
    [string]$LogPath = '',
    [switch]$ToStdout,
    [Parameter(Mandatory = $true)][ValidateSet('Release', 'Dev', 'Both')][string]$Configuration,
    [string]$Subject = '',
    [string]$Command = '',
    [int]$ExitCode = 0,
    [string[]]$Gate = @(),
    [string[]]$PinPath = @(),
    # Set when the footer is written AFTER the run rather than while it happened (upgrading an old log from
    # 'bound by inference' to 'bound by a checkable statement'). The marker is machine-detectable and the
    # caveat says exactly what the numbers below are: the repository's state NOW.
    [switch]$Recomputed,
    # The HEAD the caller states the run happened at. It is NOT measured here - it cannot be, after the fact -
    # so it is labelled as supplied, and it is what makes a recomputed footer better than an inference.
    [string]$RanAtHead = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $ToStdout) {
    if (-not $LogPath) { throw '-LogPath is required unless -ToStdout is given.' }
    if (-not (Test-Path -LiteralPath $LogPath)) {
        throw "the log '$LogPath' does not exist; refusing to write a footer for a run whose output was not kept."
    }
}

function Get-Fingerprint([string]$relative) {
    $full = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $full)) { return "$relative (missing)" }
    $item = Get-Item -LiteralPath $full
    return "$relative sha256=$((Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash) bytes=$($item.Length) mtime=$($item.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss.fff'))"
}

# Hash AND mtime, as a pair: the hash identifies bytes and the mtime detects a rewrite of equal bytes.
function Get-Pair([string]$relative) {
    $full = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $full)) { return "$relative (missing)" }
    $item = Get-Item -LiteralPath $full
    return "sha256=$((Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash) bytes=$($item.Length) mtime=$($item.LastWriteTimeUtc.ToString('o'))"
}

# The artifact line is DERIVED, never hard-coded: one place computes it, from the configuration.
$artifactLine = switch ($Configuration) {
    'Release' { '# artifact (derived from the configuration this run triggered: Release): ' + (Get-Fingerprint 'dist/build/Release/FerriteLib.UiKit.dll') }
    'Dev'     { '# artifact (derived from the configuration this run triggered: Dev): ' + (Get-Fingerprint 'dist/build/Dev/FerriteLib.UiKit.dll') }
    default   {
        $releaseFingerprint = Get-Fingerprint 'dist/build/Release/FerriteLib.UiKit.dll'
        $devFingerprint = Get-Fingerprint 'dist/build/Dev/FerriteLib.UiKit.dll'
        "# artifacts (derived from the configurations this run triggered: Release and Dev):`n  $releaseFingerprint`n  $devFingerprint"
    }
}

$generatorHash = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
$head = (& git -C $root rev-parse HEAD).Trim()
$dirty = (& git -C $root status --porcelain) -join ';'
$batchMaterial = "$Subject|$Command|$Configuration|$ExitCode|$head|$dirty"
$batchHash = [BitConverter]::ToString(
    [System.Security.Cryptography.SHA256]::Create().ComputeHash(
        [Text.Encoding]::UTF8.GetBytes($batchMaterial))).Replace('-', '')

$lines = @()
$lines += if ($Recomputed) { '--- M6 FOOTER (recomputed, not recorded at run time) ---' } else { '--- M6 FOOTER ---' }
$lines += "# generator: sha256=$generatorHash"
$lines += "# batch: sha256=$batchHash"
if ($Subject) { $lines += "subject: $Subject" }
$lines += "run: $Command"
$lines += "exit: $ExitCode"
$lines += "git HEAD: $head"
$lines += "git dirty: $dirty"
if ($RanAtHead) { $lines += "# HEAD at run time (supplied by the caller, not measured here): $RanAtHead" }
if ($Recomputed) {
    $lines += '# RECOMPUTED AFTER THE RUN. This footer was written from the repository state at the moment it was'
    $lines += '#   added, not recorded while the run happened: git HEAD above is the HEAD NOW, and the fingerprints'
    $lines += '#   below are the CURRENT bytes of those files. The run-time HEAD, when given, was supplied by the'
    $lines += '#   caller rather than measured here. What this establishes is that the log was kept in a tree with'
    $lines += '#   these bytes - it does not claim the run observed them.'
}
$lines += $artifactLine
$lines += '# carriers (the frozen payload a consumer link resolves, and the paired dev package - the root one is'
$lines += '#   what a consumer still compiles against, so both are recorded as hash + mtime pairs):'
$lines += "  1.6/Assemblies/FerriteLib.UiKit.dll $(Get-Pair '1.6/Assemblies/FerriteLib.UiKit.dll')"
$lines += "  dist/dev/FerriteLib/1.6/Assemblies/FerriteLib.UiKit.dll $(Get-Pair 'dist/dev/FerriteLib/1.6/Assemblies/FerriteLib.UiKit.dll')"
if ($Gate.Count -gt 0) {
    $lines += '# gates / lanes (as this run reported them, verbatim):'
    foreach ($g in $Gate) { $lines += "  $g" }
}
if ($PinPath.Count -gt 0) {
    $lines += '# pin carriers (the files that hold the assertions this run relied on):'
    foreach ($p in ($PinPath | Select-Object -Unique)) { $lines += "  $(Get-Fingerprint $p)" }
}
$lines += '# convention: the artifact line is derived from the run configuration (FL tools/mutation/Invoke-Mutation.ps1'
$lines += '#   is the reference for the K3 rule); the carrier pair is recorded because equal bytes can be rewritten.'
$lines += '#   A footer appended to a log that already existed is marked recomputed, not recorded at run time.'

if ($ToStdout) { $lines | ForEach-Object { Write-Host $_ } }
else { Add-Content -LiteralPath $LogPath -Value $lines -Encoding UTF8; Write-Host "[evidence-footer] wrote $($lines.Count) line(s) to $LogPath (configuration=$Configuration)" }
