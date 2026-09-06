# FerriteLib

[**English**](./README.md) | [中文](./README.zh-CN.md)

A **no-content prerequisite mod** for RimWorld 1.6. FerriteLib carries one assembly —
`FerriteLib.UiKit.dll` — that Coahuilite mods compile against and bind to at runtime. It adds no Defs,
no patches, no languages, no textures: enabling it alone changes nothing in the game. If it appeared in
your mod list without a consumer installed, that consumer was uninstalled or disabled; the mod is safe
to disable only when no enabled mod depends on it.

## What the library provides

Two layers, kept separable on purpose:

1. **A declarative page engine** — XML manifests, constrained layout, typed bindings, per-window
   sessions, a widget registry, and creation-time contract validation. Consumer code writes business
   logic and bindings; structure lives in XML.
2. **A visual core** — theme tokens, drawing helpers, text measurement, text-fit audit, and the version
   contract. Reachable *without* adopting the page model.

The split is enforced by a gate, not by convention: no visual-core file may name a page-model type.

## Requirements

- RimWorld 1.6
- Consumers install this mod **and** their own mod; the game warns when a consumer is enabled without
  this carrier. Do not copy `FerriteLib.UiKit.dll` into another mod's folder — exactly one carrier may
  be installed, because two copies bind by load order through the game's single global
  `AssemblyResolve` and the losing copy has no way to find out.

## Identity

- packageId: `coahuilite.ferritelib`
- namespace root: `FerriteLib.UiKit` (kernel surface: `FerriteLib.UiKit.Kernel`)
- log prefix: `[FerriteLib.UiKit]`
- license: MPL-2.0, text ships inside every package (`LICENSE`)

## For modders

RimWorld's `modDependencies` cannot express a version, so every consumer must assert the API range in
its own constructor — `FerriteLibVersion.Require(min, max, packageId, out report)` enumerates loaded
carriers, reports collisions, and fails readable. Compile against the DLL from a GitHub Release asset
of this repository (the release body quotes its SHA-256); the assembly is `net472`. The public API is
**pre-1.0 and provisional**: a minor bump is a breaking change, and the freeze decision is gated on a
second wired consumer.

**Third-party contributions are not currently solicited** — the API freeze / invited-vs-unsupported
call is an open maintainer decision (see `TODO.md` §5). Bug reports are welcome; a PR cannot be
promised a merge while the surface is provisional.

## Local verification and build

```powershell
pwsh -NoProfile -File scripts/verify-local.ps1            # the gate suite (harness + builds + payload + content-free + licence + identity)
pwsh -NoProfile -File scripts/verify-local.ps1 -PackDev   # + dev mod package and NuGet package (dist/, artifacts/)
pwsh -NoProfile -File scripts/privacy-audit.ps1 -FullHistory   # three-vector privacy gate, run before any push
```

## Documentation index

- Protocol / invariants: `AGENTS.md` · durable facts: `MEMORY.md` · action surface: `TODO.md`
- Release flow and rc scheme: `.github/workflows/release.yml` header comments (the tag dialect is the
  contract: `vBASE-rcN` trials, bare `vBASE` on the last rc's commit)
