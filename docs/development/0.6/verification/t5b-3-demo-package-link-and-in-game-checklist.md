# T5b-3 — demo package, demo→carrier link, and the in-game operator checklist

Owner `probe`. Branch `feat/0.6-probe`, synced with `git merge 0.6.x` for this round (merge commit
`058371b607272a682604f3eb3ead2847e4322090`; the 0.6.x side is `7d2b645`). Demo repository
`ferritelib_uikit_demo` at `d47b2216ab4555f39a0e0bc86c10edd463d3e53f` — READ-ONLY in this work: it was not
built, not modified and not installed anywhere, and its `git status --porcelain` was empty before and after
the checks below. Nothing was pushed, tagged, released or placed in a game `Mods/` folder.

Evidence class: **已实现 + 已自动化验证** for the package and link checks (files hashed and metadata read
from outside). The in-game checklist in §4 is **尚待实机** in every row. Nothing in this record is
mutation-proven — planting a defect and watching a lane go red belongs to the package lanes, not to an
outside probe.

## 1. Demo package content (task-29 item 5a)

Independently inspected over the staged folder `dist/FerriteLibUiKitDemo/` of the demo repo. Raw listing
(relative path | bytes):

```text
LoadFolders.xml | 102
1.6/Assemblies/FerriteLibUiKitDemo.dll | 47616
About/About.xml | 861
Languages/ChineseSimplified/Keyed/FerriteLibUiKitDemo.xml | 2911
Languages/English/Keyed/FerriteLibUiKitDemo.xml | 2955
Xml/Panel.xml | 1674
Xml/Settings.xml | 7512
Xml/Style.xml | 332
```

Assertions, each run here rather than taken from the demo's `pack.ps1`:

- exactly those eight files, and no ninth;
- **no `FerriteLib.UiKit.dll` in the package**: a recursive filename search over the whole demo repository
  returns exactly one hit, `tools/DemoProbe/bin/Release/net472/FerriteLib.UiKit.dll`. That is the dev-only
  probe's own output directory, excluded by the repository's `.gitignore` (`bin/`), and not part of the mod
  package. Its SHA-256 is `F1CB344DDFA73982A79F266AB1BB838F72B7BF6DD319CB6EC09919244942A18D`, the packaged
  carrier's own bytes (§2);
- **no drive-letter path in any tracked file**: for every path from `git ls-files`, the file was read and
  scanned with `Select-String -Pattern '[A-Za-z]:[\\/]'`; zero matches. `FerriteLib.local.props.example`
  documents that a copy named `FerriteLib.local.props` may hold an absolute path and is gitignored; that
  copy is untracked and was not present;
- demo package assembly SHA-256 `8F30685E68A8D05CFA0EAA5B10D7586DA104F788AE88243DDDAB05674886DEFA` (47616 B).

**Nothing found in the shapes probed.**

## 2. The demo links the packaged carrier, not a stale copy (item 5b)

- **Where the demo compiles against it.** `Source/FerriteLibUiKitDemo/FerriteLibUiKitDemo.csproj` references
  `FerriteLib.UiKit` with `<HintPath>$(FerriteLibArtifactDir)\FerriteLib.UiKit.dll</HintPath>` and
  `<Private>false</Private>`; `Directory.Build.props` derives `FerriteLibArtifactDir` as
  `$(FerriteLibRoot)\dist\dev\FerriteLib\1.6\Assemblies` with `FerriteLibRoot` defaulting to the relative
  sibling `..\ferritelib`. No carrier copy exists inside the demo repo to resolve instead.
- **Identity it references.** Reflection-only metadata of the staged demo assembly (my probe, demo path passed
  as an argument) reports `references FerriteLib.UiKit 0.6.0.0`. The carrier my probe loads is
  `FerriteLib.UiKit 0.6.0.0`, loaded from the packaged bytes hashing `F1CB344D…`.
- **The bytes it resolved.** The demo's dev-probe output carries a copy of the carrier at SHA-256
  `F1CB344D…` — byte-identical to the freshly staged package. (That copy exists because the dev probe
  references the carrier with `Private=true` so its output is runnable; the mod project's `Private=false` is
  what keeps the package clean, which §1 confirms.)
- **What this does not prove.** The in-game runtime bind. Outside a game the demo assembly is never loaded and
  the carrier is resolved by the installed mod list and the game's own loader; the probe proves the compiled
  identity and the artifact bytes, and §4 is where the runtime bind has to be observed.

## 3. Carrier re-check and probe re-run (item 5c)

- The three FL version axes are unchanged at `0.6.0`: `FerriteLibVersion.cs` `new Version(0, 6, 0)`,
  `About/About.xml <modVersion>0.6.0`, csproj `<VersionPrefix>0.6.0` (the branch moved only by docs commits
  since T5b-2).
- Fresh carrier (the lead's staging, read from the main checkout and copied into this worktree's gitignored
  `dist/dev/FerriteLib`): `version.txt` = `FerriteLib 0.6.0-dev` / `build=dev` / `commit=0d48d25ed1cc`;
  packaged DLL SHA-256 `F1CB344DDFA73982A79F266AB1BB838F72B7BF6DD319CB6EC09919244942A18D`; the closed five-file
  set and the absence of content directories are unchanged from T5b-2 §1.
- Probe re-run against it, with the demo assembly as the optional argument (the full catalogue/adapter/
  scheduler output was re-run in the same execution; it is unchanged from T5b-2 §2 and omitted here except
  for the carrier line and the new section):

```powershell
dotnet run --project dist/probe-0.6/Probe.csproj -c Release -- <repo-root>/ferritelib_uikit_demo/dist/FerriteLibUiKitDemo/1.6/Assemblies/FerriteLibUiKitDemo.dll
```

```text
-- loaded carrier --
  assembly   : FerriteLib.UiKit
  version    : 0.6.0.0
  location   : <repo-root>\dist\probe-0.6\bin\Release\net472\FerriteLib.UiKit.dll
  sha256     : F1CB344DDFA73982A79F266AB1BB838F72B7BF6DD319CB6EC09919244942A18D
-- demo package link (optional argument: the staged demo DLL) --
  demo sha256 : 8F30685E68A8D05CFA0EAA5B10D7586DA104F788AE88243DDDAB05674886DEFA
  demo        : FerriteLibUiKitDemo 1.0.0.0
    references mscorlib 4.0.0.0
    references FerriteLib.UiKit 0.6.0.0
    references System 4.0.0.0
    references System.Core 4.0.0.0
    references Assembly-CSharp 1.6.9676.17735
    references UnityEngine.CoreModule 0.0.0.0
    references UnityEngine.TextRenderingModule 0.0.0.0
  OK   the demo references the carrier assembly by its packaged identity  ::  referenced FerriteLib.UiKit vs loaded 0.6.0.0
[probe-0.6] unexpected-throw count: 0
```

**Nothing found in the shapes probed.**

## 4. In-game operator checklist — every row 尚待实机

Install once (manual; neither repository's scripts copy anything into `Mods/`):

1. copy `ferritelib/dist/dev/FerriteLib` into the game's `Mods/FerriteLib`;
2. copy `ferritelib_uikit_demo/dist/FerriteLibUiKitDemo` into `Mods/FerriteLibUiKitDemo`;
3. enable FerriteLib and then FerriteLib UiKit Demo, and restart the game.

The packageIds are `coahuilite.ferritelib` and `coahuilite.ferritelibuikit.demo`; the demo declares
FerriteLib as a dependency and loads after it. Every row below is unchecked until a human runs it.

**1. Entry and the three tabs.** Options → Mod settings → **FerriteLib UiKit Demo**.
*See:* the vanilla settings shell with the demo's content area: a "FerriteLib UiKit 0.6 demo" banner, a
"catalogue source: UiWidgetCatalog.Snapshot" line, and three buttons — Core components / Consumer components /
Multi-window and reload; clicking each switches the body and the "tab: …" readout follows.
*Failure looks like:* a blank content area; the "This page could not be entered" notice; a prerequisite line
starting "[FerriteLib.UiKit]" (carrier missing or version-mismatched — copy that text); or the shell itself no
longer looking vanilla.

**2. Core samples.** On Core components, scan "Every registered core kind", then use "Working samples".
*See:* the checkbox toggles; the progress bar and the meter move with it; slider, number field, dropdown,
stepper and mode row all react; the tree expands and the "tree selection: …" readout follows the click; the
chart draws and its drag stays inside its rect; the list count matches the entries.
*Failure looks like:* a control that draws but never reacts; a dropdown option click stolen by its trigger; a
recovery band in place of a control (screenshot its path); the count line disagreeing with the rows.

**3. Consumer directory.** Consumer components; click All scopes / Core scope / This demo's scope / Unknown
sources.
*See:* rows grouped by scope; this demo's own scope shows its two kinds with their declared attributes and
labels; any other non-core scope is marked "unknown source"; a kind with no declared schema prints
"not declared"; the "consumer kinds listed" count matches the rows.
*Failure looks like:* ownership guessed for a scope the demo does not know; "not declared" rendered as "no
attributes allowed"; rows changing between two identical filter clicks (a live view rather than a snapshot).

**4. Dual window: click, map-through, input, close.** On Multi-window and reload, open panel alpha then panel
beta.
*See:* two panels coexist; clicking one makes it the keyboard target; each keeps its own search term,
selection, draft and scroll; clicking and dragging on the map outside the panels still moves the camera; each
panel's Close button closes only that panel; the "panels=… active=…" readout follows.
*Failure looks like:* the second open closing or re-activating the first; the map not responding while a panel
is open; a panel field capturing clicks aimed at the map; closing one panel freezing or closing the other.

**5. Pause, then save.** Pause with space; edit `Mods/FerriteLibUiKitDemo/Xml/Settings.xml` (for example
`tab-windows`'s `Width="170"` to `"220"`); wait about a second.
*See:* the settings page re-arranges while the game is paused; the buttons resize; the reload report line on
the Windows tab shows the settings layout and its scheduler counters return to `pending=0 deferred=0
retrying=0`.
*Failure looks like:* no change while paused (a scheduler tied to the simulation clock); the update only after
unpausing; an update that resets a value that was typed in.

**6. Layout save (automatic).** With the page open, change `Xml/Settings.xml` (banner `Height`, or a
`TextKey`); save; wait.
*See:* the open page updates in place, no restart; the report line shows a report for the settings layout.
*Failure looks like:* nothing until the page is reopened; a blank or half-arranged page; the old layout still
drawn after the report says accepted.

**7. Style save.** Change `Xml/Style.xml` (the `demo-night` Panel colour, or `demo-compact` Padding); save;
wait; then try the "Reload style" button.
*See:* the open settings page and both open panels re-tint together; geometry stays sane; the report line names
the style document.
*Failure looks like:* only one window re-tinting; a colour change forcing a re-measure that loses input state;
no change at all.

**8. Invalid XML recovery.** Windows tab → "Write an invalid candidate".
*See:* the report reads `invalid candidate: rejected … | live page still has N root(s)`; the page keeps
drawing the previous valid version; the demo restores the file; a following "Reload layout" reports
accepted/skipped and the page stays.
*Failure looks like:* the body going blank or covered by a recovery band; a report claiming acceptance; the
file left broken on disk.

**9. Drag, and IME where reachable.** Drag: on Core components hold the slider/stepper and drag; while still
held, save `Xml/Style.xml`; release. Text: in panel alpha type into "draft value"; with the field focused,
save `Xml/Panel.xml`; then click elsewhere.
*See:* the drag ends cleanly and the mouse is never left captured; the typed draft is still there unless the
reload legitimately replaced that element; the next click lands where it was aimed.
*Failure looks like:* a control that keeps tracking after mouse-up (a stuck capture); a draft lost by a batch
that reported itself rolled back; a click going to the wrong window.
*IME note:* this demo has no free-text field — its only keyboard entry is a number field — so a CJK IME
composition cannot be observed here. That half is **N/A for this demo** and stays 尚待实机 for whichever
consumer page ships a real text field; it is recorded rather than faked.

**10. Close and reopen.** Close both panels, close the settings dialog, reopen it and the panels; then quit to
the main menu and come back.
*See:* no stale window survives; reopening builds a fresh page and fresh panels; the scheduler/report readout
is fresh; no log line about a reused released host; nothing points at the previous game after a reload.
*Failure looks like:* a panel that will not reopen; a frozen map after closing; an exception on reopen; a
report from the previous session still displayed.

## 5. What this record does not prove

- Every row of §4 is **尚待实机**: no game session was run, so nothing about rendering, input routing, the map,
  or the runtime carrier bind is observed.
- The demo's own dev probe and build were **not run** here: that would rebuild the demo repository, which this
  task treats as read-only. Its README's ten checks are the demo owner's evidence, not mine.
- The package and link checks are file-set, hash and metadata checks on one staged build; they do not prove the
  demo DLL matches its sources beyond the repository tip it was built from.
