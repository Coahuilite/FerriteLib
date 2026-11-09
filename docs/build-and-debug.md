# Build inputs, diagnostics, and development slices

This is the operational guide for the existing toolchain. **0.7.x is frozen.** These commands describe
mechanisms; running a build, export, install or publication still needs task authorization. The current product contract
is [the 0.8 specification](specification.md).

## Build once, select the input explicitly

Ordinary builds and harnesses write `dist/build/<Configuration>/FerriteLib.UiKit.dll`.
`Dev` includes `FER_DEV` instrumentation; `Release` excludes it. They no longer overwrite each other.
Harness executables and the existing canonical `tools/FerriteLib.UiKit.Tests/bin/stubs` remain in place.
Serialize harness builds because the stub outputs are still shared.

```powershell
pwsh -NoProfile -File scripts/verify-local.ps1
pwsh -NoProfile -File scripts/pack-dev.ps1
```

The second command builds Dev and stages `dist/dev/FerriteLib`. Its installable DLL is still
`1.6/Assemblies/FerriteLib.UiKit.dll` inside that folder. Existing release/steam packers read
`dist/build/Release`; NuGet output goes to `dist/package`. No additional `artifacts` directory is needed.
Staging records `payload-sha256` in `version.txt` and verifies the copied DLL before replacing the previous package.
Do not build and pack the same configuration concurrently.

### Keep Dev debug paths virtual

The retained 1.6 Dev project maps its source and resolved intermediate directories to
`/_/FerriteLib.UiKit` and `/_/FerriteLib.UiKit/obj`. This default also covers an isolated, out-of-tree
`IntermediateOutputPath`; it is evaluated after the SDK resolves that path. An explicit caller
`PathMap` is preserved and must cover both locations. Release's existing `DebugType=none` is unchanged.

Omitting the PDB from a mod root does not remove the DLL's CodeView PDB path. Before distributing a
new Dev carrier, inspect the actual DLL's CodeView entry and personal-path bytes, not just its staged
file names. The repaired fixed producer and its before/after evidence are linked from the
[implementation map](openspec/implementation.md#fl08-dev-path-delivery).

The Dev, GitHub and Steam packers share `scripts/Release.Identity.psm1` for the build axis;
GitHub/Steam and the release workflow also share the `vBASE` / `vBASE-rcN` tag grammar.
The two publication packers use the same source-commit checks. Only GitHub's explicit
`-AllowDirtyTree` local rehearsal can omit clean-tree/source-commit attribution; it still requires
a Release-configured payload without the dev suffix. Steam has no dirty-tree exception and never
archives. Shared `stage-package.ps1` retains the content, licence and measured-configuration rules.

To stage beside a held package, supply a new `-StageDir 'dist/<fresh-attempt>/FerriteLib'` to any
packer; omitted, the original channel paths are unchanged. This retained stager requires metadata
that loads its `1.6` carrier root; it cannot label a different target's bytes as a 1.6 package.
The scripts and CI retain their pwsh/.NET prerequisites, with no Node dependency.

The conditional RC route recorded on 2026-10-09 uses integrated main and the existing release workflow;
the earlier backup-only phase is historical. No documentation edit authorizes publication or refreshes a
held payload. For a bounded local rehearsal, select its named candidate rather than rebuilding by habit.

The repository-root `1.6/Assemblies/FerriteLib.UiKit.dll` is a compatibility delivery, independent of build
outputs. Existing consumers can continue reading it. Updating it is deliberate:

```powershell
dotnet build Source/FerriteLib.UiKit/FerriteLib.UiKit.csproj -c Release
pwsh -NoProfile -File scripts/export-carrier.ps1
```

Export checks the Release stamp, copies through a temporary file, removes a stale compatibility PDB, and
prints SHA-256 plus mtime. Verification and packers never invoke export. Neither export nor staging installs
anything into the game. Export copies the last built Release artifact, whose embedded source identity is
also printed; it does not assert that the artifact represents current HEAD or a clean tree. Build from the
intended revision first. Publication packers and downstream release verification retain their stricter checks.
Consumers compiling against a staged Dev DLL must install that same FL package;
they must not ship another copy of the library in their own package.
For the current1.6 step-Dev handoff, use the [instrument guide](development/0.8/dev-step.md):
switch the complete consumer/FL Dev pair, keep cost sampling separate from geometry, and select one
live Host/session for capture, independent outline and explicit reports. A Release carrier cannot be
renamed into this capability.

## Check the affected contract

For a change to the platform-neutral Kernel, this existing entry compiles and exercises the production
sources without a game or a carrier:

```powershell
dotnet run --project tools/FerriteLib.UiKit.Core.ContractTests -c Release
```

It covers neutral behavior, layout, failure/lifetime and dependency boundaries, not native input or drawing.
Exercise affected adapter, Dev/Release, consumer or package paths separately; keep the complete
`verify-local.ps1` entry for its CI and release purpose. A documentation-only edit does not by itself
invalidate unchanged code evidence.

## One owner for restore, then build and execute

The complete existing entry is scripts/verify-local.ps1. It restores three roots: the no-platform
Core, the main harness (including payload/authors/stub references), and the adapter contract executable.
The workflow no longer restores the same graphs before calling that entry. With -NoRestore, all
three roots skip restore; builds always use --no-restore and fail on missing assets rather than
silently repairing them. Use SDK10 for the net10.0 Core target and net472 reference packages.

Verification rebuilds each selected executable from current sources without incremental outputs; only
NuGet downloads are cached. Core Release, main Release and main Dev remain distinct builds. Build-only stub references preserve
the canonical bin/stubs paths without adding native doubles to compile references. Verse's graph
builds Core/IMGUI/TextRendering together; InputLegacy is its independent sibling. Main Release already
builds the payload and external-author assembly; the adapter entry compiles separately with
BuildProjectReferences=false against those same inputs. The old separate payload Release/Dev builds,
five nested stub dotnet-build commands on every harness build and dotnet-run rebuilds are removed.
The Dev reader executes the already-built Dev harness and retains its named-assertion/control checks.

These are deterministic duplicate-command reductions, not a claimed speedup. Keep separate configurations,
compile constants, dependency variants and execution processes. Final clean release identity still needs
its own build after committing; verification's -dev bytes are not the release payload.

Each invocation retains complete group logs, Dev output and summary.json under a new dist/verification
directory, or the supplied -LogDirectory. The summary records source/dirty inputs, configuration,
exit and elapsed time. Its action field contains the invoked scriptblock text, including its flags
and variable references; it is null for a BLOCKED group. retry_hint is separate recovery guidance,
not an execution record. Older command fields held that retry text; use their full logs and source
actions to establish execution. Failed dependencies block only groups that need them; independent
content/license/identity checks still run. CI uploads the complete logs even on failure. Historical
gate names remain searchable although setup/build/execute groups no longer target a fixed gate count.

NuGet download caching is shared CI practice; stale bin/obj output is not cached as proof.
The same method can serve a consumer's distinct project variants, but FL's Core, public-author,
native-stub, single-carrier and Dev-instrument assertions remain product-specific. Consumer audio,
configuration and UI-identity gates belong to that consumer, not a copied FL test platform.

## RC2 local input and later publication

The planned next prerelease is v0.8.0-rc2, non-latest, with RimWorld1.6 and basic1.7 compatibility.
The 1.7 native rendering/interaction acceptance limits below still apply. Its
[notes](release-notes/v0.8.0-rc2.md) preserve the accepted runtime contracts. A local fixed Release
input has a real local source commit, not a future merge SHA. Stage it into an unused dist directory
with the existing stage-package.ps1 and RequireReleaseIdentity; use a local preparation label.
Do not create a tag or relax ancestry checks just to invoke the formal cloud path.

Under later explicit cloud authorization:
1. Push/review the FL branch and PR; main CI and the full-history privacy gate must pass.
2. Resolve the actual final main SHA, then create v0.8.0-rc2 there. The existing ordered-tag/ancestry
   checks remain. Release CI verifies again, runs privacy, builds a clean base-version Release payload,
   stages it and publishes a CI-built prerelease asset with make_latest=false.
3. US selects that actual repo/tag/full SHA/asset and records its compiler/runtime input. Run US CI,
   or the affected existing local consumer checks for an authorized working-branch baseline. US main
   integration and its v0.6.0-rc1 publication require separate authorization; neither is part of a
   baseline-only working-branch push. Keep the exact independent Dev pair for its original uses.
4. Execute the existing post-release verification against the real asset.

The DLL's base ProductVersion and About.xml remain0.8.0; the tag/archive owns rc2 identity and the DLL
embeds source HEAD. Keeping the same suffix for rc/final tags preserves the tested-rc/final byte rule.
Neither future rc2 main SHA nor asset exists during local preparation. No rc1 or moving-branch fallback
is substituted. Publication and installation remain separate authorized operations.

## Basic 1.7 compatibility candidate

The local basic-compatibility candidate keeps the net472 payload and public API. Both 1.6 and 1.7
load the single DLL under `1.6/Assemblies`; the explicit 1.7 load-folder entry does not add a second carrier.
Its isolated fixed input and measured receipt live under `dist/rw17-basic-compat-20261030/`.
This is a local candidate whose native rendering and main interactions still await human acceptance.

The inspected 1.7.4990 rev1069 `UnityStubs` Font has no `fontNames` getter. Calling it while capturing
the metric environment threw before ordinary text measurement and caused recovery-height bands.
The adapter checks the defining assembly of its already-bound Font type once. Unity's
`UnityEngine.TextRenderingModule` keeps the original complete fingerprint; other providers return null
through the existing optional environment contract. This conservatively revokes metric reuse instead of
inventing a stable font-data revision. The Unity-only getter body is marked NoInlining so the 1.7 JIT
does not resolve its unavailable members on the common path. Normal measurement still calls Verse
CalcHeight/CalcSize, and drawing, clipping, font choice, text content and GUI-state restoration keep
their existing owners. There is no reflection-based member lookup or invocation.

The Local17 source evidence also preserves TextAnchor values and the Label/BeginGroup/EndGroup entry
points. It does not prove native rendering. Stub checks and the adapter executable remain 1.6 contract
evidence; an installed reference set from a different game version cannot stand in for 1.7 compilation.
Use the exact fixed DLL with `Private=false`, and distribute its complete FerriteLib folder separately
from a consumer. A new build from the same HEAD can include different uncommitted source, so keep the
dirty-source declaration and measured MVID with the selected package.

## What a click report establishes

The instrument remains opt-in, bounded, per-host, and observation-only. A Dev consumer enables
`host.Diagnostics.GeometryEnabled`, draws the host, and reads `DumpGeometry()` after each event pass.

- Header: pass number, input phase at host draw entry, sample counts, and dropped counts.
- Geometry: arranged, draw-local, and window-space rectangles, the coordinate offset, height mode,
  and scroll content extent.
- Input: queried element path, window-space pointer/rect, verdict, and event type before/after the query.

A `miss` with `event-before=MouseDown event-after=Used` can be native press capture before release, not a
failed command. A later `miss` with `Used` on both sides means the event was already consumed. It does not
identify an earlier consumer outside the instrumented funnel. Entry phase `Used` means the event was
consumed before host drawing; its original phase is unknown. Repaint passes must not inherit click records.
Normal overlapping content still follows native IMGUI consumption; the existing hit stack arbitrates popups.

The executable event double now uses the enum constants from the pinned game reference, changes the type
to `Used` on `Use()`, and respects group/scroll viewport clipping. The native-event lane checks consumption,
later-query visibility, and equal command counts with diagnostics on/off. These are **contract tests against
stubs**, not evidence that the real game's input stack or fonts behave identically.

Failure-sensitivity was checked by temporarily removing entry-phase sampling: the consumed-input lane
failed. Export fixtures exercise first delivery, replacement, stale-PDB removal, and refusal of Dev bytes
without changing the held delivery. These checks belong to the existing build/diagnostic gates.

## Evidence boundaries

The bounded 2026-10-09 input/popup candidate was accepted in game; see the
[0.7 verification record](development/0.7/40-verification.md). That pass is not an acceptance of every
current source path. Automated contracts, artifact identity, and real-game observations answer different
questions. Current requirements and open deviations are reached through the [0.8 specification](specification.md).
