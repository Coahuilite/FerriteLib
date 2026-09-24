<#
    One mutation, one run, one log that can be checked without trusting the author.

    The tracked, hardened form of the ad-hoc harness the T2 batch used, implementing the battery criteria
    written down after that batch (team ledger, M1-M10):

      M1  every mutation has an anchor check; a missing or ambiguous anchor refuses the run (a bare .Replace
          is a silent no-op - the stale-green shape)
      M2  the run must exit NON-ZERO *and* print the assertion named by -ExpectAssertion. 'The log has a
          FAIL' is not evidence: a red for the wrong reason looks identical (measured once in this very
          battery, where a leftover stub folder reddened a different lane)
      M3  one mutation per invocation, attributable by assertion name
      M4  restore by BYTES, then re-read and assert the file is byte-identical to what it was
      M5  after the restore, REBUILD the mutated configuration, assert the build succeeded, and assert the
          rebuilt artifact is no longer the poisoned one. Without this step a run leaves a clean tree with
          a poisoned build output - a state 'git status' cannot see, and it also poisons the NEXT
          mutation's fingerprint (both measured in this repository)
      M6  the log carries its own binding: the command, each mutated file's sha256+mtime (before, mutated,
          restored), the artifact fingerprint (before, poisoned, rebuilt), HEAD, the dirty set, exit code
      K3  the artifact fingerprint is DERIVED from the mutated file. Fingerprinting an artifact the mutation
          cannot change produces a number with no signal, which reads exactly like a signal - so a caller
          that names a different artifact is refused rather than obeyed
      M10 the repository-root carrier is watched, not written: hash and mtime identical before and after

    -SelfTest proves the generator itself cannot lie, with fixtures in a TEMP tree and no build: a missing
    anchor, a red for the wrong reason, an exit-0 non-mutation, a restore that cannot write, a rebuild that
    fails, a rebuild that does not consume the restored source, an artifact that does not belong to the
    mutated project, and a run that touches the carrier must each fail loudly; a correct run must pass.

    Logs go to dist/dev-work/<Name>.log. dist/ is gitignored and may be cleared - this script is tracked, and
    the log's own header carries the command that produced it.
#>
param(
    # Not Mandatory: -SelfTest must be able to bind without a mutation to describe. The main path checks
    # them explicitly below, so a real run cannot start half-specified.
    [string]$Name,
    [string[]]$CommandArgs,
    [string]$ExpectAssertion,
    [string]$Path,
    [string]$Old,
    [string]$New,
    [ValidateSet('Dev', 'Release')][string]$Configuration = 'Dev',
    # 'derive' (default) computes the artifact from the mutated file; 'none' is for a mutation that changes
    # no build output (a licence, a document); anything else must equal the derived path or the run refuses.
    [string]$Artifact = 'derive',
    [string[]]$RebuildArgs,
    [string]$Path2 = '',
    [string]$Old2 = '',
    [string]$New2 = '',
    # The file M10 WATCHES: it must be untouched (hash AND mtime) when the run ends. It is not the payload the
    # command links - that is the DRIVER carrier, and the command scripts choose it (the consumer half passes
    # -p:FerriteLibArtifactPath to this repository's Dev payload, because that is where a Dev run puts the
    # instrument). Two files, two roles, two names: an earlier version called this one 'Carrier' while the
    # command used a different file, which reads as if they were the same.
    [Alias('Carrier')]
    [string]$WatchCarrier = '1.6/Assemblies/FerriteLib.UiKit.dll',
    # How this run's red is to be read. The distinction is not cosmetic: a red that names the intended
    # assertion, a red where the instrument refused an invalid setting, and a red for some other reason are
    # three different facts, and only the first two are evidence for the mutation (team ledger, the outcome
    # labels). 'wrong-reason-red' is kept so an incident log can be labelled honestly instead of deleted -
    # and it is checked in the opposite direction: the named assertion must be ABSENT.
    [ValidateSet('intended-red', 'instrument-refused-invalid-setting', 'wrong-reason-red')]
    [string]$Outcome = 'intended-red',
    # Required whenever -Outcome is not 'intended-red': a red whose cause is not the mutation has to say why,
    # because two logs whose red text is identical are otherwise told apart only by a label.
    [string]$OutcomeWhy = '',
    [string]$LogDirectory = 'dist/dev-work',
    # M11: the log pins the identity of the GENERATOR too, not only of the product. A run whose engine was
    # dirty is not the same evidence as one whose engine is the committed file, and only the hash shows which.
    [string]$BatchScript = '',
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [switch]$ValidateOnly,
    [switch]$SelfTest,
    # Runs only the fixtures whose name contains this text, so one acceptance check can be shown alone.
    [string]$Fixture = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ArtifactForPath {
    <#  The build output a mutation of this file can actually change. Anything else is a false signal. #>
    param([string]$Relative, [string]$Configuration)
    $normalized = $Relative.Replace('\', '/')
    if ($normalized.StartsWith('Source/FerriteLib.UiKit/', [StringComparison]::Ordinal)) {
        return "dist/build/$Configuration/FerriteLib.UiKit.dll"
    }
    # A stub project's output is the published bin/stubs TREE, not one file: the mutation this rule exists
    # for renames a folder inside it. (The per-stub folder/assembly table lives in the guard lane; duplicating
    # it here would give the repository a second copy of the published surface to drift.)
    if ($normalized.StartsWith('tools/FerriteLib.UiKit.Tests/Stubs/', [StringComparison]::Ordinal)) {
        return 'tools/FerriteLib.UiKit.Tests/bin/stubs'
    }
    if ($normalized.StartsWith('tools/FerriteLib.UiKit.Tests/', [StringComparison]::Ordinal)) {
        return "tools/FerriteLib.UiKit.Tests/bin/$Configuration/net472/FerriteLib.UiKit.Tests.exe"
    }
    # The consumer's layout, for the cross-repo half of the battery (mutation-check.ps1 -Half Us). It is here
    # because the rule must be one rule: whichever repository a mutation runs in, the fingerprint has to name
    # an artifact that mutation can actually change.
    if ($normalized.StartsWith('Source/UniversalSqueaker/', [StringComparison]::Ordinal)) {
        return "dist/build/$Configuration/UniversalSqueaker.dll"
    }
    if ($normalized.StartsWith('tools/UniversalSqueakerKernelHostTests/', [StringComparison]::Ordinal)) {
        return "tools/UniversalSqueakerKernelHostTests/bin/$Configuration/net472/UniversalSqueakerKernelHostTests.exe"
    }
    return 'none'
}

function Invoke-Native {
    param([string[]]$Argv, [string]$Log)
    $exe = $Argv[0]
    $rest = @()
    if ($Argv.Count -gt 1) { $rest = $Argv[1..($Argv.Count - 1)] }
    & $exe @rest *>> $Log
    return $LASTEXITCODE
}

function Fingerprint([string]$path, [switch]$WithMtime) {
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        $item = Get-Item -LiteralPath $path
        $shown = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash + ' bytes=' + $item.Length
        # The mtime is appended only where a rewrite of equal bytes matters (the carrier). For an artifact the
        # question is 'are these the same bytes', and a rebuild legitimately moves the mtime.
        if ($WithMtime) { $shown += ' mtime=' + $item.LastWriteTimeUtc.ToString('o') }
        return $shown
    }
    if (Test-Path -LiteralPath $path -PathType Container) {
        # A directory is fingerprinted by its whole file manifest, because the mutation this exists for changes
        # which folders exist rather than the bytes of one file.
        $manifest = Get-ChildItem -LiteralPath $path -Recurse -File | Sort-Object FullName | ForEach-Object {
            $_.FullName.Substring($path.Length) + '=' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $hash = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($manifest -join "`n"))))).Replace('-', '') } finally { $sha.Dispose() }
        return ('DIR sha256=' + $hash + ' files=' + @($manifest).Count)
    }
    return 'ABSENT'
}

function Resolve-MutationTarget {
    param([string]$Root, [string]$Relative, [string]$Anchor, [string]$Label)
    if ($Relative.Length -eq 0) { return $null }
    $full = Join-Path $Root $Relative
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { throw "$Label target does not exist: $Relative" }
    $text = [IO.File]::ReadAllText($full)
    $count = ([regex]::Matches($text, [regex]::Escape($Anchor))).Count
    if ($count -ne 1) { throw "$Label anchor occurs $count time(s) in $Relative; a mutation must have exactly one anchor (M1)." }
    return [pscustomobject]@{ Full = $full; Relative = $Relative; Text = $text; Original = [IO.File]::ReadAllBytes($full) }
}

function Invoke-MutationRun {
    param([string]$Root, [string]$Log, [string]$Name, [string[]]$CommandArgs, [string]$ExpectAssertion,
          [string]$Path, [string]$Old, [string]$New, [string]$Configuration, [string]$Artifact,
          [string[]]$RebuildArgs, [string]$Path2, [string]$Old2, [string]$New2, [string]$WatchCarrier, [string]$Outcome,
          [string]$OutcomeWhy, [string]$BatchScript)

    $derived = Get-ArtifactForPath -Relative $Path -Configuration $Configuration
    if ($Artifact -eq 'derive') { $Artifact = $derived }
    if ($Artifact -ne $derived) {
        throw "K3: this mutation changes '$derived', and the run asked for a fingerprint of '$Artifact'." +
              " A fingerprint of an artifact the mutation cannot change is a number with no signal that reads like one; refusing."
    }
    if ($Outcome -ne 'intended-red' -and $OutcomeWhy.Length -eq 0) {
        throw "-OutcomeWhy is required when -Outcome is '$Outcome': a non-intended red must say why it is one."
    }
    if ($Artifact -ne 'none' -and ($null -eq $RebuildArgs -or $RebuildArgs.Count -eq 0)) {
        throw 'M5: a mutation that changes a build output must name -RebuildArgs so the restored source is rebuilt after the restore.'
    }

    $first = Resolve-MutationTarget -Root $Root -Relative $Path -Anchor $Old -Label 'first'
    $second = Resolve-MutationTarget -Root $Root -Relative $Path2 -Anchor $Old2 -Label 'second'
    if ($ValidateOnly) {
        return "${Name}: anchors validated (M1); artifact would be '$Artifact'; nothing run, nothing written."
    }

    $artifactFull = if ($Artifact -eq 'none') { '' } else { Join-Path $Root $Artifact }
    $carrierFull = Join-Path $Root $WatchCarrier

    $carrierBefore = Fingerprint -path $carrierFull -WithMtime
    $artifactBefore = if ($Artifact -eq 'none') { 'not applicable (this mutation changes no build output)' } else { 'measured after a baseline build, below' }

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('# ' + $Name)
    $lines.Add('# generated (UTC): ' + [DateTime]::UtcNow.ToString('o'))
    $lines.Add('# command: ' + ($CommandArgs -join ' '))
    $rebuildLabel = if ($null -eq $RebuildArgs) { 'not applicable' } else { $RebuildArgs -join ' ' }
    $lines.Add('# rebuild command: ' + $rebuildLabel)
    $lines.Add('# expected assertion (M2): ' + $ExpectAssertion)
    $lines.Add('# outcome: ' + $Outcome)
    if ($OutcomeWhy.Length -gt 0) { $lines.Add('# why this label: ' + $OutcomeWhy) }
    $lines.Add('# artifact (K3, derived from the mutated file): ' + $Artifact + ' :: ' + $artifactBefore)
    $lines.Add('# carrier watched by M10: ' + $WatchCarrier)
    $lines.Add('# carrier before: ' + $carrierBefore)
    $lines.Add('# HEAD: ' + (((& git -C $Root rev-parse HEAD 2>$null) -join '').Trim()))
    $lines.Add('# generator: ' + $PSCommandPath + ' sha256=' + (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash)
    if ($BatchScript.Length -gt 0) {
        $batchLine = '# batch: ' + $BatchScript
        if (Test-Path -LiteralPath $BatchScript -PathType Leaf) { $batchLine += ' sha256=' + (Get-FileHash -LiteralPath $BatchScript -Algorithm SHA256).Hash }
        $lines.Add($batchLine)
    }
    $dirtyLines = @()
    foreach ($statusLine in @(& git -C $Root status --porcelain 2>$null)) {
        $relative = $statusLine.Substring(3).Trim()
        $full = Join-Path $Root $relative
        if (Test-Path -LiteralPath $full -PathType Leaf) {
            $dirtyLines += ($relative + ' sha256=' + (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash)
        } else {
            $dirtyLines += ($relative + ' (not a file on disk)')
        }
    }
    $lines.Add('# dirty (path + sha256, so "which bytes" is answerable, not only "which names"): ' + (($dirtyLines -join ' ; ')))
    foreach ($mutation in @($first, $second)) {
        if ($null -eq $mutation) { continue }
        $lines.Add('# mutated file: ' + $mutation.Relative + ' sha256 before: ' + (Fingerprint $mutation.Full))
    }

    $artifactPoisoned = 'n/a'
    $runOutputPath = $Log + '.run-output'
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $Log) -Force

    # The reference the artifact has to come back to is established with the SAME rebuild command, from the
    # clean source, immediately before the mutation. Taking whatever happened to be on disk instead compares
    # apples to oranges: that artifact may have been built at an older commit (measured: a stale baseline made
    # every case fail M5's restoration clause for a reason that had nothing to do with the mutation).
    $baselineLine = '# artifact before the mutation, after a baseline build: ' + $artifactBefore
    if ($Artifact -ne 'none') {
        $baselineOutputPath = $Log + '.baseline-output'
        Push-Location $Root
        try { $baselineCode = Invoke-Native -Argv $RebuildArgs -Log $baselineOutputPath } finally { Pop-Location }
        if ($baselineCode -ne 0) {
            $baselineOutput = if (Test-Path -LiteralPath $baselineOutputPath) { Get-Content -LiteralPath $baselineOutputPath -Raw } else { '' }
            throw ("the baseline build exited $baselineCode; nothing can be compared against it." + [Environment]::NewLine + $baselineOutput)
        }
        $artifactBefore = Fingerprint $artifactFull
        $baselineLine = '# artifact before the mutation, after a baseline build: ' + $artifactBefore
    }
    try {
        [IO.File]::WriteAllText($first.Full, $first.Text.Replace($Old, $New))
        if ($null -ne $second) { [IO.File]::WriteAllText($second.Full, $second.Text.Replace($Old2, $New2)) }
        foreach ($mutation in @($first, $second)) {
            if ($null -eq $mutation) { continue }
            $lines.Add('# mutated file: ' + $mutation.Relative + ' sha256 WITH the mutation: ' + (Fingerprint $mutation.Full))
        }
        $lines.Add($baselineLine)
        $lines | Set-Content -LiteralPath $Log -Encoding UTF8

        # The command's own output is captured to a separate file and then appended: the log's header names
        # the expected assertion, so searching the WHOLE log would match the header and accept any red at all
        # (the generator's self-test caught exactly that).
        Push-Location $Root
        try { $code = Invoke-Native -Argv $CommandArgs -Log $runOutputPath } finally { Pop-Location }
        if ($Artifact -ne 'none') { $artifactPoisoned = Fingerprint $artifactFull }
        $output = Get-Content -LiteralPath $runOutputPath -Raw
        Add-Content -LiteralPath $Log -Encoding UTF8 -Value $output
        $lines.Add('# exit code: ' + $code)
        $lines.Add('# artifact after the mutated run (poisoned): ' + $artifactPoisoned)

        $named = $output.IndexOf($ExpectAssertion, [StringComparison]::Ordinal) -ge 0
        if ($code -eq 0) { throw "M2: the mutated run exited 0 - the mutation reddened nothing (expected '$ExpectAssertion')." }
        if ($Outcome -eq 'wrong-reason-red') {
            if ($named) { throw "outcome: this run was labelled a wrong-reason red, but it DID print '$ExpectAssertion'; label it 'intended-red' or 'instrument-refused-invalid-setting'." }
        } elseif (-not $named) {
            throw "M2: the run went red but never printed '$ExpectAssertion'; a red for another reason is not evidence - if that is what this run is, label it 'wrong-reason-red'."
        }
    } finally {
        foreach ($mutation in @($first, $second)) {
            if ($null -eq $mutation) { continue }
            [IO.File]::WriteAllBytes($mutation.Full, $mutation.Original)
            if (-not [Linq.Enumerable]::SequenceEqual([byte[]]$mutation.Original, [byte[]][IO.File]::ReadAllBytes($mutation.Full))) {
                throw "M4: $($mutation.Relative) did not come back byte-identical; the tree is left mutated."
            }
            Add-Content -LiteralPath $Log -Encoding UTF8 -Value ('# mutated file: ' + $mutation.Relative + ' sha256 restored: ' + (Fingerprint $mutation.Full))
        }
    }

    if ($Artifact -ne 'none') {
        Push-Location $Root
        try { $rebuildCode = Invoke-Native -Argv $RebuildArgs -Log $runOutputPath } finally { Pop-Location }
        Add-Content -LiteralPath $Log -Encoding UTF8 -Value (Get-Content -LiteralPath $runOutputPath -Raw)
        Add-Content -LiteralPath $Log -Encoding UTF8 -Value ('# rebuild exit code: ' + $rebuildCode)
        if ($rebuildCode -ne 0) { throw "M5: the rebuild after the restore exited $rebuildCode; the artifact may still be poisoned." }
        $artifactRebuilt = Fingerprint $artifactFull
        Add-Content -LiteralPath $Log -Encoding UTF8 -Value ('# artifact after the rebuild: ' + $artifactRebuilt)
        if ($artifactRebuilt -eq $artifactPoisoned) { throw 'M5: the rebuilt artifact is byte-identical to the poisoned one; the build did not consume the restored source.' }
        if ($artifactRebuilt -ne $artifactBefore) {
            throw ("M5: after the restore and the rebuild the artifact is still not what it was before the mutation " +
                   "(before=$artifactBefore rebuilt=$artifactRebuilt). A rename-shaped mutation leaves the OLD output " +
                   "in place, and rebuilding does not remove it - that leftover IS the poison, and the rebuild command " +
                   "must clean it up.")
        }
    } else {
        Add-Content -LiteralPath $Log -Encoding UTF8 -Value '# no build output is affected; no rebuild required (M5 does not apply).'
    }

    $carrierAfter = Fingerprint -path $carrierFull -WithMtime
    Add-Content -LiteralPath $Log -Encoding UTF8 -Value ('# carrier after: ' + $carrierAfter)
    if ($carrierBefore -ne $carrierAfter) { throw 'M10: the run changed the repository-root carrier (hash or mtime).' }

    Add-Content -LiteralPath $Log -Encoding UTF8 -Value ('# verdict: ' + $Outcome + '; restored; rebuilt; carrier unchanged')
    Remove-Item -LiteralPath $runOutputPath -Force -ErrorAction SilentlyContinue
    return "$Name ok"
}

function Invoke-SelfTest {
    $FixtureFilter = $Fixture
    $sandbox = Join-Path ([IO.Path]::GetTempPath()) ('fl-mutation-selftest-' + [guid]::NewGuid().ToString('N'))
    $null = New-Item -ItemType Directory -Path $sandbox -Force
    $script:fixtureFailures = 0
    $script:fixturesRun = 0
    if ($FixtureFilter.Length -gt 0) { Write-Host "[mutation] self-test filtered to fixtures containing '$FixtureFilter'" }

    function Test-Fixture {
        param([string]$FixtureName, [string[]]$Argv, [bool]$ExpectSuccess, [string]$ExpectReason)
        if ($FixtureFilter.Length -gt 0 -and $FixtureName.IndexOf($FixtureFilter, [StringComparison]::OrdinalIgnoreCase) -lt 0) { return }
        $script:fixturesRun++
        # Each fixture starts from the same artifact bytes: a fixture that leaves the output poisoned would
        # otherwise decide what the NEXT fixture measures (the exact statefulness this battery exists to stop).
        [IO.File]::WriteAllText($script:fixtureArtifact, "library-v1`r`n", [Text.UTF8Encoding]::new($false))
        Remove-Item -LiteralPath (Join-Path $sandbox 'flip.marker') -Force -ErrorAction SilentlyContinue
        $out = (& pwsh -NoProfile -File $PSCommandPath @Argv -LogDirectory 'dist/dev-work' -ProjectRoot $sandbox 2>&1 | Out-String)
        $ok = $LASTEXITCODE
        $want = if ($ExpectSuccess) { 'success' } else { 'refusal' }
        if ($ExpectSuccess) {
            if ($ok -ne 0) { Write-Host "SELFTEST FAIL [$FixtureName]: a correct run was refused: $out"; $script:fixtureFailures++; return }
            Write-Host "[fixture] $FixtureName -> generator exit 0 (wanted $want) PASS"
        } else {
            if ($ok -eq 0) { Write-Host "SELFTEST FAIL [$FixtureName]: the generator reported success where it must refuse."; $script:fixtureFailures++; return }
            if ($ExpectReason.Length -gt 0 -and $out.IndexOf($ExpectReason, [StringComparison]::Ordinal) -lt 0) {
                Write-Host "SELFTEST FAIL [$FixtureName]: refused, but not for the named reason ('$ExpectReason'): $out"; $script:fixtureFailures++
                return
            }
            Write-Host "[fixture] $FixtureName -> generator exit $ok (wanted $want) PASS"
        }
    }

    try {
        $source = Join-Path $sandbox 'Source/FerriteLib.UiKit'
        $tests = Join-Path $sandbox 'tools/FerriteLib.UiKit.Tests'
        $libraryArtifactDir = Join-Path $sandbox 'dist/build/Dev'
        $testsArtifactDir = Join-Path $sandbox 'tools/FerriteLib.UiKit.Tests/bin/Dev/net472'
        $carrierDir = Join-Path $sandbox '1.6/Assemblies'
        $null = New-Item -ItemType Directory -Path $source, $tests, $libraryArtifactDir, $testsArtifactDir, $carrierDir -Force
        [IO.File]::WriteAllText((Join-Path $source 'Probe.cs'), "// anchor-one`n", [Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText((Join-Path $tests 'Lane.cs'), "// anchor-two`n", [Text.UTF8Encoding]::new($false))
        # CRLF on purpose: the fixture rebuild command restores this file with `echo`, and the comparison is
        # byte-for-byte, so the two have to write the same bytes.
        $script:fixtureArtifact = Join-Path $libraryArtifactDir 'FerriteLib.UiKit.dll'
        [IO.File]::WriteAllText($script:fixtureArtifact, "library-v1`r`n", [Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText((Join-Path $testsArtifactDir 'FerriteLib.UiKit.Tests.exe'), 'tests-v1', [Text.UTF8Encoding]::new($false))
        [IO.File]::WriteAllText((Join-Path $carrierDir 'FerriteLib.UiKit.dll'), 'carrier', [Text.UTF8Encoding]::new($false))
        $logDir = Join-Path $sandbox 'dist/dev-work'
        $null = New-Item -ItemType Directory -Path $logDir -Force

        # Fixture commands are single-token .cmd files on purpose. A [string[]] parameter cannot cross a
        # `pwsh -File` boundary as an array - the parent joins it into one string - so a fixture that needs
        # a real child-process exit code uses one executable token. That keeps every fixture at the CLI
        # level, where 'must exit non-zero' is literally what is measured.
        function New-FixtureCommand([string]$FileName, [string[]]$Lines) {
            $path = Join-Path $sandbox $FileName
            Set-Content -LiteralPath $path -Encoding ASCII -Value (@('@echo off') + $Lines)
            return , @($path)
        }

        $libraryRed = New-FixtureCommand 'run-red.cmd' @('echo FAIL: the named assertion', 'echo poisoned>"dist\build\Dev\FerriteLib.UiKit.dll"', 'exit /b 1')
        $libraryGreen = New-FixtureCommand 'run-other.cmd' @('echo FAIL: something else', 'exit /b 1')
        $libraryPass = New-FixtureCommand 'run-pass.cmd' @('echo ALL PASS', 'exit /b 0')
        $carrierTouch = New-FixtureCommand 'run-touch.cmd' @('echo FAIL: the named assertion', 'echo poisoned>"dist\build\Dev\FerriteLib.UiKit.dll"', 'echo touched>>"1.6\Assemblies\FerriteLib.UiKit.dll"', 'exit /b 1')
        $rebuildOk = New-FixtureCommand 'rebuild-ok.cmd' @('echo library-v1>"dist\build\Dev\FerriteLib.UiKit.dll"', 'exit /b 0')
        $rebuildNoop = New-FixtureCommand 'rebuild-noop.cmd' @('exit /b 0')
        $rebuildFail = New-FixtureCommand 'rebuild-fail.cmd' @('exit /b 3')
        # Stateful on purpose: the FIRST invocation (the baseline build) produces the right bytes, the second
        # (after the restore) produces different ones. That is the only way one rebuild command can express
        # "it came back to the wrong state", which is the leftover-poison shape M5's second clause exists for.
        $rebuildFlip = New-FixtureCommand 'rebuild-flip.cmd' @(
            'if exist flip.marker (',
            '  echo library-v2>"dist\build\Dev\FerriteLib.UiKit.dll"',
            ') else (',
            '  echo library-v1>"dist\build\Dev\FerriteLib.UiKit.dll"',
            '  echo x>flip.marker',
            ')',
            'exit /b 0')
        # Each array-valued parameter is passed as ONE array element, because splatting passes an element
        # as one argument; these elements hold exactly one token, so the child binds them intact.
        $base = @('-Name', 'fixture', '-ExpectAssertion', 'the named assertion', '-Configuration', 'Dev')
        function New-Argv([string]$MutationPath, [string]$Old, [string]$New, [string[]]$Run, [string[]]$Rebuild, [string]$Artifact) {
            $argv = $base + @('-CommandArgs', $Run, '-Path', $MutationPath, '-Old', $Old, '-New', $New)
            if ($Artifact.Length -gt 0) { $argv += @('-Artifact', $Artifact) }
            if ($null -ne $Rebuild) { $argv += @('-RebuildArgs', $Rebuild) }
            return $argv
        }

        Test-Fixture 'a correct run is accepted' (New-Argv 'Source/FerriteLib.UiKit/Probe.cs' 'anchor-one' 'anchor-one-mutated' $libraryRed $rebuildOk '') $true ''
        Test-Fixture 'a missing anchor is refused (M1)' (New-Argv 'Source/FerriteLib.UiKit/Probe.cs' 'anchor-that-is-not-there' 'x' $libraryRed $rebuildOk '') $false 'M1'
        Test-Fixture 'an exit-0 non-mutation is refused (M2)' (New-Argv 'Source/FerriteLib.UiKit/Probe.cs' 'anchor-one' 'x' $libraryPass $rebuildOk '') $false 'M2'
        Test-Fixture 'a red that never names the assertion is refused (M2)' (New-Argv 'Source/FerriteLib.UiKit/Probe.cs' 'anchor-one' 'x' $libraryGreen $rebuildOk '') $false 'M2'
        Test-Fixture 'a failing rebuild command is refused (M5)' (New-Argv 'Source/FerriteLib.UiKit/Probe.cs' 'anchor-one' 'x' $libraryRed $rebuildFail '') $false 'baseline build exited'
        Test-Fixture 'a rebuild that does not consume the restored source is refused (M5)' (New-Argv 'Source/FerriteLib.UiKit/Probe.cs' 'anchor-one' 'x' $libraryRed $rebuildNoop '') $false 'M5'
        Test-Fixture 'a rebuild that does not return the artifact to the baseline is refused (M5)' (New-Argv 'Source/FerriteLib.UiKit/Probe.cs' 'anchor-one' 'x' $libraryRed $rebuildFlip '') $false 'M5: after the restore and the rebuild'
        Test-Fixture 'an artifact the mutation cannot change is refused (K3)' (New-Argv 'tools/FerriteLib.UiKit.Tests/Lane.cs' 'anchor-two' 'x' $libraryRed $rebuildOk 'dist/build/Dev/FerriteLib.UiKit.dll') $false 'K3'
        Test-Fixture 'a run that changes the carrier is refused (M10)' (New-Argv 'Source/FerriteLib.UiKit/Probe.cs' 'anchor-one' 'x' $carrierTouch $rebuildOk '') $false 'M10'

        $restoreTarget = Join-Path $source 'Probe.cs'
        $readonly = Get-Item -LiteralPath $restoreTarget
        $readonly.IsReadOnly = $true
        Test-Fixture 'a restore that cannot write fails loudly (M4)' (New-Argv 'Source/FerriteLib.UiKit/Probe.cs' 'anchor-one' 'x' $libraryRed $rebuildOk '') $false ''
        $readonly.IsReadOnly = $false
    } finally {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }

    if ($script:fixturesRun -eq 0) { throw "mutation generator self-test: the filter '$FixtureFilter' matched no fixture; a filter that selects nothing is not a pass." }
    if ($script:fixtureFailures -gt 0) { throw "mutation generator self-test: $($script:fixtureFailures) of $($script:fixturesRun) fixture(s) failed." }
    Write-Host ("[mutation] self-test passed: {0} fixture(s) behaved as required - a correct run is accepted, and a missing anchor, an exit-0 non-mutation, a red for another reason, a failing rebuild command, a rebuild that consumes nothing, an artifact the mutation cannot change, a carrier change and an unverifiable restore are each refused." -f $script:fixturesRun)
    Write-Host '[mutation] M5 second clause is covered by a stateful fixture: its rebuild command returns the RIGHT bytes on the baseline build and DIFFERENT ones after the restore, which is the leftover-poison shape the clause exists for.'
}

if ($SelfTest) { Invoke-SelfTest; exit 0 }

# Presence, not emptiness: a mutation that REMOVES an anchor passes -New '' and that is a real run.
foreach ($required in @('Name', 'ExpectAssertion', 'Path', 'Old', 'New')) {
    if (-not $PSBoundParameters.ContainsKey($required)) { throw "-$required is required for a real run (only -SelfTest may omit it)." }
}
if ($null -eq $CommandArgs -or $CommandArgs.Count -eq 0) { throw '-CommandArgs is required for a real run.' }

$root = [IO.Path]::GetFullPath($ProjectRoot)
$log = Join-Path $root (Join-Path $LogDirectory ($Name + '.log'))
# The log directory is created inside the run, after the -ValidateOnly early return: a validation pass must
# write nothing anywhere, including in a sibling checkout.
$report = Invoke-MutationRun -Root $root -Log $log -Name $Name -CommandArgs $CommandArgs -ExpectAssertion $ExpectAssertion `
    -Path $Path -Old $Old -New $New -Configuration $Configuration -Artifact $Artifact -RebuildArgs $RebuildArgs `
    -Path2 $Path2 -Old2 $Old2 -New2 $New2 -WatchCarrier $WatchCarrier -Outcome $Outcome -OutcomeWhy $OutcomeWhy `
    -BatchScript $BatchScript -ValidateOnly:$ValidateOnly
Write-Host ($report + '; log=' + $log)