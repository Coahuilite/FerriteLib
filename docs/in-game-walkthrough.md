# In-game walkthrough — the checklist §1 cannot substitute for

Why this file exists: every geometry claim in this library rests on `StubTextWidth`, a linear
character-count model, and the game's real glyph advance exists only inside the game (`MEMORY.md`,
"Text fit"). The harness lanes are future-regression guards. Nothing below is answered by a test, a build
or a code read — the answers are only observable on screen, and the branch is already built.

## Before you launch

1. `pwsh -NoProfile -File scripts/verify-local.ps1 -PackDev`
   Stages `dist/dev/FerriteLib/`. Stop if any of the seven checks fails — that means the bytes you are about
   to test are not the bytes the lanes just passed.
2. Copy `dist/dev/FerriteLib/` **and** the consumer's migrated build into your own `Mods/` directory by
   hand. No script does this and no link or junction stands in for it (`AGENTS.md`, Boundaries).
3. Read `dist/dev/FerriteLib/version.txt`: the `carrier=` line must say `Release 0.3.x+…` and `commit=`
   must name a commit you recognise from `git log --oneline -3`.
4. Enable detailed UI logging in the consumer (`usdiag`), so `evt=ui.text.overflow` can be silent *and
   provably armed*.
5. Predicate for "you are testing what we tested": `git diff --name-only main 0.3.x` returns `.md` files
   only. If it names a source file, run step 1 again on that tree before you trust anything below.

## The seven live checks

Record one line each: **observed / surprised-what / changed-nothing**. A blank row is not a pass.

| # | Check | What you do | What you are looking at | If it surprises you |
|---|---|---|---|---|
| 1 | Auto width in real glyphs | Open the consumer's mood-tuning card, both Chinese and English | Factor labels hug their translated names — no clipped tail, no wide gap before the control run | Round 3's `Width="Auto"` measurement path is wrong in a way the model cannot show; re-shape the label-set budget, not the manifest |
| 2 | No degenerate interval survives | Drag every slider in the mood card to both ends, then to a value between two steps | The slider track never collapses to zero usable width; the stepper's `−`/`+` stay clickable | US's threshold deletion or our clamp arithmetic is at fault; re-derive from the shipped rect before touching either |
| 3 | Narrow → wide re-arrange | Drag the window's edge across the declared `Breakpoint`, both directions, while the page is open | Columns re-flow without a reopen, without flicker into a dead frame, and `NarrowHidden` items disappear/reappear rather than overlapping | `Breakpoint` state is not re-evaluated per pass; the engine owns the fix, not the consumer |
| 4 | Shell in the real window stack | Open the consumer's settings window once and close it with the shell's `CloseText` button | It opens above the map, takes the drag, plays the close sound, and the game's own windows still behave | A signature the ref assembly and the stub both agreed on is lying; this is the only lane that can catch it (`TODO.md` §1) |
| 5 | Fit audit's two new paths | Walk all five workspaces plus the overlay in both languages, logging on | `usdiag evt=ui.text.overflow` stays silent; no container title sits misaligned vertically | Either an overflow the audit could not see before is real (fix the layout), or the routing changed an anchor (fix the outlet); record which |
| 6 | Recovery band trip | Temporarily make one core widget throw at Draw (a test build of the consumer, not the repo), open the page | A warning-toned band naming that element's path, the rest of the page still drawing, exactly one log line per slot | The band's paint or the session slot key is wrong; the engine's recovery contract is what is on trial, not the demo widget |
| 7 | Carrier absent, and carrier duplicated | (a) Disable FerriteLib, restart, look at the consumer. (b) Re-enable, then copy `FerriteLib.UiKit.dll` into the *installed* consumer's `1.6/Assemblies` and restart | (a) what the player actually sees: a readable message, a silent absence, or an exception wall. (b) which of the three outcomes in `TODO.md` §1 happens | (a) is the first failure a real user meets, so its wording is a product decision; (b) outcome 2 means the duplicate guard counts names where it must compare identity |

## After

Write the seven results into `TODO.md` §1 — the section is the ledger, this file is only the form. Anything
that surprises you changes code in the branch that owns the mistake, and `MEMORY.md` records what was
learned together with which lane now guards it. Nothing here blocks a local commit; only the `v0.3.0`
trial decision waits on this page being filled in.
