# 0.6 work packages — completed-round index

The old shared task board, worktree assignments and merge order are no longer active. This file preserves
the T identifiers for reading the line's contracts and evidence; it is not a dispatch plan.

| Package | Delivered concern | Contract / evidence |
| --- | --- | --- |
| T0 | 0.6 baseline, version axes and public contract | [Baseline](00-baseline.md), [contract](05-api-contract.md) |
| T1 | `UiNotifyAdapter`, bounded mapping/batching and lifecycle subscription ownership | [T1 lanes](verification/t1-mvvm-lanes.md) |
| T2 | quiet-period/retry/defer scheduling, main-thread commit and newest-valid content on reopen | [T2 record](verification/t2-reload-scheduling.md) |
| T3 | catalog snapshots preserving scope identity without invoking factories | [T3 lanes](verification/t3-catalogue-lanes.md) |
| T4 | independent public-API demo, sample composition and two-window/reload exercises | [Demo package and operator checklist](verification/t5b-3-demo-package-link-and-in-game-checklist.md) |
| T5 | independent attacks, outside public-API probe and integrated verification | [Verification summary](40-verification.md), [R06 disposition](60-review-r06.md) |

The API and usage documents own actual behavior: map replacement and unmapped property handling;
main-thread notification refusal; idempotent unsubscribe without disposing the model; host attach/detach
ordering; reload without recreating the model or subscriptions; bounded scheduling independent of game
simulation time; per-document rather than global atomicity; and copied catalog snapshots with no factory
calls. Do not reconstruct these contracts from an obsolete task brief.

The separately owned demo was checked through its public-API/package boundary; this repository does not
require a sibling checkout to build or read its documents. Historical tests say which inputs they observed.
Final R06 fixes supersede the earlier green candidate where the independent review found a counterexample.
