# Build a window that fits the player's screen

Use the game's **logical UI space**, then choose a layout that fits the window's **actual content area**.
A 1920×1080 display does not give a page 1920×1080 units, and a wide aspect ratio does not guarantee enough
height for a two-column settings panel.

This guide covers FerriteLib.UiKit **0.8.0-rc1**, the separately identified local fixes below, and the
**RimWorld 1.6 adapter**. It brings together the window, scaling and desktop interaction findings recorded
on 2026-10-25. For wiring a page, use the
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

- `Row` with `Breakpoint` and `Narrow="Column"` handles a width-driven stack. A declared breakpoint
  must be positive; `0` is rejected, not a way to disable responsive behavior.
- `Wrap` with `Cols`/`NarrowCols` provides finite wrapping; those column attributes belong to `Wrap`.
- `Column`, `Scroll`, `Clip`, measured wrapped text and min/max/fill sizing provide readable overflow.
- Typed bindings and conditional groups can express a consumer-owned compact mode or detail view.

The manifest's width breakpoint is not a built-in two-axis media query. If height changes the product's
mode, compute that mode at the consumer's layout boundary and compose it using the supported page
mechanisms. No new host, global state machine or UI toolkit is needed. Keep compatible node identities
and follow [definition-update](../development/0.8/public-contract.md) and
[editing](../development/0.8/editing-contract.md) rules. Removing a node and creating a different editor
is not the same as retaining its draft while hidden.

When filters are collapsed, leave their active state and a clear action visible. Content being hidden
does not mean its effects have stopped.

## Make the main page useful before adding navigation

A frequent task and its relevant status can share the main page. Put infrequent editing tools on a
secondary page or in a conditional group. More desktop space should support comparison and direct
actions, not justify a permanent navigation rail or a wall of status cards. This applies the use of
space and precise input described in [Apple's macOS guidance](https://developer.apple.com/design/human-interface-guidelines/designing-for-macos/)
and [Android's desktop design principles](https://developer.android.com/design/ui/desktop/guides/foundations/design-principles)
to a game settings window. RimWorld's window and input conventions remain the host contract.

### Describe the player's state, not the implementation

Answer three questions near the affected control: **what am I changing, what is effective now, and what
can I do next?** Reserve internal state codes, transaction ordering and service ownership for diagnostics.
For example, “Couldn't save these changes. Retry” is useful when a disk write failed; “Saved, but not yet
active” describes a different result and must not share the same success indicator.

Use a compact status line for ordinary progress. Give an actual blocker readable emphasis and a relevant
next action; do not style it as muted background explanation. Keep detailed diagnostics behind a labeled
route. A `Banner`, `WrappedText` and an explicit `Button` can present these states using existing theme
roles. The consumer's save/publication owner supplies the facts; the UI does not invent a successful
outcome or a recovery action the product cannot perform.

Give the base surface, raised controls, primary text and supporting text distinct roles. A dark palette
still needs bright readable text; an accent belongs on selected states and important actions, not on
every label. Judge the rendered colors over the actual window background, including alpha and inherited
native GUI state. A theme selector belongs to the consumer's appearance settings and should update that
window without changing layout, scroll or editing state. It must not recolor unrelated native menus.

### Use the list to find and compare, and detail to explain

Keep each list row's name, short disambiguating source, essential summary and direct action. Put full
metadata and diagnostic detail in the selected object's detail view. Removing every source label makes
two identically named objects impossible to distinguish; copying full metadata into both panes makes
both harder to scan. Gain density by removing repetition and unnecessary spacing, not by shrinking text
or hit targets.

Compose the list with `Repeat` and ordinary rows; use the same selected-object model for side-by-side
detail and compact drill-down. Google's [adaptive list-detail guidance](https://developer.android.com/develop/ui/compose/layouts/adaptive/canonical-layouts)
describes the useful relationship between these arrangements. Its toolkit APIs and breakpoint values
are not FL capabilities or defaults.

The template root of a `Repeat` follows the same visibility rules as an ordinary child. A hidden row
contributes no arranged box or gap; its stable item identity and existing state remain while its key is
still present. Visibility-binding changes must invalidate the owning container so the row can return.
The local `f362c99` fix restores this path, which RC1 omitted for template roots. Do not compensate by
removing item keys or adding a wrapper solely to make root visibility work: removal has a different
state lifetime.

Keep **viewed object**, **edited target**, **enabled state** and **keyboard focus** distinct. A selected
row can mean “show this detail”; it does not also mean “enabled.” Use explicit labels and existing state
styles alongside the actual enable control. [Apple's focus and selection guidance](https://developer.apple.com/design/human-interface-guidelines/focus-and-selection/)
is a useful distinction, not a reason to replace the host's input behavior. FL's additional K1 keyboard
navigation is deferred; native editing does not imply a complete keyboard-navigation system.

### Let secondary tools switch directly without becoming the main architecture

Once inside a secondary group, keep its category control available. A short `Button` group in a
`Row`/`Wrap` can work at wide sizes; a `Dropdown` showing the current category can retain the same route
at compact sizes. This is a composition of existing controls, not a requirement for a new tab widget.
Do not make every sibling switch travel back through a directory of large buttons.

Keep a clear return to the original main-page context. Sibling switches should not create a long trail
of meaningless Back entries. Help and detail are temporary destinations: return to the original object,
filters, list position and valid draft. Preserve compatible editor identity and the existing commit/cancel
contract; changing categories must not silently retarget an unfinished edit.

### Give help buttons an explicit destination

The page header's Help action should open that page's topic deterministically. A complex field or group
can have its own labeled explanation action carrying an explicit opaque topic and relevant object
context. Use `Button` with existing `BindAction`/`BindCommand` bindings; the consumer owns topic lookup,
the pinned topic and the return context.

`HelpRef` and the Session's `CurrentHelpRef` support contextual publication; a newly hovered control can
replace the current topic. Reading the last hover at click time therefore does not define a stable page
Help command. Hover help may supplement explicit actions, but must not change a topic the player opened
deliberately. An element's `HelpRef` alone does not implement the click destination. See the
[help publication contract](../development/0.8/public-contract.md#publish-a-topic-without-installing-a-help-panel)
for the separate lifetimes and explicit per-choice references.

Show long help in the existing content area with a return action, rather than permanently reserving a
large help band or opening another main window. Keep short, necessary guidance next to its control.
These arrangements need no custom widget, additional window API, drag-and-drop system or desktop visual
effects.

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

### When Close responds but the page does not

First separate shell interaction from page execution. A working native entry point and Close button can
coexist with a page that draws but never accepts input. Follow one actual click through native input
eligibility, event type and coordinates, capture, page hit testing, binding invocation and the owner's
mutation. Locate the first missing transition before changing bindings or replacing controls.

Convert native event types by explicit enum constants, not by `ToString()` followed by name parsing.
The inspected Unity `EventType` contains obsolete same-value aliases such as `MouseDown`/`mouseDown`;
Mono need not choose the canonical spelling. Case-sensitive parsing can therefore discard a supported
event. The local `069b88a` fix maps both `type` and `rawType` explicitly, leaving unsupported events as
`None`; a numeric cast is also unsuitable because the native and neutral enum values differ. The shell's
Close button uses a direct native path, so verify it separately from page press/release and editor input.

If the page also stalls, measure the hot path rather than attributing it to a disconnected button.
Look for repeated host/definition creation, full-directory projection or text measurement on every GUI
event, and invalidation that continually defeats reuse. Record the relevant call frequency or elapsed
time and whether it came from the game or platform doubles; a static screenshot cannot establish input
latency.

Palette conversion also belongs in that inspection. `UiElementProperties.Apply` resolves the finite
colour vocabulary during arrangement and again during pointer-layer collection and drawing. Repeating
enum parsing, reflection-based validation and enum-to-string conversion for each known role multiplies
small costs across the tree. Use exact canonical-name dispatch and reflection-free enum validation on
this path; keep declaration precedence, removal, source attribution and theme revisions intact rather
than skipping resolution or caching an entire GUI frame.

The separate `0.8.0-local.paletteperf1` candidate applies this correction and includes the earlier
event-mapping and Repeat fixes. It retains the public case-sensitive role names and rejection behavior,
including the parsed `out` value on failure. It is a local candidate, not an update to the published RC1
asset. Native responsiveness and FPS still need validation with its matched consumer build.

Check the actual loaded build and available diagnostic capability before expecting Dev output. A quiet
log does not show whether a click reached an action. Prefer existing diagnostics; any additional
observation should be explicitly enabled and bounded, not a per-frame log stream. Do not make a broken
page's diagnostic button the only way to collect evidence about that page.

After an input blocker, stop the broad player checklist. The next native pass should first establish
that opening remains responsive, a page button and editor accept input, and Close still works. Resume
the remaining checks only after that path works. Keep the original failure alongside any headless
checks, which describe a different execution boundary.

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

Match the control family at the insertion point. An Options category is not styled like a main-menu
command button merely because both can open the same window. Use the category's shape, spacing and
hover treatment without showing a false selected category while the original category's content remains
visible. Consumer palette changes stay inside the consumer window.

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
| Reduce a list to names alone | Two objects have the same name | Retain a short source label while moving full metadata to detail. |
| Quiet every status to save space | A blocked primary task looks available | Keep the blocker readable and its next action reachable at the minimum size. |
| Return to a directory for every tool switch | Comparing two related settings repeatedly loses context | Keep local category switching and a meaningful return to the main page. |
| Open Help from the last hovered field | The same page Help button opens different topics | Give page and field actions explicit destinations; retain pinned help and return state. |
| Treat opening and closing as proof that controls work | The shell responds but the drawn page ignores clicks | Trace a real page action through native input to its binding and owner. |

Inspect the rendered page with representative long labels, nonempty lists, an open detail and a visible
status/error message. Exercise the primary action, return, close and an active editor. Check popup
anchoring and capture when the geometry changes; do not force a draft commit merely to change layouts.
Choose cases that can expose these failures rather than multiplying every resolution by every language
and scale.

## Evidence and maintenance

The evidence below separates platform mechanisms, local corrections and user observations:

| Evidence | Scope |
| --- | --- |
| RimSage 1.6 source: `ResolutionUtility`, `Dialog_Options`, `UI.ApplyUIScale`, `Window` and `MainMenuDrawer` | Platform mechanisms and extension points; source dataset identified as 1.6.4850. |
| Target assembly `1.6.9676.17735`: focused inspection of `ResolutionUtility`, `Dialog_Options`, `MainMenuDrawer`, `Window.Notify_ResolutionChanged` and `WindowStack.InsertAtCorrectPositionInList` | Confirms scale/menu constraints, initial-size reapplication and layer-ordered insertion on the target 1.6.4871 build; not a rendered overlap check. |
| FL source at RC commit `a0b5b983c3c13a099c62d936305eaebaf1deb5a8` | Window ownership, chrome, catalog and page capabilities. |
| Target Unity `EventType` and Mono `RuntimeType.GetEnumName` / `Enum` source; local fix `069b88a` | Confirms same-value aliases and name-selection risk; explicit event mapping removes that dependency. Existing adapter checks passed, not a native event trace. |
| Local fix `f362c99`, including `069b88a` | Restores template-root visibility through the ordinary hidden/read-invalidation path. Existing Repeat, visibility, invalidation and layout-reuse suites passed; no native hide/show acceptance. |
| Later 2026-10-25 user feedback: consumer `9ee0703` + FL `f362c99`; subsequent cost observation at consumer `6c51c72` uses the same FL | Page input reported working again, while all consumer buttons remained slow. Font, IME, overlap and scaling acceptance remains open. |
| Local candidate `0.8.0-local.paletteperf1`, following `f362c99` | Removes repeated enum parsing/reflection/string conversion for canonical palette lookups and reflection from palette enum validation. Native responsiveness remains to be measured against the matched consumer. |
| Executed calculations from the scale predicate | Accepted scale table and logical-size examples; not native render acceptance. |

The linked Apple and Google guidance informs the interaction recommendations. Those recommendations are
design reasoning applied to the existing FL contract, not observed player outcomes or a promise to
reproduce another operating system's UI. Record native observations separately from source facts and
design choices.

The first 2026-10-25 consumer observation on RimWorld 1.6.4871 showed both native entries opening the
window and a rendered page; the player reported severe lag and no page-control response while Close
remained usable, then ended the pass. Input/performance acceptance therefore failed; the responsible
layer was not established by that observation. The installed FL was subsequently identified as `c3c3070`,
not the consumer's fixed RC1 dependency `a0b5b9`; their production source trees are identical, but the
observation is not a test of the intended artifact pairing. The event-mapping and Repeat fixes address
confirmed contract defects; the palette candidate removes redundant work on the identified hot path.

Later user feedback with consumer `9ee0703` and FL `f362c99` reported page input working again, while all
consumer buttons remained slow. The subsequent cost observation at consumer `6c51c72` used the same FL.
This is user-reported page-input recovery for that pairing; performance and remaining font, IME, overlap
and scaling acceptance are open. The palette candidate has not been tested in game. Record the actual
candidate, resolution, scale, language, page state and result for each game pass.
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
[layout engine](../../Source/FerriteLib.UiKit/Kernel/UiLayoutEngine.cs); native event conversion lives in
[VerseUiExecution](../../Source/FerriteLib.UiKit/Adapters/RimWorld16/VerseUiExecution.cs).
