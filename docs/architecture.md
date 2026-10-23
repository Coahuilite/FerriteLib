# FerriteLib UiKit — one retained core, explicit platform execution

The active 0.8 [specification](development/0.8/README.md) and [author migration](development/0.8/refactor-migration.md)
describe the approved prepublication refactor. The payload is still one DLL. Core source/dependency,
stub protocol and native game acceptance are separate evidence classes.

## Four parallel questions share stable identities

| Concern | Question | Production homes |
| --- | --- | --- |
| Structure | What exists, and which stable item/part owns it? | UiElementSpec/Manifest, UiNode, UiHost reconciliation |
| Layout | What is measured, allocated, positioned and clipped? | UiLayoutEngine/UiPlacement, neutral UiLayout, ITextMetrics |
| Semantics | What operation is eligible; which capture/draft/request transition occurs? | UiControls, UiInteractionSession, UiEditSession, binding endpoints |
| Appearance | How are those facts painted, and which text/geometry affects measurement? | UiTheme/Style/Properties, UiThemeDraw, default/retained paint |

These are neither four trees nor four threads. Business values stay in the consumer model. One Session
carries transient state and identity; layout owns geometry. Optional UiWidgetParts composes only the
capabilities a custom element uses. Headless behavior and independent visuals require no page model.

## The entire Kernel is neutral source

NeutralSources.props includes all Kernel/**/*.cs, including actual Host/Session/Node/layout scheduling,
transaction/lifetime, compiler, bindings/reactive, all18 default uses and recovery. The same production
source builds the no-platform contract executable and runtime DLL. Transitive references of the former
must resolve to the standard runtime; a planted hidden platform lookup must fail the source guard.
No object/reflection/resource-generic escape carries native textures into that core.

Adapters/RimWorld16 contains native window/font/thread/time/execution mechanisms. VerseUiExecution
captures one actual native event, maps logical controls to native IDs/focus, calls each native leaf
synchronously, returns current scroll offset and restores temporary GUI state in finally. Valid focus
is not rolled back. VerseUiImages owns resource references, not supplier Texture lifetimes. Only
UiImage tokens cross into bindings/core. The containment guard pins actual paths and native members.

## Declarations and updates have one authority

XML and typed C# use the same immutable UiElementSpec Values and UiWidgetDefinition/schema/compiler.
Parsed number/Boolean/padding/offset results reach layout without a second lexical interpretation.
Static/dynamic choices use UiChoice<T>; strings are not a separate protocol. Author registry and runtime
reader are distinct. UiBindingAddress is shared by item helpers and Repeat, so reorder/current-object
requests cannot fall back to dotted ambiguity or row number identity.

RU1–RU3, finite layout math/reuse and cross-Host prepare/stage/seal rollback retain their production
algorithms. Hiding is not destruction: reliable drafts/local state survive while another Host continues
shared business. Actual removal/close releases only its UI owner. A normal request return ends that
request before display readback; failed readback/paint cannot replay it. Retained fault paint does not
re-enter native input. Explicit Retry/Resume and business acceptance remain different operations.

## Independent native, visual and semantic use

Retain a UiSession and call CreateContext with an explicit name for native controls without Host/XML/
reactive. SetFrame once at the actual callback boundary, then use ctx.Controls on that same owner/frame.
StandaloneNativeNumber is an external no-friend real author. Pure visuals take IUiDrawing/theme/neutral
geometry; pure semantics take interaction target/eligibility and need no fabricated node/rect.

Detailed public paths and stable-member exceptions are in [S1–S7](development/0.8/public-contract.md).
Every retained use remains in the [complete inventory](development/0.8/migration-status.md). Native1.6
fonts/IME/window checks require human playtest;1.7 uses the separately identified real snapshot and has
no delivered compiled/native adapter. Historical0.7 evidence remains under its versioned documents.
