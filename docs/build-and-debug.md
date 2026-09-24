# Build inputs, diagnostics, and development slices

This is the operational guide. Development priorities, slice order, ownership decisions, and completion
criteria live in the [next-stage development handbook](development/0.7/next-stage-guide-zh.md).

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

**This phase runs the dev channel only (maintainer ruling 2026-09-24).** `pack-release` and `pack-steam` are not
run, no tag and no release are created, and the remote is an **off-site backup**. Local testing uses the Dev
package. The release/steam packers and their stricter checks stay in the tree for the published line they belong
to; they are not an action of this phase.

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

## Development direction

Follow the [next-stage handbook](development/0.7/next-stage-guide-zh.md) and the active queue in `TODO.md`.
The immediate game scenario remains row selection inside scroll/overlay composition. Automated contracts,
package identity, and real-game acceptance are separate claims; the in-game defect remains open until its
own real-game acceptance passes.
