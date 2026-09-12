<#
  stub-coverage-scan.ps1 - the harness stub is a test double for the game, and a double that does not
  declare a member the code under test calls is a defect in the double, not in the caller.

  This scan is REFERENCE-DRIVEN. It reads the MemberRef table of the assemblies that run on the stubs in
  this repository (the shipped payload, and the harness assembly itself) and asserts that every member
  those assemblies take from a stub-replaced game assembly - Assembly-CSharp, UnityEngine.CoreModule,
  UnityEngine.IMGUIModule, UnityEngine.TextRenderingModule - is declared by the stub metadata, or is
  named in the explicit exemption table with a reason.

  Why a scan and not a lane. A lane only sees what its own path executes. A member the stub lacks still
  compiles green (the Krafs reference assembly advertises it), and then dies at run time INSIDE THE
  HARNESS ONLY: the session guard swaps the element for its recovery band, the frame survives, and a lane
  that asserts a live session stays green while the path under test never ran. That is exactly how
  Verse.GenUI.ContractedBy hid (measured 2026-09-12, MEMORY.md). The guard lane added then catches it
  when a lane drives that page; this scan catches the class - any referenced member, whether or not a
  lane happens to exercise it today.

  What this is NOT. A claim that the stub EQUALS the game's surface. The stub deliberately declares only
  what this repository needs, so a full surface diff would be permanently red and would teach nobody
  anything. The claim is one-directional: what this repository's code actually references must resolve.

  Ratchet. The exemption table is exact in both directions: an unresolved member that is not listed fails
  (a new reference must be declared in the stub or explicitly exempted), a listed member that is no
  longer unresolved fails as stale (an exemption is rent - remove it once the stub catches up), and every
  entry must carry a non-empty reason. Today the table is empty: the measured state is zero unresolved.

  Why PowerShell and not a lane: the harness targets net472, which has no System.Reflection.Metadata, and
  gate 9's precedent in this repository is the same reader under pwsh (PEReader -> GetMetadataReader).
  The C# type provider below is the minimum needed to render a signature as a comparable string; the
  script fails loudly (exit 3) if the provider cannot be built, the way this repository treats a trimmed
  PowerShell or an unreadable input: a silent empty scan is worse than no gate.

  Usage:
    pwsh -NoProfile -File scripts/stub-coverage-scan.ps1                    # scan this checkout
    pwsh -NoProfile -File scripts/stub-coverage-scan.ps1 -Path <dir>        # scan another tree
    pwsh -NoProfile -File scripts/stub-coverage-scan.ps1 -StubsDir <dir>    # against another stub set
    pwsh -NoProfile -File scripts/stub-coverage-scan.ps1 -Assembly <dll> -StubsDir <dir>   # sweep any assembly
    pwsh -NoProfile -File scripts/stub-coverage-scan.ps1 -SelfTest          # + three fixture controls
  Exit codes:
    0 = scanned, and every referenced member resolves or is exempted with a reason.
    2 = scanned, and at least one reference does not: unresolved member(s), stale exemption(s), an
        exemption without a reason, or a self-test fixture that did not behave.
    3 = NOT SCANNED: the inputs were unusable (no payload, no harness build, no stub assembly, no xref at
        all, no exemption table, no metadata reader). Never reported as clean.
#>
param(
    [string]$Path = (Split-Path -Parent $PSScriptRoot),
    [string]$StubsDir,
    [string]$Exemptions,
    # Scan these assemblies instead of this repository's payload + harness build. Used by the self-test
    # fixtures and to sweep a consumer's assembly against this stub surface; read-only either way.
    [string[]]$Assembly,
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Stop-NotScanned([string]$message) {
    Write-Host ('[stub-coverage] NOT SCANNED: ' + $message)
    exit 3
}

$root = [System.IO.Path]::GetFullPath($Path)
if (-not $StubsDir) { $StubsDir = Join-Path $root 'tools/FerriteLib.UiKit.Tests/bin/stubs' }
if (-not $Exemptions) { $Exemptions = Join-Path $root 'scripts/stub-coverage-exemptions.txt' }

# ---------------------------------------------------------------- metadata reader (same idiom as gate 9)
try {
    Add-Type -AssemblyName System.Reflection.Metadata
} catch {
    Stop-NotScanned ('System.Reflection.Metadata is unavailable in this PowerShell (' + $_.Exception.Message + '); the Store build ships a trimmed copy (MEMORY.md, measured 2026-09-07)')
}

$providerSource = @"
using System;
using System.Collections.Immutable;
using System.Reflection.Metadata;

public sealed class FlStubCoverageProvider : ISignatureTypeProvider<string, object>
{
    public static string TypeName(MetadataReader reader, TypeDefinitionHandle h)
    {
        TypeDefinition td = reader.GetTypeDefinition(h);
        string n = reader.GetString(td.Name);
        if (td.IsNested) return TypeName(reader, td.GetDeclaringType()) + "+" + n;
        string ns = reader.GetString(td.Namespace);
        return ns.Length == 0 ? n : ns + "." + n;
    }

    public static string MethodKey(string name, MethodSignature<string> sig)
    {
        string s = name + "(" + string.Join(",", sig.ParameterTypes) + ")";
        if (sig.GenericParameterCount > 0) s += "^" + sig.GenericParameterCount;
        return s;
    }

    public static string FieldKey(string name, string type) { return name + ":" + type; }

    private static string Kind(byte rawTypeKind) { return rawTypeKind == 0x11 ? "v:" : "c:"; }

    public string GetPrimitiveType(PrimitiveTypeCode code) { return code.ToString(); }
    public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte k) { return Kind(k) + TypeName(r, h); }
    public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte k)
    {
        TypeReference tr = r.GetTypeReference(h);
        string n = r.GetString(tr.Name);
        string ns = r.GetString(tr.Namespace);
        return Kind(k) + (ns.Length == 0 ? n : ns + "." + n);
    }
    public string GetTypeFromSpecification(MetadataReader r, object c, TypeSpecificationHandle h, byte k)
    {
        return r.GetTypeSpecification(h).DecodeSignature(this, c);
    }
    public string GetSZArrayType(string e) { return e + "[]"; }
    public string GetArrayType(string e, ArrayShape s) { return e + "[" + new string(',', s.Rank - 1) + "]"; }
    public string GetByReferenceType(string e) { return e + "&"; }
    public string GetPointerType(string e) { return e + "*"; }
    public string GetGenericInstantiation(string g, ImmutableArray<string> a) { return g + "<" + string.Join(",", a) + ">"; }
    public string GetGenericMethodParameter(object c, int i) { return "!!" + i; }
    public string GetGenericTypeParameter(object c, int i) { return "!" + i; }
    public string GetModifiedType(string m, string u, bool req) { return u; }
    public string GetPinnedType(string e) { return e; }
    public string GetFunctionPointerType(MethodSignature<string> s) { return "fnptr"; }
}
"@

try {
    Add-Type -TypeDefinition $providerSource -Language CSharp
} catch {
    Stop-NotScanned ('could not build the signature provider (' + $_.Exception.Message + ')')
}
$provider = New-Object FlStubCoverageProvider

function Open-Reader([string]$file) {
    $stream = [System.IO.File]::OpenRead($file)
    $pe = New-Object System.Reflection.PortableExecutable.PEReader($stream)
    $md = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
    return @{ Stream = $stream; Pe = $pe; Md = $md }
}

# The signature string a member is matched by has to be identical on both sides. Two things it
# deliberately does NOT encode: modopt/modreq wrappers (a call-site signature may omit them) and the
# return type (CLR member identity is name + parameter types; C# cannot declare two members that differ
# only by return type). It DOES encode the class/valuetype kind, so a stub that declares a struct as a
# class is reported rather than matched.

# ------------------------------------------------------------------------ stub surface (the test double)
if (-not (Test-Path -LiteralPath $StubsDir -PathType Container)) {
    Stop-NotScanned ('the stub directory does not exist: ' + $StubsDir + ' (build the harness first: dotnet build tools/FerriteLib.UiKit.Tests)')
}
$stubFiles = @(Get-ChildItem -Path $StubsDir -Recurse -Filter '*.dll' -ErrorAction SilentlyContinue | Sort-Object FullName)
if ($stubFiles.Count -eq 0) {
    Stop-NotScanned ('no stub assembly under ' + $StubsDir + ': an absent stub surface cannot be read as covered')
}

# The assemblies this harness replaces with a stub (FerriteLib.UiKit.Tests.csproj builds exactly these
# four into bin/stubs and copies them beside the test assembly). This list is the RULE, deliberately not
# derived from what the stub happens to declare: deriving it - caught by self-test fixture 1 on its first
# run, 2026-09-12 - made a stub set missing one whole assembly read as nothing to check instead of
# everything missing, which is the silent-green shape this tool exists for.
$ReplacedAssemblies = @(
    'Assembly-CSharp',                   # Stubs/VerseStub
    'UnityEngine.CoreModule',            # Stubs/UnityEngineStub
    'UnityEngine.IMGUIModule',           # Stubs/UnityEngineImGuiStub
    'UnityEngine.TextRenderingModule'    # Stubs/UnityEngineTextRenderingModuleStub
)

$stubAssemblies = New-Object System.Collections.Generic.List[string]
$stubTypes = @{}
foreach ($file in $stubFiles) {
    $reader = Open-Reader $file.FullName
    $assemblyName = $reader.Md.GetString($reader.Md.GetAssemblyDefinition().Name)
    if (-not $stubAssemblies.Contains($assemblyName)) { [void]$stubAssemblies.Add($assemblyName) }
    foreach ($handle in $reader.Md.TypeDefinitions) {
        $typeName = [FlStubCoverageProvider]::TypeName($reader.Md, $handle)
        $definition = $reader.Md.GetTypeDefinition($handle)
        $methods = New-Object 'System.Collections.Generic.HashSet[string]'
        $fields = New-Object 'System.Collections.Generic.HashSet[string]'
        foreach ($methodHandle in $definition.GetMethods()) {
            $method = $reader.Md.GetMethodDefinition($methodHandle)
            [void]$methods.Add([FlStubCoverageProvider]::MethodKey($reader.Md.GetString($method.Name), $method.DecodeSignature($provider, $null)))
        }
        foreach ($fieldHandle in $definition.GetFields()) {
            $field = $reader.Md.GetFieldDefinition($fieldHandle)
            [void]$fields.Add([FlStubCoverageProvider]::FieldKey($reader.Md.GetString($field.Name), $field.DecodeSignature($provider, $null)))
        }
        $key = $assemblyName + '!' + $typeName
        if (-not $stubTypes.ContainsKey($key)) {
            $stubTypes[$key] = @{ Methods = $methods; Fields = $fields }
        } else {
            foreach ($one in $methods) { [void]$stubTypes[$key].Methods.Add($one) }
            foreach ($one in $fields) { [void]$stubTypes[$key].Fields.Add($one) }
        }
    }
    $reader.Pe.Dispose()
    $reader.Stream.Dispose()
}

# ------------------------------------------------------------------------- the targets (what must resolve)
function Find-HarnessAssembly {
    foreach ($configuration in @('Release', 'Dev')) {
        foreach ($extension in @('.exe', '.dll')) {
            $candidate = Join-Path $root ('tools/FerriteLib.UiKit.Tests/bin/' + $configuration + '/net472/FerriteLib.UiKit.Tests' + $extension)
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
        }
    }
    return $null
}

$targets = New-Object System.Collections.Generic.List[string]
$payload = Join-Path $root '1.6/Assemblies/FerriteLib.UiKit.dll'
if ($Assembly) {
    foreach ($one in $Assembly) {
        $full = [System.IO.Path]::GetFullPath($one)
        if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { Stop-NotScanned ('the requested assembly does not exist: ' + $full) }
        [void]$targets.Add($full)
    }
} else {
    if (Test-Path -LiteralPath $payload -PathType Leaf) { [void]$targets.Add($payload) }
    $harness = Find-HarnessAssembly
    if ($harness) { [void]$targets.Add($harness) }
    if ($targets.Count -eq 0) {
        Stop-NotScanned ('nothing to scan: neither ' + $payload + ' nor a harness build under tools/FerriteLib.UiKit.Tests/bin exists')
    }
}

function Get-ReferencedTypeKey($reader, $typeReferenceHandle) {
    # A nested type reference resolves through a chain of TypeReference scopes; the key has to come out
    # identical to the one the stub index uses for the corresponding TypeDefinition (Outer+Inner).
    $names = New-Object System.Collections.Generic.List[string]
    $namespace = ''
    $current = $reader.Md.GetTypeReference($typeReferenceHandle)
    while ($true) {
        $names.Insert(0, $reader.Md.GetString($current.Name))
        $currentNamespace = $reader.Md.GetString($current.Namespace)
        if ($currentNamespace.Length -gt 0) { $namespace = $currentNamespace }
        $scope = $current.ResolutionScope
        if ($scope.Kind -eq [System.Reflection.Metadata.HandleKind]::AssemblyReference) {
            $assembly = $reader.Md.GetAssemblyReference([System.Reflection.Metadata.AssemblyReferenceHandle]$scope)
            return @{ Assembly = $reader.Md.GetString($assembly.Name); Type = (($namespace.Length -gt 0) ? ($namespace + '.') : '') + ($names -join '+') }
        }
        if ($scope.Kind -eq [System.Reflection.Metadata.HandleKind]::TypeReference) {
            $current = $reader.Md.GetTypeReference([System.Reflection.Metadata.TypeReferenceHandle]$scope)
            continue
        }
        return @{ Assembly = $null; Type = $null }
    }
}

$scan = @{}
$counts = New-Object System.Collections.Generic.List[object]
$skippedForeign = 0
$skippedParent = 0
$constructedGameType = New-Object System.Collections.Generic.List[string]
foreach ($target in $targets) {
    $reader = Open-Reader $target
    $references = 0
    $resolved = 0
    foreach ($handle in $reader.Md.MemberReferences) {
        $memberReference = $reader.Md.GetMemberReference($handle)
        if ($memberReference.Parent.Kind -eq [System.Reflection.Metadata.HandleKind]::TypeSpecification) {
            # A call on a constructed type (Foo<Bar>.Method) would hide a game member from this scan, so
            # it is reported as an unscannable input instead of being silently skipped - but only when the
            # constructed type is one the stub actually declares.
            $shape = $reader.Md.GetTypeSpecification([System.Reflection.Metadata.TypeSpecificationHandle]$memberReference.Parent).DecodeSignature($provider, $null)
            $bare = $shape
            if ($bare.StartsWith('c:') -or $bare.StartsWith('v:')) { $bare = $bare.Substring(2) }
            $base = ($bare -split '<')[0]
            foreach ($stubKey in $stubTypes.Keys) {
                if ($stubKey.EndsWith('!' + $base)) { [void]$constructedGameType.Add($shape + ' called from ' + $target); break }
            }
            $skippedParent++
            continue
        }
        if ($memberReference.Parent.Kind -ne [System.Reflection.Metadata.HandleKind]::TypeReference) {
            $skippedParent++
            continue
        }
        $ownerType = Get-ReferencedTypeKey $reader ([System.Reflection.Metadata.TypeReferenceHandle]$memberReference.Parent)
        if (-not $ownerType.Assembly) { $skippedParent++; continue }
        if (-not $ReplacedAssemblies.Contains($ownerType.Assembly)) { $skippedForeign++; continue }
        $references++
        $isMethod = ($memberReference.GetKind() -eq [System.Reflection.Metadata.MemberReferenceKind]::Method)
        if ($isMethod) {
            $memberKey = [FlStubCoverageProvider]::MethodKey($reader.Md.GetString($memberReference.Name), $memberReference.DecodeMethodSignature($provider, $null))
        } else {
            $memberKey = [FlStubCoverageProvider]::FieldKey($reader.Md.GetString($memberReference.Name), $memberReference.DecodeFieldSignature($provider, $null))
        }
        $typeKey = $ownerType.Assembly + '!' + $ownerType.Type
        $declared = $false
        if ($stubTypes.ContainsKey($typeKey)) {
            $entry = $stubTypes[$typeKey]
            if ($isMethod) { $declared = $entry.Methods.Contains($memberKey) } else { $declared = $entry.Fields.Contains($memberKey) }
        }
        if ($declared) { $resolved++; continue }
        $fullKey = $typeKey + '::' + $memberKey
        if (-not $scan.ContainsKey($fullKey)) { $scan[$fullKey] = New-Object System.Collections.Generic.List[string] }
        [void]$scan[$fullKey].Add($target)
    }
    $relative = $target.Replace($root, '').TrimStart([System.IO.Path]::DirectorySeparatorChar, '/')
    [void]$counts.Add([pscustomobject]@{ Path = $relative; References = $references; Resolved = $resolved })
    $reader.Pe.Dispose()
    $reader.Stream.Dispose()
}

$totalReferences = 0
foreach ($one in $counts) { $totalReferences += $one.References }
if ($totalReferences -eq 0) {
    Stop-NotScanned ('read ' + $targets.Count + ' assembly/assemblies but found no reference to a stub-replaced game assembly: an empty scan is not a clean result')
}
if ($constructedGameType.Count -gt 0) {
    Stop-NotScanned ('this scanner does not resolve a call made on a constructed game type; found: ' + ($constructedGameType -join '; '))
}

# ---------------------------------------------------------------------------------- the exemption table
if (-not (Test-Path -LiteralPath $Exemptions -PathType Leaf)) {
    Stop-NotScanned ('the exemption table is missing: ' + $Exemptions + ' (a ruling that is not written down cannot be reviewed)')
}
$exempt = @{}
foreach ($line in [System.IO.File]::ReadAllLines($Exemptions)) {
    $text = $line.Trim()
    if ($text.Length -eq 0 -or $text.StartsWith('#')) { continue }
    $separator = $text.IndexOf('|')
    if ($separator -lt 1) {
        $exempt[$text] = ''
        continue
    }
    $exempt[$text.Substring(0, $separator).Trim()] = $text.Substring($separator + 1).Trim()
}

$unresolved = New-Object System.Collections.Generic.List[string]
$stale = New-Object System.Collections.Generic.List[string]
$unreasoned = New-Object System.Collections.Generic.List[string]
foreach ($key in ($scan.Keys | Sort-Object)) { if (-not $exempt.ContainsKey($key)) { [void]$unresolved.Add($key) } }
foreach ($key in ($exempt.Keys | Sort-Object)) {
    if (-not $scan.ContainsKey($key)) { [void]$stale.Add($key); continue }
    if ($exempt[$key].Length -eq 0) { [void]$unreasoned.Add($key) }
}

# ------------------------------------------------------------------------------------------ the report
Write-Host ('[stub-coverage] stub assemblies: ' + $stubAssemblies.Count + ' (' + ($stubAssemblies -join ', ') + '), indexed types: ' + $stubTypes.Count)
$unstubbed = @($ReplacedAssemblies | Where-Object { -not $stubAssemblies.Contains($_) })
if ($unstubbed.Count -gt 0) {
    Write-Host ('[stub-coverage] and with no stub at all, so every reference to them is unresolved: ' + ($unstubbed -join ', '))
}
foreach ($one in $counts) {
    Write-Host ('[stub-coverage] scanned ' + $one.Path + ': ' + $one.References + ' member reference(s) to the stub surface, ' + $one.Resolved + ' declared by the stub')
}
Write-Host ('[stub-coverage] skipped: ' + $skippedForeign + ' reference(s) outside the stub assemblies, ' + $skippedParent + ' whose declaring type is not a plain type reference')
Write-Host ('[stub-coverage] exemption table ' + $Exemptions + ': ' + $exempt.Count + ' entries, ' + ($exempt.Count - $stale.Count) + ' still needed')

$failed = $false
if ($unresolved.Count -gt 0) {
    $failed = $true
    Write-Host ('[stub-coverage] FAIL: ' + $unresolved.Count + ' referenced game member(s) are not declared by the harness stub:')
    foreach ($key in $unresolved) { Write-Host ('[stub-coverage]   MISSING ' + $key + '   <- ' + (($scan[$key] | Sort-Object) -join ', ')) }
    Write-Host '[stub-coverage] A member that lives only in the compile-time reference assembly (Krafs.Rimworld.Ref) compiles green and then dies at run time in the harness: the session guard replaces the element with its recovery band, the frame survives, and a lane that asserts a live session stays green while the path under test never ran (measured 2026-09-12: Verse.GenUI.ContractedBy). Declare it in tools/FerriteLib.UiKit.Tests/Stubs (VerseStub for Assembly-CSharp, the UnityEngine*Stub projects for the Unity assemblies), or add the key above to the exemption table with a reason.'
}
if ($stale.Count -gt 0) {
    $failed = $true
    Write-Host ('[stub-coverage] FAIL: ' + $stale.Count + ' exemption(s) are stale - the member resolves now, so the entry is rent nobody pays:')
    foreach ($key in $stale) { Write-Host ('[stub-coverage]   STALE ' + $key) }
    Write-Host ('[stub-coverage] Remove them from ' + $Exemptions + '.')
}
if ($unreasoned.Count -gt 0) {
    $failed = $true
    Write-Host ('[stub-coverage] FAIL: ' + $unreasoned.Count + ' exemption(s) carry no reason:')
    foreach ($key in $unreasoned) { Write-Host ('[stub-coverage]   NO-REASON ' + $key) }
    Write-Host '[stub-coverage] Every exemption must say why the member may stay out of the stub; write it after the | separator.'
}
if (-not $failed -and -not $SelfTest) {
    Write-Host ('[stub-coverage] OK: every member the scanned assemblies take from a stub-replaced game assembly is declared by the stub (' + $totalReferences + ' reference(s), ' + $exempt.Count + ' exemption(s)).')
}

# -------------------------------------------------------------------------------------------- self-test
if ($SelfTest) {
    $fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('fl-stub-coverage-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
    try {
        # Fixture 1: the stub set without Assembly-CSharp. The scan must not only fail, it must name a
        # Verse member: this is the sensitivity control (the tree decides the verdict, not the script).
        $noVerse = Join-Path $fixtureRoot 'stubs-without-assembly-csharp'
        New-Item -ItemType Directory -Path $noVerse | Out-Null
        Get-ChildItem -Path $StubsDir -Recurse -Filter '*.dll' | Where-Object { $_.Name -ne 'Assembly-CSharp.dll' } | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $noVerse $_.Name) -Force }
        $output1 = (& pwsh -NoProfile -File $PSCommandPath -Path $root -StubsDir $noVerse -Exemptions $Exemptions 2>&1 | Out-String)
        $code1 = $LASTEXITCODE
        $names = ($output1 -match 'MISSING Assembly-CSharp!')
        if ($code1 -eq 2 -and $names) {
            Write-Host '[stub-coverage] self-test fixture 1 (stub set without Assembly-CSharp): exit 2 and names a Verse member - the scan is sensitive to the stub surface.'
        } else {
            Write-Host ('[stub-coverage] SELF-TEST FAILED: fixture 1 exited ' + $code1 + ' (expected 2) and named-a-Verse-member=' + $names)
            $failed = $true
        }

        # Fixture 2: an empty stub directory. That is not a violated contract, it is an unusable input,
        # and it must read as NOT SCANNED rather than as a clean zero.
        $emptyStubs = Join-Path $fixtureRoot 'stubs-empty'
        New-Item -ItemType Directory -Path $emptyStubs | Out-Null
        $output2 = (& pwsh -NoProfile -File $PSCommandPath -Path $root -StubsDir $emptyStubs -Exemptions $Exemptions 2>&1 | Out-String)
        $code2 = $LASTEXITCODE
        if ($code2 -eq 3) {
            Write-Host '[stub-coverage] self-test fixture 2 (empty stub directory): exit 3, not scanned - an absent surface is never a clean result.'
        } else {
            Write-Host ('[stub-coverage] SELF-TEST FAILED: fixture 2 exited ' + $code2 + ' (expected 3)')
            $failed = $true
        }

        # Fixture 3: a repository tree with no payload and no harness build. Same rule, the other input.
        $emptyRepo = Join-Path $fixtureRoot 'repo-without-targets'
        New-Item -ItemType Directory -Path $emptyRepo | Out-Null
        $output3 = (& pwsh -NoProfile -File $PSCommandPath -Path $emptyRepo -StubsDir $StubsDir -Exemptions $Exemptions 2>&1 | Out-String)
        $code3 = $LASTEXITCODE
        if ($code3 -eq 3) {
            Write-Host '[stub-coverage] self-test fixture 3 (tree without a payload or a harness build): exit 3, not scanned.'
        } else {
            Write-Host ('[stub-coverage] SELF-TEST FAILED: fixture 3 exited ' + $code3 + ' (expected 3)')
            $failed = $true
        }

        if (-not $failed) {
            Write-Host '[stub-coverage] OK: the scan is armed and tree-sensitive (3 fixture controls).'
        }
    } finally {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if ($failed) { exit 2 }
exit 0
