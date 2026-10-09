param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Gate 6's checker: the licence this repository ships, asserted from inside this repository alone.
#
# Why a script of its own. The mutation proof for a licence guard is "edit LICENSE and the gate goes
# red", and running that proof through the whole chain would leave gates 1-5 as candidates for the red.
# A gate that can be run by itself is a gate whose evidence is attributable, and this one is read-only:
# it opens nothing for writing, so re-running it is never a delivery step.
#
# Why the pin. The prose this replaces credited gate 6 with a SHA-256 comparison against a consumer's
# copy. It never ran one, so a LICENSE edited or truncated here could not turn any gate in this
# repository red - only a consumer-side gate noticed, and only while its sibling tree was present. The
# 2026-09-10 boundary narrowing settles the choice: this repo answers for itself, so the series licence's
# SHA-256 is pinned here as a literal and asserted locally. A sibling-path check is not an option - that
# is the vacuous-guard shape the neutrality scan was moved in-repo to stop.
#
# What the pin adds over the text probes below: it covers the WHOLE file, so a truncation that still
# carries every searched phrase, a reordering, or a whitespace edit is caught. It also means a legitimate
# licence change must update the literal in the same commit; that is what a drift detector is, and the
# failure message says so. The cross-repository half - this file byte-identical to the consumer's copy -
# stays the consumer's own gate; this repository answers only for its own copy.

$root = [IO.Path]::GetFullPath($ProjectRoot)
$licensePath = Join-Path $root 'LICENSE'

# SHA-256 of the series licence text (MPL-2.0 plus this series' Exhibit A notice), 15 780 bytes.
# Measured 2026-09-24: byte-identical to the wired consumer's copy, which is what makes it the series
# licence rather than one repository's local copy.
$expectedSha256 = '71B96808D967417BCE548B8159ED0409909763CC4371575CCF58455DE044F968'

if (-not (Test-Path -LiteralPath $licensePath -PathType Leaf)) {
    throw 'FerriteLib has no LICENSE file.'
}

$text = Get-Content -LiteralPath $licensePath -Raw
if ($text -notmatch 'Mozilla Public License Version 2\.0') { throw 'LICENSE is not the MPL-2.0 text.' }
# A truncated paste is the realistic failure: someone copies the header and stops.
if ($text -notmatch 'Exhibit B') { throw 'LICENSE is missing Exhibit B; the text is truncated.' }
if ($text -notmatch '10\.4\. Distributing Source Code Form') { throw 'LICENSE is missing section 10.4; the text is truncated.' }

# Deliberately NOT declared incompatible with secondary licenses: that would bar the assembly from being
# combined with GPL-family mods, and nothing here needs it.
#
# Scoped to the header block on purpose. The full MPL text reproduced below always contains Exhibit B's
# sample notice, so searching the whole file reports a defect in every correct copy of the licence - the
# assertion that failed here once, not the file.
$separator = $text.IndexOf('-----')
$header = if ($separator -gt 0) { $text.Substring(0, $separator) } else { $text }
if ($header -match 'Incompatible With Secondary Licenses., as defined') {
    throw 'The applied notice declares incompatibility with secondary licenses; that was meant to stay allowed.'
}

$actual = (Get-FileHash -LiteralPath $licensePath -Algorithm SHA256).Hash
if ($actual -ne $expectedSha256) {
    throw ("LICENSE is not the pinned series licence: sha256=$actual expected=$expectedSha256 " +
        '- the two must match. If the licence text legitimately changed, update $expectedSha256 in scripts/verify-license.ps1 in the SAME commit; do not edit LICENSE to match the literal. If it did not change, this copy has drifted from the licence the series ships.')
}

Write-Host "[license] MPL-2.0 text intact, no incompatibility notice, sha256=$actual matches the pinned series licence."