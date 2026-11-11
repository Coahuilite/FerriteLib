# FerriteLib

**English** | [中文](./README.zh-CN.md)

FerriteLib is a **no-content prerequisite mod** for RimWorld. It carries one assembly, `FerriteLib.UiKit.dll`: a UI toolkit for
mod authors. It adds no Defs, patches, languages or textures; enabling it alone changes nothing in the game.

## Status

| Item | Status |
| --- | --- |
| Development | 0.9.0, API range `[0.9.0, 0.10.0)`; [Tools/semantic/headless guide](docs/development/0.9/tools.md) |
| Published | [v0.8.0-rc1](https://github.com/Coahuilite/FerriteLib/releases/tag/v0.8.0-rc1) (GitHub prerelease, not latest) |
| Local only | 0.9 normal carrier; not published. The separate 0.8 maintenance candidate remains unreleased. |
| RimWorld 1.6 | Separate 0.8 maintenance/recovery; c3-dev inherits r6 native acceptance gaps |
| RimWorld 1.7 | 0.9 net10.0; isolated full-window Native17 consumption recorded. Game17 is build/package only; game, physical input and IME remain unverified. See the implementation map. |

Per-item evidence and known deviations live in [implementation and open deviations](docs/openspec/implementation.md), not here.

Ordinary `NotifyChanged` still permits redundant, same-value and off-page keys. A notification/layout-observation
convergence gap remains [open](docs/openspec/implementation.md#fl09-reliability); consumer workarounds do not establish a core fix.
[Dual-axis scrolling and a limited pan/zoom viewport](docs/openspec/changes/fl09-basic-ui-capabilities/proposal.md)
are adopted development targets, not implemented features. The [0.9 plan](docs/development/0.9/extension-plan.md) separates them from existing APIs and later focus/accessibility work.

## For players

- You only need it when a mod that depends on it (such as Universal Squeaker) is installed. Load it before those mods.
- **Install exactly one copy.** Never copy `FerriteLib.UiKit.dll` into another mod's folder: two copies race for the same
  assembly name by load order, and the losing copy cannot tell.
- If it is in your list and no enabled mod depends on it, it is safe to disable.

## How it works

In one sentence: **an engine-free UI core (Kernel) plus one RimWorld adapter, shipped in a single DLL.**
The core keeps UI structure, local state and geometry; text rendering, input and windows still run through the game's native
systems; business data, saving and the decision whether a request is accepted always belong to the consuming mod.

### The main line: five roles

```text
Author's mod ─── business model (values, eligibility, saving)
   │  bindings read values and send requests
   ▼
UiHost ───────── composition entry for one page: adopts definitions, syncs presentation, drives layout and drawing, closes
   ├─ UiSession / UiNode ── local UI state: node identity, drafts, focus, popups, scrolling
   ├─ UiBindings ────────── connection to the author's model: reads, write requests, commands; reactive is optional
   ├─ LayoutEngine ──────── measure and arrange, incremental reuse, geometry results
   └─ Widgets / Controls ── default controls: handle eligible input first, then paint
          │
          ▼  neutral ports: IUiExecution / IUiDrawing / ITextMetrics
Public Verse adapter (1.7 target) ── real events, windows, font metrics, native editing and IME, drawing
```

| Role | Owns | Does not own |
| --- | --- | --- |
| UiHost | page definition, layout engine, presentation connection, close | the business model |
| UiSession / UiNode | node identity, drafts, interaction, popups, scrolling, help state | game object lifetimes, saved configuration |
| UiBindings | the channel for reads and write requests | whether a write is accepted |
| LayoutEngine | size, position, clipping, hit geometry | the application's breakpoint policy |
| Adapter | native events, windows, fonts, IME, texture references | neutral core logic |

### What happens in one frame

1. A native window callback fires; the adapter captures that event as a `UiFrame`.
2. `UiHost.DrawFrame` syncs presentation data, measures and arranges, then lets each control handle input and paint on the
   current layout.
3. When a control confirms an eligible operation, it sends one request through its binding. A normal return or an exception is
   kept as it happened; nothing is retried automatically.

Two common misreadings: a call named Draw also processes input; and `DrawFrame` is a synchronous call, not a game tick or
animation clock, so it may run several times in one displayed frame.

### You do not have to adopt everything

| You need | Use |
| --- | --- |
| A complete settings page or window | XML or C# definition + bindings + `UiHost`; `UiPageWindow` supplies the window shell |
| A page inside your own native window | your own host + `UiHost.DrawFrame` |
| A single control | `UiSession.CreateContext` + Controls |
| Interaction semantics only | `UiInteractionSession` and the other neutral state machines |
| Visuals only | `UiThemeDraw` + `IUiDrawing` + `ITextMetrics` |

### Source map

| Path | Contents |
| --- | --- |
| `Source/FerriteLib.UiKit/Kernel/` | the whole neutral core, with no game references: definitions and compiler, bindings, Host/Session/Node, layout, appearance, diagnostics and recovery |
| `Kernel/Neutral/` | the lowest pure-logic layer: layout math, interaction and editing state machines, reactive runtime, palettes |
| `Kernel/Widgets/` | default controls: Button, Checkbox, Dropdown, Slider, TextField, WrappedText and others |
| `Adapters/RimWorld16/` | shared public Verse adapter source, also compiled for 1.7: window host, `VerseUiExecution`, font metrics, clock and main thread |
| `NeutralSources.props` | compiles the whole Kernel into both the runtime DLL and the platform-free contract tests |
| `tools/` | core and adapter contract tests, author examples |

For more detail see the [architecture page](docs/architecture.md) and the composition diagram in the
[current specification](docs/specification.md).

## For mod authors

- **Assert the version range in your own startup code:** `FerriteLibVersion.Require(min, max, packageId, out report)`.
  RimWorld's `modDependencies` cannot express a version; this call enumerates loaded carriers, reports collisions and fails readably.
- For 0.9, compile against the supplied fixed 1.7 DLL, targeting `net10.0`. Published 0.8 assets remain the `net472` maintenance input.
  Do not ship a copy of the DLL with your mod.
- Only types marked **stable** in the [API tiers](docs/api-tiers.md) keep their signatures inside the `[min, max)` range you
  compiled against; **public-unstable** types need a recompile at each minor. After publication, breaking a stable contract
  requires a major release, and a deprecation keeps the old contract through at least two later published minors.
- Start here: [ordinary settings recipe](docs/consumers/ordinary-settings.md) →
  [public author paths S1–S7](docs/openspec/specs/policies/public-authoring/spec.md) →
  [window layout guide](docs/consumers/rimworld-window-layout.md) → [0.9 connection/target guide](docs/development/0.9/tools.md).
- Third-party use is welcome. When the library forced you to hand-roll something, open an issue with a citation into your own
  working code. Bug reports are always welcome.

## For contributors

The 0.9 target commands are in the connection guide. The following retained maintenance verification/packing commands do not stage a 0.9 1.7 delivery.

```powershell
pwsh -NoProfile -File scripts/verify-local.ps1                  # gate suite: tests, both build flavors, payload, content-free, licence, identity
pwsh -NoProfile -File scripts/verify-local.ps1 -PackDev         # also stages dist/dev/FerriteLib (no archive)
pwsh -NoProfile -File scripts/privacy-audit.ps1 -FullHistory    # full-history privacy gate, run before any push
```

| Document | Purpose |
| --- | --- |
| [docs/specification.md](docs/specification.md) | current specification entry, the single authority for behavior guarantees |
| [docs/openspec/specs/](docs/openspec/specs/) | specification text, one folder per capability |
| [docs/openspec/implementation.md](docs/openspec/implementation.md) | source-to-specification mapping and known deviations |
| [docs/api-tiers.md](docs/api-tiers.md) | stability tier of every public type |
| [docs/build-and-debug.md](docs/build-and-debug.md) | explicit 0.9 targets, diagnostics, and retained maintenance mechanisms |
| [docs/in-game-walkthrough.md](docs/in-game-walkthrough.md) | checks that can only be done in the game |
| [docs/release-notes/](docs/release-notes/) | per-version release notes |
| `AGENTS.md`, `MEMORY.md`, `TODO.md` | working rules, durable facts and open actions for development agents, not user documentation |

Release flow and tag rules are in the header comments of `.github/workflows/release.yml`: `vBASE-rcN` tags are trials, and the
final `vBASE` must land on the same commit as the last rc.

## Identity

- packageId: `coahuilite.ferritelib`
- Namespace root: `FerriteLib.UiKit` (kernel: `FerriteLib.UiKit.Kernel`)
- Log prefix: `[FerriteLib.UiKit]`
- License: [MPL-2.0](LICENSE), full text ships in every package
