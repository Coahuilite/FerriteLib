# Complete first-version migration status

## 2026-10-21 reactive foundation boundary

The first foundation package connects the same neutral production runtime and actual UiBindings/Host
presentation path. Counter/shared-boolean and independent safe business projection E2E are covered;
this is not full reactive migration of this inventory. RU2 local layout caching, collection surfaces,
remaining custom/native entry migrations and native acceptance stay open. New scalar binding inputs use
the existing full arrangement, not a new cache. Ordinary untracked getters retain their original contract.
See [reactive foundation](reactive-foundation-contract.md) for exact operations, owners and evidence.

This is the maintained inventory for the complete 0.8 first version, not a list of only the latest
slice. **Exists** means the retained production use is present. **Migrated** names the applicable 0.8
capability already connected. **Automated** distinguishes new shared-contract tests from legacy
regression coverage. **Native** means acceptance of these 0.8 inputs in RimWorld 1.6; all rows remain
pending. Earlier 0.7 human acceptance stays attached to its own package and does not close these rows.

## All 18 registered built-ins remain

| Registered use | Exists | Migrated to 0.8 | Automated evidence | Native | New reactive connection | New automatic evidence |
| --- | --- | --- | --- | --- | --- | --- |
| `input/button` | Yes | Shared activation/capture/request; F1 release | SharedBehavior + pure contract; existing widget lanes | Pending | Command implicit batch connected | Host E2E command path; wrapper regression |
| `input/slider` | Yes | Shared range, independent font/density, width-aware label layout; F1 immediate samples | SharedBehavior P/H/I/E + pure contract | Pending | Tracked getter seam available; float source/migration not delivered | No targeted tracked range E2E |
| `input/checkbox` | Both switch/checkbox looks | Shared boolean intent/capture/request and pressed facts; F2 release; explicit Set→Invoke retained | BooleanSelectionExpansion routes, render eligibility/held facts, failures; ControlKind look/eligibility guards | Pending | Tracked bool + shared Set→Invoke connected, both looks share path | Actual Checkbox Host E2E; both-look regression |
| `input/mode-row` | String/typed options | Shared discrete selection, stable option targets, model-derived Missing; F2 | BooleanSelectionExpansion; WidgetBehavior/CoreWidget typed/help guards | Pending | Scalar value opt-in available; options/collection not connected | No targeted tracked selection E2E |
| `input/dropdown` | String/pairs/typed options and both looks | Shared selection/trigger expansion; per-instance popup ownership even with same Bind; F2/F3; retained popup geometry/scroll owner | BooleanSelectionExpansion same-Bind owners/three routes/raw release/outside/close; Popup/InputEligibility/CoreWidget | Pending | Scalar value opt-in available; options/collection not connected | No targeted tracked popup E2E |
| `container/tree` | Flattened keyed rows, disclosure/check/body/image | Shared expansion/boolean, owner-derived pressed facts and independent row operation identities; F2 | BooleanSelectionExpansion render eligibility/held facts/reorder/parts/removal/I2; ControlKind row/geometry guards | Pending | Row/collection projection not connected | Existing behavior only |
| `input/number-field` | Yes | Shared editor/finite numeric codec/read-only-before-edit; B1 default/invalid-conflict retention/Cancel approved; E2/valid blur still candidate | SharedEditing three routes/failure/I2/isolation; pure editor and migrated Atom/EditTransaction guards | Pending | Generic tracked getter seam; numeric source/editor migration not delivered | No targeted tracked editor E2E |
| `input/text-field` | Single-line and basic plain-text multiline | Shared editor, native selection/copy/composition protocol and width-aware multiline measure; B1 default/invalid-conflict retention/Cancel approved; E2/valid blur still candidate | SharedEditing three routes/multiline/native composition/read-only; TextField/EditTransaction guards | Pending | String opt-in seam available; editor/IME migration not completed | Existing editor/IME regression, not new reactive E2E |
| `input/stepper-slider` | Numeric editor + range + ± | Shared editing/range/activation; explicit local takeover, N1 immediate21/end example approved; N2/N3 still candidate | SharedEditing actual composite and pure E/N cases; WidgetBehavior guards | Pending | Numeric/composite migration not completed | Existing takeover regression only |
| `chart/line` | Read-only and editable points retained | Shared point capture/mapping/request; optional stable PointKeysBind with current Index/Key, conservative legacy ambiguity end | SharedChart three routes/keyed reorder/echo/Used/failure/Close + pure point; InputEligibility/ControlKind | Pending | Point collection/edit projection not connected | Existing keyed chart regression only |
| `text/wrapped` | Yes | Shared read-only Text description and actual width/font facts | DescriptiveFacts read-only/font/geometry; CoreWidget/TextAudit/Style guards | Pending | Tracked string read seam connected | Counter text→neutral geometry; generic actual Host probe |
| `section/header` | Yes | Heading description and width-aware effective-font title | DescriptiveFacts effective-font/width; SectionHeader/Layout guards | Pending | Tracked string seam available; not individually migrated | Existing description/geometry regression only |
| `chrome/banner` | Yes | Shared descriptive read seam and effective-font measurement | DescriptiveFacts; CoreWidget/Atom guards | Pending | Tracked string seam available; not individually migrated | Existing description regression only |
| `state/empty` | Yes | Shared descriptive read seam and effective-font measurement | DescriptiveFacts; ControlKind/Atom guards | Pending | Tracked string seam available; not individually migrated | Existing description regression only |
| `display/image` | Natural size/letterbox/missing slot | Meaningful/decorative description and known/unknown texture availability | DescriptiveFacts meaningful/decorative/missing; ControlKind/image guards | Pending | Resource/reference projection not connected | Existing image regression only |
| `chrome/rule` | Decoration | Decoration description and retained shared paint geometry; no input state | DescriptiveFacts nonoperative paint; Atom/ControlKind guards | Pending | No business binding; retained decoration | Existing paint regression only |
| `display/progress` | Model value/range display | Actual value/range versus clamped fill and explicit nonfinite/missing facts; no write capability | DescriptiveFacts current/nonfinite/missing model; ControlKind guards | Pending | Tracked read seam available; numeric source not delivered | Existing range display regression only |
| `Repeat` | Registered template schema and engine keyed assembly | Actual immutable assembly/update contract connected; exact scoped binding roles, same-item anchors and stable item identity retained | Assembly keyed real-wrapper reorder + Repeat/Identity/DocumentReload guards | Pending | Item/collection observation not connected | Existing stable assembly regression only |

The seven descriptive uses are wrappers too. They need their appropriate descriptive, measure and
appearance contracts, not artificial activation state machines. A passing legacy lane is not a claim
that a row's new capability has been migrated.

## Nine layout containers and non-kind entry points

| Retained use | Exists / owner | 0.8 migration and evidence | Native | New reactive connection / automatic evidence |
| --- | --- | --- | --- | --- |
| Stack | Engine measure/arrange | Shared production neutral vertical flow; Layout + pure Layout; bounded runtime observation connected | Pending | Host synchronized scalar inputs enter existing full geometry; no RU2 local cache; retained layout regression |
| Row | Engine measure/arrange | Shared production neutral row allocation/cross sizing + optional shared label column; Layout/Placement + pure Layout | Pending | Host synchronized scalar inputs enter existing full geometry; no RU2 local cache; retained layout regression |
| Column | Engine measure/arrange | Shared production neutral vertical flow + optional shared label column; Layout + pure Layout | Pending | Host synchronized scalar inputs enter existing full geometry; no RU2 local cache; retained layout regression |
| Wrap | Engine responsive wrapping | Shared production neutral width/column wrapping; Layout + pure Layout | Pending | Host synchronized scalar inputs enter existing full geometry; no RU2 local cache; retained layout regression |
| Overlay | Engine stacking | Shared production neutral Overlay + finite same-parent anchors; Layout/Placement/atomic-failure + pure Layout | Pending | Host synchronized scalar inputs enter existing full geometry; no RU2 local cache; retained layout regression |
| Section | Engine grouping/chrome | Shared neutral vertical geometry with retained title/chrome; Layout/SectionHeader + pure Layout | Pending | Host synchronized scalar inputs enter existing full geometry; no RU2 local cache; retained layout regression |
| Surface | Engine grouping/chrome | Shared neutral vertical geometry; per-property environment sources/removal connected; Layout/StyleScope + pure Layout | Pending | Host synchronized scalar inputs enter existing full geometry; no RU2 local cache; retained layout regression |
| Scroll | Engine viewport + session scroll position | Shared neutral content/viewport rows/clamping + real native adapter; actual nested-scroll/offset-event + existing viewport/popup fixtures | Pending | Host synchronized scalar inputs enter existing full geometry; no RU2 local cache; retained layout regression |
| Clip | Engine effective clip/native groups | Shared neutral effective child geometry + existing balanced native clip; actual nested native scope/callback-close + existing clip fixtures | Pending | Host synchronized scalar inputs enter existing full geometry; no RU2 local cache; retained layout regression |
| Conditional/dynamic content | Engine Visible/Hidden/VisibleKey and structure reconciliation | Immutable replacement + visibility/structure reconciliation retained; actual authoring/update and capture cleanup tested | Pending | Tracked bool query seam available; dynamic/hidden observation policy not migrated |
| Limited same-parent positioning / shared label columns | Confirmed first-version additions | Implemented finite axis dependency + five-wrapper actual label demand/stacking; pure + actual Layout C3 cases | Pending | Retained shared geometry; no reactive environment/local-cache migration |
| Standard window / embedded host | UiWindowHost / UiHost, session lifecycle | Common owner-qualified DrawFrame adapter; explicit-context owner cleanup; ordinary concrete PageWindow and embedded edit/modal/Close fixtures; existing stack/dual-host/callback failure paths | Pending | Host-owned presentation sync/Close connected; actual counter and safe service Host E2E |
| Window chrome / return / business close | UiWindowChrome, shell and explicit commands | Existing shell/options/return commands retained on shared frame/capture/edit lifecycle; WindowHost/WindowCatalog/CancelLadder fixtures; no new keyboard/global Cancel defaults | Pending | Command entry batching connected; existing close/Cancel regression, no new native evidence |
| Tooltip / help / option-help output | Session hover and OptionHelp bindings | Retained real hover owner/help and option-help outputs; popup close clears help; bounded R9 last-frame facts + typed/help/Popup fixtures | Pending | Retained hover/help path; automatic observation migration not delivered |
| Bindings / change notifications / subscriptions | Model + UiBindings/UiNotifyAdapter | Retained; model remains authority, existing lifecycle tests; shared editor observes actual model independently of announcements; Assembly update preserves compatible drafts | Pending | Int/string/bool, tracked reads, four operation batches and independent owners connected; neutral + Host E2E |
| XML/program assembly / document update | UiHost/UiDocumentService/engine | Shared XML/program Create+Host UpdateDefinition validation/stage/seal, exact current theme declaration rollback, keyed Repeat; Assembly/DocumentReload evidence | Pending | Same Host consumes tracked keys; collection/definition-reactivity not introduced |
| Diagnostics / layout snapshots / agent observation | Existing diagnostics and development geometry probe | Bounded detached host/node/model/editor/source/last-geometry read surface; weak discovery lifecycle; Observation + pure DTO/budget contracts; unsupported/unsampled/stale/failure/truncation explicit | Pending | Existing detached observations retained; no new agent semantic tree or hidden policy |
| Visual core / text metrics / styles | Theme/draw/metrics and platform font service | Neutral30-role palette and same-source parser replace old26 bag/API/XML; all default and retained visual paint reads the palette adapter; font/density/source/removal retained, no new geometry rights; native pending | Pending | Existing property/metrics contracts retained; reactive palette/environment observation not connected |

## Retained/confirmed technical candidate connected; A8/native acceptance separate

All retained registered uses, nine containers, assembly/update, standard/embedded hosts and bounded R9
have applicable production contracts and automated coverage. B1 defaultfalse/invalid-conflict retention/
Cancel-draft-only and the N1 immediate21/end example are user-approved. E2/valid blur, remaining N2/N3,
P1 colour/default-authoring direction is implemented; typography/density opening-set details and R9 public surface remain candidates; L1 no-implicit-change principle is agreed, not every size algorithm; F1/F2/F3 remain approved. Target 1.6 native
input/IME, font pixels, window stack, clipping/scroll/popup geometry and image correlation remain pending.
K1 is now approved for grouped deferral; H1 continues as approved. A3 structural focus traversal, B2 Enter/Space and B3 Slider keyboard increment/Home/End remain concrete
unimplemented recommendations. The approved first-version deferral keeps native editor keys and existing
Cancel/Accept hooks; these items are not counted as implemented. See the
A8 section of `host-observation-contract.md` for the exact difference from the earlier recommendation.
A green harness is automated evidence for the selected source, not final product or native acceptance.
Implementation contracts: [S0/S1](s01-contract.md), [boolean/selection/expansion](bse-contract.md), [editing/numeric composition](editing-contract.md), [descriptive wrappers and chart](wrappers-contract.md), [shared layout](layout-contract.md), [property sources](property-contract.md), [assembly/update](assembly-contract.md), [hosts/observation](host-observation-contract.md).
The product requirements and remaining policy decisions live in [the specification](README.md),
with the [contract-to-test migration](acceptance-and-test-migration.md) defining evidence classes.

Palette authoring and exact intentional theme migration: [theme-migration.md](theme-migration.md). All18 default uses above retain their full-purpose checks; style-table outlets, surface/text helpers, native range/checkbox/row/image/chart paints read the same neutral palette through UiTheme. No new interaction owner or palette-specific business tree is introduced.

Content overlap now uses current shared input geometry and neutral layer decisions. Default controls participate without extra author configuration; precise custom overlap uses the optional capability with the explicit old-custom boundary in [layers-images-contract.md](layers-images-contract.md).

Image additionally supports finite Fit/inner alignment, opt-in width-aspect measurement and supplier-owned Texture/RenderTexture values. Standard UiPageWindow adds program/declarative/instance background configuration; unconfigured defaults remain. Layer/image/background regressions are in KernelLayerDispatchTests and KernelImagePlacementTests; actual same-source neutral checks include PointerLayers and ImageGeometry. Native acceptance remains pending. See [the single contract](layers-images-contract.md) for defaults, ownership, exact custom seam and migration conditions.
