param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$PackDev,
    [switch]$PackZip,
    [switch]$PackNupkg,
    # Skip ALL restore roots; missing/stale assets must fail the corresponding build, not self-heal.
    [switch]$NoRestore,
    [switch]$EvidenceFooter,
    # Full group logs and a summary survive success or failure. Defaults to a fresh ignored run.
    [string]$LogDirectory = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($ProjectRoot)
$env:TEMP = Join-Path $root 'dist/test-temp'
$env:TMP = $env:TEMP
$null = New-Item -ItemType Directory -Force -Path $env:TEMP
if (-not $LogDirectory) { $LogDirectory = 'dist/verification/' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8) }
$logs = [IO.Path]::GetFullPath($LogDirectory, $root)
if (-not $logs.StartsWith([IO.Path]::GetFullPath((Join-Path $root 'dist')) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Verification logs must be under this repository dist/.' }
if (Test-Path -LiteralPath $logs) { throw "Log directory already exists: $logs" }
$null = New-Item -ItemType Directory -Path $logs
$sourceHead = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve source identity.' }
$sourceChanges = @(& git -C $root status --porcelain=v1)
$projectFile = Join-Path $root 'Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj'
$testsProject = Join-Path $root 'tools/FerriteLib.UiKit.Tests/FerriteLib.UiKit.Tests.csproj'
$coreProject = Join-Path $root 'tools/FerriteLib.UiKit.Core.ContractTests/FerriteLib.UiKit.Core.ContractTests.csproj'
$adapterProject = Join-Path $root 'tools/FerriteLib.UiKit.Adapter.ContractTests/FerriteLib.UiKit.Adapter.ContractTests.csproj'
$assembliesDir = Join-Path $root 'dist/build/Release'
$script:gateResults = @()
$script:groups = @()
$script:failed = 0
$runWatch = [Diagnostics.Stopwatch]::StartNew()

function Write-Summary {
    [ordered]@{
        source_head=$sourceHead; source_changes=$sourceChanges; no_restore=[bool]$NoRestore
        projects=@($coreProject,$testsProject,$adapterProject); runtime=[Environment]::Version.ToString()
        elapsed_ms=$runWatch.Elapsed.TotalMilliseconds; failures=$script:failed; groups=$script:groups
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $logs 'summary.json')
}
function Invoke-Check {
    param([string]$Name, [string]$Retry, [scriptblock]$Action, [bool]$Ready=$true, [string]$Configuration='n/a')
    $number = $script:groups.Count + 1
    $log = Join-Path $logs ('{0:d2}-{1}.log' -f $number, ($Name -replace '[^A-Za-z0-9]+','-').Trim('-'))
    $started = [DateTime]::UtcNow
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $status = 'PASS'; $code = 0; $message = ''
    if (-not $Ready) {
        $status = 'BLOCKED'; $code = $null
        'Not executed: required restore/build group failed.' | Set-Content -LiteralPath $log
    } else {
        $global:LASTEXITCODE = 0
        try { & $Action *> $log; $code = $LASTEXITCODE; if ($code -ne 0) { $status='FAIL' } }
        catch { $status='FAIL'; $code=1; $message=$_.Exception.ToString(); Add-Content -LiteralPath $log -Value $message }
    }
    $watch.Stop()
    if ($status -ne 'PASS') { $script:failed++ }
    $script:gateResults += "$Name=$status"
    $script:groups += [ordered]@{ name=$Name; status=$status; exit=$code; configuration=$Configuration; source_head=$sourceHead; started_utc=$started.ToString('o'); elapsed_ms=$watch.Elapsed.TotalMilliseconds; command=$Retry; log=$log }
    Write-Summary
    Write-Host ("[{0}] {1} ({2:N0} ms) -> {3}" -f $status,$Name,$watch.Elapsed.TotalMilliseconds,$log)
    if ($status -eq 'FAIL') { Get-Content -LiteralPath $log -Tail 12 | ForEach-Object { Write-Host "    $_" }; Write-Host "  [hint] retry: $Retry" }
    return $status -eq 'PASS'
}
trap { Write-Summary; throw }

# Preserve the delivery that an already-running consumer may hold.
$deliveredPath = Join-Path $root '1.6/Assemblies/FerriteLib.UiKit.dll'
$deliveredBefore = if (Test-Path -LiteralPath $deliveredPath) { (Get-FileHash -LiteralPath $deliveredPath).Hash + ':' + (Get-Item -LiteralPath $deliveredPath).LastWriteTimeUtc.Ticks } else { 'absent' }
$coreRestored=$true; $testsRestored=$true; $adapterRestored=$true
if (-not $NoRestore) {
    $coreRestored = Invoke-Check 'restore no-platform Core' "dotnet restore $coreProject" { dotnet restore $coreProject --nologo }
    $testsRestored = Invoke-Check 'restore harness including payload authors and stubs' "dotnet restore $testsProject" { dotnet restore $testsProject --nologo }
    $adapterRestored = Invoke-Check 'restore adapter contract entry' "dotnet restore $adapterProject" { dotnet restore $adapterProject --nologo }
}
$coreBuilt = Invoke-Check -Ready $coreRestored -Configuration Release 'build no-platform Core' "dotnet build $coreProject -c Release --no-restore" { dotnet build $coreProject -c Release --no-restore --no-incremental --nologo -m:1 }
$null = Invoke-Check -Ready $coreBuilt -Configuration Release 'execute no-platform Core' 'dotnet tools/FerriteLib.UiKit.Core.ContractTests/bin/Release/net10.0/FerriteLib.UiKit.Core.ContractTests.dll' { & dotnet (Join-Path $root 'tools/FerriteLib.UiKit.Core.ContractTests/bin/Release/net10.0/FerriteLib.UiKit.Core.ContractTests.dll') }
$releaseBuilt = Invoke-Check -Ready $testsRestored -Configuration Release 'build Release payload authors harness and stubs' "dotnet build $testsProject -c Release --no-restore" { dotnet build $testsProject -c Release --no-restore --no-incremental --nologo -m:1 }
$null = Invoke-Check -Ready $releaseBuilt -Configuration Release 'FerriteLib.UiKit harness (kernel + version + neutrality + boundary)' 'tools/FerriteLib.UiKit.Tests/bin/Release/net472/FerriteLib.UiKit.Tests.exe' { & (Join-Path $root 'tools/FerriteLib.UiKit.Tests/bin/Release/net472/FerriteLib.UiKit.Tests.exe') }
# This entry has its own compilation, but the exact Release payload/authors/stub dependencies above
# have already been built. Resolve them without re-entering their Build targets.
$adapterBuilt = Invoke-Check -Ready ($adapterRestored -and $releaseBuilt) -Configuration Release 'build adapter contract entry' "dotnet build $adapterProject -c Release --no-restore -p:BuildProjectReferences=false" { dotnet build $adapterProject -c Release --no-restore -p:BuildProjectReferences=false --no-incremental --nologo -m:1 }
$null = Invoke-Check -Ready $adapterBuilt -Configuration Release 'execute adapter and public author contracts' 'dist/build/Release/adapter-probe/net472/FerriteLib.UiKit.Adapter.ContractTests.exe' { & (Join-Path $root 'dist/build/Release/adapter-probe/net472/FerriteLib.UiKit.Adapter.ContractTests.exe') }
$devBuilt = Invoke-Check -Ready $testsRestored -Configuration Dev 'build Dev payload authors harness and stubs' "dotnet build $testsProject -c Dev --no-restore" { dotnet build $testsProject -c Dev --no-restore --no-incremental --nologo -m:1 }
$null = Invoke-Check -Ready ($releaseBuilt -and $devBuilt) 'configuration-isolated build outputs exist and match evaluated TargetPath' `
    'dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release' `
    {
        foreach ($configuration in @('Dev', 'Release')) {
            $payload = Join-Path $root "dist\build\$configuration\FerriteLib.UiKit.dll"
            if (-not (Test-Path -LiteralPath $payload -PathType Leaf)) { throw "Missing build output: $payload" }
            $target = (& dotnet msbuild $projectFile -getProperty:TargetPath "-p:Configuration=$configuration" -nologo | Select-Object -Last 1)
            if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($target)) { throw 'Could not evaluate TargetPath.' }
            if ([IO.Path]::GetFullPath($target.Trim()) -ne [IO.Path]::GetFullPath($payload)) {
                throw "Unexpected $configuration build output: $target; expected $payload"
            }
        }
        & (Join-Path $PSScriptRoot 'verify-carrier-export.ps1') -ProjectRoot $root
    }

$null = Invoke-Check 'carries no game content (assemblies-only mod)' `
    'dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release' `
    {
        # A prerequisite that also ships Defs silently changes every consumer's game, and the library
        # has zero translation keys today, so a Languages folder here would be unclaimed content.
        # Tested as named paths rather than by filtering an enumeration: an empty enumeration would
        # make a filter-based check pass vacuously, which is the bug class this whole pass is about.
        $contentRoot = Join-Path $root '1.6'
        foreach ($directory in @('Defs', 'Patches', 'Languages', 'Sounds', 'Textures', 'ThingSets')) {
            $probe = Join-Path $contentRoot $directory
            if (Test-Path -LiteralPath $probe) {
                throw "FerriteLib ships assemblies only, found a content directory: $probe"
            }
        }
    }

$null = Invoke-Check 'LICENSE present, full MPL-2.0, and the pinned series licence' `
    'pwsh -NoProfile -File scripts/verify-license.ps1' `
    {
        # The checker is its own script so the mutation proof for this gate ("edit LICENSE and it goes
        # red") can be run against this gate ALONE; through the whole chain, gates 1-5 would be
        # candidates for the red. It is read-only, which is why the hint above is a re-run and not a
        # build: re-running it can never touch the carrier.
        #
        # What it asserts, and the boundary it respects: the MPL-2.0 body, the two truncation probes, the
        # allowed incompatibility notice - and the SHA-256 of the whole series licence pinned as a literal
        # in that script. The cross-repository half (this file byte-identical to the consumer's copy) is
        # the consumer's own gate; this repository answers only for its own copy.
        & pwsh -NoProfile -File (Join-Path $root 'scripts\verify-license.ps1') -ProjectRoot $root
    }

$null = Invoke-Check 'About.xml identity is present and well-formed' `
    'manually' `
    {
        [xml]$about = Get-Content -LiteralPath (Join-Path $root 'About\About.xml') -Raw
        $packageId = $about.SelectSingleNode('/ModMetaData/packageId')
        if (-not $packageId -or $packageId.InnerText.Trim() -ne 'coahuilite.ferritelib') {
            throw "packageId must be coahuilite.ferritelib"
        }
        $modVersion = $about.SelectSingleNode('/ModMetaData/modVersion')
        if (-not $modVersion) { throw "About.xml has no <modVersion>; the release axis has no home" }
        $normalized = $modVersion.InnerText.Trim()
        $split = $normalized.IndexOf('-')
        if ($split -gt 0) { $normalized = $normalized.Substring(0, $split) }
        $parsed = New-Object Version
        if (-not [Version]::TryParse($normalized, [ref]$parsed)) {
            throw "<modVersion> '$($modVersion.InnerText.Trim())' is not a Version; the game's own parser would flag it"
        }
        # The contract axis is asserted against this value inside gate 1, where FerriteLibVersion.Api
        # is actually readable. Here we only prove the release axis is well-formed on its own.
    }

$null = Invoke-Check -Ready ($releaseBuilt -and $devBuilt) 'runtime-resolvability traps: net472 surface + harness stub surface' `
    'pwsh -NoProfile -File scripts/net472-trap-scan.ps1 ; pwsh -NoProfile -File scripts/stub-coverage-scan.ps1 -SelfTest' `
    {
        # The harness compiles against a reference assembly and executes on net472, so a member the
        # reference advertises can still be missing at runtime -- and the call site need not name the
        # enum that gives it away (`text.Split(',')` binds to `Split(char, StringSplitOptions)` through
        # a default argument). The scan reads the argument shape instead of the enum name, which is the
        # only form that finds it: a seven-gate-green tree shipped exactly this failure until a lane ran
        # it (2026-09-11).
        & pwsh -NoProfile -File (Join-Path $root 'scripts\net472-trap-scan.ps1') -SelfTest
        if ($LASTEXITCODE -ne 0) { throw 'net472 trap classifier self-test failed.' }
        & pwsh -NoProfile -File (Join-Path $root 'scripts\net472-trap-scan.ps1') -Path $root
        if ($LASTEXITCODE -ne 0) { throw 'net472 payload scan failed.' }

        # Same family, the other surface. The harness compiles against the game's reference assemblies
        # and executes on the stubs, so a member only the reference declares compiles green and dies at
        # run time inside the harness - quietly, because the session guard swaps the element for its
        # recovery band and the frame survives, which leaves a lane that asserts a live session green
        # while the path under test never ran (measured 2026-09-12: Verse.GenUI.ContractedBy, then
        # Mathf.Clamp(int, int, int)). The scan is reference-driven: every member the payload and the
        # harness take from a stub-replaced game assembly must be declared by the stub or be named in
        # scripts/stub-coverage-exemptions.txt with a reason. -SelfTest adds three fixture controls, so
        # the scan cannot fail open on a stub set missing a whole assembly, an empty stub directory, or a
        # tree with no payload and no harness build.
        & pwsh -NoProfile -File (Join-Path $root 'scripts\stub-coverage-scan.ps1') -Path $root -SelfTest
    }

$null = Invoke-Check 'rule (c) consumer half is armed and tree-sensitive (dependency-reality)' `
    'pwsh -NoProfile -File tools/dependency-reality.ps1 -SelfTest' `
    {
        # Why this gate exists: rule (c) - the half a consumer runs over its own tree - was reachable
        # only by a human remembering to invoke -SelfTest, so its pattern set could rot while every other
        # gate stayed green. It is NOT run over this repository's Source/: bare "Widgets." is the game's
        # class in a consumer tree but this library's own Kernel.Widgets namespace here, so the same
        # pattern flags 12 innocent registration lines (measured 2026-09-12) and a green run would have
        # meant allowlisting a namespace. The library's tree is measured by the containment lane (gate 1)
        # with per-file symbol precision; this gate proves the consumer half still works where it is meant
        # to work.
        $tool = Join-Path $root 'tools\dependency-reality.ps1'

        # (1) The pattern set is armed: every planted shape fires, the migrated two-argument form and the
        # lowercase-local decoy do not.
        # Captured rather than redirected to the gate's own log: Invoke-Check already holds that file
        # open for the whole block, and opening it twice is a sharing violation, not a test failure.
        $selfTest = & pwsh -NoProfile -File $tool -SelfTest *>&1 | Out-String
        if ($LASTEXITCODE -ne 0) {
            throw "dependency-reality -SelfTest failed (exit $LASTEXITCODE): the shared pattern set is not armed. $selfTest"
        }

        # (2) The scan is tree-sensitive, and the allowlist actually decides. The fixture lives in TEMP
        # and is removed in the finally, so the repository keeps no fixture and nothing here is written
        # into a game Mods directory.
        $sandbox = Join-Path ([System.IO.Path]::GetTempPath()) ("fl-rulec-" + [guid]::NewGuid().ToString('N'))
        $sourceRoot = Join-Path $sandbox 'Source'
        $allowlist = Join-Path $sandbox 'ui-chrome-allowlist.txt'
        try {
            New-Item -ItemType Directory -Path $sourceRoot -Force | Out-Null
            Set-Content -LiteralPath (Join-Path $sourceRoot 'Planted.cs') -Encoding UTF8 -Value @(
                'class Planted { void Hit(Rect r) { if (UiNative.Button(r)) { } } }'
            )
            Set-Content -LiteralPath (Join-Path $sourceRoot 'Migrated.cs') -Encoding UTF8 -Value @(
                'class Migrated { void Hit(Rect r, UiWidgetContext ctx) { if (UiNative.Button(r, ctx)) { } } }'
            )
            Set-Content -LiteralPath (Join-Path $sourceRoot 'Locals.cs') -Encoding UTF8 -Value @(
                'class Locals { int M(string text) { return text.Length; } }'
            )
            Set-Content -LiteralPath $allowlist -Encoding UTF8 -Value @('# ui-chrome allowlist (fixture)')

            $verdict = & pwsh -NoProfile -File $tool -SourceRoot $sourceRoot -Allowlist $allowlist *>&1 | Out-String
            if ($LASTEXITCODE -ne 1) {
                throw "rule (c) exited $LASTEXITCODE over a tree holding a bare UiNative.Button(rect); it must be 1. A gate wired to a tool that cannot go red is not wired."
            }
            if ($verdict.IndexOf('Planted.cs', [System.StringComparison]::Ordinal) -lt 0) {
                throw "rule (c) failed the fixture without naming the offending file: $verdict"
            }
            if ($verdict.IndexOf('Migrated.cs', [System.StringComparison]::Ordinal) -ge 0) {
                throw "rule (c) named the migrated two-argument call as a breach; the pattern would punish the contract's own answer. $verdict"
            }
            if ($verdict.IndexOf('Locals.cs', [System.StringComparison]::Ordinal) -ge 0) {
                throw "rule (c) named a lowercase local (text.Length) as backend contact; the scan is case-insensitive. $verdict"
            }

            Add-Content -LiteralPath $allowlist -Encoding UTF8 -Value 'Planted.cs'
            $verdict = & pwsh -NoProfile -File $tool -SourceRoot $sourceRoot -Allowlist $allowlist *>&1 | Out-String
            if ($LASTEXITCODE -ne 0) {
                throw "rule (c) still failed after its one boundary file was allowlisted (exit $LASTEXITCODE): $verdict"
            }
        }
        finally {
            Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

$null = Invoke-Check -Ready $devBuilt -Configuration Dev 'development instrument: the Dev half really ran (numeric geometry + press verdicts)' 'pwsh -File scripts/verify-dev-instrument.ps1 -NoBuild' {
    & pwsh -NoProfile -File (Join-Path $root 'scripts/verify-dev-instrument.ps1') -ProjectRoot $root -NoBuild -LogPath (Join-Path $logs 'dev-harness-output.log')
}
if ($PackDev) {
    $null = Invoke-Check -Ready ($devBuilt -and $script:failed -eq 0) -Configuration Dev 'stage optional Dev package' 'pwsh -File scripts/pack-dev.ps1 -NoBuild' {
        $packArgs = @{ ProjectRoot=$root; NoBuild=$true; NoRestore=$true }
        if ($PackZip) { $packArgs.Zip=$true }; if ($PackNupkg) { $packArgs.Nupkg=$true }
        & (Join-Path $PSScriptRoot 'pack-dev.ps1') @packArgs
    }
}
$null = Invoke-Check 'compatibility carrier unchanged (hash and mtime)' 'inspect held delivery' {
    $deliveredAfter = if (Test-Path -LiteralPath $deliveredPath) { (Get-FileHash -LiteralPath $deliveredPath).Hash + ':' + (Get-Item -LiteralPath $deliveredPath).LastWriteTimeUtc.Ticks } else { 'absent' }
    if ($deliveredBefore -ne $deliveredAfter) { throw 'Verification changed the compatibility carrier (hash or mtime).' }
}
$runWatch.Stop()
Write-Summary
Write-Host "[verify] $($script:groups.Count) groups, $($script:failed) failed/blocked; complete logs: $logs"
if ($EvidenceFooter) {
    $footerArgs = @{ ToStdout=$true; Configuration='Both'; Subject='scripts/verify-local.ps1 (complete Core + Release + Dev + adapter entry)'; Command='pwsh -File scripts/verify-local.ps1 -EvidenceFooter'; ExitCode=[int]($script:failed -gt 0); Gate=$script:gateResults; PinPath=@('scripts/verify-local.ps1','scripts/verify-license.ps1','scripts/verify-dev-instrument.ps1','tools/FerriteLib.UiKit.Tests/StubTextWidth.cs','tools/FerriteLib.UiKit.Tests/KernelTextAuditTests.cs') }
    & (Join-Path $root 'tools/evidence/Write-EvidenceFooter.ps1') @footerArgs
}
if ($script:failed -gt 0) { exit 1 }