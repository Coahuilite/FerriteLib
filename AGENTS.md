# AGENTS.md — FerriteLib

> Stable layer: only the rules a session must hold before acting. Everything evidenced, measured, or
> dated lives in `MEMORY.md`; everything actionable in `TODO.md`. Read `MEMORY.md` before claiming
> project context — and note that a `MEMORY.md` citation into `OBLIVIONIS.md` is a citation into the cold
> archive, which holds the pre-0.7 history verbatim and cannot override a current source.

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
held to general-library standards: `OBLIVIONIS.md`, "Charter" (the founding spec, the surveys and the
style-layer argument live there).

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
  **TEMPORARY EXEMPTION, with an expiry: on the 0.7.x line a public addition does NOT bump the minor**
  (maintainer ruling 2026-09-22, "minor 可以不升级，仍然算到 0.7.x 内"; reasoning added the same day: "这个算临时放行，
  因为现在就在跨库合作开发 fl0.7.x" — this is a release valve, NOT a change to the rule itself). **Reason:**
  the library and its consumer are in cross-repository lockstep development on 0.7.x, where a minor bump
  buys no compatibility signal and only churns both trees. **Expires** at the first of: the 0.7.x line's first
  release/tag, or the end of that lockstep — then the generic pre-1.0 sentence above resumes and an addition
  moves the minor again. **Never inherited:** it belongs to the `0.7.x` line and this stage alone, and must
  not be carried into the next line or into any published state by analogy. **No lane reads "additions" today, so
  nothing has to be re-cut for this exemption** — measured 2026-09-22 across the whole harness and the
  script gates: `FerriteLibVersionTests` pins the three axes agreeing on major.minor and never inspects the
  surface, `FerriteLibApiTierTests` reacts to an addition by requiring a tier (a documentation act in
  `docs/api-tiers.md`), and no gate or lane anywhere enforces the minor bump itself. If a future lane starts
  reading additions — or its message wording starts asserting the minor rule — align it in the batch that
  next touches the carrier, because running the harness WRITES the shared carrier and would otherwise move
  the freeze. `MEMORY.md` (§ Version axes) carries the same exemption, its expiry, and the lane-by-lane audit
  behind "no gate reads additions".
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
- **API stabilization is not gated on a consumer count.** Define the supported contract, verify its
  behavior, and state the compatibility commitment so consumers have a dependable surface to adopt.
  Real-consumer integration is valuable validation, not a prerequisite for stabilizing that surface;
  missing integration evidence must remain explicit. This does not automatically promote existing API
  tiers or waive version, migration, or verification requirements. After a Workshop page carries the
  stable packageId, "breaking changes are expected" stops being free — the invited-vs-unsupported call
  (`TODO.md` §5) remains a maintainer decision, never a session's.

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
   (harvest loop and full form: `OBLIVIONIS.md`, "Ecosystem protocol").
3. **Register an exemption** — only for what structurally cannot live in a page tree, which after the
   round-1 shell is world-space rendering and nothing else currently known. Allowlisted file + written
   ruling + named closing item: rent, not exit.

Promotion gate — all four or it stays consumer-side: provenance cited; neutral, and no consumer's
numbers as library defaults; no new process-wide mutable statics; a harness-drivable lane exists.

**The gate governs *new specialized kinds*, not confirmed public capabilities** (2026-09-15, superseding
the broader reading of the rule above). Registering a kind freezes vocabulary, which is why it waits for a
citation. A capability a page model structurally cannot express — window-instance identity, binding
notification, collection reconciliation, document hot reload, per-host diagnostics — is implemented from
the confirmed requirement directly. The distinguishing question is "does the page model own a general
surface here?", not "has somebody already hand-rolled a copy elsewhere"; a request is still not evidence
for a *kind*, and our own demo is still not consumption. The superseded wording, the round it changed in and
the affected decisions are recorded in `docs/development/0.5/00-baseline.md` §2.4.

**What earns a kind.** Registering a widget kind is not shipping a convenience, it is freezing vocabulary:
every kind brings an attribute schema, a declared label set, a tier entry and eventually a deprecation debt.
A kind is earned by owning what a manifest cannot express — per-element interaction state, a measure
contract over its own content, or a hit/geometry rule. The test is about ownership, not about being atomic:
a composite may hold a name (no surveyed toolkit dissolves its named composites into atom-only code —
`OBLIVIONIS.md`, the industry survey), but it must not duplicate a surface the engine already carries,
and once the atoms exist its innards must be an explicit composition over them, so the name stays stable
and the tree inside is rebuilt.
What never earns a name is a widget instance handed out by type: composing by type is the bypass the
tree-membership metric counts.

**Our own demo is not consumption.** Rendering a kind in this library's diagnostic surface proves it draws,
measures and recovers under the real font; it proves nothing about whether the kind should exist, and it is
never provenance for the promotion gate. Consumption is a page in another repository that a player uses and
whose needs forced the shape — the only evidence the gate accepts, and the only thing that may raise the
validated-surface count.

Unproven surface is debt, not inventory: zero-citation kinds and session axes get deleted or reshaped to
a cited consumer's proven form, never kept "for symmetry".

Raw IMGUI outside the tree is not forbidden — it is unsupported and unmeasurable: the harness, the fit
audit, session recovery, popup geometry rules and the dependency-reality proof bind to tree code only. The
metric is raw-backend call sites outside the funnel files the containment gate names; the cross-repo total
is a maintainer-side number, never a measurement a clone can reproduce.

## Build and verification

```powershell
pwsh -NoProfile -File scripts/verify-local.ps1                       # 9 gates
pwsh -NoProfile -File scripts/verify-local.ps1 -PackDev              # + staged dev folder (placement is manual)
pwsh -NoProfile -File scripts/pack-release.ps1 -Version v0.3.0-rc2   # + GitHub asset (what CI runs)
pwsh -NoProfile -File scripts/pack-steam.ps1  -Version v0.3.0-rc2    # + Workshop upload folder
```

`-PackDev` stages `dist/dev/FerriteLib/` and stops there: nothing under `scripts/` writes outside the
repository, and installing a folder into a game `Mods/` directory is the developer's own step (Boundaries).

**The payload path is shared, and that is the trap.** `1.6/Assemblies/FerriteLib.UiKit.dll` is written by
both configurations, so whatever ran last decides what a sibling-`HintPath` consumer compiles against:

- **`-PackDev` writes Dev bytes to the shared carrier.** After it — and after any `verify-local` run, whose
gate 2 is the Dev build — the delivery step ends with a forced `dotnet build -c Release --no-incremental`
**and** removal of the stale `1.6/Assemblies/FerriteLib.UiKit.pdb` (Release sets `DebugType=none`, so it
neither rewrites nor deletes an existing PDB). `AssemblyConfigurationAttribute` is the detector, never the
version suffix: a correct Release carrier still reads `0.7.0-dev+<sha>`.
- **`verify-local` is a writer, and so is the harness.** Its gate 2 is the Dev build, and gate 1's
`dotnet run --project tools/FerriteLib.UiKit.Tests` is not a reader either: the harness project carries a
`ProjectReference` to the library, whose `OutputPath` is this very folder, and the generated `AssemblyInfo`
embeds the commit SHA — so any MSBuild pass over that graph can rewrite the carrier, and a HEAD that has moved
since the last build is enough to make it out of date. (Measured twice, and both were real accidents rather
than a precaution: 2026-09-20, a pre-commit harness run produced the round-5 carrier as a dirty build; and
2026-09-22, a verify-only gate run after a freeze left the stale Dev PDB back beside the frozen carrier.)
**Verification of a frozen carrier is therefore read-only**: the hash, the configuration, the stamp read in a
child process, the absence of a PDB, the exclusivity probe, and `git rev-parse`/`status` — never a build of the
project graph. A full gate run belongs to the delivery step: the same builder ends it with the forced Release
rebuild, the PDB removal and the re-verification, and issues a **new FREEZE NOTICE** — the hash may have moved,
and downstream must re-verify against the new identity. Consequently a session that only means to VERIFY a
frozen carrier must not run the gate chain at all.

**A gate's retry hint is a build command, and that is the second face of the same trap.** The output of a red
gate is read inside "I am verifying" — so a hint like `dotnet build ... -c Release --no-incremental` invites a
rebuild in exactly the frame that must not perform one. That is not hypothetical: 2026-09-22 a consumer-side
verification session copied a red gate's rebuild hint, rebuilt the carrier and moved the frozen hash. Any
verification step therefore starts by asking **whether it writes**, and a hint that does write is a delivery
step: owner-only, hash-moving, ended by a new FREEZE NOTICE. The judgement is **whether the command writes**,
not whether it is named a build: gate 1's `dotnet run --project tools/FerriteLib.UiKit.Tests` writes too — the
harness project's `ProjectReference` puts the library's own `OutputPath` in the graph, so moving HEAD alone is
enough to recompile the carrier, which is why the rule reads "and so is the harness". `scripts/verify-local.ps1`
prints its hints under `[hint]` and annotates exactly the two writers, but the rule holds for every gate in
every repository.

**A non-building execution of an already-built harness is a reader — measured 2026-09-22.**
`dotnet run --no-build --no-restore --project tools/FerriteLib.UiKit.Tests -c Release` against a frozen carrier
(`490d4f0`, Release) left it untouched: SHA-256 `E396E089…ACB` before and after, and the **mtime identical**
(`14:24:45`) — the decisive signal, because a rewrite with equal bytes still moves it — with no PDB appearing,
`HARNESS_EXIT=0`, `ALL PASS` 2623 ok. The copy the harness loaded
(`tools/…/bin/Release/net472/FerriteLib.UiKit.dll`) was byte-identical to the carrier, so that run genuinely
exercised the frozen payload rather than a stale copy. **Two boundaries:** it does not make the plain
`dotnet run` a reader (that one still builds and writes — it is the writer this section names), and it runs the
**last built** binaries, so with sources or HEAD moved since that build it measures stale code and is not a
substitute for the gate chain when anything changed.
- **A hash is an identity only with its inputs pinned.** Quote one with the build command and the clean/dirty
state beside it. The stamp records the **committed** revision and is silent about uncommitted source.
- **A doc-only commit reddens "embedded commit == HEAD" until the carrier is rebuilt.** That is expected
rather than a defect — but broadcast it, because the consumer's gates read it.
- **The carrier is exclusive, and readers count.** A loaded assembly holds its file open, so a sibling
checkout's harness or probe blocks this build exactly as another builder would; the symptom is
`MSB3026`/`MSB3027`/`MSB3021` deep inside captured build output, which reads like a broken build.
**Never `Assembly.LoadFile` the payload in the session that is measuring it** — the handle survives to
process exit. Read identity from a child process or from a copy.
- **A build happens only when the maintainer intends to test.** Building is not a session's reflex, and two
concurrent harness runs collide on the shared output path (`CS2012`).

Three channels (`pack-dev` / `pack-release` / `pack-steam`), one staging engine (`stage-package.ps1`). The
engine owns what a package *is* — the closed file set, the content probe, the licence copy, `version.txt`,
and a **measured** build configuration, so a channel label that does not match the payload's bytes is
refused rather than trusted. The packers own identity only: dev tolerates a dirty tree and says so, github
requires the tag shape and the build axis, steam additionally a clean tree, and only github archives.
`About/PublishedFileId.txt` is gitignored and the stager copies `About.xml` as a file rather than the
directory, so no rehearsal or GitHub artifact can carry a Workshop identity. The measurements behind this
split live in `MEMORY.md` ("Packaging discipline").

A consumer integrates through the published GitHub Release asset; a same-level sibling folder with
`Private=false` is one developer's lockstep arrangement, not a contract and not a layout a clone can
assume (rationale: `MEMORY.md`). Either way `1.6/Assemblies/` and the `tools/.../Stubs/` tree with its
`bin/stubs/` shape are de-facto published surfaces — relocating them breaks a consumer's harness while
every gate here stays green.

## Evidence discipline (each rule cost a wasted round)

The ledger rule that a PASS names which half is mutation-proven needs these to be worth anything:

- **Red is no more trustworthy than green.** Before changing the product or the assertion, check the
  **instrument's inputs** — the fixture, the ruler, the screen, the channel. A lane can go green for the
  wrong reason (a fallback that accepts the case under test, a process-wide accumulator another lane
  satisfied, a stub ruler smaller than the real one, a scan reading a stale DLL) and red for the wrong
  reason too (a lane with no language table measuring the key instead of the translation).
- **A lane must go red under a faithful revert.** A lane that cannot tell the two states apart is not
  evidence, and shipping a feature whose lane stays green when the feature is removed is the failure this
  rule exists to stop. One mutation per item, attributable by assertion name.
- **When a measurement or notification channel changes, re-audit every lane that asserts the old one.**
  A stale lane is worse than no lane: it still runs, still passes, and no longer measures the thing.
- **Counter assertions `Reset()` and measure an increment.** The fit-audit counters are cumulative and
  process-wide, so a bare `count == 1` can be satisfied by a finding some other lane produced.
- **A spatial budget only means something at the real size.** Under a small stub ruler the content is
  shorter than it is in the game, so a budget that would collapse for real passes anyway.
- **A new gate is mutation-tested, not just written**, and its criterion is "plant the defect and the
  process exits non-zero", never "the console shows FAIL" — a lane that prints a failure without counting
  it is not a gate.
- **One coherent step per commit, and never leave an uncommitted half-finished state.** State explicitly
  what is unbuilt and unverified rather than letting "fixed" cover it.
- **A gate may only get stronger, or be re-cut in the same batch as the fix it depends on.**

## Memory protocol

Four-file split: `AGENTS.md` stable, `MEMORY.md` the only volatile ledger, `TODO.md` the action surface,
`OBLIVIONIS.md` the cold archive.

At every non-trivial session:

- Read `MEMORY.md` before claiming project context; it stores confirmed durable facts, decisions,
  constraints and evidence pointers.
- Read `TODO.md` before continuing work; it stores only current goals, open actions, blockers and explicit
  deferrals.
- Read `OBLIVIONIS.md` only for a historical conflict or an explicit request; it is cold archive evidence
  and cannot override current sources.
- The three active memory files are maintained in accurate English; `OBLIVIONIS.md` follows the same
  language rule when appended.

Maintain these boundaries:

- Update `MEMORY.md` only when durable facts or the open action surface changes.
- **Compact by default.** Settled release and implementation detail lives in `docs/`; `MEMORY.md` keeps a
  pointer to it, never a second copy of it — `OBLIVIONIS.md` exists so the ledger can shrink. Do not grow an
  active memory file with finished work.
- Update `TODO.md` only when its current task surface changes.
- Record a PASS only with its scope and evidence source, saying which half is mutation-proven and which is
  only a future-regression guard.
- Do not store session narratives, transient artifacts, raw logs, completed test matrices, commit chains or
  release checklists in an active memory file.
- Documentation edits alone are not memory events; an external-state summary never overrides its
  authoritative source.

**Handoff material is transient and is not a memory tier** (maintainer ruling 2026-09-18, correcting a
wrongful promotion). A maintainer-local `HANDOFF.md` is gitignored, is read at the moment of a round, holds
no standing authority, and is never a place a tracked file points at for a protocol. Anything durable it
carries is promoted into the files above or into `docs/` at that moment; a round that stays open keeps a
pointer line in `TODO.md`, and a closed round leaves nothing behind.

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

## Push discipline (added 2026-09-17; each rule was paid for once, on the 0.6.x first push)

Extends the boundaries above; it does not relax them.

- **Run `scripts/privacy-audit.ps1 -FullHistory` before the FIRST push of a line, not only before a
  release.** A line that has never been pushed is where a history rewrite is still cheap; afterwards it is
  not. (The 0.6 line sat local-only for a whole round and its pre-push audit failed on a blob from the
  *fork* commit.)
- **The audit scans `git rev-list --all`, so the check is "no reachable ref carries a personal path", not
  "the working tree is clean today".** Consequences seen: a path already fixed in the tree still failed
  while history kept the blob (`4a9c6b9` fixed the file; `02a6aea` still carried it); and a **backup tag
  created after the fix re-exposed the old history and failed the audit by itself**. Keep backups outside
  the repository (`git bundle` on local disk), not as tags or branches.
- **Clean history by rewriting only the unpublished range**, preserving author/committer dates and
  messages, and verify the final tree is byte-identical to the pre-rewrite tip. Expect a cross-repo
  cascade: the rewritten SHA is what a consumer cites and what the payload's
  `AssemblyInformationalVersion` embeds, so the payload must be **rebuilt** and the consumer's gates
  re-run (`scripts/stage-package.ps1` measures the build, so a stale payload is refused rather than
  trusted). Never rewrite a range that is already public.
- **Short-lived feature branches are deleted with their worktrees when their work lands.** Eleven stale
  `feat/0.5-*` / `feat/0.6-*` branches plus eleven `.fl-worktrees/` entries survived earlier rounds —
  hygiene debt, and (per the rule above) an audit liability. Confirm `git worktree list` shows only the
  main checkout before a push.
- **The commit identity is set once per repository and checked before writing history**, not before
  pushing it: a stray identity in one commit forces a rewrite of everything after it.
