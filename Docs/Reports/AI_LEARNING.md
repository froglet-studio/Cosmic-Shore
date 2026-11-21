# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-11-16 22:21 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**12 new flight(s)** since the last learn.

## AstroLeague

New flights: Swordfish 3.

- **Swordfish: measured, not written** - no measure for RampBoostAIPolicySO yet (its policy RhinoAIBoostPolicy).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Scarab | I1 | 1 | 0 | - | 1 of 1 | 2026-11-02 16:49 |
| Swordfish | I2 | 3 | 0 | - | 1 of 2 | 2026-11-15 23:49 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Broadside

New flights: Manta 2.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I2 | 2 | 0 | - | - | 2026-11-13 15:51 |
| Manta | I1 | 2 | 0 | - | - | 2026-10-23 14:16 |
| Manta | I2 | 7 | 0 | - | - | 2026-11-15 02:36 |
| Sparrow | I2 | 3 | 0 | - | - | 2026-11-13 20:25 |
| Squirrel | I2 | 1 | 0 | - | - | 2026-11-10 06:22 |
| Urchin | I2 | 2 | 0 | - | - | 2026-11-13 20:13 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## BroodRush

New flights: Dolphin 2.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I2 | 5 | 0 | - | 0 of 1 | 2026-11-15 02:29 |
| Manta | I1 | 3 | 0 | - | 1 of 1 | 2026-11-02 16:18 |
| Manta | I2 | 1 | 0 | - | 0 of 1 | 2026-11-13 12:44 |
| Rhino | I1 | 1 | 0 | - | 1 of 1 | 2026-11-02 16:21 |
| Sparrow | I2 | 2 | 0 | - | 1 of 2 | 2026-11-14 21:35 |
| Swordfish | I2 | 4 | 0 | - | 0 of 1 | 2026-11-12 21:58 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## ScarabRally

New flights: Scarab 2.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Scarab | I1 | 7 | 2 | 62 s | 2 of 3 | 2026-11-15 01:56 |
| Scarab | I4 | 1 | 0 | - | 0 of 1 | 2026-11-13 20:29 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
scarab         8     12    7.2   220       0%     0%    2/8    5: 6, 8: 6, 3: 5, 11: 2
```

## SkimRace

New flights: Squirrel 1.

- `learn_from_flights --mode SkimRace --write`: OK. wrote Docs/AI_SYSTEM/LEARNED_FROM_FLIGHTS_SkimRace.md

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Squirrel | I1 | 10 | 0 | - | 0 of 4 | 2026-11-02 16:04 |
| Squirrel | I2 | 15 | 0 | - | 0 of 7 | 2026-11-17 02:02 |
| Squirrel | I3 | 1 | 0 | - | 0 of 1 | 2026-10-26 06:31 |
| Squirrel | I4 | 1 | 0 | - | - | 2026-10-31 17:16 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
squirrel      59      0    0.0   163       2%     2%    0/59   0: 57
```

## Switchback

New flights: Dolphin 1.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 19 | 7 | 58 s | 2 of 10 | 2026-11-15 03:44 |
| Dolphin | I2 | 19 | 12 | 80 s | 7 of 9 | 2026-11-16 21:10 |
| Dolphin | I3 | 3 | 0 | - | 0 of 2 | 2026-10-29 07:13 |
| Dolphin | I4 | 20 | 4 | 106 s | 1 of 9 | 2026-11-04 15:41 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
dolphin      127     13    6.9    77       4%     2%   22/127  0: 124, 3: 38, 7: 31, 6: 30
```

## WildlifeLiberation

New flights: Sparrow 1.

- **Sparrow: measured, not written** - SparrowAIBoostPolicy is switched off in WildlifeLiberation (disabledInModes).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Sparrow | I3 | 3 | 0 | - | 1 of 1 | 2026-11-15 02:05 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Files the learners changed

- `Docs/AI_SYSTEM/LEARNED_DRIVES_Broadside.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_BroodRush.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_ScarabRally.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Switchback.md`
- `Docs/AI_SYSTEM/LEARNED_FROM_FLIGHTS_SkimRace.md`

## What the sweep judged (2026-11-16 22:17 UTC)

- **Switchback (Dolphin), 1 new I2 race (Yash, Ys-bleeding-edge f80a064aad):** the AI won all 26 rings in 144.5 s; Yash
  had 26 gates left. **Skim Race (Squirrel), 1 new I2 race (Yash):** the AI won in 93.6 s. Nothing written (unfinished
  for the person).
- **Scarab Rally (2), Brood Rush (Dolphin 2), Broadside (Manta 2), all Garrett's:** left before the end; nothing to judge.
- Reported only: Astro League (Swordfish 3), Wildlife Liberation (Sparrow 1).
- No AI numbers changed; the promoted trained drives are untouched (`promote_trained_drives.py --check` OK).

## Waiting on

- **Headlong's Rhino AI (Garrett, the AI plan's promoted drive):** 4 of 40 rings in 146 s at Legendary I4 in the real
  game (2026-11-13); diagnose in the real game before the next round grows from this genome.
- **Hood and Jackstraw's AIs (Shombith):** report only while their controllers steer the AI.
- **Trained drives against races (Garrett, the AI plan):** when a promoted variant should learn from people again.
- The Breakwater rocket max range (about 679 u, past its 229 u reach) waits on whoever owns the Breakwater rocket.
- A Tiger Moth learner (Tapestry, Waystation, Dustup) is still to build.
