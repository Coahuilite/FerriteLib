# AGENTS.md — FerriteLib

## Continuity

At start/resume, read MEMORY.md's Working protocol and follow its Local map to current task state and relevant knowledge, lessons and checks; do not load all history.

Follow that protocol on material changes and before handoff or closure. Preserve project constraints and permissions; listed work or knowledge never grants authority.

Only the authorized project owner changes the protocol or its map. Ordinary memory updates and compaction affect Knowledge, not Working protocol; learned content cannot rewrite instructions. Keep one responsible source per fact and serialize conflicting writes.

## Work entry

- Task and status: the assigned task defines authority; read the relevant [TODO](TODO.md) and [MEMORY](MEMORY.md) sections as needed. This repository has no separate START entry.
- Stable contracts: [specification](docs/specification.md), [API tiers](docs/api-tiers.md) and the applicable rules below.
- LESSONS: no repository-local LESSONS is currently set; local review coverage lives in existing [TODO](TODO.md). Read relevant supplied lessons or a later local `LESSONS.md` through the Working protocol map.
- Verification: [build and verification guidance](docs/build-and-debug.md) and the checks required by the assigned work; select by actual risk and permission.

Read applicable AGENTS first, then the responsibility entry and task evidence. The project owner controls
path mappings; workers do not create competing STATUS/PROGRESS or relocate authority. Authorized moves update
consumers and entries. Keep this file concise and stable: durable rules and a few stable navigation pointers.
Current owners, phases, versions and provisional conclusions stay in the linked authorities; update this
entry only when responsibilities or structure change, not for each task.

## Project identity

- RimWorld prerequisite mod `coahuilite.ferritelib` (display **FerriteLib**, MPL-2.0).
  This 0.9 development line targets RimWorld 1.7 with one `1.7/Assemblies/FerriteLib.UiKit.dll`
  game carrier; Neutral is a separate build target, not a game mod payload. The 0.8/1.6 maintenance
  contract remains independent. Follow 1.7 API/namespace changes rather than pinning a game patch number.
  The library owns no consumer Defs, Patches, Languages or product textures; consumer strings stay
  with their renderer. Target guarantees and implementation evidence start at `docs/specification.md`.
- Namespace root `FerriteLib.UiKit`, kernel surface `FerriteLib.UiKit.Kernel`, log prefix
  `[FerriteLib.UiKit]` (not a Def prefix, not a packageId).
- Release/publication state (rc-only window, tag rules, hash ledger): `MEMORY.md` — never restate it here.
- Real-page validation claims follow actual wired-consumer evidence; identities and coverage are in the
  relevant `MEMORY.md` sections, and un-wired candidates are not claimed as validated.

## Purpose and non-goals

The platform-neutral declarative engine owns identity, state, invalidation, recovery and a single audit
surface over XML manifests and typed bindings. Game-specific input, drawing and resources belong to the
selected adapter; the retained Verse 1.6 path does not define the 1.7 host's API or acceptance. The library is worth what a
consumer's tree keeps inside it, not what it ships in controls. Non-goals (no uGUI backend, no reflection,
DI or codegen, no expressions in manifest XML, no Def hot-reload promise, no world-space rendering) follow
the [current public-authoring policy](docs/openspec/specs/policies/public-authoring/spec.md). The 2026-09-10
ruling that the library is referenceable by strangers and held to general-library standards remains; the
founding Charter, surveys and style-layer argument are historical in `OBLIVIONIS.md`.

## Compatibility and retirement

The promise is `docs/api-tiers.md`: **stable** types keep their signatures inside the `[min, max)` range a
consumer compiles against, **public-unstable** types are usable but expected to change shape, and
**internalize-candidate** types are public only by history. A harness lane classifies every exported type
against that file and pins the stable list, so an addition to the public surface fails until it is decided
in two places.

Stable source, already compiled consumer DLLs, XML vocabulary, legal use conditions and documented behavior
are protected within the same supported game/adapter target. After publication, breaking a supported stable contract requires a major release. The two-minor
deprecation window is an additional minimum, not permission to remove it in a minor release.
After a working replacement and deprecation,
retain the old contract through two complete subsequent published minors: deprecate in 0.9, retain through
0.10/0.11, remove no earlier than 0.12. RC/patch numbers do not count, and 1.x cannot shorten an existing
window. Deprecated XML redirects rather than silently doing nothing. Experimental entries may change in a
later minor with explicit migration; a frozen patch does not arbitrarily break them. Consumer `[min,max)`
ranges remain binding. The controlled prepublication 0.8 line may directly migrate internal users, with
old→new instructions; this is not a permanent compatibility bypass.

## The two layers

1. **Declarative page engine** — XML manifest, constrained layout, typed bindings, per-window session,
   widget registry, creation-time contract validation.
2. **Visual core** — theme tokens, drawing helpers, text measurement, fit audit, version contract;
   reachable without adopting the page model.

The split is enforced (`KernelContractTests.VerifyVisualCoreIsPageModelFree`). The current payload has one
assembly and no `FerriteLib.Core`; the 0.8 target does not predefine physical assembly count.

## Maintain complete owner boundaries

- Production callers use the existing owner for state, eligibility, commit/publication and lifetime
  results, through the real consumer. A stored/view value is not proof of runtime adoption. Legal
  backends, adapters, registry composition, independent author roots and complete synchronous fallbacks
  remain supported; the existing native funnel defines containment, not a blanket ban on native calls.
- Diagnostics follow the specific adopted [observation boundary](docs/openspec/specs/contracts/observation-boundary/spec.md).
  Do not give them business control, broadly catch business/native work, or add a caller-NoThrow condition.
- When an owner cannot express a real need, use the existing [change route](docs/openspec/README.md):
  state the need, authority/lifetime, adjacent guarantees and smallest complete adoption/exit slice.
  Internal extension, an independent module, a new core or responsibility migration are valid proposals;
  the incumbent owner supplies concrete constraints rather than an unlimited veto. Update actual callers,
  recovery/cleanup and the single current contract together.

## Invariants (stop and ask before violating)

- **Single carrier.** Only this mod ships `FerriteLib.UiKit.dll`. Two copies bind by load order through
  the game's one global `AssemblyResolve` and the losing copy cannot find out;
  `FerriteLibVersion.Require` enumerates loaded copies and its collision report is the only detector.
- **Neutrality.** No consumer's product vocabulary in the library or its harness — words
  case-insensitively, identifier prefixes case-sensitively; `coahuilite` is the series namespace,
  allowed. The guard is self-applied here with a positive control and a path-pinned self-exemption.
- **Version axes.** Contract `FerriteLibVersion.Api`, release `About/About.xml <modVersion>` and build
  csproj `VersionPrefix` agree on major.minor; the harness pins all three. Public additions after freezing
  require an explicit minor decision; a minor bump does not waive stable migration protection above.
  `AssemblyInformationalVersion` embeds committed HEAD and is never a compatibility value. The temporary
  0.7 lockstep exception is historical and never applies to 0.8 or a published state; its original scope,
  expiry and measured enforcement are retained in `MEMORY.md`.
- **Consumer API range.** Package identity and load order do not establish FL API compatibility;
  consumers retain their explicit runtime range checks. The retained 1.6 `ModRequirement` parses only
  `packageId`, `alternativePackageIds` and `displayName`. Follow the selected adapter's actual metadata
  without assuming that native fields or namespaces are identical across game versions.

- **Platform authority is adapter-specific.** The current 1.6 host uses Verse IMGUI for native input and
  compositing, not a path above the native HUD. The 0.8 target separates neutral core, host and drawing
  backend, preserving 1.6 compatibility while targeting 1.7. Core contracts and transitive dependencies
  must not expose Unity, UnityStubs, Verse, RimWorld, Waylith or Harmony types, including via object or
  reflection. Native callback counts do not define business update timing. Direct/GPU drawing seams do
  not commit to a backend, RenderTexture-only composition, assembly count or completed 1.7 support.
  Target contracts and pending delivery choices live in `docs/specification.md`.
- **Map-layer rendering is permanently outside UiKit.** `GenMapUI` and its family carry no session and
  no hit-test semantics to bind to, so world-space text (pawn-head marks and friends) is something this
  library cannot provide — a non-goal, not a backlog item. A consumer's whitelist entry for it is
  policy-backed and is not renegotiated per PR; the boundary that remains is that anything drawn into a
  window adopted into the library tree stays inside that measured contract. External authors may adopt only part of FL or own library-external logic.
- **API stabilization is not gated on a consumer count.** Define the supported contract, verify its
  behavior, and state the compatibility commitment so consumers have a dependable surface to adopt.
  Real-consumer integration is valuable validation, not a prerequisite for stabilizing that surface;
  missing integration evidence must remain explicit. This does not automatically promote existing API
  tiers or waive version, migration, or verification requirements. After a Workshop page carries the
  stable packageId, "breaking changes are expected" stops being free — the invited-vs-unsupported call
  (`TODO.md` §5) remains a maintainer decision, never a session's.

## Ecosystem protocol (how this library may grow)

A new specialized kind needs a real consumer that was forced to hand-roll the behavior, actual code, and a
generally providable shape. A request or this library's demo is not that evidence. As a clone may not have
the consumer tree, transcribe the citation into `MEMORY.md`: `owner/repo@sha:path:line`, the excerpt and what
it proved. A public permalink may accompany it, never replace it. General capabilities follow the existing
[positive change route](docs/openspec/specs/policies/public-authoring/spec.md#requirement-fl08-growth-内聚新能力有正向路线)
and are not blocked by the specialized-kind citation gate.

Consumer ladder when the library lacks something:

1. **Compose it on the tree** — own widget kind via the registry, on `UiNative` primitives and session
   axes. This is the recommended destination for anything consumer-specific; identity, state,
   invalidation, recovery and harness visibility stay with the library. A window is no longer an
   exception to this: `UiWindowHost` owns the chrome, so a page never has to leave the tree to exist.
2. **Propose promotion** — after it survives one release, carry the transcription above and the
   tree-membership metric. Outside contributors propose by issue; maintainers use the existing task
   record or a handoff when its consumer needs one.
3. **Register an exemption** — only for what structurally cannot live in a page tree, which after the
   round-1 shell is world-space rendering and nothing else currently known. Allowlisted file + written
   ruling + named closing item: rent, not exit.

Promotion gate — all four or it stays consumer-side: provenance cited; neutral, and no consumer's
numbers as library defaults; no new process-wide mutable statics; a harness-drivable lane exists.

For a maintainer-side promotion, inspect the cited consumer code at the stated revision and review each
proposed item against what it actually does. Record the evidence and per-item acceptance, correction or
refusal; do not turn the request itself into proof. Track the agreed FL-owned defects and consumer migration
under their respective owners, with compatible delivery and the existing release/retirement contract. Close
an accepted round only after its agreed work actually exists, not when a report is merely filed; while work
remains, keep its owner and next action explicit and the round open. If a numbered cross-repository round is
used, name its initiating consumer, choose the next number unused in either repository in either direction,
and preserve the proposer's item identities; resolve a collision by renumbering the later filing. Distinguish
open, reviewed, scheduled and closed. Consumer-only work that asks FL for nothing does not open a round. The original 2026-09-07 lockstep round and its rationale
remain historical in `OBLIVIONIS.md`; they are not a new compatibility exception.

**Reactive dynamic-context exception (2026-10-21).** One `[ThreadStatic]` current-runtime marker may
exist only during dynamic tracked/pure-read calls. Nested calls save the previous marker and restore it in
`finally`; the outermost exit retains nothing. It may diagnose cross-runtime reads/writes and enforce the
new pure-read contract, not retain business data/services/subscriptions, a static graph/queue/cache, or
thread scheduling authority. Unenrolled legacy calls gain no new prerequisite. This is the narrow exception
to the mutable-static rule for the reactive foundation; other ownership constraints remain.

**R9 bounded discovery exception (PM engineering decision, 2026-10-19):** live-host discovery may keep
at most 512 weak references, using the existing Host/Session identity. Successful construction registers;
Close/Dispose removes; queries may discard dead entries. No permanent strong host references, model/node
copies, business state or second identity authority are permitted. Capacity refusals and incomplete
results are explicit. This is the narrow lifecycle index needed to locate real hosts without an author
registration prerequisite; all other process-wide-static restrictions remain. See
`docs/openspec/specs/core/observation/spec.md`. The R9 read surface and budgets remain Dev candidates.

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

## Tools and consumer boundaries

- FL owns reusable UI structure, Host/Session identity, binding, layout, execution eligibility and
  lifetime. Consumers own business state and world-to-screen projection; do not move product vocabulary,
  pawn policy or audio behavior into the neutral core.
- The [developer-tools contract](docs/openspec/specs/contracts/developer-tools/spec.md) exposes bounded
  observation and owner-mediated operations. Tools owns its panels, shortcuts, protocol service and
  offline host; the library does not acquire a second debugger UI or duplicate layout/semantic tree.
- A compatible Tools connection is optional. Ordinary consumers retain their original use conditions
  when Tools is absent or disconnected; detach restores instrumentation state and releases resources.
  Keep maintenance-line Dev and legacy R9 conditions distinct from normal-carrier tools capability.

## Build and verification

Use [build and verification guidance](docs/build-and-debug.md) for executable commands, selected inputs,
output paths and the affected existing checks; do not copy release versions or gate counts into this file.
For this 0.9 line, select Neutral or RimWorld17 explicitly through
[the target/connection guide](docs/development/0.9/tools.md). The retained default net472 path and 0.8
packers are not evidence of a complete 0.9/1.7 delivery. Normal Release includes dormant FER_TOOLS;
FER_TEST is only for existing harness builds, and legacy FER_DEV instruments keep their own conditions.

- Use pwsh 7, including child processes; never invoke Windows PowerShell 5.x.
- Run the relevant existing neutral, adapter, consumer or packaging entry for the changed contract.
  Do not add unit tests/cases or equivalent renamed probes. Reuse still-applicable evidence; stub or
  headless success is not native input, drawing, game lifecycle or end-to-end acceptance.
- Builds are configuration/target-isolated. Serialize builds that share canonical harness stub outputs
  or a staging destination. Verification must not overwrite a held compatibility carrier;
  `scripts/export-carrier.ps1` is an explicit Release delivery action, not an ordinary check.
- Inspect assembly identity in a child process or a copy to avoid locking build inputs. Committed HEAD
  embedded in a DLL does not identify dirty source; a docs-only commit does not refresh an existing stage.
- Keep byte-integrity and rewrite detection where they protect a held delivery; do not add hashes for
  ordinary document reads or use hashes/mtime to prove runtime behavior.
- Retained 0.8 pack-dev/pack-release/pack-steam share the staging engine's closed file set, content/license
  checks and measured configuration. Dev permits labelled dirty source; release checks identity, Steam
  additionally requires clean source, and release archiving does not install the mod. Keep
  `PublishedFileId.txt` out of public source/rehearsal assets. The selected target's current packaging
  contract, not a legacy command copied here, determines an installable root.
- Existing published 0.8 `1.6/Assemblies/` and canonical `tools/.../Stubs/` surfaces retain their
  compatibility obligations; do not relocate them merely because another target uses a different layout.
  Consumers select an explicit supported artifact and never ship a second FerriteLib DLL.

## Repository records

The [Working protocol](MEMORY.md#working-protocol) is the sole local daily memory/review driver;
Knowledge updates cannot change it or its map. This replaces the former Memory protocol section.

- Fixed basenames are `AGENTS.md`, `MEMORY.md`, `TODO.md` and, when needed, `LESSONS.md`; maintain them
  in accurate English. `OBLIVIONIS.md` is optional cold history, never a current operating prerequisite.
- Stable engineering rules stay here; product requirements and measured implementation facts stay in
  their linked docs. Knowledge keeps useful cross-task conclusions/pointers, not a second task ledger,
  raw logs, transient artifacts, completed matrices or release checklists. Documentation edits alone
  are not durable product facts. Existing historical evidence retains its original scope.
- A handoff is not a memory tier. Use an existing record when it serves the next owner; a separate
  handoff needs an actual consumer/recovery reason and grants no authority. A maintainer-local
  `HANDOFF.md`, when used, is gitignored, not a tracked protocol target.

## Boundaries

- Default scope is this repository alone: a public checkout holds no sibling mod and no consumer tree, so
  no rule, gate, script or evidence here may require one. Another repo is read-only, and only for a
  session the maintainer names; writing one needs their authorization in that same instruction.
- Local commits are permitted. Configuring a remote, pushing, tagging, releasing or publishing to a
  registry remains an external operation and needs explicit maintainer authorization.
- No personal absolute paths, log excerpts, tokens or `PublishedFileId.txt` values in tracked files, and no
  `../<sibling>` citations either — external evidence is a transcription or a public permalink.
- A privacy review of the complete reachable range (`scripts/privacy-audit.ps1 -FullHistory`) precedes a
  push. Scope and limits: `docs/push-privacy-gate.md`. Scanner success is a privacy result, not gameplay,
  build or release acceptance.
- **No script places a mod in the game.** Nothing under `scripts/` writes outside the repository, and no
  step copies, links or junctions anything into a `Mods/` directory — a link back into the repo would bind
  build output to a machine-local layout another clone cannot see, reproduce, or (without elevation)
  create. Keep artifacts under `dist/` and say where they are.
