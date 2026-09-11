param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    # Stage the dev folder after the gates. It is a directory by design (pack-dev.ps1), and placing it
    # in a game Mods directory is never done here — that is the developer's step. -PackZip and
    # -PackNupkg add the artifacts that only other purposes need.
    [switch]$PackDev,
    [switch]$PackZip,
    [switch]$PackNupkg,
    [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# One-input local verification for the FerriteLib prerequisite mod.
# Fail-fast order:
#   1   FerriteLib.UiKit harness, Release (kernel lanes + version contract + neutrality + boundary)
#   2   library Dev build (FER_DEV, TreatWarningsAsErrors)
#   3   library Release build (TreatWarningsAsErrors)
#   4   mod payload present at the path consumers bind to
#   5   payload is content-free: no Defs, Patches, Languages, Sounds or Textures under 1.6
#   6   LICENSE present and full MPL-2.0, with no applied incompatibility notice
#   7   About.xml identity (packageId, modVersion present and parsable as a Version)
#   8   net472 trap scan: no call site uses a member the reference assembly advertises but the
#       net472 runtime lacks (compiles green, fails at runtime -- see the script's own header)
# -PackDev: after all checks pass, stage the dev folder (a directory, not an archive). Placing it
#   in a game Mods directory is the developer's own step - no script here writes outside the repository.
#   -PackZip also writes the dev zip; -PackNupkg also writes the consumer reference package. Both are
#   opt-in because nothing on the dev path needs them.
#
# The neutrality guard, the visual-core/page-model boundary and the two version axes all run INSIDE
# gate 1, from the library's own harness, each with a positive control. There is no second copy of
# those checks here on purpose: the previous arrangement had a consumer repository asserting the
# library's neutrality, and that scan silently passed on an empty tree the moment the trees moved.

$root = [System.IO.Path]::GetFullPath($ProjectRoot)
$projectFile = Join-Path $root 'Source\FerriteLib.UiKit\FerriteLib.UiKit.csproj'
$testsProject = Join-Path $root 'tools\FerriteLib.UiKit.Tests\FerriteLib.UiKit.Tests.csproj'
$assembliesDir = Join-Path $root '1.6\Assemblies'
$tempLog = Join-Path ([System.IO.Path]::GetTempPath()) ("fl-verify-" + [guid]::NewGuid().ToString('N') + '.log')
$buildExtraArgs = @()
if ($NoRestore) { $buildExtraArgs += '--no-restore' }

function Invoke-Check {
    param([string]$Name, [string]$Retry, [scriptblock]$Action)

    Write-Host -NoNewline "[run] $Name ... "
    $previousEap = $ErrorActionPreference
    $ErrorActionPreference = 'Stop'
    $failed = $false
    $failureMessage = $null
    try { & $Action *> $tempLog } catch { $failed = $true; $failureMessage = $_.Exception.Message } finally { $ErrorActionPreference = $previousEap }
    $code = $LASTEXITCODE
    if ($failed -or $code -ne 0) {
        Write-Host 'FAIL'
        # A gate that enforces a contract without saying which one turns every red into a scavenger
        # hunt: the throw message used to be swallowed by this catch and never reached the console
        # (found by the independent verifier, 2026-09-11).
        if (-not [string]::IsNullOrWhiteSpace($failureMessage)) { Write-Host "    $failureMessage" }
        if (Test-Path -LiteralPath $tempLog) {
            Get-Content -LiteralPath $tempLog -Tail 12 | ForEach-Object { Write-Host "    $_" }
        }
        Write-Host "  retry: $Retry"
        Remove-Item -LiteralPath $tempLog -Force -ErrorAction SilentlyContinue
        exit 1
    }
    Write-Host 'OK'
}

# A fresh clone has no obj/ tree, and every lane below runs --no-restore on purpose (a cross-repo gate
# must never silently re-resolve a stale graph). So the bootstrap restore happens exactly once, here,
# measurably, instead of being smuggled into gate 1 where a missing restore surfaced as MSB3644
# (measured 2026-09-11 by the independent verifier on a git-archive extraction).
if (-not $NoRestore) {
    Write-Host -NoNewline '[setup] restore the harness project graph (fresh-tree bootstrap) ... '
    dotnet restore $testsProject *> $tempLog
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'FAIL'
        if (Test-Path -LiteralPath $tempLog) {
            Get-Content -LiteralPath $tempLog -Tail 12 | ForEach-Object { Write-Host "    $_" }
        }
        Write-Host '  retry: dotnet restore tools/FerriteLib.UiKit.Tests/FerriteLib.UiKit.Tests.csproj'
        Remove-Item -LiteralPath $tempLog -Force -ErrorAction SilentlyContinue
        exit 1
    }
    Write-Host 'OK'
}

Invoke-Check 'FerriteLib.UiKit harness (kernel + version + neutrality + boundary)' `
    'dotnet run --no-restore --project tools/FerriteLib.UiKit.Tests -c Release' `
    { dotnet run --no-restore --project $testsProject -c Release }

Invoke-Check 'library Dev build (warnings as errors)' `
    'dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Dev' `
    { dotnet build $projectFile -c Dev @buildExtraArgs }

Invoke-Check 'library Release build (warnings as errors)' `
    'dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release' `
    { dotnet build $projectFile -c Release @buildExtraArgs }

Invoke-Check 'mod payload present at the path consumers bind to' `
    'dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release' `
    {
        # Consumer mods reference 1.6/Assemblies/FerriteLib.UiKit.dll by this exact relative shape.
        # If the output path moves, every consumer's compile-time reference and the runtime binding
        # break together, so the layout is a contract and not an implementation detail.
        $payload = Join-Path $assembliesDir 'FerriteLib.UiKit.dll'
        if (-not (Test-Path -LiteralPath $payload -PathType Leaf)) {
            throw "Missing payload: $payload"
        }

        # Existence alone is not the claim. A stale DLL left in this gitignored folder kept the gate
        # green while consumers bound to bytes this tree never built (measured 2026-09-11 by the
        # independent verifier: moving <OutputPath> elsewhere reddened nothing). So ask MSBuild where
        # it will actually write, and compare that evaluated path with the one consumers bind to.
        $target = (& dotnet msbuild $projectFile -getProperty:TargetPath -p:Configuration=Release -nologo | Select-Object -Last 1)
        if ([string]::IsNullOrWhiteSpace($target)) {
            throw 'Could not read TargetPath from the project.'
        }
        $expected = [System.IO.Path]::GetFullPath($payload)
        $actual = [System.IO.Path]::GetFullPath($target.Trim())
        if ($actual -ne $expected) {
            throw "Build output does not land where consumers bind: TargetPath=$actual expected=$expected"
        }
    }

Invoke-Check 'carries no game content (assemblies-only mod)' `
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

Invoke-Check 'LICENSE present and MPL-2.0' `
    'manually' `
    {
        $licensePath = Join-Path $root 'LICENSE'
        if (-not (Test-Path -LiteralPath $licensePath -PathType Leaf)) {
            throw "FerriteLib has no LICENSE file."
        }
        $text = Get-Content -LiteralPath $licensePath -Raw
        if ($text -notmatch 'Mozilla Public License Version 2\.0') { throw 'LICENSE is not the MPL-2.0 text.' }
        # A truncated paste is the realistic failure: someone copies the header and stops.
        if ($text -notmatch 'Exhibit B') { throw 'LICENSE is missing Exhibit B; the text is truncated.' }
        if ($text -notmatch '10\.4\. Distributing Source Code Form') { throw 'LICENSE is missing section 10.4; the text is truncated.' }
        # Deliberately NOT declared incompatible with secondary licenses: that would bar the assembly
        # from being combined with GPL-family mods, and nothing here needs it.
        #
        # Scoped to the header block on purpose. The full MPL text reproduced below always contains
        # Exhibit B's sample notice, so searching the whole file reports a defect in every correct
        # copy of the licence - the assertion that failed here, not the file.
        $separator = $text.IndexOf('-----')
        $header = if ($separator -gt 0) { $text.Substring(0, $separator) } else { $text }
        if ($header -match 'Incompatible With Secondary Licenses., as defined') {
            throw 'The applied notice declares incompatibility with secondary licenses; that was meant to stay allowed.'
        }
    }

Invoke-Check 'About.xml identity is present and well-formed' `
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

Invoke-Check 'net472 trap scan (compiles green, fails at runtime)' `
    'pwsh -NoProfile -File scripts/net472-trap-scan.ps1' `
    {
        # The harness compiles against a reference assembly and executes on net472, so a member the
        # reference advertises can still be missing at runtime -- and the call site need not name the
        # enum that gives it away (`text.Split(',')` binds to `Split(char, StringSplitOptions)` through
        # a default argument). The scan reads the argument shape instead of the enum name, which is the
        # only form that finds it: a seven-gate-green tree shipped exactly this failure until a lane ran
        # it (2026-09-11).
        & pwsh -NoProfile -File (Join-Path $root 'scripts\net472-trap-scan.ps1') -Path $root
    }

if ($PackDev) {
    $packArgs = @{ ProjectRoot = $root }
    if ($PackZip) { $packArgs.Zip = $true }
    if ($PackNupkg) { $packArgs.Nupkg = $true }
    & (Join-Path $PSScriptRoot 'pack-dev.ps1') @packArgs
    if ($LASTEXITCODE -ne 0) { throw "pack-dev failed." }
}

Remove-Item -LiteralPath $tempLog -Force -ErrorAction SilentlyContinue
Write-Host '[verify] all checks passed.'
