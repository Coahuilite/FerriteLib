# Privacy audit gate - the one human-run check before any push (TODO.md §5). Three independent
# vectors, scanned separately - a clean working tree does NOT imply clean history, and clean
# messages do not imply clean blobs. Vector 1: tracked files at HEAD. Vector 2: commit
# messages (all refs). Vector 3: every reachable revision (-FullHistory only). Plus:
# credential patterns, PublishedFileId VALUES (wording mentions are policy text and must
# not self-trip), and one-account author/committer identity - measured by email address, never by
# display name. Exit 0 = everything clean.
[CmdletBinding()]
param(
    [switch]$FullHistory
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    $failures = @()

    function Add-Failure([string]$what, [string]$detail) {
        $script:failures += "$what`n$detail"
    }

    # Personal-path forms: drive-letter + one-or-two separators + Users/WorkSpace.
    # {1,2} separators is deliberate: JSON-escaped double-backslash forms were
    # measured in historical blobs and a single-separator pattern cannot see them.
    $pathPattern = '[A-Za-z]:[\\/]{1,2}(Users|WorkSpace)'

    # Credential patterns. The private-key marker is built by concatenation so
    # this script never matches itself in vector 1.
    $credPatterns = [ordered]@{
        'github-classic-pat' = 'ghp_[A-Za-z0-9]{36}'
        'github-fine-grained' = 'github_pat_[A-Za-z0-9_]{20,}'
        'openai-style-key'   = 'sk-[A-Za-z0-9]{20,}'
        'private-key-block'  = ('-----' + 'BEGIN')
    }

    # PublishedFileId is scanned as a VALUE (tag followed by digits), never as
    # the bare word: policy text, .gitignore rules and negative assertions all
    # mention the filename legitimately (wording mentions are common in policy text and .gitignore here).
    $pidValuePattern = '<PublishedFileId>[0-9]+'

    function Scan-Text([string]$label, [string]$text) {
        $all = @($pathPattern, $pidValuePattern) + $credPatterns.Values
        foreach ($pat in $all) {
            $hits = @([regex]::Matches($text, $pat))
            if ($hits.Count -gt 0) {
                Add-Failure "$label : pattern '$pat' x $($hits.Count)" `
                    (($hits | ForEach-Object { $_.Value.Substring(0, [Math]::Min(40, $_.Value.Length)) } | Sort-Object -Unique) -join "`n")
            }
        }
    }

    # ---- Vector 1: working tree (tracked files) ----
    $v1 = @(git grep -l -I -E $pathPattern -- . 2>$null)
    if ($v1.Count -gt 0) { Add-Failure 'vector1 working-tree personal paths' ($v1 -join "`n") }
    $v1pid = @(git grep -l -I -E $pidValuePattern -- . 2>$null)
    if ($v1pid.Count -gt 0) { Add-Failure 'vector1 working-tree PublishedFileId values' ($v1pid -join "`n") }
    foreach ($name in $credPatterns.Keys) {
        $v1c = @(git grep -l -I -E $credPatterns[$name] -- . 2>$null)
        if ($v1c.Count -gt 0) { Add-Failure "vector1 working-tree credential '$name'" ($v1c -join "`n") }
    }
    $pidFile = @(git ls-files -- 'About/PublishedFileId.txt')
    if ($pidFile.Count -gt 0) { Add-Failure 'vector1 PublishedFileId.txt tracked' ($pidFile -join "`n") }
    Write-Host 'vector1 (working tree): scanned'

    # ---- Vector 2: commit messages ----
    $messages = git log --all --format='%s%n%b' | Out-String
    Scan-Text 'vector2 commit messages' $messages
    Write-Host 'vector2 (commit messages): scanned'
    # ---- Identity: one account, measured by email ----
    # Author and committer are collected separately: a combined format string puts two
    # identities on one line and no single-identity regex can match it.
    #
    # The leak vector is the EMAIL, not the display name. A stamped name is whatever the
    # authoring tool chose, and GitHub's web merge button stamps the account's own public
    # display name - verified against `gh api user`, which reports login Coahuilite, id
    # 19252128, name Fe, email null. So a second name carrying the same noreply address is
    # the same human and the same account, not a second identity, and rewriting published
    # history to make one string appear twice would buy no privacy at all. The platform
    # committer (GitHub <noreply@github.com>) is web-merge boilerplate and carries nothing.
    # What still fails: any address that is not the noreply form (a real mailbox is the
    # thing this check exists to catch), and more than one distinct noreply account.
    $ids = @(
        @(git log --all --format='%an <%ae>') +
        @(git log --all --format='%cn <%ce>') |
        Sort-Object -Unique
    )
    $accounts = @()
    $foreign = @()
    foreach ($id in $ids) {
        if ($id -match '^[^<]+ <([0-9]+\+[^@]+)@users\.noreply\.github\.com>$') {
            $accounts += $Matches[1]
        } elseif ($id -eq 'GitHub <noreply@github.com>') {
            # web-merge committer, not an identity
        } else {
            $foreign += $id
        }
    }
    $accounts = @($accounts | Sort-Object -Unique)
    if ($foreign.Count -gt 0) {
        Add-Failure "identity: non-noreply author/committer address(es)" ($foreign -join "`n")
    }
    if ($accounts.Count -ne 1) {
        Add-Failure "identity: expected exactly one GitHub account, found $($accounts.Count)" ($accounts -join "`n")
    }
    $names = @($ids | ForEach-Object { ($_ -split ' <', 2)[0] } | Where-Object { $_ -ne 'GitHub' } | Sort-Object -Unique)
    Write-Host "identity: account $($accounts -join ', ') - $($names.Count) stamped display name(s): $($names -join ', ')"

    # ---- Vector 3: historical blobs (opt-in) ----
    if ($FullHistory) {
        $revs = @(git rev-list --all)
        Write-Host "vector3 (historical blobs): scanning $($revs.Count) revisions"
        $v3 = @(git grep -l -I -E $pathPattern $revs 2>$null | ForEach-Object { ($_ -split ':', 2)[1] } | Sort-Object -Unique)
        if ($v3.Count -gt 0) { Add-Failure 'vector3 historical personal paths' ($v3 -join "`n") }
        $v3pid = @(git grep -l -I -E $pidValuePattern $revs 2>$null | ForEach-Object { ($_ -split ':', 2)[1] } | Sort-Object -Unique)
        if ($v3pid.Count -gt 0) { Add-Failure 'vector3 historical PublishedFileId values' ($v3pid -join "`n") }
        foreach ($name in $credPatterns.Keys) {
            $v3c = @(git grep -l -I -E $credPatterns[$name] $revs 2>$null | ForEach-Object { ($_ -split ':', 2)[1] } | Sort-Object -Unique)
            if ($v3c.Count -gt 0) { Add-Failure "vector3 historical credential '$name'" ($v3c -join "`n") }
        }
    }
    else {
        Write-Host 'vector3 (historical blobs): SKIPPED (pass -FullHistory for first-upload / pre-push checks)'
    }

    if ($failures.Count -gt 0) {
        Write-Host ''
        Write-Host 'PRIVACY AUDIT FAILED:'
        foreach ($f in $failures) { Write-Host "- $f" }
        exit 1
    }
    Write-Host 'PRIVACY AUDIT CLEAN'
    exit 0
}
finally {
    Pop-Location
}
