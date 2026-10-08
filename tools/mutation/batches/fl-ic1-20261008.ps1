<#
    The FL-IC1 batch (2026-10-08) as a re-runnable script.

    The faithful-revert evidence for the receive-eligibility / capture-ownership / bounded-menu slice. Each case
    puts ONE rule back the way the witnessed defect had it and names the assertion that must then redden, so a
    reader can tell a red for the mutation from a red for another reason (M2).

    What this batch is NOT: it is not a re-run of the t2, r12 or r3a batteries and does not restate their
    coverage. It covers only behaviour FL-IC1 changed - the popup-owner exemption, the chart's new-press gate, the
    menu's bounded height and published scroll range, and the element identity a capture carries. The
    edit-transaction, window Cancel/Accept and tree-cancel work (FL-IC2) is absent because it is not in this slice.

    Assertion classes, stated per case so a reader never has to upgrade a guard into proof:
      MUTATION-TARGET  the assertion that MUST redden; it is the reason the case exists.
      GUARD            an assertion that holds on BOTH sides of the mutation.

    Every anchor is a SINGLE line on purpose (the r12 note still applies): a multi-line anchor is line-ending
    sensitive, and this batch must validate on a tree checked out under either convention. Where the retired code
    needed two decisions, case c1 restores both inside one anchored block rather than only one of them - a
    mutation that left the second decision in place would redden nothing, and the engine would report it as a run
    that exited 0.

    NOTE, recorded rather than papered over: the wheel-OWNERSHIP rule (a notch inside the open menu belongs to the
    menu even where its scroll is clamped) has NO case in this batch, and that is a real limit of this evidence,
    not an oversight to hide. The pre-fix shape it replaces was
    `if (session.ScrollPopupByRows(rows)) { ConsumePointerEvent(); }` - a call and a consume on two separate
    statements - and one run restores one anchored region, so narrowing only the consume leaves the unconditional
    consume in place (no red) while a block that also deletes the second line is a two-edit change the same file
    cannot carry through two targets. That rule is therefore carried by its lane instead:
    `KernelInputEligibilityTests.VerifyWheelBelongsToTheOpenMenu` pumps a notch at the saturated end and asserts
    the event came back consumed with the scroll unmoved, which is the reading the PM's independent probe measured
    as failing before the fix. Saying so is the honest form; a fabricated single-line mutant that reddens for an
    adjacent reason would not be.

    Run it with -ValidateOnly first: that resolves every anchor and writes nothing.
#>
param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path,
    [switch]$ValidateOnly,
    [string[]]$Only
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$mutationRoot = Split-Path -Parent $PSScriptRoot
$engine = Join-Path $mutationRoot 'Invoke-Mutation.ps1'
$commands = Join-Path $mutationRoot 'commands'
$root = [IO.Path]::GetFullPath($ProjectRoot)

# (c1) the retired owner exemption, restored WITH ITS CALL CHAIN. The old rule was not a node test: the trigger
# passed its OWN popup id to two coverage decisions - the one at the anchor and the context-carrying button behind
# it - and a layer owned by that id did not cover it. The block below puts both back at the single place the
# decision is made now, so the case restores the original owner-id behaviour (close, not select) without the
# sibling regression a node-identity test would reintroduce: a sibling's id differs from the covering layer's, so
# a sibling keeps yielding, exactly as it did before the slice.
$ownerYield = '        if (covered) return false;'
$ownerExemptionBack = @'
        bool ownedByThisTrigger = string.Equals(session.OpenPopupId, elementId, StringComparison.Ordinal);
        if (covered && !ownedByThisTrigger) return false;
        if (ownedByThisTrigger)
        {
            if (!Button(rect)) return false;
            session.ClosePopup();
            return true;
        }
'@

# (c2) the chart's new-press gate, removed: the raw-pointer path is back to asking the pointer alone.
$chartGate = '        if (UiNative.IsPointerDown() && !captured && UiNative.CanReceivePointerPress(elementRect, ctx))'
$chartGateOff = '        if (UiNative.IsPointerDown() && !captured) // mutation: a new press asks nothing.'

# (c3) the menu's bounded height, un-bounded: the D4 geometry the audit confirmed as a contract violation.
$boundedHeight = '        float height = VisibleRowCount(optionCount, viewport) * OptionHeight;'
$unboundedHeight = '        float height = optionCount * OptionHeight; // mutation: the viewport bounds nothing.'

# (c4) the capture's owner, unrecorded: the pass boundary can then never tell whose draw ended.
$captureOwner = '        ownedHotControlOwner = ReferenceEquals(activeNode, unscopedNode) ? null : activeNode;'
$captureOwnerOff = '        ownedHotControlOwner = null; // mutation: the capture records no drawing element.'

# (c5) the scroll range the bounded list publishes for its own rect.
$publishExtent = '        ctx.Session.NotePopupScrollExtent(optionCount - visible);'
$publishExtentOff = '        ctx.Session.NotePopupScrollExtent(0); // mutation: the menu publishes no range.'

$cases = @(
    @{ Name = 'fl-ic1-c1-owner-exemption-restored'; Expect = 'the option row under the pointer took the click and wrote its value'; Path = 'Source/FerriteLib.UiKit/Kernel/UiNative.cs'; Old = $ownerYield; New = $ownerExemptionBack; Run = 'run-fl-harness-release.cmd'; Rebuild = 'rebuild-fl-release.cmd'; Configuration = 'Release'; Target = 'the trigger yields to the menu it owns, so the option row under that pointer selects'; Guard = 'KernelPopupTests keeps all nine of its scenarios green on both sides - the lower-trigger and same-node sibling coverage lanes included, because a sibling id differs from the covering layer and still yields' },
    @{ Name = 'fl-ic1-c2-chart-press-gate-removed'; Expect = 'the covered chart captured nothing into its session'; Path = 'Source/FerriteLib.UiKit/Kernel/Widgets/LineChartWidget.cs'; Old = $chartGate; New = $chartGateOff; Run = 'run-fl-harness-release.cmd'; Rebuild = 'rebuild-fl-release.cmd'; Configuration = 'Release'; Target = 'a chart under an open menu takes no press and commits no drag'; Guard = 'the continuation and release assertions hold on both sides - they follow the capture, never the funnel - and so does the normal drag after the menu closes' },
    @{ Name = 'fl-ic1-c3-menu-height-unbounded'; Expect = 'a forty-option menu is bounded inside the viewport instead of running past it'; Path = 'Source/FerriteLib.UiKit/Kernel/UiPopup.cs'; Old = $boundedHeight; New = $unboundedHeight; Run = 'run-fl-harness-release.cmd'; Rebuild = 'rebuild-fl-release.cmd'; Configuration = 'Release'; Target = 'the menu height is limited to the whole rows the window can show'; Guard = 'the whole-row-multiple assertion holds on both sides, and so does every short-list lane: six options sit under the bound either way' },
    @{ Name = 'fl-ic1-c4-capture-owner-unrecorded'; Expect = 'an owner that is no longer drawn ends its own capture instead of holding it forever'; Path = 'Source/FerriteLib.UiKit/Kernel/UiSession.cs'; Old = $captureOwner; New = $captureOwnerOff; Run = 'run-fl-harness-release.cmd'; Rebuild = 'rebuild-fl-release.cmd'; Configuration = 'Release'; Target = 'the capture names the element that took it, so the pass boundary can end a hold whose owner stopped being drawn'; Guard = 'the normal MouseUp release and the other-session-untouched assertions hold on both sides: neither reads the owner record' },
    @{ Name = 'fl-ic1-c5-scroll-extent-not-published'; Expect = 'the scroll stops at the end of the list instead of running past it'; Path = 'Source/FerriteLib.UiKit/Kernel/UiPopup.cs'; Old = $publishExtent; New = $publishExtentOff; Run = 'run-fl-harness-release.cmd'; Rebuild = 'rebuild-fl-release.cmd'; Configuration = 'Release'; Target = 'the bounded list publishes its own scroll range, so the wheel clamps at the end of the menu'; Guard = 'the bounded-height and whole-row assertions hold on both sides: the geometry is untouched, only the published range is wrong' }
)

$ran = 0
$outcomes = @{}
foreach ($case in $cases) {
    if ($null -ne $Only -and $Only.Count -gt 0 -and $Only -notcontains $case.Name) { continue }
    $ran++
    $label = if ($case.ContainsKey('Outcome')) { $case.Outcome } else { 'intended-red' }
    if (-not $outcomes.ContainsKey($label)) { $outcomes[$label] = 0 }
    $outcomes[$label]++
    Write-Host "[fl-ic1-batch] $($case.Name)"
    Write-Host "           mutation-target: $($case.Target)"
    Write-Host "           guard (green on both sides): $($case.Guard)"
    $argv = @('-NoProfile', '-File', $engine,
        '-Name', $case.Name, '-ExpectAssertion', $case.Expect, '-Configuration', $case.Configuration,
        '-Path', $case.Path, '-Old', $case.Old, '-New', $case.New,
        '-CommandArgs', (Join-Path $commands $case.Run),
        '-ProjectRoot', $root, '-BatchScript', $PSCommandPath,
        # The payload the harness links here is this repository's own Release build (ProjectReference), and it is
        # recorded as an INPUT. The watched carrier stays the frozen root payload alone: the Dev/Release build
        # output is what a run rebuilds, so watching it would redden every case by design (README).
        '-DriverCarrier', 'dist/build/Release/FerriteLib.UiKit.dll')
    if ($ValidateOnly) { $argv += '-ValidateOnly' }
    if ($case.ContainsKey('Outcome')) { $argv += @('-Outcome', $case.Outcome) }
    if ($case.ContainsKey('OutcomeWhy')) { $argv += @('-OutcomeWhy', $case.OutcomeWhy) }
    if ($case.ContainsKey('Rebuild')) { $argv += @('-RebuildArgs', (Join-Path $commands $case.Rebuild)) }
    & pwsh @argv
    if ($LASTEXITCODE -ne 0) { throw "the mutation '$($case.Name)' failed (exit $LASTEXITCODE)." }
}

if ($ran -eq 0) { throw 'no case matched -Only; a filter that selects nothing is not a pass.' }
$summary = ($outcomes.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ', '
Write-Host "[fl-ic1-batch] $ran case(s) done; outcomes: $summary"
