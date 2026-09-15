# Wave-A audit — W0 contract docs vs the committed tree

Verifier `verifier` (task-4), branch `feat/0.5-verify`.

Audited revisions, both read from a clean `git archive` extraction (not a working tree):

| sha | what it is |
| --- | --- |
| `c53bd37` | the W0-declared baseline; `0.4.x` and `origin/0.4.x` tip; carries **no** `docs/development/0.5/` |
| `99acc5f` | W0 tip = `0.5.x` = every `feat/0.5-*` branch tip at audit time; carries `docs/development/0.5/**` |

## 1. Baseline reproduction (clean extraction)

Commands, with the extraction directory as cwd:

```
git archive --format=tar -o <tmp>/c53bd37.tar c53bd37
git archive --format=tar -o <tmp>/99acc5f.tar  99acc5f
tar -xf <tmp>/<sha>.tar -C <tmp>/<sha>          # Windows bsdtar; Git-bash tar misreads "C:" as a host
pwsh -NoProfile -File scripts/verify-local.ps1  # cwd = <tmp>/<sha>
dotnet run --no-restore --project tools/FerriteLib.UiKit.Tests -c Release
```

Results (dotnet SDK 10.0.204):

| sha | `verify-local.ps1` | steps | harness |
| --- | --- | --- | --- |
| `c53bd37` | exit 0, every step OK | `[setup]` + 9 `[run]` gates | exit 0, 867 `ok:`, 0 `FAIL:`, `ALL PASS` |
| `99acc5f` | exit 0, every step OK | `[setup]` + 9 `[run]` gates | exit 0, 867 `ok:`, 0 `FAIL:`, `ALL PASS` |

This is a **clean-extraction run, not a working-tree run**: the only input is the archived commit, and no
untracked build output or local edit is involved. It confirms `00-baseline.md` §1 ("gate suite re-run green
on the baseline") and `40-verification.md` §1 for `c53bd37`, and additionally confirms the W0 tip
`99acc5f`.

## 2. W0 doc audit

Verdicts: CONFIRMED / FALSE / INCONSISTENT / UNCONFIRMABLE-HERE / CONFIRMED-as-plan.

| # | statement | where | verdict | evidence |
| --- | --- | --- | --- | --- |
| 1 | baseline revision `c53bd37`; `0.4.x` and `origin/0.4.x` at the same point | `00-baseline.md:9`, `README.md:6` | CONFIRMED | `git rev-parse 0.4.x` = `git rev-parse origin/0.4.x` = `c53bd37552ae27a16d1ef7fba239d694cd977746` |
| 2 | contract axis at baseline = `0.4.0` | `00-baseline.md:10` | CONFIRMED | extracted `c53bd37`: `FerriteLibVersion.cs` `Api = new Version(0,4,0)`, `About.xml` `<modVersion>0.4.0`, csproj `<VersionPrefix>0.4.0` |
| 3 | gate suite = 10 items, all green on the baseline | `00-baseline.md:12`, `40-verification.md:12-23` | CONFIRMED | clean-extraction run above: `[setup]` + 9 gates, exit 0 |
| 4 | harness Release `ALL PASS` | `00-baseline.md:13` | CONFIRMED | 867 ok / 0 FAIL / `ALL PASS`, exit 0, at both shas |
| 5 | `v0.4.0-rc1` is a prerelease and `/releases/latest` still 404s | `00-baseline.md:11` | UNCONFIRMABLE-HERE | `api.github.com` is unreachable from this environment; needs a network-enabled run. Not a tree claim. |
| 6 | node/identity layer landed in 0.4.x: `UiNodeId` (`Key` + `Path`, U+001F/U+001E separators), `UiNode` (identity + named state slots + `IsDirty` + children), `UiSession` node-keyed (`GetNode`/`GetOrCreateNode`/`GetNodeByElementId`/`SetScrollPosition(UiNode,…)`/`TrippedNodes`) | `00-baseline.md:24-26` | CONFIRMED | `UiNodeId.cs:30,42,49,67,72`; `UiNode.cs:20,81,84,91,106`; `UiSession.cs:125,363,373,431,488,494` |
| 7 | hit layer exists as one popup layer: `UiHitLayer` (element + Host-space rect + is-popup); `UiSession.BeginHitPass`/`PushHitLayer`/`IsPointerOverHigherLayer`; `UiPopup.RectFor` is the only popup-rect rule | `00-baseline.md:27-29` | CONFIRMED | `UiHitLayer.cs:13-29`; `UiSession.cs:142,160,172,190`; `UiPopup.cs:28` (definition, one call site `DropdownWidget.cs:117`) |
| 8 | "仍然欠的: … `UiNative.YieldsToCoveringPopup` 仍是下拉触发器的私有分支" | `00-baseline.md:30` | **FALSE** | `git grep -i YieldsToCoveringPopup 99acc5f` → **0 hits** under `Source/**`; `UiHitLayer.cs:9-11` says the owned stack replaced "the per-element yield branch the funnel used to carry"; `UiNative.cs:101,136` now call `ctx.Session.IsPointerOverHigherLayer`; `docs/api-tiers.md:176-177` records the branch as **removed** in the 0.4.0 window. The same stale sentence also survives in `MEMORY.md:236` and `TODO.md:193` (the latter still cites `UiNative.cs:259`, a line that no longer exists), so §2.1 copied an already-superseded claim instead of correcting it. The other half of the sentence (content layers do not arbitrate each other, `UiSession.cs:183-188`) is true. |
| 9 | "完整 z 序调度 … 是本轮 P2/P3 的目标" | `00-baseline.md:31` | INCONSISTENT | `10-work-packages.md:12` P2 = binding notification/invalidation/command/visibility; `:13` P3 = keyed repeater + three controls. No P1–P5 row mentions topmost-first dispatch or element-level yield, so the residual hit-stack generalisation has no owning package. |
| 10 | hot reload: a save must update the open window; manual reload + last-known-good; C# kind changes excluded | `00-baseline.md:35-40` | CONFIRMED-as-plan | master plan §4 (lines 64-75) and `20-api-and-xml.md` §4 (lines 67-83) say the same; nothing here claims implementation, and the baseline genuinely has no production caller of `ParseFile` (see #12) |
| 11 | ecosystem-gate supersession synced into `AGENTS.md` | `00-baseline.md:48-56` | CONFIRMED | `AGENTS.md:102-109` carries the 2026-09-15 superseding wording and points back to `00-baseline.md` §2.4 |
| 12 | `UiLayoutManifest.ParseFile` "has zero callers" / "no caller" | not in `docs/development/0.5/`; `docs/api-tiers.md:107,276`, `MEMORY.md:377` | **FALSE-as-written** | `UiLayoutManifest.cs:81` and `UiStyleDocument.cs:235` are called from `KernelStyleDocumentTests.cs:340,345,348,357`. The accurate form is "no **production** caller", which `MEMORY.md:1273` already says. Outside the verifier's edit scope; reported. |
| 13 | `0.5.0` on all three version axes | `00-baseline.md:78-79`, `30-consumer-handoff.md:10` | CONFIRMED | extracted `99acc5f`: `Api = new Version(0,5,0)`, `<VersionPrefix>0.5.0`, `<modVersion>0.5.0` |
| 14 | `ModRequirement` "只解析 packageId/displayName" | `30-consumer-handoff.md:15` | FALSE-as-written (minor) | `AGENTS.md:63-64` lists three fields: `packageId`, `alternativePackageIds`, `displayName`. The point (no version range) stands; the field list is incomplete. |
| 15 | no script places a mod in a game directory | `50-dev-package.md:14`, `AGENTS.md:180` | CONFIRMED-as-static-check | every `Mods` occurrence under `scripts/**` is a comment; no write path. Not mutation-proven (the claim is about absence). |
| 16 | `40-verification.md` §1 gate table + "what the gates do/don't prove" | `40-verification.md:12-27` | CONFIRMED | matches the 9 gate names and the clean-extraction run |
| 17 | `40-verification.md` §3 A1–A11 all pending | `40-verification.md:48-61` | CONFIRMED-as-honest | every row is labelled pending; no in-game claim is made |
| 18 | `20-api-and-xml.md` is a draft | `20-api-and-xml.md:3-5` | CONFIRMED | "状态：设计草案 … 任何伪代码都不是已实现证据"; the P1/P2 type names are placeholders — P6 must re-audit §3 after merge |
| 19 | vanilla-boundary facts | `00-baseline.md:58-74` | PARTIAL: existence CONFIRMED, behaviour UNCONFIRMABLE-HERE | every member name in the table (`RemoveWindowsOfType`, `onlyOneOfTypeAllowed`, `Notify_ManuallySetFocus`, `WindowsForcePause`, `WindowsPreventCameraMotion`, `windowRect`, `draggable`, `resizeable`, `WindowResizer`, `Notify_ResolutionChanged`, `GetsInput`, `WindowOnGUI`) exists in the pinned `Krafs.Rimworld.Ref 1.6.4871` `ref/net472/Assembly-CSharp.dll` (SHA-256 `B7EB69E7614C6484AD1B306597AFDBA4852D7A9C29770762A0083A83C804235D`). The control-flow claims ("Add calls `RemoveWindowsOfType`", "the click path reorders", "`PreOpen` notifies the selector") are IL-level; no decompile artifact is tracked, so they are not reproducible from the repo. |
| 20 | "any public addition also bumps minor" | `00-baseline.md:78` | CONFIRMED-as-policy | `FerriteLibVersionTests` pins the three axes; the bump rule itself is a rule, not a measurement |

The four §2 subsections map to audit rows: §2.1 node layer → #6, §2.1 hit layer → #7 and **#8**, §2.2 hot
reload → #10 (with #12 as the code-level fact behind it), §2.3/#2.4 → #11 and the master-plan
supersessions. **#8 is the one false statement these "corrected" statements left behind.**

## 3. Trap A — a lane file that exists but never runs

Checked on the committed tree, never on the checkout:

```
git ls-tree -r --name-only 99acc5f -- tools/FerriteLib.UiKit.Tests
git show 99acc5f:<file>            # per *.cs: does it declare "static int RunAll"?
git show 99acc5f:tools/FerriteLib.UiKit.Tests/Program.cs
```

Result at `99acc5f`: **22** files declare `RunAll`; all **22** appear as
`failures += <Name>.RunAll();` in `Program.cs`; 22 registered names against 22 lane files — zero
unregistered lanes and zero phantom registrations. The test csproj globs every `.cs` under its own
directory and removes only `Stubs/**/*.cs`, so a lane file cannot silently fall out of the compilation.
The recorded `dfba792` failure (a 491-line lane committed but never registered) does **not** repeat at
`99acc5f`.

## 4. Trap B — public type vs `docs/api-tiers.md`

Two independent checks:

1. The repo's own lane (`FerriteLibApiTierTests`), run inside gate 1 at `99acc5f`, reported its four
   `ok:` lines, including "Every public payload type is classified in exactly one tier" and "No tier entry
   names a type that no longer exists".
2. An independent source-level scan of `Source/FerriteLib.UiKit/**/*.cs` for
   `public (static|sealed|abstract|readonly|partial)* (class|struct|interface|enum|record) NAME`, compared
   against tier entries parsed exactly the way the lane parses them: **58** public type declarations vs
   **58** entries (12 stable / 33 public-unstable / 13 internalize-candidate), with zero
   declared-but-unclassified and zero entry-without-declaration.

Limitation: check 2 is a source regex, not reflection; nested-type and accessibility edge cases are only
covered by the bijection, and check 1 remains authoritative. The "40 types" figure in the older
`MEMORY.md` entry is a historical count, not a contradiction.

## 5. Vocabulary finding (reported, not edited)

`docs/development/0.5/40-verification.md:53` (the A3b scenario row) names the second consumer's two
content windows with that consumer's own window names — the same word pair used as window names in the
master plan §3. `00-baseline.md` §4 forbids consumer business vocabulary in this repository's documents,
and `FerriteLibNeutralityTests.cs:19-23` scans only `Source/FerriteLib.UiKit` and
`tools/FerriteLib.UiKit.Tests`, so no gate can catch a document hit. The terms are deliberately **not
reproduced here**. The file is Lead-owned; reported, not edited.

Lower confidence, reported for a ruling rather than asserted: `TODO.md:149` uses the lowercase English
stem of the second repository's framework **assembly name**. It reads as a sanitised generic noun, so this
is a flag, not a finding; the term is not reproduced here either.

## 6. What this wave did NOT verify

- any in-game behaviour (no game session available here);
- the actual game `Assembly-CSharp.dll` hash `5CF1B5BE…` and the IL control-flow claims of §3;
- GitHub release state (`v0.4.0-rc1`, `/releases/latest` 404) — the host is unreachable;
- any consumer repository compiling against the 0.5 surface.
