# 0.8 window terminal-state maintenance candidate

The local `native-a001-r3` Release carrier keeps API 0.8.0 and the 1.6/net472 adapter.
It was built from a dirty Source Form, not the published rc2 artifact. Use its matching Source Form and
the consumer's recorded fixed input; do not infer its bytes from the rc2 tag or the API version.

The current source and engineering records are committed as a local closeout checkpoint. The fixed
carrier retains its original dirty-build identity; the new HEAD does not identify the source state
that produced its bytes. This checkpoint is not a release or performance acceptance.

## Current engineering checkpoint and next owner

PM accepted `native-a001-r3` engineering and the identified partial RW1.6 native evidence. Performance
is unfinished: the consumer's original F08 10-FPS symptom was not reproduced, F09 rapid-operation P95
did not improve, and physical input/IME, user-save/colony operation and restart acceptance remain open.
The old consumer Packs fixture still fails before its click; no assertion was changed.

Exact Release identity: `0.8.0+native-a001-r3.d893163.dirty`, MVID
`ece40178-19f6-4a89-9849-24326502b8be`; source base `d893163b2b029a5d370928e10b3506d2364523fa`
plus the frozen Source Form's dirty inputs. The fixed producer is at
`dist/usfl08-ui-attack-20261110/a001-native-r3/FerriteLib`.
Universal Squeaker's same-named delivery owns the complete pair, `IDENTITY.json`, `EVIDENCE-zh.md`,
Source Forms and whole-pair rollback. Its task archive
`dist/usfl08-ui-attack-20261110/handoff-evidence-local/INDEX.md` preserves raw frames/events, diagnostic
sources, original replay/build recipes and check logs locally; it contains no game or dependencies.
Do not require that private consumer archive to build a public FL checkout.

This session is closed to implementation, builds, tests and native operation. The next session's
proposed focus is FL.UiKit performance and its affected existing checks, under a new assignment.
0.9 is paused for this session, not automatically started or permanently prohibited next session.
The user has approved read-only copying of formal ModsConfig.xml and US/FL configuration files into
the isolated directory. It has not been executed; the next session must carry this exact permission
into its execution scope. No saves, arbitrary other configuration or formal-file writes are included.
This session remains closed: no formal reads/copies or game operation occur here.

### First discriminating experiment, after authorization

Start from the existing four-toggle timing and same-sequence raw data, not a new blanket suite.
The remaining measured hot path is `TryIncremental` visible-entry validation (455.418 ms self),
`PipelineContext` (263.453 ms) and `UiTextFonts.Environment` (161.252 ms) in the final four-toggle slice;
`ReusePlan` self is only 6.927 ms. These are instrumented slice totals, not per-frame timings.

The first new measurement should split **collapsed idle versus expanded idle on the same native Host**
using the existing driver's `timing-start`/`timing-stop` around the existing F09 replay's settled states.
Keep the candidate, profile, logging/fit policy and environment fixed; leave transition frames outside
those two idle slices. Compare calls per Arrange pass and self time per call for those three methods,
then relate them to the already-recorded transition/full-layout cost. This distinguishes recurrent
visible-tree validation from one-time row materialization before choosing the next edit. Reuse the
current driver and existing checks; do not create another harness, fixture or generic cache platform.
This experiment is proposed, not performed by the documentation closeout.

Any optimization must retain ordinary-getter backing changes **without Notify** at their original
read/synchronization points; it must not poll them at new points either. Shared-label maximum changes,
unknown rulers at their existing legal revalidation boundary, failed/half candidates, direct standalone
reopening and next-pass freshness remain counterexamples to a dirty-only or cross-pass shortcut.
A fallback returned by a guarded measurement is not a successful reusable result. Live draft/IME/focus,
0.35-second consumer save debounce, field identities and displayed content remain unchanged.
The [build guide](../../build-and-debug.md#check-the-affected-contract) names the existing commands;
full suite success is neither required by this handoff nor sufficient for native performance acceptance.

## Close ends the current shell lifetime

`PreClose` marks the shell closed before releasing its owned Host and native execution. The
existing `PreOpen` hook records that this instance entered a native window lifetime. Once native,
a closed shell ignores late drawing and Accept/Cancel dispatch until its next `PreOpen`.
Independent/off-stack instances retain both their initial draw and the old `PreClose` followed by
a new top-level `WindowOnGUI` reopening entry, without a new caller prerequisite or `IsOpen` gate.

The sealed `DoWindowContents` boundary counts active contents calls; `PreClose` also guards its
teardown callbacks. A closed direct instance can start a new lifetime only after both have unwound.
A close (or a reentrant native reopen) changes the lifetime identity, preventing nested drawing
from reviving the active pass. Native key dispatch before contents is covered by the native-lifetime
marker. A veto does not run `PreClose` and leaves the same Host usable. Either legitimate reopen
resets failure state and creates a fresh Host/session.

The draw pass checks that lifetime after chrome, `BeforeDraw`, prerequisites, creation, attachment
and page drawing. A factory returning after a synchronous close hands its result only to teardown;
an attach handler that closes stops later attach handlers and the page pass. Background callbacks
cannot continue into normal chrome after ending their lifetime. `BeforeDraw` errors use the existing
deferred failure notice and teardown rather than escaping outside the shell's failure boundary.

Revision history: `a001` incorrectly required `PreOpen` even for independent direct reopening;
five assertions in `KernelPageLifecycleTests.VerifyCloseAndReopen` failed (published rc2 passed).
The PM rejected that restriction. `a001-r2` restores the original contract and all five assertions
pass unchanged. The original candidate and failure evidence remain available as history. The engineering mistake was
adding a native-lifetime prerequisite to an existing independent caller. Future lifecycle/cache edits
must write down both original entry conditions and failure returns before narrowing the hot path;
the existing reopening and failure checks are the regression evidence, not a reason to weaken that contract.

## Fit measurements reuse the existing bounded ruler

`UiHost.Draw` supplies its fit audit with a Host-owned `UiTextMetricsCache`, separate from the
transactional layout cache. Unchanged text/effective font/purpose/width reuses measurements only
while the supplied metric environment remains equal. A missing environment still measures every
time. The existing per-font/purpose 512-entry bound applies; the cache is dropped on Host close or
metric-source replacement. Findings are still evaluated and published, including repeated counts.

The drawing context retains the caller's original metrics object and capabilities. Standalone
audits, custom rulers, native editors, draft length, focus, IME, model publication and layout
invalidation conditions are unchanged. Distinct text still requires measurement. The existing
instrument therefore counts underlying metric calls, not the number of fit checks.

## Full-layout candidates copy only changed leaf records

Scope dictionaries remain transactional, but their immutable leaf records now share list storage
until that candidate actually modifies the list. Unvisited and unchanged scopes no longer deep-copy
all records on every full traversal. Incremental validation and failed-candidate rollback retain
separate list ownership. Full-traversal conditions, ordinary getters and unknown metric environments
remain conservative; this does not redefine a retained pass or a published geometry counter.

## Numeric drafts reuse only a pure codec result

The numeric codec retains one complete text/result pair under its immutable bounds. The numeric
widgets retain that codec across an unchanged declaration and replace it on reconfiguration. New
text still parses normally, including failed out values, finite checks and reversed bounds. The
standalone codec remains thread-safe. Arbitrary editor parsers still run fresh; draft length, native
editing, IME, Live publication, confirmation and readback semantics remain unchanged.

## A layout plan does not measure the same container twice

Native profiling identified repeated recursive container measurement in a single plan's natural,
reuse-validation and final-allocation steps. Each child-measure delegate now retains that child's
last successful container result for exactly equal width, effective available height and content
height reference. Non-height-consuming containers normalize available height as before. Leaves
keep their existing measurement path; changed constraints remeasure, and A/B/A constraints do not
keep a second historical result. The delegate and its retained results end with that plan invocation.
A guarded measurement failure marks the result unsuccessful even when it returns a fallback instead of throwing; any tripped node disables this local reuse. Shared-label columns are established before constructing the delegate and are recomputed by later plan invocations.
This is not a cross-pass subtree cache: full traversal, ordinary getters, content revisions and
unknown metric-environment validation keep their original conditions.

Repeated measurement of the same Repeat node registers its row set once in the current pass's
materialization list. An immutable element declaration retains its exact ordered attribute inputs
instead of sorting and serializing them again for every plan key. Replacing an existing plan-key
entry does not scan all scopes for an eviction; adding a key still enforces the original four-variant
bound. Failure rollback, node identity and displayed content are unchanged.

## Construct the final context without intermediate copies

The native-r3 tightening keeps the same style resolution, journal, node-owner validation and
per-entry metric/layout validation. An internal `WithLayout` constructs the final context once
instead of chaining temporary style/theme/width/origin/node contexts. Public context methods and
identity remain unchanged. `UiMeasureInputs.Join` copies its two already-owned, immutable input
sets directly into final arrays; the public constructor still copies and validates caller arrays.
Neither change retains mutable values, skips a callback or introduces a cache.

A native four-toggle timing slice retained 1,984 recursive core measurements and 740 plan lookups.
PipelineContext cost per call fell from 1.770 to 1.258 microseconds; metric-environment composition
fell from 4.268 to 2.855 microseconds. Total Arrange was 1503.138 ms over 990 passes before and
1333.564 ms over 1010 passes after. Method instrumentation has observer cost; clean FPS is reported
by the consumer separately. Existing layout, reuse, identity, lifecycle, failure/editing, style,
item-binding, readonly API and 391 neutral checks pass without new cases.

Incremental validation still traverses visible entries and checks styles, layout stamps, captured
measurement inputs and metric environments. Unchanged geometry is not proof of unchanged inputs:
a shared-label peer can change width, an ordinary getter can read a changed backing value without
notification, and an unversioned ruler can change measurement. The relevant fallback must still
measure and publish fresh geometry. Plan-key construction is no longer the dominant cost (6.927 ms
self time in the four-toggle slice); recursive inclusive method totals must not be summed.

## Evidence and limits

The earlier r3 Release build and existing page-lifecycle (including all five original reopening
assertions), layout-reuse, failure-lifetime, shared/closeout-editing, text-audit and API/containment
entries passed. A fixed external consumer navigation run reduced copied leaf records while retaining
its full-candidate and geometry counts. These are managed production paths with platform doubles,
not real font, operating-system editing or frame-rate measurements.

The native candidate was built in Release and passed the existing layout/layout-reuse, page-lifecycle,
failure-lifetime, shared/closeout-editing, style-scope, item-binding and readonly API/containment entries.
No cases or assertions were added or changed. Its paired production consumer was also rebuilt and
exercised through its existing host entries. The existing platform-neutral Core.ContractTests entry
also passed all 391 checks against the complete production Kernel source set.

An authorized isolated RimWorld 1.6.4871 rev591 / Unity 2022.3.35f1 / D3D11 run used the actual
Release assemblies, Chinese fonts and rendered settings window. In a four-toggle diagnostic slice,
recursive core measurements fell from 10,768 in r3 to 1,984 after the per-plan change. Profiling was
disabled for the subsequent frame comparisons. The older carrier recreated a Host immediately
after native chrome close, before the explicit reopen; the final candidate left the Host cleared
until reopening. Fresh sessions and bounded sampling completion/close cleanup were observed.

Physical keyboard/IME, a loaded colony and exact close-during-factory/background/BeforeDraw or
nested-direct-draw branches were not dynamically exercised. The isolated copy deliberately leaves
Steam uninitialized and uses its own application identity and preferences. This does not validate
Steam integration. The consumer owns its operation-specific frame and input-tail results; no general
60-FPS guarantee follows from the reduced work count.
