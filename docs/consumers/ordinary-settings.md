# The ordinary settings page — the recommended recipe

Audience: an author wiring a plain settings group (labeled values, a dropdown, a conditional section,
explanatory text) into a consumer mod. Nothing here needs a custom widget, a VM base class, a builder DSL,
or a new library type. This guide describes the 0.7 supported path; the runnable, compile-checked source of
truth is the harness file

`tools/FerriteLib.UiKit.Tests/KernelOrdinarySettingsRecipeTests.cs`

— read the recipe halves (`SettingsModel`, `ValueRow`, `PageXml`, `RegisterBindings`) alongside this page.
The guide deliberately links the file instead of pasting a second listing that could drift. (The library's
own fixture is not consumption evidence — it proves the recipe compiles and arranges, not that a player
uses it.)

## The four parts, and which ones you write

1. **The page (XML)** — one manifest. A labeled numeric setting is one `Row` containing `input/slider` and
   `input/number-field` bound to the *same* key. Adding an analogous setting is a second call to the same
   row builder plus a second `BindValue`. You never write a rectangle, a `Measure`, or a `Draw`.
2. **The model (plain C#)** — an ordinary class with fields and mutation methods. No base class, no
   attributes, no reflection, no DI.
3. **The wiring (bindings)** — `UiBindings`: `BindValue`/`BindReadOnly` for typed values, `BindOptions` for
   a dropdown's list, `BindCommand` behind a button, and one read-only value for a live status line.
4. **The host (already yours or the library's)** — `UiWindowHost`/`UiPageWindow` own the chrome, the
   `UiHost`, and the session. You attach/detach; you do not pump.

## Notification: the model announces, `Set` does not

`UiBindings.Set` **stores a value; it does not notify.** The page's write path and every non-UI write path
call the *same* mutation method on the model, and that method calls
`bindings.NotifyChanged(key)`. That is the whole basic recipe — one place announces, and it is reached from
both directions:

```csharp
bindings.BindValue("level", () => model.Level, v => model.SetLevel(v), UiInvalidation.Paint);
// a settings preset, a keybind, a save-load path: model.SetLevel(x) — same door, same announcement
```

If you skip the announcement, a *value* change is still read live on the next paint (values are pulled per
pass), but anything geometry depends on keeps its stale arrangement. Which brings the one distinction an
author must keep in mind:

- **Paint-only content** — a value that moves no rect: a slider thumb, a number field's text, a progress
  fill. Announce with `UiInvalidation.Paint`; the arranged snapshot is reused as it stands.
- **Measured content** — a string whose width/height feeds the layout: a label, a wrapped explanation, a
  visible-key group, an options list that changes the popup's rows. Announce the key the *element
  declares*, with `Measure`/`Structure` (or the conservative `Everything` default). Notifying an internal
  key the page never declares invalidates nothing: the announcement must reach the dependency the layout
  actually recorded.

Declare the class once, at registration, next to the key (`invalidates:` above). The recipe page shows all
three classes in one file.

## Show/hide, narrow layout, scrolling

A conditional group is a container with `VisibleKey="show-advanced"` bound to a `bool` — toggling it does
not reopen the page. Narrow behavior is the containers' own vocabulary, not a second framework:
`Breakpoint` + `Narrow="Column"` flips a `Row` to a stack, `NarrowHidden` drops children in the narrow
state, and a `Scroll` around the column gives overflowing content a viewport instead of a cut-off. Grid
flow across states is a `Wrap` with `Cols`/`NarrowCols` — from 0.7 those two attributes are refused on any
other container, because there they never did anything (see the 0.7 contract note).

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

It is not a Form framework and there is no plan for one: the page model composes these atoms and you own
your information architecture, navigation, and workflows. If you find a settings-shaped thing you cannot
express with the atoms, bindings, containers and the host above, that is a bug report against FL — see
`consume-from-0.7.0.md` §last for where to file it.
