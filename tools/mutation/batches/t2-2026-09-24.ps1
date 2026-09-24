<#
    The T2 batch (2026-09-24) as a re-runnable script.

    Every mutation this batch's evidence rests on, with the assertion each one must redden. It exists because
    a generator change invalidates every log the generator produced: the first run of this battery left
    polluted fingerprints (each log's 'before' was the PREVIOUS mutation's leftover artifact, K2) and one log
    that was red for the wrong reason (a leftover stub folder, not the mutation, K5/incident). Re-run this
    file after any engine change, and read the logs rather than the exit code.

    -ValidateOnly resolves every anchor and writes nothing: use it first.
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

$coveredRecording = '            UiDevGeometryProbe.NoteInput(ctx, element, rect, "covered", eventBefore);'
$coveredYield = @"
            UiDevGeometryProbe.NoteInput(ctx, element, rect, "covered", eventBefore);
#endif
            return false;
        }
"@
$coveredYieldKept = @"
            UiDevGeometryProbe.NoteInput(ctx, element, rect, "covered", eventBefore);
#endif
        }
"@
$namedCheck = @"
            Check(!HasInputVerdict(dump, "root/under", "hit"),
                "and it records no hit sample in the same pass: " + ShowInputs(dump, "root/under"));
"@
$namedCheckEchoed = @"
            // mutation: the name survives in the output, but no longer as a passing assertion.
            Console.WriteLine("    | note: and it records no hit sample in the same pass: " + ShowInputs(dump, "root/under"));
"@
$rowConsumption = @"
            if (UiNative.DropdownOptionRow(rowRect, elementId, ctx.Session))
            {
                UiNative.ConsumePointerEvent();
                ctx.Session.ClosePopup();
                onSelected(options[i].Value);
            }
"@
$rowConsumptionKept = @"
            if (UiNative.DropdownOptionRow(rowRect, elementId, ctx.Session))
            {
                UiNative.ConsumePointerEvent();
                onSelected(options[i].Value);
            }
"@
$themeDrawClass = "public static class UiThemeDraw`n{"
$themeDrawPlanted = "public static class UiThemeDraw`n{`n    // mutation: a real reference from the visual core into the page model.`n    private static Type BoundaryProbe() => typeof(UiPopup);"

$cases = @(
    @{ Name = 't2-b1-covered-recording-removed'; Expect = "the dump carries no line with 'verdict=covered'"; Path = 'Source/FerriteLib.UiKit/Kernel/UiNative.cs'; Old = $coveredRecording; New = '            // mutation: the covered recording is removed.'; Run = 'run-fl-harness-dev.cmd'; Rebuild = 'rebuild-fl-dev.cmd'; Configuration = 'Dev' },
    @{ Name = 't2-b2-covered-yield-removed'; Expect = 'and it records no hit sample in the same pass:'; Path = 'Source/FerriteLib.UiKit/Kernel/UiNative.cs'; Old = $coveredYield; New = $coveredYieldKept; Run = 'run-fl-harness-dev.cmd'; Rebuild = 'rebuild-fl-dev.cmd'; Configuration = 'Dev' },
    @{ Name = 't2-b3-rect-blind-override'; Outcome = 'instrument-refused-invalid-setting'; OutcomeWhy = 'the red text is the same as t2-b1''s, but the cause is the instrument refusing an invalid setting (a rect-blind override closes the popup before the covered element is drawn), not the product defect t2-b1 removes'; Expect = "the dump carries no line with 'verdict=covered'"; Path = 'tools/FerriteLib.UiKit.Tests/KernelDevGeometryTests.cs'; Old = '            UiNative.ButtonOverride = rect => Over(rect, point);'; New = '            UiNative.ButtonOverride = rect => true; // mutation: rect-blind'; Run = 'run-fl-harness-dev.cmd'; Rebuild = 'rebuild-fl-tests-dev.cmd'; Configuration = 'Dev' },
    @{ Name = 't2-b4-uipopup-planted'; Expect = "reaches into the page model via 'UiPopup'"; Path = 'Source/FerriteLib.UiKit/Kernel/UiThemeDraw.cs'; Old = $themeDrawClass; New = $themeDrawPlanted; Run = 'run-fl-harness-release.cmd'; Rebuild = 'rebuild-fl-release.cmd'; Configuration = 'Release' },
    @{ Name = 't2-b5-stub-rename-warm'; Expect = 'a rename leaves both names behind'; Path = 'tools/FerriteLib.UiKit.Tests/Stubs/VerseStub/VerseStub.csproj'; Old = '<OutputPath>..\..\bin\stubs\verse\</OutputPath>'; New = '<OutputPath>..\..\bin\stubs\verse-stub\</OutputPath>'; Run = 'run-fl-harness-release.cmd'; Rebuild = 'rebuild-fl-stubs-release.cmd'; Configuration = 'Release' },
    @{ Name = 't2-b6-stub-rename-both-files'; Expect = 'a rename leaves both names behind'; Path = 'tools/FerriteLib.UiKit.Tests/Stubs/VerseStub/VerseStub.csproj'; Old = '<OutputPath>..\..\bin\stubs\verse\</OutputPath>'; New = '<OutputPath>..\..\bin\stubs\verse-stub\</OutputPath>'; Path2 = 'tools/FerriteLib.UiKit.Tests/FerriteLib.UiKit.Tests.csproj'; Old2 = '<UiKitRuntimeStub Include="$(MSBuildProjectDirectory)\bin\stubs\verse\Assembly-CSharp.dll" />'; New2 = '<UiKitRuntimeStub Include="$(MSBuildProjectDirectory)\bin\stubs\verse-stub\Assembly-CSharp.dll" />'; Run = 'run-fl-harness-release.cmd'; Rebuild = 'rebuild-fl-stubs-release.cmd'; Configuration = 'Release' },
    @{ Name = 't2-b7-assertion-removed'; Expect = "no 'and it records no hit sample in the same pass:'"; Path = 'tools/FerriteLib.UiKit.Tests/KernelDevGeometryTests.cs'; Old = $namedCheck; New = '            // mutation: the named assertion was removed from the run output.'; Run = 'run-gate10.cmd'; Rebuild = 'rebuild-fl-tests-dev.cmd'; Configuration = 'Dev' },
    @{ Name = 't2-b8-name-not-passing'; Expect = 'showed 13 of the 14 named'; Path = 'tools/FerriteLib.UiKit.Tests/KernelDevGeometryTests.cs'; Old = $namedCheck; New = $namedCheckEchoed; Run = 'run-gate10.cmd'; Rebuild = 'rebuild-fl-tests-dev.cmd'; Configuration = 'Dev' },
    @{ Name = 't2-b9-license-byte'; Expect = 'LICENSE is not the pinned series licence'; Path = 'LICENSE'; Old = 'rights reserved.'; New = 'rights reserved. '; Run = 'run-license-check.cmd'; Configuration = 'Dev' },
    @{ Name = 't2-b10-license-truncated'; Expect = 'LICENSE is missing section 10.4'; Path = 'LICENSE'; Old = '10.4. Distributing Source Code Form'; New = '10.4. Distributing Source Code'; Run = 'run-license-check.cmd'; Configuration = 'Dev' },
    @{ Name = 't2-b11-row-consumption-removed'; Expect = 'the option row consumed the click and closed the popup, not the trigger'; Path = 'Source/FerriteLib.UiKit/Kernel/UiPopup.cs'; Old = $rowConsumption; New = $rowConsumptionKept; Run = 'run-fl-harness-dev.cmd'; Rebuild = 'rebuild-fl-dev.cmd'; Configuration = 'Dev' }
)

$ran = 0
$outcomes = @{}
foreach ($case in $cases) {
    if ($null -ne $Only -and $Only.Count -gt 0 -and $Only -notcontains $case.Name) { continue }
    $ran++
    $label = if ($case.ContainsKey('Outcome')) { $case.Outcome } else { 'intended-red' }
    if (-not $outcomes.ContainsKey($label)) { $outcomes[$label] = 0 }
    $outcomes[$label]++
    $argv = @('-NoProfile', '-File', $engine,
        '-Name', $case.Name, '-ExpectAssertion', $case.Expect, '-Configuration', $case.Configuration,
        '-Path', $case.Path, '-Old', $case.Old, '-New', $case.New,
        '-CommandArgs', (Join-Path $commands $case.Run),
        '-ProjectRoot', $root, '-BatchScript', $PSCommandPath,
        # The payload the harness links here is this repository's own Dev build (ProjectReference), and it is
        # recorded as an INPUT; the watched carrier stays the frozen root one, deliberately single.
        '-DriverCarrier', 'dist/build/Dev/FerriteLib.UiKit.dll')
    if ($ValidateOnly) { $argv += '-ValidateOnly' }
    if ($case.ContainsKey('Outcome')) { $argv += @('-Outcome', $case.Outcome) }
    if ($case.ContainsKey('OutcomeWhy')) { $argv += @('-OutcomeWhy', $case.OutcomeWhy) }
    if ($case.ContainsKey('Rebuild')) { $argv += @('-RebuildArgs', (Join-Path $commands $case.Rebuild)) }
    if ($case.ContainsKey('Path2')) { $argv += @('-Path2', $case.Path2, '-Old2', $case.Old2, '-New2', $case.New2) }
    Write-Host "[t2-batch] $($case.Name)"
    & pwsh @argv
    if ($LASTEXITCODE -ne 0) { throw "the mutation '$($case.Name)' failed (exit $LASTEXITCODE)." }
}

if ($ran -eq 0) { throw "no case matched -Only; a filter that selects nothing is not a pass." }
# The batch level reports the outcome distribution, so a reader does not have to open eleven logs to learn
# whether every red was the mutation's or one of them was the instrument refusing an invalid setting.
$summary = ($outcomes.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ', '
Write-Host "[t2-batch] $ran case(s) done; outcomes: $summary"