# Build inputs, diagnostics, and development slices

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

Keep the retained page model over Verse IMGUI. Grow it through complete slices of an actual consumer's
settings redesign: one observed failure, its ownership decision, the smallest contract/fix, a failure-sensitive
test, and a real-game acceptance case. Consumer data, business actions, and page composition stay with the
consumer. FL owns element identity, coordinate/layout rules, input arbitration, state lifetime, recovery,
and the diagnostic facts emitted at those boundaries.

The next acceptance slice is a real row click inside scroll/overlay composition. Record the loaded package
hashes, window size/UI scale, language, scroll position, intended row, before/after selection, and the
corresponding geometry/input dump. Exercise repeated clicks at the same position, scrolling, and popup
occlusion. First reproduce the failure; do not change dispatch on the strength of a suspected cause.

Add clip-chain or command-dispatch tracing only if that reproduction shows the current report cannot
locate the loss. Prefer an internal diagnostic addition before a new public API. A general interactive
inspector, a new layout engine, and new widget kinds are not prerequisites for this slice.

Acceptance has separate claims: automated contracts pass; package inputs match; the real-game scenario
passes. Report each independently. A missing third claim keeps the in-game defect open even when all
automated gates are green. Stabilize documented contracts after their own behavior and compatibility
rules are established; do not use either consumer count or a hypothetical complete UI framework as a gate.
