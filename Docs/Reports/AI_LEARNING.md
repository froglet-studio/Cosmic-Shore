# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-11-24 22:20 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**10 new flight(s)** since the last learn.

## Breakwater

New flights: Sparrow 5.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Sparrow | I1 | 7 | 4 | 84 s | 0 of 1 | 2026-11-20 23:22 |
| Sparrow | I2 | 1 | 0 | - | - | 2026-10-27 18:29 |
| Sparrow | I3 | 4 | 3 | 128 s | 3 of 4 | 2026-11-20 16:00 |
| Sparrow | I4 | 7 | 4 | 89 s | 2 of 5 | 2026-11-25 02:20 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
sparrow       33     22    8.9    79       9%     7%    5/33   0: 31, 1: 21, 5: 16, 10: 14
```

## DogFight

New flights: Sparrow 1.

- **Sparrow: measured, not written** - SparrowAIBoostPolicy is switched off in DogFight (disabledInModes).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Sparrow | I1 | 7 | 0 | - | 2 of 2 | 2026-11-23 21:20 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## PerchRun

New flights: Goby 4.

- **Goby: measured, not written** - its boost policy SquirrelAIBoostPolicy (SkimRingAIPolicySO) presses SquirrelTubeActionSO, which this hull does not bind - the drive cannot act.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Goby | I1 | 3 | 2 | 28 s | 2 of 2 | 2026-11-23 21:58 |
| Goby | I2 | 1 | 0 | - | - | 2026-11-23 21:53 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
goby           4     10   25.8   102       0%    12%    0/4
```

## Files the learners changed

- `Docs/AI_SYSTEM/LEARNED_DRIVES_Breakwater.md`

## What the sweep judged (2026-11-24 22:20 UTC)

10 new flights since the 2026-11-23 sweep: 5 Breakwater (Sparrow I4, Yash), 4 Perch Run (Goby, Will) and 1 Dog Fight
(Sparrow, Will). **No asset under `Assets/Resources/AIBoostPolicies` changed**: the learner wrote nothing.

- **Breakwater (Sparrow I4, 5 flights on `Ys-bleeding-edge` d1839423ce, 7d11758a31, 8d94708c00 and 9c680513a8, which
  all have the committed `SparrowAIBoostPolicy_Breakwater`):** an AI rival won 3 of the 4 races that had rivals, in
  145 to 180 s. The person's fastest win was 182 s (one AI was 2 stations short), and the person's solo run took 272 s.
  The AI finishes and beats the person at I4, so the numbers are enough. The learner moved only its "AI today"
  measures (398 episodes over 624 s) and wrote nothing. Across all 33 Breakwater rival races, circling is 9% and
  stuck is 7%. Nothing to fix.
- **Perch Run (Goby I1/I2, 4 flights, Will):** report only. The Goby borrows `SquirrelAIBoostPolicy`, which presses
  `SquirrelTubeActionSO`, and the Goby does not bind that action. Its rival rows still read 25.8 rings a minute, 0%
  circling and 12% stuck, and the person won 2 of 2. The Goby joins the borrowed-policy item below.
- **Dog Fight (Sparrow I1, 1 flight, Will):** report only, because the Sparrow policy is switched off in Dog Fight.
- `promote_trained_drives.py --check` OK; `learn_drives_from_flights --check` OK.

## Waiting on

- **Brood Rush and Broadside are arena games the learner writes into** (2026-11-23, Yash's call on `ai_learn.py`,
  Garrett's on the plan): add them to `HOLD` beside Regatta, or say the committed `DolphinAIBoostPolicy_BroodRush`
  variant stays. Until then the sweep keeps no new Brood Rush or Broadside numbers.
- **The Manta learner writes a Soar band the AI cannot fly** (`BoostStickBand` 0.025 in Bloomrush, proposed again for
  Brood Rush, 2026-11-23): the person's band is measured on the person's stick, and the AI's stick is never that
  still. This is most likely why **Bloomrush's Manta AI never soars** (0.00 of 545 samples, 76 vs 6074 volume at I1,
  2026-11-20): diagnose with `/vessel-ai` in the Manta's Soar autopilot and the learner's Manta measure together;
  whoever owns the Manta AI.
- **Corona's Manta AI does not finish** (9 blooms left at Medium I1 when the person finished in 472 s, 2026-11-21, one
  match): look at it together with the Bloomrush item; more matches before anything is written.
- **Brood Rush's AI seats took 0 broods in both matches with a result** (2026-11-23, Medium and Legendary, I2): two
  matches, so watch; if it holds, it is a `massspace-arena` balance finding for Garrett, not a number to learn.
- **The Shrike, the Meerkat and the Goby carry a borrowed boost policy that cannot act** (2026-11-22; the Goby's `SquirrelAIBoostPolicy` in Perch Run added 2026-11-24): the Shrike's
  `SparrowAIBoostPolicy` presses `BoostActionSO` and the Meerkat's `StoatAIBoostPolicy` presses `SquirrelTubeActionSO`,
  and neither hull binds that action. Their AIs race on their trained controllers (the Shrike won Thornline), so this
  is a missing piece, not a broken race; whoever owns those hulls in the elemental loop gives each its own policy
  (or none) before a learner can measure it.
- **Rival velocity is not recorded for the Jacks and the Shrike** (0 u/s in every sample, 2026-11-22), so the rival
  report calls a winning AI stuck; a recorder fix in a normal session.
- **Headlong's Rhino AI (Garrett, the AI plan's promoted drive):** 4 of 40 rings in 146 s at Legendary I4 in the real
  game (2026-11-13); diagnose in the real game before the next round grows from this genome.
- **Hood and Jackstraw's AIs (Shombith):** report only while their controllers steer the AI.
- **Trained drives against races (Garrett, the AI plan):** when a promoted variant should learn from people again.
- The Breakwater rocket max range (about 679 u, past its 229 u reach) waits on whoever owns the Breakwater rocket.
- **No measure for `RampBoostAIPolicySO` yet:** Swordfish (Brood Rush 5, Spearline 4 races waiting) and Jacks (Decoy
  Run 1, Trawl 2).
- **To build:** a Tiger Moth learner (Tapestry, Waystation, Dustup, Sirocco), a Jewel Wasp learner (Gorge) and a
  Brittle Star measure (`BrittleStarSpeedModeAIPolicySO`, Flatline 4 races waiting).
