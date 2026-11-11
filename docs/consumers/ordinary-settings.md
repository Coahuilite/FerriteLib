# The ordinary settings page — the recommended recipe

Audience: an author wiring a plain settings group (labeled values, a dropdown, a conditional section,
explanatory text) into a consumer mod. Nothing here needs a custom widget, a VM base class, a builder DSL,
or a new library type. This retained 0.8 recipe remains legal on the same public author path; use the [0.9 target guide](../development/0.9/tools.md) for the new compiler input. See the
[0.8 consumer guide](consume-from-0.8.0-dev.md) for package and author paths. The runnable, compile-checked source
is the harness file

`tools/FerriteLib.UiKit.Tests/KernelOrdinarySettingsRecipeTests.cs`

— read the recipe halves (`SettingsModel`, `ValueRow`, `PageXml`, `RegisterBindings`) alongside this page.
The guide deliberately links the file instead of pasting a second listing that could drift. (The library's
own fixture is not consumption evidence — it proves the recipe compiles and arranges, not that a player
uses it.)

For an expensive page, the optional 0.9 [region port](../openspec/specs/contracts/presentation-execution/region-api.md)
can retain pure preparation for one explicit scope. The ordinary recipe below does not require it;
old bindings, widget callbacks and standalone controls keep their original conditions.

## Start a 0.9 page with Schema 3

```xml
<UiPage Schema="3" Source="my-settings">
  <Widget Id="note" Kind="text/wrapped" TextKey="MySettings.Note" />
</UiPage>
```

Schema 3 uses event-tiered execution by default: interaction, editing, IME, eligibility, Cancel,
help and control identities still run on every event. Layout and context inputs synchronize on the
first event after a Repaint, on Repaint itself, and on the next event after a declared change or
completed business request. Built-in `text/wrapped`, `input/button` and `input/checkbox` display
work runs on Repaint, where ordinary display and layout inputs are read fresh. No business clock is
created by this policy.

Schema 2 keeps its existing per-pass default. Set `TieredEvents="true"` on its `UiPage` root to opt in,
or set `TieredEvents="false"` on a Schema 3 root when you depend on per-pass ordinary display reads.
Both schemas use the same remaining vocabulary. `TieredEvents` accepts only `true` or `false` and is
root-only. Existing C# factories retain Schema 2 behavior; use
`manifest.WithTieredEvents(true)` (or `false`) for the synonymous explicit programmatic policy.

On either schema, the library omits getter-free built-in display work on non-Repaint events when
the execution owner supports display isolation. Ordinary `SelectedBind` reads on a per-pass page
remain per pass, as do checkbox input reads. An execution backend without isolation keeps the
original per-pass display path. Custom widgets and editors keep their per-pass `Draw`; this policy
does not give custom renderers a new deferred-display interface.

`IUiTranslation.Translate` call counts are not guaranteed. Return a stable result for a key until
`TranslationRevision` changes; do not use translation calls to advance business state.

### Defer display in fully clipped Overlay subtrees

`<Overlay DeferClippedDisplay="true">` may defer built-in display reads, formatting, fit and paint
while its effective clip is empty. Input/control calls keep their order, and the first visible pass
reads the current display value without requiring a notification. This policy requires display
isolation; custom widgets and editors keep their existing behavior.

The default is off on a per-pass page and on for Overlays in a tiered page. Descendant Overlays
inherit; an explicit `false` disables the policy for that subtree, and a deeper `true` re-enables it.
The attribute is Overlay-only and accepts only `true` or `false`. This is the same explicit policy
as on the 0.8 maintenance line. The retained 1.6 execution owner declares isolation; the 1.7 adapter
keeps its conservative per-pass display fallback until it declares that capability separately.

## The four parts, and which ones you write

1. **The page (XML)** — one manifest. A labeled numeric setting is one `Row` containing `input/slider` and
   `input/number-field` bound to the *same* key. Adding an analogous setting is a second call to the same
   row builder plus a second `BindValue`. You never write a rectangle, a `Measure`, or a `Draw`.
2. **The model (plain C#)** — an ordinary class with fields and mutation methods. No base class, no
   attributes, no reflection, no DI.
3. **The wiring (bindings)** — `UiBindings`: `BindValue`/`BindReadOnly` for typed values, `BindChoices` with UiChoice<T> for
   a dropdown's list, `BindCommand` behind a button, and one read-only value for a live status line.
4. **The host (already yours or the library's)** — `UiWindowHost`/`UiPageWindow` own the chrome, the
   `UiHost`, and the session. You attach/detach; you do not pump.

## Notification: the model announces, `Set` does not

`UiBindings.Set` **requests the model owner's write; it does not imply acceptance or notify.** The page's write path and every non-UI write path
call the *same* mutation method on the model, and that method calls
`bindings.NotifyChanged(key)`. That is the whole basic recipe — one place announces, and it is reached from
both directions:

```csharp
bindings.BindValue("level", () => model.Level, v => model.SetLevel(v), UiInvalidation.Paint);
// a settings preset, a keybind, a save-load path: model.SetLevel(x) — same door, same announcement
```

If you skip the announcement, ordinary interactive values still follow their existing input reads.
On a tiered page, built-in display and ordinary layout inputs are refreshed at Repaint; other events
may use the most recent synchronized geometry. A per-pass page retains its original ordinary layout
synchronization conditions. Announce changes so the next event can adopt them rather than waiting for
Repaint. Choose the invalidation class according to what the value can affect:

- **Paint-only content** — a value that moves no rect: a slider thumb, a number field's text, a progress
  fill. Announce with `UiInvalidation.Paint`; the arranged snapshot is reused as it stands.
- **Measured content** — a string whose width/height feeds the layout: a label, a wrapped explanation, a
  VisibleBind group, an options list that changes the popup's rows. Announce the key the *element
  declares*, with `Measure`/`Structure` (or the conservative `Everything` default). Notifying an internal
  key the page never declares invalidates nothing: the announcement must reach the dependency the layout
  actually recorded.

Declare the class once, at registration, next to the key (`invalidates:` above). The recipe page shows all
three classes in one file.

## Show/hide, narrow layout, scrolling

Choose the outer window size and the available logical page space using the
[RimWorld window guide](rimworld-window-layout.md), including the game's UI-scale restrictions.

A conditional group is a container with `VisibleBind="show-advanced"` bound to a `bool` — toggling it does
not reopen the page. Narrow behavior is the containers' own vocabulary, not a second framework:
`Breakpoint` + `Narrow="Column"` flips a `Row` to a stack, `NarrowHidden` drops children in the narrow
state, and a `Scroll` around the column gives overflowing content a viewport instead of a cut-off. Grid
flow across states is a `Wrap` with `Cols`/`NarrowCols` — those two attributes are refused on any
other container; use them only on Wrap.

## Who owns cleanup

The **window host** owns the `UiHost` and its session: closing the window detaches and disposes them (the
`HostDetached` announcement still sees a live session). The **page** owns its model subscriptions: attach
them in `HostAttached`, release them in `HostDetached`. The recipe's close/reopen lane asserts the
arithmetic — one open → one handler, close → zero, reopen → still one per live host, not one per attach.
If your draft/focus policy exists, it stays the host's existing one; the recipe does not invent another.

## Optional advanced: `UiNotifyAdapter` (MVVM-shaped models)

If your model already raises `INotifyPropertyChanged`, the optional adapter maps property names to binding
keys (`Map("Level", "level")` / `MapAll`), and `Attach(source)`/`Detach` plus `Dispose` handle the
subscription. Two facts to keep straight: notifications arriving **off the UI main thread are refused, not
queued** (observe `NotificationRejected` if you care), and the adapter still does not make `Set` announce —
it forwards *your* model's announcements to the binding keys. The basic recipe above does not need it, and
nothing in this guide assumes it.

## What this recipe is not

It is not a Form framework: the page model composes the existing capabilities and you own information
architecture, navigation and workflows. If they cannot express a real shared requirement, describe the
missing outcome and existing call path; the [public author policy](../openspec/specs/policies/public-authoring/spec.md)
provides the route for a new cohesive capability without inventing a separate form system.
