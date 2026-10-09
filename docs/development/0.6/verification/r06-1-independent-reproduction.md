# R06 — independent reproduction of the external review's seven must-fix items

Owner: `verify` (shared task `task-38`). **Pre-fix baseline**, bound to:

| artefact | revision / hash |
| --- | --- |
| FerriteLib source | `a6e8885` on `0.6.x` (the fixes for task-34/35/36 are not in this tree) |
| Payload driven by the probe | `dist/dev/FerriteLib/1.6/Assemblies/FerriteLib.UiKit.dll`, staged at `f3595a1`, SHA-256 `185C57601FB0D66781B6145B79711221FBB2096334F6355F18D8B7357388B365` |
| Demo repository | `ferritelib_uikit_demo`, branch `master`, tip `d47b221`, read-only |
| Demo payload driven | `dist/FerriteLibUiKitDemo/1.6/Assemblies/FerriteLibUiKitDemo.dll`, SHA-256 `8F30685E68A8D05CFA0EAA5B10D7586DA104F788AE88243DDDAB05674886DEFA` |
| Game stand-in | the harness's own stub projects (`Assembly-CSharp.dll` from `Stubs/VerseStub`, `UnityEngine.CoreModule/IMGUIModule/TextRenderingModule` from the other three stubs) — the same stand-ins the reviewer's probe used, but my own fixture code |

**Method.** I did not run or read the reviewer's probe. I wrote my own console project in a unique temp
directory (`net472`, references: the two delivered payloads + the four stub assemblies) and one fixture per
item, each selected by argument and run in its own process. Each item below shows the fixture's own raw line.
The probe is scratch and is not committed; the excerpts below are the actual fixture statements.

## R06-1 — LoadFolders: the demo's DLL is unreachable while FerriteLib's is reachable (P1)

**Reviewer's claim.** The demo's `LoadFolders.xml` declares only `/` while its DLL is at `1.6/Assemblies/`,
whereas FerriteLib's own declares `/` and `1.6`.

**Delivered manifests (read from both trees and both staged packages).**

```
ferritelib_uikit_demo/LoadFolders.xml  ->  <loadFolders><v1.6><li>/</li></v1.6></loadFolders>
FerriteLib/LoadFolders.xml             ->  <loadFolders><v1.6><li>/</li><li>1.6</li></v1.6></loadFolders>
```

**Rule derived from the game's own code** (rimsage, `Verse/ModContentPack.cs` and
`Verse/ModAssemblyHandler.cs`):

1. `ModContentPack.InitLoadFolders` (declaration at `ModContentPack.cs:260`) collects the declared version
   list: when `ModLister.GetModWithIdentifier(PackageId).loadFolders` has defined versions, the list for the
   current version is taken (`LoadFoldersForVersion(VersionControl.CurrentVersionString)` -> `AddFolders(list);`
   at `:269`), with the nearest-lower-version list (`:294`) and `"default"` (`:301`) as fallbacks. Only when
   none of those produced a folder does it fall back to a `RootDir/<CurrentVersionStringWithoutBuild>`
   directory or a version-directory scan. **`<v1.6>` is a defined version, so the fallback never runs for
   either mod**: the load-folder list is exactly the declared `<li>` entries.
2. `ModAssemblyHandler.ReloadAll` (full body read) enumerates
   `ModContentPack.GetAllFilesForModPreserveOrder(mod, "Assemblies/", e => e.ToLower() == ".dll")` and calls
   `Assembly.LoadFrom(item.FullName)` for each result; a load failure logs and **breaks**.
3. `ModContentPack.GetAllFilesForModPreserveOrder` (`:418`) walks the mod's folders with a content path;
   `:425` is `new DirectoryInfo(Path.Combine(text, contentPath))` — i.e. each load folder joined with
   `"Assemblies/"`.
4. `DebugActionsMods.cs:49` uses the same walker with `"Assemblies/"`, which is consistent with that reading.

**My computation on the delivered manifests** (no game needed):

| mod | load folders resolved from the manifest | `Assemblies/` search paths | payload present there? | reachable? |
| --- | --- | --- | --- | --- |
| demo `coahuilite.ferritelibuikit.demo` | `/` only | `<root>/Assemblies/*.dll` | no (DLL is at `1.6/Assemblies/`) | **no** |
| FerriteLib `coahuilite.ferritelib` | `/`, `1.6` | `<root>/Assemblies/*.dll`, `<root>/1.6/Assemblies/*.dll` | yes, `1.6/Assemblies/FerriteLib.UiKit.dll` | **yes** |

**Verdict: reproduced, P1 is right.** The demo's `Mod` subclass is never loaded, so its `ModSettings` entry
does not exist at all: the deliverable is not merely degraded, it is absent.

**What I could not confirm without launching the game** (stated, not implied): that the running game's
`VersionControl.CurrentVersionString` selects `<v1.6>` for RimWorld 1.6.4871 exactly as the version-key
matching implies; that no other loader path (dynamic folders, `Common`, an assembly earlier in load order)
brings the demo DLL in; and the observed absence of the settings entry. I also could not retrieve the full
bodies of `AddFolders` and `GetAllFilesForModPreserveOrder` from the source index (the reader truncates), so
the `folder + "Assemblies/"` join above rests on `ModContentPack.cs:425` plus the call sites, not on a full
transcription of those two methods.

## R06-2 — LIB-REOPEN: a reopen drops the pending signal and applies the stale document (P1)

**Reviewer's claim.** `before=new pending=True / after=old pending=False` — a signal that arrives while
nothing is attached is consumed without a read when the next host attaches.

**My own sequence** (fixture `r062`; no reuse of the reviewer's probe):

```
service.Add(new UiDocumentSource("d", UiDocumentKind.Layout, path), v1);   // valid v1 in force
var h1 = new UiHost("probe", UiLayoutManifest.Parse(v1), ...); service.Attach(h1, "d"); h1.Dispose();
File.WriteAllText(path, v2);                    // the file is now NEW
bool signalled = service.Signal("d");           // watcher channel, no Pump after this
var h2 = new UiHost("probe", UiLayoutManifest.Parse(v2), ...);   // the new host starts from the NEW text
service.Attach(h2, "d");
```

Raw line:

```
R062 firstAccepted=False attached1=True fileIsNew=true signalled=True pendingAfterSignal=True attached2=True hasOld=True hasNew=False pendingAfterAttach=False schedulerPending=0 reports=0
```

**Exact precondition.** `Signal` was called with **no attached host and no `Pump` between `Signal` and
`Attach`**. In that window `pending` holds the id but `schedules` is empty (only a pump moves a signal into the
schedule table). `Attach` -> `ResolveScheduledNow` (`UiDocumentService.cs:743-758`) reads
`scheduled = schedules.Remove(documentId); pending.Remove(documentId);` and only reloads when `scheduled` is
true — so the pending entry is destroyed without a read, and then `Attach` (`:462-470`) applies the service's
stale `GoodLayout` (v1) over the host the consumer just built from v2. With a host attached, or with one
`Pump` in between, the signal resolves normally.

**Verdict: reproduced exactly, P1 is right for this round's promise** ("the next attach resolves the newest
valid content"). The failure is silent: `reports=0`, no notice, and the signal is gone.

## R06-3 — FILTER: every scope-filter click throws on a duplicate binding (P1)

**Reviewer's claim.** `InvalidOperationException Duplicate value binding 'consumerKinds'`.

**My fixture** (`r063`): construct `DemoSettingsPage`, read its private `bindings` field by reflection, then
invoke the two commands the XML filter buttons are bound to.

```
R063 invoke(scope-core) -> InvalidOperationException: Duplicate value binding 'consumerKinds'.
R063 invoke(scope-all) -> InvalidOperationException: Duplicate value binding 'consumerKinds'.
```

Cause, read from the demo: `DemoSettingsPage.BindPage` calls `BindConsumerRows()` once at `:263`, and every
scope command calls `SwitchScope` -> `BindConsumerRows()` again (`:272-277`), which re-registers
`BindReadOnly("consumerKinds", ...)` and the per-row keys at `:356-367`. `UiBindings` refuses a duplicate value
binding, so the first filter click — including re-clicking the default `all` — throws out of the command.

**Verdict: reproduced, P1 is right.** The page opens, then throws on the most ordinary interaction.

## R06-4 — ADD: the model grows while both view models' lists stay stale (P2)

**Reviewer's claim.** `before=8 model=9 alpha=8 beta=8`.

**My fixture** (`r064`): construct the page, then `page.Model.AddSyntheticRow()` and read the public
`Model.Keys` / `Alpha.VisibleKeys` / `Beta.VisibleKeys`.

```
R064 before=8 model=9 alpha=8 beta=8 alphaBefore=8 betaBefore=8
```

Cause: `DemoPanelViewModel` builds its `visible` list and its item bindings only in `Sync()` (`:81-106`),
which a model change does not call; the model's `ItemsRevision` announcement reaches the window through the
adapter's `ItemsKey` mapping, whose getter reads the already-built list. The new row exists in the model and
is absent from both panels' item key sets.

**Verdict: reproduced, P2 is right.** No crash, but the two views silently disagree with the model.

## R06-5 — REOPEN: a broken layout file throws out of the window factory (P2)

**Reviewer's claim.** `REOPEN: FormatException Invalid UI layout XML`.

**My fixture** (`r065`): copy the packaged demo `Xml` into a temp mod root, reload the panel document
successfully, then truncate `Xml/Panel.xml` to `<UiPage`.

```
R065 serviceValid=accepted=False skipped=True reason=the bytes are unchanged since the last accepted version
R065 serviceBroken=accepted=False skipped=False reason=file '<temp>/Xml/Panel.xml' did not parse: Invalid UI layout XML at line 1, position 8: 分析 Name 时，出现意外的文件结尾。
R065 docs.Layout(broken) -> FormatException: Invalid UI layout XML at line 1, position 8: ...
R065 page.OpenPanel(broken panel) -> FormatException: Invalid UI layout XML at line 1, position 8: ...
R065 page.OpenPanel(repaired panel) -> no exception
```

**What this shows.** The service side is behaving: a broken candidate is refused and the last valid version
stays in force (R06-5's reload is refused, `skipped=False`, reason recorded). The demo side is not:
`DemoDocuments.Layout` (`:41-45) and `Style` (`:48-52) read the external file directly with
`UiLayoutManifest.ParseFile` / `UiStyleDocument.ParseFile` whenever it exists, and `DemoSettingsPage.CreatePanel`
(`:193`) calls that **before** the window's `HostAttached` is raised, so the service's last-known-good never
gets a chance and the window cannot open at all. Repairing the file makes the very next open succeed.

**Verdict: reproduced, P2 is right** (the reviewer's P2, not higher: it needs a broken or half-written file).
The reviewer's one-line label is accurate, and it is worth saying explicitly that this is **not** a library
defect — `UiDocumentService` handled the same broken bytes correctly.

## R06-6 — IDENTITY/COLLISION: two legitimate scopes produce one row key (P2)

**Reviewer's claim.** `review_a__kind` collision -> `Duplicate value binding
'consumerKinds.review_a__kind.owner'`.

**My fixture** (`r066`): register `("review-a", "kind")` and `("review_a", "kind")` through the public
registry, read `DemoCatalog.Read()`, then construct the page.

```
R066 key1=review_a__kind key2=review_a__kind equal=True
R066 page ctor -> InvalidOperationException: Duplicate value binding 'consumerKinds.review_a__kind.owner'.
```

Cause, read from the demo: `DemoCatalogEntry.Key = Sanitize(scope) + "__" + Sanitize(kind)` and `Sanitize`
replaces every non-alphanumeric with `_` (`:18, `:38-50`). Two distinct declared pairs therefore collapse to
one row key, and `BindConsumerRows` binds the per-row keys twice. **It is a demo identity defect, not a
library one**: `UiWidgetCatalog` keeps both pairs distinct (my fixture lists both), so the library's
`(scope, kind)` identity is intact and only the demo's row key loses information.

**Verdict: reproduced, P2 is defensible.** The trigger needs two scopes that sanitize identically (punctuation
vs. underscore), which ordinary registrations avoid — but when it happens the whole settings page fails to
construct, not just one row. I would not argue for a higher severity, and I would say in the fix record that
the consequence is page-level.

## R06-7 — LATE: a late registration never reaches the page (P2)

**Reviewer's claim.** `live=1 page=0`.

**My fixture** (`r067`): construct the page, count core-scope entries in `page.Catalogue` and in
`UiWidgetCatalog.Snapshot()`, register one new core kind through the public registry, count both again.

```
R067 liveBefore=16 liveAfter=17 liveDelta=1 pageBefore=16 pageAfter=16 pageDelta=0
```

Cause: `DemoSettingsPage.BindPage` takes the catalogue snapshot once (`:212` `catalog = DemoCatalog.Read()`)
and `RebuildCatalogBindings`/`BindConsumerRows` build the tab's key lists from it; the page object is cached by
the mod (`FerriteLibUiKitDemoMod.cs:43`), and no tab/scope action re-reads the registry — the scope actions
only re-filter the same captured list (and, per R06-3, throw). The library surface does see the new kind
(`liveDelta=1`), so the round's "late registration is visible without a restart" contract holds for the API and
not for this consumer page.

**Verdict: reproduced, P2 is right.** No data loss; the demo simply cannot show what registered after it
opened.

## Severity and overstatement summary

| item | reviewer severity | my verdict | note |
| --- | --- | --- | --- |
| R06-1 | P1 | **agree** | the demo mod never loads; not a degraded feature but an absent one |
| R06-2 | P1 | **agree** | silent, and it contradicts the contract the fix task must now define |
| R06-3 | P1 | **agree** | first filter click throws |
| R06-4 | P2 | **agree** | stale views, no crash |
| R06-5 | P2 | **agree** | requires a broken/half-written file; the library side is correct |
| R06-6 | P2 | **agree** | trigger needs punctuation-colliding scopes; consequence is page-level |
| R06-7 | P2 | **agree** | the library sees the late kind; the demo page does not |

Nothing the reviewer reported turned out to be wrong or overstated. Two clarifications worth recording:
R06-5 and R06-6 look like library problems from their one-line symptoms but are demo-side (the service refuses
the broken candidate correctly, and the library identity keeps both scopes); R06-2's window is precisely "a
signal with nothing attached and no pump before the next attach", so a reader should not generalise it to
"reopen always loses updates".

## What I did not verify

- **In-game behaviour of every item.** My probe drives the delivered demo and library assemblies against the
  harness's game **stubs** (`Verse.WindowStack`, `Verse.Mod`, `Widgets`, `UnityEngine.*`, translation and text
  measurement). It proves the demo/library logic shapes; it does not prove rendering, the real window stack,
  the real `ModSettings` shell, or real text measurement. For R06-2 I injected a constant-height
  `ITextMetrics` stub rather than the production metrics.
- **R06-1 at runtime**: the derived rule and the manifest computation are static; I did not launch the game to
  observe the missing settings entry or FL's successful load.
- **The rest of the review's risk list** (the re-entrancy item on its own task, and anything else the reviewer
  raised) — out of this task's scope.
- **The fixes**: this is the pre-fix baseline; the fix tasks were running in parallel and nothing here is a
  statement about the fixed tree.
