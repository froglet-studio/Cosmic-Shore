# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-11-22 22:20 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**18 new flight(s)** since the last learn.

## Bloomrush

New flights: Manta 5.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/MantaSoarAITuning_Bloomrush.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Manta | I1 | 11 | 0 | - | 1 of 1 | 2026-11-22 21:55 |
| Manta | I3 | 2 | 0 | - | - | 2026-10-26 04:52 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## BurrowRun

New flights: Meerkat 1.

- **Meerkat: measured, not written** - its boost policy StoatAIBoostPolicy (SkimRingAIPolicySO) presses SquirrelTubeActionSO, which this hull does not bind - the drive cannot act.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Meerkat | I1 | 2 | 0 | - | - | 2026-11-22 17:04 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
meerkat        2     10    8.4    72       0%     2%    0/2    1: 3, 0: 2
```

## DecoyRun

New flights: Jacks 1.

- **Jacks: measured, not written** - no measure for RampBoostAIPolicySO yet (its policy RhinoAIBoostPolicy).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Jacks | I1 | 1 | 0 | - | 0 of 1 | 2026-11-22 17:01 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
jacks          2     12   19.0     0       0%    67%    1/2
```

## Flatline

New flights: BrittleStar 1.

- **BrittleStar: measured, not written** - no measure for BrittleStarSpeedModeAIPolicySO yet (its policy BrittleStarAIBoostPolicy).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| BrittleStar | I1 | 1 | 0 | - | - | 2026-11-21 01:58 |
| BrittleStar | I2 | 1 | 0 | - | 1 of 1 | 2026-11-10 05:23 |
| BrittleStar | I4 | 2 | 0 | - | 1 of 2 | 2026-11-10 06:16 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## SkimRace

New flights: Squirrel 1.

- `learn_from_flights --mode SkimRace --write`: OK. wrote Docs/AI_SYSTEM/LEARNED_FROM_FLIGHTS_SkimRace.md

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Squirrel | I1 | 14 | 0 | - | 0 of 6 | 2026-11-22 07:40 |
| Squirrel | I2 | 15 | 0 | - | 0 of 7 | 2026-11-17 02:02 |
| Squirrel | I3 | 1 | 0 | - | 0 of 1 | 2026-10-26 06:31 |
| Squirrel | I4 | 1 | 0 | - | - | 2026-10-31 17:16 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
squirrel      67      0    0.0   178       2%     2%    0/67   0: 65
```

## Spearline

New flights: Swordfish 3.

- **Swordfish: measured, not written** - no measure for RampBoostAIPolicySO yet (its policy SwordfishAIBoostPolicy).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Swordfish | I1 | 4 | 0 | - | - | 2026-11-22 07:46 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
swordfish      9      2    6.2   123       0%     1%    0/9    0: 10, 4: 2, 2: 1, 1: 1
```

## Switchback

New flights: Dolphin 1.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 28 | 8 | 58 s | 8 of 17 | 2026-11-20 19:54 |
| Dolphin | I2 | 19 | 12 | 80 s | 7 of 9 | 2026-11-16 21:10 |
| Dolphin | I3 | 3 | 0 | - | 0 of 2 | 2026-10-29 07:13 |
| Dolphin | I4 | 20 | 4 | 106 s | 1 of 9 | 2026-11-04 15:41 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
dolphin      137     14    7.1    79       4%     2%   28/137  0: 131, 3: 38, 7: 32, 6: 31
```

## Thornline

New flights: Shrike 1.

- **Shrike: measured, not written** - its boost policy SparrowAIBoostPolicy (HoldBoostAIPolicySO) presses BoostActionSO, which this hull does not bind - the drive cannot act.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Shrike | I1 | 1 | 0 | - | 0 of 1 | 2026-11-22 17:02 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
shrike         2     12   17.4     0       2%    62%    1/2    5: 1, 6: 1
```

## ThresherTime

New flights: Thresher 2.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Thresher | I1 | 13 | 5 | 65 s | 4 of 7 | 2026-11-22 07:47 |
| Thresher | I2 | 8 | 2 | 106 s | 3 of 7 | 2026-11-19 02:36 |
| Thresher | I4 | 3 | 1 | 120 s | 1 of 3 | 2026-10-31 23:26 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
thresher      43     10    5.2   133       1%     0%    9/43   0: 41, 1: 33, 2: 30, 3: 26
```

## Trawl

New flights: Jacks 2.

- **Jacks: measured, not written** - no measure for RampBoostAIPolicySO yet (its policy RhinoAIBoostPolicy).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Jacks | I1 | 2 | 0 | - | - | 2026-11-22 17:08 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Files the learners changed

- `Docs/AI_SYSTEM/LEARNED_DRIVES_Bloomrush.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Switchback.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_ThresherTime.md`
- `Docs/AI_SYSTEM/LEARNED_FROM_FLIGHTS_SkimRace.md`

## What the sweep judged (2026-11-22 22:20 UTC)

18 new flights since the 2026-11-21 sweep, 17 Yash's on `Ys-bleeding-edge` and 1 on `bleeding-edge`. No asset under
`Assets/Resources/AIBoostPolicies` changed; only the learners' reports did. Two races had a result, and the AI won both.

- **Decoy Run (Jacks I1, Legendary rivals, 412355e425):** the AI (Cassian) finished in 36.6 s with Yash 4 gates short
  and the second AI 1 short. Nothing to fix. Report only: no measure for `RampBoostAIPolicySO` yet.
- **Thornline (Shrike I1, Legendary rivals, 412355e425):** the AI (WillOfFire) finished in 39.6 s with Yash 11 gates
  short (5 of 16) and the second AI 9 short. The winner is fine; the learner says the Shrike's boost policy
  (`SparrowAIBoostPolicy`) presses `BoostActionSO`, which the hull does not bind: under "Waiting on".
- **Burrow Run (Meerkat I1, Legendary rivals, 412355e425):** Yash left at 71 s with 8 of 16, no result. The two AI
  seats threaded 10 rings at 8.4 a minute and never circled, so they fly the course; one unfinished race says no more.
  Same policy finding as the Shrike (`StoatAIBoostPolicy` presses `SquirrelTubeActionSO`): under "Waiting on".
- **Switchback (Dolphin I1, 7788ad036d, 135 s):** a two-seat Jade match recorded as won at 127.3 s, the recorded seat
  16 of 26 rings; not a finished flight, so the Dolphin variant was not rewritten ("nothing").
- **Thresher Time (Thresher I1, e2a0bb1783), 2 flights of 46 s and 32 s:** left early, no result; the variant did not
  move (24 flights, 9 finished).
- **Bloomrush (Manta I1), 5 flights of 10 to 25 s:** all left in seconds, no result, and 4 of them name no build.
  Nothing learned (13 flights, still 7 finished) and nothing new on the open "never soars" item.
- **Skim Race (Squirrel I1, 27 s)**, **Spearline (Swordfish I1, 3 flights of 19 to 36 s)**, **Trawl (Jacks I1, 2
  flights)** and **Flatline (Brittle Star I1, 20 s, bleeding-edge 9b57d5606f)**: left early, no result; reported only.
- The rival table's Jacks and Shrike rows read 0 u/s and over 60% stuck although one of each finished first: their
  recorded rival velocity is zero, so those two columns say nothing for these hulls yet.
- `promote_trained_drives.py --check` OK; no promoted trained drive was touched.

## Waiting on

- **Bloomrush's Manta AI never soars** (0.00 of 545 samples, 76 vs 6074 volume at I1, 2026-11-20): diagnose with
  `/vessel-ai` in the Manta's Soar autopilot before the learned numbers mean anything; whoever owns the Manta AI.
- **Corona's Manta AI does not finish** (9 blooms left at Medium I1 when the person finished in 472 s, 2026-11-21, one
  match): look at it together with the Bloomrush item; more matches before anything is written.
- **The Shrike and the Meerkat carry a borrowed boost policy that cannot act** (2026-11-22): the Shrike's
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
