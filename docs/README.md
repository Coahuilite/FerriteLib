# FerriteLib.UiKit documentation

**0.8.0-rc1 is a published prerelease; 0.7.x is the frozen previous line.** Choose the document for the
version and task you actually have. Native acceptance remains separate from the published API and source
evidence. Consumer documentation is versioned with this source tree; the installable Release archive
is the runtime carrier, not a copy of the documentation tree.

## Use the 0.8 library

| Need | Read |
| --- | --- |
| Integrate or migrate a consumer | [0.8 RC consumer guide](consumers/consume-from-0.8.0-dev.md) |
| Author an ordinary settings page | [Settings recipe](consumers/ordinary-settings.md) |
| Fit a native window, handle UI scaling and choose compact layouts | [RimWorld window guide](consumers/rimworld-window-layout.md) |
| Understand the current architecture | [Architecture map](architecture.md) |
| Check compatibility commitments | [Public API tiers](api-tiers.md) |
| Look up supported 0.8 author paths | [Public contract](development/0.8/public-contract.md) |
| Build, select a DLL, or read diagnostics | [Build and debugging](build-and-debug.md) |
| Plan a real-game observation | [In-game walkthrough](in-game-walkthrough.md) |

## Read the 0.8 specification

[UiKit 0.8 first-edition specification](development/0.8/README.md) separates confirmed architecture,
default-wrapper contracts, implementation evidence and remaining acceptance. It does not require
maintainer-local project-management records to be understood.

## Trace evidence without treating it as current behavior

- [0.7 consumer guide](consumers/consume-from-0.7.0.md) and [line record](development/0.7/README.md):
  frozen usage, bounded acceptance, and remaining evidence gaps.
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
