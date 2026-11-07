# Measurement, hover and domain diagnostics

## Auto controls reserve what they paint

`input/button` contributes its two caption insets through the existing NaturalBody seam. The inset
comes from the same theme Padding used by caption measurement and painting, including Chrome="none".
Explicit widths, MinWidth/MaxWidth and the parent's remaining budget still constrain Auto.
Literal Auto labels keep their original whitespace, as painting does; translation-key handling stays
unchanged. A text-field placeholder uses single-line presentation unless Multiline is true. Read-only,
empty and focused placeholder eligibility is unchanged.

## Content help respects the effective clip

The context-aware `UiControls.IsMouseOver(rect, ctx)` checks the current composed effective clip in
window coordinates before content hover. Disabled controls remain eligible to explain themselves.
Window-space popup row hover, press qualification, capture and the native editor keep their own owners.
This is a library clip correction, not the consumer's follow/pin/reading-hold policy.

The existing layout, core-widget, text-field, element/option-help, input-eligibility, editing,
invalidation, containment and API-tier checks are reused. No new tests or cases are added. Managed
checks use the 1.6 stubs; actual glyph fit and nested scrolling/help interaction remain native work.
A001's full Dev instrument entry and Release KernelAtomTests recorded two old expectations at
KernelAtomTests.cs:221/224: button16 and remainder304 omitted the required caption inset. Under the
explicit WO r3 authorization, A003 corrects only those two expectations and their descriptions to28/292:
16 + 2*6 for the button, then400 - (32 + 28 + 32 + 16) for the remainder. The scenario, assertion count,
Near precision and all other assertions are unchanged. The original Atom suite and Dev instrument gate
now pass; A001's failed logs remain historical evidence. Runtime code and the r3 DLL were not rebuilt.

## Create the output owner before the Host

For native domain logging, replace only the `new UiHost(...)` expression in an existing factory:

```csharp
UiHost host = VerseUiDiagnosticLog.CreateHost("your.package.id", source, manifest,
    bindings, theme, metrics, translation, document,
    new UiDiagnosticLogOptions { DetailEnabled = detailed });
VerseUiDiagnosticLog log = VerseUiDiagnosticLog.Attach(host, "your.package.id");
```

CreateHost constructs the real Host, with the supplied metrics unchanged. Consumer and output ownership
exist before constructor work, including second-style-source and dropped-document reports. A creation
failure is reported then rethrown; there is no second Host, replay or automatic retry. `UiPageWindow`
uses this factory with its existing key consumer; missing/whitespace consumer identity is logged as
unknown without changing the original window key or adding validation to the old standalone shell.
Custom `UiWindowHost.CreateHost` overrides keep their
signature and opt in through the same factory. Existing factories and the original UiHost constructor
remain legal without enrolling in logging or diagnostic subscriptions.

Attach returns the live binding for the same consumer, or installs a future-only binding on an existing
Host. A conflicting live consumer is refused. Attach does not replay constructor facts. Configure
copies DetailEnabled; omitted options on Attach preserve an existing policy. Closing the Host or directly
disposing its Session releases the binding; consumer Dispose is idempotent. An explicitly disposed live
binding no longer owns native output, so the old warning fallback remains available. Re-enrollment is
explicit, never a side effect of Configure. The binding holds detached identity and output counters, not
Host, Session, model, node or execution references.

Update detail live, without rebuilding the Host:

```csharp
log.Configure(new UiDiagnosticLogOptions { DetailEnabled = detailed });
host.Session.Controls.Trace = detailed ? log.Trace : (Action<string>?)null;
```

Keep Trace null when off: the callback's own gate cannot prevent the caller preparing a string. Timing,
geometry, Observe/dump and fit retain their independent opt-ins. Attach/Configure never enables them,
subscribes a fact ring or clears one. No-Host authors may create `new VerseUiDiagnosticLog(consumer)`
and wire OnOverflow/OnStyleFallback into their existing UiFitAudit callbacks, retaining their own Dispose.
Legacy author callbacks, metrics, Warning observers and legal standalone use remain supported. An adopted
Host routes its native output once, instead of also invoking the legacy global fit printer; original
subscription facts remain available. Remove consumer reprinting of those same FL facts. An explicitly
retained custom Warning printer is still called, so it must be migrated deliberately rather than doubled.

## Daily facts and detail have different costs

Native output is `[FerriteLib.UIKIT] consumer=... level=... event=... host=... session=...`.
Normal/warning/error call the corresponding Verse Message/Warning/Error methods. Recovery, pure
presentation/qualification/layout faults and appearance fallbacks are warnings; thrown requests,
window/creation and cleanup failures are errors. Reload refusal
retains last-good and is a warning, not an error; accepted/skipped reload, timing and Trace are detail-normal.
Legal control or business refusal does not itself generate an error. Unenrolled legacy warning callbacks
keep their previous text and conditions; the domain format belongs to the adopted adapter path.

Trip, LastRequestFailure, parser issues, document reports and subscribed fit/timing records remain with
their original owners and are recorded before optional native publication. This adapter creates no fact
ring. Default output includes bounded token identities, cause category, player impact and safe next action,
not business values, authored strings, exception messages/stacks or document paths. Detail adds only the
available producer-time pass/phase/raw-phase/consumed and committed/current revision values, actual fit
font/axis/need/available/wrap width and optional bounded Trace text. Construction/standalone context is
NotSampled, not a guessed Repaint. Host passes are not FPS. Detail formatting and moment capture happen
after its gate; expensive samplers retain their existing producer gates. Native output failure is isolated,
counts in OutputFailures and disables that binding's sink without recursively reporting or retrying work.

Layout-state is Stale for known dirty/revision mismatch, otherwise NotSampled: matching revision numbers
alone do not sample pending notifications, translation or theme validity. The logger does not call Observe
or perform a tree query to manufacture a Fresh answer.

The existing dynamic diagnostic route carries the current Session only inside its push/pop scope, restores
the previous route in finally, and retains no Session after the outermost scope. No new lasting static
logger index or native owner exists. Logger-only Hosts keep legacy fit saturation; subscribed Hosts keep
their existing per-host ruler/ring behavior. Logging does not expand a legacy sampling budget.

## Output budgets do not change fact retention

Each binding (consumer plus one Host/Session, or the explicit standalone lifetime) has a monotonic60-second
window,24 warning emissions,24 reserved error emissions and12 detail-normal emissions. These are an
operational flood ceiling, not a measured fault-frequency estimate: errors have the same independent
allowance as warnings, while optional detail gets half. At most64 emitted keys are retained; the normal
budgets permit at most60. A key is bounded event + node + cause category (fit includes axis/effective font),
never text, colour, pass, timestamp or exception message. Repeated keys consume counters, not emission slots.

The first unprinted new key at a level's budget limit emits one budget notice naming that key immediately.
Later denied keys update that level's bounded latest-unprinted sample and counter. On the next real event
after expiry, or Close/Dispose, one summary reports repeated/limited counts and latest unprinted identities
at the highest suppressed severity. Expiry then resets all level budgets, so a new independent error is
eligible in the next window instead of joining a permanent first-N blacklist. There is no timer, polling,
replay or unbounded denied-key set. A short-lived burst may exceed output coverage; original facts retain
their own retention/drop rules. Configure does not renew quotas, and Close does not clear consumer memory.

Host Close now attempts presentation, document, subscription, Session and layout-cache cleanup even when
an earlier ordinary release fails, then propagates the original single failure or aggregate. Session closes
its output after its owned cleanup has produced its result. No borrowed execution is disposed by Host.
