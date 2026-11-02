# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-11-01 22:25 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**317 new flight(s)** since the last learn.

## Bends

New flights: Dolphin 1.

- **Dolphin: measured, not written** - DolphinAIBoostPolicy is switched off in Bends (disabledInModes).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I2 | 1 | 0 | - | - | 2026-10-28 23:57 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Bloomrush

New flights: Manta 7.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/MantaSoarAITuning_Bloomrush.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Manta | I1 | 5 | 0 | - | - | 2026-10-20 16:16 |
| Manta | I3 | 2 | 0 | - | - | 2026-10-26 04:52 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Breakwater

New flights: Sparrow 12.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/SparrowAIBoostPolicy_Breakwater.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Sparrow | I1 | 6 | 4 | 84 s | - | 2026-10-31 19:21 |
| Sparrow | I2 | 1 | 0 | - | - | 2026-10-27 18:29 |
| Sparrow | I3 | 3 | 3 | 128 s | 3 of 3 | 2026-10-26 22:04 |
| Sparrow | I4 | 2 | 2 | 89 s | - | 2026-10-20 16:35 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
sparrow       19     16    7.2    40      11%     1%    0/19   0: 24, 1: 13, 2: 9, 5: 8
```

## Broadside

New flights: Manta 2.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Manta | I1 | 2 | 0 | - | - | 2026-10-23 14:16 |

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
| Meerkat | I1 | 1 | 0 | - | - | 2026-10-31 12:59 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Cleave

New flights: Rhino 4.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Rhino | I1 | 2 | 0 | - | - | 2026-10-26 13:02 |
| Rhino | I2 | 1 | 0 | - | - | 2026-10-26 13:52 |
| Rhino | I4 | 1 | 0 | - | 1 of 1 | 2026-10-26 12:59 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## DogFight

New flights: Sparrow 4.

- **Sparrow: measured, not written** - SparrowAIBoostPolicy is switched off in DogFight (disabledInModes).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Sparrow | I1 | 4 | 0 | - | - | 2026-10-21 07:29 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Dustup

New flights: Butterfly 5.

- **Butterfly: measured, not written** - its AI's one choice, Mass or Dust, is made by each Butterfly mode's controller (ButterflyAutopilotModeDriver) in code, and the recorder logs no Dust-mode bit: nothing to learn into yet.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Butterfly | I1 | 1 | 0 | - | - | 2026-10-23 13:58 |
| Butterfly | I2 | 3 | 0 | - | - | 2026-10-29 01:46 |
| Butterfly | I4 | 1 | 0 | - | 0 of 1 | 2026-10-28 22:11 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## GrizzlyCharge

New flights: Grizzly 7.

- **Grizzly: measured, not written** - its AI bombs through GrizzlyTriggerBombConfigSO's autopilot fields, which do not load per mode, and jtgjones's grizzly-v3 is reworking the bomb triggers (15 unmerged commits): learn it once that lands.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Grizzly | I1 | 7 | 0 | - | - | 2026-10-25 15:27 |
| Unknown | I1 | 1 | 0 | - | - | 2026-10-17 06:02 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## GrizzlyTime

New flights: Grizzly 5.

- **Grizzly: measured, not written** - its AI bombs through GrizzlyTriggerBombConfigSO's autopilot fields, which do not load per mode, and jtgjones's grizzly-v3 is reworking the bomb triggers (15 unmerged commits): learn it once that lands.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Grizzly | I1 | 1 | 0 | - | - | 2026-10-26 06:27 |
| Grizzly | I4 | 4 | 2 | 106 s | 2 of 4 | 2026-10-31 23:48 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
grizzly        6     12    3.5    52       3%     2%    2/6    0: 9, 2: 6, 6: 5, 1: 4
```

## Hijack

New flights: Urchin 2.

- **Urchin: measured, not written** - the Urchin's riding is its modes' scene fields, not a drive asset (reported).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Urchin | I1 | 2 | 0 | - | 1 of 1 | 2026-10-31 13:17 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Joust

New flights: Squirrel 1.

- **Squirrel: measured, not written** - SquirrelAIBoostPolicy is switched off in Joust (disabledInModes).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Squirrel | I1 | 1 | 0 | - | - | 2026-10-20 15:33 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Regatta

New flights: Dolphin 2, Manta 13, Rhino 6, Scarab 3, Serpent 2, Sparrow 6, Squirrel 49, Stoat 3, Urchin 7.

**On hold, not written:** the AIs train in their own minigames; Regatta is rebuilt from those skills later (Yash, 2026-10-27).


- **Urchin: measured, not written** - the Urchin's riding is its modes' scene fields, not a drive asset (reported).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 1 | 0 | - | - | 2026-10-22 23:55 |
| Dolphin | I3 | 1 | 0 | - | 0 of 1 | 2026-10-30 20:08 |
| Manta | I1 | 6 | 5 | 29 s | - | 2026-10-20 17:33 |
| Manta | I3 | 2 | 1 | 51 s | - | 2026-10-20 17:48 |
| Manta | I4 | 5 | 3 | 75 s | - | 2026-10-20 17:45 |
| Rhino | I1 | 1 | 1 | 36 s | - | 2026-10-20 17:54 |
| Rhino | I3 | 5 | 1 | 61 s | - | 2026-10-20 18:02 |
| Scarab | I2 | 3 | 0 | - | - | 2026-10-25 21:56 |
| Serpent | I3 | 2 | 1 | 164 s | - | 2026-10-17 04:54 |
| Sparrow | I1 | 3 | 1 | 64 s | - | 2026-10-23 00:24 |
| Sparrow | I3 | 1 | 0 | - | - | 2026-10-23 00:14 |
| Sparrow | I4 | 2 | 0 | - | - | 2026-10-23 00:10 |
| Squirrel | I1 | 38 | 2 | 104 s | - | 2026-10-17 01:34 |
| Squirrel | I3 | 11 | 4 | 117 s | - | 2026-10-17 01:44 |
| Stoat | I2 | 2 | 0 | - | - | 2026-10-26 00:13 |
| Stoat | I3 | 1 | 0 | - | 1 of 1 | 2026-10-31 05:38 |
| Unknown | I1 | 4 | 0 | - | - | 2026-10-16 17:39 |
| Urchin | I1 | 2 | 2 | 55 s | - | 2026-10-17 03:18 |
| Urchin | I3 | 5 | 2 | 76 s | 1 of 1 | 2026-10-30 17:08 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
urchin        25     24   23.3   301       0%     6%   14/25   1: 3, 4: 3, 5: 3, 2: 2
stoat          5     24   18.5   202       1%     2%    3/5    4: 2, 7: 2
squirrel      35     20   13.8   152       0%     1%    9/35   3: 7, 5: 5, 1: 4, 2: 3
scarab         4     11    9.4   274       0%     3%    0/4    1: 3, 4: 1, 5: 1
serpent       15     16    9.3   157       1%     0%    1/15   6: 7, 7: 4, 0: 3, 3: 3
manta         61     13    7.9   257       0%     0%    1/61   3: 47, 1: 36, 4: 31, 5: 31
dolphin       53     13    7.2    77       0%    11%    0/53   5: 61, 4: 47, 3: 44, 2: 34
rhino         16     11    6.8   137       2%     1%    0/16   2: 14, 1: 12, 4: 11, 3: 8
sparrow       11     12    6.4    76       0%     7%    0/11   1: 14, 5: 13, 2: 11, 7: 10
```

## ScarabScramble

New flights: Scarab 6.

- **Scarab: measured, not written** - ScarabAIBoostPolicy is switched off in ScarabScramble (disabledInModes).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Scarab | I1 | 5 | 0 | - | 2 of 3 | 2026-10-31 05:28 |
| Scarab | I4 | 1 | 0 | - | - | 2026-10-26 20:16 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Scurry

New flights: Squirrel 10.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Squirrel | I1 | 10 | 0 | - | - | 2026-10-25 07:03 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## SerpentGulag

New flights: Serpent 3.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Serpent | I1 | 1 | 0 | - | - | 2026-11-01 02:16 |
| Serpent | I2 | 1 | 0 | - | 0 of 1 | 2026-11-01 02:44 |
| Serpent | I3 | 1 | 0 | - | - | 2026-11-01 02:45 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## SerpentTime

New flights: Serpent 27.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/SerpentAIBoostPolicy_SerpentTime.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Serpent | I1 | 14 | 9 | 64 s | 9 of 13 | 2026-10-31 23:30 |
| Serpent | I2 | 3 | 0 | - | 0 of 2 | 2026-10-31 18:57 |
| Serpent | I3 | 5 | 1 | 111 s | 1 of 5 | 2026-11-01 02:56 |
| Serpent | I4 | 5 | 2 | 98 s | 2 of 3 | 2026-10-31 20:01 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
serpent       49     12    8.1   109       1%     3%   11/49   0: 37, 5: 25, 7: 17, 4: 16
```

## Skein

New flights: Urchin 1.

- **Urchin: measured, not written** - the Urchin's riding is its modes' scene fields, not a drive asset (reported).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Unknown | I1 | 1 | 0 | - | - | 2026-10-16 13:09 |
| Unknown | I3 | 1 | 0 | - | - | 2026-10-16 18:29 |
| Urchin | I3 | 1 | 0 | - | - | 2026-10-16 18:35 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
urchin         3      4    1.7    64       5%     1%    0/3    1: 1, 2: 1, 3: 1, 4: 1
```

## SkimRace

New flights: Squirrel 20.

- `learn_from_flights --mode SkimRace --write`: OK. wrote Docs/AI_SYSTEM/LEARNED_FROM_FLIGHTS_SkimRace.md

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Squirrel | I1 | 6 | 0 | - | - | 2026-10-25 07:22 |
| Squirrel | I2 | 12 | 0 | - | 0 of 5 | 2026-10-28 00:05 |
| Squirrel | I3 | 1 | 0 | - | 0 of 1 | 2026-10-26 06:31 |
| Squirrel | I4 | 1 | 0 | - | - | 2026-10-31 17:16 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
squirrel      42      0    0.0   183       1%     1%    0/42   0: 42
```

## Slingshot

New flights: Stoat 4.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Stoat | I1 | 1 | 0 | - | 0 of 1 | 2026-10-31 13:13 |
| Stoat | I2 | 3 | 1 | 75 s | 1 of 3 | 2026-10-29 20:17 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
stoat          7     15   11.0    69       0%     0%    3/7    6: 4, 1: 3, 0: 3, 2: 1
```

## Switchback

New flights: Dolphin 51.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback.asset, Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback_I2.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 12 | 7 | 58 s | 2 of 5 | 2026-10-29 04:18 |
| Dolphin | I2 | 18 | 12 | 80 s | 7 of 8 | 2026-11-01 17:43 |
| Dolphin | I3 | 3 | 0 | - | 0 of 2 | 2026-10-29 07:13 |
| Dolphin | I4 | 11 | 4 | 106 s | 1 of 3 | 2026-10-29 22:27 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
dolphin       95     11    5.4    74       5%     1%   10/95   0: 92, 3: 29, 7: 27, 1: 23
```

## Tandava

New flights: Squirrel 1.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Squirrel | I1 | 1 | 0 | - | - | 2026-10-25 18:48 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Tapestry

New flights: Butterfly 1.

- **Butterfly: measured, not written** - its AI's one choice, Mass or Dust, is made by each Butterfly mode's controller (ButterflyAutopilotModeDriver) in code, and the recorder logs no Dust-mode bit: nothing to learn into yet.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Butterfly | I1 | 1 | 0 | - | - | 2026-10-23 14:03 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## ThresherTime

New flights: Thresher 15.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/ThresherAIBoostPolicy_ThresherTime.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Thresher | I1 | 9 | 5 | 65 s | 4 of 7 | 2026-10-31 13:20 |
| Thresher | I2 | 3 | 1 | 106 s | 1 of 3 | 2026-10-31 23:23 |
| Thresher | I4 | 3 | 1 | 120 s | 1 of 3 | 2026-10-31 23:26 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
thresher      25     12    7.0   118       1%     1%    6/25   0: 24, 1: 20, 3: 14, 2: 12
```

## Warpline

New flights: Stoat 20.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/StoatDipoleConfig_Warpline.asset, Assets/Resources/AIBoostPolicies/StoatSlingConfig_Warpline.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Stoat | I1 | 5 | 5 | 87 s | - | 2026-10-25 07:33 |
| Stoat | I2 | 3 | 3 | 66 s | 1 of 1 | 2026-10-27 00:16 |
| Stoat | I3 | 6 | 6 | 79 s | 6 of 6 | 2026-10-27 00:18 |
| Stoat | I4 | 6 | 3 | 77 s | 3 of 6 | 2026-10-27 01:27 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
stoat         40      4    2.7   140       8%     1%    3/40   0: 46, 2: 29, 3: 27, 1: 26
```

## Waystation

New flights: Butterfly 2, TigerMoth 2.

- **Butterfly: measured, not written** - its AI's one choice, Mass or Dust, is made by each Butterfly mode's controller (ButterflyAutopilotModeDriver) in code, and the recorder logs no Dust-mode bit: nothing to learn into yet.

**No learner reads these yet** (the sweep's to-do): TigerMoth.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Butterfly | I1 | 2 | 1 | 205 s | - | 2026-10-21 15:32 |
| TigerMoth | I4 | 2 | 1 | 143 s | 1 of 2 | 2026-10-29 17:59 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
tigermoth      4     15    6.3    56       8%     1%    1/4    0: 4, 1: 4, 12: 4, 7: 3
butterfly      2      0    0.0    50      24%     3%    0/2    0: 2
```

## WaystationPrototype

New flights: TigerMoth 2.

**No learner reads these yet** (the sweep's to-do): TigerMoth.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| TigerMoth | I1 | 1 | 1 | 223 s | 1 of 1 | 2026-11-01 02:36 |
| TigerMoth | I4 | 1 | 0 | - | - | 2026-11-01 02:49 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
tigermoth      2      4    2.0    67       1%     1%    0/2    0: 2, 2: 1, 3: 1, 1: 1
```

## WrasseRun

New flights: Wrasse 9.

- **Wrasse: measured, not written** - its boost is the crew's: KiteNavigator spends a saved boost while the next ring is aiBoostRingDistance away (AIPilotWantsBoost, KiteNavigatorConfig), which does not load per mode; its AIPilot names no policy.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Wrasse | I1 | 9 | 1 | 94 s | 0 of 4 | 2026-10-31 13:26 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
wrasse        14     15    8.7   108       1%     1%    6/14   0: 17, 3: 6, 7: 3, 1: 3
```

## WreckingBall

New flights: Scarab 1.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Scarab | I1 | 1 | 0 | - | - | 2026-10-22 23:45 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Files the learners changed

- `Docs/AI_SYSTEM/LEARNED_DRIVES_Switchback.md`

## What the sweep judged (2026-11-01 22:40 UTC)

- **Every flight was learned again; no race was new.** b079ef27d9 / fc4ef88ce7 (2026-10-29, learners skip the
  flights no person flew, such as a trainer's) changed the learners' fingerprint, so `ai_learn.py` relearned all 332
  flights, 7 of which no person flew. No learned asset changed; only the date in `LEARNED_DRIVES_Switchback.md`
  moved. The 2026-11-01 19:45 UTC verdicts and "Waiting on" stand.
