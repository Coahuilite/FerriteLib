# Consumers — how another mod takes FerriteLib

This directory is the **permanent, always-updated** home for consumer integration, referenced across sessions
and by other projects. It does not duplicate the surface docs; it tells a new consumer which three documents
to read and what to take.

| Document | When to read it |
| --- | --- |
| `consume-from-0.6.0.md` | Start here. Artefact to take, version assertion, reference setup, the registry-scope trap, what is still open. |
| `../development/0.6/20-api-and-xml.md` | How each capability is used (XML + C#), incl. vocabulary traps. |
| `../api-tiers.md` | The compat promise (stable / public-unstable / internalize-candidate). |
| `../development/0.5/20-api-and-xml.md` | The 0.5 base surface (manifest, styles, bindings, tabs, repeat, window catalog). |

Permanent consumer-facing facts (kept current here so a later session or a consumer session does not have to
re-derive them):

- Dev package: `dist/dev/FerriteLib/` (folder) and `dist/dev/FerriteLib-0.6.0-dev.zip` (same, zipped).
- Package label: `FerriteLib 0.6.0-dev`, commit `7b62416fa623`; DLL SHA-256
  `2A7F9C9E…ECC0D` (R06-fixed).
- Single-carrier rule: only `coahuilite.ferritelib` ships `FerriteLib.UiKit.dll`; consumers reference with
  `<Private>false</Private>` and never copy the DLL.
- After fetching a new package, rebuild with `--no-incremental` (path-resolved reference + stale build trap).
- `UiHost.Source` is the widget-registry scope — register kinds under the exact page identity you open.
