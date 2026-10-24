# Prepublication refactor: one core and explicit execution

The 0.8 author model replaces the unpublished compatibility shapes. Use the supported paths in
[public contract](public-contract.md); no legacy resolver or switch selects a second implementation.
The payload remains one net472 DLL. Native1.6 validation and1.7 research remain separate axes.

## Move author calls to the actual owner

| Previous entry | Current entry and condition |
| --- | --- |
| IUiBindings author+reader / IUiTypedChoices companion | UiBindings registration; IUiBindingAccess runtime view; UiValueEndpoint/UiChoiceEndpoint |
| UiOption, string/typed probes, OptionN XML | UiChoice<T> and BindChoices/BindTrackedChoices; XML Choice child values normalize at the boundary |
| Register overloads / known-widget prepare whitelist | One UiWidgetDefinition + UiAttributeSchema via Define; complete tracked roles or IUiPresentationDemand |
| String layout reparsing | UiAttributeValue results shared by compiler/layout/placement/default consumers; diagnostic Text retained |
| Dotted item address | UiBindingAddress(source,item,field).Key used by helper, Repeat and manual callers |
| Ambient UiNative and session-plus-key native bridges | Explicit owner UiWidgetContext.Controls; no-Host session.CreateContext; one SetFrame per actual callback |
| Native geometry/Colour/Texture in core | UiLayoutRect/Point/UiColour/UiImage; conversion/resource ownership at VerseUiExecution |
| Writable UiValueState projections | Shared editor/interaction state machine; explicit typed node-local State for custom-only data |
| Font XML redirect | Metric Token="Font"; retired Font element is a located invalid document, last-good kept |
| Drawing helpers using global GUI | UiThemeDraw takes IUiDrawing; independent visuals do not acquire a page prerequisite |
| IUiBindingReader | IUiBindingAccess: the same read/write/request capability, including ordinary/tracked proxies |
| HelpKey / Choice.Help / XML Choice Help | HelpRef; constructor named argument helpRef; opaque consumer topic reference |
| HoverClaim / HoverClaimElement / ClaimHover | CurrentHelpRef / CurrentHelpSource / PublishHelp |
| HoverGraceFrames / BeginHoverClaimFrame | HelpGracePasses / BeginHelpPass; retain the original pass-based grace sequence |
| HoverHelpKey | HoveredOptionHelpOutputBind; optional writable string output, not a prerequisite for Session help |
| VisibleKey / WidthKey / SelectedKey / PayloadKey | VisibleBind / WidthBind / SelectedBind / PayloadBind; actual binding addresses |
| ActiveTabKey / UiNode.CancelBindingKey | ActiveTabBind / UiNode.CancelBind; literal active-tab address unchanged |

Translation TextKey/LabelKey/TitleKey/PlaceholderKey/DescriptionKey stay keys. Real element identity
parameters and UiNodeId/UiControlKey/item keys stay identities. Registry/access parameters formerly
called elementId/actionId now say binding/actionBind; migrate named arguments. UiPopup.DrawChoices
and Controls.PopupOption use popupId because that value is a session-local popup identity, not
necessarily a declared element or binding. No old aliases remain. Tooltip still takes explicit
presentable text; the caller translates it, and FL never resolves a help catalog.

Forwarding IUiBindingAccess implementations now return their current registry from BindingOwner.
This names existing endpoint/presentation capabilities; it does not replace the reader with UiBindings
or enroll ordinary getters in tracking. Host and DisplayOnly/demand/compound operations consume that
same owner. Switch backing only between synchronous passes and reacquire retained endpoints.
Missing or mismatched declared endpoint ownership is diagnosed on widgets, containers/roots and
materialized item subtrees. The shared compiler keeps source/id/path on the error; it does not guess
arbitrary collection internals. Public ForwardingBindings and the isolated Raven reader use this path.

Omitted or empty Choice.HelpRef means no option-specific help for every value type; Value and Text are
never inferred as help. This confirmed policy changes two old Dropdown
HoverHelpKey defaults: string/static rows published Value, while typed rows published Text.
Preserving either old output requires explicit HelpRef=oldValue or HelpRef=oldText at the author boundary.
For example, use `new UiChoice<string>(text, value, helpRef: value)` for the old string output, or
`new UiChoice<int>(text, value, helpRef: text)` for the old typed output. XML authors explicitly set
`<Choice Value="a" Text="Alpha" HelpRef="a"/>` to preserve the old static value output.
This is a semantic migration, not a rename. Per-row explicit HelpRef, hover transitions/clearing and
typed value identity remain supported by the same runtime protocol. An option with no HelpRef withdraws
its owned output and falls back to the element HelpRef, including a hovered popup row. A consumer's
unified help panel and page-overview fallback are not disabled by an option having no help.
TextKey resolution follows the same normal draw/popup/bounded observation
label path. BindOptions/GetOptions still serve arbitrary non-choice collections; selection widgets
require the actual typed choice endpoint and diagnose a value registered in its place.

The optional output is a publisher connection, not an unconditional mirror of whichever pointer the
last Host saw. An empty/unvisited option only withdraws that publisher's last still-owned value.
Initial/consumer-written values and another element/Host's newer publication survive, even when the
topic strings match. This corrects old stale/overbroad clearing behavior. Hide, empty choices, removal,
popup/Host close and successful backing replacement release the existing connection; failed backing
validation keeps the prior one. A consumer Set or NotifyChanged relinquishes old publication ownership.
Unannounced ordinary getter changes retain their ordinary notification conditions; cleanup also compares
the actual value before writing. A later actual hover can publish again.

Both publication and withdrawal use the owner-bound UiValueEndpoint obtained through access.Value.
This is the same existing capability selection widgets use, not a universal proxy.Set interception
promise. The node retains that endpoint so a proxy backing switch cannot redirect old cleanup into
the new registry. Proxies must forward endpoint and BindingOwner consistently; no access-capture API,
new switch prerequisite, global publisher table or general lease framework is introduced.

## Four questions share facts, not four state machines

Structure retains stable node/item/part identity and membership. Layout owns measured arrangements,
viewport/clip and reuse validity. Semantics owns operation eligibility, requests, capture transitions
and editor drafts. Appearance reads those facts and contributes actual measurement inputs. Business
values/acceptance stay in the model. UiWidgetParts composes only selected capabilities; independent
visuals and headless behavior do not require it or a registry. Native needs explicit geometry and
owner/context; missing/foreign/disposed ownership is diagnosed rather than guessed.

Core ownership remains synchronous: prepare → one native leaf call → shared editing/request/readback →
finally restore temporary GUI state. Legitimate native focus is not rolled back. Retained fault paint
has no input/request replay. No async interface pair, task scheduler, DI, bus or generic renderer was
introduced. Existing RU1–RU3/layout math/reconciliation/last-good/cross-Host transactions are retained.

## Complete source boundary and current consumers

`NeutralSources.props` imports **all Kernel/**/*.cs**, including actual UiHost, UiSession, UiNode,
UiLayoutEngine, context, default wrappers, compiler, reactive/edit/recovery/document logic. The
no-platform contract target records the expanded source and checks BCL transitive closure plus planted
platform/reflection lookups. The same source, not a copied behavior model, builds the one runtime DLL.

Only Adapters/RimWorld16 contains UiWindowHost/PageWindow/Options/Catalog, UiKitFonts,
VerseFerriteMainThread/TextMetrics/TimeSource and VerseUiExecution/VerseUiImages. It owns native event,
control IDs/focus, IME, font measurement, clip/scroll/window and resource references. No native object
is hidden in a core object/reflective lookup or resource generic. KernelContainmentTests pins actual
native contacts by complete relative path and member, including wrong-path negative controls.

| New abstraction | Current production/author consumers |
| --- | --- |
| UiFrame, IUiExecution, UiControlKey, native edit request/result | UiHost/UiSession/UiControls + VerseUiExecution; Adapter.ContractTests and StandaloneNativeNumber |
| IUiDrawing, UiTextAnchor, UiImage | UiThemeDraw/default+retained paint, image/background layout; VerseUiExecution and independent public author visuals |
| IUiBindingAccess, value/choice endpoints, Choice<T> | All default/custom widgets, UiBindings/presentation/item scopes/observation; public author and isolated source reader |
| UiAttributeValue/schema/definition/binding roles | Manifest/Create, common root/template compiler, registry/default wrappers, custom author descriptor and Repeat |
| UiWidgetParts/capability selectors | Public custom author paint and chosen pointer/description composition; engine dispatch reads those selected parts |
| UiBindingAddress | UiItemBindings, actual Repeat qualification, shared inventory sample and three real-source probes |
| Explicit node-local release callback | External custom lifetime example and document/remove/failed-arrangement recovery |
| Session.CreateContext | External native no-Host author; existing Host uses its ordinary context factory |

The complete18-use/nine-container/non-kind inventory remains [migration-status](migration-status.md),
not a reduced vertical-slice checklist. Original E/N, qualified pointer, failure/recovery, readonly,
image/layer, hidden/reorder/current-target and R9 counterexamples remain executable. Harness fixtures
translate events and read native recording facts only; they do not own draft/business/capture behavior.
An actual Used event is reused by later helpers/owners, never recreated by debug flags.

## Author migration and support limits

Rebuild against the fixed candidate. For an existing IMGUI callback retain its explicit execution,
translate native rects once and capture/set once at the callback boundary. A session-owned standalone
context provides the same editor without Host/XML/reactive. Use Child names for distinct parts; don't
reuse an editor identity for a different target. Session disposal precedes execution disposal.

Execution is borrowed, not automatically fixed forever. Idle owners may change it, including an idle
session sharing another session's outer execution. SetFrame rejects replacement during the owner's
open pass/capture/active edit before changing frame or input qualification; use the current execution
to end capture or SuspendEditor first. Drafts survive a legal idle switch. Native mappings and tokens
do not migrate; supplier resources remain with their original execution scope.
Dispose the borrowed execution in finally after Session/Host disposal. Session remains closed and
attempts all releases if one native release throws; the outer owner still clears its mappings/images.
VerseUiExecution derives native focus names from its existing scope plus the unchanged logical key.

Native images must be registered in that execution's Images scope. Store only UiImage in bindings;
release references at owner close without destroying supplier objects. Ordinary getter notifications,
tracked purity/equality, native WindowStack qualification and font validity remain distinct conditions.
Old custom tracked readers now declare their actual demand; ordinary untracked access is not enrolled.

The three current source migrations do not declare HoveredOptionHelpOutputBind: Camera has no selection list;
Nivarian uses typed project choices without a help sink; Raven ToolUi.Select uses typed choices
without a help sink. Consequently neither old fallback is consumed by these three actual pages.
Other authors must apply the explicit migration above if they need either old output.

Three isolated real-source migrations are rebuilt under the new refactor evidence, while old closeout
packages/logs stay read-only. Nivarian explicitly proves hidden/restored owner versus continuing shared
business and UI-selected a versus actual current b/index2. Camera retains real matching/cache bodies;
Raven uses source-linked ToolView/ToolUi/reader/live text plus full consumer compile. These controlled
platform collaborators do not establish in-game font/IME/window acceptance.

1.7 evidence is the approved snapshot1.7.4981rev136; synchronization lacked deps.json. Unity-named
compatibility types are not neutral core evidence. A1.7 compiled/native adapter and direct drawing
backend are not delivered. No game is installed/launched or real Config/save changed by this work.
