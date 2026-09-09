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
   sessions, a widget registry, creation-time contract validation, per-element recovery, and a window
   shell that owns the chrome (background, title, close affordance) so a consumer never has to leave the
   tree to open a page. Consumer code writes business logic and bindings; structure lives in XML.
2. **A visual core** — theme tokens, drawing helpers, text measurement, text-fit audit, and the version
   contract. Reachable *without* adopting the page model.

Both boundaries are gates, not conventions: no visual-core file may name a page-model type, and every
call into the game's immediate-mode surface must live in one of the five named funnel files. Outside
them, raw IMGUI is unsupported and unmeasurable rather than forbidden — it is invisible to the harness,
the fit audit and session recovery, which is the whole point of the tree.

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

**Third-party use is invited (maintainer ruling 2026-09-10).** Compile against the release asset, and open
an issue when the library forced you to hand-roll something — a citation into your own working code is how
this library grows; a request alone is not. Bug reports are always welcome. A PR is not promised a merge
while the API is provisional, and `CONTRIBUTING.md` does not exist yet: when the surface freezes, what will
and will not break afterwards is written down there.

## Local verification and build

```powershell
pwsh -NoProfile -File scripts/verify-local.ps1            # the gate suite (harness + builds + payload + content-free + licence + identity)
pwsh -NoProfile -File scripts/verify-local.ps1 -PackDev   # + staged dev folder dist/dev/FerriteLib (no archive; you place it)
pwsh -NoProfile -File scripts/privacy-audit.ps1 -FullHistory   # three-vector privacy gate, run before any push
```

- Protocol / invariants: `AGENTS.md` · durable facts: `MEMORY.md` · action surface: `TODO.md`
- Release flow and rc scheme: `.github/workflows/release.yml` header comments (the tag dialect is the
  contract: `vBASE-rcN` trials, bare `vBASE` on the last rc's commit)
