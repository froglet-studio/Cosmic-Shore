# Trained Time-arcade AIs written to assets (2026-11-08)

Plan step: time-arcade. Garrett, 2026-11-08: "stop gating things through yash he is busy. Get things merged and ready to
test." Seven searched drives from the Regatta-plan round (2026-11-06/07) are now what the game flies in each hull's own
Time minigame. **Every number here is a MODEL number** (each search's offline evaluator, run by hand in cloud threads
while Froglet Compute is paused). None was raced in the Unity editor, and no human has finished most of these modes on
today's kits.

Writer: `Tools/Build/promote_trained_drives.py` (the learner's own `copy_with`, so a variant keeps every field it does not
name), and for the Squirrel `Tools/Build/author_skimrace_ai_config.py`. `promote_trained_drives.py --check` fails when an
asset drifts from its promoted row; `--revert <variant>` writes the old values back (they are in its `OLD` table and in
the table below). The hull's own assets and every other mode are untouched. Regatta is untouched.

The Time-mode nose lead (`GateRaceController.AINoseLeadRow`, PR #1843) is on at the gains these were trained at:
Thresher 2.5, Dolphin 2.5, Rhino 2.5, Stoat 2.5, Sparrow 2.0, Serpent 1.0.

| Hull, mode | Asset | Promoted (source) | Model result | Old values (revert) |
|---|---|---|---|---|
| Serpent, Serpentine | `SerpentAIBoostPolicy_SerpentTime` | Trained C, job `serpenttime-serpent-time` (#1788) | fresh composite 15.85 vs 17.66 for the old variant; 6-11% faster at every intensity, 4 missed mouths vs 40. Lead 1.0: 15.24 (#1832) | align 34.29, stick 0.198, runway 0.985, burns 3, between 0.675, reserve 0 |
| Thresher, Thresher Time | `ThresherAIBoostPolicy_ThresherTime` | Lead-on B at 1,503 evals, job `threshertime-thresher-time-lead` (#1830) | fresh 2.985-3.013 with the lead vs 21.69 for the old genome, which finishes 0 of 48 nest courses. **Needs the lead**: without it this genome DNFs 4-17 of 48 | turn 6.691, distance 115.249, interval 2.866, cone 15, max plant 3.156, hold 1.54, tolerance 3 |
| Stoat, Slingshot | `StoatSlingConfig_Slingshot` | Parent's trained B, job `slingshot-stoat-time` (#1792), the lead-on round's pick (#1831) | re-raced here on bleeding-edge's 60 u trampoline (`slingshot_fit`, 6 courses an intensity, seedbase 1001): 61-70 s, 24/24 finished, 0-3 misses, against the old rows' 150-225 s, 23/24, 14-78 misses; with the lead 2.5: 61-66 s. Trained on Yash's 30 u kit (not on bleeding-edge yet) | turn 35, interval 0.941, distance 123.282, aim scale 100, hold 1.0, warp 0 |
| Sparrow, Breakwater | `SparrowAIBoostPolicy_Breakwater` | Version 2 restarts D/E, job `breakwater-sparrow-time` (#1793): hold the afterburner | 8 fresh courses an intensity: 115-124 s vs 158-162 s; clips the Danger weave 13-22 times a race (the model prices a clip at ~1.5 s) | align 45, release align 60, stick 1, release stick 1, engage runway 2, release runway 1.25 |
| Dolphin, Switchback | `DolphinAIBoostPolicy_Switchback`, `_I1`, `_I2`, `_I4` | Lead-on C, job `switchback-dolphin-time-lead` (#1833), ONE drive raced at every intensity over the `_I1` genome, so all four files carry it (and `_I1`'s stickBand 0.29) | fresh 52 courses: 1.724 vs 1.894 shipped, 52/52 finished | see `OLD` in the script: mode file and `_I2` align 35.308 / stick 0.262 / runway 1.422 / spend 0.835; `_I1` 34.14 / 0.29 / 0.26 / 0.96; `_I4` 51.947 / 0.327 / 1.105 / 0.502; all hold-off 4, margin 3, discharge 0.2, pulse 30 |
| Squirrel, Skim Race | `SkimRaceAIConfig` (general, flown at I3 and wherever a map has no file of its own) | Restart C, job `skimrace-squirrel-time`, 1,204 evals over this policy | loss 4.907 vs 5.456 over I1-I4, 6/6 finished at every intensity. I1, I2 and I4 keep their own specialist files | `GENERAL_V3` in `author_skimrace_ai_config.py` (the 36 skimrace-v3-general numbers); Regatta's copy keeps flying them |
| Rhino, Headlong | `RhinoAIBoostPolicy_Headlong` (new) | Lead-on B, job `drive-rhino-headlong-on-lead` (#1828), over the hull's own policy | with the lag and lead 2.5: 81-102 s a race, 10 missed mouths over 12 races, vs 150-170 s for the parent's drive | none: Headlong flew `RhinoAIBoostPolicy` (delete the variant to revert) |

## What the editor has to say

The model is not the game. For each, the hand test is: AI-only races at I1-I4 in the mode, alone and in a grid, counting
missed mouths and finishes, against the model times above (within ~10%).

- **Thresher**: `ThresherTimeSoakRunner` on the nests. If AIs never finish, check the nose lead is on first.
- **Sparrow**: whether a Danger weave clip costs more than the model's 1.5 s (the element debuff is not modelled). If the
  held drive loses, `--revert SparrowAIBoostPolicy_Breakwater` puts the let-go drive back.
- **Stoat**: whether the warp press throws the hull off the course on the 60 u shell (the model has no Orrery prisms,
  rivals or roll).
- **Rhino**: 10-12 missed mouths remained at I3/I4 in the model.
- **Squirrel**: an AI Squirrel in a grid with a human Squirrel runs the generic AIPilot, not this policy (PR #1782's bug).

## Two things that can undo a row

- **The AI learning sweep** (`ai_learn.py`, hourly, on `Ys-bleeding-edge`) relearns mode variants from people's
  flights through `learn_drives_from_flights.py`, which starts from the existing variant and overwrites the fields it
  learns. A Switchback or Breakwater relearn would replace these numbers; `promote_trained_drives.py --check` says so.
  The learner also floors Breakwater's release runway at 1.25 s (`FLOORS`), which the held drive is under.
- **Merging `Ys-bleeding-edge`** (Yash's 30 u Stoat shell and hard core) changes the Stoat's kit; the rows were trained
  on that kit, so that merge should only help them.

Affects: Yash's AI (the Time-mode variants), Garrett's vessels (read only, no kit changed).
