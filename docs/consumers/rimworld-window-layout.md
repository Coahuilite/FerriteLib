# Build a window that fits the player's screen

Use the game's **logical UI space**, then choose a layout that fits the window's **actual content area**.
A 1920×1080 display does not give a page 1920×1080 units, and a wide aspect ratio does not guarantee enough
height for a two-column settings panel.

This guide covers FerriteLib.UiKit **0.8.0-rc1** and the **RimWorld 1.6 adapter**. It brings together the
window and scaling findings recorded on 2026-10-25. For wiring a page, use the
[ordinary settings recipe](ordinary-settings.md); for supported signatures and ownership, use
[S6 in the public contract](../development/0.8/public-contract.md#s6--use-a-standard-window-or-embed-in-an-existing-callback).

## Start with the logical viewport

RimWorld's `Verse.UI.ApplyUIScale` obtains its logical dimensions from the physical display and
`Prefs.UIScale`. At 1× they are the physical dimensions; otherwise it rounds each divided dimension:

```text
logical width  = RoundToInt(physical width  / UI scale)
logical height = RoundToInt(physical height / UI scale)
```

The game also applies the scale to GUI drawing and translates pointer coordinates. Native adapter code
should use `UI.screenWidth`, `UI.screenHeight` and the provided rectangle. Neutral page code should use
its allocated layout bounds. Do not scale those bounds or pointer coordinates a second time.

### The settings menu rejects some scale choices

`ResolutionUtility.UIScaleSafeWithResolution` requires **both** unrounded dimensions to pass:

```text
physical width / scale >= 1024  AND  physical height / scale >= 768
```

The built-in choices are 1×, 1.25×, 1.5×, 1.75×, 2×, 2.5×, 3×, 3.5× and 4×. In the inspected 1.6
`Dialog_Options`, they remain listed; choosing an unsafe scale other than 1× produces a rejection rather
than applying it. Selecting another resolution also checks the current scale. The 1× option provides
a way back. Do not describe an unsafe listed option as a supported configuration.

The resolution list is obtained from the device's modes, deduplicated and filtered to at least
1024×768. It is not a fixed list shared by every installation. For the modes examined in this study,
the largest accepted built-in scale is:

| Largest accepted scale | Physical resolutions |
| --- | --- |
| 1× | 1024×768; 1152×864; 1280×768; 1280×800; 1360×768; 1366×768; 1600×900 |
| 1.25× | 1280×960; 1280×1024; 1440×1080; 1600×1024; 1680×1050; 1920×1080 |
| 1.5× | 1600×1200; 1920×1200; 2048×1152 |
| 1.75× | 1920×1440; 2560×1440 |

These values were calculated from the source rule and the listed choices. They are not recommended
scales: `GetRecommendedUIScale` uses a separate, more spacious target of 1700×910 logical units.

Three useful consequences:

- **1920×1080 at 1.25× gives 1536×864 logical units.** The next choice, 1.5×, fails the height rule.
- **1280×960 at 1.25× gives 1024×768.** A physically larger display can need the minimum-size layout.
- **1600×900 accepts only 1×.** Its width does not compensate for insufficient height at 1.25×.

The menu rule describes accepted menu choices, not a guarantee about every transient viewport produced
by OS resizing, configuration edits or other mods. Fit the actual rectangle you receive. Do not change
the player's global scale to rescue a page that is too large.

## Fit the outer window, then measure the page

The 1.6 `UiWindowHost` owns the chrome, native execution and one page host. Window size and modality are
consumer policies:

- A subclass can override `InitialSizePolicy`; leaving it null preserves `Verse.Window.InitialSize`.
- A catalog-registered `UiPageWindow` takes `UiWindowOptions.InitialSize`. A captured fixed size is not
  a dynamic screen-sizing policy.
- `ForcePause`, `PreventCameraMotion`, `AbsorbInputAroundWindow`, dragging, resizing and close behavior
  are chosen by the consumer. Nullable options leave the game's existing value unchanged.
- `NormalSize` is reapplied on opening; it is not a responsive layout rule or continuous fit service.

In the inspected native source, `Window.SetInitialSizeAndPosition` centers the supplied initial size.
It does not clamp an oversized initial rectangle to the display. `Notify_ResolutionChanged` calls that
placement method again. Prefer this existing lifecycle path to a second per-frame resize controller;
verify notification and confirmation rollback on the actual target build.

Bound the requested window to the current logical viewport with a deliberate outer margin. Then use
the actual page rectangle to choose its arrangement. The current shell's default title band is 56 units
and its left/right/bottom inset is 20 units. Thus an outer `W × H` initially leaves `W−40 × H−76` for the
page, before page-specific padding. These are overridable chrome values, not space a page can reclaim
by drawing underneath the title.

For current geometry, use `UiWindowHost.ShellChrome` rather than duplicating chrome constants.
Its rectangles are in logical window/screen space, not page-local coordinates. For a completed Dev
observation, use that pass's `UiDevGeometrySnapshot.ShellChrome`; attaching a later live measurement to
an earlier pass can misreport a moved or resized window. The observation surface's tier and limits remain
those of the [public contract](../development/0.8/public-contract.md).

## Choose a layout breakpoint from the content it must hold

Use two arrangements of the same task, not a smaller-font copy of a large panel:

- **Wide:** a primary list and supporting detail can share the page when each retains enough width.
- **Compact:** keep the primary action and search visible; open detail in the same content area with a
  clear return path. Keep selection, filters and list position when returning.

For example, a consumer might budget 620 units for its list, 440 for details and 16 between them, then
start evaluating a **1100-wide, 640-high page-content breakpoint**. Those are design numbers, not FL
defaults or a validated minimum. The height criterion prevents a short, wide window from losing its
useful viewport to fixed controls. Decide the mode before arranging the common header, so header wrapping
does not create a feedback loop that repeatedly changes the mode.

The existing page engine provides the pieces:

- `Row` with `Breakpoint` and `Narrow="Column"` handles a width-driven stack.
- `Wrap` with `Cols`/`NarrowCols` provides finite wrapping; those column attributes belong to `Wrap`.
- `Column`, `Scroll`, `Clip`, measured wrapped text and min/max/fill sizing provide readable overflow.
- Typed bindings and conditional groups can express a consumer-owned compact mode or detail view.

The manifest's width breakpoint is not a built-in two-axis media query. If height changes the product's
mode, compute that mode at the consumer's layout boundary and compose it using the supported page
mechanisms. No new host, global state machine or UI toolkit is needed. Keep compatible node identities
and follow [definition-update](../development/0.8/public-contract.md) and
[editing](../development/0.8/editing-contract.md) rules. Removing a node and creating a different editor
is not the same as retaining its draft while hidden.

Put low-frequency controls on a secondary page or in a conditional group. When filters are collapsed,
leave their active state and a clear action visible. Use long help in the content area instead of
permanently subtracting a large help band from every page. Neither policy needs a custom widget.

## Keep one window when several entry points open it

Register a stable `UiWindowKey` and use `UiWindowCatalog.Open(key)` for repeated access. It creates a
window only when that key is absent; otherwise it activates the existing instance, retaining its session,
scroll and drafts. Do not implement “open settings” by closing the current window first.

Two native distinctions matter:

1. **Catalog activation is not bring-to-front.** The current adapter calls
   `WindowStack.Notify_ManuallySetFocus`; it does not reorder the native window stack. Check that the
   intended window is actually visible and input-eligible when another window covers it. Catalog
   `FollowClicks` uses its activation order for overlapping catalog windows, not a query of native
   stacking order.
2. **Visibility is not focus.** `SetDisplayDemand` expresses reliable display demand, not modality or
   input ownership. Keep native qualification in the adapter. Closing disposes the host; hiding and
   switching a page are different lifecycle operations.

Keep `AllowMultipleInstances=true` for a shared `UiPageWindow` class unless you deliberately want the
game's exact-C#-type eviction rule. Catalog keys deduplicate your intended instance; setting this flag
false can also evict another kind that uses the same shell class.

Attach model subscriptions through `HostAttached`, detach through `HostDetached`, and keep business
save policy in the consumer. A click, a setter request, a successful publication and a disk save are
different events; a library editor cannot decide what counts as a successfully saved product setting.

## Make native entry points discoverable without replacing native menus

A startup-menu item, an Options category and an in-game bottom main button are different surfaces.
Name the intended surface before selecting a patch. FL supplies the window, not automatic registration
in those native menus.

The inspected 1.6 source supplies two useful constraints:

- `MainMenuDrawer.DoMainMenuControls` builds `List<ListableOption>` and passes it to
  `OptionListingUtility.DrawOptionListing`. Prefer a narrowly scoped integration over copying the whole
  main-menu method. Do not intercept unrelated listings solely because they use the same helper.
- `Dialog_Options.PostOpen` reports unofficial `OptionCategoryDef` entries, and its draw path filters
  categories to official content. Adding such a Def alone will not create a working custom category.
  Do not misidentify a mod as official to bypass that rule.

Keep added entries optional when they alter vanilla navigation. A useful default is visible, with
independent visibility settings and the ordinary Mod Settings route retained for recovery. Opening from
the startup menu must not require a loaded map or world. Check translated labels against the real native
column width; a long product name is not a reason to widen or replace the whole Options window.

## Use counterexamples to check the design

| Tempting shortcut | Counterexample | Check that changes the design |
| --- | --- | --- |
| Choose wide mode from aspect ratio | 1366×768 is wide but short | Keep a usable primary-content viewport after header and tool rows. |
| Choose mode from physical resolution | 1280×960 at 1.25× is logically 1024×768 | Measure the allocated logical content rectangle. |
| Use height to justify two columns | A tall 1280×1024 screen can still leave narrow columns | Measure both columns with long translated labels. |
| Shrink every control to fit | The minimum-size layout becomes hard to read and click | Recompose the task; preserve normal font and input sizes. |
| Reopen the window on a mode change | An active edit, search or list position disappears | Cross the breakpoint with state present, retaining the host and compatible identities. |
| Assume activating a catalog key raises the window | A native dialog still covers it | Exercise the real window stack, not only the catalog's active flag. |
| Treat a canceled resolution change as finished | The old scale/viewport returns | Verify the restored window geometry and popup/input positions. |

Inspect the rendered page with representative long labels, nonempty lists, an open detail and a visible
status/error message. Exercise the primary action, return, close and an active editor. Check popup
anchoring and capture when the geometry changes; do not force a draft commit merely to change layouts.
Choose cases that can expose these failures rather than multiplying every resolution by every language
and scale.

## Evidence and maintenance

The platform findings above come from these named source methods, not a screenshot-derived rule:

| Evidence | Scope |
| --- | --- |
| RimSage 1.6 source: `ResolutionUtility`, `Dialog_Options`, `UI.ApplyUIScale`, `Window` and `MainMenuDrawer` | Platform mechanisms and extension points; source dataset identified as 1.6.4850. |
| Target assembly `1.6.9676.17735`: focused inspection of `ResolutionUtility`, `Dialog_Options`, `MainMenuDrawer` | Confirms the scale predicate, options and menu/category constraints on the target 1.6.4871 build. |
| FL source at RC commit `a0b5b983c3c13a099c62d936305eaebaf1deb5a8` | Window ownership, chrome, catalog and page capabilities. |
| Executed calculations from the scale predicate | Accepted scale table and logical-size examples; not native render acceptance. |

The target-game window, font, IME, overlap and scaling observations for 0.8 remain open. Record the actual
candidate, resolution, scale, language, page state and observed result when a game pass is performed.
Keep a design breakpoint labeled as a proposal until its rendered layout and key interactions have been
checked. Earlier-version human observations do not validate a new adapter or candidate.

Maintain this guide in the same change as affected window or adapter behavior. Update the scale table
when the game's predicate or choices change; update the capability guidance when the public contract
changes; add measured consumer findings only with their conditions. Keep application-specific defaults
in the application, not in FL's defaults.

Relevant implementation: [window host](../../Source/FerriteLib.UiKit/Adapters/RimWorld16/UiWindowHost.cs),
[catalog](../../Source/FerriteLib.UiKit/Adapters/RimWorld16/UiWindowCatalog.cs),
[options](../../Source/FerriteLib.UiKit/Adapters/RimWorld16/UiWindowOptions.cs),
[page window](../../Source/FerriteLib.UiKit/Adapters/RimWorld16/UiPageWindow.cs) and
[layout engine](../../Source/FerriteLib.UiKit/Kernel/UiLayoutEngine.cs).
