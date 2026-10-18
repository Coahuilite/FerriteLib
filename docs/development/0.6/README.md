# FerriteLib 0.6 — versioned contract and evidence

This completed round began on 2026-09-16, branch `0.6.x`, contract axis `0.6.0`. It added lifecycle hooks,
notification adaptation, reload scheduling and a read-only widget catalog. Its independent demo was a
reference consumer, not evidence of a second real consumer. Current integration starts at the
[0.7 guide](../../consumers/consume-from-0.7.0.md); the next design is [0.8](../0.8/README.md).

| Document | Purpose |
| --- | --- |
| [20-api-and-xml.md](20-api-and-xml.md) | Usage of the additions; still referenced by current consumer guidance |
| [05-api-contract.md](05-api-contract.md) | Versioned public contract, not a new implementation assignment |
| [30-consumer-handoff.md](30-consumer-handoff.md) | 0.6 range, migration and limits |
| [40-verification.md](40-verification.md) | Recorded results and external evidence gaps |
| [60-review-r06.md](60-review-r06.md) | R06-1..R06-7 and risk dispositions |
| [verification/r06-2-final-verification.md](verification/r06-2-final-verification.md) | Final independent checks after the review fixes |
| [00-baseline.md](00-baseline.md) | Historical rationale, scope and known risks |
| [10-work-packages.md](10-work-packages.md) | Package identifiers and evidence map |
| [50-dev-package.md](50-dev-package.md) | The historical R06-fixed candidate identity, not today's artifact |

The R06 review exposed real defects, including demo payload reachability and pending-document refresh on
reopen. Their reproduction and corrected outcomes are retained in the evidence reports. A successful build,
public-API probe or demo package inspection is not an in-game pass. The recorded 0.6 game/operator checklist
and real-consumer gaps retain their original status; later version observations do not retroactively verify
this candidate.
