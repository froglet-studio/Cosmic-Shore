# Caleb's arena project log

Project branch: `claude/caleb-arena`. Start each thread from it and read
[`GARRETT_HANDOFF_2026-11-25.md`](GARRETT_HANDOFF_2026-11-25.md) first. Newest entry on top.

## 2026-11-26: project branch created

- Created `claude/caleb-arena` from bleeding-edge `e9b0c4c7` (#2407) and merged `claude/sleepy-ride-r3wozq`
  (the hand-off doc, Caleb's Regatta / Broadside / Brood Rush lane claims, and the `element_ability_table` JSON
  reader fix for swimlanes, fleet codex and mode chart). The merge had no conflicts and touched no `.meta`,
  settings or scene file, so no clean-open check or refcompile was needed.
- `approvals.py check --me Caleb`: no collision. Heads-up only (Shombith: fleet_codex, swimlanes; Yash:
  vessel_mode_chart). Their queue entries came with the merged branch.
- The tool fix is also open on its own for bleeding-edge as #2408 (Shombith's review).

### PRs #2332 and #2346 (checked 2026-11-26, neither merged)

| PR | What | State | Why it can't merge now |
|---|---|---|---|
| #2332 | Learned Mass-Space AI rows (`AIMassSpacePlanSO`, `AIPilot` hook) for Meerkat, Sparrow, Stoat, Urchin | Draft, merge conflicts with bleeding-edge, no reviews | Garrett's draft in Yash's AIPilot lane (queued for Yash's `/approve`) and Shombith's judge. Needs the conflicts resolved, marked ready, and Yash's yes. |
| #2346 | Grizzly Time AI retrain | Open, mergeable, blocked by branch protection, no reviews | Body says "Do not merge without Garrett's go". It is Garrett's open call 11, collides with Yash's Grizzly lane, and was trained before the grizzly-v3 kit (#2358, #2385), so it likely needs a re-run first. |

Does the project need them?
- **#2332: yes, for Brood Rush.** The hand-off says Brood Rush on bleeding-edge is missing it and the judge's
  numbers and Unity runs were on a tree with it. Regatta and Broadside don't depend on it.
- **#2346: no.** It only moves Regatta's Grizzly 246 to 242 s (still flagged) and is Garrett's call. Not needed to
  start.

## Where work stopped

Branch pushed and ready for Caleb's Unity playtest. Regatta and Broadside can start now. Brood Rush waits on #2332
(see above) or on Caleb's choice to carry it on this branch for playtesting.
