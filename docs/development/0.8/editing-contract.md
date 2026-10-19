# Shared editing and numeric composition

Use the existing text and numeric kinds. `Multiline="true"` selects basic plain text without adding
a new kind or an author-wired private text engine:

```xml
<Widget Id="name" Kind="input/text-field" Bind="Name" />
<Widget Id="notes" Kind="input/text-field" Bind="Notes" Multiline="true" Height="96" />
<Widget Id="amount" Kind="input/number-field" Bind="Amount" Min="0" Max="100" />
<Widget Id="level" Kind="input/stepper-slider" Bind="Level" Min="0" Max="100" Step="1" />
```

User-approved subitems: default `Live=false`, retention of invalid/conflicting drafts, and Cancel
changing only the draft (no business rollback). The N1 example model10/draft20/Step1/Live=false is also
approved: plus immediately requests21 and ends the draft, not merely changing the draft to21.
E2 confirmation/normal draft ending, valid automatic blur and other unapproved E/N details remain
reversible candidates; N2/N3 are not promoted by the N1 answer. Approved F1/F2/F3 remain unchanged. The full remaining inventory is maintained only
in [migration status](migration-status.md).

The current sole consumer US requires `Live=true` when it later migrates to this FL line, so that
consumer must declare it explicitly. US has not adopted this Dev and is not changed here. Explicit
`Live=true` remains legal for every author; the approved default does not forbid it.

## Draft and model are different facts

`UiEditSession<T>` is the only draft authority. Each node's native editor slot references this same
production type; `UiValueState.EditText`, `Focused` and `FloatValue` are compatibility projections.
`UiNumericEdit` supplies finite invariant parsing, formatting, clamping and local step composition.
No editor caches a second business value. `Observe` samples the actual model; a normal setter return
does not imply business adoption.

The native `UiNative.Edit<T>` driver, default wrappers and nonrendering clients use these same source
files and transitions. Independent native widgets can pass their formatter/parser, request callback
and actual-model getter to `UiNative.Edit`; `UiNative.Editor<T>` accesses their instance-local draft.
Use the widget's normal context/node scope, just as for the other session-owned native primitives.
The no-platform contract target compiles these actual production sources without Unity/Verse stubs.

## Approved subitems, candidate details and existing entry conditions

* **Approved default:** implicit `Live` changes from `true` to `false` for all three wrappers. A focused edit keeps its draft;
  use explicit `Live="true"` to retain per-qualified-candidate publication. Intermediate IME text does
  not publish. A native change arriving without a held edit preserves the old immediate-return condition.
  Returning to the initial text after a Live publication is another candidate: `10 → 20 → 10` requests
  both `20` and `10`, rather than mistaking the last input for an untouched edit.
* Single-line Return confirms; multiline Return remains native newline and Ctrl+Return confirms.
  Unchanged text, or an already-published Live candidate without a new observed basis, does not resend.
* **E2 candidate:** confirmation sends one valid pending candidate and, after normal return, shows the actual model.
  Rejection by business code is not repaired by repeated Set calls.
* **Valid automatic blur remains candidate:** ordinary blur confirms a valid nonconflicting draft. Invalid or conflicting text remains visible
  without trapping focus. An invalid draft gets the danger underline; an observed model conflict gets
  the selected-tone underline. These treatments read shared facts rather than reparsing in paint.
* Unmodified text follows observed model changes. Modified text remains local, acquires `Conflict`,
  and pauses Live/blur publication. Explicit confirmation can replace that known changed basis.
* **Approved Cancel boundary:** Cancel shows the latest actual model without rolling back earlier Live requests. Hidden drafts
  survive; hide/disable releases the active-edit record. Retarget/remove/dispose do not invent a commit.

The old raw `TextField(rect,text)` and session/key `TextField`/`NumberField` signatures remain usable
without tree registration. The identity-bearing forms now adapt the same driver; they still report a
candidate for their caller to dispatch. Their historical discard-on-disabled policy remains explicit
in that compatibility adapter. New callback-based wrappers preserve a disabled draft while releasing
focus. No old entry silently acquires a binding registration prerequisite.

## One explicit numeric operation

With model `10`, local valid draft `20` and Step `1`, the combination's plus operation requests `21`
once and ends the draft; it never first requests `20`. This N1 example is user-approved, not draft-only increment. N2/N3 remain candidates: an invalid draft cannot be the step base. A real range selection
requests its selected value once and replaces only this combination's draft; capture without a new
selection does not replace it. Another instance sharing the same Bind keeps its own draft and observes
the resulting model change. A following no-draft step starts from the actual model, not a rejected request.

Parsing rejects nonfinite candidates. Formatting or an out-of-range model never creates a repair
write. Existing valid Min/Max/Step declarations retain their conditions.

## Native boundary and recovery

Verse `Widgets.TextField`/`TextArea` remain the character/selection/clipboard engines. Read-only filters
mutating key and native command events before the native control while retaining navigation, SelectAll
and Copy, and never adopts a returned changed draft. Platform eligibility, event consumption, font and
composition facts are in `UiNative.Editing`, not in the neutral state.

Host Accept/Cancel reserve a composition key for the native edit instead of also confirming/discarding
the whole draft or closing the window. This is fixture-tested protocol, not target-game IME acceptance.
The remote 1.6.4850 `Widgets.cs` lines 1764–1789 establish the native text entry seam; its `readOnly`
TextArea parameter changes style only, so mutation protection is not delegated to that style flag.

Established requests dispatch before subsequent native painting and wrapper labels. Callback Close
stops normal drawing. A setter may partially change the model before throwing: the request boundary
retains its original cause/target, never retries or rolls back. Lifecycle focus projections are updated
even when a request or subsequent native rendering throws.
After a failed takeover, the attempted new value is not recorded as publication of the old draft.
Automatic publication pauses; a later distinct explicit confirmation can still request that valid draft.
The same distinction applies to a failed Live or Confirm request: passive repaint/Live/blur cannot
retry or silently end it, while another explicit confirmation is a new request, even for the same text.

Automated evidence: `KernelSharedEditingTests` compares standard/custom/nonrendering trajectories,
native multiline/read-only/composition protocols, actual combination takeover, same-Bind isolation,
retargeting, partial setter side effects and a rendering failure after established confirmation.
`Core.ContractTests` runs E1–E5/N1–N3 on the same neutral sources. Target RimWorld 1.6 input, caret,
selection, clipboard, IME and final appearance acceptance remain pending.
