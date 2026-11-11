# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-11-10 22:24 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**4 new flight(s)** since the last learn.

## Flatline

New flights: BrittleStar 1.

- **BrittleStar: measured, not written** - no measure for BrittleStarSpeedModeAIPolicySO yet (its policy BrittleStarAIBoostPolicy).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| BrittleStar | I2 | 1 | 0 | - | 1 of 1 | 2026-11-10 05:23 |
| BrittleStar | I4 | 2 | 0 | - | 1 of 2 | 2026-11-10 06:16 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Harvest

New flights: Thresher 1.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Thresher | I4 | 1 | 0 | - | - | 2026-11-11 02:52 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Rampage

New flights: Dolphin 1.

- **Dolphin: measured, not written** - DolphinAIBoostPolicy is switched off in Rampage (disabledInModes).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 1 | 0 | - | - | 2026-11-05 09:38 |
| Dolphin | I3 | 1 | 0 | - | 0 of 1 | 2026-11-11 03:04 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Shatterplane

New flights: BrittleStar 1.

- **BrittleStar: measured, not written** - no measure for BrittleStarSpeedModeAIPolicySO yet (its policy BrittleStarAIBoostPolicy).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| BrittleStar | I4 | 1 | 0 | - | 1 of 1 | 2026-11-11 02:55 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Files the learners changed

- `Docs/AI_SYSTEM/LEARNED_DRIVES_Harvest.md`

## What the sweep judged (2026-11-10 22:22 UTC)

- **Harvest (Thresher), 1 new I4 race (Shombith, the first in this mode):** left before the end at 200 s (no
  result); nothing written below 3 finished races (`LEARNED_DRIVES_Harvest.md`).
- Reported only, no learner: Flatline and Shatterplane (Brittle Star, 1 each), Rampage (Dolphin 1).
- No AI numbers changed; the promoted trained drives are untouched (`promote_trained_drives.py --check` OK).

## Waiting on

- **Serpent Gulag's AI (Shombith):** report only until the learner measures against the post the marksman steers to.
- **Trained drives against races (Garrett, the AI plan):** when a promoted variant should learn from people again.
- The Breakwater rocket max range (about 679 u, past its 229 u reach) waits on whoever owns the Breakwater rocket.
- A Tiger Moth learner (Tapestry, Waystation, Dustup) is still to build.
