# Push privacy gate

The scanner is adapted from the series' Mwah gate as of 2026-10-08. It scans
tracked text and names, commit/tag messages and author/committer identities,
reachable historical text, structured binary metadata and runtime-derived
personal tokens. Repository-specific history exemptions start empty.

**Accepted automation placeholder (maintainer decision 2026-10-09).** The
identity vector additionally accepts the exact literal
`worker <worker@localhost>` by case-sensitive full-string equality only. It is a
placeholder for automation commits, **not a GitHub noreply address**, and it
opens no localhost-domain, email-domain, display-name or pattern exemption: a
near-miss spelling (another name, another domain, a case change) is still a
finding, and every other vector — credentials, paths, messages, binary
metadata, runtime anonymity — is unchanged.

Run once in each clone: pwsh -NoProfile -File scripts/install-hooks.ps1.
The pre-push hook automatically runs privacy-audit.ps1 -FullHistory. A dedicated
GitHub workflow runs the same audit with fetch-depth: 0 on every push and PR,
including version branches. Scanner success is a privacy result, not gameplay,
build or release acceptance. Existing build checks retain their own scope.

For a local review: pwsh -NoProfile -File scripts/privacy-audit.ps1 -FullHistory.
Stage newly added files before review so tracked-file scans include them; the
hook checks the resulting commit again before transmission. -PrePush adds
release-oriented clean-tree/main/tag reporting and is not required for a normal
version-branch push. Output suppresses matched sensitive values.

Image pixels and compressed/opaque binary content are not fully covered by
metadata scanning. Inspect tracked images before pushing. Historical binary
metadata is not covered by the historical text vector. Findings do not authorize
history rewriting; resolve genuine leaks and classify false positives explicitly.

Ordinary authorized pushes of an already-public version branch still run the hook and CI. They are not a
first-push of an unpublished line. Findings still do not authorize rewriting published history.

Scope for this change: privacy automation only. No product payload, release,
tag or settings data is produced or changed by the scanner or hook installer.
