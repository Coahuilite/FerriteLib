# FerriteLib — dependency-reality checker (D-1)
#
# What this answers: a mod that DECLARES FerriteLib as a prerequisite but never uses the runtime is a
# build-time convention with no enforcement, and the series has already produced one. This turns it into
# a red exit code. FL owns the rule text below; a consumer's own boundary gate may adopt rule (c) and
# should read this file rather than restate it.
#
#   (a) assembly-reference  the checked DLL's AssemblyRef table must name FerriteLib.UiKit
#   (b) page-model contact  at least one MemberRef must target a page-model type, i.e. the mod actually
#                           drives the declarative layer rather than only borrowing the theme helpers
#   (c) chrome allowlist    every SOURCE file that calls the game's immediate-mode surface, or the
#                           library's context-free hit overload (UiNative.Button with one argument),
#                           must appear in the mod's declared ui-chrome allowlist (needs -SourceRoot
#                           + -Allowlist)
#
# Rule (c) is the same measurement as the library's own containment gate (see
# tools/FerriteLib.UiKit.Tests/KernelContainmentTests.cs), applied to a consumer tree: raw backend calls
# are not forbidden, but they are declared, counted and named, or they are a failure.
#
# The two halves of this metric share exactly one thing: the pattern set below. A term ratified on either
# half - this rule, or the library's own containment lane - is added to both, or the halves stop
# describing one boundary. The allowlists are NOT shared: each side rules on its own exemptions, so a file
# sanctioned in one tree means nothing in the other, and syncing the entries would launder one side's
# ruling into the other's boundary. `GenMapUI` is the term this rule carries for world-space labeling:
# the library never draws it (map-layer rendering is a permanent non-goal there, so no allowance is filed
# on that side), while a consumer that keeps an in-world marker declares it in its own ui-chrome
# allowlist - counted, then exempted, never invisible.
#
# Usage:
#   pwsh -NoProfile -File tools/dependency-reality.ps1 -Assembly <path-to-mod.dll> `
#        [-SourceRoot <dir>] [-Allowlist <file>] [-SelfTest]
#
# Exit codes: 0 = every requested rule holds; 1 = a rule failed; 2 = the inputs were unusable.
# The tool never writes anything, and it takes no personal absolute paths — a consumer passes its own
# relative shape from its own harness.

[CmdletBinding()]
param(
    [string]$Assembly,
    [string]$SourceRoot,
    [string]$Allowlist,
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$CarrierName = 'FerriteLib.UiKit'

# The declarative layer. Contact with any of these is what makes "uses the library" more than
# "compiles against the library".
$PageModelTypes = @(
    'UiHost',
    'UiWindowHost',
    'UiSession',
    'IUiBindings',
    'UiBindings',
    'UiWidgetRegistry',
    'UiLayoutManifest',
    'UiLayoutEngine',
    'IUiWidget',
    'UiWidgetContext',
    'UiSessionGuard',
    'UiPopup'
)

# The shared pattern set: two families, one boundary. The first is raw contact with the game's
# immediate-mode surface, as qualified member accesses. The second is the context-free hit overload
# named by its owner - UiNative.Button with a single argument - the one library call that bypasses the
# owned hit stack, so a call site that keeps it is declared or it is a failure. The two-argument form
# (UiNative.Button(rect, ctx)) is the migration the contract asks for and must NOT fire.
$BackendPatterns = @(
    '(?<![A-Za-z0-9_.])(UnityEngine\.|Verse\.)?(GUI|GUIUtility|GenMapUI|Mouse|Text|VerseWidgets|Widgets|Event)\s*\.\s*[A-Za-z_][A-Za-z0-9_]*',
    # Reach, measured 2026-09-12: catches a bare variable, an inline construction, one nested call, a cast,
    # a qualified name and a comma inside the argument's own parentheses; refuses every two-argument form.
    # It does NOT reach an argument that nests parenthesised calls two or more deep - a text pattern cannot
    # recurse - and this scan is line by line, so a call split across lines escapes it too. The library's
    # half runs the same term over whole-file code and carries the same note.
    '(?<![A-Za-z0-9_])UiNative\s*\.\s*Button\s*\((?:[^,()]|\([^()]*\))*\)'
)

function Find-BackendCalls {
    param([string]$Root)

    $found = New-Object System.Collections.Generic.List[string]
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) {
        throw "Source root is not a directory: $Root"
    }

    foreach ($file in Get-ChildItem -LiteralPath $Root -Recurse -Filter *.cs -File) {
        $normalized = $file.FullName -replace '[\\/]', [string][System.IO.Path]::DirectorySeparatorChar
        if ($normalized.Contains("$([System.IO.Path]::DirectorySeparatorChar)obj$([System.IO.Path]::DirectorySeparatorChar)")) { continue }
        if ($normalized.Contains("$([System.IO.Path]::DirectorySeparatorChar)bin$([System.IO.Path]::DirectorySeparatorChar)")) { continue }

        $lines = @(Get-Content -LiteralPath $file.FullName -Encoding UTF8)
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]
            $probe = $line.TrimStart()
            if ($probe.StartsWith('//')) { continue }
            foreach ($pattern in $BackendPatterns) {
                if ($probe -match $pattern) {
                    $found.Add(("{0}:{1}" -f $file.FullName, ($i + 1)))
                    break
                }
            }
        }
    }

    return $found
}

function Test-AssemblyRules {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Assembly not found: $Path"
    }

    Add-Type -AssemblyName System.Reflection.Metadata
    Add-Type -AssemblyName System.Collections.Immutable

    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $pe = New-Object System.Reflection.PortableExecutable.PEReader($stream)
        if (-not $pe.HasMetadata) { throw "Not a managed assembly: $Path" }
        $md = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)

        # (a) AssemblyRef table
        $referencesCarrier = $false
        foreach ($h in $md.AssemblyReferences) {
            if ($md.GetAssemblyReference($h).GetAssemblyName().Name -eq $CarrierName) { $referencesCarrier = $true }
        }

        # (b) MemberRef -> TypeRef -> page-model type. Every FerriteLib type name is library-specific
        # enough that the namespace is checked for attribution and not for discrimination.
        $pageModelHits = New-Object System.Collections.Generic.HashSet[string]([StringComparer]::Ordinal)
        foreach ($h in $md.MemberReferences) {
            $member = $md.GetMemberReference($h)
            $handle = $member.Parent
            $typeName = $null
            $typeNamespace = $null
            if ($handle.Kind -eq [System.Reflection.Metadata.HandleKind]::TypeReference) {
                $tr = $md.GetTypeReference([System.Reflection.Metadata.TypeReferenceHandle]$handle)
                $typeName = $md.GetString($tr.Name)
                $typeNamespace = $md.GetString($tr.Namespace)
            }
            if ($null -eq $typeName) { continue }
            if ($typeNamespace -ne $CarrierName -and -not $typeNamespace.StartsWith("$CarrierName.")) { continue }
            if ($PageModelTypes -contains $typeName) { [void]$pageModelHits.Add($typeName) }
        }

        return [pscustomobject]@{
            ReferencesCarrier = $referencesCarrier
            PageModelHits     = @($pageModelHits)
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Get-AllowedFileNames {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Allowlist not found: $Path"
    }

    $names = New-Object System.Collections.Generic.List[string]
    foreach ($raw in Get-Content -LiteralPath $Path -Encoding UTF8) {
        $entry = $raw.Trim()
        if ($entry.Length -eq 0 -or $entry.StartsWith('#')) { continue }
        $names.Add($entry)
    }
    return $names
}

$failures = New-Object System.Collections.Generic.List[string]
$checked = 0

if ($SelfTest) {
    # A scan that finds nothing because a path moved or a pattern broke is the vacuous-guard failure
    # this repository has been burned by twice, so the tool proves it can fire before it reports clean.
    $sandbox = Join-Path ([System.IO.Path]::GetTempPath()) ('fl-depdip-' + [guid]::NewGuid().ToString('N'))
    try {
        New-Item -ItemType Directory -Path $sandbox | Out-Null
        Set-Content -LiteralPath (Join-Path $sandbox 'Planted.cs') -Encoding UTF8 -Value @(
            'class Planted { void M() { bool b = Verse.Mouse.IsOver(default); } }',
            '// Mouse.IsOver in a comment must not count'
        )
        Set-Content -LiteralPath (Join-Path $sandbox 'PlantedWorldLabel.cs') -Encoding UTF8 -Value @(
            'class PlantedWorldLabel { void M() { GenMapUI.DrawPawnLabel(default, "x", default); } }',
            '// GenMapUI.DrawPawnLabel in a comment must not count'
        )
        Set-Content -LiteralPath (Join-Path $sandbox 'PlantedRawHit.cs') -Encoding UTF8 -Value @(
            'class PlantedRawHit { void M(Rect r) { if (UiNative.Button(r)) { } } }',
            '// UiNative.Button(r) in a comment must not count'
        )
        # The migration the contract asks for: the two-argument form must NOT be a hit, or the pattern
        # would redden the answer it recommends.
        Set-Content -LiteralPath (Join-Path $sandbox 'PlantedMigratedHit.cs') -Encoding UTF8 -Value @(
            'class PlantedMigratedHit { void M(Rect r, UiWidgetContext ctx) { if (UiNative.Button(r, ctx)) { } } }'
        )
        $planted = @(Find-BackendCalls -Root $sandbox)
        if ($planted.Count -ne 3) {
            Write-Error "SELFTEST: expected exactly 3 planted boundary call sites, got $($planted.Count). The source scan is not trustworthy." -ErrorAction Continue
            exit 1
        }
        if (-not $planted[0].EndsWith(':1')) {
            Write-Error "SELFTEST: planted site was not reported on line 1: $($planted[0])" -ErrorAction Continue
            exit 1
        }
        if (-not ($planted | Where-Object { $_.EndsWith('PlantedWorldLabel.cs:1') })) {
            Write-Error "SELFTEST: the shared in-world term (GenMapUI) did not fire on its planted site; the two halves of the metric would disagree about the boundary." -ErrorAction Continue
            exit 1
        }
        if (-not ($planted | Where-Object { $_.EndsWith('PlantedRawHit.cs:1') })) {
            Write-Error "SELFTEST: the context-free hit overload (UiNative.Button with one argument) did not fire on its planted site." -ErrorAction Continue
            exit 1
        }
        if ($planted | Where-Object { $_.EndsWith('PlantedMigratedHit.cs:1') }) {
            Write-Error "SELFTEST: the two-argument form (UiNative.Button(rect, ctx)) was reported; that is the migration the contract asks for, not a breach." -ErrorAction Continue
            exit 1
        }
        Write-Host "selftest ok: the source scan finds every planted shape (raw backend, the shared in-world term, the context-free hit overload), ignores comments and refuses the migrated two-argument form"
    }
    finally {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}
if ([string]::IsNullOrWhiteSpace($Assembly) -and [string]::IsNullOrWhiteSpace($SourceRoot)) {
    if ($SelfTest -and $failures.Count -eq 0) {
        Write-Host '[dependency-reality] selftest only: scan proven live, no target supplied.'
        exit 0
    }

    Write-Host 'nothing to check: pass -Assembly and/or -SourceRoot (or -SelfTest)'
    exit 2
}

if (-not [string]::IsNullOrWhiteSpace($Assembly)) {
    $checked++
    $result = Test-AssemblyRules -Path $Assembly

    if (-not $result.ReferencesCarrier) {
        # (b) is about a reference that is never exercised; with no reference at all it has nothing to
        # say, and reporting both would read as two defects where there is one.
        $failures.Add("(a) $Assembly declares no AssemblyRef to $CarrierName, so it cannot be a consumer at all")
    }
    elseif ($result.PageModelHits.Count -eq 0) {
        $failures.Add("(b) $Assembly references $CarrierName but never touches the page model (" + ($PageModelTypes -join ', ') + '); a prerequisite it cannot exercise is dead weight for the player')
    }
    else {
        Write-Host ("page-model contact: " + ($result.PageModelHits -join ', '))
    }
}

if (-not [string]::IsNullOrWhiteSpace($SourceRoot)) {
    if ([string]::IsNullOrWhiteSpace($Allowlist)) {
        Write-Host 'rule (c) needs both -SourceRoot and -Allowlist'
        exit 2
    }

    $checked++
    $allowed = Get-AllowedFileNames -Path $Allowlist
    $allowedFull = @($allowed | ForEach-Object { [System.IO.Path]::GetFullPath((Join-Path $SourceRoot $_)) })
    $sites = @(Find-BackendCalls -Root $SourceRoot)

    $offending = New-Object System.Collections.Generic.List[string]
    foreach ($site in $sites) {
        $file = $site.Substring(0, $site.LastIndexOf(':'))
        if ($allowedFull -notcontains [System.IO.Path]::GetFullPath($file)) {
            if ($offending -notcontains $file) { $offending.Add($file) }
        }
    }

    foreach ($file in $allowedFull) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
            $failures.Add("(c) allowlist names a file that no longer exists: $file — an exemption nobody claimed is not an exemption, it is a hole left open")
        }
    }

    if ($offending.Count -gt 0) {
        $failures.Add("(c) undeclared boundary call sites (" + $offending.Count + " file(s)): " + ($offending -join '; ') + ' — register them in the ui-chrome allowlist with a written ruling (date, reason, what retires it), or funnel them through the library: raw backend contact goes through the seams, and the context-free hit overload takes the context instead')
    }
    else {
        Write-Host ("boundary call sites: " + $sites.Count + ", all inside the declared allowlist")
    }
}

foreach ($failure in $failures) {
    Write-Host "FAIL $failure"
}

if ($failures.Count -gt 0) { exit 1 }
Write-Host "[dependency-reality] $($checked) rule group(s) checked, all satisfied."
exit 0
