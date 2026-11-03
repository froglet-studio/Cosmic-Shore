# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-11-03 22:21 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**48 new flight(s)** since the last learn.

## AstroLeague

New flights: Scarab 1.

- **Scarab: measured, not written** - ScarabAIBoostPolicy is switched off in AstroLeague (disabledInModes).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Scarab | I1 | 1 | 0 | - | 1 of 1 | 2026-11-02 16:49 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Regatta

New flights: Dolphin 2, Sparrow 11, Squirrel 2.

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
| Sparrow | I1 | 24 | 1 | 64 s | 0 of 12 | 2026-11-03 11:39 |
| Sparrow | I3 | 1 | 0 | - | - | 2026-10-23 00:14 |
| Sparrow | I4 | 2 | 0 | - | - | 2026-10-23 00:10 |
| Squirrel | I1 | 40 | 2 | 104 s | 0 of 1 | 2026-11-03 11:48 |
| Squirrel | I3 | 11 | 4 | 117 s | - | 2026-10-17 01:44 |
| Stoat | I2 | 2 | 0 | - | - | 2026-10-26 00:13 |
| Stoat | I3 | 1 | 0 | - | 1 of 1 | 2026-10-31 05:38 |
| Unknown | I1 | 4 | 0 | - | - | 2026-10-16 17:39 |
| Urchin | I1 | 2 | 2 | 55 s | - | 2026-10-17 03:18 |
| Urchin | I3 | 5 | 2 | 76 s | 1 of 1 | 2026-10-30 17:08 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
urchin        32     24   23.1   301       0%     5%   17/32   1: 3, 4: 3, 5: 3, 2: 2
stoat         11     23   15.5   163       0%     1%    5/11   4: 2, 7: 2, 3: 2, 1: 1
squirrel      39     21   13.8   148       0%     1%   11/39   3: 8, 1: 5, 2: 5, 5: 5
serpent       17     17    9.7   157       1%     0%    1/17   6: 7, 1: 5, 0: 4, 7: 4
scarab         4     11    9.4   274       0%     3%    0/4    1: 3, 4: 1, 5: 1
manta         69     14    8.6   252       0%     0%    4/69   3: 49, 1: 37, 4: 32, 5: 32
rhino         22     11    7.7   130       2%     1%    2/22   1: 16, 4: 15, 2: 14, 3: 9
dolphin       58     13    7.2    78       0%    10%    2/58   5: 61, 4: 48, 3: 45, 2: 36
sparrow       14     12    6.6    79       0%     6%    0/14   1: 14, 5: 13, 2: 11, 7: 10
```

## Slingshot

New flights: Stoat 30.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/StoatSlingConfig_Slingshot.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Stoat | I1 | 1 | 0 | - | 0 of 1 | 2026-10-31 13:13 |
| Stoat | I2 | 15 | 1 | 75 s | 1 of 15 | 2026-11-03 04:33 |
| Stoat | I3 | 17 | 1 | 90 s | 1 of 16 | 2026-11-04 01:39 |
| Stoat | I4 | 1 | 0 | - | 0 of 1 | 2026-11-02 17:50 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
stoat         66     15    9.5   102       0%     0%   31/66   0: 68, 1: 22, 5: 22, 2: 16
```

## Switchback

New flights: Dolphin 2.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback.asset, Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback_I2.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 14 | 7 | 58 s | 2 of 6 | 2026-11-04 00:21 |
| Dolphin | I2 | 18 | 12 | 80 s | 7 of 8 | 2026-11-01 17:43 |
| Dolphin | I3 | 3 | 0 | - | 0 of 2 | 2026-10-29 07:13 |
| Dolphin | I4 | 13 | 4 | 106 s | 1 of 3 | 2026-11-02 19:57 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
dolphin      102     11    5.5    74       5%     1%   11/102  0: 99, 3: 31, 7: 27, 1: 25
```

## Files the learners changed

- `Assets/Resources/AIBoostPolicies/StoatSlingConfig_Slingshot.asset`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Slingshot.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Switchback.md`

## What the sweep judged (2026-11-03 22:18 UTC)

- **Slingshot (Stoat), 30 new races by Yash on 2026-11-03 (04:12-05:52 IST) plus 1 by Garrett:** the learner had 4
  finished races and wanted to write `StoatSlingConfig_Slingshot.asset` (`aiSlingMinTurnDegrees` 35 -> 10.1,
  `aiReleaseConeDegrees` 16 -> 22.3, `aiSlingIntervalSeconds` 0.94 -> 1.23, `aiSlingMinDistance` 123 -> 82). **Not
  committed.** Every one of those races flew the ORBIT sling; Yash replaced it with the trampoline later the same day
  (`8abe1fe83b`, 2026-11-04 02:13 IST) and set `aiSlingMinTurnDegrees` 35 for it on purpose. The orbit races would
  undo that tuning with numbers from a kit the game no longer ships. The Stoat's own AI pressed nothing in these races
  (0 presses), so they do not judge the AI either.
- **Switchback (Dolphin), 2 races:** Garrett's I4 Blast Run (2026-11-02 19:57) has no result (left before the end);
  nothing to judge. No Blast Run race on a build with the AI Vessels training session's steps 1-3 has finished yet.
- **Regatta** (Sparrow 11, Dolphin 2, Squirrel 2): held. **Astro League** (Scarab 1): reported only.

## Waiting on

- **Switchback Blast Run AI (Yash, in progress):** the AI Dolphins stall at the blocked gates (the key crystal and the
  door) and reach about half the rings Yash does at Legendary I2. Numbers alone will not close that.
- **Slingshot learner and the trampoline (Yash):** the learner has no way to drop races flown on a retired kit, so the
  next new Slingshot race will propose the same orbit numbers again. Either the learner learns only from trampoline
  races (a cut-off like Switchback's course family), or Yash says the orbit numbers are fine to keep.
- The Breakwater rocket max range (about 679 u, past its 229 u reach) waits on whoever owns the Breakwater rocket.
- A Tiger Moth learner (Tapestry, Waystation) is still to build.
