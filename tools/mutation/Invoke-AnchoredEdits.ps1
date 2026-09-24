<#
    Two-phase anchored edits - the machinery M1 and M9 require of any script that rewrites tracked files.

    Why it exists. A migration written as a chain of `$text.Replace(old, new)` calls fails in two ways that
    both look like success: a missing anchor is a SILENT no-op (the script prints 'updated' while nothing
    changed - the stale-green shape), and a script that writes file by file can die halfway and leave a
    half-applied tree whose diff reads as deliberate (the stale-red shape).

    So this engine collects operations first, SIMULATES them all in memory against the on-disk text, and only
    writes when every operation validated. A missing anchor, an ambiguous anchor (the text occurs more than
    once) or a range whose end anchor precedes its start refuses the WHOLE batch and writes NOTHING.

    -ValidateOnly stops after the simulation: that is how a historical migration can be re-run today to show
    it refuses instead of half-applying.

    Operations, all paths repo-relative to -Root:
      Set-Text    <path> <anchor> <replacement>              replace one unique anchor
      Set-Range   <path> <startAnchor> <endAnchor> <replacement>  replace [startAnchor, endAnchor)
      New-Text    <path> <content>                           create a whole file; refused if it exists
      Append-Text <path> <text>                              append after the file's last character

    -SelfTest runs the fixture battery below in a TEMP tree and touches no repository file.
#>
param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [System.Collections.ArrayList]$Ops,
    [switch]$ValidateOnly,
    [switch]$SelfTest
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function New-AnchoredEditSet { return ,(New-Object System.Collections.ArrayList) }

function Add-SetText {
    param([System.Collections.ArrayList]$Set, [string]$Path, [string]$Anchor, [string]$Replacement)
    $null = $Set.Add([pscustomobject]@{ Kind = 'SetText'; Path = $Path; A = $Anchor; B = $Replacement })
}

function Add-SetRange {
    param([System.Collections.ArrayList]$Set, [string]$Path, [string]$StartAnchor, [string]$EndAnchor, [string]$Replacement)
    $null = $Set.Add([pscustomobject]@{ Kind = 'SetRange'; Path = $Path; A = $StartAnchor; B = $EndAnchor; C = $Replacement })
}

function Add-NewText {
    param([System.Collections.ArrayList]$Set, [string]$Path, [string]$Content)
    $null = $Set.Add([pscustomobject]@{ Kind = 'NewText'; Path = $Path; A = $Content })
}

function Add-AppendText {
    param([System.Collections.ArrayList]$Set, [string]$Path, [string]$Text)
    $null = $Set.Add([pscustomobject]@{ Kind = 'AppendText'; Path = $Path; A = $Text })
}

function Invoke-AnchoredEdits {
    <#
        Simulate every operation against the on-disk text, then write. Returns a report; throws on the first
        refusal, having written nothing.
    #>
    param([string]$Root, [System.Collections.ArrayList]$Ops, [switch]$ValidateOnly)

    if ($null -eq $Ops -or $Ops.Count -eq 0) { throw 'No operations were collected; refusing to report success for an empty batch.' }

    $original = @{}
    $working = @{}
    foreach ($op in $Ops) {
        $full = Join-Path $Root $op.Path
        if ($working.ContainsKey($full)) { continue }
        if (Test-Path -LiteralPath $full -PathType Leaf) {
            $bytes = [IO.File]::ReadAllBytes($full)
            $original[$full] = $bytes
            $working[$full] = [Text.Encoding]::UTF8.GetString($bytes)
        } else {
            $original[$full] = $null
            $working[$full] = $null
        }
    }

    $index = 0
    foreach ($op in $Ops) {
        $index++
        $full = Join-Path $Root $op.Path
        $text = $working[$full]
        switch ($op.Kind) {
            'SetText' {
                if ($null -eq $text) { throw "op $index ($($op.Path)): Set-Text on a file that does not exist." }
                $count = ([regex]::Matches($text, [regex]::Escape($op.A))).Count
                if ($count -eq 0) { throw "op $index ($($op.Path)): anchor not found - a bare replace here would have been a silent no-op: '$($op.A)'" }
                if ($count -gt 1) { throw "op $index ($($op.Path)): anchor is ambiguous ($count occurrences); a replace would have moved text the author never looked at." }
                $working[$full] = $text.Replace($op.A, $op.B)
            }
            'SetRange' {
                if ($null -eq $text) { throw "op $index ($($op.Path)): Set-Range on a file that does not exist." }
                $startCount = ([regex]::Matches($text, [regex]::Escape($op.A))).Count
                $endCount = ([regex]::Matches($text, [regex]::Escape($op.B))).Count
                if ($startCount -eq 0) { throw "op $index ($($op.Path)): range start anchor not found: '$($op.A)'" }
                if ($startCount -gt 1) { throw "op $index ($($op.Path)): range start anchor is ambiguous ($startCount occurrences)." }
                if ($endCount -eq 0) { throw "op $index ($($op.Path)): range end anchor not found: '$($op.B)'" }
                if ($endCount -gt 1) { throw "op $index ($($op.Path)): range end anchor is ambiguous ($endCount occurrences)." }
                $start = $text.IndexOf($op.A, [StringComparison]::Ordinal)
                $end = $text.IndexOf($op.B, [StringComparison]::Ordinal)
                if ($end -lt $start) { throw "op $index ($($op.Path)): range end anchor precedes its start; the span would be negative." }
                $working[$full] = $text.Substring(0, $start) + $op.C + $text.Substring($end)
            }
            'NewText' {
                if ($null -ne $text) { throw "op $index ($($op.Path)): New-Text would overwrite an existing file; use Set-Text/Set-Range so the change is anchored." }
                $working[$full] = $op.A
            }
            'AppendText' {
                if ($null -eq $text) { throw "op $index ($($op.Path)): Append-Text on a file that does not exist." }
                $working[$full] = $text + $op.A
            }
            default { throw "op ${index}: unknown operation '$($op.Kind)'." }
        }
    }

    if ($ValidateOnly) { return "validated $($Ops.Count) operation(s) across $($working.Count) file(s); nothing written (-ValidateOnly)." }

    foreach ($full in $working.Keys) {
        $directory = Split-Path -Parent $full
        if (-not (Test-Path -LiteralPath $directory)) { $null = New-Item -ItemType Directory -Path $directory -Force }
        [IO.File]::WriteAllText($full, $working[$full], [Text.UTF8Encoding]::new($false))
    }

    return "applied $($Ops.Count) operation(s) across $($working.Count) file(s)."
}

function Invoke-SelfTest {
    $sandbox = Join-Path ([IO.Path]::GetTempPath()) ('fl-anchored-edits-' + [guid]::NewGuid().ToString('N'))
    $null = New-Item -ItemType Directory -Path $sandbox -Force
    $script:fixtureFailures = 0

    function Test-Fixture {
        param([string]$Name, [scriptblock]$Body)
        try { & $Body } catch { Write-Host "SELFTEST FAIL [$Name]: $($_.Exception.Message)"; $script:fixtureFailures++ }
    }

    function Assert-Same {
        param([string]$Name, [byte[]]$Left, [byte[]]$Right)
        if (-not [Linq.Enumerable]::SequenceEqual($Left, $Right)) { throw "$Name changed on disk" }
    }

    try {
        $alpha = Join-Path $sandbox 'alpha.txt'
        [IO.File]::WriteAllText($alpha, "one`ntwo`nthree`n", [Text.UTF8Encoding]::new($false))

        Test-Fixture 'a valid batch applies, including a created file' {
            $set = New-AnchoredEditSet
            Add-SetText $set 'alpha.txt' 'two' 'TWO'
            Add-AppendText $set 'alpha.txt' 'four'
            Add-NewText $set 'beta.txt' 'created'
            $null = Invoke-AnchoredEdits -Root $sandbox -Ops $set
            if ([IO.File]::ReadAllText($alpha) -ne "one`nTWO`nthree`nfour") { throw 'alpha.txt is not what the batch specified' }
            if ([IO.File]::ReadAllText((Join-Path $sandbox 'beta.txt')) -ne 'created') { throw 'beta.txt was not created' }
        }

        Test-Fixture 'a missing anchor refuses the whole batch and writes nothing' {
            $before = [IO.File]::ReadAllBytes($alpha)
            $set = New-AnchoredEditSet
            Add-SetText $set 'alpha.txt' 'TWO' 'again'
            Add-SetText $set 'alpha.txt' 'not-present-anywhere' 'x'
            $refused = $false
            try { $null = Invoke-AnchoredEdits -Root $sandbox -Ops $set } catch { $refused = $true }
            if (-not $refused) { throw 'the batch was accepted' }
            Assert-Same 'alpha.txt' $before ([IO.File]::ReadAllBytes($alpha))
        }

        Test-Fixture 'an ambiguous anchor is refused' {
            $before = [IO.File]::ReadAllBytes($alpha)
            $set = New-AnchoredEditSet
            Add-SetText $set 'alpha.txt' 'o' '0'
            $refused = $false
            try { $null = Invoke-AnchoredEdits -Root $sandbox -Ops $set } catch { $refused = $true }
            if (-not $refused) { throw 'an anchor occurring several times was accepted' }
            Assert-Same 'alpha.txt' $before ([IO.File]::ReadAllBytes($alpha))
        }

        Test-Fixture 'a reversed range is refused' {
            $set = New-AnchoredEditSet
            Add-SetRange $set 'alpha.txt' 'three' 'one' 'x'
            $refused = $false
            try { $null = Invoke-AnchoredEdits -Root $sandbox -Ops $set } catch { $refused = $true }
            if (-not $refused) { throw 'a range whose end precedes its start was accepted' }
        }

        Test-Fixture 'New-Text refuses to overwrite an existing file' {
            $set = New-AnchoredEditSet
            Add-NewText $set 'alpha.txt' 'clobbered'
            $refused = $false
            try { $null = Invoke-AnchoredEdits -Root $sandbox -Ops $set } catch { $refused = $true }
            if (-not $refused) { throw 'an existing file was overwritten' }
        }

        Test-Fixture '-ValidateOnly validates and writes nothing' {
            $before = [IO.File]::ReadAllBytes($alpha)
            $set = New-AnchoredEditSet
            Add-SetText $set 'alpha.txt' 'three' 'THREE'
            $null = Invoke-AnchoredEdits -Root $sandbox -Ops $set -ValidateOnly
            Assert-Same 'alpha.txt' $before ([IO.File]::ReadAllBytes($alpha))
        }

        Test-Fixture 'an empty batch is refused rather than reported as success' {
            $refused = $false
            try { $null = Invoke-AnchoredEdits -Root $sandbox -Ops (New-AnchoredEditSet) } catch { $refused = $true }
            if (-not $refused) { throw 'an empty batch reported success' }
        }
    } finally {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }

    if ($script:fixtureFailures -gt 0) { throw "anchored-edit engine self-test: $($script:fixtureFailures) fixture(s) failed." }
    Write-Host '[anchored-edits] self-test passed: a valid batch applies; a missing anchor, an ambiguous anchor, a reversed range, a whole-file overwrite and an empty batch are all refused; -ValidateOnly writes nothing.'
}

if ($SelfTest) { Invoke-SelfTest; exit 0 }
if ($PSBoundParameters.ContainsKey('Ops')) { Write-Host (Invoke-AnchoredEdits -Root $Root -Ops $Ops -ValidateOnly:$ValidateOnly) }