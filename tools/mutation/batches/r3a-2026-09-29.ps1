<#
    The R3-A batch — PROPOSED 2026-09-29, NOT RUN by the authoring session (the DSH shell fails OS-side,
    SetNamedSecurityInfoW Win32 5; the build/verification slot is the PM's).

    It is written in the tracked batch format so it can be validated and run like any other battery:
      pwsh -NoProfile -File tools/mutation/batches/r3a-2026-09-29.ps1 -ValidateOnly
      pwsh -NoProfile -File tools/mutation/batches/r3a-2026-09-29.ps1
      pwsh -NoProfile -File tools/mutation/batches/r3a-2026-09-29.ps1 -Only r3a-c2-clip-read-from-the-draw-rect

    Every case mutates the development-only instrument, so the whole batch runs in the DEV configuration:
    the lane that observes these facts lives in the `#if FER_DEV` half of KernelDevGeometryTests, and a
    Release payload has no capture to mutate. The Fl half must NOT pass -p:FerriteLibArtifactPath (the
    README's recorded trap): the harness's ProjectReference puts the library in dist/build/Dev, which is the
    artifact this batch fingerprints.

    Cases cover only the behaviour this slice ADDED or re-pointed. It does not repeat the R12 battery and it
    does not restate the T2 coverage.

    Per case: MUTATION-TARGET is the assertion that must redden (the reason the case exists); GUARD is an
    assertion that holds on BOTH sides and is listed so nobody upgrades it to proof.
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
$probe = 'Source/FerriteLib.UiKit/Kernel/UiDevGeometryProbe.cs'

# (R3-A.1) the two renderings must agree: the text dump prints the same effective clip the snapshot carries.
$dumpClip = '                .Append(" clip=").Append(Boundary(sample.Clip, UiDevNodeSnapshot.ClipUnknownReason))'
$dumpClipAsWindow = '                .Append(" clip=").Append(Rect(sample.Window))'

# (R3-A.2) the clip is the boundary actually in force, not the element's own rect.
$effectiveClip = '            Clip = ctx.Session.CurrentClip,'
$ownRectClip = '            Clip = ctx.ToWindowRect(drawRect),'

# (R3-A.2) an unobservable boundary stays unknown; filling it with a plausible rect is the failure this
# ordering exists to prevent. The anchor is the SNAPSHOT side only, so the case also exercises the
# dump-versus-snapshot agreement: the text still prints the documented unknown while the data claims a rect.
$hoverUnknown = '                    : UiDevBoundary.Unknown(UiDevNodeSnapshot.HoverUnknownReason),'
$hoverFilled = '                    : UiDevBoundary.Known(sample.Window),'

# (R3-A.2) identity is the canonical key, not the display path.
$canonicalKey = '            NodeKey = node.Id.Key,'
$displayKey = '            NodeKey = node.Path,'

# (R3-A.3) the bounded capture counts what it dropped.
$droppedCount = '            DroppedGeometry++;'
$droppedCountKept = '            // mutation: the dropped node is not counted.'

# (R3-A.1) the overlay is a rendering of the bounded capture, so it must not paint what was dropped.
$overlayRetained = '        if (capture == null || !capture.Overlay || !retained) return;'
$overlayUnbounded = '        if (capture == null || !capture.Overlay) return;'

# (R3-A.3) a snapshot is a COPY, and the mutant has to be a LIVE VIEW for this case to mean anything.
#
# The first version of this case cached the FIRST snapshot (`reused ??= new UiDevGeometrySnapshot { ... }`),
# which only stops later calls from allocating: the snapshot goes STALE, and the named assertion - an old
# snapshot still describing the earlier pass - stays GREEN, so the case proved nothing about live views. The
# production seam is `Snapshot()` creating a fresh instance and calling `Fill(...)`, which assigns EVERY field
# on every call; reusing that instance here therefore REFRESHES IT IN PLACE, so an already-handed-out
# snapshot follows the next draw. Two single-line anchors: the cache field, then the instance Fill writes
# into. The old cache field is declared nullable, so the mutated build stays warning-clean under
# TreatWarningsAsErrors - a case that fails to COMPILE is not the intended red.
$cacheField = '    private readonly List<UiDevGeometrySample> geometry = new List<UiDevGeometrySample>();'
$cacheFieldAdded = $cacheField + "`n" + '    private UiDevGeometrySnapshot? reused;'
$freshInstance = '        UiDevGeometrySnapshot snapshot = new UiDevGeometrySnapshot();'
$reusedInstance = '        UiDevGeometrySnapshot snapshot = reused ??= new UiDevGeometrySnapshot();'

# (R3-A.2) the effective appearance: reporting the kind's DEFAULT where the element declared a supported look
# is the defect this case plants, and the declared-vs-default pair is what reddens.
$resolvedLook = '            Appearance = appearance.Value,'
$defaultLook = '            Appearance = appearance.Default,'

$cases = @(
    @{ Name = 'r3a-c1-dump-renders-a-different-clip'; Expect = 'and the same effective clip for '; Path = $probe; Old = $dumpClip; New = $dumpClipAsWindow; Run = 'run-fl-harness-dev.cmd'; Rebuild = 'rebuild-fl-dev.cmd'; Configuration = 'Dev'; Target = 'the text rendering and the data rendering describe the same capture, fact by fact'; Guard = 'the pass and count agreement assertions hold on both sides' },
    @{ Name = 'r3a-c2-clip-read-from-the-draw-rect'; Expect = 'and a scoped container''s own sample carries the clip it was drawn INSIDE'; Path = $probe; Old = $effectiveClip; New = $ownRectClip; Run = 'run-fl-harness-dev.cmd'; Rebuild = 'rebuild-fl-dev.cmd'; Configuration = 'Dev'; Target = 'the clip is the boundary in force (the intersection), not the element''s own rect'; Guard = 'the clip-known assertions hold on both sides: every case still reports A rect' },
    @{ Name = 'r3a-c3-unknown-hover-filled-with-a-rect'; Expect = 'hover is unknown at a geometry sample'; Path = $probe; Old = $hoverUnknown; New = $hoverFilled; Run = 'run-fl-harness-dev.cmd'; Rebuild = 'rebuild-fl-dev.cmd'; Configuration = 'Dev'; Target = 'an unobservable value is left explicitly unknown, never filled with a plausible default - and, because both renderings read the sample field, the dump/snapshot hover agreement assertion reddens with it'; Guard = 'the focus-unknown assertion holds on both sides (this mutation does not touch it)' },
    @{ Name = 'r3a-c4-identity-from-the-display-string'; Expect = 'the identity is the node''s CANONICAL key rather than a re-derived display string'; Path = $probe; Old = $canonicalKey; New = $displayKey; Run = 'run-fl-harness-dev.cmd'; Rebuild = 'rebuild-fl-dev.cmd'; Configuration = 'Dev'; Target = 'the node identity carried is the canonical key, not the display path'; Guard = 'the dump/snapshot key agreement holds on both sides, because both renderings read the same field' },
    @{ Name = 'r3a-c5-dropped-count-not-kept'; Expect = 'so the dropped count is the real remainder, not a marker that happens to be non-zero: '; Path = $probe; Old = $droppedCount; New = $droppedCountKept; Run = 'run-fl-harness-dev.cmd'; Rebuild = 'rebuild-fl-dev.cmd'; Configuration = 'Dev'; Target = 'a bounded capture reports how many nodes it did not keep'; Guard = 'the retained-head count assertion holds on both sides: the bound itself is untouched' },
    @{ Name = 'r3a-c6-overlay-paints-dropped-entries'; Expect = 'the overlay outlines exactly the entries the bounded capture kept'; Path = $probe; Old = $overlayRetained; New = $overlayUnbounded; Run = 'run-fl-harness-dev.cmd'; Rebuild = 'rebuild-fl-dev.cmd'; Configuration = 'Dev'; Target = 'the overlay is the third rendering of the same bounded capture, so it outlines only what was retained'; Guard = 'the overlay refusal/acceptance assertions hold on both sides' },
    @{ Name = 'r3a-c7-snapshot-is-a-live-view-not-a-copy'; Expect = 'still describes the EARLIER pass, unchanged'; Path = $probe; Old = $cacheField; New = $cacheFieldAdded; Path2 = $probe; Old2 = $freshInstance; New2 = $reusedInstance; Run = 'run-fl-harness-dev.cmd'; Rebuild = 'rebuild-fl-dev.cmd'; Configuration = 'Dev'; Target = 'a snapshot is a COPY: an instance reused by the collector is refreshed in place by Fill, so the snapshot taken before a second draw on the SAME host would follow it and stop describing the earlier pass (two anchors: the cache field, then the instance Fill writes into)'; Guard = 'the new-pass advancement assertion holds on BOTH sides (the live instance does advance), and so does the cross-host isolation - which is exactly why the same-host redraw is the case' },
    @{ Name = 'r3a-c8-declared-look-reported-as-the-default'; Expect = 'and one that declares the other look reports the declared value'; Path = $probe; Old = $resolvedLook; New = $defaultLook; Run = 'run-fl-harness-dev.cmd'; Rebuild = 'rebuild-fl-dev.cmd'; Configuration = 'Dev'; Target = 'the effective appearance is the RESOLVED value: reporting the kind''s default instead reddens the declared-vs-default pair, while the default case stays green'; Guard = 'the no-seam and provenance assertions hold on both sides (this mutation does not touch them)' }
)

$ran = 0
$outcomes = @{}
foreach ($case in $cases) {
    if ($null -ne $Only -and $Only.Count -gt 0 -and $Only -notcontains $case.Name) { continue }
    $ran++
    $label = if ($case.ContainsKey('Outcome')) { $case.Outcome } else { 'intended-red' }
    if (-not $outcomes.ContainsKey($label)) { $outcomes[$label] = 0 }
    $outcomes[$label]++
    Write-Host "[r3a-batch] $($case.Name)"
    Write-Host "           mutation-target: $($case.Target)"
    Write-Host "           guard (green on both sides): $($case.Guard)"
    $argv = @('-NoProfile', '-File', $engine,
        '-Name', $case.Name, '-ExpectAssertion', $case.Expect, '-Configuration', $case.Configuration,
        '-Path', $case.Path, '-Old', $case.Old, '-New', $case.New,
        '-CommandArgs', (Join-Path $commands $case.Run),
        '-ProjectRoot', $root, '-BatchScript', $PSCommandPath,
        # The harness links this repository's own Dev build through its ProjectReference, and that artifact is
        # recorded as an INPUT; the watched carrier stays the frozen root one.
        '-DriverCarrier', 'dist/build/Dev/FerriteLib.UiKit.dll')
    if ($ValidateOnly) { $argv += '-ValidateOnly' }
    if ($case.ContainsKey('Outcome')) { $argv += @('-Outcome', $case.Outcome) }
    if ($case.ContainsKey('OutcomeWhy')) { $argv += @('-OutcomeWhy', $case.OutcomeWhy) }
    if ($case.ContainsKey('Rebuild')) { $argv += @('-RebuildArgs', (Join-Path $commands $case.Rebuild)) }
    # A case may need a second anchor in the same file (the copy property cannot be broken by one edit).
    if ($case.ContainsKey('Old2')) {
        $argv += @('-Path2', $case.Path2, '-Old2', $case.Old2, '-New2', $case.New2)
    }

    & pwsh @argv
    if ($LASTEXITCODE -ne 0) { throw "the mutation '$($case.Name)' failed (exit $LASTEXITCODE)." }
}

if ($ran -eq 0) { throw 'no case matched -Only; a filter that selects nothing is not a pass.' }
$summary = ($outcomes.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ', '
Write-Host "[r3a-batch] $ran case(s) done; outcomes: $summary"
