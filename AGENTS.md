# AGENTS.md — FerriteLib

> Memory agreement for AI agents working in this repository. This file is the stable layer; volatile
> state lives in `MEMORY.md`, work items in `TODO.md`.

## Project identity

- RimWorld 1.6 **prerequisite mod** `coahuilite.ferritelib`, display name **FerriteLib**. Local repo, no
  remote configured, nothing published.
- Payload is one assembly: `FerriteLib.UiKit`, namespace root `FerriteLib.UiKit`, kernel surface
  `FerriteLib.UiKit.Kernel`.
- It ships **no game content of any kind**: no Defs, no Patches, no Languages, no textures. The mod's
  entire contribution is `1.6/Assemblies/FerriteLib.UiKit.dll`. There are therefore zero translation
  keys here today — do not add a `Languages/` folder to "fix" a missing string; the string belongs to
  whichever consumer renders it.
- License is **MPL-2.0** across the Coahuilite mod series. `LICENSE` is byte-identical between this repo
  and Universal Squeaker (measured 2026-09-07: same SHA-256); SqueakyRatkin carries the bare MPL text
  without the repo-level Exhibit A notice header, so "byte-identical in each repo" is true of the
  lib/consumer pair, not the whole series. The notice is deliberately **not** accompanied by an
  "Incompatible With Secondary Licenses" statement, so the assembly can still be combined with
  GPL-family mods. A distributed mod package is an *Executable Form*, so MPL 3.2 obliges us to say how
  to get source: `pack-dev.ps1` copies `LICENSE` into the package, and `verify-local` gate 6 rejects a
  truncated paste or an applied incompatibility notice.
- Log prefix for the library's own diagnostics: `[FerriteLib.UiKit]`. It is not a Def prefix and not a
  packageId.
- Known consumers: `coahuilite.universalsqueaker` (Universal Squeaker, the only one wired so far, and the
  only one whose behaviour this library's shape has been validated against).
  `coahuilite.nivariansgrandstructure` is **deferred by maintainer decision (2026-09-04)**, not scheduled.

## What this library is for

Two things, kept separable on purpose:

1. A **declarative page engine** — XML manifest, constrained layout, typed bindings, per-window
   session, widget registry, creation-time contract validation. Consumer code writes business logic and
   bindings; structure lives in XML.
2. A **visual core** — theme tokens, drawing helpers, text measurement, text-fit audit, and the version
   contract. Reachable *without* adopting the page model.

The split between those two is enforced, not documented: `KernelContractTests.VerifyVisualCoreIsPageModelFree`
fails if any visual-core file names a page-model type. That gate is the reason there is currently **one
assembly and no `FerriteLib.Core` package** — the boundary is already airtight, and a second assembly
would only add a second version contract to keep in step. Re-open the question only if a consumer needs
the visual core without the whole DLL.

## Invariants (stop and ask before violating)

- **Single carrier.** This mod is the only shipper of `FerriteLib.UiKit.dll`. Two mods shipping it bind
  by load order through RimWorld's one global `AssemblyResolve`, and the copy that loses has no way to
  find out. `FerriteLibVersion.Require` enumerates loaded copies and reports the collision; the report
  is the only detector.
- **Neutrality.** No consumer's product vocabulary may appear in the library or its harness, checked
  case-insensitively for words and case-sensitively for identifier prefixes. `coahuilite` is allowed —
  it is the series namespace, not a product. The guard is self-applied from inside this repo with a
  positive control and a self-exemption pinned to the scanner file itself.
- **Two version axes, never conflated.** The contract axis is `FerriteLibVersion.Api` (hand-authored;
  pre-1.0, a minor bump IS breaking). The release axis is `About/About.xml <modVersion>`. The harness
  pins them together on major/minor. `AssemblyInformationalVersion` embeds the commit SHA and must
  never be used as a compatibility value.
- **RimWorld's `modDependencies` cannot express a version** (`ModRequirement` parses only `packageId`,
  `alternativePackageIds`, `displayName`). Every consumer must assert the API range in its own
  constructor; do not assume the game will do it.
- **IMGUI is the only input and compositing authority in RimWorld.** The kernel renders through Verse
  IMGUI by design. A Canvas/uGUI surface cannot be placed above the game's IMGUI (verified: IMGUI
  composites last; `GUI.depth` orders IMGUI against IMGUI), so any future non-IMGUI backend has to be
  composited through a RenderTexture blit, not expected to sort above the HUD.
- **API freeze is gated on a second wired consumer**, not on features. Until NivarianGrandStructure
  actually builds against this surface, the public API is provisional and breaking changes are expected.
  **Caveat added with the publication decision (2026-09-04, `TODO.md` §5)**: "breaking changes are
  expected" is true *within our own repos* because we release in lockstep. It stops being free the moment
  a Workshop page carries a stable packageId, because a third party can then compile against this surface
  and lockstep cannot protect them. Do not read this invariant as licence to break things after publishing;
  the invited-vs-unsupported call on the page is what settles it, and it has not been made yet.

## Build and verification

```powershell
pwsh -NoProfile -File scripts/verify-local.ps1          # 7 gates
pwsh -NoProfile -File scripts/verify-local.ps1 -PackDev # + mod zip and NuGet package
```

Consumers reference the built payload **by relative sibling path**, not through a package feed. Measured
reason (`MEMORY.md`): NuGet strips build metadata from the cache identity, so a `0.1.0-dev+<sha>` republish
still lands in one stale `lib/0.1.0-dev` folder, and the consumer gates run with restore disabled, so a
floating version would go green against the previously resolved graph. `dotnet pack` is kept working so
that switching to a feed or a registry later is a source-line change, not a redesign.

## Memory protocol

Same three-file split as the sibling consumer repos: `AGENTS.md` stable, `MEMORY.md` the only volatile
ledger, `TODO.md` the action surface. Read `MEMORY.md` before claiming project context. Record a PASS
only with its scope and its evidence source, and say which half of a check is mutation-proven versus
which half is only a future-regression guard.

## Boundaries

- Default scope is this repository root plus the read-only inspection of sibling repos a consumer
  contract requires.
- No `git remote`, no push, no tag, no release, no registry publish without explicit maintainer
  authorization. Local commits are fine.
- No personal absolute paths, log excerpts, tokens or `PublishedFileId.txt` values in tracked files.
  Consumer references are relative by design, so nothing here needs a machine path.
