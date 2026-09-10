# AGENTS.md — FerriteLib

> Stable layer: only the rules a session must hold before acting. Everything evidenced, measured, or
> dated lives in `MEMORY.md`; everything actionable in `TODO.md`. Read `MEMORY.md` before claiming
> project context.

## Project identity

- RimWorld 1.6 prerequisite mod `coahuilite.ferritelib` (display **FerriteLib**, MPL-2.0). The entire
  payload is `1.6/Assemblies/FerriteLib.UiKit.dll` — zero Defs, Patches, Languages, textures. Never add
  a `Languages/` folder to "fix" a missing string; the string belongs to whichever consumer renders it.
- Namespace root `FerriteLib.UiKit`, kernel surface `FerriteLib.UiKit.Kernel`, log prefix
  `[FerriteLib.UiKit]` (not a Def prefix, not a packageId).
- Release/publication state (rc-only window, tag rules, hash ledger): `MEMORY.md` — never restate it here.
- Exactly one consumer is wired, so every validated-against-a-real-page claim rests on that one tree; its
  identity and coverage are `MEMORY.md` facts, and un-wired candidates stay unnamed.

## Purpose and non-goals

The product is a **retained UI layer over RimWorld's immediate-mode `Verse` GUI**: structure lives in data
(an XML manifest plus typed bindings), and the five things a consumer cannot hand-roll safely — identity,
state, invalidation, recovery, and one audit surface — are owned here. The library is worth what a
consumer's tree keeps inside it, not what it ships in controls. Non-goals (no uGUI backend, no reflection,
DI or codegen, no expressions in manifest XML, no Def hot-reload promise, no world-space rendering), the
evidence classes behind each, and the 2026-09-10 ruling that the library is referenceable by strangers and
held to general-library standards: `MEMORY.md`, "Charter".

## Compatibility and retirement

The promise is `docs/api-tiers.md`: **stable** types keep their signatures inside the `[min, max)` range a
consumer compiles against, **public-unstable** types are usable but expected to change shape, and
**internalize-candidate** types are public only by history. A harness lane classifies every exported type
against that file and pins the stable list, so an addition to the public surface fails until it is decided
in two places.

Manifest vocabulary retires the way code does, and never silently: a deprecated attribute keeps working —
redirected to its replacement, not ignored — for at least one minor, and removal happens only at a minor
boundary (maintainer ruling 2026-09-10). A minor bump is the breaking signal pre-1.0, which makes this the
one mechanism that lets the library say "compile against what you tested" without lying.

## The two layers

1. **Declarative page engine** — XML manifest, constrained layout, typed bindings, per-window session,
   widget registry, creation-time contract validation.
2. **Visual core** — theme tokens, drawing helpers, text measurement, fit audit, version contract;
   reachable without adopting the page model.

The split is enforced (`KernelContractTests.VerifyVisualCoreIsPageModelFree`), which is why there is one
assembly and no `FerriteLib.Core`; re-open only if a consumer needs the visual core without the whole DLL.

## Invariants (stop and ask before violating)

- **Single carrier.** Only this mod ships `FerriteLib.UiKit.dll`. Two copies bind by load order through
  the game's one global `AssemblyResolve` and the losing copy cannot find out;
  `FerriteLibVersion.Require` enumerates loaded copies and its collision report is the only detector.
- **Neutrality.** No consumer's product vocabulary in the library or its harness — words
  case-insensitively, identifier prefixes case-sensitively; `coahuilite` is the series namespace,
  allowed. The guard is self-applied here with a positive control and a path-pinned self-exemption.
- **Version axes.** Contract `FerriteLibVersion.Api` (pre-1.0: a minor bump IS breaking, and any public
  addition bumps minor too), release `About/About.xml <modVersion>`, build csproj `VersionPrefix` — the
  harness pins all three. `AssemblyInformationalVersion` embeds the commit SHA and is never a
  compatibility value.
- **The game cannot express a prerequisite version** (`ModRequirement` parses only `packageId`,
  `alternativePackageIds`, `displayName`); every consumer asserts the API range in its own constructor.

- **IMGUI is RimWorld's only input and compositing authority.** The kernel renders through Verse IMGUI
  by design; a Canvas/uGUI surface cannot sort above the game's IMGUI, so any future non-IMGUI backend
  composites through a RenderTexture blit, never "above the HUD".
- **Map-layer rendering is permanently outside UiKit.** `GenMapUI` and its family carry no session and
  no hit-test semantics to bind to, so world-space text (pawn-head marks and friends) is something this
  library cannot provide — a non-goal, not a backlog item. A consumer's whitelist entry for it is
  policy-backed and is not renegotiated per PR; the boundary that remains is that anything drawn into a
  window goes through the tree.
- **API freeze is gated on the second wired consumer**, not on features; until it builds against this
  surface, the API is provisional. After a Workshop page carries the stable packageId, "breaking
  changes are expected" stops being free — the invited-vs-unsupported call (`TODO.md` §5) is open and
  is a maintainer decision, never a session's.

## Ecosystem protocol (how this library may grow)

Growth has one legal source: a real consumer was forced to hand-roll something, the code exists, and the
shape is generally providable. A request is not evidence; a citation into a consumer tree is — and as no
clone has that tree, the citation must be transcribed into `MEMORY.md`: `owner/repo@sha:path:line`, the
excerpt, and what it proved. A public permalink may accompany it, never replace it.

Consumer ladder when the library lacks something:

1. **Compose it on the tree** — own widget kind via the registry, on `UiNative` primitives and session
   axes. This is the recommended destination for anything consumer-specific; identity, state,
   invalidation, recovery and harness visibility stay with the library. A window is no longer an
   exception to this: `UiWindowHost` owns the chrome, so a page never has to leave the tree to exist.
2. **Propose promotion** — one release after it survives, carrying the transcription above plus the
   metric. Outside contributors propose by issue; the maintainer-side vehicle is a `HANDOFF` round
   (harvest loop and full form: `MEMORY.md`).
3. **Register an exemption** — only for what structurally cannot live in a page tree, which after the
   round-1 shell is world-space rendering and nothing else currently known. Allowlisted file + written
   ruling + named closing item: rent, not exit.

Promotion gate — all four or it stays consumer-side: provenance cited; neutral, and no consumer's
numbers as library defaults; no new process-wide mutable statics; a harness-drivable lane exists.

**What earns a kind.** Registering a widget kind is not shipping a convenience, it is freezing vocabulary:
every kind brings an attribute schema, a declared label set, a tier entry and eventually a deprecation debt.
A kind is earned by owning what a manifest cannot express — per-element interaction state, a measure
contract over its own content, or a hit/geometry rule. Something that only arranges children and paints
chrome is a container plus attributes, and a composite worth handing to two consumers is a recipe that
returns element specs — never a registered kind, and never a widget instance, because composing by type is
the bypass the tree-membership metric counts.

Unproven surface is debt, not inventory: zero-citation kinds and session axes get deleted or reshaped to
a cited consumer's proven form, never kept "for symmetry".

Raw IMGUI outside the tree is not forbidden — it is unsupported and unmeasurable: the harness, the fit
audit, session recovery, popup geometry rules and the dependency-reality proof bind to tree code only. The
metric is raw-backend call sites outside the funnel files the containment gate names; the cross-repo total
is a maintainer-side number, never a measurement a clone can reproduce.

## Build and verification

```powershell
pwsh -NoProfile -File scripts/verify-local.ps1                       # 7 gates
pwsh -NoProfile -File scripts/verify-local.ps1 -PackDev              # + staged dev folder (placement is manual)
pwsh -NoProfile -File scripts/pack-release.ps1 -Version v0.3.0-rc2   # + GitHub asset (what CI runs)
pwsh -NoProfile -File scripts/pack-steam.ps1  -Version v0.3.0-rc2    # + Workshop upload folder
```

`-PackDev` stages `dist/dev/FerriteLib/` and stops there: nothing under `scripts/` writes outside the
repository, and installing a folder into a game `Mods/` directory is the developer's own step (Boundaries).

Three channels (`pack-dev` / `pack-release` / `pack-steam`), one staging engine (`stage-package.ps1`). The
engine owns what a package *is* — the closed file set, the content probe, the licence copy, `version.txt`,
and a **measured** build configuration, so a channel label that does not match the payload's bytes is
refused rather than trusted. The packers own identity only: dev tolerates a dirty tree and says so, github
requires the tag shape and the build axis, steam additionally a clean tree, and only github archives.
`About/PublishedFileId.txt` is gitignored and the stager copies `About.xml` as a file rather than the
directory, so no rehearsal or GitHub artifact can carry a Workshop identity. The measurements behind this
split live in `MEMORY.md` ("Three channels, one staging engine", "A release asset must be built after the
gates run", "Only the GitHub channel archives").

A consumer integrates through the published GitHub Release asset; a same-level sibling folder with
`Private=false` is one developer's lockstep arrangement, not a contract and not a layout a clone can
assume (rationale: `MEMORY.md`). Either way `1.6/Assemblies/` and the `tools/.../Stubs/` tree with its
`bin/stubs/` shape are de-facto published surfaces — relocating them breaks a consumer's harness while
every gate here stays green.

## Memory protocol

Three-file split: `AGENTS.md` stable, `MEMORY.md` the only volatile ledger, `TODO.md` the action surface.
Record a PASS only with its scope and evidence source, saying which half is mutation-proven and which is
only a future-regression guard. Cross-repo coordination lives in maintainer-local `HANDOFF.md`, which is
gitignored: its section kinds and round lifecycle are pinned in that file's own header, no tracked file
may depend on reading it, and an un-CLOSED round keeps a pointer line in `TODO.md`.

## Boundaries

- Default scope is this repository alone: a public checkout holds no sibling mod and no consumer tree, so
  no rule, gate, script or evidence here may require one. Another repo is read-only, and only for a
  session the maintainer names; writing one needs their authorization in that same instruction.
- No `git remote`, no push, no tag, no release, no registry publish without explicit maintainer
  authorization. Local commits are fine.
- No personal absolute paths, log excerpts, tokens or `PublishedFileId.txt` values in tracked files, and no
  `../<sibling>` citations either — external evidence is a transcription or a public permalink.
- **No script places a mod in the game.** Nothing under `scripts/` writes outside the repository, and no
  step copies, links or junctions anything into a `Mods/` directory — a link back into the repo would bind
  build output to a machine-local layout another clone cannot see, reproduce, or (without elevation)
  create. Keep artifacts under `dist/` and say where they are.
