# Independent verification surface (P6)

Owner: `verifier` (task-4). This directory is the adversary's working surface; it is **not** a second
status source. The round's live status stays in `10-work-packages.md` and `40-verification.md`.

## Rules this directory follows

- A PASS is recorded only with the exact command, the revision it ran on, and its evidence class:
  **mutation-proven** (the behaviour was broken, a lane went red, the break was reverted and the lane went
  green again) vs **regression guard** (green on the tested revision, never observed red).
- A claim this environment cannot exercise is recorded as `pending` / `UNVERIFIED-IN-GAME` with the reason.
- Nothing here edits a claim to make it true. A statement the tree contradicts is reported as FAILED with
  `file:line` on both sides, and the owning file is left alone.
- Nothing under `Source/**` or `scripts/**` is edited from here; the package owner fixes its own tree.

## Files

| file | content |
| --- | --- |
| [wave-A-audit.md](wave-A-audit.md) | W0 contract-doc audit against the committed tree + the two institutional traps |
| [plan-P1-windows.md](plan-P1-windows.md) | falsification plan for P1 (window shell / keyed instances / focus) |
| [plan-P2-bindings.md](plan-P2-bindings.md) | falsification plan for P2 (bindings announce / invalidation / commands / visibility) |
| [plan-P4-documents.md](plan-P4-documents.md) | falsification plan for P4 (document sources / hot reload) |
| [plan-P5-diagnostics.md](plan-P5-diagnostics.md) | falsification plan for P5 (per-host diagnostics) |
| [turnkey-mutations.md](turnkey-mutations.md) | the helper + copy-pasteable mutation recipes per claim (P4's are measured) |
| [p4-adversarial.md](p4-adversarial.md) | P4 adversarial pass at `85faab0`: mutation counts, gap probes, invariant verdicts |
| [p1-adversarial.md](p1-adversarial.md) | P1 adversarial pass at `688cb5d`: stub faithfulness, 7-mutation battery, gap classifications, invariants |
| [p4b-adversarial.md](p4b-adversarial.md) | P4b adversarial pass at `6e0781a`: 4 mutations confirmed, seam vacuity probe, baseline-gate residual, invariants |
| [p2-adversarial.md](p2-adversarial.md) | P2/P2b adversarial pass at `0ee55b6`: 6/7 mutation counts confirmed, a2 redundancy judged, borrow check, gap classifications |
| [p4c-adversarial.md](p4c-adversarial.md) | P4c adversarial pass at `9078812`: both vacuity mutations confirmed (6/8), first-load fallback measured, adjudication judged |
| [final-integrated.md](final-integrated.md) | Final round at `1769e37`: P3/P5 batteries, cross-package lanes, invariant gate, merge-resolution checks |
| [post-fix-verification.md](post-fix-verification.md) | Post-fix round at `0d1f38b`: external probe, invariants, R1-R7 lane sweep and adversarial probes |

## How wave B falsifies a claim

1. Take the merged revision of the package branch. Mutations are applied to a **disposable copy** of that
   revision (a scratch worktree); never to the shared checkout, and never committed. Revert immediately
   after reading the red output.
2. Targeted run: `dotnet run --no-restore --project tools/FerriteLib.UiKit.Tests -c Release`
   (the harness prints `ALL PASS` and exits 0, or `N test(s) failed.` on stderr and exits 1).
   Full run: `pwsh -NoProfile -File scripts/verify-local.ps1`.
3. Expected red signature: one or more `  FAIL: <lane name> - <message>` lines, then
   `N test(s) failed.`, exit code 1. A mutation that leaves every lane green means that lane is not wired.
4. Revert, re-run: green again ⇒ mutation-proven. A lane only ever seen green is a regression guard and is
   labelled as one.
5. One mutation = one small behaviour break. No refactor, no drive-by cleanup, no change to a file the
   verifier does not own.
6. `docs/api-tiers.md` and `tools/.../Program.cs` are Lead/owner append-only surfaces: a public type, its
   tier line and its lane registration must be present in the **same commit** as the package; if any of the
   three is missing, that is a FAILED finding, not a formatting nit.
