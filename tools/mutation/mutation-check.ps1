<#
    The mutation battery: two halves, one engine.

    Half 'Fl' runs in this repository. Half 'Us' runs in the consumer checkout next to it, and is a CROSS-REPO
    step: a public checkout has no sibling, so 'Us' refuses with a clear message rather than pretending. Both
    halves go through tools/mutation/Invoke-Mutation.ps1, which is where M1/M2/M4/M5/M6/K3/M10 live - so the
    consumer half gets the same guarantees, including the one it used to be missing (M5: after the restore,
    REBUILD the mutated configuration and assert the build succeeded).

    Why M5 matters here, in the words of the incident: the consumer half used to mutate, run, restore and
    stop. The tree was clean afterwards and 'git status' said so, while dist/build/Dev/UniversalSqueaker.dll
    had been built from the MUTATED source - a poisoned artifact any later consumer (pack-dev, a --no-build
    harness, a copy into Mods/) would take for real. Measured at roughly six minutes of poison.

    -ValidateOnly resolves every anchor and writes nothing: that is how the battery is checked before it is
    allowed to write, and how a missing anchor is shown to refuse (M1) instead of no-op.

    The commands are one-token .cmd files under commands/ because a [string[]] cannot cross a `pwsh -File`
    boundary as an array; the engine is handed an executable rather than a token list.

    Logs: dist/dev-work/ in each repository.
#>
param(
    [ValidateSet('Fl', 'Us', 'Both')][string]$Half = 'Fl',
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$SiblingRoot = (Split-Path -Parent (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path),
    [switch]$ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$engine = Join-Path $PSScriptRoot 'Invoke-Mutation.ps1'
$root = [IO.Path]::GetFullPath($ProjectRoot)
$flAnchor = "            || capture.EntryEvent == `"MouseDown`" || capture.EntryEvent == `"MouseUp`"`n            || capture.EntryEvent == `"Used`""

function Invoke-Half {
    param([string]$Name, [string[]]$Arguments)
    Write-Host "[mutation-battery] $Name"
    $argv = @('-NoProfile', '-File', $engine) + $Arguments
    if ($ValidateOnly) { $argv += '-ValidateOnly' }
    & pwsh @argv
    if ($LASTEXITCODE -ne 0) { throw "the '$Name' half failed (exit $LASTEXITCODE)." }
}

if ($Half -eq 'Fl' -or $Half -eq 'Both') {
    Invoke-Half 'Fl: dropping the entry-phase sampling is detected by the consumed-input lane' @(
        '-Name', 'fl-entry-phase-sampling',
        '-ExpectAssertion', 'Consumed input remains visible',
        '-Configuration', 'Dev',
        '-Path', 'Source/FerriteLib.UiKit/Kernel/UiDevGeometryProbe.cs',
        '-Old', $flAnchor,
        '-New', '',
        '-CommandArgs', (Join-Path $PSScriptRoot 'commands/run-fl-harness-dev.cmd'),
        '-RebuildArgs', (Join-Path $PSScriptRoot 'commands/rebuild-fl-dev.cmd'),
        '-ProjectRoot', $root)
}

if ($Half -eq 'Us' -or $Half -eq 'Both') {
    $consumer = Join-Path $SiblingRoot 'UniversalSqueaker'
    if (-not (Test-Path -LiteralPath (Join-Path $consumer '.git'))) {
        throw "the consumer checkout is not present at '$consumer'; the 'Us' half is a cross-repo step and a public checkout has no sibling. Run -Half Fl, or run this battery from a workspace that holds both repositories."
    }
    Invoke-Half 'Us: deduplicating different passes by identical text is detected' @(
        '-Name', 'us-publisher-dedup',
        '-ExpectAssertion', 'identical unclaimed releases in different passes must not',
        '-Configuration', 'Dev',
        '-Path', 'Source/UniversalSqueaker/UI/Kernel/UsTextFitAudit.cs',
        '-Old', 'string header = headerEnd < 0 ? dump : dump.Substring(0, headerEnd);',
        '-New', 'string header = presses;',
        '-CommandArgs', (Join-Path $PSScriptRoot 'commands/run-us-harness-dev.cmd'),
        '-RebuildArgs', (Join-Path $PSScriptRoot 'commands/rebuild-us-dev.cmd'),
        '-Carrier', '..\ferritelib\1.6\Assemblies\FerriteLib.UiKit.dll',
        '-ProjectRoot', $consumer)
}

Write-Host '[mutation-battery] done'