<#
    The FL-IC2 batch (2026-10-08) as a re-runnable script.

    The faithful-revert evidence for the edit-transaction / native-hook / tree-cancel slice. Each case puts ONE
    rule back the way the witnessed defect had it and names the assertion that must then redden, so a reader can
    tell a red for the mutation from a red for another reason (M2).

    What this batch is NOT: it is not a re-run of the FL-IC1, t2, r12 or r3a batteries and does not restate their
    coverage. It covers only behaviour FL-IC2 changed - the commit-once rule a deferred editor had lost, the
    coverage pause, the two window keys and what they consume, the Cancel ladder's order and its climb, and the
    lifecycle of the three interaction records. The receive-eligibility, capture-ownership and bounded-menu rules
    are FL-IC1's and are not mutated here.

    Three of these cases (c3, c4, c5) are the witnesses the PM ran against the in-progress copy and recorded in
    evidence/interaction-development-20261008/fl/pm-ic2-child-probe/ and its recheck directory. Restoring the
    shape those probes measured is the point: a case that reddened nothing would mean the lane had been passing
    for some reason other than the rule.

    Assertion classes, stated per case so a reader never has to upgrade a guard into proof:
      MUTATION-TARGET  the assertion that MUST redden; it is the reason the case exists.
      GUARD            an assertion that holds on BOTH sides of the mutation.

    Every anchor is a SINGLE line on purpose (the r12 note still applies): a multi-line anchor is line-ending
    sensitive, and this batch must validate on a tree checked out under either convention. Where the retired code
    needed two decisions, the case restores both inside one anchored block rather than only one of them - a
    mutation that left the second decision in place would redden nothing, and the engine would report it as a run
    that exited 0.

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

# (c1) witness 2, restored: the deferred editor's exit frame answers "no commit", so the parsed draft is still in
# the session when the next frame resets the buffer onto the model. This is the half the string field already
# stated and the numeric field did not - the drift the contract's single-rule note exists to prevent.
$exitCommit = '            if (parsed && Math.Abs(state.FloatValue - value) > 0.0001f) committed = true;'
$exitCommitOff = '            // mutation: the exit frame reports no commit, so a deferred draft is dropped.'

# (c2) coverage ends an edit instead of pausing it: the pre-slice covered branch, which reset the buffer onto the
# model and dropped focus, so a menu opening over a field answered the edit for the player.
$coveredPause = @'
            committed = false;
            return state.Focused ? state.EditText : FormatValue(ClampValue(value, min, max), format);
'@
$coveredEndsEdit = @'
            state.Focused = false;
            state.FloatValue = ClampValue(value, min, max);
            state.EditText = FormatValue(state.FloatValue, format);
            session.NoteEditClosed(state);
            committed = false;
            return state.EditText;
'@

# (c3) the PM's hidden-edit witness, restored: the pass boundary clears the RECORD and leaves the state's own
# Focused flag standing, so a field that comes back is still editing while the entry that ends an edit is empty.
$hiddenEndsEdit = '                dropped.Focused = false;'
$hiddenRecordOnly = '                // mutation: only the reference goes; the state keeps claiming an open edit.'

# (c4) the PM's child-cancel witness, restored at the line that drops the subject: liveness read as geometry
# alone, which is exactly the test that discards a press on a composite's own sub-control, because a child node
# has no arranged rect by construction.
$inPlayChain = '        if (lastInteractionNode != null && !InPlay(lastInteractionNode)) lastInteractionNode = null;'
$inPlayGeometryOnly = '        if (lastInteractionNode != null && !lastInteractionNode.IsArranged) lastInteractionNode = null; // mutation: a child with no rect never counted as a subject.'

# (c5) the PM's blur witness, restored: the blur reads only a still-live MouseDown, so an ordinary control drawn
# earlier in the pass consumes the press and the field never learns the edit is over.
$blurPressFact = '        return session.PassBeganWithPrimaryPress && !IsMouseOver(rect);'
$blurLiveOnly = '        return false; // mutation: a press someone else consumed is not evidence of a click.'

# (c6) the ladder's first layer, removed: the popup stops being what a Cancel answers, which is the ordering the
# contract puts first because a menu is the topmost thing on screen.
$popupStep = '        if (session.OpenPopupId != null)'
$popupStepOff = '        if (session.OpenPopupId != null && false) // mutation: the open menu is no longer the first layer a Cancel answers.'

# (c7) the ladder answers an edit and leaves the key live: the game re-reads an unconsumed Cancel key at the end
# of the same window pass, finds the edit already gone, and closes the window the press had just used - which is
# the "one key, two returns" failure the consumption rule exists to prevent.
$editConsume = @'
            edit.DiscardRequested = true;
            UiNative.ConsumeKeyEvent();
'@
$editNoConsume = @'
            edit.DiscardRequested = true;
            // mutation: the answered key is left live for the rest of the window pass.
'@

# (c8) the shell hands the key straight back to Verse: no page layer is offered first refusal at all, so Enter on
# an open edit closes the settings window instead of answering the edit.
$acceptHook = '        if (page != null && page.TryHandleAccept()) return;'
$acceptHookOff = '        if (page != null && false) return; // mutation: the page is never offered the Accept key.'

$cases = @(
    @{ Name = 'fl-ic2-c1-deferred-exit-commit-removed'; Expect = 'the frame that ends the edit commits the parsed draft'; Path = 'Source/FerriteLib.UiKit/Kernel/UiNative.cs'; Old = $exitCommit; New = $exitCommitOff; Run = 'run-fl-harness-release.cmd'; Rebuild = 'rebuild-fl-release.cmd'; Configuration = 'Release'; Target = 'the exit frame of a deferred editor carries its own commit, which is the half witness 2 was missing'; Guard = 'the live-field keystroke lane and the invalid-draft lane hold on both sides - one writes on the keystroke, the other never parses - and so does the string field, whose rule this mutation does not touch' },
    @{ Name = 'fl-ic2-c2-covered-edit-ended-not-paused'; Expect = 'the edit is paused, not ended'; Path = 'Source/FerriteLib.UiKit/Kernel/UiNative.cs'; Old = $coveredPause; New = $coveredEndsEdit; Run = 'run-fl-harness-release.cmd'; Rebuild = 'rebuild-fl-release.cmd'; Configuration = 'Release'; Target = 'a popup drawn over a field pauses the edit instead of answering it'; Guard = 'the no-native-call and no-write halves hold on both sides: the mutation ends the edit, it does not start reaching the native control, which is why the pause assertion is the one that reddens' },
    @{ Name = 'fl-ic2-c3-hidden-edit-clears-reference-only'; Expect = 'hiding the field ended the edit on the state itself'; Path = 'Source/FerriteLib.UiKit/Kernel/UiSession.cs'; Old = $hiddenEndsEdit; New = $hiddenRecordOnly; Run = 'run-fl-harness-release.cmd'; Rebuild = 'rebuild-fl-release.cmd'; Configuration = 'Release'; Target = 'the record and the field state move together when the element leaves the page, which is the PM hidden-edit witness'; Guard = 'the draft-stays-unwritten assertion holds on both sides, and so does the disabled-element lane: that path ends the edit inside the funnel, not at the pass boundary' },
    @{ Name = 'fl-ic2-c4-child-liveness-by-geometry'; Expect = 'the session still holds it as the subject after the pass boundary'; Path = 'Source/FerriteLib.UiKit/Kernel/UiSession.cs'; Old = $inPlayChain; New = $inPlayGeometryOnly; Run = 'run-fl-harness-release.cmd'; Rebuild = 'rebuild-fl-release.cmd'; Configuration = 'Release'; Target = 'a composite child inherits liveness from the element that minted it, which is the PM child-cancel witness'; Guard = 'the hidden-subject lane holds on both sides: a hidden element is unarranged whatever the rule, so only the geometry-less child distinguishes them' },
    @{ Name = 'fl-ic2-c5-blur-needs-a-live-press'; Expect = 'the press another control consumed still committed the draft'; Path = 'Source/FerriteLib.UiKit/Kernel/UiNative.cs'; Old = $blurPressFact; New = $blurLiveOnly; Run = 'run-fl-harness-release.cmd'; Rebuild = 'rebuild-fl-release.cmd'; Configuration = 'Release'; Target = 'an outside click ends a deferred edit even when a control drawn earlier consumed it, which is the PM blur witness'; Guard = 'the blank-space click commits on both sides (its MouseDown is still live when the field reads it), and the covered-pause lane holds because coverage returns before the blur is consulted' },
    @{ Name = 'fl-ic2-c6-ladder-answers-no-popup'; Expect = 'neither the tree layer nor the window was touched by the same press'; Path = 'Source/FerriteLib.UiKit/Kernel/UiHost.cs'; Old = $popupStep; New = $popupStepOff; Run = 'run-fl-harness-release.cmd'; Rebuild = 'rebuild-fl-release.cmd'; Configuration = 'Release'; Target = 'the open option menu is the first layer a Cancel answers, so the key stops there instead of reaching Verse'; Guard = 'the tree lanes hold on both sides - they start with no menu open - and so does the capture lane, which is the step below' },
    @{ Name = 'fl-ic2-c7-answered-key-not-consumed'; Expect = 'the window stays open'; Path = 'Source/FerriteLib.UiKit/Kernel/UiHost.cs'; Old = $editConsume; New = $editNoConsume; Run = 'run-fl-harness-release.cmd'; Rebuild = 'rebuild-fl-release.cmd'; Configuration = 'Release'; Target = 'one press performs one undo: a key the ladder answered must be consumed, or the game closes the window at the end of the same pass'; Guard = 'the discard-writes-nothing and edit-closed assertions hold on both sides - the answer itself is unchanged - which is why the window-stays-open assertion is the one that reddens' },
    @{ Name = 'fl-ic2-c8-shell-skips-page-accept'; Expect = 'the open edit committed exactly once'; Path = 'Source/FerriteLib.UiKit/Kernel/UiWindowHost.cs'; Old = $acceptHook; New = $acceptHookOff; Run = 'run-fl-harness-release.cmd'; Rebuild = 'rebuild-fl-release.cmd'; Configuration = 'Release'; Target = 'Enter answers the edit the page holds before it can mean close the window'; Guard = 'the no-edit-close lane holds on both sides: with no edit open the shell calls Verse either way, which is the preservation half of the slice' }
)

$ran = 0
$outcomes = @{}
foreach ($case in $cases) {
    if ($null -ne $Only -and $Only.Count -gt 0 -and $Only -notcontains $case.Name) { continue }
    $ran++
    $label = if ($case.ContainsKey('Outcome')) { $case.Outcome } else { 'intended-red' }
    if (-not $outcomes.ContainsKey($label)) { $outcomes[$label] = 0 }
    $outcomes[$label]++
    Write-Host "[fl-ic2-batch] $($case.Name)"
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
Write-Host "[fl-ic2-batch] $ran case(s) done; outcomes: $summary"
