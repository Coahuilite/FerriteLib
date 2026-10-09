# P4 adversarial verification — merged tip `85faab0`

Verifier `verifier` (task-4). Date 2026-09-15. Target: `merge(0.5): P4 document sources, dependency
tracking and atomic hot reload` (`85faab0`, parents `9eeb86d` + `bd2e04c`), read from a clean
`git archive` extraction in a temp directory — the shared checkout was never mutated. All P4 deltas:

```
Source/FerriteLib.UiKit/Kernel/UiDocumentService.cs   | 839 ++  (new)
Source/FerriteLib.UiKit/Kernel/UiDocumentSource.cs    | 160 ++  (new)
Source/FerriteLib.UiKit/Kernel/UiHost.cs              | 277 +-  (P4 section appended)
Source/FerriteLib.UiKit/Kernel/UiStyleDocument.cs     |  47 +-
docs/api-tiers.md                                     |  13 +
tools/.../KernelDocumentReloadTests.cs                | 799 ++  (new)
tools/.../Program.cs                                  |   3 +
```

## 1. Baseline (green, reproducible)

Commands (cwd = the extraction; `--no-restore` after the gate's own bootstrap):

```
pwsh -NoProfile -File scripts/verify-local.ps1          # exit 0, [setup] + 9 gates OK
dotnet run --no-restore --project tools/FerriteLib.UiKit.Tests -c Release
```

Result: `exit=0 ok=948 fail=0 ALL PASS`. The P4 lane section contributes **81** executed checks
(948 − 867 = 81); the P4 lane is the last section in `Program.cs`. This reproduces the Lead's
"gates green at the merge" claim at the exact sha.

## 2. Author mutation claims — independently reproduced

Each mutation was applied to the extraction, the harness re-run, then the file restored byte-for-byte
from a snapshot; `FINAL-RESTORED exit=0 ok=948 fail=0`.

| author claim | planted change (exact) | measured red | verdict |
| --- | --- | --- | --- |
| batch atomicity → 6 FAIL | `UiDocumentService.ReloadCore`: `for (int i = 0; i < affected.Count && failureReason.Length == 0; i++)` → `for (int i = 0; i < 0 && …)` (skip host validation) | `exit=1 ok=942 fail=6` | **CONFIRMED** |
| state cleanup → 5 FAIL | `UiHost`: `PruneDetachedState(previous, manifest);` → commented out | `exit=1 ok=943 fail=5` | **CONFIRMED** |
| duplicate-commit skip → 2 FAIL | `UiDocumentService`: the `state.GoodVersion == candidate.Version` guard → `if (false && …)` | `exit=1 ok=946 fail=2` | **CONFIRMED** |

Raw red signatures:

```
### MUTATION-A-skip-host-validation exit=1 ok=942 fail=6
    FAIL: the shared batch is refused because one of its hosts cannot draw the candidate
    FAIL: the refusal names the element the contract stopped at
    FAIL: and names the host that refused it
    FAIL: host A - which could have drawn it - keeps the old version
    FAIL: host B keeps the old version
    FAIL: manual reload commits the same batch to both hosts once every host accepts it
### MUTATION-B-no-prune exit=1 ok=943 fail=5
    FAIL: a kind change under a stable identity cleans the old element's state
    FAIL: and the slots the old kind used
    FAIL: a removed element's state is cleaned up
    FAIL: and its named slots with it
    FAIL: a removed scroll container's position is cleaned up too
### MUTATION-C-no-unchanged-skip exit=1 ok=946 fail=2
    FAIL: an unchanged version is skipped rather than committed again
    FAIL: a skip is not a new report
```

So the atomicity, state-cleanup and duplicate-commit behaviour are **mutation-proven**, not merely green.
One reporting discrepancy: the author's "13 lane tests / 68 checks" — **13 tests is confirmed**, and the
executed check count on this tip is **81**, not 68 (`Check(` appears 83 times in source; two are not
executed, one of them the `#if FER_DEV/#else` pair in `VerifyWatching`). Cosmetic, but the number
should be corrected rather than repeated.

## 3. The three declared gaps — probed, then classified

### (a) A removed / kind-changed element's `UiNode` stays in the session table

Probe injected into the existing removal sequence: after the element is gone from the manifest,
`Check(host.Session.GetNode(keep!.Id) == null, "PROBE-A: a removed element node leaves the session table")`.

```
### PROBE-A-removed-node exit=1 ok=948 fail=1
    FAIL: PROBE-A: a removed element node leaves the session table
```

Confirmed: `UiHost.PruneDetachedState` (UiHost.cs:447-489) zeroes the state only; the node stays in
`UiSession`'s table. **Additional finding the declaration did not mention:** `UiSession.GetNodeByElementId`
(UiSession.cs:373-385) iterates the node table with no `IsArranged` filter, and its own doc says it
returns null "when no arranged element carries it". After a reload removes the element, the lookup still
returns a non-arranged node with zeroed state — the newly reachable half of this gap.

**Classification: must-fix-in-0.5** — not because retention is fatal (it is session-scoped, bounded by
distinct identities ever arranged, released on close, and re-use of the identity reuses the cleaned node),
but because the bridge lookup now returns a node the documented contract says it will not. Fix is one
predicate or one doc sentence in the owner's file; the verifier did not touch it.

### (b) A reload resets document-settable theme tokens to construction-time values

Probe: build a host on `UiTheme.DarkGold.Clone()` held by the caller, re-tint `theme.Base` after
construction, then reload the layout document.

```
### PROBE-B-consumer-retint exit=1 ok=949 fail=1
  > ok: PROBE-B setup: the reload committed
  > FAIL: PROBE-B: a consumer re-tint applied after construction survives a reload
```

Confirmed: `UiHost.RebuildTree` calls `RestoreStyleBaseline()` (UiHost.cs:394-424), which assigns every
token back to `styleBaseline` — a `theme.Clone()` captured in the constructor (UiHost.cs:38,101) — before
the new document re-applies. A consumer that re-tints the theme instance it handed the host loses that
tint on the next reload, silently.

**Classification: must-document** — the design intent ("the document is the authority, so a token the
document no longer declares must stop applying") is defensible and the comment says so, but "your
post-construction re-tint is discarded by a reload" is a consumer-visible contract fact and belongs in
`30-consumer-handoff.md` §4. If the intended contract is "re-tint through the document", say that; if not,
re-baseline on first commit instead of construction.

### (c) The watcher is armed only if the directory exists when `AutoWatch` is enabled

Probe: register a path under a not-yet-existing directory, enable `AutoWatch`, create the directory,
enable `AutoWatch` again.

```
### PROBE-C-watcher-dir exit=1 ok=949 fail=1
  > ok: PROBE-C control: no watcher while the directory is absent
  > FAIL: PROBE-C: re-enabling AutoWatch after the directory appears arms the watcher
```

Confirmed: `Watch` returns early when `!Directory.Exists(directory)` (UiDocumentService.cs:648), and the
`AutoWatch` setter's `if (autoWatch == value) return;` (UiDocumentService.cs:98) makes the obvious retry a
no-op. Only `false → true` or re-`Add` recovers; manual `Reload` still works.

**Classification: must-fix-in-0.5 (small)** — this is the dev hot-reload path itself: a first run whose
`Layouts/` folder does not exist yet silently never arms, and the developer sees edits do nothing with no
error. A re-arm attempt in `Pump`/`Reload` (cheap, bounded by the document table) or a non-early-returning
setter closes it. If the owner will not fix it in 0.5, it is at minimum must-document in the consumer
handoff with the exact recovery steps.

## 4. Shared-file invariants at `85faab0` (sha-pinned)

| invariant | command / method | verdict |
| --- | --- | --- |
| public types vs tier entries | parse `docs/api-tiers.md` exactly as the lane does vs every `public (class\|struct\|interface\|enum\|record)` in every committed `Source/FerriteLib.UiKit/**/*.cs` (52 files) | **62 = 62** (12 stable / 37 public-unstable / 13 internalize-candidate); zero declared-but-unclassified; zero tier-entry-without-declaration. P4's 4 entries are appended at the end of Public-unstable ✅ |
| lane registration | `git ls-tree` + `git show 85faab0:…Program.cs`; class that declares `RunAll` vs `…RunAll()` invocations | **23 = 23**, zero unregistered, zero phantom; `KernelDocumentReloadTests` is registered by the P4 commit ✅ |
| three version axes | `FerriteLibVersion.Api`, `<VersionPrefix>`, `<modVersion>` at `85faab0` | **0.5.0 / 0.5.0 / 0.5.0** ✅ |
| neutrality | gate 1 neutrality lane (inside the green clean-extraction run); committed-tree vocabulary grep for the second consumer's terms | no consumer vocabulary; the wave-A F4 finding is fixed at this tip (`40-verification.md` A3b now says "面板 A / 面板 B", recorded as F4) ✅ |
| privacy | `pwsh -NoProfile -File scripts/privacy-audit.ps1` (vector 1 working tree, vector 2 commit messages, identity by email; `-FullHistory` not run) | **PRIVACY AUDIT CLEAN, exit 0** ✅ |

## 5. The two things the Lead asked to check most

**ParseFile is actually used.** `UiDocumentService.ReadExternal` calls
`UiLayoutManifest.ParseFile(path)` (UiDocumentService.cs:570) and `UiStyleDocument.ParseFile(path)`
(:578); `ReadEmbedded` uses `UiLayoutManifest.Parse(xml)` / `UiStyleDocument.Parse(xml)` for the fallback.
`UiLayoutManifest.ParseFile` is `return Parse(File.ReadAllText(path));` and `UiStyleDocument.ParseFile`
reads the text and calls the same `Parse(xml)` — so there is **one parser per vocabulary, no second
parser beside it**. The lane's own "production caller" assertion is only a source-text presence check
(UiDocumentService.cs contains both literal call strings, with a planted control); the call path above is
the independent evidence that it is live, not just present.

**The worker thread never touches Verse/Unity.** The entire `FileSystemWatcher` surface is
`Changed/Created/Deleted/Renamed → Signal(documentId)` plus `Error → lock { overflowed = true; }`
(UiDocumentService.cs:675-689), and `Signal` only inserts into a locked `HashSet<string>` (:187-196).
An independent grep of the file finds **no `using Verse`, no `UnityEngine`** and no game type — the only
hits are the words "Verse"/"Unity" inside doc comments. Commit/validation runs in `Pump`, which
`UiHost.BeginFrame` calls before the session's own frame (:186-190).

## 6. Additional coverage observations (not requested, still findings)

- **Style documents have no pre-validation across a batch.** `ReloadCore`'s validation loop only fires for
  `dependency.LayoutId == documentId` (UiDocumentService.cs:452); a shared **style** document commits host
  by host with no rollback around the commit loop (:478-492). Nothing in the lane covers a style document
  shared by two hosts. Low risk today because a structurally invalid style document never reaches here and
  declaration-level drops are soft — but the "whole batch validated before anything commits" claim is
  proven for layouts only.
- The P4 lane's green is genuine: every claim above is either mutation-proven (the three behaviours) or
  backed by a positive control (source-wiring planted control, sandboxed parser controls elsewhere).

## 7. Not verified here

- any in-game behaviour (no game session in this environment); the reload contract remains
  "stub-verified", and the in-game checklist A6/A7/A8 stays pending;
- a real editor-driven multi-notification storm against a real filesystem watcher (the lane drives
  `Signal` directly and one real write);
- `-FullHistory` privacy (not a pre-push check here).

## 8. Branch note for the Lead

The verifier branch is `feat/0.5-verify`; at the time of this pass its HEAD is the dead-lane guard commit
(below) whose parent is a byte-identical duplicate of the Lead's `8d4bdb0` (the wave-A docs). The lane
guard commit is the only commit that needs to reach `0.5.x`; cherry-picking it onto the tip applies
cleanly (it adds one `*Tests.cs` file and one registration line in `Program.cs`).
