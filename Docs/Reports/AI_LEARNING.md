# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-10-25 22:18 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**159 new flight(s)** since the last learn.

## Bloomrush

New flights: Manta 5.

**No learner reads these yet** (the sweep's to-do): Manta.

## Breakwater

New flights: Sparrow 5.

**No learner reads these yet** (the sweep's to-do): Sparrow.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest |
|---|---|---:|---:|---:|
| Sparrow | I1 | 3 | 3 | 84 s |
| Sparrow | I4 | 2 | 2 | 89 s |

The AI rivals in those races (`regatta_rival_report.py`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
sparrow        5      1    0.7    36      56%     2%    0/5    0: 5, 1: 2, 9: 1
```

## Broadside

New flights: Manta 2.

**No learner reads these yet** (the sweep's to-do): Manta.

## DogFight

New flights: Sparrow 4.

**No learner reads these yet** (the sweep's to-do): Sparrow.

## Dustup

New flights: Butterfly 1.

**No learner reads these yet** (the sweep's to-do): Butterfly.

## GrizzlyCharge

New flights: Grizzly 7.

**No learner reads these yet** (the sweep's to-do): Grizzly.

## Hijack

New flights: Urchin 1.

**No learner reads these yet** (the sweep's to-do): Urchin.

## Joust

New flights: Squirrel 1.

**No learner reads these yet** (the sweep's to-do): Squirrel.

## Regatta

New flights: Dolphin 1, Manta 13, Rhino 6, Scarab 3, Serpent 2, Sparrow 6, Squirrel 49, Stoat 2, Urchin 6.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/MantaSoarAITuning_Regatta.asset.
- `learn_drives_from_flights --check`: OK. check: OK
- `author_race_lines --check`: OK. author_race_lines --check: OK (7 hulls, 4 learned)
- `learn_from_flights --write`: OK. wrote Docs/AI_SYSTEM/LEARNED_FROM_FLIGHTS.md

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest |
|---|---|---:|---:|---:|
| Dolphin | I1 | 1 | 0 | - |
| Manta | I1 | 6 | 5 | 29 s |
| Manta | I3 | 2 | 1 | 51 s |
| Manta | I4 | 5 | 3 | 75 s |
| Rhino | I1 | 1 | 1 | 36 s |
| Rhino | I3 | 5 | 1 | 61 s |
| Scarab | I2 | 3 | 0 | - |
| Serpent | I3 | 2 | 1 | 164 s |
| Sparrow | I1 | 3 | 1 | 64 s |
| Sparrow | I3 | 1 | 0 | - |
| Sparrow | I4 | 2 | 0 | - |
| Squirrel | I1 | 38 | 2 | 104 s |
| Squirrel | I3 | 11 | 4 | 117 s |
| Stoat | I2 | 2 | 0 | - |
| Unknown | I1 | 4 | 0 | - |
| Urchin | I1 | 2 | 2 | 55 s |
| Urchin | I3 | 4 | 1 | 76 s |

The AI rivals in those races (`regatta_rival_report.py`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
urchin        23     24   23.6   301       8%     7%   12/23   1: 3, 4: 3, 5: 3, 2: 2
stoat          3     24   19.0   209       0%     0%    3/3    
squirrel      35     20   13.8   152       5%     1%    9/35   3: 7, 5: 5, 1: 4, 2: 3
serpent       15     16    9.3   157      11%     0%    1/15   6: 7, 7: 4, 0: 3, 3: 3
manta         58     14    7.9   266       2%     0%    1/58   3: 44, 1: 33, 4: 31, 5: 31
dolphin       50     13    7.2    76      11%    11%    0/50   5: 58, 4: 47, 3: 44, 2: 34
sparrow       11     12    6.4    76      15%     7%    0/11   1: 14, 5: 13, 2: 11, 7: 10
rhino         13     10    6.3   130      14%     0%    0/13   1: 12, 2: 11, 4: 8, 5: 6
scarab         2      5    5.5   322       0%     0%    0/2    1: 1, 4: 1, 5: 1
```

## ScarabScramble

New flights: Scarab 1.

**No learner reads these yet** (the sweep's to-do): Scarab.

## Scurry

New flights: Squirrel 9.

**No learner reads these yet** (the sweep's to-do): Squirrel.

## Skein

New flights: Urchin 1.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest |
|---|---|---:|---:|---:|
| Unknown | I1 | 1 | 0 | - |
| Unknown | I3 | 1 | 0 | - |
| Urchin | I3 | 1 | 0 | - |

The AI rivals in those races (`regatta_rival_report.py`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
urchin         3      4    1.7    64      21%     1%    0/3    1: 1, 2: 1, 3: 1, 4: 1
```

## SkimRace

New flights: Squirrel 7.

**No learner reads these yet** (the sweep's to-do): Squirrel.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest |
|---|---|---:|---:|---:|
| Squirrel | I1 | 1 | 0 | - |
| Squirrel | I2 | 6 | 0 | - |

The AI rivals in those races (`regatta_rival_report.py`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
squirrel      15      0    0.0   183       8%     1%    0/15   0: 15
```

## Switchback

New flights: Dolphin 12.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest |
|---|---|---:|---:|---:|
| Dolphin | I1 | 4 | 3 | 75 s |
| Dolphin | I2 | 5 | 5 | 80 s |
| Dolphin | I4 | 3 | 2 | 247 s |

The AI rivals in those races (`regatta_rival_report.py`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
dolphin       26      6    3.3    68      20%     1%    2/26   0: 26, 5: 6, 6: 6, 8: 5
```

## Tapestry

New flights: Butterfly 1.

**No learner reads these yet** (the sweep's to-do): Butterfly.

## ThresherTime

New flights: Thresher 2.

**No learner reads these yet** (the sweep's to-do): Thresher.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest |
|---|---|---:|---:|---:|
| Thresher | I1 | 2 | 1 | 127 s |

The AI rivals in those races (`regatta_rival_report.py`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
thresher       2      8    6.6   116      16%     0%    0/2    2: 2, 1: 1, 0: 1, 4: 1
```

## Warpline

New flights: Stoat 6.

**On hold, not written:** the Stoat's AI waits for Garrett's configuration (issue 1198).


The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest |
|---|---|---:|---:|---:|
| Stoat | I1 | 4 | 4 | 87 s |
| Stoat | I2 | 2 | 2 | 86 s |

The AI rivals in those races (`regatta_rival_report.py`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
stoat         12      4    2.7   144      41%     0%    0/12   0: 13, 2: 8, 3: 7, 1: 6
```

## Waystation

New flights: Butterfly 2.

**No learner reads these yet** (the sweep's to-do): Butterfly.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest |
|---|---|---:|---:|---:|
| Butterfly | I1 | 2 | 1 | 205 s |

The AI rivals in those races (`regatta_rival_report.py`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
butterfly      2      0    0.0    50      44%     3%    0/2    0: 2
```

## WrasseRun

New flights: Wrasse 3.

**No learner reads these yet** (the sweep's to-do): Wrasse.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest |
|---|---|---:|---:|---:|
| Wrasse | I1 | 3 | 1 | 94 s |

The AI rivals in those races (`regatta_rival_report.py`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
wrasse         3     16    8.4   113       7%     1%    2/3    0: 4, 7: 3, 3: 1
```

## WreckingBall

New flights: Scarab 1.

**No learner reads these yet** (the sweep's to-do): Scarab.

## Files the learners changed

- `Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback.asset`
- `CLAUDE.md`
- `Docs/AI_SYSTEM/AUTO_LEARNING.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Skein.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Switchback.md`
- `Docs/AI_SYSTEM/LEARNED_FROM_FLIGHTS.md`
- `Docs/AI_SYSTEM/MINIGAME_TRAINING_PLAN.md`
- `Docs/AI_SYSTEM/README.md`
