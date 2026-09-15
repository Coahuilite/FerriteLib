# P2 / P2b adversarial verification — merged tip `0ee55b6`

Verifier `verifier` (task-4). Date 2026-09-15. Target: `merge(0.5): P2 per-key notification,
invalidation classes, command state and visibility; P2b node prune`. Read from a clean `git archive`
extraction in a temp directory; the shared checkout was never mutated; every mutation restored
byte-for-byte with a `BASELINE`/`FINAL-RESTORED` assert at `exit=0 ok=1173 fail=0`.

## 1. Baseline

```
dotnet restore tools/FerriteLib.UiKit.Tests/FerriteLib.UiKit.Tests.csproj
pwsh -NoProfile -File scripts/verify-local.ps1          # exit 0, [setup] + 9 gates OK
dotnet run --no-restore --project tools/FerriteLib.UiKit.Tests -c Release
```

`exit=0 ok=1173 fail=0 ALL PASS`. P2 adds 101 executed checks over P4b's 1072.

## 2. Item 1 — the author's mutations, independently run

| # | planted change | measured | author | verdict |
| --- | --- | --- | --- | --- |
| a1 | `UiLayoutEngine`: ignore the declared class — every notification read as Measure/Structure | `exit=1 ok=1170 fail=3` | 3 | **CONFIRMED** |
| a2 | remove only the per-node `MarkDirty()` calls, keep the clock bump | `exit=0 ok=1173 fail=0` | ALL PASS | **CONFIRMED** |
| a3 | every notification read as `Paint` (the whole per-key action becomes a no-op) | `exit=1 ok=1160 fail=13` | 13 | **CONFIRMED** |
| b1 | drop `if (!CanRun(descriptor!)) return;` in `UiBindings.Invoke` | `exit=1 ok=1170 fail=3` | 3 | **CONFIRMED** |
| b2 | drop `if (IsInputDisabled(element)) return false;` in `UiNative.Button(rect, ctx)` | `exit=1 ok=1172 fail=1` | 1 | **CONFIRMED** |
| c1 | `VisibleChildren`: index the slot by visible position instead of declared index | `exit=1 ok=1169 fail=4` | 4 | **CONFIRMED** |
| c2 | `PruneNodesExcept`: prune on `!node.IsArranged` instead of "not in the declared set" | `exit=1 ok=1166 fail=**7**` | 6 | **REFUTED by one** — the break is caught, but seven assertions redden, not six |

Raw signatures (a1 and c2 shown; the rest are in the run above):

```
### a1 ignore declared class exit=1 ok=1170 fail=3
    FAIL: a Paint-class key reuses the arrangement: a fixed-size readout must not re-measure the page
    FAIL: and a Paint-class announcement must not move the arrangement clock
    FAIL: and the batch moved the arrangement clock exactly one step
### a2 remove per-node flags only exit=0 ok=1173 fail=0
### a3 all-Paint exit=1 ok=1160 fail=13
### c2 prune anything not arranged exit=1 ok=1166 fail=7
    FAIL: a Tab switch does not renumber the visible sibling :: the Tab-hidden sibling lost its state slot
    FAIL: a hidden element keeps its node - it is not arranged, but it is still declared
    FAIL: GetNodeByElementId answers by identity rather than by arrangement, ...
    FAIL: and its draft survives the hide
    FAIL: showing it again resumes on the same node with the same state
    FAIL: a declared but hidden identity keeps its node and its state ...
    FAIL: including a hidden one the new definition still declares
```

The c2 delta is almost certainly mutation breadth: my edit prunes every non-arranged node in one place,
which also releases the `Tab`-hidden sibling, so one extra assertion in the invalidation lane reddens. The
claim "the prune distinguishes removed from merely hidden" is mutation-proven either way.

## 3. Item 2 — is a2's redundancy real, and is the paint claim proven for the right reason?

**Yes to both, with one wording correction.**

The engine's arrangement cache is keyed by the page-wide clock plus the dirty set:
`UiLayoutEngine.cs:133` reuses the cached snapshot only when
`cachedContentRevision == ctx.Session.ContentRevision && !ctx.Session.HasDirtyNodes`. The per-key commit
sets both mechanisms under the same predicate: for a Measure/Structure notification it calls
`node.MarkDirty()` (and the parent for Structure) **and** `ctx.Session.BumpContentRevision()`. So
removing the node flags changes nothing observable — the clock alone forces the re-arrange. a2's
`exit=0 ok=1173 fail=0` is a correct self-refutation, not a lane weakness: the flags are redundant with
the clock *today*.

The Paint claim is proven for the right reason. A Paint-class notification hits the
`(invalidates & (Measure | Structure)) == 0` branch and `continue`s before either mechanism, so no node is
marked and the clock does not move; the lane's paired control (the same page with a Measure key) shows a
new snapshot and `clock + 1`. Mutation a1 (force everything to Measure/Structure) reddens exactly the two
Paint assertions plus the clock assertion, so the contrast is wired, not incidental.

**Correction:** the api-tiers wording "`Measure` re-measures the elements that declared the key" is
stronger than the mechanism. There is no element-level re-measure: a Measure announcement marks the
declaring nodes dirty and bumps a page-wide clock, and the page re-arranges as a unit. What is genuinely
per-key is the *targeting of announcements* (a key no element declares invalidates nothing — pinned by
`VerifyUndeclaredKeyTargetsNothing`). Recommend: (i) reword the tier entry to "marks the elements that
declared the key and re-arranges the page", and (ii) either delete the currently-dead
`MarkDirty`/`HasDirtyNodes` path or pin it with a lane that dirties a node directly and expects a
re-arrange — right now it is a latent mechanism nothing exercises.

## 4. Item 3 — the declared gaps

| gap | evidence | classification |
| --- | --- | --- |
| `ContentRevision`/`BumpContentRevision` still exist | Deliberate and documented in three places: `api-tiers.md`'s `UiSession` entry demotes them to "the engine's arrangement-cache clock", and the 0.5 "Landed breaking changes" section states a consumer must not use them and that a Paint-class announcement must not move the clock. The a1/a3 mutations show the clock is the real arrangement unit. My plan-P2 check B (the counter is gone) is satisfied by contract, not deletion — that is honest given the docs, but see the wording correction in §3. | **acceptable-and-documented** |
| `UiNative.Slider`/`NumberField`/`TextField` do not consult the disabled flag | Only `Button(rect, ctx)` and `DropdownButton` call `IsInputDisabled`. `ResolveDisabled` disables an element only when it carries an `ActionBind` whose `CanExecute` is false, so this is reachable for any interactive element the author command-binds. A disabled slider/field would still take input. | **must-document** (and consider routing the remaining primitives through `IsInputDisabled`) |
| a consumer kind hand-rolling drag bypasses the funnel guard | The guard lives in the funnel primitives; raw IMGUI in a consumer widget is outside the tree and outside every guard by design (`AGENTS.md`: raw IMGUI is unsupported and unmeasurable). | **acceptable-and-documented** (add one line to the consumer handoff) |
| "announce before the element exists" | A notification for a key no arranged element declares is recorded and then does nothing (`VerifyUndeclaredKeyTargetsNothing` pins it: no re-measure, clock unchanged). Nothing is replayed when the element later appears, but its first arrange reads the current model value, so no update is lost. | **acceptable-and-documented** |
| *(found here)* the invalidation class is declared at binding registration, so every element sharing one key shares one class | `GetInvalidation(entry.Key)` is per key; there is no per-element override, and an undeclared binding answers `Everything`. | **must-document** |
| *(found here)* the `MarkDirty`/`HasDirtyNodes` mechanism has no observable effect | a2. | **must-document**, with a delete-or-pin decision from the Lead (§3) |

## 5. Item 4 — the authorized borrow did not leak

Diff of the whole P2 range for the two borrowed files:

- `Kernel/UiHost.cs` — exactly two additions: `Visible`/`VisibleKey` appended to
  `CommonWidgetAttributes` and to `ContainerAttributes`, and one new creation-time validation block that
  refuses a `Visible` value that is not true/false/1/0 (`UiContractException`). No other change.
- `Kernel/Widgets/AtomVocabulary.cs` — exactly one line: `Visible`/`VisibleKey` appended to
  `EngineWideAttributes`. No other change.

`Hidden` is untouched: it remains in both allow-lists and in `EngineWideAttributes`, its engine handling is
unchanged, and the visibility lane's `Hidden`-sibling identity checks pass at baseline. The c1 mutation's
failure text (hiding the preceding sibling renumbered B) confirms the `Hidden` path is still exercised.

## 6. Item 5 — invariants at `0ee55b6`

| invariant | method | verdict |
| --- | --- | --- |
| public types vs tiers | parse `docs/api-tiers.md` vs every committed `public` type under `Source/FerriteLib.UiKit` | **68 = 68**, zero diff both ways (`UiInvalidation` is P2's one new public type) |
| lane registration | committed `Program.cs` `X.RunAll()` vs classes declaring `RunAll` in `*Tests.cs` | **28 = 28**, zero unregistered, zero phantom (three new P2 lanes) |
| version axes | `Api` / `<VersionPrefix>` / `<modVersion>` | **0.5.0 / 0.5.0 / 0.5.0** |
| neutrality | gate 1 neutrality lane (green) + committed-tree grep for the second consumer's terms | clean |
| privacy | `pwsh -NoProfile -File scripts/privacy-audit.ps1` (vectors 1-2; identity by email) | **PRIVACY AUDIT CLEAN**, exit 0 |

## 7. Not verified here

- in-game behaviour (A4 and the keyboard/visibility scenarios stay pending);
- the disabled path for a command-bound slider/number field (no lane exercises it);
- consumer compilation against the new binding surface.

## 8. Branch note

`feat/0.5-verify` HEAD after this report; the file is the only change. P2's owner is inactive; the
must-document items above are routed to the Lead.
