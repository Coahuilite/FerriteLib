$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$hook = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'githooks/pre-push')
if ($hook.Contains("`r")) { throw 'pre-push must use LF line endings' }
$previous = & git -C $repoRoot config --get core.hooksPath
if ($previous -and $previous -ne 'scripts/githooks') {
    throw 'An existing custom hooksPath requires reconciliation before installation.'
}
& git -C $repoRoot config core.hooksPath scripts/githooks
if ($LASTEXITCODE -ne 0) { throw 'git config core.hooksPath failed' }
Write-Host 'pre-push privacy gate installed: scripts/githooks'
