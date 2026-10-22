# S1–S7: supported author paths

Use the default kinds to describe a page, or implement `IUiWidget` for custom content. The stable
surface includes each required parameter, return type, callback and implementation interface, not
just an entry-point name. [API tiers](../../api-tiers.md) lists every exported type and the explicit
advanced-member exceptions; [stable-api.txt](stable-api.txt) records every stable public/protected
signature, constant and optional default. The harness compares that exact surface and checks its
transitive signature dependencies.

## S1 — declare, compose and replace a page

- `UiElementSpec(id, kind, attributes, children)` copies its inputs. Read `Id`, `Kind`, `Attributes`,
  `Children` and `TryGetAttribute`; definitions are not mutable runtime nodes.
- `UiLayoutManifest.Parse`, `ParseFile`, `Create` and `CreateWithBackground` produce the same validated
  definition model. `Roots`, `Templates`, `Styles` and `Background` describe its content.
- `UiKinds` supplies all 18 registered kind identifiers. The nine container names and the complete
  supported attribute/use inventory remain in [migration status](migration-status.md).
- `UiHost.UpdateDefinition` performs explicit prepare/stage/seal. Compatible identity retains applicable
  state; failed candidates keep the last good tree. File workflows use `UiDocumentSource`,
  `UiDocumentService.Add/Attach/Signal/Pump/Reload/Detach/Dispose`, `UiReloadPolicy`, `IUiTimeSource`
  and `IUiMainThread`. No document service is required for programmatic updates.

Repeat's source is an ordered key projection; templates qualify supported binding roles to that key.
Visibility, finite anchors, shared labels and UpdateScope retain their existing contracts. See the
[assembly](assembly-contract.md) and [layout](layout-contract.md) contracts.

## S2 — connect data, requests and independent owners

- `IUiBindings` and `UiBindings` include `BindValue`, `BindReadOnly`, `BindOptions`, `BindAction`,
  `BindCommand`, `Get/TryGet/TryGetBool`, `Set`, `GetOptions`, `Invoke/TryInvokeCommand`, `CanExecute`,
  `IsWritable`, validation, `NotifyChanged`, `GetRevision` and `GetInvalidation`. Hand-written
  implementations implement this whole interface. `IUiTypedChoices`, `UiChoice<T>` and `UiOption`
  supply optional typed/string choices without stringifying business identity.
- `UiReactiveRuntime.Int/Text/Boolean/Value/List/CreateOwner/Batch/ReadPhase` provide explicit sources,
  equality, serial ownership and pure-read enrollment. Ordinary getters keep their old conditions.
- `UiReactiveValue.Value`, controlled `UiReactiveList` operations and `Snapshot`,
  `UiReactiveOwner.CreateChild/Compute/Respond/RespondManaged/Dispose`,
  `UiReactiveComputed.Value/Fault/Retry/Dispose` and `UiReactiveResponse.IsPaused/Fault/Resume/Dispose`
  preserve RU1–RU3. Model authority, safe result delivery and callback recovery remain distinct.
- `UiBindings.BindTracked/BindTrackedOptions/BeginOperation/CreatePresentation` and
  `UiBindingPresentation.Synchronize/Enter/GetStatus/Retry/Dispose` support explicit consumers as well
  as Host-owned presentation. `UiPresentationStatus` and `UiReactiveFault` expose current validity.
- `UiNotifyAdapter` maps external notifications explicitly and unsubscribes on disposal. No automatic
  deep observation or cross-thread delivery is inferred.

The [reactive](reactive-foundation-contract.md) and [failure/lifetime](failure-lifetime-contract.md)
contracts define timing, failure and ownership. A dispatched request is not business acceptance.

## S3 — describe item connections once

`UiItemBindings<T>(bindings, itemsKey, source, identity, configure)` and
`UiItemBindingSchema<T>.Value/Command` register typed current-target fields/commands. `Keys`, `Refresh`
and `Dispose` manage UI connections over reference objects. Same-key snapshots update targets;
duplicate keys are located before mutation; deletion releases presentation subscriptions and caches.
Reorder never rewrites business order. See the [item contract](item-binding-contract.md), including
filtering, active notifications and third-party snapshot bridges.

## S4 — customize without inheriting a default implementation

Implement `IUiWidget.Kind/Configure/Validate/Measure/Draw`, register a factory with
`UiWidgetRegistry.Register`, and declare its real attribute/label schema. `IUiMeasureParticipant`
and `UiMeasurement`/`UiMeasureInputs` opt into captured-input reuse; `IUiTextMetricsEnvironment`
supplies truthful per-font environment validity without changing `ITextMetrics`.

Use the provided `UiWidgetContext`: Bindings, Theme, Metrics, Translation, ViewWidth, ElementId,
ElementPath, WindowOrigin, ToWindowRect, Child, State, Properties, InteractionScope, Invalidate,
SharedLabelWidth and IsActive/IsDisabled. A named State slot belongs to that custom instance; do not
write a shared editor's compatibility projections as a second draft authority. Child identities and
state release with their owner. Changing InteractionScope declares a genuinely different target.

`UiNative.Activation/Range/Point/PopupOption` bridge native facts to `UiInteractionSession`;
`UiInteractionTarget`, `UiInputEligibility`, `UiPointerSample`, `UiHitGeometry`, `UiRange` and
`UiInteractionResult` are the complete neutral dependencies. `UiInteractionRequests` and
`UiValueBehavior` dispatch once. `IUiPointerLayout`/`UiPointerRegion` declare effective configured
operation geometry; unknown old custom Draw keeps its conservative behavior rather than acquiring
an implicit purity or registration prerequisite.

`UiNative.Edit/Editor/SuspendEditor`, `UiEditSession<T>`, `UiEditParser<T>`, `IUiEditState` and
`UiNumericEdit` share editing/composite behavior with standalone nonrendering callers. Native
character editing remains the adapter's role. Descriptive/label contracts and tree row bands use
`IUiDescriptiveWidget`, `IUiLabelledWidget`, `UiDescription`, `UiTreeRow` and `UiRowBand` dependencies.
Default widget implementation types and the layout engine are now internal; use kinds and these seams.

## S5 — configure appearance or use visual core alone

`UiTheme`, `UiPalette`, `UiPaletteRole`, `UiColour`, `UiElementProperties`, per-property declarations,
resolved values/origins, `UiDensity`, `UiGeometry`, `UiStyleDocument/Resolver/Declaration` and style
issues form the supported finite appearance path. Set/remove individual metrics, fonts and semantic
colours; Scheme then Density combine per property. Bare container allocation is separate.

Independent visual clients use `UiThemeDraw`, `ITextMetrics`, `UiFont/UiKitFonts`,
`VerseFerriteTextMetrics` and neutral layout/image helpers without a Host or registry. Text/font and
distance changes remeasure; pure colour changes repaint. Image suppliers retain resource ownership.
See [properties](property-contract.md), [theme migration](theme-migration.md) and
[layers/images](layers-images-contract.md). The current drawing/text adapter is 1.6-specific.

## S6 — choose a standard or embedded host

Construct `UiPageWindow` directly, or use `UiWindowCatalog.Register/Open/Activate/TryGet/Close/CloseAll`
with `UiWindowKey`, `UiWindowOptions` and `UiFocusPolicy`. `UiWindowHost` is the custom-shell extension,
not a prerequisite. Protected shell/failure/close hooks and their callback dependencies are stable.

Embedded windows create one `UiHost`, call `DrawFrame(rect, realOwner)` and Dispose on actual close.
The original owner-free DrawFrame and manual frame/measure/draw entries retain their legal conditions.
`SetDisplayDemand`, `InputState`, `ElementProperties`, `Get/SetScrollPosition`, `SetInteractionScope`,
`Invalidate`, `TryHandleAccept/Cancel`, `Close/Dispose`, attachment events and `PageHost` provide the
necessary lifetime/use path without Session tables. InputState is a current fact, not a cached queue.
See [hosts and observation](host-observation-contract.md); native window authority remains native.

## S7 — locate errors and release diagnostics

`FerriteLibVersion.Api/Require`, `UiContractException`, `UiUnknownWidgetKindException`,
`UiHost.LayoutFault`, presentation status and recovery facts provide located contract/current-value
errors. `host.Diagnostics` returns a bounded `UiDiagnosticSubscription`; Snapshot/TryGetLatest,
CountOf/Clear/Dispose and `UiDiagnosticEvent/Kind/Timing`, overflow/style/reload records provide basic
diagnostics. Text wording and cache counters are not a business protocol.

Detailed R9 discovery/tree/budgets and Dev geometry sampling remain delivered experimental entries.
Ordinary author paths above do not depend on them. Graphs, queues, caches, private nodes and writable
runtime tables remain internal; the detailed reader returns detached bounded facts.

## Compile and run the author examples

`tools/FerriteLib.UiKit.AuthorExamples/AuthorPaths.cs` is a separate assembly without an internals
grant. It compiles standard/embedded pages, custom shared editing, independent visual consumption and
active/third-party item bridges against the public contract. The main harness's `--closeout-api` lane
runs those compiled examples plus stable type/signature/dependency and containment checks. This is
source/compiled-consumer/adapter evidence; native 1.6/1.7 acceptance stays separate.
