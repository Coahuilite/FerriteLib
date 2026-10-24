# S1–S7: supported author paths

Declare a page with the default kinds, compose selected capabilities in C#, or operate independently
of a page. The [API tiers](../../api-tiers.md) classify the exact surface; [stable-api.txt](stable-api.txt)
pins supported signatures, constants and optional defaults. This is the approved prepublication
replacement described in [refactor migration](refactor-migration.md), not a compatibility shim.

## S1 — declare, compose and replace a page

`UiElementSpec(id, kind, values, children, choices)` copies its inputs. `Values` holds immutable
`UiAttributeValue` literals; `Attributes`/`TryGetAttribute` retain diagnostic/text vocabulary. Use
`Number`, `Boolean` or `Literal` from C#. XML constructs those same values. Number, Boolean, padding
and offset layout consumers read the parsed result, not a second string grammar. Boolean literals
accept trimmed true/false/1/0. Padding accepts one, two or four finite numbers; offsets accept pixels
or0..100% subject to the placement rules. Invalid/contradictory declarations fail with a located error.

`UiLayoutManifest.Parse/ParseFile/Create/CreateWithBackground` use the same definition model.
`Roots`, `Templates`, `Styles` and `Background` are read-only. `UiWidgetDefinition` and
`UiAttributeSchema` supply one root/template/compiler vocabulary; typed binding roles also qualify
custom attributes inside Repeat. `UiKinds` names all18 kinds; the nine containers and non-kind uses
remain in [migration status](migration-status.md). `UiHost.UpdateDefinition` retains the existing
prepare/stage/seal transaction, compatible identities and last-good rollback. File workflows use
`UiDocumentSource`, `UiDocumentService`, `UiReloadPolicy`, `IUiTimeSource` and `IUiMainThread`.

## S2 — connect ordinary or tracked data and requests

`UiBindings` is the author registry. `IUiBindingReader` is the runtime capability given to widgets:
value reads/writes, commands/actions/eligibility, revisions/validation and value/choice endpoints.
Registration and `NotifyChanged` belong to the registry, not the reader. A custom reader can delegate
to an owned registry as the real isolated consumer does. No reflection/type-probing fallback exists.

A forwarding reader implements `BindingOwner => Current`, naming the registry that owns its sealed
endpoints and optional tracked capabilities. Host still reads/invokes through the reader. Ordinary
registrations remain ordinary; this member does not activate tracking or impose reactive thread rules.
Keep one backing during a synchronous pass; replace it between passes. Host validates before replacing
its per-view projection, then invalidates cached input/layout and releases old source demand.
A failed definition can restore the previous backing without losing its prior projection.
Missing owner or mismatched declared Value/Choices endpoints is a located contract error, including
container/root attributes and materialized item declarations. Collection internals are not inferred.
Endpoints retained by author code belong to the registry that minted them: reacquire them from the
proxy after changing backing. The no-friend `ForwardingBindings` example implements the complete route.
Changing the backing does not identify a new business target: stable item/InteractionScope conditions
still distinguish a compatible refresh from retargeting an editor.

- `BindValue/BindReadOnly`, `BindAction/BindCommand` and `NotifyChanged` retain ordinary getter and
  explicit invalidation conditions. `Set` requests the owner's mutation; it does not imply acceptance.
- `BindChoices<T>` carries `UiChoice<T>(text,value,help,textKey)`. String is just T=string. Dynamic
  labels, help and real identity use this same representation; XML `<Choice Value Text Help TextKey/>`
  normalizes into it. Omitted/empty Help means no option-specific help; neither Value nor Text is
  inferred. Old Dropdown string/static Value and typed Text outputs each require explicit Help to
  preserve them (see refactor migration). HoverHelpKey clears for a no-help option; element HelpKey
  and the consumer's unified panel/page-overview fallback remain independent.
  The explicit omission policy is settled; final freeze of the public help mechanism/naming remains
  a separate PM decision. This technical candidate retains HelpKey/HoverHelpKey/Tooltip unchanged.
  BindOptions/GetOptions remain generic collection facilities
  for tree/chart and other collection consumers, not a second selection protocol.
- `UiValueEndpoint` resolves current registration/type/writability/read/write; `UiChoiceEndpoint`
  reads the same choices or a bounded prefix without enumerating the rest.
- `UiReactiveRuntime.Int/Text/Boolean/Value/List/CreateOwner/Batch/ReadPhase`, value/list operations,
  `UiReactiveOwner.CreateChild/Compute/Respond/RespondManaged`, computed Fault/Retry and response
  IsPaused/Fault/Resume retain RU1–RU3. No cross-thread delivery or implicit ordinary-getter purity.
- `BindTracked`, `BindTrackedChoices`, `BindTrackedOptions` enroll pure immutable projections with
  explicit equality. Host-owned `UiBindingPresentation` keeps current validity separate from retained
  display. Custom tracked reads declare actual descriptor binding roles or `IUiPresentationDemand`;
  there is no prepare-everything compatibility whitelist. Ordinary getters gain no such prerequisite.
- `UiNotifyAdapter` explicitly maps subscriptions and releases them on disposal.

See [reactive](reactive-foundation-contract.md) and [failure/lifetime](failure-lifetime-contract.md).

## S3 — connect stable items to current business targets

`UiItemBindings<T>` and `UiItemBindingSchema<T>.Value/Command` describe a source/key/field once.
Refresh validates then updates current targets; same-key replacement and reorder do not redirect a
request to a stale instance, selected object or old ordinal. The model owns business order and refusal.
`UiBindingAddress(source,item,field).Key` is the injective address used by both helper and Repeat;
manual callers use it too. Deletion releases UI connections, not external business objects. Filtering
and temporary hiding retain or release connections according to the author's stated membership owner.
See [item binding](item-binding-contract.md).

## S4 — use only the capabilities a custom control owns

Use `UiWidgetParts` for optional measure/paint/operate/pointer/describe/validate callbacks, or implement
`IUiWidget`. Register a single immutable `UiWidgetDefinition` with `UiWidgetRegistry.Define`, including
its schema, label/font facts and optional natural-body width. There is no Register alias/numbered XML
protocol. `IUiMeasureParticipant` captures complete actual inputs; `IUiTextMetricsEnvironment` adds
truthful font-environment validity without changing `ITextMetrics`.

A supplied `UiWidgetContext` gives Bindings, Controls, Drawing, Theme, Metrics, Translation, ViewWidth,
identity/path, WindowOrigin/ToWindowRect, Child, State, Properties, InteractionScope, Invalidate,
SharedLabelWidth and active/disabled facts. State slots can supply an explicit release callback; hiding
is not destruction, removal/close releases once. Arbitrary business IDisposable objects are not invoked.

`ctx.Controls.Activation/Range/Point/PopupOption/Edit/Editor/SuspendEditor` use the same neutral
`UiInteractionSession`, `UiEditSession<T>`, `UiNumericEdit`, eligibility and geometry contracts as
headless callers. A native pointer/editor requires an explicit owned context and execution; foreign or
released handles are diagnosed, never redirected to an ambient active node. `IUiPointerLayout` supplies
configured operation regions. Descriptions/labels use `IUiDescriptiveWidget`, `IUiLabelledWidget`,
`UiDescription`, `UiTreeRow` and row-band helpers.

For **native controls without a Host**, retain a `UiSession` and create a named control context with
`session.CreateContext(source,scope,metrics,theme,translation,reader,width)`. Capture one frame on the
actual execution owner and call `SetFrame` once per native callback, then operate any number of controls
on that same frame. Dispose session before execution, with execution disposal in a `finally`. The complete no-friend example is
`tools/FerriteLib.UiKit.AuthorExamples/StandaloneNativeNumber.cs`. Pure semantic operations use
`UiInteractionSession`/`UiInteractionRequests` directly, without invented Nodes/Rects.

## S5 — configure appearance or draw independently

`UiTheme`, `UiPalette`, `UiPaletteRole`, `UiColour`, `UiElementProperties`, per-property declarations,
resolved values/origins, `UiDensity`, `UiGeometry`, style document/resolver and finite layout helpers
keep font, five metrics and semantic colours independently removable. Colour repaints; font/geometry
remeasure. Read-only resolved views stay live across normal resolution.

Independent visuals use `UiThemeDraw` with an explicit `IUiDrawing`, theme and neutral rect. They need
no Host/XML/registry/reactive runtime. `VerseFerriteTextMetrics` supplies actual1.6 font rules.
Images are neutral `UiImage` values supplied by `VerseUiExecution.Images`; the adapter owns references
and never destroys supplier textures. A token resolves only within its resource scope.

## S6 — use a standard window or embed in an existing callback

`UiPageWindow` is concrete; `UiWindowCatalog` supports registration/open/activation/lookup/close with
`UiWindowKey`, `UiWindowOptions` and `UiFocusPolicy`. `UiWindowHost` owns its native execution, image
scope and native qualification; its protected shell/failure/close hooks remain supported.

Embedded callers retain one UiHost and one VerseUiExecution, translate the rect and invoke
`host.DrawFrame(rect, execution.CaptureFrame(nativeInputAllowed), execution)`. The explicit frame is
current native fact; window qualification remains the native owner's responsibility. No ambient
owner-free overload remains. Manual frame/measure/draw requires that same explicit preparation.
SetDisplayDemand is reliable visibility, not focus; actual close disposes the owner. InputState,
ElementProperties, scroll/interaction scope/invalidation, Accept/Cancel and attachment hooks avoid
runtime tables. Compatible update, current scroll offset and cross-Host rollback use the same core.

Execution is borrowed by Host/Session. Idle replacement is legal, and sessions can share an outer
execution. `SetFrame` rejects a different execution during an open draw pass, capture or active/focused
edit before changing input, qualification or ownership. End the interaction or explicitly suspend its
editor on the old execution first; a reliable suspended draft survives an idle replacement.
No native focus/capture or image token is transferred. Register images in their new execution scope.
Closing Host/Session never disposes the borrowed execution; its platform owner does so in `finally`.
Session closes logically and attempts every owned release even if native cleanup throws, then reports
aggregate cleanup failures. Native focus names and pointer mappings are execution-scoped, including
when two public callers use the same logical key; disposing one cannot release the other's focus.

## S7 — find errors and inspect bounded facts

Version Require/API, located contract errors, LayoutFault, presentation/recovery status, basic bounded
Diagnostics Snapshot/TryGetLatest/CountOf/Clear/Dispose and overflow/style/reload records are supported.
Detailed R9 discovery/tree/budgets and Dev geometry sampling remain experimental. They are detached
facts, not another model, graph or write handle. The stable Session subset is explicitly listed in
api-tiers.md; Node and the rest of Session runtime plumbing are not blanket promoted.

## Executable evidence

AuthorExamples is a separate no-friend net472 assembly. RefactorExamples executes public-only
editor/typed choice/Repeat/custom, current-target, hidden multi-owner, pure visual/headless and typed
XML/C# layout cases. Adapter.ContractTests executes real VerseUiExecution and these external authors;
Core.ContractTests compiles the entire Kernel without any engine/stub dependency. Main harness retains
all purpose-specific lanes and Dev-only assertions. Native1.6 font/IME/window and1.7 acceptance remain
separate human/platform records.
