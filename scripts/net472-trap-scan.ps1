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
  Exit code: 0 = clean, 2 = hit(s).
#>
param(
    [string]$Sha = 'HEAD',
    [string]$Path,
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
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
    foreach ($file in (Get-ChildItem -LiteralPath $Path -Recurse -File -Filter '*.cs')) {
        if ($file.FullName -match '\\obj\\|\\bin\\') { continue }
        Scan-Text ($file.FullName.Substring($Path.Length).TrimStart('\', '/')) (Get-Content -LiteralPath $file.FullName)
    }
    Write-Output ("scan scope: {0}" -f $Path)
}
else {
    $full = (& git -C $RepoRoot rev-parse $Sha).Trim()
    $files = & git -C $RepoRoot ls-tree -r --name-only $full -- Source tools | Where-Object { $_ -like '*.cs' }
    foreach ($rel in $files) {
        Scan-Text $rel (& git -C $RepoRoot show ($full + ':' + $rel))
    }
    Write-Output ("scan scope: {0} ({1} .cs files)" -f $full, ($files | Measure-Object).Count)
}

foreach ($hit in $hits) { Write-Output $hit }
Write-Output ("HITS=" + $hits.Count)
if ($hits.Count -gt 0) { exit 2 } else { exit 0 }
