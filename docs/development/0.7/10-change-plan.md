# 0.7 placement and appearance decisions — historical index

The 2026-09-18 change plan is closed as an execution plan. This file retains its decision identities for
existing references; the implemented rules and migrations live in [05-api-contract.md](05-api-contract.md)
and the [consumer guide](../../consumers/consume-from-0.7.0.md). The 0.7 line is frozen.

## Decisions and outcomes

| Item | Settled result and current home |
| --- | --- |
| CP-0: density reaches containers | Implemented: absent `Padding`/`Gap` use theme geometry. Explicit values, including zero, win. Contract: Batch 1. |
| CP-1 / CP-2: parent-relative placement | Implemented: four child attributes, `AlignX`, `OffsetX`, `AlignY`, `OffsetY`; reference is the parent's inner box. `Overlay` permits both axes; flow permits its cross-axis subset and pixel nudges. Contract: Batch 1 and `UiPlacement`. |
| D2 / D3: representation and units | Attributes rather than a new `<Place>` node; percentages refer to the parent's inner span, while alignment supplies the child's pivot. No solver or second geometry authority. |
| CP-4 / D5: move roles into a style document | Withdrawn. Role vocabulary and meaning stay in code; authored roles are not arbitrary names a skin may omit. The skin chooses treatment. |
| CP-6(1,3) / D6: roles versus states | Authored `Tone` narrowed to `Neutral`, `Success`, `Warning`, `Danger`. Legacy `Active`/`Disabled` are redirected for the documented minor; stable `UiStatusTone` enum members remain. No predicted `Resolve` signature rewrite was needed. |
| CP-6(4) / D7: accent | `HoverPoint` was removed, not retained as an alias; `AccentHover` is derived from `AccentGold`. This was an explicit coordinated 0.7 break. |
| CP-3 / CP-6(2) / CP-5: skin source, treatment data, regional targeting | These were separate deferred proposals, not delivered by Batch 1. Later R1/R2/SA1 contracts define the actual appearance surface; the original proposal is not an API specification. |
| CP-7: named sibling placement | Not implemented in 0.7. Limited same-parent relations and shared label columns are now part of the [0.8 design scope](../0.8/README.md), not retrospective 0.7 capabilities. |

## Boundaries retained by the delivered contract

- Placement uses the arranged box, not the painted silhouette. A hidden/unarranged element has no current
  arranged placement; retained identity and visible geometry are distinct.
- Flow and placement do not both own the same axis. Invalid combinations fail at creation. Validation
  uses the declared container kind; a responsive kind swap may leave placement inert, as documented.
- Template roots reject the placement names because the collection owns their slots.
- Container spacing changed intentionally; `Padding="0"` / `Gap="0"` retain the previous zero spacing.
- This batch did not supply arbitrary control parts, a headless behavior layer, or general relation solving.

## Evidence and version history

The batch landed on 2026-09-19 with `KernelPlacementTests`, `KernelToneVocabularyTests` and recorded stub
verification. The historical independent review reported 18 probes and clarified pixel cross-axis nudges,
template-root refusal and per-runtime-element diagnostics. It did not establish in-game behavior.
Detailed regression provenance remains in [40-verification.md](40-verification.md).

The original no-minor-change argument relied on no delivered consumer. Once a consumer compiled, the
maintainer explicitly retained coordinated additions within 0.7. That temporary exemption had an expiry at the
first release/tag or end of lockstep; it is not inherited by 0.8. Old estimates, intermediate recommendations
and task ownership have been removed rather than carried forward as a competing plan.
