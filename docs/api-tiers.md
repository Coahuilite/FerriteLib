# Public API tiers — 0.8 prepublication refactor

The stable target covers S1–S7 and their necessary parameter, return, callback and implementation-interface dependencies. It protects a supported game/adapter target, not a native acceptance result. Current 1.6 and pending 1.7 evidence remain separate.

- **Stable:** protect source, already compiled consumer DLLs, XML vocabulary, data meaning, legal use conditions and documented behavior. A replacement must work before deprecation. Deprecation in 0.9 keeps the old contract working through both complete subsequent minors 0.10 and 0.11; earliest removal is 0.12. Only published formal minors count; patches/RCs do not. Moving to 1.x cannot shorten an existing window.
- **Public-unstable:** usable experimental/advanced adapter or diagnostic surface. Later minor changes need an explicit migration; a frozen 0.8.x patch cannot arbitrarily break it.
- **Internalize-candidate:** no exported types remain in this tier after the explicit prepublication migration below.

After publication, breaking a supported stable contract requires a major release. The two-minor
deprecation window is an additional minimum, not permission to remove it in a minor release.

Consumer `[min,max)` checks remain binding even when old signatures are retained. No 0.7 lockstep exemption is inherited. This prepublication 0.8 development change may migrate controlled internal consumers directly; it does not establish a permanent dual API or erase configuration/save data.

The type census and stable set are pinned in `FerriteLibApiTierTests`. Its dependency-closure check covers exported/protected signatures, generic arguments and extension interfaces; explicit advanced-member exceptions are listed below. A stable implementation never requires those advanced members.

## Stable

- `FerriteLibVersion` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `ITextMetrics` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `VerseFerriteTextMetrics` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiFont` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiKitFonts` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `IUiTranslation` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `IUiWidget` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiWidgetRegistry` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiContractException` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiUnknownWidgetKindException` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiStatusTone` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiOverflowAxis` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiItemBindings` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiItemBindingSchema` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiMeasureInputs` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiMeasurement` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `IUiTextMetricsEnvironment` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `IUiMeasureParticipant` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiReactiveRuntime` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiReactiveValue` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiReactiveList` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiReactiveComputed` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiReactiveOwner` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiBindingPresentation` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiPresentationStatus` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiReactiveFault` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiReactiveResponse` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `IUiPresentationDemand` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiInteractionTarget` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiHitGeometry` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiPointerPhase` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiInteractionEnd` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiInputEligibility` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiPointerSample` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiRange` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiInteractionResult` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiInteractionSession` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiValueOperation` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiEditSession` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiEditParser` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `IUiEditState` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiNumericEdit` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiDescriptionRole` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiRangeObservation` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiDescription` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `IUiDescriptiveWidget` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `IUiLabelledWidget` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiLayoutDiagnostic` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiLayoutSize` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiLayoutPoint` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiLayoutRect` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiLayoutHeight` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiLayoutFrame` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiLayoutChild` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiLayoutOffset` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiLayoutAnchor` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiLayoutPlan` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiLayoutMeasure` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiLayout` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiWidthMode` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiWidthDemand` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiWidthPlan` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiSelectionObservation` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiValueBehavior` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiInteractionRequests` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiPropertyOrigin` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiPropertyDeclaration` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiResolvedProperty` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiPropertyResolver` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `IUiPointerLayout` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiPointerRegion` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiImageFit` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiImageAlignX` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiImageAlignY` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiImageOptions` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiImageLayout` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiWindowBackground` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiPaletteRole` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiColour` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiPalette` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiDensity` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiSliderLayout` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiElementProperties` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiChoice` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiHost` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiElementSpec` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiWidgetContext` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiBindings` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiTheme` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiThemeDraw` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiFitAudit` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiLayoutManifest` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiLayoutSnapshot` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiWindowHost` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiWindowChrome` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiWindowNotice` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiChartPointChange` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiOverflowReport` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiSurfaceStyle` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiGeometry` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiEmphasis` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiResolvedStyle` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiStyleTable` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiNodeId` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiStyleFallbackReport` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiStyleDocument` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiStyleResolver` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiStyleDeclaration` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiStyleIssue` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiWindowKey` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiWindowOptions` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiWindowCatalog` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiPageWindow` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiFocusPolicy` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiDocumentKind` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiDocumentSource` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiDocumentService` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiReloadReport` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiInvalidation` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiDiagnosticSubscription` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiDiagnosticEvent` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiDiagnosticKind` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiDiagnosticTiming` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiTreeRow` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiRowBand` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiRowBandLayout` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiRowBandActions` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `IUiMainThread` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `VerseFerriteMainThread` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiNotifyAdapter` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `IUiTimeSource` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `VerseFerriteTimeSource` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiReloadPolicy` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiReloadSchedulerState` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiWidgetDescriptor` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiWidgetCatalog` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiKinds` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).
- `UiHostInputState` — supported 0.8 usage; ownership and behavior are in [S1–S7](development/0.8/public-contract.md).

- `VerseUiExecution` — explicit author/adapter contract; current consumers and ownership are in the refactor migration contract.
- `VerseUiImages` — explicit author/adapter contract; current consumers and ownership are in the refactor migration contract.
- `IUiBindingReader` — explicit author/adapter contract; current consumers and ownership are in the refactor migration contract.
- `UiBindingRole` — explicit author/adapter contract; current consumers and ownership are in the refactor migration contract.
- `UiAttributeKind` — explicit author/adapter contract; current consumers and ownership are in the refactor migration contract.
- `UiAttributeValue` — explicit author/adapter contract; current consumers and ownership are in the refactor migration contract.
- `UiAttributeDefinition` — explicit author/adapter contract; current consumers and ownership are in the refactor migration contract.
- `UiAttributeSchema` — explicit author/adapter contract; current consumers and ownership are in the refactor migration contract.
- `UiBindingAddress` — explicit author/adapter contract; current consumers and ownership are in the refactor migration contract.
- `UiValueEndpoint` — explicit author/adapter contract; current consumers and ownership are in the refactor migration contract.
- `UiChoiceEndpoint` — explicit author/adapter contract; current consumers and ownership are in the refactor migration contract.
- `UiControls` — explicit author/adapter contract; current consumers and ownership are in the refactor migration contract.
- `UiFrame` — explicit author/adapter contract; current consumers and ownership are in the refactor migration contract.
- `UiFramePhase` — explicit author/adapter contract; current consumers and ownership are in the refactor migration contract.
- `UiKey` — explicit author/adapter contract; current consumers and ownership are in the refactor migration contract.
- `UiTextAnchor` — explicit author/adapter contract; current consumers and ownership are in the refactor migration contract.
- `UiControlKey` — explicit author/adapter contract; current consumers and ownership are in the refactor migration contract.
- `UiImage` — explicit author/adapter contract; current consumers and ownership are in the refactor migration contract.
- `UiNativeEdit` — explicit author/adapter contract; current consumers and ownership are in the refactor migration contract.
- `UiNativeEditResult` — explicit author/adapter contract; current consumers and ownership are in the refactor migration contract.
- `IUiExecution` — explicit author/adapter contract; current consumers and ownership are in the refactor migration contract.
- `IUiDrawing` — explicit author/adapter contract; current consumers and ownership are in the refactor migration contract.
- `IUiWidgetCapabilities` — explicit author/adapter contract; current consumers and ownership are in the refactor migration contract.
- `UiWidgetParts` — explicit author/adapter contract; current consumers and ownership are in the refactor migration contract.
- `UiWidgetDefinition` — explicit author/adapter contract; current consumers and ownership are in the refactor migration contract.
- `UiSession` — explicit standalone control ownership; only the member subset below is stable.

## Public-unstable

- `UiNode` — advanced adapter/runtime inspection; not required by the stable author paths.
- `UiHitLayer` — advanced adapter/runtime inspection; not required by the stable author paths.
- `UiPopup` — advanced adapter/runtime inspection; not required by the stable author paths.
- `UiSessionGuard` — advanced adapter/runtime inspection; not required by the stable author paths.
- `UiPointerLayer` — advanced adapter/runtime inspection; not required by the stable author paths.
- `UiPointerLayers` — advanced adapter/runtime inspection; not required by the stable author paths.
- `UiDiagnosticHub` — advanced adapter/runtime inspection; not required by the stable author paths.
- `UiObservationStatus` — advanced adapter/runtime inspection; not required by the stable author paths.
- `UiObservationBudget` — advanced adapter/runtime inspection; not required by the stable author paths.
- `UiObservedFact` — advanced adapter/runtime inspection; not required by the stable author paths.
- `UiObservedNode` — advanced adapter/runtime inspection; not required by the stable author paths.
- `UiHostDiscovery` — advanced adapter/runtime inspection; not required by the stable author paths.
- `UiHostObservation` — advanced adapter/runtime inspection; not required by the stable author paths.
- `UiDevGeometrySnapshot` — advanced adapter/runtime inspection; not required by the stable author paths.
- `UiDevNodeSnapshot` — advanced adapter/runtime inspection; not required by the stable author paths.
- `UiDevInputSnapshot` — advanced adapter/runtime inspection; not required by the stable author paths.
- `UiDevBoundary` — advanced adapter/runtime inspection; not required by the stable author paths.

## Internalize-candidate

None. The former registrar, layout engine and default implementation classes are internal. Use `UiWidgetRegistry.InitializeCore`, `UiKinds`/XML and public shared behaviors rather than instantiating a default widget type. Custom widgets implement `IUiWidget`; they do not inherit a default renderer.

## Advanced members on otherwise stable types

- `UiWidgetContext.Session`, `.Node`, `.WithNode` and constructors taking a Session/Node remain advanced adapter construction seams. Ordinary custom widgets receive a context; use ElementId/ElementPath, Child, State, Properties, InteractionScope, Invalidate, SharedLabelWidth and IsActive/IsDisabled instead.
- `UiHost.Session` and `UiWindowHost.Session` remain advanced legacy adapter access. Stable surrounding windows use InputState, ElementProperties, Get/SetScrollPosition, SetInteractionScope, Invalidate, DrawFrame, Accept/Cancel and Close/Dispose.
- `UiHost.Observe`, `.ObserveLiveHosts`, `.ObserveLiveHost` and their R9 result/budget dependencies are experimental. So are `UiDiagnosticSubscription.GeometryEnabled`, `.GeometryOverlay`, `.DumpGeometry` and `.TryGetGeometrySnapshot`; basic bounded diagnostic records/subscription/disposal are stable.
- `UiControls.Trace` is advanced diagnostic instrumentation. `UiSession` supports only construction, `CreateContext`, `SetFrame`, `Controls`, `IsActive`, `Identity`, `Dispose` and the inherited IDisposable contract as stable. All other Session members remain advanced Host/runtime plumbing. No Session/Node tables are promoted.

Mutable dependency graphs, queues, caches, default private nodes, geometry staging and event-routing tables are internal. R9 returns detached bounded facts, never those tables or business write handles. Detailed budgets and sampled fields remain experimental while the read facility is delivered.

## Contract and migration references

No ambient UiNative bridge or context-free core hit overload remains. Native controls use explicit
UiSession.CreateContext and UiControls, with a single callback frame and execution owner. The only
raw backend contacts are path-pinned and member-pinned in KernelContainmentTests:
- Adapters/RimWorld16/VerseUiExecution.cs — native input/capture/focus/editing/draw/clip/scroll/resources.
- Adapters/RimWorld16/VerseFerriteTextMetrics.cs — real-font measurement and temporary-state restoration.
- Adapters/RimWorld16/UiWindowHost.cs — window pointer attribution and its own chrome close button.
The complete Kernel has no backend allowance. The positive control rejects adapter basenames at wrong
paths, ungranted members, qualified calls and retired ambient calls, including nested arguments.

- [S1–S7 exact paths and dependencies](development/0.8/public-contract.md)
- [Complete 18-use/nine-container migration inventory](development/0.8/migration-status.md)
- [Theme/API/XML migration](development/0.8/theme-migration.md)
- [Editing and combination behavior migration](development/0.8/editing-contract.md)
- [Consumer development entry](consumers/consume-from-0.8.0-dev.md)

Historical 0.4–0.7 changes retain their original evidence and per-line contracts under `docs/development/`; they do not override this 0.8 commitment.
