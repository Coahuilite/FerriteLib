# Boolean, selection and expansion (0.8 development)

Use the existing `input/checkbox` (both `switch` and `checkbox` appearances), `input/mode-row`,
`input/dropdown` and `container/tree` declarations. Their bindings and registration conditions remain
unchanged. F2 establishes an operation on a qualified primary release; a press arms but does not write.
Consumed releases end owned capture without requesting. F3 closes a popup on an outside primary press
and consumes that input before underlying content runs. An uncovered trigger toggles on its own release.
No new keyboard, Space, focus traversal or global Cancel policy is introduced.

## Compose another presentation with the same behavior

```csharp
var target = UiNative.InteractionTarget(ctx, "enabled");
var input = UiNative.Activation(hitRect, ctx, "enabled", "", writable: canWrite);
var change = UiValueBehavior.Boolean(input, !model.Enabled);
UiValueBehavior.Request(target, change, value => bindings.Set("enabled", value));
// Paint model.Enabled, not change.Value: a request is not business acceptance.
```

`UiValueBehavior.Boolean` carries a target boolean intent. `Selection<T>` retains the real candidate
value (including null); it never parses a display label. `Expansion` additionally requires an applicable
content relationship. `Request<T>` is the common once-per-operation/error boundary, shared with the
activation/range request boundary. Pass an already-established operation once; do not replay its result.
A callback may mutate the model then throw. The contextual exception preserves the cause; there is no
retry, rollback or inference that a successful return means acceptance.

For a headless client, use `UiInteractionSession.Activate` for a semantic operation or
`PointerActivation` with neutral input, effective geometry and eligibility. Feed that result into the
same Boolean/Selection/Expansion operation and `Request`. There is no second headless event loop or
behavior state machine. Semantic operations have no fabricated hover or native focus requirements.
Pointer qualification/capture is common machinery; boolean value, selection identity and expansion's
content applicability remain distinct domain facts rather than new button kinds.

## Keep business scope separate from sibling targets

`UiInteractionTarget` identifies node, target scope, part and item key. `BusinessScope` identifies a
replaced business object; changing it ends the old gesture. The original two-argument constructor keeps
its target-replacement meaning. A composite can supply a common business scope and distinct parts/keys:

```csharp
var target = new UiInteractionTarget(nodeKey, "choices", "option", candidate, objectScope);
var input = interactions.PointerActivation(target, sample, geometry, eligibility);
UiValueBehavior.Request(target, UiValueBehavior.Selection(input, candidate), requestSelection);
```

Stable key equality must remain stable during the gesture. A typed value uses its existing equality
contract, not its label or list index. The native adapter derives business scope from
`UiNode.InteractionScope`; change it when a getter retargets to another object. Traversing a sibling
does not cancel or release the real owner. Reordering keeps capture with its stable item; removal or
omission ends capture rather than transferring a press to the item now occupying the same geometry.
For the same part/item, changing the binding target ends the original gesture immediately. Tree's
default wrapper passes its row source and each explicit action key through `UiRowBandActions` target
metadata; the original three-callback constructor remains valid for a fixed-target custom composition.
When a custom composition retargets callbacks, use the target-aware constructor or its node business
scope. A Dropdown trigger's logical key is separate from its binding: the default uses the stable node,
and the native trigger-key overload lets independent same-node dropdowns retain distinct identities.
Default wrappers and `UiRowBand` use these same identities. Independent composites use the part/key
overloads without a new registration requirement. The previous native overloads remain available.
The legacy raw `DropdownOptionRow(rect, id, session)` hit probe has no stable option identity and keeps
its historical primitive contract; production option lists use `PopupOption` through `UiPopup` instead.
It is not an alternate selection state machine or the migrated default option path.

## Read selection facts without repairing the model

`UiValueBehavior.Observe(options, current, selectable)` returns `UiSelectionObservation` with Index,
Missing and Selectable. The current Dropdown/ModeRow sample is available at `UiNode.Selection`.
It is the latest model-derived observation, not a cached selected-value authority or a historical frame.
A current value missing from all options stays missing; observation never calls a setter.

String Dropdown display keeps its exact value match, then legacy display match, then original current
text. Typed Dropdown keeps its original equality match and empty-display fallback. Both existing
typed/string binding paths remain. Every established explicit selection requests once even when the
model already has that value. Repaint does not request. A read-only value can still be displayed and
its menu inspected; it does not permit an option request.

## State and cleanup owners

- The model owns checked/selected/expanded answers. Tree rows retain their stable keys and the
  consumer's already-flattened hierarchy. UiKit does not reorder, evaluate or invent a graph.
- `UiInteractionSession` owns the single logical pointer capture. `UiSession` owns native capture
  metadata and popup open/anchor/scroll facts. Native adaptation applies capture and consumption;
  it does not duplicate the pointer transitions.
- Popup ownership is the actual trigger instance/target, not its model binding string. Two Dropdown
  nodes may bind the same value without closing, reanchoring or registering each other's menu.
  The historical string/session popup entries remain usable without registering an instance; the
  default wrappers additionally qualify ownership with their actual node/trigger facts.
- Popup option capture spans the content and popup passes. Content reconciliation defers only the
  capture explicitly recorded by that menu's `PopupOption` path, not a target-scope string prefix.
  Popup options and ModeRow options are distinct parts even inside one composite node.
  `EndPopupPass` releases an option omitted from the rendered menu. Close,
  owner hiding/removal, target replacement and session disposal release their own resources.
- `UiRowBand` assigns disclosure/check/body independent part targets. A handled part never also
  invokes the body. Its legacy callbacks still receive the stable row key. Checkbox's explicit
  ActionBind runs after Set and does not run if Set throws; this is wrapper composition, not an
  implicit second command in boolean behavior.
- Checkbox's two looks paint the shared interaction result. Tree's row-band highlight reads its
  disclosure/check/body results before painting; raw MouseDown or hover cannot manufacture pressed
  state. A valid held capture remains visible during repaint, and lost eligibility clears it.
- Selection closes the menu before requesting; callback close stops remaining popup traversal.
  Closing does not undo already-published model changes. Passive IMGUI triggers do not become
  focusable, and no synthetic focus restoration is performed.

The runtime and no-platform contract target import the same `NeutralSources.props` source set, still
within one runtime DLL. Existing S0/S1 request, property and range evidence remains applicable where
unchanged. `KernelBooleanSelectionExpansionTests` adds real-event wrapper, independent and headless
trajectories, sibling/reorder, Used raw MouseUp, outside-dismiss, part priority and failure cases.
The pure contract target exercises the same production value operations without engine stubs.

Native RimWorld 1.6 acceptance is pending: verify both Checkbox looks, ModeRow options, typed/string
menus, Tree parts, reorder/remove while held, other-window qualification, scrolling, outside dismissal,
callback close and the actual native focus remaining unchanged. Record package identity, game build,
UI scale and language. Remote 1.6.4850 source guidance and 1.6.4871 reference compilation do not replace it.

All retained first-version work is tracked in [migration status](migration-status.md).
