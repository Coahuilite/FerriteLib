# FerriteLib 0.6 round — lightweight MVVM, reload scheduling, and an independent demo mod

Round started 2026-09-16. Branch `0.6.x`, contract axis `0.6.0`. Maintainer plan:
`modding_documents/team-mode/ferritelib-mvvm-hotreload-demo-plan-zh.md` (this round's product contract) and
`.../ferritelib-mvvm-demo-team-start-zh.md` (the instruction that opened it).

**This directory is the round's only live status home.** Nothing here is a claim that code is implemented or
has been accepted in game; every entry states which evidence class it carries.

| Document | What it holds |
| --- | --- |
| `00-baseline.md` | What the round inherited, the version ruling, scope in and out, risks |
| `05-api-contract.md` | **Frozen** cross-package public API. Read this before writing any of it |
| `10-work-packages.md` | T0–T5, owners, write scopes, dependencies, acceptance |
| `20-api-and-xml.md` | Consumer-facing XML and C# usage of the new surface (written as packages land) |
| `30-consumer-handoff.md` | Version range, migration, known limits for a real consumer |
| `40-verification.md` | Commands, results, the in-game checklist, everything unfinished |
| `50-dev-package.md` | Where the dev packages are, their hashes and the source revision behind them |
| `verification/` | Per-package adversarial records, including what a lane does **not** pin |

Evidence classes used throughout, and the rule they exist for (never collapse them):

1. **已实现** — the code exists in the payload.
2. **已自动化验证** — a lane in `tools/FerriteLib.UiKit.Tests` asserts the behaviour, and the record says
   which half is mutation-proven and which is only a future-regression guard.
3. **已由真实消费者接入** — another repository a player uses compiles against this surface. Our own demo is
   **not** this: `AGENTS.md` ("Our own demo is not consumption").
4. **已实机验证** — observed in a running game session, with the session's shape recorded.
5. **尚待外部团队验证** — named, owned elsewhere, and never recorded as done from here.
