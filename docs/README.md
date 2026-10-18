# FerriteLib.UiKit documentation

**0.7.x is frozen; 0.8.x is a design review, not an implemented release.** Choose the document for the
version and task you actually have. A design decision does not change the current DLL or its API tiers.

## Use the frozen 0.7 library

| Need | Read |
| --- | --- |
| Integrate or migrate a consumer | [0.7 consumer guide](consumers/consume-from-0.7.0.md) |
| Author an ordinary settings page | [Settings recipe](consumers/ordinary-settings.md) |
| Understand the current architecture | [Architecture map](architecture.md) |
| Check compatibility commitments | [Public API tiers](api-tiers.md) |
| Look up detailed 0.7 behavior | [0.7 contract](development/0.7/05-api-contract.md) |
| Build, select a DLL, or read diagnostics | [Build and debugging](build-and-debug.md) |
| Plan a real-game observation | [In-game walkthrough](in-game-walkthrough.md) |

## Review the next design

[UiKit 0.8 first-edition specification](development/0.8/README.md) separates confirmed architecture,
default-wrapper contracts, and remaining decisions. It is the distributed design entry; it does not require
maintainer-local project-management records to be understood.

## Trace evidence without treating it as current behavior

- [0.7 line record](development/0.7/README.md): frozen contract, bounded acceptance, and remaining evidence gaps.
- [0.6 record](development/0.6/README.md) and [0.5 record](development/0.5/README.md): versioned usage and
  historical verification. Their old work packages are not a live execution queue.
- [Dated consumer-impact report](handoff-ngs-uikit-delta-2026-09-25-zh.md): historical source comparison,
  with the current migration route linked rather than duplicated.
- [Push privacy gate](push-privacy-gate.md): publication-time privacy procedure, not product acceptance.

Source inspection establishes what code says; a stub run establishes the named modeled behavior; a game
pass establishes only the candidate and scenarios observed. A held DLL identifies its own build, not the
current checkout. Publication decisions and actual publication are separate events.

Document ownership: consumer instructions live under `consumers/`; compatibility membership lives only in
`api-tiers.md`; line contracts and scoped evidence live under `development/<version>/`. Keep one current
statement of a rule and link it from other entries. Preserve historical evidence where it explains a
result, not as a second set of current instructions.
