# P1 falsification plan — window shell / keyed instances / focus

Package P1 (owner `windowing`, task-1). Claims are taken from the task board's MUST DELIVER list and
`10-work-packages.md:11`. This plan is executed in wave B against the **merged** P1 revision.

Preconditions before any mutation is trusted: P1's new lane file(s) are registered in
`tools/FerriteLib.UiKit.Tests/Program.cs`; every new public type (`UiWindowKey`, `UiWindowCatalog`,
`UiWindowOptions`, `UiFocusPolicy`, …) has a `docs/api-tiers.md` entry added at the end of its section;
all three are in the same commit; `pwsh -NoProfile -File scripts/verify-local.ps1` exits 0 on that revision.

Run every mutation with:
```
dotnet run --no-restore --project tools/FerriteLib.UiKit.Tests -c Release
```
Expected red: `  FAIL: <lane name> - <message>` then `N test(s) failed.` and exit 1.

| # | claim | mutation that must falsify it | expected red signature | covered by the author's own lane? |
| --- | --- | --- | --- | --- |
| P1-C1 | `UiWindowKey` is a value type with value equality over (consumer, windowKind, contextKey) | compare only `Consumer` (or one field) in `Equals`/`GetHashCode` | the lane that asserts "a different context key is a different instance" / "the same key is one instance" reddens; also any dictionary-keyed reuse assertion | yes, if the lane drives both same-key and different-key paths |
| P1-C2 | reopening the same key **activates** the existing instance instead of creating a second host | delete the `TryGet` reuse branch in `UiWindowCatalog.Open` so it always constructs | instance-count / "second open returned the same host" lane reddens | yes |
| P1-C3 | two instances of the same C# `Window` type may coexist, because vanilla `Add` calls `RemoveWindowsOfType` by exact type | remove the option that sets the vanilla per-window flag for the second instance | a "two instances of one type survive a second Open" lane reddens **only if the harness stub models `Verse.WindowStack.Add` + `onlyOneOfTypeAllowed`**. At W0 the stub has `Verse.Window` (VerseStubs.cs:648) and **no `WindowStack`** and no `onlyOneOfTypeAllowed`, so this is currently untestable in-harness. | **NO — the author's lane cannot cover the vanilla-side closing rule at W0.** If P1 does not extend the stub and assert it, the claim must carry `UNVERIFIED-IN-GAME`; in-game check A1 is the fallback. |
| P1-C4 | a consumer's refusal to close is preserved | make `Close` bypass the refusal path (force-close or skip `OnCloseRequest`) | "a window that refuses to close stays open" lane reddens | yes |
| P1-C5 | each option is per-window (forcePause, preventCameraMotion, absorbInputAroundWindow, draggable, resizeable, closeOnAccept/closeOnCancel/closeOnClickedOutside, initial/normal size) | apply one option through a shared/static value so all windows inherit it | "setting the option on window A does not change window B" lane reddens | yes for the per-window independence; **NO** for the vanilla global any-true semantics of `WindowsForcePause`/`WindowsPreventCameraMotion` (not modelled by any stub) |
| P1-C6 | the library chooses no product default for pause/camera | default `ForcePause = true` (or set the vanilla global from the library) | "the library sets neither pause nor camera motion by itself" lane reddens | yes |
| P1-C7 | exactly one active target receives keyboard input; keyboard follows the click; a deactivated control stops receiving input | keep the previously active target active (never clear it) or route input to every target | "only the active target receives the key" lane reddens | yes for the in-tree routing. **NO** for the activation-combination claim itself: "does not mistake `Notify_ManuallySetFocus` for bring-to-front" cannot be tested because the stub does not model that member (`VerseStubs.cs` has no `WindowStack`, no `Notify_ManuallySetFocus`). The package must label it `UNVERIFIED-IN-GAME` with an explicit fallback. |
| P1-C8 | closing a window disposes its session and releases its subscriptions | skip the disposal on `PostClose` | "close releases the session" lane reddens | yes |
| P1-C9 | the existing failure-notice contract and chrome behaviour survive | make the shell swallow the failing pass (drop the notice path) | `KernelWindowHostTests` "the failing pass reported the exception once" reddens | yes — an existing regression guard; P1 must not weaken it |

## Minimum wave-B evidence for P1

1. Reproduce a red for **P1-C1 or P1-C2** (core identity), then revert and show green.
2. Reproduce a red for **P1-C4** or **P1-C8** (lifecycle), then revert and show green.
3. Record P1-C3 and the tail of P1-C7 as `pending` unless P1 has extended the stub; a green lane on a
   stub that cannot express the vanilla behaviour is **not** evidence for the claim.
4. Confirm all four public P1 types are present in `docs/api-tiers.md` and in the same commit as the lane
   registration (`git show --stat <merge-sha>`).
