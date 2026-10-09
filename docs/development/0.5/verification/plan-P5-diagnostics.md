# P5 falsification plan — per-host / per-session diagnostics isolation

Package P5 (owner `diagnostics`). Claims from the task board's MUST DELIVER list and
`10-work-packages.md:15`. Executed in wave B against the merged P5 revision.

Preconditions: P5's lane file(s) registered in `Program.cs`; all new public types (`UiDiagnosticHub`,
`UiDiagnosticEvent`, …) in `docs/api-tiers.md`; same commit; gate suite green.

Run: `dotnet run --no-restore --project tools/FerriteLib.UiKit.Tests -c Release`.
Expected red: `  FAIL: <lane name> - <message>`, then `N test(s) failed.`, exit 1.

| # | claim | mutation that must falsify it | expected red signature | covered by the author's own lane? |
| --- | --- | --- | --- | --- |
| P5-C1 | every event carries host/session identity (and, where applicable, window/node) | emit events through the old shared single slot / drop the session id | "two hosts coexist, each event is attributed to its own host" lane reddens | yes |
| P5-C2 | a subscription is released when its host closes, and closing one host does not stop the other | make `Dispose` clear **all** sinks instead of only its own | "close A, B keeps reporting" lane reddens | yes |
| P5-C3 | events are bounded (quota) and deduplicated per source, independently per host | remove the quota and/or the dedup | "N identical events produce one entry" and "the quota is per host" lanes redden | yes |
| P5-C4 | the event kinds are reload / fit / recovery, with timing | drop one kind (e.g. never emit recovery) | the lane for that kind reddens | yes |
| P5-C5 | no unbounded process-wide mutable static cache and no cross-game references | add a static dictionary caching host/session objects | a "no mutable static of a host/session type" lane / the containment review reddens; if no such lane exists, the claim is unproven | **partially** — a static-field assertion is easy to write and P5 should ship it; the cross-game-reference half is only provable in game (A9) |
| P5-C6 | the hub replaces the shared single-slot sink without breaking the existing fit-audit surface | route fit events through both the old slot and the hub | pre-existing `KernelTextAuditTests` either redden or pass; if they stay green while the old slot is still the one the audit reads, the replacement claim is not proven | yes if the old slot is deleted in the same commit |

## At least one claim the author's lane does NOT cover

- **Real host destruction across a save change.** The task requires "closing one window / loading another
  save / target destroyed ⇒ subscriptions cancelled, no old Pawn/task reference carried into the new game".
  The harness can only close and reopen a host object; the cross-game half is in-game check A9 and must be
  labelled `pending`, not implied by a green lane.
- **Boundedness under interleaved hosts.** A lane that drives one host at a time cannot show the quotas and
  dedup tables are per host rather than per process; require one lane that interleaves two hosts and checks
  each host's own count.
- **No second sink.** Independent check: `git grep -n "static" <new diagnostic files>` and
  `git grep -n "UiFitAudit\.\(Report\|Attach\)" ` — a surviving process-wide slot next to the hub is a
  finding.

## Minimum wave-B evidence for P5

Two mutations: **P5-C1** (attribution) and **P5-C3** (bounded/dedup per host). Each: red output + revert +
green. Plus the interleaved-hosts check.
