# What was verified, and what was not

Round 0.6. Every claim below names its evidence class (see `README.md`). **No item in this round reaches
已由真实消费者接入 or 已实机验证.** The final tip and the staged package are recorded in `50-dev-package.md`;
the records this file summarises are all under `verification/`.

## 1. What was run

| Run | Command | Result |
| --- | --- | --- |
| Local gates (10) | `pwsh -NoProfile -File scripts/verify-local.ps1` | all checks passed — after every merge and at the frozen tip |
| Harness (direct) | `dotnet run --no-restore --project tools/FerriteLib.UiKit.Tests -c Release` | **1853 ok / 0 FAIL / ALL PASS** at the pre-doc tip, 36 lanes |
| Privacy | `pwsh -NoProfile -File scripts/privacy-audit.ps1` | CLEAN (vectors 1+2; vector 3 needs `-FullHistory` and is not run in-loop) |
| Dev package | `pwsh -NoProfile -File scripts/verify-local.ps1 -PackDev` | five-file staged folder; hashes in `50-dev-package.md` |
| External probe | `dotnet run --project dist/probe-0.6/Probe.csproj -c Release` | exit 0, 0 unexpected throws, against the **packaged** DLL (gitignored probe, never committed) |
| Demo build + probe | demo repo: `dotnet build … -c Release`, `dotnet run --project tools/DemoProbe -c Release`, `pack.ps1` | build 0 warnings/0 errors; probe PASS 10/10; 8-file package, no FL DLL, no absolute path |

The privacy audit failed once, on the Lead's own T0 document: a machine-absolute drive path in the T4 scope
line of `10-work-packages.md`. Fixed in `4a9c6b9`, re-run CLEAN. It is recorded rather than quietly
corrected because the gate is what caught it, and because it is the one class of failure a round can ship
without noticing.

## 2. Per package

### T0 — contract (Lead)
Skeletons compiled, tier entries and the three version axes landed together, ten gates green at `02a6aea`.
The four `NotImplementedException` markers that named their owning package are **all gone** at the frozen tip
(`grep -rn NotImplementedException Source/` is empty) — that grep is the round's own delivery condition and
the independent verifier re-ran it.

### T1 — page lifecycle and the adapter (`mvvm`, task-24)
20 planted defects, each red on its named assertion, each reverted; record `verification/t1-mvvm-lanes.md`.
The T0 skeleton had real defects the implementer found and fixed: a per-key flush made one property three
writes; a dropped inner batch handle could strand the pending set; a refused `Map` could still replace the
live mapping; a stale handle could release a newer subscription; a throwing `HostDetached` skipped
`Dispose`.

**The independent verifier (task-28, `t5a-independent-verification.md`) then found a hole and it was
closed:** re-announcing `HostAttached` from the activation path left the whole harness green, because no lane
both subscribed to the event and re-activated a window after its first draw. T1b added that lane with two
named mutations (`t1-mvvm-lanes.md` §2a). Product behaviour was already correct; the lane is a regression
guard, and it is described as one.

**Honest status of two T1 claims: future-regression guards, not mutation-proven.** "The uncommitted draft
survives a reload" and "a reload runs no command" cannot be reddened from inside T1's files — the verifier
measured that the R1 defect in `UiHost` reddens 4 document/scheduler assertions and **zero** T1 ones, and
that even a session-destroying host defect leaves them failing only as an unnamed lane-level
`NullReferenceException`. Their mutation-proven owner is `KernelDocumentReloadTests`.

### T2 — reload scheduling (`reload`, task-25)
11 planted defects (quiet period, trailing edge, bounded retry, zero-host, deferral ceiling, manual channel,
clock seam, per-pump batch, R1 rollback, deferred-vs-pending), each red; record
`verification/t2-reload-scheduling.md`, which also carries the **vanilla commit-point evidence** transcribed
with `file:line` and the honest statement of what the ordering does not prove.

Two adaptations were approved and are recorded rather than hidden: the P4 and P5 lanes became clock-driven
(24 lanes/175 check sites and 15/92 before and after, **identical counts**, four check *call forms* changed
with byte-identical messages), and `VerifyHotControlReleased` now reaches its commit through the deferral
ceiling.

The verifier measured the "one read" proxy's blind spot: two real reads with one report reddens only the R7
lexical assertion, and the same second read written as a bare `ReadAllBytes(path)` under
`using static System.IO.File;` reddens **nothing**. That is the round's most precisely stated uncovered
shape.

### T3 — registry description (`catalog`/`collections`, task-26)
Nine planted defects across eight requirements, all red; record `verification/t3-catalogue-lanes.md`,
including a **declared boundary** the verifier reviewed and agreed with: `Register` copies a caller-supplied
schema collection under the registry lock and `Resolve` calls the factory under it — pre-existing public
behaviour, not this round's to change. The round's narrower invariant (a listing path never exposes a writable
registry, a factory or a consumer callback under the lock) is laned and green.

### T4 — the demo mod (`demo`/`windowing`, task-27)
Build 0 warnings/0 errors; its own probe 10/10 including a scope-identity check that the old
`KnownKinds`-per-scope fallback structurally could not express; two mutations red on the matching probe
checks. Package: exactly eight files, no `FerriteLib.UiKit.dll`, no drive-letter path in any tracked file.
The catalogue path was re-verified against a **re-staged carrier** after T3 landed, and the probe now *fails*
if the fallback is taken while the snapshot is implemented.

**Its first build used a stale carrier** (staged before T3), which is exactly the failure mode a handoff has
to prevent: the demo reported it instead of adjusting the probe, and the Lead re-staged. Recorded here because
"the consumer built" is not a claim about which bytes it built against.

### T5 — outside checks
`probe`/`documents` (task-29): the external probe over the packaged DLL (`t5b-2`, `t5b-3`) exercises the
catalogue, the scheduler with an injected clock, and the adapter; the demo package and the demo→carrier link
were checked independently of the demo's own `pack.ps1`; the ten-row in-game operator checklist lives in
`t5b-3` marked 尚待实机 row by row.

## 3. Unfinished, and who owns it

1. **In-game acceptance — 尚待外部团队验证.** Operator steps: `verification/t5b-3-demo-package-link-and-in-game-checklist.md`.
   Nothing in this round may be recorded as 实机.
2. **A real consumer compiling against `[0.6.0,0.7.0)` — 尚待外部团队验证.** Handoff:
   `30-consumer-handoff.md`. The demo is not that consumer.
3. **`0.5.x`'s own in-game acceptance (A1–A11) and its consumer compile remain open** and were not closed
   by this round; `0.5.x` is untouched at `354d90a`.
4. **Uncovered shapes, named:** the bare-`ReadAllBytes` second read (T2/T5a); sibling-window event atomicity,
   `GUILayout` Layout/Repaint consistency and consumer `Pre/PostClose` re-entrancy (T2); IME composition,
   bounded only by `MaxDeferSeconds` (T2); no concurrency-stress lane on the registry (T3); the T1
   draft/no-command claims are guards (T1); `HostAttached`/`HostDetached` cannot be fired from an outside
   process, so the external probe does not cover the lifecycle door (T5b).
5. **Follow-ups filed, not fixed:** a dropdown bound with `BindReadOnly<IReadOnlyList<T>>` reports the wrong
   cause at creation (`TODO.md`, 0.6 section); the XML vocabulary traps a real integrator hit
   (`20-api-and-xml.md` §5).

## 4. Final integrated pass

The independent verifier's last pass (`verification/t5c-final-integrated.md`, bound to `e246aa4`) found
**nothing**. It reproduced the ten gates and `1853 ok / 0 FAIL` across 36 lanes with no lane skipped;
`privacy-audit.ps1` CLEAN; `grep NotImplementedException Source/` empty; the dev package and the demo
package re-checked file by file with matching hashes; and it reconciled the package's
`commit=f3595a1b28f1` label against a tip that is documentation-commits ahead
(`git diff --name-status f3595a1 e246aa4` = the package document alone).

**The one hole it had found is closed and named.** Re-planting its own attack (re-announcing
`HostAttached` from `UiWindowHost.SetActiveTarget`) now produces three **named** failures —
`ACTIVATION-NO-SECOND-ANNOUNCEMENT`, "drawing the reactivated window announces nothing more", "the
announcement count is still exactly one after two full activation cycles" — where the same attack left the
whole harness green at `84537fc` before T1b.

It also reported two non-defects so they cannot be mistaken for findings: `privacy-audit.ps1` false-FAILs
when run outside a git work-tree (it cannot find the account), and `dist/dev/` still holds a previous
round's gitignored `FerriteLib-0.4.0-dev.zip` beside this round's staged folder.

## 5. Regression of the previous round

R1 (rolled-back batch keeps the draft) and R7 (single-read document pipeline) were re-planted at the merged
tip by the verifier and by the Lead's own T5a-1 pass: R1 reddens 4 assertions, R7 reddens its single-read
assertion. Both still hold; neither was weakened by the T2 lane adaptation (identical assertion counts,
diff-checked call forms only). The other R2–R6 fixes carry their 0.5 lanes unchanged and are re-run by gate 1
on every round run.
