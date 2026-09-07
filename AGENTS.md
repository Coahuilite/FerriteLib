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
- Known consumers: Universal Squeaker (wired; the only behaviour this shape is validated against) and
  one sibling deferred — its identity lives in maintainer-local `HANDOFF.md`, published docs stay neutral.

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
  library cannot provide — a non-goal, not a backlog item (ruled in the US→FL round 1 review). A
  consumer's whitelist entry for it is policy-backed and is not renegotiated per PR; the boundary that
  remains is that anything drawn into a window goes through the tree.
- **API freeze is gated on the second wired consumer**, not on features; until it builds against this
  surface, the API is provisional. After a Workshop page carries the stable packageId, "breaking
  changes are expected" stops being free — the invited-vs-unsupported call (`TODO.md` §5) is open and
  is a maintainer decision, never a session's.

## Ecosystem protocol (how this library may grow)

Growth has one legal source: a real consumer was forced to hand-roll something, the code exists, and the
shape is generally providable. A request is not evidence; a citation into a consumer tree is.

Consumer ladder when the library lacks something:

1. **Compose it on the tree** — own widget kind via the registry, on `UiNative` primitives and session
   axes. This is the recommended destination for anything consumer-specific; identity, state,
   invalidation, recovery and harness visibility stay with the library. A window is no longer an
   exception to this: `UiWindowHost` owns the chrome, so a page never has to leave the tree to exist.
2. **Propose promotion** — one release after it survives, via a HANDOFF round (harvest loop and full
   form, with provenance and the metric: `MEMORY.md`).
3. **Register an exemption** — only for what structurally cannot live in a page tree, which after the
   round-1 shell is world-space rendering and nothing else currently known. Allowlisted file + written
   ruling + named closing item: rent, not exit.

Promotion gate — all four or it stays consumer-side: provenance cited; neutral, and no consumer's
numbers as library defaults; no new process-wide mutable statics; a harness-drivable lane exists.

Unproven surface is debt, not inventory: zero-citation kinds and session axes get deleted or reshaped to
a cited consumer's proven form, never kept "for symmetry".

Raw IMGUI outside the tree is not forbidden — it is unsupported and unmeasurable: the harness, the fit
audit, session recovery, popup geometry rules and the dependency-reality proof bind to tree code only.
The ecosystem metric is raw-backend call sites outside the funnel files, counted across both repos; the
funnel files are named by the containment gate, and that list is the measurement.

## Build and verification

```powershell
pwsh -NoProfile -File scripts/verify-local.ps1              # 7 gates
pwsh -NoProfile -File scripts/verify-local.ps1 -PackDev     # + mod zip and NuGet package
pwsh -NoProfile -File scripts/verify-local.ps1 -PackDev -StageOnly   # + installable folder only, no archive
```

`-StageOnly` lays out `dist/dev/FerriteLib/` for an in-game pass without producing a zip or a nupkg;
the gates run first either way, so a directory rehearsal is a gated artifact. `pack-dev.ps1` builds with
`--no-incremental` because Dev and Release share the payload path — see `MEMORY.md`.

Consumers reference the payload by relative sibling path with `Private=false` (measured rationale:
`MEMORY.md`). The `1.6/Assemblies/` output path and the `tools/.../Stubs/` tree with its `bin/stubs/`
shape are de-facto published surfaces the consumer's harness builds against — relocating any of them
breaks the consumer while every gate here stays green.

## Memory protocol

Same three-file split as the sibling repos: `AGENTS.md` stable, `MEMORY.md` the only volatile ledger,
`TODO.md` the action surface. Record a PASS only with its scope and evidence source, saying which half
is mutation-proven and which is only a future-regression guard. `HANDOFF.md` is transport, never
the system of record, and carries three section kinds with distinct lifecycles (pinned in its own
header): a session buffer (read-and-drop next session), cross-repo **rounds**
(`OPEN→REVIEWED→SCHEDULED→CLOSED`; proposal + review share one file; each repo consolidates only
its own buffer; an un-CLOSED round keeps a pointer line in `TODO.md`, and closing means the bodies
are gone from the buffer), and a standing local annex (never emptied). Rounds count on the library
side and name the initiating consumer in full — consumer feedback is this library's only growth
engine, and the attribution is the incentive; item ids are proposer-given and never renumbered.

## Boundaries

- Default scope is this repository root plus read-only inspection of sibling repos a consumer contract
  requires.
- No `git remote`, no push, no tag, no release, no registry publish without explicit maintainer
  authorization. Local commits are fine.
- No personal absolute paths, log excerpts, tokens or `PublishedFileId.txt` values in tracked files;
  consumer references are relative by design.
