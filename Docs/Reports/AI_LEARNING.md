# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-11-04 22:20 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**24 new flight(s)** since the last learn.

## Dustup

New flights: TigerMoth 1.

**No learner reads these yet** (the sweep's to-do): TigerMoth.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Butterfly | I1 | 1 | 0 | - | - | 2026-10-23 13:58 |
| Butterfly | I2 | 3 | 0 | - | - | 2026-10-29 01:46 |
| Butterfly | I4 | 1 | 0 | - | 0 of 1 | 2026-10-28 22:11 |
| TigerMoth | I3 | 1 | 0 | - | - | 2026-11-03 16:13 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Regatta

New flights: Sparrow 12, Stoat 2.

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
| Sparrow | I1 | 36 | 1 | 64 s | 0 of 18 | 2026-11-03 11:39 |
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
stoat         13     23   18.5   165       0%     1%    6/13   4: 2, 7: 2, 3: 2, 1: 1
squirrel      43     21   14.3   148       0%     1%   12/43   3: 8, 1: 6, 2: 6, 5: 5
scarab         5     17   12.9   321       0%     2%    1/5    1: 4, 4: 1, 5: 1, 6: 1
serpent       18     16    9.8   157       1%     0%    1/18   6: 8, 1: 5, 0: 4, 7: 4
manta         71     14    8.8   251       0%     0%    5/71   3: 49, 1: 37, 4: 32, 5: 32
rhino         24     11    7.8   130       2%     1%    2/24   1: 16, 2: 15, 4: 15, 3: 9
dolphin       62     13    7.5    79       0%     9%    3/62   5: 61, 4: 48, 3: 45, 2: 36
sparrow       14     12    6.6    79       0%     6%    0/14   1: 14, 5: 13, 2: 11, 7: 10
```

## Switchback

New flights: Dolphin 9.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback.asset, Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback_I2.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 18 | 7 | 58 s | 2 of 9 | 2026-11-04 04:31 |
| Dolphin | I2 | 18 | 12 | 80 s | 7 of 8 | 2026-11-01 17:43 |
| Dolphin | I3 | 3 | 0 | - | 0 of 2 | 2026-10-29 07:13 |
| Dolphin | I4 | 18 | 4 | 106 s | 1 of 7 | 2026-11-04 15:41 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
dolphin      119     11    6.4    77       5%     1%   18/119  0: 116, 3: 36, 7: 31, 6: 28
```

## Files the learners changed

- `Docs/AI_SYSTEM/LEARNED_DRIVES_Switchback.md`

## What the sweep judged (2026-11-04 22:18 UTC)

- **Switchback (Dolphin), Blast Run, 9 new races, the first on a build with the AI Vessels training session's steps
  1-3** (`0f15257017`, `8963a920d4`, `71384beab9`): Yash's 8 were flown on `Ys-bleeding-edge` `a3a5a4aad6`, Garrett's on
  `bleeding-edge` `b9823ab119`; both contain step 3 (`git merge-base --is-ancestor`). The other one (owner, 15:41 IST)
  names no build. Seven have a result, every rival Legendary:

  | Race | I | Winner (AI) | AI rings per minute | Longest gap at one gate | Person |
  |---|---:|---|---|---|---|
  | 04:00 | 4 | 26 rings, 184 s | 9.1 / 7.4 | 12 s / 15 s | 9 rings |
  | 04:04 | 4 | 26 rings, 202 s | 8.3 / 7.6 | 17 s / 16 s | 9 rings |
  | 04:07 | 4 | 26 rings, 180 s | 9.3 / 6.4 | 11 s / 20 s | 9 rings |
  | 04:14 | 1 | 26 rings, 73 s | 22.7 / 18.3 | 8 s / 9 s | 9 rings |
  | 04:26 | 1 | 26 rings, 77 s | 21.6 / 14.1 | 9 s / 8 s | 14 rings |
  | 04:31 | 1 | 26 rings, 84 s | 19.9 / 12.2 | 11 s / 10 s | 12 rings |
  | 15:41 (owner) | 4 | 26 rings, 142 s | 12.0 / 10.2 | 12 s / 10 s | 16 rings |

  **The blocked-gate stall is closed as measured**: on the old build the AIs threaded 10-24 rings in about 200 s at I2
  (about 3.5 a minute) and stalled at gates 4, 8 and 9; now a Legendary Dolphin finishes all 26 at I4 in 142-202 s
  (7-12 a minute) and no AI waits more than 20 s at any gate. An AI won every race. The learned numbers did not change:
  the person finished none of these races (the learner writes only from 3 finished races per cell).
- **Regatta** (Sparrow 12, Stoat 2): held. **Dustup** (Tiger Moth 1): no Tiger Moth learner.

## Waiting on

- **Slingshot learner and the trampoline (Yash):** the learner has no way to drop races flown on a retired kit, so the
  next new Slingshot race will propose the orbit numbers again (`StoatSlingConfig_Slingshot.asset`, 2026-11-03 above).
  Either the learner learns only from trampoline races, or Yash says the orbit numbers are fine to keep.
- The Breakwater rocket max range (about 679 u, past its 229 u reach) waits on whoever owns the Breakwater rocket.
- A Tiger Moth learner (Tapestry, Waystation, Dustup) is still to build.
