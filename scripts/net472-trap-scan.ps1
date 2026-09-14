<#
  net472-trap-scan.ps1 — static scan for the net472 "compiles green, fails at runtime" family.
  The harness compiles against the Krafs.Rimworld.Ref reference assembly but executes on net472, and the
  reference assembly advertises a few netstandard2.1-only members that the net472 runtime does not have.
  A call site like 'text.Split(',')' never spells the enum name (StringSplitOptions is a default argument
  of the overload the compiler picks), so grepping for the enum name cannot find it - this scan looks at
  the SHAPE of the call instead.

  Measured on this harness runtime (see the shape table it prints; every entry below was observed):
    1) string.Split(<single element>)                       -> MissingMethodException
       ('text.Split(',')' binds to Split(char, StringSplitOptions = None), which net472 lacks)
    2) string.Split(<single element>, StringSplitOptions)   -> MissingMethodException
    3) string.Contains(char)                                -> MissingMethodException
    4) string.Contains(string, StringComparison)            -> MissingMethodException
    5) string.StartsWith(char)                              -> MissingMethodException
    6) string.EndsWith(char)                                -> MethodAccessException (NOT MissingMethodException,
       so a guard that filters on the exception type alone would miss it)
    7) string.Replace(string, string, StringComparison)     -> MissingMethodException
    8) string.Join(char, ...)                               -> MissingMethodException
    9) string.TrimStart() / TrimEnd() with no arguments     -> MissingMethodException
   10) System.IO.Path.GetRelativePath(string, string)       -> MissingMethodException

  Not reported, because these DO exist on net472 (controls): the array forms
  'Split(new[] { ',' })', 'Split(new char[] { ',' }, StringSplitOptions...)', 'Contains(string)',
  'TrimStart(new[] { ' ' })', 'string.Join(string, ...)', 'IndexOf(char)'.

  Conservative by design: a single-argument Split whose argument is not written as an array
  ('Split(separator)' with a variable) is reported, because its declared type cannot be known statically;
  a hit is a prompt to check the call, not a proof. A clean scan is not proof of runtime health either -
  only running the harness proves that half.

  Usage:
    pwsh -NoProfile -File scripts/net472-trap-scan.ps1                 # scan this repo's tree at HEAD
    pwsh -NoProfile -File scripts/net472-trap-scan.ps1 -Sha <ref>      # scan another revision
    pwsh -NoProfile -File scripts/net472-trap-scan.ps1 -Path <dir>     # scan any directory (positive control)
  Exit codes:
    0 = scanned and clean.
    2 = scanned and hit(s) found.
    3 = NOT SCANNED: nothing was scanned, so this is not a clean result. Used when the directory holds no
        .cs file, when repository mode has no git work tree (a source archive or a copied tree - pass
        -Path <dir> instead), when the git work tree is not this script's root (refusing to scan another
        repository's files), when the root is not a FerriteLib checkout, or when a revision yields fewer
        than -MinimumRepoFiles .cs files. A silent empty scan is worse than no gate: a native git failure
        does NOT throw in PowerShell, so an absent .git used to produce "HITS=0" over zero files
        (measured 2026-09-11, the same day gate 8 was wired). Every one of those paths now fails loudly.
#>
param(
    [string]$Sha = 'HEAD',
    [string]$Path,
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    # A real FerriteLib revision holds 60-75 .cs files under Source/ and tools/; anything far below that
    # is an empty or foreign scan, not a clean tree.
    [int]$MinimumRepoFiles = 10
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Statements([string[]]$lines) {
    $statements = New-Object System.Collections.Generic.List[object]
    $depth = 0
    $buffer = ''
    $start = 0
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        $commentAt = $line.IndexOf('//')
        $code = if ($commentAt -ge 0) { $line.Substring(0, $commentAt) } else { $line }
        if ($buffer.Length -eq 0) { $start = $i + 1 }
        $buffer += ' ' + $code
        foreach ($ch in $code.ToCharArray()) {
            if ($ch -eq '(' -or $ch -eq '[') { $depth++ }
            elseif ($ch -eq ')' -or $ch -eq ']') { if ($depth -gt 0) { $depth-- } }
        }
        if ($depth -eq 0) {
            $statements.Add([pscustomobject]@{ Line = $start; Text = $buffer })
            $buffer = ''
        }
    }
    if ($buffer.Trim().Length -gt 0) { $statements.Add([pscustomobject]@{ Line = $start; Text = $buffer }) }
    return $statements
}

# Character and string literals become LIT so a ',' inside ', ' does not read as an argument separator.
function Get-Normalized([string]$arg) {
    $step1 = [regex]::Replace($arg, "'(?:[^'\\]|\\.)*'", 'LIT')
    return [regex]::Replace($step1, '"(?:[^"\\]|\\.)*"', 'LIT')
}

$hits = New-Object System.Collections.Generic.List[string]

function Scan-Text([string]$name, [string[]]$lines) {
    foreach ($statement in (Get-Statements $lines)) {
        $text = $statement.Text

        foreach ($m in [regex]::Matches($text, '\.Split\(([^()]*(?:\([^()]*\))?[^()]*)\)')) {
            $normalized = Get-Normalized $m.Groups[1].Value
            if ($normalized.Trim().Length -eq 0) { continue }
            if ($normalized -match 'new\b|\[') { continue }
            $parts = $normalized -split ','
            if ($parts.Count -eq 1) {
                $hits.Add(("{0}:{1}: Split(single element) binds the optional-options overload net472 lacks :: {2}" -f $name, $statement.Line, $text.Trim()))
            }
            elseif ($normalized -match 'StringSplitOptions') {
                $hits.Add(("{0}:{1}: Split(single element, StringSplitOptions) :: {2}" -f $name, $statement.Line, $text.Trim()))
            }
        }

        if ($text -match '\.Contains\([^)]*StringComparison') {
            $hits.Add(("{0}:{1}: Contains(String, StringComparison) :: {2}" -f $name, $statement.Line, $text.Trim()))
        }

        foreach ($method in @('Contains', 'StartsWith', 'EndsWith')) {
            foreach ($m in [regex]::Matches($text, ('\.' + $method + '\(([^()]*(?:\([^()]*\))?[^()]*)\)'))) {
                if ($m.Groups[1].Value.Trim().StartsWith("'")) {
                    $hits.Add(("{0}:{1}: {2}(char) is a netstandard2.1-only overload (EndsWith throws MethodAccessException) :: {3}" -f $name, $statement.Line, $method, $text.Trim()))
                }
            }
        }

        if ($text -match '\.Replace\([^)]*StringComparison') {
            $hits.Add(("{0}:{1}: Replace(String, String, StringComparison) :: {2}" -f $name, $statement.Line, $text.Trim()))
        }

        foreach ($m in [regex]::Matches($text, 'string\.Join\(([^()]*(?:\([^()]*\))?[^()]*)\)')) {
            if ($m.Groups[1].Value.Trim().StartsWith("'")) {
                $hits.Add(("{0}:{1}: string.Join(char, ...) is a netstandard2.1-only overload :: {2}" -f $name, $statement.Line, $text.Trim()))
            }
        }

        if ($text -match 'Path\.GetRelativePath\(') {
            $hits.Add(("{0}:{1}: Path.GetRelativePath :: {2}" -f $name, $statement.Line, $text.Trim()))
        }

        if ($text -match '\.Trim(Start|End)\(\s*\)') {
            $hits.Add(("{0}:{1}: TrimStart()/TrimEnd() with no arguments :: {2}" -f $name, $statement.Line, $text.Trim()))
        }
    }
}

if ($Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "no such path: $Path" }
    $scanned = 0
    foreach ($file in (Get-ChildItem -LiteralPath $Path -Recurse -File -Filter '*.cs')) {
        if ($file.FullName -match '\\obj\\|\\bin\\') { continue }
        $scanned++
        Scan-Text ($file.FullName.Substring($Path.Length).TrimStart('\', '/')) (Get-Content -LiteralPath $file.FullName)
    }
    if ($scanned -eq 0) {
        Write-Output ("NOT SCANNED: no .cs file under '{0}' (obj/ and bin/ are skipped). This is not a clean tree." -f $Path)
        exit 3
    }
    Write-Output ("scan scope: {0} ({1} .cs files)" -f $Path, $scanned)
}
else {
    # No truncating pipeline around the native call: 'Select-Object -First 1' kills the upstream command
    # and leaves $LASTEXITCODE unset, which StrictMode then reports as an error (measured while writing
    # this guard).
    $toplevelRaw = & git -C $RepoRoot rev-parse --show-toplevel 2>$null
    $gitExit = $LASTEXITCODE
    $toplevel = if ($null -eq $toplevelRaw) { '' } else { (@($toplevelRaw) | Where-Object { $_.Trim().Length -gt 0 } | Select-Object -First 1) }
    if ($gitExit -ne 0 -or [string]::IsNullOrWhiteSpace($toplevel)) {
        Write-Output ("NOT SCANNED: no git work tree at '{0}'; repository mode needs git metadata." -f $RepoRoot)
        Write-Output "             A source archive or a copied tree has no .git - scan it with -Path <dir> instead."
        exit 3
    }

    $toplevelFull = [System.IO.Path]::GetFullPath($toplevel.Trim()).TrimEnd('\', '/')
    $repoFull = [System.IO.Path]::GetFullPath($RepoRoot).TrimEnd('\', '/')
    if (-not [string]::Equals($toplevelFull, $repoFull, [StringComparison]::OrdinalIgnoreCase)) {
        Write-Output ("NOT SCANNED: the git work tree is '{0}', not the script's root '{1}'; refusing to scan" -f $toplevelFull, $repoFull)
        Write-Output "             a different repository's files. Use -Path <dir> to scan a tree directly."
        exit 3
    }

    if (-not (Test-Path -LiteralPath (Join-Path $RepoRoot 'Source\FerriteLib.UiKit')) -or
        -not (Test-Path -LiteralPath (Join-Path $RepoRoot 'About\About.xml'))) {
        Write-Output ("NOT SCANNED: '{0}' does not look like a FerriteLib checkout (Source/FerriteLib.UiKit and About/About.xml are missing)." -f $RepoRoot)
        exit 3
    }

    $full = (& git -C $RepoRoot rev-parse $Sha).Trim()
    $files = @(& git -C $RepoRoot ls-tree -r --name-only $full -- Source tools | Where-Object { $_ -like '*.cs' })
    if ($files.Count -lt $MinimumRepoFiles) {
        Write-Output ("NOT SCANNED: revision {0} yielded only {1} .cs file(s) under Source/ and tools/; that is an empty scan, not a clean tree." -f $full, $files.Count)
        exit 3
    }

    foreach ($rel in $files) {
        Scan-Text $rel (& git -C $RepoRoot show ($full + ':' + $rel))
    }
    Write-Output ("scan scope: {0} ({1} .cs files)" -f $full, $files.Count)
}

foreach ($hit in $hits) { Write-Output $hit }
Write-Output ("HITS=" + $hits.Count)
if ($hits.Count -gt 0) { exit 2 } else { exit 0 }
