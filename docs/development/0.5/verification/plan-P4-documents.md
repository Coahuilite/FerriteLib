# P4 falsification plan — document sources / dependency tracking / atomic hot reload

Package P4 (owner `documents`, task-3). Claims from the task board's MUST DELIVER list and
`10-work-packages.md:14`. Executed in wave B against the merged P4 revision.

Preconditions: P4's lane file(s) registered in `Program.cs`; all new public types (`UiDocumentSource`,
`UiDocumentService`, `UiReloadReport`, …) in `docs/api-tiers.md`; same commit; gate suite green.

Run: `dotnet run --no-restore --project tools/FerriteLib.UiKit.Tests -c Release`.
Expected red: `  FAIL: <lane name> - <message>`, then `N test(s) failed.`, exit 1.

| # | claim | mutation that must falsify it | expected red signature | covered by the author's own lane? |
| --- | --- | --- | --- | --- |
| P4-C1 | `UiDocumentSource` is data handed in by the consumer; nothing scans a directory | add `Directory.GetFiles`/enumerate over a folder in the document service | a lane asserting the source list is exactly what was handed in reddens; if the scan lands in a non-funnel file, the containment aspect is reported separately | yes if the lane pins the source set |
| P4-C2 | a candidate is parsed **and fully validated** before the current tree is swapped | delete the validation call on the load path | "a malformed candidate is rejected and the previous tree still draws" lane reddens | yes |
| P4-C3 | batch atomicity: validate every affected window first; any failure keeps the whole batch on the old version | commit each window as soon as it validates | "(b) one of two affected windows fails ⇒ both keep the old version" lane reddens | yes |
| P4-C4 | a content/version identity prevents duplicate commits for the same bytes | remove the identity check | "loading the same bytes twice commits once" lane reddens | yes |
| P4-C5 | last-known-good: first load falls back to the embedded resource; a later failure keeps the last good version | on failure clear/replace the current tree | "(c) a failed candidate leaves the previous tree drawing" lane reddens | yes |
| P4-C6 | first-failure and repeated-failure reporting is deduplicated per failed version | report on every retry | "the same failed version reports once" lane reddens | yes |
| P4-C7 | dependency tracking: a change to one layout updates only the hosts that depend on it | broadcast every change to all hosts | "(a) an edited file updates the affected host while an unrelated host is untouched" lane reddens | yes |
| P4-C8 | the file watcher only posts a change signal and never touches Verse/Unity or the business model | add one `Verse`/`UnityEngine` call to the watcher class | `KernelContainmentTests` reddens, naming the new call site (the containment gate is the enforcement) | yes — but note `KernelContainmentTests.cs` is Lead-owned: a needed allowlist entry is a request, not a silent edit |
| P4-C9 | state preservation: stable id/key + compatible kind keep scroll/selection/expansion/drafts; a removed or kind-changed element cleans its old state; reload does not commit drafts, replay commands or reset the model | rebuild the affected session state on reload | "draft/scroll/selection survive a compatible reload" lane reddens; a "removed element's state is gone" second lane pins the other half | yes |
| P4-C10 | active drag / text editing: the commit is deferred or the capture cancelled, and no global hot control is left held | commit while a capture is held without releasing it | `Session.OwnedHotControl` / `IsHotControlOwned` assertion reddens | yes for the session-side capture; **NO** for a real IMGUI hot control surviving a live pass (needs the event-pump lane with group origins) |
| P4-C11 | a dropped/coalesced watcher notification is recoverable through the manual reload entry point | drop the coalescing queue and never commit; then call manual reload | "manual reload recovers after the file is fixed" lane reddens only if it is exercised through the coalescing path, not a direct parse | partial — require the lane to drive the watcher signal path |

## At least one claim the author's lane does NOT cover

- **The watcher really runs off-thread.** If the lane drives the watcher by calling a synchronous fake,
  "the worker thread only posts signals" is untested. Independent test: start the real watcher, touch the
  file from a second thread, and assert (i) the commit lands on the main-thread boundary, and (ii) no
  backend/business object is touched from the watcher thread.
- **Dropped-notification recovery.** A lane that only ever lets the watcher succeed cannot show that a
  dropped signal is recoverable; force the drop and require the manual path to recover.
- **No second coordinator.** The deliverable forbids a second state-coordination mechanism around
  `UiSession`/`UiLayoutEngine`. Independent check: `git grep -n "ContentRevision\|LayoutRevision"` on the
  merged revision — a new global counter introduced by P4 is a finding.

## Minimum wave-B evidence for P4

The task board asks for at least two planted-failure mutations; this plan requires three: **P4-C3**
(batch atomicity), **P4-C5** (LKG), and **P4-C4** (duplicate-commit identity). Each: red output + revert +
green. Plus the watcher-thread and dropped-notification checks above.
