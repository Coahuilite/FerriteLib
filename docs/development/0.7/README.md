# 0.7.x — subtraction and stabilization: execution record

Status home for the line (plan: `ferritelib-0.7-development-plan-en.md`, maintainer approval 2026-09-17:
"conform, go on"). Contract text lives in `05-api-contract.md` (written before the behavior changes);
command evidence in `40-verification.md`. Not a second task ledger.

Baseline: branch `0.7.x` forked from `0.6.x` at `12d0dbb` + the maintainer's uncommitted ruling docs
(preserved into the first commit). Axes opened at 0.7.0/0.7.0/0.7.0 in `8fe3026`.

## Fixed scope and status

| Package | Content | Status |
| --- | --- | --- |
| A | Five reliability fixes: Row Auto fallback, Height creation validation, Wrap-only `Cols`/`NarrowCols`, dropdown value precedence, wrong-kind options diagnostics | 已实现 + 已自动化验证; every lane re-run against the reverted baseline and observed red (mutation proof in `40-verification.md`) |
| B | Ordinary-author recipe: compile-checked public-only harness fixture + `docs/consumers/ordinary-settings.md` | 已实现 + 已自动化验证 (eight lanes). Not consumption evidence. |
| C | Two peer palettes `UiTheme.Vanilla` / `UiTheme.DarkGold`; `new UiTheme()` is an unpainted bag | 已实现 + 已自动化验证; palette literals verified; **视觉验收 pending** (comparable state sheet needs a real game pass) |
| D | Gates + dev staging + contract/handoff docs | 已实现 + 已自动化验证 (9 gates, `-PackDev`); external acceptance below |

**Amended by Batch 1 (2026-09-18).** Until Batch 1 this line added and removed no XML vocabulary; it now
does both, plus a member removal, all inside `0.7.0` under the fast-development ruling. The API-tier *type*
list is still the 0.6 pin — Batch 1 adds no public type. What moved, and the migrations, are in
`05-api-contract.md` under "Batch 1": four placement attribute names added; container `Padding`/`Gap`
defaulting to the theme's geometry instead of 0; the authored tone vocabulary shrinking to four meanings with
`Active`/`Disabled` redirecting to states for one minor; and `UiTheme.HoverPoint` removed in favour of a
derived accent step. `UiTheme.Vanilla` is a public-unstable member addition, recorded in the same file.

**Amended again by B7 (2026-09-20).** The coordination ruling that followed Batch 1 allows **additions** inside
`0.7.0` while the local consumer is mid-development, and the first one landed: `input/text-field`, the
single-line string sibling of `input/number-field`, plus one public-unstable member —
`UiNative.TextField(Rect, string, UiSession, string, out bool)`. The API-tier *type* list is therefore **no
longer the 0.6 pin**: it gains one internalize-candidate entry (`TextFieldWidget`). `Api` stays `0.7.0`. The
entry, the "no migration — it is an addition" stance and the commit rule are in `05-api-contract.md` under
Amendment 3 and "### B7". The same day's second addition is `input/mode-row`'s **per-option hover help**: the
manifest attribute `HoverHelpKey` publishes which option is hovered (the option's `DescriptionN`, or its
`ValueN` when it declares none) so a consumer renders the help itself, plus `UiNative.IsMouseOver(Rect,
UiWidgetContext)` for the arbitration-aware hover test. No new type there either; the section is "Mode-row
hover help" in the same contract file.

**Amended a third time, by G1 + G4 (2026-09-20).** The approval of route (a) added the **element-level help
hook**: `HelpKey` is engine-wide on widget elements and the engine claims it on the session's existing hover
surface — no new member, no new channel — with a binding-resolved sibling recorded as **contingent** on the
hierarchy × composition decision rather than built. The same round generalized `HoverHelpKey` to the dropdown's
popup rows and gave `input/mode-row` localized option labels (`TitleKey1..8`, plus the indexed-key fix in the
`Width="Auto"` seam that the label set needed). `Api` stays `0.7.0`; the contract sections are "Element-level
help, and the option level generalized" and "Mode-row hover help".

## What ordinary authors stop doing (B, honestly measured)

Before/after on the same operation ("add a second numeric setting to a settings group"), against the
recipe's real pre-change path — the 0.6 surface is identical, because B adds no code:

- **Edit sites:** 1 XML row-builder call + 1 `BindValue` registration. Same before and after.
- **Manual geometry / Measure / Draw:** 0, both.
- **Binding/notification work:** one mutation method on the model that announces; both UI and non-UI
  writers reach it. Same capability before — the difference is that the announcement discipline
  (paint vs measure vs structure, who notifies, who cleans up) was scattered across round notes and the
  demo's vocabulary, not stated anywhere a stranger could follow.
- **Concepts needed to start:** now one guide + one compile-checked file. Before: four documents and a
  consumer's private example.

The gain is discoverability and a checked reference, not fewer runtime lines. Stated as the plan required.

## Deferred notes (not replacement packages)

From the plan's excluded list, only what is actually useful later: a general intrinsic-size measurement
seam (A1 keeps the text-only seam honest until a citation forces more); a comparable-state-sheet tool for
theme acceptance (the in-game checklist covers it manually); consumer-repo 0.7 migration of the wired page
tree (maintainer-owned, separate authorization).

## External acceptance — ownership and status

Each item below needs a real game or a real consumer compile; none is claimed here:

1. Compile one authorized representative consumer section against `[0.7.0, 0.8.0)`, behavior preserved —
   consumer operator / maintainer.
2. In-game sizing/UI-scale/long-text/scroll/input/disabled/model-from-outside/close-reopen checks —
   maintainer.
3. Document-reload last-known-good path in-game (valid/invalid candidates, no VM re-create, no duplicate
   subscriptions, no draft commit) — maintainer.
4. One comparable state sheet: `Vanilla` vs `DarkGold` vs a custom override at the same
   window/scale/language across normal/hover/pressed/focus/disabled — maintainer visual acceptance.

Supported-0.7-contract status: **established** (contract recorded, implementation automated-verified).
Consumer-verified / game-verified: **not claimed**. Release acceptance and any push: maintainer decision,
separate authorization.
