# T2 — automatic reload scheduling: evidence record

Owner: `reload`. Branch `feat/0.6-reload` (forked from `02a6aea`).
Commits: `f8d6d62` (implementation + lanes + the two clock-driven 0.5 lane files), plus the commit that
carries this record.

**Evidence class: 已实现 / 已自动化验证.** Nothing here is 实机验证. The in-game timing item - a real editor's
save reaching the scheduler, a real drag inside a real window, and the commit point running inside the game's
own GUI pass - stays **尚待外部团队验证** and is listed in §5.

The public signatures are the frozen ones from `docs/development/0.6/05-api-contract.md`. No public member
was added or changed, so no tier edit was needed; `FerriteLibApiTierTests` re-pins the surface unchanged.

---

## 1. Vanilla commit-point evidence

Read from the game's own source with the source index (queries such as `class WindowStack`, `WindowsUpdate()`,
`ShouldWaitForEvent`). Paths below are the game's source paths; the excerpts are transcription, not paraphrase.

**Name correction first.** There is no `Window.OnGUI` in this codebase. The per-window virtual is
`WindowOnGUI()` (`Verse/Window.cs:183`), and the private indirection that reaches the page body is
`InnerWindowOnGUI(int)` (`Verse/Window.cs:205`). Anything that reasons from the name `OnGUI` is reasoning
from a method that does not exist.

### 1.1 The frame path: update

`Verse/Root.cs:120` `public virtual void Update()`:

```csharp
LongEventHandler.LongEventsUpdate(out var sceneChanged);      // :123
if (sceneChanged)
{
    destroyed = true;
}
else if (!LongEventHandler.ShouldWaitForEvent)                // :128
{
    ...
    uiRoot.UIRootUpdate();                                    // :138
    ...
}
```

`Verse/UIRoot.cs:73`:

```csharp
public virtual void UIRootUpdate()
{
    ScreenshotTaker.Update();
    DragSliderManager.DragSlidersUpdate();
    windows.WindowsUpdate();          // :77
    MouseoverSounds.ResolveFrame();
    UIHighlighter.UIHighlighterUpdate();
    Messages.Update();
    CellInspectorDrawer.Update();
}
```

`Verse/WindowStack.cs:171`:

```csharp
public void WindowsUpdate()
{
    AdjustWindowsIfResolutionChanged();
    for (int i = 0; i < windows.Count; i++)
    {
        windows[i].WindowUpdate();     // :176
    }
}
```

`Verse/Window.cs:125` `public virtual void WindowUpdate()` - the base body only maintains
`sustainerAmbient`; everything else is a subclass override.

The simulation tick is a different call on a different branch: `Verse/Root_Play.cs:61` `public override void
Update()` calls `base.Update()` and then `Current.Game.UpdatePlay()`, which calls
`tickManager.TickManagerUpdate()` (`Verse/Game.cs:670`). `Verse/TickManager.cs:318`:

```csharp
public void TickManagerUpdate()
{
    ticksThisFrame = 0;
    if (Paused)                        // :321
    {
        return;
    }
    ...
}
```

`Verse/TickManager.cs:130` `public bool Paused` is `curTimeSpeed == TimeSpeed.Paused || ForcePaused`, and
`ForcePaused` includes `Find.WindowStack.WindowsForcePause` (any window with `forcePause`) plus
`LongEventHandler.ForcePause`.

**What this proves for T2:** pause is a gate on `TickManagerUpdate` only. `uiRoot.UIRootUpdate()` sits in the
other branch (`!LongEventHandler.ShouldWaitForEvent`), so a paused game keeps updating and drawing windows
while the simulation clock is frozen. A scheduler whose clock is `Time.realtimeSinceStartup` therefore still
notices a saved file while paused. It also identifies the one thing that *does* stop the pump: a waiting long
event.

### 1.2 The frame path: GUI

`Verse/Root.cs:148` `public void OnGUI()`:

```csharp
LongEventHandler.LongEventsOnGUI();
if (LongEventHandler.ShouldWaitForEvent)          // :164
{
    ScreenFader.OverlayOnGUI(new Vector2(UI.screenWidth, UI.screenHeight));
    return;
}
uiRoot.UIRootOnGUI();                             // :169
```

`Verse/UIRoot.cs:38` `public virtual void UIRootOnGUI()` runs the cross-window input pass at `:52`
`windows.HandleEventsHighPriority();` and then the play override continues:
`RimWorld/UIRoot_Play.cs:25` `public override void UIRootOnGUI()` calls
`windows.WindowStackOnGUI();` at `RimWorld/UIRoot_Play.cs:43`.

`Verse/WindowStack.cs:205`:

```csharp
public void WindowStackOnGUI()
{
    windowStackOnGUITmpList.Clear();
    windowStackOnGUITmpList.AddRange(windows);
    for (int num = windowStackOnGUITmpList.Count - 1; num >= 0; num--)
    {
        windowStackOnGUITmpList[num].ExtraOnGUI();          // :211
    }
    UpdateImmediateWindowsList();                           // :214 - Repaint only
    ...
    for (int i = 0; i < windowStackOnGUITmpList.Count; i++)
    {
        if (windowStackOnGUITmpList[i].drawShadow)
        {
            ...
            Widgets.DrawShadowAround(windowStackOnGUITmpList[i].windowRect);
        }
        windowStackOnGUITmpList[i].WindowOnGUI();           // :228
    }
    ...
}
```

`UpdateImmediateWindowsList` returns unless `Event.current.type == EventType.Repaint`; the window draw loop
does not - it runs for **every** IMGUI event of the frame (Layout, Repaint, MouseDown, KeyDown, ScrollWheel,
...), one `OnGUI` invocation per event.

`Verse/Window.cs:183`:

```csharp
public virtual void WindowOnGUI()
{
    ...
    windowRect = GUI.Window(ID, windowRect, innerWindowOnGUICached, "", windowDrawing.EmptyStyle);   // :202
}
```

`GUI.Window` invokes the callback during that same event; `Verse/Window.cs:205` `private void
InnerWindowOnGUI(int x)`:

```csharp
Find.WindowStack.currentlyDrawnWindow = this;
...
if (Event.current.type == EventType.MouseDown)
{
    Find.WindowStack.Notify_ClickedInsideWindow(this);       // :222-225
}
...
windowDrawing.BeginGroup(rect3);
try
{
    DoWindowContents(rect3.AtZero());                        // :258
}
catch (Exception ex)
{
    Log.Error("Exception filling window for " + GetType()?.ToString() + ": " + ex);
}
windowDrawing.EndGroup();
LateWindowOnGUI(rect3);
...
```

`Verse/Window.cs:133` `public abstract void DoWindowContents(Rect inRect);`.

### 1.3 Per-window lifecycle

`Verse/WindowStack.cs:430` `public bool TryRemove(Window window, bool doCloseSound = true)`:

```csharp
if (!window.OnCloseRequest())        // :445
{
    return false;
}
if (doCloseSound && window.soundClose != null)
{
    window.soundClose.PlayOneShotOnCamera();
}
window.PreClose();                   // :447
windows.Remove(window);              // :448
window.PostClose();                  // :449
```

The `Window` virtual hooks are `PreOpen` (:139), `PostOpen` (:158), `OnCloseRequest` (:170),
`PreClose` (:175), `PostClose` (:179). `WindowStack.Add` calls `PreOpen()` before inserting and
`PostOpen()` after.

### 1.4 Where this library commits

`Source/FerriteLib.UiKit/Kernel/UiHost.cs` `BeginFrame()` calls `documentService?.Pump()` before
`session.BeginFrame()`, and `DrawFrame` wraps `BeginFrame` → `MeasureAndArrange` → `Draw`. A consumer
renders a page from `Window.DoWindowContents`. So the library's only commit point is:

`DoWindowContents` → `UiHost.DrawFrame` → `BeginFrame` → `UiDocumentService.Pump`

nested inside `InnerWindowOnGUI` (`Verse/Window.cs:258`) inside `WindowStackOnGUI`
(`Verse/WindowStack.cs:228`) inside `UIRootOnGUI` (`Verse/UIRoot.cs:38` / `RimWorld/UIRoot_Play.cs:43`)
inside `Root.OnGUI` (`Verse/Root.cs:169`).

**This is the finding that matters, and it is not what the old method name suggested.** There is no mod-owned
hook in `Root.Update` and no "between frames" gap the library can commit in. The commit happens *inside* an
arbitrary IMGUI event pass, at the beginning of the host's own draw.

### 1.5 What the ordering proves

1. The commit is main-thread. Every frame in the chain above is the Unity main thread, and so is every
   `DoWindowContents`. `Pump` never reads a file off-thread; the watcher thread only posts.
2. For the host that pumps, the pass is coherent: the tree is swapped before that same call's
   `MeasureAndArrange`/`Draw`, and the pass that finished in the previous event is not mutated under itself.
3. A waiting long event stops the pump completely: `Root.cs:128` skips `UIRootUpdate` and `Root.cs:164`
   returns before `UIRootOnGUI`. Files keep changing and the watcher keeps signalling, but nothing is read
   until the event clears; the pending set coalesces per document and the quiet window opens on the first
   frame after. Recovery is also the manual `Reload` path.
4. Pause does **not** stop it: see §1.1. This is the ordering half of "pause independence"; the behavioural
   half is lane (4) in §3.

### 1.6 What the ordering does NOT prove

- **Sibling windows are not atomic with each other.** `Pump` is one object shared by every attached host and
  commits every due document in one call. `WindowStackOnGUI` draws index 0..N-1, so a window drawn earlier in
  the same event can have drawn the old tree while a window drawn later commits the new one. Each window's own
  pass is coherent; the event as a whole is not, and no lane plants that.
- **Layout/Repaint consistency is untested for a GUILayout consumer.** The tree may change between the Layout
  pass and the Repaint pass. The engine only balances `GUI.BeginGroup`/`EndGroup` inside one pass and every
  measured call shape is Rect-based (no GUILayout), so the library's own path is safe; a consumer that mixes
  GUILayout into a page is exposed and nothing here covers it.
- **Re-entrancy through consumer lifecycle overrides is not analysed.** `TryRemove` calls
  `OnCloseRequest`/`PreClose`/`PostClose` at `WindowStack.cs:445/447/449`, and this library's own hooks run
  inside them (the window catalog's lifecycle, T1's `HostAttached`/`HostDetached`). A consumer whose
  `PostClose` (or a diagnostics handler) calls `Pump`/`Reload` re-enters `ReloadCore` from inside
  `TryRemove`'s list mutation - `windows.Remove(window)` at :448 has already run. `TryRemove` may itself be
  reached from `CloseWindowsBecauseClicked`/`UpdateImmediateWindowsList`, which iterate snapshots
  (`closeWindowsTmpList`, `updateImmediateWindowsListTmpList`), so the *list* iteration is snapshot-safe; the
  *service* re-entrancy is unverified and unpinned. **UNVERIFIED-IN-GAME.**
- **The watcher's real latency is not proven.** A `FileSystemWatcher` event is asynchronous by nature; the
  lane models the signal, not the editor. The in-game half stays external.
- **The real IMGUI hot-control lifetime is not proven.** The deferral lane drives
  `UiSession.CaptureHotControl` on the stub; a real drag's capture/release cadence is not the stub's.

---

## 2. The scheduler contract these lanes pin

- **Two channels.** `Signal(id)` is the watcher channel and is debounced; `Reload(id)`/`ReloadAll()` are the
  manual channel and are immediate. Both end in the identical candidate/validate/commit path (same parser,
  same per-host pre-check, same atomic batch and rollback).
- **Quiet window is a trailing edge.** A later observed signal restarts it. The window does not open at the
  file change; it opens when the main-thread `Pump` first observes the signal, because the
  `FileSystemWatcher` thread may touch neither the clock nor Verse/Unity. Effective delay is
  `[QuietSeconds, QuietSeconds + one pump interval]`.
- **Bounded retry.** A transient read failure (sharing violation, momentary absence) is held and retried at
  `RetrySeconds`; the attempt past `MaxRetryAttempts` retries is the one reported. A failure waiting cannot
  fix (malformed XML, oversized file) is reported on the first attempt. The previous valid version stays in
  force throughout, and nothing re-attempts without a new signal or a due retry window.
- **Bounded deferral.** A ready commit is deferred while an affected attached host is interacting, and the
  ceiling is absolute: `MaxDeferSeconds` past the moment the document became ready, the commit is forced
  through (and releases the held capture). `SchedulerState.Deferred` is the visible reason.
- **Nothing open, nothing rebuilt.** With zero attached hosts `Pump` observes signals and reads nothing; the
  next `Attach` resolves the newest valid content on the explicit path.
- **Time is only `IUiTimeSource`.** There is no frame counter, host counter or pump counter anywhere in the
  scheduler, and the fail-loud lane below pins that against the harness's constant-zero stub clock.

---

## 3. Lanes and mutation status

Every mutation below was planted in a scratch state of the committed tree, the harness was rebuilt and run,
and the tree was restored; `git status --porcelain` was clean afterwards.

| # | Claim | Lane | Planted defect → red | Status |
| --- | --- | --- | --- | --- |
| 1 | Vanilla commit point relative to update/GUI/layout/repaint/input, and the pause/long-event gates | *(source reading, no lane)* | — | transcribed in §1; unpinnable by a harness lane |
| 2 | A burst inside QuietSeconds → one read and one commit; a later signal restarts the window | `VerifyQuietPeriodAndTrailingEdge` | **M1** drop the quiet condition → 23 red, incl. *"nothing is read before the window opens"*, *"the host still draws the old tree"*; **M2** stop stamping `LastObservedSeconds` on a repeat observation (leading edge) → 2 red, *"a later signal restarts the window: t=0.30 is past the original 0.25 deadline and still commits nothing"* | **mutation-proven** |
| 3 | Transient read failure retried at most `MaxRetryAttempts` at `RetrySeconds`, then reported; previous version in force; no spin | `VerifyBoundedRetry`, `VerifyZeroRetriesReportsImmediately` | **M3** pass `deferTransientFailure: false` → 4 red, *"attempt 1 (a locked file) is held as one retrying document"*, *"the transient read failure is not reported while retries remain"*; **M4** hold every transient failure forever → 5 red, *"the first attempt past the retry budget (attempt 3 = 1 + MaxRetryAttempts) is reported as a failure"* | **mutation-proven** |
| 4 | A commit happens while the simulation clock is frozen | `VerifyPauseIndependence` | **M8** make `NowSeconds()` ignore `IUiTimeSource` (return 0) → 67 red overall, incl. *"the commit happens anyway: the scheduler's clock is real time, not the simulation clock"* | **mutation-proven against the clock source**; the pause *shape* is a fixture assertion (§5) |
| 5 | With zero attached hosts nothing is read or parsed; the next attach resolves the newest content | `VerifyNothingOpenIsNotRebuilt` | **M5** remove the `dependencies.Count == 0` guard → 3 red, *"with zero attached hosts the service reads and parses nothing ... (reports=1)"* | **mutation-proven** |
| 6 | A ready commit is deferred for interaction, never past `MaxDeferSeconds`, and says so | `VerifyBoundedDeferral`, `VerifyStateSeparatesQuietFromDeferral` | **M6** remove the deferral ceiling → 4 red, incl. *"past the ceiling the commit is forced through"* and the adapted P4 *"a reload releases the hot control instead of leaving it held"*; **M11** count deferred as pending → 4 red, *"SchedulerState names the reason - deferred, not pending"* | **mutation-proven** |
| 7 | Per-document atomicity survives: layout+style in quick succession; R1's rolled-back batch loses no draft; R7's single snapshot still holds | `VerifyLayoutAndStyleInQuickSuccession`, `VerifySchedulerRollbackKeepsDraft` + the unchanged P4 `VerifyRolledBackBatchKeepsInteractionState` and `VerifySourceWiring` | **M9** process one due document per pump → 3 red, *"the style version reached the same host"*, *"the unrelated document's batch commits in the same pump"*; **M10** run `SealDocumentCommit` before a rollback (the R1 defect) → 4 red, *"and R1's draft survives the scheduler-driven rollback"*, *"and its uncommitted draft survived the rollback"* | **mutation-proven** |
| 8 | The two channels: watcher debounced, manual immediate, same commit path | `VerifySignalIsDebouncedAndReloadIsImmediate` | **M7** make `Reload` return null → 48 red, incl. *"Reload commits the same bytes at once"* and every manual-recovery lane | **mutation-proven** |
| 9 | With a constant-zero clock, pumping alone never commits (a lane that forgets the clock fails loudly) | `VerifyZeroClockNeverCommitsByPumping` | **M1** (quiet ignored) commits on the first pump → red *"with the constant-zero stub clock, pumping 25 times commits nothing"*; **M8** likewise | **mutation-proven** |

Honest limits of the mutation evidence:

- **"One read" is measured as one report.** The lane asserts `Reports.Count == 1` after the commit; an
  implementation that read the file a second time would publish a `skipped` report for the unchanged bytes and
  the count would rise. That is a strong proxy, not a direct count of `File.ReadAllBytes` calls. The R7
  single-snapshot lane (`VerifySourceWiring`) remains the lexical guard for the read shape, with its own
  planted controls.
- **"No re-parse with zero hosts" is measured the same way.** A read with no affected host always records an
  accepted report (*"accepted; no live host depends on this document yet"*), so an empty ring proves no read
  happened; it is still not a call counter.
- **The pause lane's simulation-clock half is a shape, not a defect plant.** The service has no simulation
  input to break, so the only plantable defect is breaking the clock source, which M8 does. The lane asserts
  the fixture is frozen (paused, 0 ticks) and that the commit still happens; it cannot prove a real game is
  paused.
- **`M6` also reddens the adapted P4 hot-control lane**, so that lane is now coupled to the deferral-ceiling
  contract (it holds a capture and relies on the ceiling to reach the commit). Recorded rather than hidden.

---

## 4. The two adapted 0.5 lane files

Neither file could keep its old timing: automatic signals are now debounced, and the harness's
`Time.realtimeSinceStartup` stub is a constant zero, so `Signal(); Pump();` no longer commits - by design.
Both files therefore inject a manual `IUiTimeSource` and replace the call with an observe/advance/commit
helper. **No assertion was removed, weakened or reworded.**

| File | Before `02a6aea` | After `f8d6d62` |
| --- | --- | --- |
| `tools/FerriteLib.UiKit.Tests/KernelDocumentReloadTests.cs` | 24 lanes / 175 `Check(` sites | 24 lanes / 175 `Check(` sites |
| `tools/FerriteLib.UiKit.Tests/KernelDiagnosticsTests.cs` | 15 lanes / 92 `Check(` sites | 15 lanes / 92 `Check(` sites |

The only behavioural consequence for an existing assertion: `VerifyHotControlReleased` holds a hot control
across the reload, so it now passes through the **deferral ceiling** (the helper advances 3.0s, past the 2.0s
ceiling) before asserting the release and the kept draft. Its assertions are textually unchanged; its timing
path is the new contract. M6 reddens exactly that lane, which is the honest coupling.

The R7 single-read pipeline and R1 draft-preservation assertions are unchanged in substance and still pass;
M10 shows the R1 assertion still fires on the real defect.

---

## 5. What is NOT verified

1. **In-game timing.** That a real editor save reaches `Signal`, that the commit runs inside a real
   `DoWindowContents`, that a real drag's hot control defers and the ceiling forces it, and that a real
   paused game keeps pumping frames. **尚待外部团队验证.** §1 is a source reading; no lane can make a game
   run.
2. **The simulation-clock half of pause independence.** The lane proves the scheduler's only time input is
   `IUiTimeSource` and that a frozen simulation shape does not stop it; it cannot prove the game is paused.
3. **Sibling-window event atomicity, GUILayout Layout/Repaint consistency, and consumer pre/post-close
   re-entrancy** - §1.6. No lane plants any of them.
4. **IME composition coverage for the deferral signal.** `UiHost.IsInteracting` is
   `session.OwnedHotControl != null` - the library's own IMGUI capture. A text field that is merely focused,
   a keyboard-navigated control, and an OS-level IME composition hold no library-owned capture and are not
   observed; those shapes are bounded only by `MaxDeferSeconds`. Stated as a boundary, not claimed as
   coverage.
5. **Direct file-read counts.** §3 limits: the "one read" and "no re-parse" claims are report-count proxies.
6. **Cross-machine watcher behaviour.** `FileSystemWatcher` coalescing/dropping is exercised only through
   `Signal`; the `overflowed` recovery path is pre-existing and not re-laned here.
