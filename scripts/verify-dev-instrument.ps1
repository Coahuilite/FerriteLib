param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Gate 10's reader: the development-only geometry instrument's DEV half really ran.
#
# Why a gate and not a habit. The instrument lives inside '#if FER_DEV' and its lane has two halves
# ('#if FER_DEV' / '#else'). The gate chain runs the harness in Release (gate 1), so before this gate the
# dev half existed only for whoever remembered to run '-c Dev' by hand - and a mutation proof that only a
# human reproduces rots silently: the lane keeps compiling, keeps being skipped, and nobody notices. The
# same bug class is already recorded in this repository: the harness project did not define FER_DEV, so the
# '#if FER_DEV' branch in KernelDocumentReloadTests had never been compiled by any run at all.
#
# What it proves, and what it refuses to accept:
#   - the Dev harness run exits 0 and ends with ALL PASS;
#   - every DEV-ONLY assertion name is present in its output. Those names exist only inside the lane's dev
#     branch, so their presence is what distinguishes "the dev half executed" from "the project compiled";
#   - every one of those names is observed as a PASSING assertion - an '  ok: ' line - and the gate prints
#     the list length and the measured count side by side. Presence anywhere was not enough: a name echoed
#     in a dump line, a FAIL line or a comment satisfied the old check, and the old report quoted the LIST
#     length as if it were the measurement;
#   - the RELEASE-only assertion name is ABSENT: in a Dev build the other half must not run, and seeing both
#     halves would mean the two branches are no longer exclusive;
#   - the printed dump itself: its header, at least a minimum number of node lines, and one press line.
#     The instrument's whole purpose is numbers, so a run that omitted them is not a passing run.
#
# '-SelfTest' (always run, it is pure string logic) plants five fixtures against the SAME checker the real
# output goes through: an empty output, an output holding only the release half, an output with a perfect
# exit code but a missing dev assertion, an output where a listed name appears only outside a passing
# assertion line, and a fully-populated output that must NOT be rejected. A checker that cannot fail is not
# a gate, and a checker that rejects everything is not one either.
#
# The harness builds into dist/build/Dev; staged deliveries and the compatibility carrier are untouched.

$root = [System.IO.Path]::GetFullPath($ProjectRoot)
$testsProject = Join-Path $root 'tools\FerriteLib.UiKit.Tests\FerriteLib.UiKit.Tests.csproj'

# Assertion names that exist only inside the lane's '#if FER_DEV' branch.
$devOnlyAssertions = @(
    'the instrument is off until a subscription asks: default off',
    'the MatchContent band reports the mode it declared',
    'the band''s recorded height is the sibling''s measured height:',
    'an element inside the scoped container is reported in the other space',
    'a scoped container is reported as a viewport rather than as an ordinary rect',
    'a claimed press names the element that claimed it',
    'a disabled element reports why the press went nowhere',
    'a later query remains visible after consumption',
    'diagnostics on and off dispatch exactly one command per native click',
    'an element under another element''s open popup layer reports the covered verdict:',
    'and it records no hit sample in the same pass:',
    'and it does not dispatch its command while the layer is above it',
    'the option row consumed the click and closed the popup, not the trigger:',
    'and with no popup layer left the same press hits it and dispatches once (fired'
)

# The name only the release half can print. Seeing it in a Dev run means the branches are not exclusive.
$releaseOnlyAssertion = 'a release payload has no instrument and refuses to pretend it has one'

$dumpHeader = '[ferritelib.geometry] pass='
$dumpNode = '    | rect path=root/row/band '
$dumpPress = '    | input path=root/row/band '

# Floors, not promises - but a floor is only a floor if the quantity it measures can move.
#
# The floor that used to live here counted 'ok:' lines and demanded 12 of them. A real Dev run prints
# thousands (measured 2026-09-24 by the independent verifier: 2681), so it could not tell a complete run
# from a mutilated one, and the gate's own report had the same defect one level up: it printed
# "N dev-only assertion(s) present" where N was $devOnlyAssertions.Count - the LIST length, quoted as if it
# were a measurement.
#
# Both are replaced by a quantity that moves with the state: how many of the listed names are observed as
# PASSING assertions ('  ok: ' lines), counted from the output by Measure-DevOnlyPassing. The floor for the
# total is derived from the list rather than a magic number, and the report states list length and measured
# count side by side.
$minimumDumpNodes = 5

# Measured 2026-09-24 on the shipped lane: list 14 named dev-only assertions, 14 observed passing, 2687
# 'ok:' lines in total, 6 dumped node lines.

function Measure-DevOnlyPassing {
    <#
        How many of the named dev-only assertions the output shows as PASSING assertions. Counted from the
        output, never from the list: a name that only appears in a dump line, a FAIL line or a comment is
        not a run assertion, and the difference between the two counts is what this gate now measures.

        It counts NAMES, not lines. The first version counted matching 'ok:' lines and this gate went red on
        a green run (12 of 11) because one named assertion is legitimately printed twice - the consumed-input
        lane loops over diagnostics on and off - so a line count is not the quantity the list describes.
    #>
    param([string]$Output)

    $lines = [regex]::Matches($Output, '(?m)^  ok: .*$')
    $measured = 0
    foreach ($name in $devOnlyAssertions) {
        foreach ($line in $lines) {
            if ($line.Value.IndexOf($name, [System.StringComparison]::Ordinal) -ge 0) { $measured++; break }
        }
    }

    return $measured
}

function Test-DevRun {
    <#
        Returns $null when the output is a real dev-half run, otherwise the reason it is not. One function for
        the real output and for every fixture, so the self-test exercises the checker the gate actually uses.
    #>
    param([string]$Output, [int]$ExitCode)

    if ($ExitCode -ne 0) {
        # Name the assertions that failed, not only the exit code: a red gate is read by a person, and the
        # harness's own output is otherwise thrown away by Invoke-Check's capture.
        $failed = @([regex]::Matches($Output, '(?m)^  FAIL: .*') | ForEach-Object { $_.Value.Trim() })
        $named = if ($failed.Count -eq 0) { 'no FAIL line was printed' } else { ($failed | Select-Object -First 5) -join ' | ' }
        return "the Dev harness run exited $ExitCode ($named)"
    }
    if ($Output -notmatch 'ALL PASS') { return 'the Dev harness run did not report ALL PASS' }

    foreach ($name in $devOnlyAssertions) {
        if ($Output.IndexOf($name, [System.StringComparison]::Ordinal) -lt 0) {
            return "the dev half did not run (or lost the assertion): no '$name'. A Dev build compiles it only when the tests project defines FER_DEV, and a lane that is compiled out is a lane that never runs."
        }
    }

    if ($Output.IndexOf($releaseOnlyAssertion, [System.StringComparison]::Ordinal) -ge 0) {
        return "the release-only assertion ran in a Dev build: the two halves of the lane are no longer exclusive ('$releaseOnlyAssertion')"
    }

    # The measured half. Every listed name must be observed as a PASSING assertion, not merely present
    # somewhere in the text, and the two counts are reported together so the gate's own words cannot be
    # mistaken for a measurement again.
    $measured = Measure-DevOnlyPassing -Output $Output
    if ($measured -ne $devOnlyAssertions.Count) {
        return "the dev half showed $measured of the $($devOnlyAssertions.Count) named dev-only assertion(s) as passing '  ok: ' line(s); a name that appears only in a dump line, a FAIL line or a comment is not a run assertion"
    }

    # A floor derived from the list, not a magic number: a run that printed fewer 'ok:' lines than it has
    # named dev-only assertions is not a populated run.
    $assertions = ([regex]::Matches($Output, '(?m)^  ok: ')).Count
    if ($assertions -lt $devOnlyAssertions.Count) {
        return "the run printed $assertions 'ok:' line(s), fewer than the $($devOnlyAssertions.Count) named dev-only assertions; a partially populated run is the empty-enumeration failure this gate is written against"
    }

    if ($Output.IndexOf($dumpHeader, [System.StringComparison]::Ordinal) -lt 0) {
        return "the instrument printed no dump header ('$dumpHeader'); the lane ran but the numbers did not"
    }

    $nodes = ([regex]::Matches($Output, '(?m)^    \| (rect|viewport) ')).Count
    if ($nodes -lt $minimumDumpNodes) {
        return "the dump carried $nodes node line(s), below the floor of $minimumDumpNodes"
    }

    if ($Output.IndexOf($dumpNode, [System.StringComparison]::Ordinal) -lt 0) {
        return "the dump does not name the MatchContent band ('$dumpNode')"
    }

    if ($Output.IndexOf($dumpPress, [System.StringComparison]::Ordinal) -lt 0) {
        return "the dump carries no press line ('$dumpPress'); the input half of the instrument was not exercised"
    }

    return $null
}

function Assert-DevRun {
    param([string]$Output, [int]$ExitCode, [string]$Label)
    $reason = Test-DevRun -Output $Output -ExitCode $ExitCode
    if ($reason) { throw "$Label is not a dev-half run: $reason" }
}

# --- the checker's own control, on every run ---------------------------------------------------------
# A fixture is only a control if it goes through the same function the real output does.
$controlSample = @(
    'ALL PASS'
    '  ok: the instrument is off until a subscription asks: default off'
    '  ok: the MatchContent band reports the mode it declared'
    "  ok: the band's recorded height is the sibling's measured height: 30 vs 30"
    '  ok: an element inside the scoped container is reported in the other space'
    '  ok: a scoped container is reported as a viewport rather than as an ordinary rect'
    '  ok: a claimed press names the element that claimed it'
    '  ok: a disabled element reports why the press went nowhere'
    '  ok: a later query remains visible after consumption'
    '  ok: diagnostics on and off dispatch exactly one command per native click'
    '  ok: an element under another element''s open popup layer reports the covered verdict: input path=root/under kind=input/button point=(50,30) rect=(0,28,100,28) verdict=covered event-before=MouseDown event-after=MouseDown'
    '  ok: and it records no hit sample in the same pass: input path=root/under kind=input/button point=(100,30) rect=(0,28,200,28) verdict=covered event-before=MouseDown event-after=MouseDown'
    '  ok: and it does not dispatch its command while the layer is above it (fired 0 time(s))'
    '  ok: the option row consumed the click and closed the popup, not the trigger: value=''x'' popup-open=False'
    '  ok: and with no popup layer left the same press hits it and dispatches once (fired 1 time(s)): input path=root/under kind=input/button point=(100,30) rect=(0,28,200,28) verdict=hit event-before=MouseDown event-after=MouseDown'
    '  ok: filler 3'
    '  ok: filler 4'
    '  ok: filler 5'
    '    | [ferritelib.geometry] pass=1 nodes=6 nodes-dropped=0 inputs=1 inputs-dropped=0'
    '    | rect path=root kind=Column'
    '    | rect path=root/row kind=Row'
    '    | rect path=root/row/band kind=input/button'
    '    | rect path=root/row/caption kind=chrome/banner'
    '    | viewport path=root/list kind=Scroll'
    '    | rect path=root/list/inner kind=chrome/banner'
    '    | input path=root/row/band kind=input/button point=(50,15) rect=(0,0,100,30) verdict=hit'
) -join "`n"

if (Test-DevRun -Output $controlSample -ExitCode 0) {
    throw "the dev-instrument checker rejects a fully populated dev-half sample: $(Test-DevRun -Output $controlSample -ExitCode 0)"
}

$controlCases = @(
    @{ Name = 'an empty output';                      Output = '';                                                        ExitCode = 0 },
    @{ Name = 'a release-half-only output';           Output = "ALL PASS`n  ok: $releaseOnlyAssertion";                   ExitCode = 0 },
    @{ Name = 'a populated output with exit code 1';  Output = $controlSample;                                            ExitCode = 1 },
    @{ Name = 'a populated output missing one name';  Output = ($controlSample -replace [regex]::Escape($devOnlyAssertions[2]), 'x'); ExitCode = 0 },
    # The discriminator the old presence-only check lacked: the name is still in the text, but not as a
    # passing assertion line, so the measured count is one short of the list.
    @{ Name = 'a listed name that is not a passing assertion'; Output = ($controlSample -replace [regex]::Escape('  ok: a disabled element reports why the press went nowhere'), '    | rect path=root/row/band kind=input/button note="a disabled element reports why the press went nowhere"'); ExitCode = 0 }
)

foreach ($case in $controlCases) {
    if (-not (Test-DevRun -Output $case.Output -ExitCode $case.ExitCode)) {
        throw "the dev-instrument checker passed '$($case.Name)'; a checker that cannot fail is not a gate"
    }
}

# --- the real run ------------------------------------------------------------------------------------
$log = Join-Path ([System.IO.Path]::GetTempPath()) ("fl-dev-instrument-" + [guid]::NewGuid().ToString('N') + '.log')
dotnet run --no-restore --project $testsProject -c Dev *> $log
$code = $LASTEXITCODE
try {
    $output = if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Raw } else { '' }
} finally {
    Remove-Item -LiteralPath $log -Force -ErrorAction SilentlyContinue
}

Assert-DevRun -Output $output -ExitCode $code -Label 'the Dev harness run'

$assertions = ([regex]::Matches($output, '(?m)^  ok: ')).Count
$nodes = ([regex]::Matches($output, '(?m)^    \| (rect|viewport) ')).Count
$measured = Measure-DevOnlyPassing -Output $output
# List length and measured count, side by side: the old wording printed the list constant as if it were
# the number of assertions found, which is what made the floor look like it was measuring something.
Write-Host ("dev half ran: list {0} named dev-only assertion(s), measured {1} passing, {2} 'ok:' line(s) total, {3} dumped node line(s)" -f $devOnlyAssertions.Count, $measured, $assertions, $nodes)
