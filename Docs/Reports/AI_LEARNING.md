# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-11-15 22:20 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**18 new flight(s)** since the last learn.

## Broadside

New flights: Manta 4.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I2 | 2 | 0 | - | - | 2026-11-13 15:51 |
| Manta | I1 | 2 | 0 | - | - | 2026-10-23 14:16 |
| Manta | I2 | 5 | 0 | - | - | 2026-11-15 02:36 |
| Sparrow | I2 | 3 | 0 | - | - | 2026-11-13 20:25 |
| Squirrel | I2 | 1 | 0 | - | - | 2026-11-10 06:22 |
| Urchin | I2 | 2 | 0 | - | - | 2026-11-13 20:13 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## BroodRush

New flights: Dolphin 2, Sparrow 2.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I2 | 3 | 0 | - | 0 of 1 | 2026-11-15 02:29 |
| Manta | I1 | 3 | 0 | - | 1 of 1 | 2026-11-02 16:18 |
| Manta | I2 | 1 | 0 | - | 0 of 1 | 2026-11-13 12:44 |
| Rhino | I1 | 1 | 0 | - | 1 of 1 | 2026-11-02 16:21 |
| Sparrow | I2 | 2 | 0 | - | 1 of 2 | 2026-11-14 21:35 |
| Swordfish | I2 | 4 | 0 | - | 0 of 1 | 2026-11-12 21:58 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## GrizzlyTime

New flights: Grizzly 3.

- **Grizzly: measured, not written** - its AI bombs through GrizzlyTriggerBombConfigSO's autopilot fields, which do not load per mode, and jtgjones's grizzly-v3 is reworking the bomb triggers (15 unmerged commits): learn it once that lands.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Grizzly | I1 | 1 | 0 | - | - | 2026-10-26 06:27 |
| Grizzly | I4 | 12 | 5 | 106 s | 5 of 10 | 2026-11-12 21:52 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
grizzly       14     13    4.7    51       2%     2%    5/14   0: 21, 1: 17, 2: 14, 6: 9
```

## Regatta

New flights: Dolphin 3.

**On hold, not written:** the AIs train in their own minigames; Regatta is rebuilt from those skills later (Yash, 2026-10-27).


The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 3 | 0 | - | 0 of 1 | 2026-11-03 11:44 |
| Dolphin | I2 | 5 | 0 | - | 0 of 2 | 2026-11-15 02:21 |
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
urchin        67     24   23.3   300       0%     3%   39/67   2: 5, 3: 5, 1: 4, 4: 4
squirrel      61     17   13.0   148       0%     1%   13/61   2: 10, 3: 8, 1: 7, 0: 5
scarab        13     23   12.9   323       1%     1%    6/13   1: 11, 7: 9, 6: 9, 0: 7
stoat         26     19   11.9   138       1%     1%    8/26   7: 15, 6: 15, 3: 14, 4: 13
serpent       29     15   10.7   157       1%     0%    2/29   6: 11, 7: 8, 0: 7, 3: 6
manta         95     13    9.1   240       0%     1%    7/95   3: 53, 1: 38, 4: 34, 2: 33
rhino         55     10    7.8   113       1%     1%    6/55   2: 29, 4: 29, 5: 29, 1: 27
dolphin       80     13    7.8    89       0%     8%    4/80   5: 62, 4: 48, 3: 46, 2: 37
sparrow       16     12    6.7    83       0%     5%    1/16   1: 14, 5: 13, 2: 11, 4: 10
```

## SlipLine

New flights: Honeyguide 2.

- **Honeyguide: measured, not written** - its boost policy SparrowAIBoostPolicy (HoldBoostAIPolicySO) presses BoostActionSO, which this hull does not bind - the drive cannot act.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Honeyguide | I1 | 3 | 1 | 156 s | 1 of 1 | 2026-11-15 02:25 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
honeyguide     3      2    6.1    36      14%     4%    0/3    1: 3, 3: 1, 8: 1, 9: 1
```

## Switchback

New flights: Dolphin 1.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 19 | 7 | 58 s | 2 of 10 | 2026-11-15 03:44 |
| Dolphin | I2 | 18 | 12 | 80 s | 7 of 8 | 2026-11-01 17:43 |
| Dolphin | I3 | 3 | 0 | - | 0 of 2 | 2026-10-29 07:13 |
| Dolphin | I4 | 20 | 4 | 106 s | 1 of 9 | 2026-11-04 15:41 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
dolphin      124     12    6.8    77       5%     1%   21/124  0: 121, 3: 36, 7: 31, 6: 28
```

## WildlifeLiberation

New flights: Sparrow 1.

- **Sparrow: measured, not written** - SparrowAIBoostPolicy is switched off in WildlifeLiberation (disabledInModes).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Sparrow | I3 | 2 | 0 | - | 1 of 1 | 2026-11-15 02:05 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Files the learners changed

- `Docs/AI_SYSTEM/LEARNED_DRIVES_Broadside.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_BroodRush.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Switchback.md`

## What the sweep judged (2026-11-15 22:17 UTC)

- **Switchback (Dolphin), 1 new I1 race (Yash, Ys-bleeding-edge 4b414df525, which carries the promoted trained drive
  c39317a6b6):** the Medium AI won, all 26 rings in 119.7 s (14.3 a minute, no gate wait over 10 s); Yash had 5 gates
  left. Nothing written (unfinished for the person).
- **Brood Rush, 2 new I2 races with the Sparrow (owner):** an AI won both, 3 broods to the person's 0. Two Dolphin races
  (Garrett) and four Broadside Manta races (Garrett) were left before the end; nothing to judge.
- Reported only: Grizzly Time (3), Slip Line (Honeyguide 2), Wildlife Liberation (Sparrow 1). Regatta (3): held.
- No AI numbers changed; the promoted trained drives are untouched (`promote_trained_drives.py --check` OK).

## Waiting on

- **Headlong's Rhino AI (Garrett, the AI plan's promoted drive):** 4 of 40 rings in 146 s at Legendary I4 in the real
  game (2026-11-13); diagnose in the real game before the next round grows from this genome.
- **Hood and Jackstraw's AIs (Shombith):** report only while their controllers steer the AI.
- **Trained drives against races (Garrett, the AI plan):** when a promoted variant should learn from people again.
- The Breakwater rocket max range (about 679 u, past its 229 u reach) waits on whoever owns the Breakwater rocket.
- A Tiger Moth learner (Tapestry, Waystation, Dustup) is still to build.
