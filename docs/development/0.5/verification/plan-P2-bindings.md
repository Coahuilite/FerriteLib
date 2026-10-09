# P2 falsification plan — bindings announce / invalidation / commands / visibility

Package P2 (owner `core`, task-2). Claims from the task board's MUST DELIVER list and
`10-work-packages.md:12`. Executed in wave B against the merged P2 revision.

Preconditions: P2's lane file(s) registered in `Program.cs`; all new public types (`UiInvalidation`,
new `IUiBindings` members, …) in `docs/api-tiers.md`; same commit; gate suite green on the revision.

Run: `dotnet run --no-restore --project tools/FerriteLib.UiKit.Tests -c Release`.
Expected red: `  FAIL: <lane name> - <message>`, then `N test(s) failed.`, exit 1.

| # | claim | mutation that must falsify it | expected red signature | covered by the author's own lane? |
| --- | --- | --- | --- | --- |
| P2-C1 | a per-key revision replaces the single global `ContentRevision`; notifying key A does not invalidate element B | make `NotifyChanged(key)` bump one global counter and mark every key dirty | "only the notified key's element re-measures" lane reddens | yes |
| P2-C2 | notifications raised during a frame coalesce into one commit at a safe boundary; the tree is not swapped mid-GUI-pass | commit synchronously inside `NotifyChanged` | "two notices in one frame produce one commit" lane reddens (commit-count assertion) | yes for the count |
| P2-C3 | duplicate notifications for the same key do not multiply work | drop the per-key dedup set | "N duplicate notices still do one unit of work" lane reddens | yes |
| P2-C4a | a font/size/spacing/structure change re-measures the affected element(s) | classify `Structure` as `Paint` | "a size change re-arranges" lane reddens | yes |
| P2-C4b | a fixed-size readout changing does not trigger whole-page measurement | classify a value-only change as `Measure` | "a value change on a fixed-size readout re-measures only itself / not the page" lane reddens | yes |
| P2-C5a | a command with `canExecute == false` does not execute | execute regardless of the predicate | "a disabled command does not run" lane reddens | yes |
| P2-C5b | a disabled element does not capture hot control / pointer | let the disabled path still call capture | "a disabled element captures nothing" lane reddens | yes |
| P2-C5c | disabled treatment comes through the existing tone/writability funnel, not a second decision path | bypass `UiWritabilityTests`' decision path and paint a bespoke state | "the disabled treatment is the writability decision" lane reddens | yes |
| P2-C6a | hiding/showing does not renumber stable identity (`UiNodeId` uses the declared index) | assign indices after skipping hidden siblings | "hide A, then B's identity is unchanged" lane reddens | yes |
| P2-C6b | hiding must not discard unrelated session state | clear the node's state (or the session) on hide | "draft/scroll of another element survives a visibility toggle" lane reddens | yes |
| P2-C7 | `UiValueState`, popup ownership, scroll state and the recovery surface keep working | any of the above breaks them accidentally | the pre-existing lanes (`KernelSessionTests`, `KernelPopupTests`, `KernelWritabilityTests`) redden | yes — regression guard for P2 |

## At least one claim the author's lane does NOT cover

- **Reentrancy / boundary safety.** A notification raised *from inside the draw pass* (a widget callback)
  must be deferred to the next safe boundary and must not mutate the tree during the pass. A lane that
  only notifies between frames cannot distinguish "coalesced at the boundary" from "committed whenever".
  Independent test: drive one frame, raise a notice from inside a widget's Draw, assert the pass completes
  with the old tree and the commit lands on the next boundary.
- **Retirement, not just disuse.** The deliverable says the source-text assertions that police the global
  counter are *replaced*. A green lane with the old assertions still present is a failure of the claim.
  Independent check: `git grep -n ContentRevision` on the merged revision — the global counter must be gone
  from `UiSession`/its guard lanes, or the lane must be shown to assert the per-key revision instead.

## Minimum wave-B evidence for P2

Three mutations, exactly as the task board demands: P2-C1 (targeted invalidation), P2-C5a (disabled command
still executes), P2-C6a (hiding renumbers identity). Each: red output + revert + green. Plus the two
uncovered checks above.
