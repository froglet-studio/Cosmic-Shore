# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-11-05 22:19 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**9 new flight(s)** since the last learn.

## Rampage

New flights: Dolphin 1.

- **Dolphin: measured, not written** - DolphinAIBoostPolicy is switched off in Rampage (disabledInModes).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 1 | 0 | - | - | 2026-11-05 09:38 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Regatta

New flights: Sparrow 7.

**On hold, not written:** the AIs train in their own minigames; Regatta is rebuilt from those skills later (Yash, 2026-10-27).


The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 3 | 0 | - | 0 of 1 | 2026-11-03 11:44 |
| Dolphin | I3 | 1 | 0 | - | 0 of 1 | 2026-10-30 20:08 |
| Manta | I1 | 6 | 5 | 29 s | - | 2026-10-20 17:33 |
| Manta | I3 | 2 | 1 | 51 s | - | 2026-10-20 17:48 |
| Manta | I4 | 5 | 3 | 75 s | - | 2026-10-20 17:45 |
| Rhino | I1 | 1 | 1 | 36 s | - | 2026-10-20 17:54 |
| Rhino | I3 | 5 | 1 | 61 s | - | 2026-10-20 18:02 |
| Scarab | I2 | 3 | 0 | - | - | 2026-10-25 21:56 |
| Serpent | I3 | 2 | 1 | 164 s | - | 2026-10-17 04:54 |
| Sparrow | I1 | 43 | 1 | 64 s | 0 of 21 | 2026-11-03 11:39 |
| Sparrow | I3 | 1 | 0 | - | - | 2026-10-23 00:14 |
| Sparrow | I4 | 2 | 0 | - | - | 2026-10-23 00:10 |
| Squirrel | I1 | 40 | 2 | 104 s | 0 of 1 | 2026-11-03 11:48 |
| Squirrel | I3 | 11 | 4 | 117 s | - | 2026-10-17 01:44 |
| Stoat | I1 | 2 | 0 | - | 0 of 1 | 2026-11-03 11:31 |
| Stoat | I2 | 2 | 0 | - | - | 2026-10-26 00:13 |
| Stoat | I3 | 1 | 0 | - | 1 of 1 | 2026-10-31 05:38 |
| Unknown | I1 | 4 | 0 | - | - | 2026-10-16 17:39 |
| Urchin | I1 | 2 | 2 | 55 s | - | 2026-10-17 03:18 |
| Urchin | I3 | 5 | 2 | 76 s | 1 of 1 | 2026-10-30 17:08 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
urchin        38     24   22.8   301       0%     4%   19/38   1: 3, 4: 3, 5: 3, 2: 2
stoat         14     23   17.0   164       0%     1%    6/14   4: 5, 7: 5, 3: 5, 5: 3
squirrel      43     21   14.3   148       0%     1%   12/43   3: 8, 1: 6, 2: 6, 5: 5
scarab         9     23   13.0   321       0%     1%    3/9    1: 5, 6: 5, 7: 4, 0: 4
serpent       18     16    9.8   157       1%     0%    1/18   6: 8, 1: 5, 0: 4, 7: 4
manta         71     14    8.8   251       0%     0%    5/71   3: 49, 1: 37, 4: 32, 5: 32
rhino         26     11    7.8   128       2%     1%    3/26   1: 20, 2: 18, 4: 16, 3: 11
dolphin       62     13    7.5    79       0%     9%    3/62   5: 61, 4: 48, 3: 45, 2: 36
sparrow       14     12    6.6    79       0%     6%    0/14   1: 14, 5: 13, 2: 11, 7: 10
```

## SkimRace

New flights: Squirrel 1.

- `learn_from_flights --mode SkimRace --write`: OK. wrote Docs/AI_SYSTEM/LEARNED_FROM_FLIGHTS_SkimRace.md

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Squirrel | I1 | 9 | 0 | - | 0 of 3 | 2026-11-02 16:04 |
| Squirrel | I2 | 13 | 0 | - | 0 of 5 | 2026-11-05 10:36 |
| Squirrel | I3 | 1 | 0 | - | 0 of 1 | 2026-10-26 06:31 |
| Squirrel | I4 | 1 | 0 | - | - | 2026-10-31 17:16 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
squirrel      51      0    0.0   158       2%     2%    0/51   0: 49
```

## Files the learners changed

- `Docs/AI_SYSTEM/LEARNED_FROM_FLIGHTS_SkimRace.md`

## What the sweep judged (2026-11-05 22:17 UTC)

- **Skim Race (Squirrel), 1 new I2 race (Caleb, 10:36 IST):** no result (left before the end); Skim Race is report
  only. Nothing to judge.
- **Regatta** (Sparrow 7): held. **Rampage** (Dolphin 1): reported only.
- No AI numbers changed.

## Waiting on

- **Slingshot learner and the trampoline (Yash):** the learner has no way to drop races flown on a retired kit, so the
  next new Slingshot race will propose the orbit numbers again (`StoatSlingConfig_Slingshot.asset`, 2026-11-03).
  Either the learner learns only from trampoline races, or Yash says the orbit numbers are fine to keep.
- The Breakwater rocket max range (about 679 u, past its 229 u reach) waits on whoever owns the Breakwater rocket.
- A Tiger Moth learner (Tapestry, Waystation, Dustup) is still to build.
