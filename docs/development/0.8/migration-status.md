# Complete first-version migration status

This is the maintained inventory for the complete 0.8 first version, not a list of only the latest
slice. **Exists** means the retained production use is present. **Migrated** names the applicable 0.8
capability already connected. **Automated** distinguishes new shared-contract tests from legacy
regression coverage. **Native** means acceptance of these 0.8 inputs in RimWorld 1.6; all rows remain
pending. Earlier 0.7 human acceptance stays attached to its own package and does not close these rows.

## All 18 registered built-ins remain

| Registered use | Exists | Migrated to 0.8 | Automated evidence | Native |
| --- | --- | --- | --- | --- |
| `input/button` | Yes | Shared activation/capture/request; F1 release | SharedBehavior + pure contract; existing widget lanes | Pending |
| `input/slider` | Yes | Shared range, independent font/density, width-aware label layout; F1 immediate samples | SharedBehavior P/H/I/E + pure contract | Pending |
| `input/checkbox` | Both switch/checkbox looks | Shared boolean intent/capture/request and pressed facts; F2 release; explicit Set→Invoke retained | BooleanSelectionExpansion routes, render eligibility/held facts, failures; ControlKind look/eligibility guards | Pending |
| `input/mode-row` | String/typed options | Shared discrete selection, stable option targets, model-derived Missing; F2 | BooleanSelectionExpansion; WidgetBehavior/CoreWidget typed/help guards | Pending |
| `input/dropdown` | String/pairs/typed options and both looks | Shared selection/trigger expansion; per-instance popup ownership even with same Bind; F2/F3; retained popup geometry/scroll owner | BooleanSelectionExpansion same-Bind owners/three routes/raw release/outside/close; Popup/InputEligibility/CoreWidget | Pending |
| `container/tree` | Flattened keyed rows, disclosure/check/body/image | Shared expansion/boolean, owner-derived pressed facts and independent row operation identities; F2 | BooleanSelectionExpansion render eligibility/held facts/reorder/parts/removal/I2; ControlKind row/geometry guards | Pending |
| `input/number-field` | Yes | Shared editor/finite numeric codec/read-only-before-edit; E1–E5 candidate | SharedEditing three routes/failure/I2/isolation; pure editor and migrated Atom/EditTransaction guards | Pending |
| `input/text-field` | Single-line and basic plain-text multiline | Shared editor, native selection/copy/composition protocol and width-aware multiline measure; E1–E5 candidate | SharedEditing three routes/multiline/native composition/read-only; TextField/EditTransaction guards | Pending |
| `input/stepper-slider` | Numeric editor + range + ± | Shared editing/range/activation; explicit local takeover, N1–N3 candidate | SharedEditing actual composite and pure E/N cases; WidgetBehavior guards | Pending |
| `chart/line` | Read-only and editable points retained | Shared point capture/mapping/request; optional stable PointKeysBind with current Index/Key, conservative legacy ambiguity end | SharedChart three routes/keyed reorder/echo/Used/failure/Close + pure point; InputEligibility/ControlKind | Pending |
| `text/wrapped` | Yes | Shared read-only Text description and actual width/font facts | DescriptiveFacts read-only/font/geometry; CoreWidget/TextAudit/Style guards | Pending |
| `section/header` | Yes | Heading description and width-aware effective-font title | DescriptiveFacts effective-font/width; SectionHeader/Layout guards | Pending |
| `chrome/banner` | Yes | Shared descriptive read seam and effective-font measurement | DescriptiveFacts; CoreWidget/Atom guards | Pending |
| `state/empty` | Yes | Shared descriptive read seam and effective-font measurement | DescriptiveFacts; ControlKind/Atom guards | Pending |
| `display/image` | Natural size/letterbox/missing slot | Meaningful/decorative description and known/unknown texture availability | DescriptiveFacts meaningful/decorative/missing; ControlKind/image guards | Pending |
| `chrome/rule` | Decoration | Decoration description and retained shared paint geometry; no input state | DescriptiveFacts nonoperative paint; Atom/ControlKind guards | Pending |
| `display/progress` | Model value/range display | Actual value/range versus clamped fill and explicit nonfinite/missing facts; no write capability | DescriptiveFacts current/nonfinite/missing model; ControlKind guards | Pending |
| `Repeat` | Registered template schema and engine keyed assembly | Actual immutable assembly/update contract connected; exact scoped binding roles, same-item anchors and stable item identity retained | Assembly keyed real-wrapper reorder + Repeat/Identity/DocumentReload guards | Pending |

The seven descriptive uses are wrappers too. They need their appropriate descriptive, measure and
appearance contracts, not artificial activation state machines. A passing legacy lane is not a claim
that a row's new capability has been migrated.

## Nine layout containers and non-kind entry points

| Retained use | Exists / owner | 0.8 migration and evidence | Native |
| --- | --- | --- | --- |
| Stack | Engine measure/arrange | Shared production neutral vertical flow; Layout + pure Layout; bounded runtime observation connected | Pending |
| Row | Engine measure/arrange | Shared production neutral row allocation/cross sizing + optional shared label column; Layout/Placement + pure Layout | Pending |
| Column | Engine measure/arrange | Shared production neutral vertical flow + optional shared label column; Layout + pure Layout | Pending |
| Wrap | Engine responsive wrapping | Shared production neutral width/column wrapping; Layout + pure Layout | Pending |
| Overlay | Engine stacking | Shared production neutral Overlay + finite same-parent anchors; Layout/Placement/atomic-failure + pure Layout | Pending |
| Section | Engine grouping/chrome | Shared neutral vertical geometry with retained title/chrome; Layout/SectionHeader + pure Layout | Pending |
| Surface | Engine grouping/chrome | Shared neutral vertical geometry; per-property environment sources/removal connected; Layout/StyleScope + pure Layout | Pending |
| Scroll | Engine viewport + session scroll position | Shared neutral content/viewport rows/clamping + real native adapter; actual nested-scroll/offset-event + existing viewport/popup fixtures | Pending |
| Clip | Engine effective clip/native groups | Shared neutral effective child geometry + existing balanced native clip; actual nested native scope/callback-close + existing clip fixtures | Pending |
| Conditional/dynamic content | Engine Visible/Hidden/VisibleKey and structure reconciliation | Immutable replacement + visibility/structure reconciliation retained; actual authoring/update and capture cleanup tested | Pending |
| Limited same-parent positioning / shared label columns | Confirmed first-version additions | Implemented finite axis dependency + five-wrapper actual label demand/stacking; pure + actual Layout C3 cases | Pending |
| Standard window / embedded host | UiWindowHost / UiHost, session lifecycle | Common owner-qualified DrawFrame adapter; explicit-context owner cleanup; ordinary concrete PageWindow and embedded edit/modal/Close fixtures; existing stack/dual-host/callback failure paths | Pending |
| Window chrome / return / business close | UiWindowChrome, shell and explicit commands | Existing shell/options/return commands retained on shared frame/capture/edit lifecycle; WindowHost/WindowCatalog/CancelLadder fixtures; no new keyboard/global Cancel defaults | Pending |
| Tooltip / help / option-help output | Session hover and OptionHelp bindings | Retained real hover owner/help and option-help outputs; popup close clears help; bounded R9 last-frame facts + typed/help/Popup fixtures | Pending |
| Bindings / change notifications / subscriptions | Model + UiBindings/UiNotifyAdapter | Retained; model remains authority, existing lifecycle tests; shared editor observes actual model independently of announcements; Assembly update preserves compatible drafts | Pending |
| XML/program assembly / document update | UiHost/UiDocumentService/engine | Shared XML/program Create+Host UpdateDefinition validation/stage/seal, exact current theme declaration rollback, keyed Repeat; Assembly/DocumentReload evidence | Pending |
| Diagnostics / layout snapshots / agent observation | Existing diagnostics and development geometry probe | Bounded detached host/node/model/editor/source/last-geometry read surface; weak discovery lifecycle; Observation + pure DTO/budget contracts; unsupported/unsampled/stale/failure/truncation explicit | Pending |
| Visual core / text metrics / styles | Theme/draw/metrics and platform font service | Per-property font/density metrics/all26 semantic colour sources and sparse styles/removal connected; actual wrapper paint respects effective font; native pending | Pending |

## Retained/confirmed technical candidate connected; A8/native acceptance separate

All retained registered uses, nine containers, assembly/update, standard/embedded hosts and bounded R9
have applicable production contracts and automated coverage. E1–E5/N1–N3, L1, P1 and R9 surface/budgets
remain reversible Dev candidates for concentrated closeout; F1/F2/F3 remain approved. Target 1.6 native
input/IME, font pixels, window stack, clipping/scroll/popup geometry and image correlation remain pending.
A3 structural focus traversal, B2 Enter/Space and B3 Slider keyboard increment/Home/End remain concrete
unimplemented recommendations. The proposed first-version deferral keeps native editor keys and existing
Cancel/Accept hooks, and needs product closeout; these items are not counted as implemented. See the
A8 section of `host-observation-contract.md` for the exact difference from the earlier recommendation.
A green harness is automated evidence for the selected source, not final product or native acceptance.
Implementation contracts: [S0/S1](s01-contract.md), [boolean/selection/expansion](bse-contract.md), [editing/numeric composition](editing-contract.md), [descriptive wrappers and chart](wrappers-contract.md), [shared layout](layout-contract.md), [property sources](property-contract.md), [assembly/update](assembly-contract.md), [hosts/observation](host-observation-contract.md).
The product requirements and remaining policy decisions live in [the specification](README.md),
with the [contract-to-test migration](acceptance-and-test-migration.md) defining evidence classes.
