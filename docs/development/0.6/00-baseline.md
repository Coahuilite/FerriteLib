# T0 — round baseline

Date opened: 2026-09-16. Author: Lead. Status: established, not evidence of product behaviour.

## 1. Where the round starts

| Axis | Value | How it is pinned |
| --- | --- | --- |
| Branch | `0.6.x`, forked from `0.5.x` tip `354d90a` | `git rev-parse` |
| Parent branch state | `0.5.x` at `354d90a`, working tree clean, pushed to `origin/0.5.x` | `git status --porcelain` empty |
| Contract axis | `FerriteLibVersion.Api = 0.6.0` | `FerriteLibVersionTests` |
| Release axis | `About/About.xml <modVersion>0.6.0` | same lane, major.minor must agree |
| Build axis | `<VersionPrefix>0.6.0</VersionPrefix>` | `pack-dev.ps1` label |

`0.5.x` is **left untouched** by this round: it is the frozen 0.5.0 line whose external acceptance is still
outstanding, and a fix for it must be made there and cherry-picked forward rather than made here.

## 2. The version ruling, and the reading it rejects

`FerriteLibVersion.Api`'s own summary is unconditional for additions: "while `Version.Major` is 0, ANY
change to the public surface a consumer compiles against — addition or break — bumps `Version.Minor`", and
its recorded origin is exactly an additive type (UiPopup) that shipped without the bump. This round adds
public types and public members, so the axis moves **0.5.0 → 0.6.0**, once, for the whole round.

The competing reading is real and is rejected in writing rather than silently:

- *"0.5.0 was never released, so its window is still open."* This is the precedent the maintainer used to
  refile 0.4.0 → 0.3.0 (`TODO.md` §0: "the release never shipped, so its contract axis has no goalpost to
  move").
- It does **not** apply here. The 0.5.0 dev package and its `[0.5.0,0.6.0)` handoff were delivered
  (`docs/development/0.5/30-consumer-handoff.md`), so the number has a goalpost a consumer may already have
  pinned. Growing the surface under a delivered number is the failure the rule exists to prevent;
  re-pinning a consumer's constructor is a one-line change and stays honest.
- The round's instruction says the same thing in one line: "不能默认为仍发布 0.5.0".

Consequence for consumers: **0.5.0 keeps compiling against the 0.5.0 carrier it tested.** Nothing in this
round breaks it; a consumer that wants the new surface re-pins to `[0.6.0,0.7.0)`. The migration note is
written where the promise lives (`docs/api-tiers.md`, "Breaking changes inside the open 0.6 window").

## 3. What the round inherits (and does not re-open)

Carried forward as already-established, with their evidence unchanged because the code did not change:

- `0.5.x` delivery + review round closed at `354d90a`: external review `0117c01` returned Request changes
  with 7 reproduced defects (R1–R7), all fixed, the reviewer's own probe re-run green, gates green,
  `privacy-audit.ps1` CLEAN, harness `1544 ok / 0 FAIL` at the verified tip.
  Evidence: `docs/development/0.5/40-verification.md` §7, `.../verification/post-fix-*.md`.
- R1–R7's regression lanes stay in force and are re-run by this round's gates. A round that broke one of them
  would be a finding, not a new baseline.
- **Still open, and not to be marked done here:** in-game acceptance A1–A11 and a real consumer compiling
  against the 0.5 surface. Both belong to other actors; `0.5.x`'s own documents remain their home.

Honest gaps carried forward unchanged (they are inputs to this round, not new claims): R7's TOCTOU semantics
are not behaviourally pinned; the aborted-close edge (PreClose with no PostClose) leaves a stale catalog
entry; two catalogs working at once have no combination lane; diagnostics have coverage gaps (construct-time
style drops, pass-1 chrome, terminal `PageUnavailable` notice go through the legacy channel); window
overlap follows the catalog's own activation order rather than the vanilla stack order; and the window lane
can throw an NRE instead of a named FAIL when `ApplyOptions`'s default direction is broken.

## 4. Scope of this round

In: page lifecycle and VM/View lifecycle separation; an optional explicit
`INotifyPropertyChanged` → binding-key adapter; automatic reload scheduling with testable time, pause
independence, bounded retry, bounded input deferral and no rebuild while nothing is open; a read-only widget
description snapshot that keeps scope; and an **independent demo mod** that consumes only public API.

Out, per the plan's YAGNI section: mandatory VM base class, DI, code generation, reflection or deep-path
binding, derived dependency graphs, a cross-mod business bus, a plugin marketplace, C# hot reload, docking,
cross-save window state, and any expansion of layout/style coordination into a general cross-file transaction
framework. Also out: modifying UniversalSqueaker, `nivarian_grand_structure` or any other consumer repo;
deploying into the game's `Mods/`; pushing, tagging, releasing or creating a remote.

## 5. The one contract this round is built on

`05-api-contract.md` was written and landed **before** any package started, together with compiling
skeletons for every new public type, their tier entries, and the three version axes. Parallel work is only
safe because the seam is already in the tree; a package that needs to move it asks first.

## 6. Write scopes (advisory, but the round is arranged around them)

| Writer | Exclusive scope |
| --- | --- |
| Lead | `docs/api-tiers.md`, `docs/development/0.6/**`, the three version axes, `TODO.md`, `MEMORY.md`, merges, `dist/` |
| `mvvm` | `Kernel/UiNotifyAdapter.cs`, `Kernel/IUiMainThread.cs`, `Kernel/VerseFerriteMainThread.cs`, `Kernel/ReferenceEqualityComparer.cs`, `Kernel/UiWindowHost.cs`, `Kernel/UiPageWindow.cs`, its own lane file and `Program.cs` registration |
| `reload` | `Kernel/IUiTimeSource.cs`, `Kernel/VerseFerriteTimeSource.cs`, `Kernel/UiReloadPolicy.cs`, `Kernel/UiReloadSchedulerState.cs`, `Kernel/UiDocumentService.cs`, `Kernel/UiHost.cs`, its own lane file and `Program.cs` registration |
| `catalog` | `Kernel/UiWidgetCatalog.cs`, `Kernel/UiWidgetDescriptor.cs`, `Kernel/UiWidgetRegistry.cs`, its own lane file and `Program.cs` registration |
| `demo` | `ferritelib_uikit_demo/**` — the entire project, and nothing else |
| `verify` | `docs/development/0.6/verification/**` (its own records), mutations under a temporary worktree |
| `probe` | `dist/probe-*/` (gitignored) and `docs/development/0.6/verification/**` |

`Program.cs` and `docs/api-tiers.md` are the two shared files. `api-tiers.md` is Lead-only this round so
the tier list cannot drift while three packages add types; `Program.cs` is edited additively by each package
in its own worktree and its merge conflicts are resolved by the Lead.

## 7. Risks this round accepts knowingly

1. **The demo cannot be run here.** It compiles and its package is inspectable; whether it draws correctly in
   `DoSettingsWindowContents` is an in-game item, and `40-verification.md` will say so rather than infer it
   from a successful build.
2. **Timing lanes are clock-driven, not game-driven.** A fake clock proves the scheduler's arithmetic; it does
   not prove that the commit point chosen in the game's update order is the safe one. That claim rests on the
   vanilla source reading recorded by `reload` and on an in-game item.
3. **`Show`-time registration races.** A mod registering after the settings screen opened is exercised by a
   lane, not by two real mods loading in an order nobody controls.
4. **Two verifiers, one repository.** Both are independent of the implementers, but neither is a second
   consumer repository, and neither may raise the validated-surface count.
