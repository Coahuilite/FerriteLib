param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    # Stage the installable dev folder after the gates. It is a directory by design (pack-dev.ps1);
    # -PackZip and -PackNupkg add the artifacts that only other purposes need.
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
# -PackDev: after all checks pass, stage the installable dev folder (a directory, not an archive).
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
    try { & $Action *> $tempLog } catch { $failed = $true } finally { $ErrorActionPreference = $previousEap }
    $code = $LASTEXITCODE
    if ($failed -or $code -ne 0) {
        Write-Host 'FAIL'
        if (Test-Path -LiteralPath $tempLog) {
            Get-Content -LiteralPath $tempLog -Tail 12 | ForEach-Object { Write-Host "    $_" }
        }
        Write-Host "  retry: $Retry"
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
        if (-not (Test-Path -LiteralPath (Join-Path $assembliesDir 'FerriteLib.UiKit.dll') -PathType Leaf)) {
            throw "Missing payload: $assembliesDir\FerriteLib.UiKit.dll"
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

if ($PackDev) {
    $packArgs = @{ ProjectRoot = $root }
    if ($PackZip) { $packArgs.Zip = $true }
    if ($PackNupkg) { $packArgs.Nupkg = $true }
    & (Join-Path $PSScriptRoot 'pack-dev.ps1') @packArgs
    if ($LASTEXITCODE -ne 0) { throw "pack-dev failed." }
}

Remove-Item -LiteralPath $tempLog -Force -ErrorAction SilentlyContinue
Write-Host '[verify] all checks passed.'
