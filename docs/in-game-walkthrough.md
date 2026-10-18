# In-game walkthrough — observe the selected candidate

Use this form when a task requires real-game evidence. The modelled text metrics and window stack in the
harness cannot establish exact game geometry, IME or all input attribution. **Do not rebuild or rerun an
accepted frozen candidate merely because this procedure exists.** The bounded 2026-10-09 observations are
recorded in [0.7 verification](development/0.7/40-verification.md).

## Before you launch

1. Name the candidate, its source/build configuration and the matching consumer build. Read the candidate's
   own `version.txt`; do not substitute current HEAD or an arbitrary `dist/dev` folder.
2. If a new build is authorized, use [build-and-debug.md](build-and-debug.md). Verification and staging do not
   install into the game. Installation is an explicit operator action, and the consumer must not carry a
   second UiKit DLL.
3. Record RimWorld build, UI scale, language, window size and the relevant page/state. Use an isolated
   test setup for deliberate failure injection or dependency removal; preserve the user's normal setup.
4. Enable the relevant per-host diagnostic subscription when needed. A silent report is meaningful only
   when the observation path was active; do not require unrelated instruments for every check.

## Select the checks that determine acceptance

| Capability | Observe |
| --- | --- |
| Text and automatic sizing | Long/localized labels at the actual width, wrapping, fitting and valid hit area; compare the label band rather than calling it the whole element. |
| Responsive layout and scrolling | Cross the page's breakpoint in both directions; verify reflow, visibility, scroll reachability and current—not previous-pass—hit geometry. |
| Popup/input arbitration | Open a menu over its trigger and other controls; select an option, scroll to both ends, then scroll the parent. The covered control must not also execute. |
| Editing and cancellation | Exercise the declared Live/deferred policy, Enter/Escape and blur after an earlier-drawn control consumes the press. Observe model writes and whether the window remains open. |
| Window shell | Open, move/resize and close through the real stack; record close sound separately if it is part of acceptance. Use multiple windows only when attribution is the claim being tested. |
| Recovery | In an authorized disposable test setup, make one known widget fail. Check the scoped recovery display, unaffected neighbors and attributable diagnostic—not merely absence of a crash. |
| Carrier failure presentation | In an isolated setup, observe missing and duplicate carrier cases. Record readable refusal, silent load, or engine error; “no error” alone is not proof of duplicate detection. |

Only run the rows relevant to the changed contract. A historical stub pass cannot close a game failure, and
one game pass cannot close every unobserved capability.

## Record the result

For each selected scenario, retain the candidate identity, action, expected result, observed result and
supporting user feedback or diagnostic. Mark unobserved cases as unobserved. Store the durable result in the
line's verification record; a blank checklist row is not a pass. Route an actual discrepancy to the owner
of the failed boundary before changing either product behavior or the test model.
