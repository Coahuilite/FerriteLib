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

- **The authoritative payload is the GitHub Release asset** published from `coahuilite.ferritelib` (its body
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
