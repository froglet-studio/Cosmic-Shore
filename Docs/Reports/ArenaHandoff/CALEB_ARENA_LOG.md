# Caleb's arena project log

Project branch: `claude/caleb-arena`. Start each thread from it and read
[`GARRETT_HANDOFF_2026-11-25.md`](GARRETT_HANDOFF_2026-11-25.md) first. Newest entry on top.

## 2026-11-26: #2332 landed (learned Mass-Space AI rows)

Caleb went ahead on Yash's AIPilot lane, expecting his yes; Garrett's hand-off says to merge #2332 first, and it is
not one of his 11 open calls.

- Merged bleeding-edge into `claude/project-thread-d1elkw` with a merge commit (`46849fdc`). Three conflicts:
  `PackageComponents.cs` (both sides had made `Shadow` a `UIBehaviour`; took bleeding-edge's), `BroodJudge.cs` (kept
  bleeding-edge's elemental-crystal count and held-by-trail; `presses` and `pilot` were already on both) and the
  verification checklist (kept both sections). Bleeding-edge had retired the Stoat dipole actions meanwhile; every
  ability guid in `AIMassSpacePlan.asset` still resolves (Sling, Sparrow guns, Meerkat soliton, Dolphin drift).
- `approvals.py check --me Caleb`: exit 2, a collision with Garrett's own Urchin / Froglet Compute lanes on that same
  branch (this PR). Went ahead as the hand-off asks; the commit says "Affects", and Garrett has a queue entry.
  Yash's AIPilot entry came with the PR.
- Checks on the merged result: refcompile 0 errors in changed files (the same 30 elsewhere as plain bleeding-edge,
  all from packages this container can't download); `check_meta_files.py --tracked` the same 2 orphans as
  bleeding-edge (`Assets/Plugins/FMOD/Cache/Editor.meta`, `Assets/StreamingAssets.meta`); `gates.sh` the same 4
  failures as bleeding-edge (doc_summaries in VoyageRules.cs, maelstrom_tiers, colour_grade, a pilot portrait
  drift); `massspace_ai_plan.py --check` ok. GitHub CI green (pr-compile, Static validation, checklist, large-files).
- Took #2332 out of draft and merged it into bleeding-edge as `8b2e12f3`. No approving review was needed.
- Merged bleeding-edge `8b2e12f3` into `claude/caleb-arena`: no conflicts; the clean-open check and refcompile on the
  result match bleeding-edge exactly (2 orphans, 30 package errors, 0 in changed files).
- Not run in the Unity editor. The hand test is in `Docs/UNITY_VERIFICATION_CHECKLIST.md` § Mass-Space AI rows.
- #2346 left alone (Garrett's open call 11).

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

Branch pushed and ready for Caleb's Unity playtest. Regatta and Broadside can start now. Brood Rush can start now too: #2332 is on bleeding-edge and
this branch.
