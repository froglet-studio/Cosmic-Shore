# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-11-06 22:20 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**46 new flight(s)** since the last learn.

## Regatta

New flights: Dolphin 4, Manta 5, Sparrow 19, Squirrel 6, Stoat 9.

**On hold, not written:** the AIs train in their own minigames; Regatta is rebuilt from those skills later (Yash, 2026-10-27).


The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 3 | 0 | - | 0 of 1 | 2026-11-03 11:44 |
| Dolphin | I3 | 1 | 0 | - | 0 of 1 | 2026-10-30 20:08 |
| Dolphin | I4 | 4 | 0 | - | 0 of 4 | 2026-11-06 15:10 |
| Manta | I1 | 8 | 5 | 29 s | 0 of 1 | 2026-11-03 10:32 |
| Manta | I2 | 3 | 1 | 55 s | 1 of 3 | 2026-11-06 05:31 |
| Manta | I3 | 2 | 1 | 51 s | - | 2026-10-20 17:48 |
| Manta | I4 | 5 | 3 | 75 s | - | 2026-10-20 17:45 |
| Rhino | I1 | 1 | 1 | 36 s | - | 2026-10-20 17:54 |
| Rhino | I3 | 5 | 1 | 61 s | - | 2026-10-20 18:02 |
| Scarab | I2 | 3 | 0 | - | - | 2026-10-25 21:56 |
| Serpent | I3 | 2 | 1 | 164 s | - | 2026-10-17 04:54 |
| Sparrow | I1 | 62 | 1 | 64 s | 0 of 32 | 2026-11-03 11:39 |
| Sparrow | I3 | 1 | 0 | - | - | 2026-10-23 00:14 |
| Sparrow | I4 | 2 | 0 | - | - | 2026-10-23 00:10 |
| Squirrel | I1 | 40 | 2 | 104 s | 0 of 1 | 2026-11-03 11:48 |
| Squirrel | I2 | 2 | 0 | - | 0 of 2 | 2026-11-05 16:00 |
| Squirrel | I3 | 11 | 4 | 117 s | - | 2026-10-17 01:44 |
| Squirrel | I4 | 4 | 1 | 152 s | 0 of 4 | 2026-11-06 15:44 |
| Stoat | I1 | 2 | 0 | - | 0 of 1 | 2026-11-03 11:31 |
| Stoat | I2 | 9 | 0 | - | 1 of 7 | 2026-11-06 05:37 |
| Stoat | I3 | 1 | 0 | - | 1 of 1 | 2026-10-31 05:38 |
| Stoat | I4 | 2 | 0 | - | 0 of 2 | 2026-11-06 05:19 |
| Unknown | I1 | 4 | 0 | - | - | 2026-10-16 17:39 |
| Urchin | I1 | 2 | 2 | 55 s | - | 2026-10-17 03:18 |
| Urchin | I3 | 5 | 2 | 76 s | 1 of 1 | 2026-10-30 17:08 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
urchin        62     24   23.4   300       0%     3%   39/62   2: 5, 3: 5, 1: 4, 4: 4
squirrel      56     19   13.7   153       0%     1%   13/56   2: 9, 3: 8, 1: 6, 0: 5
scarab        13     23   12.9   323       1%     1%    6/13   1: 11, 7: 9, 6: 9, 0: 7
stoat         26     19   11.9   138       1%     1%    8/26   7: 15, 6: 15, 3: 14, 4: 13
serpent       24     16    9.8   158       1%     0%    2/24   6: 10, 7: 7, 3: 6, 0: 6
manta         90     14    9.0   243       0%     1%    7/90   3: 53, 1: 38, 4: 34, 2: 33
dolphin       80     13    7.8    89       0%     8%    4/80   5: 62, 4: 48, 3: 46, 2: 37
rhino         50     10    7.7   106       1%     1%    4/50   2: 29, 4: 29, 5: 29, 1: 27
sparrow       16     12    6.7    83       0%     5%    1/16   1: 14, 5: 13, 2: 11, 4: 10
```

## Slingshot

New flights: Stoat 1.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/StoatSlingConfig_Slingshot.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Stoat | I1 | 1 | 0 | - | 0 of 1 | 2026-10-31 13:13 |
| Stoat | I2 | 15 | 1 | 75 s | 1 of 15 | 2026-11-03 04:33 |
| Stoat | I3 | 18 | 1 | 90 s | 1 of 16 | 2026-11-07 02:08 |
| Stoat | I4 | 1 | 0 | - | 0 of 1 | 2026-11-02 17:50 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
stoat         68     15    9.4   102       0%     0%   31/68   0: 71, 1: 25, 5: 25, 2: 20
```

## ThresherTime

New flights: Thresher 2.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/ThresherAIBoostPolicy_ThresherTime.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Thresher | I1 | 10 | 5 | 65 s | 4 of 7 | 2026-11-04 04:59 |
| Thresher | I2 | 4 | 1 | 106 s | 2 of 4 | 2026-11-07 01:56 |
| Thresher | I4 | 3 | 1 | 120 s | 1 of 3 | 2026-10-31 23:26 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
thresher      29     11    5.7   120       1%     0%    7/29   0: 27, 1: 24, 3: 19, 2: 17
```

## Files the learners changed

- `Assets/Resources/AIBoostPolicies/StoatSlingConfig_Slingshot.asset` and `Docs/AI_SYSTEM/LEARNED_DRIVES_Slingshot.md`: proposed, NOT committed (see below)
- `Docs/AI_SYSTEM/LEARNED_DRIVES_ThresherTime.md`

## What the sweep judged (2026-11-06 22:17 UTC)

- **Slingshot (Stoat), 1 new race (Yash, 2026-11-07 02:08 IST, unfinished):** the learner proposed the orbit numbers
  again (`aiSlingMinTurnDegrees` 50 -> 10.1 and three more), learned from the same orbit-sling races as on 2026-11-03.
  **Not committed:** Yash re-tuned the trampoline since (`aiSlingMinTurnDegrees` 50; the shell halved to 30 u in
  `c459f4423f`), and no finished race has been flown on the trampoline yet.
- **Thresher Time (Thresher), 2 new races (Yash, I1 and I2):** neither finished; nothing written.
- **Regatta** (43 races across five hulls): held.

## Waiting on

- **Slingshot learner and the trampoline (Yash):** the learner keeps proposing orbit-sling numbers every time a new
  Slingshot race arrives, and the sweep keeps holding them back. Either the learner learns only from trampoline races
  (a cut-off like Switchback's course family), or Yash says the orbit numbers are fine.
- The Breakwater rocket max range (about 679 u, past its 229 u reach) waits on whoever owns the Breakwater rocket.
- A Tiger Moth learner (Tapestry, Waystation, Dustup) is still to build.
