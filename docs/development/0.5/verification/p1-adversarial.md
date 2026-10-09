# P1 adversarial verification — merged tip `688cb5d`

Verifier `verifier` (task-4). Date 2026-09-15. Target: `merge(0.5): P1 keyed window instances, focus
policy and generic XML page shell` (`7cc8b41` of `feat/0.5-windowing` @ `2b37d3d`; tip also carries my
guard `b6b621d` and the P4 report). Read from a clean `git archive` extraction in a temp directory; the
shared checkout was never mutated and every mutation was restored byte-for-byte.

```
Source/FerriteLib.UiKit/Kernel/UiFocusPolicy.cs      |  34 +
Source/FerriteLib.UiKit/Kernel/UiPageWindow.cs       | 105 ++
Source/FerriteLib.UiKit/Kernel/UiWindowCatalog.cs    | 464 ++
Source/FerriteLib.UiKit/Kernel/UiWindowHost.cs       | 228 +-
Source/FerriteLib.UiKit/Kernel/UiWindowKey.cs        |  95 ++
Source/FerriteLib.UiKit/Kernel/UiWindowOptions.cs    |  89 ++
tools/.../KernelWindowCatalogTests.cs                | 752 ++
tools/.../Stubs/VerseStub/VerseStubs.cs              | 137 +
docs/api-tiers.md                                    |  44 +-
```

## 1. Baseline

```
pwsh -NoProfile -File scripts/verify-local.ps1                       # exit 0, [setup] + 9 gates OK
dotnet run --no-restore --project tools/FerriteLib.UiKit.Tests -c Release
```

`exit=0 ok=1050 fail=0 ALL PASS`. P1 adds 102 executed checks over the P4 tip (1050 − 948). The
pre-existing shell lanes (`KernelWindowHostTests`) run inside that green, which is the regression guard for
the `UiWindowHost` identity-attribution change.

## 2. Item 1 — is the `Verse` window double faithful to 1.6.4871?

Read member-by-member out of the pinned reference assembly itself
(`Krafs.Rimworld.Ref 1.6.4871\ref\net472\Assembly-CSharp.dll` and `UnityEngine.IMGUIModule.dll`) with a
throwaway `System.Reflection.Metadata` reader — signatures decoded, attributes printed:

| double member | reference assembly (measured) | verdict |
| --- | --- | --- |
| `Window.onlyOneOfTypeAllowed` | `field Public` | faithful |
| `Window.OnCloseRequest` | `method OnCloseRequest() -> Boolean attrs=Public, Virtual` | faithful |
| `Window.resizeable` | `field Public` | faithful (the two added field initializers are explicitly declared as non-readable guesses in the stub) |
| `WindowStack.Add(Window)` | `method Add(Window) -> Void attrs=Public` | faithful |
| `WindowStack.TryRemove(Window, bool)` | `method TryRemove(Window, Boolean) -> Boolean attrs=Public` | faithful |
| `RemoveWindowsOfType` | `method RemoveWindowsOfType(Type) -> Void attrs=Private` | faithful (private in the double too) |
| `Notify_ManuallySetFocus` | `method Notify_ManuallySetFocus(Window) -> Void attrs=Public` | faithful |
| `WindowsForcePause` / `WindowsPreventCameraMotion` | `get_…() -> Boolean` public props | faithful as members; the any-true body is the plan's reading |
| `focusedWindow` | `field Private`; the double makes it public on purpose (a lane cannot read a private field of a ref-assembly type) | deliberate, documented |

**The part that is NOT verifiable here, and the double does not pretend otherwise:** a reference assembly
carries no method bodies, so nothing in the repository can show that vanilla `Add` calls
`RemoveWindowsOfType` first, or that `RemoveWindowsOfType` tests the flag on the *existing* window with
*exact* type. The stub's own header says the ordering comes from the master plan's IL note, not from a game
run. Corroborating (not proving): the reference type publishes three distinct removals —
`TryRemove(Type, bool)`, `TryRemoveAssignableFromType(Type, bool)` and `TryRemove(Window, bool)` — so
"exact type" and "assignable from" really are separate operations in vanilla, which is the distinction the
double draws.

**Does the double assert the author's conclusion back?** No, and it is load-bearing. I mutated the double's
own rule to ignore the flag —
`if (existing.onlyOneOfTypeAllowed && existing.GetType() == type)` → `if (existing.GetType() == type)` —
and the lane went red exactly as if the library had ignored the option:

```
### STUB-A window double ignores onlyOneOfTypeAllowed exit=1 ok=368 fail=7
      FAIL: both instances are open at once
      FAIL: and both are in the vanilla stack, so neither closed the other
      ...
    UNHANDLED: System.InvalidOperationException :: UiSession is disposed; create a new host/session.
```

The lane's greens depend on the double's rule, so the double is a real emulation the library must satisfy,
not a restatement of it. One residual: the harness only ever instantiates one window class
(`UiPageWindow`), so the **exact-type vs assignable-from half is unpinned** — changing the double to
`type.IsAssignableFrom(...)` would redden nothing today. That is a coverage gap in the double, not a
correctness bug in the library.

## 3. Item 2 — the mutation battery (independently run)

Helper: single-anchor byte-snapshot mutations against the extraction, identical in shape to
`turnkey-mutations.md`. Every row below is a measured run; the last run after restore was
`exit=0 ok=1050 fail=0 ALL PASS`.

| # | planted change | measured red | named FAILs |
| --- | --- | --- | --- |
| M1 dedup | `UiWindowCatalog.Open`: the `instances.TryGetValue(key, …)` early return disabled with `&& false` | `exit=1 ok=372 fail=0` + `UNHANDLED: System.ArgumentException` (duplicate key) | **none** |
| M2 option → vanilla rule | `UiWindowHost.ApplyOptions`: `onlyOneOfTypeAllowed = !options.AllowMultipleInstances;` → `= true;` | `exit=1 ok=368 fail=7` + `UNHANDLED: InvalidOperationException :: UiSession is disposed` | both-instances-open, stack holds both, per-key resolution, reopen-activates ×3 |
| M6 forced false | same anchor → `= false;` | `exit=1 ok=1047 fail=3` | the vanilla exact-type rule evicted the sibling; eviction recorded through `PostClose`; survivor in the stack |
| M3 capture release | `UiWindowHost.SetActiveTarget`: `session.ReleaseHotControl(owned.Value);` commented | `exit=1 ok=1048 fail=2` | lost target released the capture it owned; backend no longer routes pointer events to that control |
| M4 activating click | `UiWindowHost.ResolvePointerDown`: `UiNative.ConsumePointerEvent();` commented | `exit=1 ok=1048 fail=2` | activating click marked used before the page draws (×2) |
| M5 keyed chrome identity | `UiWindowHost.BuildScope`: drop the `[key]` part | `exit=1 ok=1047 fail=3` | both chrome bands report a finding; identity carries first key; identity carries second key |
| M7 focus setter | `UiWindowCatalog.SetActive`: `stack.Notify_ManuallySetFocus(window);` commented | `exit=1 ok=1049 fail=1` | activation uses the game's own focus setter rather than a private notion of focus |

All seven break a named assertion or take the run down; none stays green. Two caveats worth recording:

- **M1's natural break is an unhandled exception, not a lane.** Disabling the dedup makes `instances.Add`
  throw `ArgumentException` before any of the six dedup assertions can report. The run is red (exit 1) but
  the red signature is a crash, so the "reopening activates instead of creating" assertions are only
  reachable if the implementation returns instead of throwing. The lane is still wired; the failure mode is
  just not the clean one.
- M2 and the STUB-A mutation both produce the same 7 FAILs and then crash the runner on
  `InvalidOperationException :: UiSession is disposed`, which truncates the remaining suites in that run.
  The count (`ok=368`) is a run-length artifact, not a coverage claim.

## 4. Item 3 — the declared gaps, classified

| gap | evidence | classification |
| --- | --- | --- |
| Keyboard routing after a target change is not enforced by P1 | No keyboard path exists in `UiWindowCatalog`/`UiWindowHost`; the doc says so in `UiWindowCatalog.NotifyPointerDown`/catalog header, in `docs/api-tiers.md` `UiFocusPolicy`, and the Lead's new `40-verification.md` §3.1 carries an explicit `UNVERIFIED-IN-GAME` line for the activation combination. `Notify_ManuallySetFocus` only sets `focusedWindow` (metadata: public method, no ordering effect). | **acceptable-and-documented** (P2 scope) |
| `NotifyPointerDown` overlap resolution is heuristic | Documented in the method itself: other instances resolve against their last known `windowRect`, overlap falls back to most-recently-activated rather than draw order, "the stubs cannot model real stacking". | **acceptable-and-documented** (in-game A3) |
| `AllowMultipleInstances=false` evicts two kinds sharing one window class | The api-tiers `UiWindowOptions` entry states it ("a false is the vanilla exact-type rule, exact C# type included, so two kinds sharing one window class would evict each other"), but `30-consumer-handoff.md` §3 only says "migrate to UiWindowCatalog" and §4 does not carry the sharp edge. | **must-document** — add one sentence to the handoff's known-limits list |
| `NormalSize` has no consumer evidence | It is required by the P1 task text and implemented at `UiWindowHost.PreOpen`; no consumer, no lane beyond its own unit assertion, no measurement. It is the weakest P1 claim. | **must-document** — label it provisional/no-consumer-evidence (or drop it until a consumer asks) |
| the lane's "activating click is consumed" reads `Event.used` by reflection | Confirmed independently: the pinned reference assembly's `UnityEngine.Event` advertises `Use() -> Void` and `Internal_Use()` but **no `used`** member, so a lane cannot compile against it. The lane throws if the double stops exposing `used`, so the assertion cannot pass vacuously. | **acceptable-and-documented** (the production call is proved; composed in-game) |

## 5. Item 4 — the generic page shell, chrome identity, no regression

- **An ordinary XML page needs no bespoke C# `Window`.** `UiPageWindow` is a concrete
  `public sealed class UiPageWindow : UiWindowHost` (`UiPageWindow.cs:24`) whose constructor takes the
  key, the parsed manifest, bindings, theme, translation, metrics, an optional style document and every
  visible string; `CreateHost` builds the `UiHost` from them (`:93-99`). The lane registers a factory
  that creates it and drives it through the catalog (`KernelWindowCatalogTests.cs:113-169`,
  `harness.Register(...)` + `Catalog.Open(key)` + draws). **Claim holds.**
- **The audit scope carries the window key.** `UiWindowHost.BuildScope` (`:475-479`) is
  `GetType().Name + "[" + key + "]" + "/" + band` when a catalog attached a key, else the bare type name.
  Mutation M5 (drop the bracketed key) reddens exactly the three keyed-identity assertions, so the claim is
  mutation-proven rather than merely described.
- **The `UiWindowHost` behaviour change did not regress.** The pre-existing `KernelWindowHostTests` suite
  (chrome geometry, deferred notice contract, failure path, close affordance) runs inside the green gate-1
  run at `688cb5d`; `Key` is null outside a catalog and `IsActiveTarget` is true by default
  (`UiWindowHost.cs:51,218-222`), which is the documented pre-catalog behaviour.

## 6. Item 5 — shared-file invariants at `688cb5d`

| invariant | method | verdict |
| --- | --- | --- |
| public types vs tiers | parse `docs/api-tiers.md` as the lane does vs every committed `public` type declaration under `Source/FerriteLib.UiKit` | **67 = 67** (12 stable / 42 public-unstable / 13 internalize-candidate), zero diff both directions; P1's 5 appended at the end of Public-unstable |
| lane registration | committed `Program.cs` `X.RunAll()` names vs classes declaring `RunAll` in `*Tests.cs` | **25 = 25**, zero unregistered, zero phantom. (The Lead's message says 26; the measured count is 25 = 22 W0 + `KernelDocumentReloadTests` + `KernelLaneRegistrationTests` + `KernelWindowCatalogTests`. No missing lane — the number is just off by one.) |
| version axes | `FerriteLibVersion.Api` / `<VersionPrefix>` / `<modVersion>` | **0.5.0 / 0.5.0 / 0.5.0** |
| neutrality | gate 1 neutrality lane (green) + committed-tree grep for the second consumer's terms | one hit, and it is a regression: `40-verification.md:73` (the new A6 step) uses the second consumer's window name that F4 had removed at `85faab0`. Reported, not edited; term not reproduced here. |
| privacy | `pwsh -NoProfile -File scripts/privacy-audit.ps1` (vectors 1-2; identity by email) | **PRIVACY AUDIT CLEAN**, exit 0 |

## 7. Not verified here

- any in-game behaviour: real keyboard routing, real stacking/overlap, whether the activating click is
  consumed by IMGUI rather than by the double — all remain `UNVERIFIED-IN-GAME` (A1/A2/A3/A3b);
- the vanilla `Add`/RemoveWindowsOfType ordering and exact-type condition (no method bodies in a reference
  assembly; no game binary in this environment);
- a consumer compiling against the new window surface.

## 8. Branch note

`feat/0.5-verify` HEAD after this report; the file is the only change. P1's owner is inactive, so every
`must-document`/`must-fix` above is routed to the Lead.
