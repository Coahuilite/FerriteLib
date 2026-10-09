# Consumers — how another mod takes FerriteLib

This directory is the **permanent, always-updated** home for consumer integration, referenced across sessions
and by other projects. It does not duplicate the surface docs; it tells a new consumer which three documents
to read and what to take.

| Document | When to read it |
| --- | --- |
| `consume-from-0.7.0.md` | Start here for the 0.7 line. Artefact to take (and which payload is authoritative), version assertion, what changed from 0.6 (default theme, validation), what is still open. **§4c is the old→new migration for the one change that cannot fail to compile: since 0.5, removing an element releases its state.** |
| `consume-from-0.6.0.md` | The 0.6 line's onboarding, kept as versioned history. |
| `../development/0.6/20-api-and-xml.md` | How each capability is used (XML + C#), incl. vocabulary traps. |
| `../api-tiers.md` | The compat promise (stable / public-unstable / internalize-candidate). |
| `../development/0.5/20-api-and-xml.md` | The 0.5 base surface (manifest, styles, bindings, tabs, repeat, window catalog). |
| `ordinary-settings.md` | The recommended authoring recipe for an ordinary settings page (0.7): plain model + typed bindings + explicit notification, no custom widget. |

Permanent consumer-facing facts (kept current here so a later session or a consumer session does not have to
re-derive them):

- **In this phase there is no Release asset and none is created** (maintainer ruling 2026-09-24): the remote is an
  off-site backup only — no tag, no release — local testing uses the **Dev** package, and `pack-release` /
  `pack-steam` are not run. **When a line is published**, the authoritative payload is the GitHub Release asset
  published from `coahuilite.ferritelib` (its body
  names the commit and the asset's SHA-256). A repo dev folder, a sibling checkout's copy and anything left in
  `1.6/Assemblies/` after a build are **rehearsals** — a Dev channel builds different bytes than a Release one,
  so two hashes for one Api are two builds, not two identities for one artifact. Verify what you actually bound
  (the assembly's `AssemblyConfiguration` and embedded commit, plus the `Require` verdict) instead of matching a
  number written in a document.
- Dev package (rehearsal, history): `dist/dev/FerriteLib/` (folder) and `dist/dev/FerriteLib-0.6.0-dev.zip`;
  label `FerriteLib 0.6.0-dev`, commit `7b62416fa623`, DLL SHA-256 `2A7F9C9E…ECC0D` (R06-fixed, Dev channel).
- Single-carrier rule: only `coahuilite.ferritelib` ships `FerriteLib.UiKit.dll`; consumers reference with
  `<Private>false</Private>` and never copy the DLL.
- After fetching a new package, rebuild with `--no-incremental` (path-resolved reference + stale build trap).
- `UiHost.Source` is the widget-registry scope — register kinds under the exact page identity you open.

## Dated consumer-facing reports

Reports written for one consumer, kept here so they have a **delivery channel** instead of living as an
untracked file. A dated report is a snapshot of its **evidence time**, not of today: read its own evidence-class
and unmeasured sections before quoting any number from it.

- [`../handoff-ngs-uikit-delta-2026-09-25-zh.md`](../handoff-ngs-uikit-delta-2026-09-25-zh.md) — **面向 NGS
  (NivariansGrandStructure) 的 0.7.0 消费者影响报告** (Chinese): what the public surface gained from early 0.7.0
  to the then-current HEAD, the four breaking changes for a consumer pinned to `[0.7.0,0.8.0)`, the capability
  boundary of the external XML-driven UI, the byte / SHA-256 identity of the existing build artifacts, and the
  explicit unmeasured list. **作者归属待维护者确认 (author attribution pending the maintainer's confirmation)**;
  committed at `1236567`; it carries a pre-delivery identity re-check in its **§6.1**. Evidence is read-only —
  no build, test, package or mutation step was run — so every UNRUN / 未取证 item stays marked as such.
