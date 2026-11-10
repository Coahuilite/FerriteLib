# Consume FerriteLib 0.8.x

Select one fixed DLL, assert its API range, and retain the same Host for an open UI. Then choose ordinary
notifications or explicitly tracked sources; neither requires a model base class. Start with the
[settings recipe](ordinary-settings.md) for a page, or [S1–S7](../openspec/specs/policies/public-authoring/spec.md)
for partial/custom adoption. Current requirements are in the [specification](../specification.md).

## Select the actual input

Select a fixed, delivered DLL that supports the XML and capabilities your page uses. Record its actual
source, configuration and DLL identity; do not substitute a moving build output or a relabelled carrier.
The `DeferClippedDisplay` policy described below is available in this task's delivered 1.6 development
input (FL MVID `6d8bea0f-75c4-4fb2-bfa6-d8405bf4656f`). Older published carriers do not acquire that
policy merely because their API version is in the same range. The local pair, its source form and rollback
are identified in `dist/private-clipped-display-20261110/USE-zh.md`.

Only FerriteLib distributes the runtime DLL. Reference it with Private=false and install no second carrier.
Every consumer checks its own tested range; game dependency metadata cannot express a version:

    if (!FerriteLibVersion.Require(new Version(0, 8, 0), new Version(0, 9, 0),
        "your.package.id", out string diagnostic))
        throw new InvalidOperationException(diagnostic);

The first public author baseline is0.8.x. Prior internal ranges and vocabulary are historical references,
not prerequisites for learning current use. Source, compiled DLL, XML, meaning and legal conditions have
the [stable compatibility promise](../api-tiers.md); public-unstable/advanced exceptions remain explicit.

## Declare and connect once

Use XML Schema2 or immutable UiElementSpec/Create definitions; both use the same compiler and typed checks.
Default kinds cover ordinary controls, descriptions, images and keyed Repeat. A compatible UpdateDefinition
keeps the Host/session; deleting and recreating an editor is not equivalent to hiding a retained one.

For ordinary data use BindValue/BindReadOnly, BindCommand/BindAction and NotifyChanged at the model mutation
boundary. Set requests the owner's write; it neither guarantees acceptance nor sends the notification.
Notify the actual declared key with its real invalidation class. Mutable Theme and ordinary/legacy Measure
remain legal under their original conditions.

For automatic dependencies explicitly use ReactiveRuntime.Int/Text/Boolean and BindTracked.
Other Value/List types require caller equality. Structural lists and item properties are separate sources;
immutable choices project all mutable data while tracking, not later in Draw. A shared model owner can outlive
multiple Hosts. External services deliver safe results in the projection's owning context; FL does not supply
tasks, thread dispatch or freshness policy. See the [counter walkthrough](../development/0.8/counter-walkthrough-zh.md)
and [binding contract](../openspec/specs/core/binding-reactivity/spec.md).

Forwarding IUiBindingAccess names its actual BindingOwner and consistently forwards endpoints and operations.
Switch backing between passes; Host validates before replacing its projection. Reacquire minting-registry
endpoints after switching. Ordinary forwarding does not activate tracking or change target identity.
Use optional UiItemBindings for current-key fields/commands; authoritative membership may outlive a filtered
Repeat key view. Filtering/paging/business Move and object disposal remain consumer-owned.

## Use one UI lifecycle

For a standard shell construct UiPageWindow, optionally register it in UiWindowCatalog with a stable key.
For an existing native callback retain UiHost plus VerseUiExecution and call DrawFrame with the actual content
rect, execution.CaptureFrame(nativeInputAllowed), and that execution. Capture once per native callback.
For native controls without Host retain Session/CreateContext/SetFrame; for headless operations or independent
visuals no fabricated Host/node is required. See the [adapter](../openspec/specs/adapters/rimworld16/spec.md).

Host/Session borrow execution. Idle switching is legal, but an open pass/capture/active editor refuses a different
execution before changing frame or qualification. End or explicitly SuspendEditor on the old one first.
Close Session before execution, and dispose execution in finally. Image tokens are execution-scoped;
the supplier retains texture lifetime. Host close does not destroy business services.

SetDisplayDemand communicates reliable hidden demand, not input focus. Hidden drafts remain under the same
target; closing the real UI disposes its connections. Subscriptions attach/detach through the existing hooks,
whose explicit failure conditions still apply. No library-wide keyboard navigation or global window manager
is implied by the native shell.

## Edit first, publish deliberately

TextField, NumberField and StepperSlider default Live=false; explicitly set Live=true when wanted.
Valid, nonconflicting ordinary blur confirms; invalid/conflicting drafts remain. Cancel changes only the local
draft, not earlier Live writes. A normal request return ends that request even if business rejects/normalizes;
paint/readback failure cannot replay it. Setter failure preserves actual side effects without rollback.

Within StepperSlider, model10/draft20/Step1 requests only21 on plus; an actual Slider30 requests only30.
Capture alone preserves the draft. Only declared local participants can take it over, not every same-Bind field.
Shared ctx.Controls.Edit/Editor/SuspendEditor support custom composites.
Composition owns its key first; single-line Return confirms, multiline Return inserts newline and Ctrl+Return
confirms. Read-only preserves selection/copy while preventing mutation.
See [default policy](../openspec/specs/policies/default-wrappers/spec.md) and
[interaction/editing](../openspec/specs/core/interaction-editing/spec.md) for the exact outcomes.

## Retain valid work without retaining stale geometry

Meaningful UpdateScope=true roots can be nested; root/Section/Surface/Scroll/Clip/Repeat already participate.
Defaults capture their measurement inputs; new IUiMeasureParticipant declares complete pure inputs,
and optional IUiTextMetricsEnvironment supplies truthful per-font validity without changing ITextMetrics.
Old widgets/metrics receive no new purity condition. Stable-domain publication can be sparse, while unknown
or unsupported dependencies take the correct full path.

Positions, scroll ranges, clip/hit and popup anchors still change when their actual dependency changes.
Pure tint/hover does not require layout. This is not retained IMGUI pixels: required Repaint continues.
Use the [layout contract](../openspec/specs/core/layout/spec.md), not an every-frame rebuild or input throttle.

### Defer display work inside a fully clipped Overlay

With the delivered 1.6 development input identified above, a repeated row using an existing `Overlay`
as its native clip may opt in to deferred display:

```xml
<Overlay Id="row-content" DeferClippedDisplay="true">
  <Widget Id="row-title" Kind="text/wrapped" Bind="title" />
</Overlay>
```

The policy is off by default, accepts only `true` or `false`, and is valid only on an `Overlay`.
Descendant Overlays inherit it when omitted; an explicit `false` disables it for that subtree and an
explicit `true` can opt a deeper subtree back in. This does not change arrangement, clipping, hit
layers or which rows exist.

Only while the effective clip is completely empty may the built-in `text/wrapped`, `input/button` and
`input/checkbox` defer display-only ordinary reads, formatting, fit checks and paint. Those display
errors and fit findings can therefore surface when the content is shown. When it becomes visible, the
current display value is read on that pass even if its ordinary getter did not send a notification.
Input and business operations still run in their original order on every pass, including control
identity, eligibility, requests, capture and cancellation. Deferred display is not a successful paint:
it does not clear a presentation fault or replace the last successful retained paint.

The 1.6 native execution owner supports this opt-in by isolating the known mutable label-style fields
during display. That isolation is a new opt-in behavior; callers must not depend on those fields leaking
from opted-in display. An execution backend without that capability, an unselected Overlay, custom
widgets and editors retain the original Draw behavior. This policy does not change standalone drawing or
grant permission to skip a custom callback.

## Configure only the appearance you mean

A new UiTheme works without palette assignments. Override one neutral UiColour role or one Font/metric
declaration; Remove reveals the current remaining source, not a creation-time theme snapshot.
Five metrics are Padding/Spacing/Gap/RowHeight/Hairline; container Padding/Gap allocate that container rather
than becoming every child's density. Retint does not replace the Host or clear drafts/scroll.
Use scoped UiImage for images/backgrounds. See [appearance](../openspec/specs/core/appearance/spec.md).

### Set a text colour without retinting other surfaces

Use TextColor for ordinary text, or one of TextColor.Muted/Active/Success/Warning/Danger/Disabled
for a state-specific colour. Each slot inherits independently; a more distant state slot beats a nearer
ordinary slot. Colour syntax is #RGB/#RGBA/#RRGGBB/#RRGGBBAA or invariant r,g,b[,a] in0..1.
An alpha of0 is an explicit transparent colour, not removal.

```xml
<Widget Kind="text/wrapped" Text="Ready" Tone="Success"
        TextColor="#E6E0D4" TextColor.Success="#8FCB9B" />
```

Inside a style Scheme, write the same slot as an element, for example
`<TextColor.Success Value="#8FCB9B" />`. Scheme, ancestor and page sources use the same per-slot
precedence as Palette roles; the slots are not new UiPaletteRole values.

The optional **public-unstable** UiTextColours facade keeps the stable Theme/ElementProperties surface
unchanged. Theme changes reach independent drawing directly. Instance/local changes in a retained-page
custom widget reach its supplied context through the Host's next normal property resolution:

```csharp
UiTextColours.Theme(theme).Set(new UiColour(0.9f, 0.9f, 0.8f));
UiTextColours.Instance(ctx.Properties, "TextColor.Warning").Set(new UiColour(1f, 0.7f, 0.3f));
UiTextColours.Instance(ctx.Properties, "TextColor.Warning").Remove();
```

UiTextColours.Local supplies runtime local-style declarations. UiTextColours.Resolved(ctx.Properties)
is the live read-only slot/source view from normal resolution.

For an independent Session context, CreateContext does not run Host property resolution. After writing
instance/local declarations, call UiTextColours.Resolve(ctx, hostTheme) and use the returned context for
both measurement and drawing. Keep hostTheme as the authoritative base; do not feed back the resolved
element theme as its host. Repeat this resolution after changing declarations. It uses the existing
property owner and ancestor rules, not a second style resolver. Existing standalone callers that do not
adopt these new declarations keep their original conditions.

For drawing without any Session, use UiTextColours.Theme(theme), then
theme.Styles.Resolve(tone, emphasis, writable).Text or UiThemeDraw.Label. A custom widget using
ctx.Drawing.Text directly passes that resolved colour explicitly; the low-level drawing interface has
no Theme argument. An explicit Label colour still wins.
A real disabled treatment ignores ordinary TextColor, using only TextColor.Disabled or
DisabledForeground. Setting the Disabled colour does not make a control writable or clickable.
Retint advances only the colour clock, not layout.

**Default change:** read-only Bind on text/wrapped now paints normal foreground, subject to its own
Tone/Emphasis/colour. Use Emphasis="Muted" or an explicit TextColor for subdued presentation.
Interactive read-only controls and shared Resolve(..., writable:false) retain their old meaning.

### Choose one effective font for measurement and drawing

FontSize is an unsigned decimal integer in1..96, in logical GUI units, not physical pixels. Omit it to
keep Tiny/Small/Medium and the host's preset rendering. Zero, negative, fractional, signed, exponential,
blank, out-of-range and overflowing values are refused. Inline errors name the element at creation;
an invalid style candidate reports an issue and retains last-good; runtime Set throws
ArgumentOutOfRangeException before changing the declaration.

```xml
<Widget Id="summary" Kind="text/wrapped" Text="Ready" Font="Small" FontSize="18"
        TextColor="#E6E0D4" />
```

In a style Scheme use `<Metric Token="FontSize" Value="18" />`, alongside
`<Metric Token="Font" Value="Small" />` if needed. FontSize is typography, not a Density distance token.
Both properties follow runtime instance → inline → runtime local → named local → nearest ancestor's
same layers → page style → host → fallback. The nearest layer declaring either Font or FontSize decides
the size: a Font-only layer resets an outer numeric size; a size-only layer keeps looking outward for
the alias. Remove reveals the current next layer.

```csharp
UiTextFonts.Theme(theme).Set(18);
UiTextFonts.Instance(ctx.Properties).Set(20);
UiTextFonts.Instance(ctx.Properties).Remove();
// UiTextFonts.Local(ctx.Properties) is the runtime local-style layer.
```

UiTextFonts.Requested(theme) returns the alias/optional-size pair. UiTextFonts.ResolvedSize(properties)
returns the requested size and source; old ResolvedFont and Styles.Font still mean the alias.
A UiTextRun exposes Requested, Font (effective), Purpose and FallbackReason. Only a matching optional
metrics/drawing pair supporting that purpose may retain the number. Editor also requires
IUiTextFontEditing. Unsupported use reports the request, effective preset, purpose and reason; there
is no nearest-preset approximation, rich-text sizing or post-draw scale.

### Adopt the production pair without changing old callers

UiPageWindow/UiWindowHost supply the new pair automatically: VerseTextExecution over the window's
existing protected VerseUiExecution and VerseTextMetrics over its native ruler. Custom Metrics overrides
and independently supplied Host rulers remain the actual rulers; the shell does not replace them with a
native singleton. An old custom ruler without numeric capability therefore gets explicit preset fallback.
The same native owner still owns focus, capture, frames, images, editing and cleanup.

For an embedded Host or standalone visual, create and retain this pair for the UI lifetime:

```csharp
var execution = new VerseTextExecution(); // Owns exactly one native owner.
var metrics = new VerseTextMetrics(VerseFerriteTextMetrics.Instance);
```

Pass metrics to the existing UiHost constructor. At each native callback call
`host.DrawFrame(contentRect, execution.CaptureFrame(nativeInputAllowed), execution)` once, preserving
that actual composed port. Close Host/Session before disposing execution, in finally. If composing an
already-owned VerseUiExecution, `new VerseTextExecution(owner)` borrows it; its Dispose does not dispose
the owner. Do not create a second owner or switch ports mid-edit/capture. The original singleton,
VerseUiExecution, ITextMetrics/IUiDrawing/IUiExecution and no-number usage remain legal unchanged.

Without any Host or Session, use the pair directly:

```csharp
UiTextRun text = UiTextFonts.Resolve(metrics, execution, UiTextFonts.Requested(theme),
    elementPath: "summary");
float height = text.MeasureText(caption, rect.Width);
text.Draw(rect, caption, theme.Styles.Resolve(UiStatusTone.Neutral).Text);
```

Use that run for measurement and painting, and pass the same capable metrics to an attached fit audit.
For a standalone Session, SetFrame with the composed execution before measurement/drawing. CreateContext
still does not resolve element declarations: call `ctx = UiTextFonts.Apply(ctx, hostTheme)` after writing
them, then use the returned context. A retained-page custom widget already receives resolved properties:

```csharp
UiTextRun text = UiTextFonts.Resolve(ctx);
float height = text.MeasureText(caption, ctx.ViewWidth);
text.Draw(rect, caption, ctx.Theme.Styles.Resolve(UiStatusTone.Warning).Text);
```

Custom Measure/Draw should request the same purpose and provide complete UiMeasureInputs through the
existing IUiMeasureParticipant. UiTextFonts.RowHeight supplies a numeric-aware natural row floor, and
UiTextFonts.PopupRect pairs with UiPopup.DrawChoices for numeric option geometry. Explicit allocations
remain the author's responsibility; a large font does not silently override an explicit Height or FieldWidth.
Old UiThemeDraw.Label and direct drawing's explicit UiFont parameters keep their preset meaning; choose
the new run when requesting numeric text.

### Editors, retained display and actual metadata

Existing ctx.Controls.Edit resolves Editor font and the effective state ink, then passes UiTextEdit through
the composed port to the same owner's real Widgets.TextField/TextArea. It preserves draft/commit/Cancel,
focus, selection, copy and IME authority. Native glyph colour is not implemented as a placeholder/label.
For lower-level editing resolve `UiTextFonts.Resolve(ctx, UiTextPurpose.Editor)` and call its Edit with
your existing UiNativeEdit request and the resolved colour; the request alias must equal run.Font.Alias.
A null colour preserves native preset ink. Custom hosts implement the optional interfaces rather than
changing old interface members.

Auto/SharedLabels, wrapping, natural row heights, fields, buttons, dropdowns/popups, layout inputs and
text-cache keys use effective font and purpose. Null CaptureEnvironment revokes reuse at a synchronization;
the old unchanged idle snapshot is still legal for numeric and legacy alike. Last-success retained drawing
captures the font, ink and draft but is display-only, never a second native editor.

UiTextFonts.FontOf/ColourOf read actual font/optional ink from overflow reports. Host observation facts
separate requested/effective per-purpose fonts, source and last paint colour; the Dev snapshot adds
ScopeTextFont (request), LabelFont/Colour and EditorFont/Colour with paint-source fields. They describe
last recorded outlets, not every glyph or current native pixels; not-sampled remains explicit, and the
old PaintedFont unknown field is unchanged.

**Host limits:**1.6 labels and editors are **静态候选、待原生**.1.7 label numeric rendering is a source-level
candidate behind the single NumericLabels17 switch; editor numbers are unsupported and use reported
preset fallback.1.7 has no reliable font-environment fingerprint, so it grants no cross-synchronization
text reuse. Native screenshot/IME/selection/UIScale/font-fallback and1.7 binding/JIT checks remain open;
see the [implementation status](../openspec/implementation.md#appearance-freedom).

## Publish help without owning a panel

Declare opaque HelpRef on elements/choices. Empty choice help does not infer Value/Text and can fall back to
the general element topic. Session CurrentHelpRef/CurrentHelpSource and HelpGracePasses retain their advanced
grade and pass-based timing (library default0; a consumer's15 is not the default).
Tooltip takes explicit translated text.

Optional HoveredOptionHelpOutputBind targets a writable string endpoint. It withdraws only the last publication
still owned by that control; a newer same-address publisher or consumer write survives cleanup, including
across proxy switches. A failed withdrawal does not replay selection or uncommit a sealed definition.
See the [help connection](../openspec/specs/contracts/help-output/spec.md).

## Diagnose failures without inventing acceptance

For native framework diagnostics use `VerseUiDiagnosticLog.CreateHost(consumer, source, manifest,
bindings, theme, metrics, translation, document, options)` in the actual factory, before Host construction.
The original constructor/factories remain legal, and supplied metrics are unchanged. `UiPageWindow` adopts
this route with its key consumer. `VerseUiDiagnosticLog.Attach(host, consumer)` retrieves that binding or
adopts future facts on an existing Host; it cannot recover missed constructor facts. Configure updates
DetailEnabled live, independently of fit/timing/geometry. Host/Session close releases its binding.
Keep Controls.Trace null when detail is off, and do not reprint the same FL facts from a consumer logger.
Standalone callbacks, exact lifecycle, normal/warning/error, daily/detail and bounded output semantics are
in the [domain diagnostic guide](../development/0.8/ui-diagnostics.md). Both new types are public-unstable.

Pure faults use Fault/Retry; managed writable responses use IsPaused/Fault/Resume after their owner checks
the actual business result. Related inputs can retry pure work but cannot resume a paused callback.
Retained paint is display-only; invalid current geometry/input cannot execute. See
[lifetime/recovery](../openspec/specs/core/lifetime-recovery/spec.md) and [open deviations](../openspec/implementation.md):
popup wheel qualification, Warning/release/seal and unaccepted Trace/fit isolation remain visible.

S1–S7 and necessary dependencies are stable as listed in API tiers. Node/Session runtime tables and detailed
R9 are not blanket stable. Surrounding authors use ElementProperties, scroll/scope/invalidation and InputState;
custom authors use their supplied context, not private tables. The external AuthorExamples assembly demonstrates
public-only usage without an internals grant.

The accepted stable-FPS pair2f22bec/833c14f4 and overall internal trialea610fa/833c14f4 retain their identities.
Later fixed compilation uses US3e42cc1 with FL6ec0c31; this guide neither rebuilds nor re-identifies them.
Unrecorded font/IME/overlap/scaling/native details and known headless errors remain limits.
1.6 is delivered; compiled/native1.7 is not. See [platform evidence](../development/0.8/platform-and-real-integrations.md)
and the [window guide](rimworld-window-layout.md).

For maintainers comparing older internal code, preserved
[author-vocabulary](../archive/specs-before-20261029/refactor-migration.md) and
[theme](../archive/specs-before-20261029/theme-migration.md) migration tables are historical aids,
not a parallel API or an upgrade-first route.
