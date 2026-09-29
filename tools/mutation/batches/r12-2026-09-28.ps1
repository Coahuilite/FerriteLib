<#
    The R12-CORR batch (2026-09-28) as a re-runnable script.

    The faithful-revert evidence for the R12-T / R12-A corrections and the four behavioural fixes that came
    with them. Each case is ONE change, and the assertion it must redden is named in -ExpectAssertion, so a
    reader can tell a red for the mutation from a red for another reason (M2).

    What this batch is NOT: it is not a re-run of the t2 battery, and it does not restate that battery's
    coverage. It covers only behaviour CHANGED by this batch, plus the two switch tokens whose draw
    observation this batch had to add.

    Assertion classes, stated per case so a reader never has to guess which half is proof:
      MUTATION-TARGET  the assertion that MUST redden; it is the reason the case exists.
      GUARD            an assertion that holds on BOTH sides of the mutation. It is regression protection,
                       not evidence, and it is listed so nobody upgrades it to proof by reading it here.

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

# (a) typography is APPLIED: the one line that carries a selected scheme's font onto the theme.
$applyFont = '                if (definition.Font.HasValue) ApplyFont(theme, definition.Font.Value);'

# (a2) the font is not a distance: the same method, with density moved beside it.
$applyFontBody = '        theme.DefaultFont = font;'
$applyFontAsSpacing = @'
        theme.DefaultFont = font;
        // mutation: the font is implemented as a spacing change.
        theme.Geometry = new UiGeometry(theme.Geometry.Padding, theme.Geometry.Spacing, theme.Geometry.Gap, 30f, theme.Geometry.Hairline);
'@

# (b) the scoped clone cache expires on the PAINT clock as well as the layout one.
$colourClock = '        if (baselineRevision != observedBaselineRevision || baselineColourRevision != observedBaselineColourRevision)'
# The field stays READ (a discard), because a mutation that leaves a private field write-only would redden the
# run for the wrong reason: CS0414 plus TreatWarningsAsErrors fails the BUILD, and a build failure cannot print
# the assertion this case exists to redden.
$layoutClockOnly = @'
        _ = observedBaselineColourRevision; // mutation: the paint clock is still read, but no longer expires the scope cache.
        if (baselineRevision != observedBaselineRevision)
'@

# (c) an assignment CLAIMS its token, so the Vanilla fallback cannot repaint an explicit transparent. The
# anchor is a SINGLE line on purpose: a multi-line anchor is line-ending sensitive, and this batch must
# validate on a tree that may have been checked out with either convention.
$claimKept = '        // A colour assignment moves the paint clock and never the layout clock.'
$claimDropped = '        claimed &= ~bit; // mutation: the assignment does not keep its claim, so the fallback repaints the token.'

# (d) the selected look's body enters natural size.
$naturalBody = '            return SwitchTrackWidth + (HasLabel(spec) ? SwitchLabelGap : 0f);'

# (e) the OFF outline is the control edge, not the raised surface's own edge. Single-line anchor, same reason.
$controlEdge = '        return theme.BorderStrong.a > 0f ? theme.BorderStrong : theme.Border;'
$controlEdgeAsRaised = '        return theme.RaisedSurface.Border; // mutation: the OFF edge reads the raised surface''s own edge again.'

# (e2) the restored ON thumb: the accent, not the selected plane's text colour.
$knobAccent = '        UiThemeDraw.Solid(knobRect, state ? theme.AccentGold : theme.TextPrimary);'

$cases = @(
    @{ Name = 'r12-c1-selected-font-not-applied'; Expect = 'an Auto-width element measures WIDER under the selected Medium font than under Small'; Path = 'Source/FerriteLib.UiKit/Kernel/UiStyleResolver.cs'; Old = $applyFont; New = '                // mutation: a selected scheme''s font is never applied.'; Run = 'run-fl-harness-release.cmd'; Rebuild = 'rebuild-fl-release.cmd'; Configuration = 'Release'; Target = 'the real host measures the same width under both selected fonts'; Guard = 'the density equality assertion in the same lane holds on both sides' },
    @{ Name = 'r12-c2-font-applied-as-density'; Expect = 'and the legacy font moved NO distance: density is a separate axis'; Path = 'Source/FerriteLib.UiKit/Kernel/UiStyleResolver.cs'; Old = $applyFontBody; New = $applyFontAsSpacing; Run = 'run-fl-harness-release.cmd'; Rebuild = 'rebuild-fl-release.cmd'; Configuration = 'Release'; Target = 'applying a font also moves RowHeight, which is the defect the axis split removes'; Guard = 'the font-selection assertions hold on both sides' },
    @{ Name = 'r12-c3-scope-cache-not-expired-on-colour'; Expect = 'after a re-tint the scope resolves the new colour: the cached clone was dropped, not kept stale'; Path = 'Source/FerriteLib.UiKit/Kernel/UiStyleResolver.cs'; Old = $colourClock; New = $layoutClockOnly; Run = 'run-fl-harness-release.cmd'; Rebuild = 'rebuild-fl-release.cmd'; Configuration = 'Release'; Target = 'the cached region clone keeps painting the palette it was built with'; Guard = 'the same-rect and same-snapshot assertions hold on both sides (a colour is not layout-bearing either way)' },
    @{ Name = 'r12-c4-assignment-does-not-claim'; Expect = 'an explicitly transparent colour is a VALUE: the fallback must not repaint it'; Path = 'Source/FerriteLib.UiKit/Kernel/UiTheme.cs'; Old = $claimKept; New = $claimDropped; Run = 'run-fl-harness-release.cmd'; Rebuild = 'rebuild-fl-release.cmd'; Configuration = 'Release'; Target = 'an explicitly transparent fill is repainted by the Vanilla fallback, because the assignment keeps no claim'; Guard = 'the role-mapping assertions in KernelToneVocabularyTests hold on both sides: every role still resolves, only the claim is gone' },
    @{ Name = 'r12-c5-look-body-not-in-natural-size'; Expect = 'an unlabelled bool control with the default appearance reserves its own body, not zero'; Path = 'Source/FerriteLib.UiKit/Kernel/Widgets/CheckboxWidget.cs'; Old = $naturalBody; New = '            return 0f; // mutation: the look''s body no longer enters natural size.'; Run = 'run-fl-harness-release.cmd'; Rebuild = 'rebuild-fl-release.cmd'; Configuration = 'Release'; Target = 'Auto measures text only, so a body-only control reserves nothing'; Guard = 'the labelled and explicit-Height cases still measure the same on both sides' },
    @{ Name = 'r12-c6-off-edge-reads-raised-surface'; Expect = 'and outlines with the theme''s control edge, so a flattened raised surface cannot hide it'; Path = 'Source/FerriteLib.UiKit/Kernel/Widgets/CheckboxWidget.cs'; Old = $controlEdge; New = $controlEdgeAsRaised; Run = 'run-fl-harness-release.cmd'; Rebuild = 'rebuild-fl-release.cmd'; Configuration = 'Release'; Target = 'under a scope whose raised fill and edge are one colour, the OFF track disappears'; Guard = 'the OFF fill assertion still holds on both sides' },
    @{ Name = 'r12-c7-on-thumb-not-the-accent'; Expect = 'an ON switch paints its THUMB in the accent token, not in the selected plane''s text colour'; Path = 'Source/FerriteLib.UiKit/Kernel/Widgets/CheckboxWidget.cs'; Old = $knobAccent; New = '        UiThemeDraw.Solid(knobRect, state ? theme.TextOnGold : theme.TextPrimary); // mutation: the restored ON accent is undone.'; Run = 'run-fl-harness-release.cmd'; Rebuild = 'rebuild-fl-release.cmd'; Configuration = 'Release'; Target = 'the ON thumb wears the selected plane''s TEXT colour instead of the accent'; Guard = 'the OFF-thumb ink and no-accent-at-all assertions hold on both sides' }
)

$ran = 0
$outcomes = @{}
foreach ($case in $cases) {
    if ($null -ne $Only -and $Only.Count -gt 0 -and $Only -notcontains $case.Name) { continue }
    $ran++
    $label = if ($case.ContainsKey('Outcome')) { $case.Outcome } else { 'intended-red' }
    if (-not $outcomes.ContainsKey($label)) { $outcomes[$label] = 0 }
    $outcomes[$label]++
    Write-Host "[r12-batch] $($case.Name)"
    Write-Host "           mutation-target: $($case.Target)"
    Write-Host "           guard (green on both sides): $($case.Guard)"
    $argv = @('-NoProfile', '-File', $engine,
        '-Name', $case.Name, '-ExpectAssertion', $case.Expect, '-Configuration', $case.Configuration,
        '-Path', $case.Path, '-Old', $case.Old, '-New', $case.New,
        '-CommandArgs', (Join-Path $commands $case.Run),
        '-ProjectRoot', $root, '-BatchScript', $PSCommandPath,
        # The payload the harness links here is this repository's own Release build (ProjectReference), and it
        # is recorded as an INPUT; the watched carrier stays the frozen root one, deliberately single.
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
Write-Host "[r12-batch] $ran case(s) done; outcomes: $summary"
