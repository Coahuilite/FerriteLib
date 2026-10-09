# Consumer handoff — 0.6.0

For the team wiring a real consumer against this surface. Read with `docs/api-tiers.md` (the promise) and
`20-api-and-xml.md` (the usage).

## 1. Version

```csharp
// In your Mod constructor, before you touch any UiKit type:
FerriteLibVersion.Require(new Version(0, 6, 0), new Version(0, 7, 0));
```

- The game cannot express a prerequisite version (`ModRequirement` parses only `packageId`,
  `alternativePackageIds`, `displayName`), so **the range assertion is yours to make**. Keep it in the
  constructor: it is the only detector for a second carrier of `FerriteLib.UiKit.dll`.
- Axis agreement: `FerriteLibVersion.Api` = `About/About.xml <modVersion>` = `<VersionPrefix>` = **0.6.0`.
  `AssemblyInformationalVersion` carries the commit SHA and is never a compatibility value.
- **Why 0.6 and not 0.5:** while the major is 0, *any* change to the compiled surface — addition or break —
  bumps the minor. This round is additive, and it is still a minor. The 0.5.0 handoff stays valid for the
  0.5.0 carrier; nothing here breaks it.
- Carrier: only the `coahuilite.ferritelib` mod ships `FerriteLib.UiKit.dll`. Never copy it into your own
  package.

## 2. Migration

**None is required to keep working.** No member was removed, moved or renamed in this round; both changed
signatures are optional-parameter additions:

| Change | Effect on your code |
| --- | --- |
| `UiDocumentService(bool? autoWatch = null, UiReloadPolicy? policy = null, IUiTimeSource? timeSource = null)` | Existing call sites compile unchanged and get `UiReloadPolicy.Default` and real time. |
| `UiPageWindow` / `UiWindowHost` gained `HostAttached`/`HostDetached` | Additive. A window with no handler behaves exactly as before. |

To use the round: subscribe `HostAttached` instead of polling `PageHost`; hand a `UiReloadPolicy` and a
`IUiTimeSource` to the document service; replace `KnownKinds`-based listing with `UiWidgetCatalog`.

**One behaviour change to know about even though no signature moved:** `UiDocumentService.Signal(id)` is now
the debounced *watcher* channel. If you called `Signal` from a UI control meaning "reload now", switch that
call to `Reload(id)`, which is immediate and uses the same validation and commit path.

## 3. Known limits, stated as limits

1. **Notification delivery is main-thread only.** An off-thread `PropertyChanged` is refused, counted and
   reported — never queued. Marshal yourself if a background producer needs to announce.
2. **IME composition is not part of the input-deferral signal.** A commit deferred for the user's own
   interaction is bounded by `UiReloadPolicy.MaxDeferSeconds` (default 2 s), and a composition can in
   principle be cut by that ceiling. It is bounded, not silent.
3. **The reload commit point is chosen from a source reading of the game's update/GUI order**, not from an
   in-game measurement. See `verification/t2-reload-scheduling.md` for the transcription and what it does not
   prove; treat the in-game item as still open in your own tree too.
4. **`UiDocumentService` events are not atomic across sibling windows.** Per-document batches are atomic; two
   windows attached to one document commit together, but a consumer hook that re-enters during
   `PreClose`/`PostClose` is outside any of this round's lanes.
5. **Listing is a snapshot.** It is not a live view and it is not a subscription: ask again after a
   registration.
6. **The draw surface is the tree.** Anything drawn into a window goes through `UiHost`; a raw IMGUI call
   outside the tree is unsupported and unmeasurable.
8. **The host's `Source` string is the widget-registry scope.** `UiWidgetRegistry.Resolve` is called with
   the host's source (`UiHost.cs`, the resolve site), so a kind you register is only found by a page whose
   source equals the scope you registered it under — otherwise it falls back to the `core` scope and then
   throws `UiUnknownWidgetKindException`. For a `UiPageWindow` the source is
   `Consumer + "/" + WindowKind` (the page's identity, not the packageId), so **register your kinds under the
   exact page identity you will open, or wrap the page in a window whose source is your scope**. This is
   pre-existing behaviour, not new in 0.6, and it is the first thing that surprises a new consumer — the
   demo mod hit it. A future round may let a manifest or a page declare its scope explicitly; until then,
   align the two strings deliberately.
9. **Our own demo is not provenance.** `ferritelib_uikit_demo` uses only public API and is shipped as a
   usage sample, but it does **not** establish real-consumer provenance or satisfy the specialized-kind
   promotion gate. API stabilization no longer has a consumer-count prerequisite (maintainer, 2026-09-17).

## 4. What this round still owes you (and is honest about)

- **No real consumer has compiled against 0.6.0 yet**, and no in-game acceptance has been run. Nothing in this
  round's evidence reaches 已由真实消费者接入 or 已实机验证; see `40-verification.md` for the checklist.
- The stable tier is unchanged and still thin: `IUiBindings`, `UiHost`, `UiWindowHost`, `UiTheme` and the
  rest of the page model remain **public-unstable** under the current tier list. The maintainer removed
  the second-wired-consumer prerequisite on 2026-09-17; stabilization now requires a documented
  contract/verification decision, not a particular consumer count. This clarification does not promote
  types or alter the frozen 0.6 signatures.
- If your integration needs something this surface cannot express, that is a finding, not a workaround: the
  library's growth rule is that a real consumer being forced to hand-roll something, with the code cited, is
  what earns a new capability. Report it rather than reaching past the public surface.
