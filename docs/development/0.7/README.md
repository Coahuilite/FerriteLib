# 0.7.x — frozen implementation and evidence

**Status reviewed 2026-10-18: 0.7.x is frozen in place.** The current design work is the
[0.8 first-edition specification](../0.8/README.md). It neither changes the 0.7 payload nor authorizes a
new build or publication.

## Read by purpose

| Need | Document |
| --- | --- |
| Existing behavior and migration detail | [Supported contract](05-api-contract.md) |
| Consumer setup and migration | [0.7 consumer guide](../../consumers/consume-from-0.7.0.md) |
| API compatibility membership | [API tiers](../../api-tiers.md) |
| Bounded verification and remaining gaps | [Verification record](40-verification.md) |
| Why the placement/tone decisions were made | [Decision index](10-change-plan.md) |
| Historical capability proposals and tier-reason review | [Capability dispositions](60-capability-dispositions.md) |

## What this line contains

The line began with Row Auto fallback, creation-time dimension validation, dropdown corrections and an
ordinary-authoring recipe. Later amendments added placement, text fields, help identities, measured-content
sizing, typed options, image composition, appearance selection, development diagnostics and the FL-IC1/IC2
input/edit contracts. Their detailed meanings have one home in the contract, rather than another chronology
here. Current palette behavior is R2: Vanilla fallback with explicit token overrides, not the earlier
unpainted-bag/two-factory proposal.

The settings recipe improves discoverability and supplies a compile-checked public-API example. It does
not claim fewer runtime operations: its numeric-row before/after comparison had one row declaration and
one `BindValue`, zero manual geometry, and one model-owned notification path on both sides.

## External acceptance — ownership and status

The recorded candidate based on source `d5af4d8209823bb19f7c112805fcb1b8caf56bbc` received a bounded
maintainer game pass on **2026-10-09**. Popup coverage/scrolling, selected input/edit/cancel paths and the
window shell were observed. It is incorrect to say the whole line has only automated evidence, and equally
incorrect to call this complete real-game validation of every capability. The scope and remaining gaps are
in [40-verification.md](40-verification.md).

The 2026-09-24 off-site-backup-only publication restriction was later superseded by the **2026-10-09
conditional RC route**: `v0.7.0-rc1`, GitHub Pre-release rather than stable/latest, from integrated `main`
and the release workflow after its checks. That record establishes a decision, not that publication
occurred. The current documentation task does not resume push/tag/release actions.

## Historical references

The [2026-09-25 consumer-impact report](../../handoff-ngs-uikit-delta-2026-09-25-zh.md) preserves its original
comparison scope, not a second current guide. The [old next-stage path](next-stage-guide-zh.md) remains a
short redirect so existing references do not dispatch obsolete work.
