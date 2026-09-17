# 0.7.x — verification record

Commands are the repository's own (plan §D); no new framework. Harness:
`dotnet run --project tools/FerriteLib.UiKit.Tests -c Release` (full run each step; no filter CLI exists and
none was invented). Gates: `pwsh -NoProfile -File scripts/verify-local.ps1 -PackDev`.

## Regression provenance (what is mutation-proven vs pinned-forward)

Each A lane was re-run against the reverted fix (`git stash` of the production file only, lane files kept)
and observed red on the baseline, then green with the fix. That half is mutation proof. The C default lane
was written red-first the same way (12 failing token assertions on the 0.6 default) while the DarkGold
freeze lane was green before the move and must stay green after it — the pairing is the proof the split is
real and not a wholesale relabel.

| Item | Baseline evidence (red assertions) | After fix | Commit |
| --- | --- | --- | --- |
| A1 Row Auto fallback | 5 of the lane's assertions red on `12d0dbb` (flex-share, empty-label, fixed-sibling split, MaxWidth, mixed row) | green | `21a81cd` |
| A2 Height validation | 7 red (malformed widget/container, NaN, ±Infinity, nested message, invalid reload candidate) | green | `e30af94` |
| A3 Wrap-only Cols | 4 red (Row, Column, Section-with-breakpoint, message) | green | `e30af94` |
| A4 dropdown precedence | required case red (`Option1=b/Value2=b` displayed `b`, not `Second`) | green | `c8fe06e` |
| A5 options diagnostic | category sentence red (baseline said only "missing") | green | `88f1c77` |
| C default palette | 12 default-token assertions red before the change | green | `88095fb` |
| C DarkGold freeze | green before AND after the change (frozen literals) | green | `88095fb` |

B's eight lanes are a new reference over existing surface: they cannot be red on a "before" (nothing in B
changed production code); their weight is compile-check plus behavior pins — geometry-free authoring, the
shared-binding round trip through the real commit seams, paint/measure/structure invalidation classes,
show/hide without reopen, narrow stack + scroll viewport, and the close/reopen subscription arithmetic
through `UiWindowCatalog`. The A4 lane also exposed (and the same commit fixes) a null-`ValueN` crash in
the static-option path: baseline drew a recovery band instead of the option list.

Everything here remains stub-harness evidence. None of it is 已由真实消费者接入 or 已实机验证.

## Integrated gates and staged package (measured, not assumed)

`verify-local.ps1 -PackDev` after the last code commit: all nine gates OK, staging OK.

- Staged folder: `dist/dev/FerriteLib/` (5 files)
- `version.txt`: `FerriteLib 0.7.0-dev / build=dev / commit=88095fb3cbed`
- `1.6/Assemblies/FerriteLib.UiKit.dll` SHA-256
  `271128299A9CFF2C4CB5EBDFBB9246C3F6BEC2791A5D0117B894E6F96FC8F82F`
- Commit stamp: built at the C commit; later commits on this line are documentation-only, so the payload's
  embedded `AssemblyInformationalVersion` and the folder's stamp stay true.
- Single-DLL/content-free payload, API tiers, visual-core separation, three-axis version agreement,
  neutrality, containment: all re-asserted by the same gate run.

## Pending external checks

The four numbered items in `README.md` §External acceptance. Consumer/game evidence gates none of the
established contract (maintainer ruling 2026-09-17), and its absence is reported here rather than
converted into a new gate.
