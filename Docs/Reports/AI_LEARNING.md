# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-11-25 22:33 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**732 new flight(s)** since the last learn.

## AstroLeague

New flights: Scarab 1, Swordfish 3.

- **Scarab: measured, not written** - ScarabAIBoostPolicy is switched off in AstroLeague (disabledInModes).
- **Swordfish: measured, not written** - no measure for RampBoostAIPolicySO yet (its policy SwordfishAIBoostPolicy).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Scarab | I1 | 1 | 0 | - | 1 of 1 | 2026-11-02 16:49 |
| Swordfish | I2 | 3 | 0 | - | 1 of 2 | 2026-11-15 23:49 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

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

New flights: Manta 17.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/MantaSoarAITuning_Bloomrush.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Manta | I1 | 15 | 0 | - | 1 of 1 | 2026-11-22 23:16 |
| Manta | I3 | 2 | 0 | - | - | 2026-10-26 04:52 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Breakwater

New flights: Sparrow 19.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Sparrow | I1 | 7 | 4 | 84 s | 0 of 1 | 2026-11-20 23:22 |
| Sparrow | I2 | 1 | 0 | - | - | 2026-10-27 18:29 |
| Sparrow | I3 | 4 | 3 | 128 s | 3 of 4 | 2026-11-20 16:00 |
| Sparrow | I4 | 7 | 4 | 89 s | 2 of 5 | 2026-11-25 02:20 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
sparrow       33     22    8.9    79       9%     7%    5/33   0: 31, 1: 21, 5: 16, 10: 14
```

## BrittleStarTime

New flights: BrittleStar 2.

- **BrittleStar: measured, not written** - no measure for BrittleStarSpeedModeAIPolicySO yet (its policy BrittleStarAIBoostPolicy).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| BrittleStar | I1 | 1 | 1 | 175 s | 1 of 1 | 2026-11-02 23:14 |
| BrittleStar | I4 | 1 | 1 | 135 s | 1 of 1 | 2026-11-02 23:17 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
brittlestar     2     10    3.7    24       1%     1%    0/2    1: 3, 0: 2, 3: 2, 4: 2
```

## Broadside

New flights: Dolphin 2, Manta 9, Scarab 1, Sparrow 3, Squirrel 1, Urchin 2.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

- **Dolphin: measured, not written** - DolphinAIBoostPolicy is switched off in Broadside (disabledInModes).
- **Urchin: measured, not written** - the Urchin's riding is its modes' scene fields, not a drive asset (reported).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I2 | 2 | 0 | - | - | 2026-11-13 15:51 |
| Manta | I1 | 2 | 0 | - | - | 2026-10-23 14:16 |
| Manta | I2 | 7 | 0 | - | - | 2026-11-15 02:36 |
| Scarab | I2 | 1 | 0 | - | - | 2026-11-23 09:58 |
| Sparrow | I2 | 3 | 0 | - | - | 2026-11-13 20:25 |
| Squirrel | I2 | 1 | 0 | - | - | 2026-11-10 06:22 |
| Urchin | I2 | 2 | 0 | - | - | 2026-11-13 20:13 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## BroodRush

New flights: Dolphin 11, Manta 5, Rhino 1, Sparrow 2, Squirrel 2, Swordfish 5.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_BroodRush.asset, Assets/Resources/AIBoostPolicies/MantaSoarAITuning_BroodRush.asset.
- `learn_drives_from_flights --check`: OK. check: OK

- **Swordfish: measured, not written** - no measure for RampBoostAIPolicySO yet (its policy SwordfishAIBoostPolicy).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 2 | 0 | - | 2 of 2 | 2026-11-21 06:19 |
| Dolphin | I2 | 9 | 0 | - | 1 of 3 | 2026-11-23 13:14 |
| Manta | I1 | 3 | 0 | - | 1 of 1 | 2026-11-02 16:18 |
| Manta | I2 | 2 | 0 | - | 1 of 2 | 2026-11-23 10:14 |
| Rhino | I1 | 1 | 0 | - | 1 of 1 | 2026-11-02 16:21 |
| Sparrow | I2 | 2 | 0 | - | 1 of 2 | 2026-11-14 21:35 |
| Squirrel | I1 | 2 | 0 | - | 2 of 2 | 2026-11-21 06:19 |
| Swordfish | I2 | 5 | 0 | - | 0 of 2 | 2026-11-19 20:17 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## BurrowRun

New flights: Meerkat 2.

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

## Corona

New flights: Manta 1.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Manta | I1 | 1 | 0 | - | 1 of 1 | 2026-11-21 01:15 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
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

## DogFight

New flights: Sparrow 7.

- **Sparrow: measured, not written** - SparrowAIBoostPolicy is switched off in DogFight (disabledInModes).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Sparrow | I1 | 7 | 0 | - | 2 of 2 | 2026-11-23 21:20 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Dustup

New flights: Butterfly 5, TigerMoth 2.

- **Butterfly: measured, not written** - its AI's one choice, Mass or Dust, is made by each Butterfly mode's controller (ButterflyAutopilotModeDriver) in code, and the recorder logs no Dust-mode bit: nothing to learn into yet.

**No learner reads these yet** (the sweep's to-do): TigerMoth.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Butterfly | I1 | 1 | 0 | - | - | 2026-10-23 13:58 |
| Butterfly | I2 | 3 | 0 | - | - | 2026-10-29 01:46 |
| Butterfly | I4 | 1 | 0 | - | 0 of 1 | 2026-10-28 22:11 |
| TigerMoth | I1 | 1 | 0 | - | - | 2026-11-10 07:58 |
| TigerMoth | I3 | 1 | 0 | - | - | 2026-11-03 16:13 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Flatline

New flights: BrittleStar 4.

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

## Gorge

New flights: JewelWasp 1.

**No learner reads these yet** (the sweep's to-do): JewelWasp.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| JewelWasp | I4 | 1 | 0 | - | - | 2026-11-16 19:37 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## GrizzlyCharge

New flights: Grizzly 11.

- **Grizzly: measured, not written** - its AI bombs through GrizzlyTriggerBombConfigSO's autopilot fields, which do not load per mode, and jtgjones's grizzly-v3 is reworking the bomb triggers (15 unmerged commits): learn it once that lands.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Grizzly | I1 | 11 | 0 | - | 1 of 1 | 2026-11-25 09:46 |
| Unknown | I1 | 1 | 0 | - | - | 2026-10-17 06:02 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## GrizzlyTime

New flights: Grizzly 17.

- **Grizzly: measured, not written** - its AI bombs through GrizzlyTriggerBombConfigSO's autopilot fields, which do not load per mode, and jtgjones's grizzly-v3 is reworking the bomb triggers (15 unmerged commits): learn it once that lands.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Grizzly | I1 | 2 | 0 | - | - | 2026-11-19 00:37 |
| Grizzly | I2 | 2 | 2 | 81 s | 2 of 2 | 2026-11-19 02:58 |
| Grizzly | I3 | 1 | 1 | 108 s | 1 of 1 | 2026-11-16 19:40 |
| Grizzly | I4 | 12 | 5 | 106 s | 5 of 10 | 2026-11-12 21:52 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
grizzly       24      9    4.2    54       1%     1%    5/24   0: 32, 1: 25, 2: 18, 3: 14
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

## Headlong

New flights: Rhino 2.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Rhino | I1 | 1 | 1 | 85 s | 1 of 1 | 2026-11-12 22:52 |
| Rhino | I4 | 1 | 1 | 146 s | 1 of 1 | 2026-11-12 22:56 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
rhino          4      6    4.0   294      10%     1%    0/4    0: 4, 2: 3, 3: 3, 1: 2
```

## Hedgerow

New flights: Serpent 1.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Serpent | I1 | 1 | 0 | - | 1 of 1 | 2026-11-21 23:14 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
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

## Jackstraw

New flights: Thresher 13.

- **Thresher: measured, not written** - its controller steers the AI (JackstrawHunter, at its rival: SetExternalTargetProvider), not at the crystal the learner measures against, and the mode's AI is Shombith's: reported, never written.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Thresher | I1 | 11 | 0 | - | - | 2026-11-12 21:57 |
| Thresher | I2 | 1 | 0 | - | 0 of 1 | 2026-11-16 19:46 |
| Thresher | I3 | 1 | 0 | - | - | 2026-11-10 05:27 |

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

## Kelpwood

New flights: Angler 3.

- **Angler: measured, not written** - no measure for AnglerLeapfrogAIPolicySO yet (its policy AnglerAIBoostPolicy).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Angler | I4 | 3 | 0 | - | 1 of 1 | 2026-11-12 21:53 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Landslide

New flights: Rhino 4.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Rhino | I1 | 3 | 0 | - | - | 2026-11-21 00:50 |
| Rhino | I4 | 1 | 0 | - | 1 of 1 | 2026-11-17 18:28 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## LanternRun

New flights: Angler 3.

- **Angler: measured, not written** - no measure for AnglerLeapfrogAIPolicySO yet (its policy AnglerAIBoostPolicy).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Angler | I1 | 1 | 0 | - | - | 2026-11-02 03:48 |
| Angler | I4 | 2 | 1 | 239 s | 1 of 1 | 2026-11-15 01:47 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
angler         3      3    2.4    40       0%     0%    0/3    0: 4, 1: 3, 2: 3, 3: 3
```

## PerchRun

New flights: Goby 13.

- **Goby: measured, not written** - its boost policy SquirrelAIBoostPolicy (SkimRingAIPolicySO) presses SquirrelTubeActionSO, which this hull does not bind - the drive cannot act.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Goby | I1 | 11 | 4 | 28 s | 4 of 9 | 2026-11-26 01:18 |
| Goby | I2 | 2 | 0 | - | - | 2026-11-25 11:51 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
goby          13     16   23.8     0       0%    65%    8/13   6: 1
```

## Railrush

New flights: Urchin 1.

- **Urchin: measured, not written** - the Urchin's riding is its modes' scene fields, not a drive asset (reported).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Urchin | I1 | 1 | 0 | - | - | 2026-11-21 02:16 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Rampage

New flights: Dolphin 2.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 1 | 0 | - | - | 2026-11-05 09:38 |
| Dolphin | I3 | 1 | 0 | - | 0 of 1 | 2026-11-11 03:04 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Regatta

New flights: Dolphin 21, Manta 23, Rhino 10, Scarab 3, Serpent 2, Sparrow 65, Squirrel 78, Stoat 14, Thresher 2, Urchin 7.

**On hold, not written:** the AIs train in their own minigames; Regatta is rebuilt from those skills later (Yash, 2026-10-27).


- **Stoat: measured, not written** - its ability is still being made (Yash, 2026-11-10: "The stoat is not ready, ignore its AI, I'm trying to make the Stoat ability better"): reported, never written, until Yash says the kit is ready.
- **Urchin: measured, not written** - the Urchin's riding is its modes' scene fields, not a drive asset (reported).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 3 | 0 | - | 0 of 1 | 2026-11-03 11:44 |
| Dolphin | I2 | 13 | 1 | 49 s | 0 of 8 | 2026-11-23 23:25 |
| Dolphin | I3 | 1 | 0 | - | 0 of 1 | 2026-10-30 20:08 |
| Dolphin | I4 | 4 | 0 | - | 0 of 4 | 2026-11-06 15:10 |
| Manta | I1 | 10 | 5 | 29 s | 0 of 1 | 2026-11-23 05:00 |
| Manta | I2 | 6 | 1 | 55 s | 1 of 6 | 2026-11-20 06:38 |
| Manta | I3 | 2 | 1 | 51 s | - | 2026-10-20 17:48 |
| Manta | I4 | 5 | 3 | 75 s | - | 2026-10-20 17:45 |
| Rhino | I1 | 1 | 1 | 36 s | - | 2026-10-20 17:54 |
| Rhino | I2 | 4 | 0 | - | 0 of 4 | 2026-11-19 20:08 |
| Rhino | I3 | 5 | 1 | 61 s | - | 2026-10-20 18:02 |
| Scarab | I2 | 3 | 0 | - | - | 2026-10-25 21:56 |
| Serpent | I3 | 2 | 1 | 164 s | - | 2026-10-17 04:54 |
| Sparrow | I1 | 62 | 1 | 64 s | 0 of 32 | 2026-11-03 11:39 |
| Sparrow | I3 | 1 | 0 | - | - | 2026-10-23 00:14 |
| Sparrow | I4 | 2 | 0 | - | - | 2026-10-23 00:10 |
| Squirrel | I1 | 58 | 2 | 104 s | 0 of 1 | 2026-11-23 05:09 |
| Squirrel | I2 | 5 | 0 | - | 1 of 5 | 2026-11-23 23:25 |
| Squirrel | I3 | 11 | 4 | 117 s | - | 2026-10-17 01:44 |
| Squirrel | I4 | 4 | 1 | 152 s | 0 of 4 | 2026-11-06 15:44 |
| Stoat | I1 | 2 | 0 | - | 0 of 1 | 2026-11-03 11:31 |
| Stoat | I2 | 9 | 0 | - | 1 of 7 | 2026-11-06 05:37 |
| Stoat | I3 | 1 | 0 | - | 1 of 1 | 2026-10-31 05:38 |
| Stoat | I4 | 2 | 0 | - | 0 of 2 | 2026-11-06 05:19 |
| Thresher | I2 | 2 | 0 | - | 0 of 1 | 2026-11-23 12:51 |
| Unknown | I1 | 4 | 0 | - | - | 2026-10-16 17:39 |
| Urchin | I1 | 2 | 2 | 55 s | - | 2026-10-17 03:18 |
| Urchin | I3 | 5 | 2 | 76 s | 1 of 1 | 2026-10-30 17:08 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
urchin        74     24   23.3   300       0%     3%   41/74   2: 5, 3: 5, 5: 5, 1: 4
thresher      13     24   23.0   214       0%     3%    7/13   4: 2, 0: 2, 6: 2, 1: 1
shrike         7     22   21.7     0       1%    66%    0/7    3: 3, 6: 3, 5: 2
brittlestar     8     20   19.4   206       2%     7%    0/8    1: 3, 4: 2
goby           9     19   19.2   101       2%    14%    0/9    5: 1
swordfish      5     17   16.3   125       2%     0%    0/5    4: 2, 5: 2, 6: 2
squirrel      79     17   13.9   152       0%     2%   15/79   2: 10, 4: 9, 3: 8, 1: 7
scarab        14     23   12.9   322       1%     1%    6/14   1: 11, 7: 9, 6: 9, 0: 7
stoat         26     19   11.9   138       1%     1%    8/26   7: 15, 6: 15, 3: 14, 4: 13
sparrow       29     15   11.4   111       0%     4%    3/29   1: 14, 5: 13, 4: 11, 2: 11
serpent       32     14   11.3   158       1%     0%    2/32   6: 11, 7: 8, 0: 8, 3: 6
tigermoth     10      9   11.1    67       2%    11%    0/10   5: 4, 0: 2, 6: 2
rhino         70     11   10.9   124       1%     1%    8/70   5: 37, 4: 33, 1: 31, 2: 29
manta        103     13    9.3   238       0%     1%    7/103  3: 53, 1: 41, 4: 35, 2: 33
dolphin       90     13    8.1    95       0%     8%    5/90   5: 63, 4: 49, 3: 46, 2: 37
```

## ScarabRally

New flights: Scarab 10.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Scarab | I1 | 7 | 2 | 62 s | 2 of 3 | 2026-11-15 01:56 |
| Scarab | I4 | 3 | 0 | - | 0 of 1 | 2026-11-15 01:49 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
scarab        10     12    7.2   220       0%     0%    2/10   8: 7, 5: 6, 3: 5, 2: 3
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

New flights: Squirrel 12.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Squirrel | I1 | 10 | 0 | - | - | 2026-10-25 07:03 |
| Squirrel | I2 | 2 | 0 | - | - | 2026-11-21 01:29 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## SerpentGulag

New flights: Serpent 5.

- **Serpent: measured, not written** - its controller steers the AI (SerpentGulagMarksman, to a sniper post: SetExternalTargetProvider), not at the crystal the learner measures against, and the mode's AI is Shombith's: reported, never written.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Serpent | I1 | 1 | 0 | - | - | 2026-11-01 02:16 |
| Serpent | I2 | 1 | 0 | - | 0 of 1 | 2026-11-01 02:44 |
| Serpent | I3 | 1 | 0 | - | - | 2026-11-01 02:45 |
| Serpent | I4 | 2 | 0 | - | 0 of 1 | 2026-11-07 21:06 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## SerpentHood

New flights: Serpent 1.

- **Serpent: measured, not written** - its controller steers the AI (SerpentHoodMarksman, round the rim to a perch: SetExternalTargetProvider), not at the crystal the learner measures against, and the mode's AI is Shombith's: reported, never written.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Serpent | I4 | 1 | 0 | - | 1 of 1 | 2026-11-10 05:36 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## SerpentTime

New flights: Serpent 29.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Serpent | I1 | 15 | 9 | 64 s | 9 of 14 | 2026-11-20 16:03 |
| Serpent | I2 | 3 | 0 | - | 0 of 2 | 2026-10-31 18:57 |
| Serpent | I3 | 5 | 1 | 111 s | 1 of 5 | 2026-11-01 02:56 |
| Serpent | I4 | 6 | 2 | 98 s | 2 of 4 | 2026-11-07 21:11 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
serpent       54     13    8.1   107       1%     4%   13/54   0: 40, 5: 28, 2: 21, 4: 20
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

## Sirocco

New flights: TigerMoth 1.

**No learner reads these yet** (the sweep's to-do): TigerMoth.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| TigerMoth | I3 | 1 | 0 | - | 1 of 1 | 2026-11-16 19:44 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Skein

New flights: Urchin 11.

- **Urchin: measured, not written** - the Urchin's riding is its modes' scene fields, not a drive asset (reported).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Unknown | I1 | 1 | 0 | - | - | 2026-10-16 13:09 |
| Unknown | I3 | 1 | 0 | - | - | 2026-10-16 18:29 |
| Urchin | I1 | 10 | 0 | - | 9 of 9 | 2026-11-23 00:15 |
| Urchin | I3 | 1 | 0 | - | - | 2026-10-16 18:35 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
urchin        26     22   19.8   208       1%     0%   10/26   1: 6, 4: 3, 12: 3, 11: 3
```

## SkimRace

New flights: Squirrel 31.

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

## Slingshot

New flights: Stoat 45.

- **Stoat: measured, not written** - its ability is still being made (Yash, 2026-11-10: "The stoat is not ready, ignore its AI, I'm trying to make the Stoat ability better"): reported, never written, until Yash says the kit is ready.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Stoat | I1 | 1 | 0 | - | 0 of 1 | 2026-10-31 13:13 |
| Stoat | I2 | 15 | 1 | 75 s | 1 of 15 | 2026-11-03 04:33 |
| Stoat | I3 | 22 | 1 | 90 s | 1 of 20 | 2026-11-21 05:03 |
| Stoat | I4 | 7 | 0 | - | 0 of 7 | 2026-11-21 22:39 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
stoat         88     15   10.3   105       0%     0%   41/88   0: 72, 1: 25, 5: 25, 2: 20
```

## SlipLine

New flights: Honeyguide 4.

- **Honeyguide: measured, not written** - its boost policy SparrowAIBoostPolicy (HoldBoostAIPolicySO) presses BoostActionSO, which this hull does not bind - the drive cannot act.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Honeyguide | I1 | 4 | 1 | 156 s | 1 of 1 | 2026-11-15 02:25 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
honeyguide     4      4    6.1    36      12%     4%    0/4    1: 4, 3: 2, 8: 1, 9: 1
```

## Spearline

New flights: Swordfish 4.

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

New flights: Dolphin 77.

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

New flights: Butterfly 1, TigerMoth 3.

- **Butterfly: measured, not written** - its AI's one choice, Mass or Dust, is made by each Butterfly mode's controller (ButterflyAutopilotModeDriver) in code, and the recorder logs no Dust-mode bit: nothing to learn into yet.

**No learner reads these yet** (the sweep's to-do): TigerMoth.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Butterfly | I1 | 1 | 0 | - | - | 2026-10-23 14:03 |
| TigerMoth | I1 | 3 | 0 | - | 1 of 1 | 2026-11-01 17:42 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
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

New flights: Thresher 24.

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

## Tollway

New flights: Scarab 1.

- **Scarab: measured, not written** - ScarabAIBoostPolicy is switched off in Tollway (disabledInModes).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Scarab | I1 | 1 | 0 | - | - | 2026-11-23 13:38 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
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

## Warpline

New flights: Stoat 20.

- **Stoat: measured, not written** - its ability is still being made (Yash, 2026-11-10: "The stoat is not ready, ignore its AI, I'm trying to make the Stoat ability better"): reported, never written, until Yash says the kit is ready.

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

New flights: Butterfly 2, TigerMoth 8.

- **Butterfly: measured, not written** - its AI's one choice, Mass or Dust, is made by each Butterfly mode's controller (ButterflyAutopilotModeDriver) in code, and the recorder logs no Dust-mode bit: nothing to learn into yet.

**No learner reads these yet** (the sweep's to-do): TigerMoth.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Butterfly | I1 | 2 | 1 | 205 s | - | 2026-10-21 15:32 |
| TigerMoth | I1 | 1 | 1 | 170 s | 1 of 1 | 2026-11-01 17:45 |
| TigerMoth | I4 | 7 | 2 | 143 s | 2 of 4 | 2026-11-12 19:23 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
tigermoth     10     22    6.3    45      10%     1%    2/10   0: 10, 1: 9, 7: 8, 2: 7
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

## WildlifeLiberation

New flights: Sparrow 6.

- **Sparrow: measured, not written** - SparrowAIBoostPolicy is switched off in WildlifeLiberation (disabledInModes).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Sparrow | I3 | 6 | 0 | - | 2 of 2 | 2026-11-15 02:05 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Windrow

New flights: Rhino 1.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Rhino | I1 | 1 | 0 | - | 1 of 1 | 2026-11-18 00:29 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
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

- `Docs/AI_SYSTEM/LEARNED_DRIVES_Rampage.md` (new). The full relearn also rewrote the Brood Rush variants and the date
  line of 18 other `LEARNED_*` docs; the sweep kept none of those (see below).

## What the sweep judged (2026-11-25)

23 new flights: 9 Perch Run (Goby), 10 Regatta (Squirrel) and 1 (Manta), 2 Rampage (Dolphin), 1 Grizzly Charge.
**No asset under `Assets/Resources/AIBoostPolicies` changed.**

- **The learner's `--check` failed on an asset it never wrote:** `SwordfishAIBoostPolicy_Riposte` (Yash, f0dadde55,
  today) flies `cornerSpeed` 2000, legal in C# (`RampBoostAIPolicySO.cornerSpeed` is `[Min(1f)]`, no maximum), but the
  check held every asset to the learner's write clamp (100-1200). Fixed in `learn_drives_from_flights.py`: `--check`
  now uses `CHECK_RANGES` (the C# bound for `cornerSpeed`); what the learner writes is still clamped to 1200. The
  learner change made `ai_learn.py` relearn all 740 flights.
- **Brood Rush:** the relearn proposed new Dolphin numbers and a first Manta Soar variant. Not kept: Brood Rush waits on
  the arena-game decision below, and the Manta band (0.025) is the one the AI cannot fly.
- **Rampage (Dolphin, 2 flights, 1 finished):** under 3 finished races, nothing written; no rival rows.
- **Perch Run (Goby, 9 new):** report only (borrowed `SquirrelAIBoostPolicy` cannot act). Rival velocity reads 0 u/s,
  see Waiting on.
- **Grizzly Charge (1):** report only until jtgjones's grizzly-v3 bomb-trigger rework lands.
- **Regatta (Squirrel 10, Manta 1):** on hold, reported only.
- `promote_trained_drives.py --check` OK; `learn_drives_from_flights.py --check` and `--self-test` OK.

## Waiting on

- **Brood Rush and Broadside are arena games the learner writes into** (2026-11-23, Yash's call on `ai_learn.py`,
  Garrett's on the plan): add them to `HOLD` beside Regatta, or say the committed `DolphinAIBoostPolicy_BroodRush`
  variant stays. Until then the sweep keeps no new Brood Rush or Broadside numbers (2026-11-25: a full relearn proposed Dolphin
  `stickBand` 0.291 -> 0.909, `engageRunwaySeconds` 1.795 -> 0.659, and a first `MantaSoarAITuning_BroodRush`; both
  dropped).
- **The Manta learner writes a Soar band the AI cannot fly** (`BoostStickBand` 0.025 in Bloomrush, proposed again for
  Brood Rush, 2026-11-23): the person's band is measured on the person's stick, and the AI's stick is never that
  still. This is most likely why **Bloomrush's Manta AI never soars** (0.00 of 545 samples, 76 vs 6074 volume at I1,
  2026-11-20): diagnose with `/vessel-ai` in the Manta's Soar autopilot and the learner's Manta measure together;
  whoever owns the Manta AI.
- **Corona's Manta AI does not finish** (9 blooms left at Medium I1 when the person finished in 472 s, 2026-11-21, one
  match): look at it together with the Bloomrush item; more matches before anything is written.
- **Brood Rush's AI seats took 0 broods in both matches with a result** (2026-11-23, Medium and Legendary, I2): two
  matches, so watch; if it holds, it is a `massspace-arena` balance finding for Garrett, not a number to learn.
- **The Shrike, the Meerkat and the Goby carry a borrowed boost policy that cannot act** (2026-11-22; the Goby's `SquirrelAIBoostPolicy` in Perch Run added 2026-11-24): the Shrike's
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
- **Perch Run's Goby rivals read 65% stuck at 0 u/s** (13 races, 2026-11-25; 12% on 2026-11-24): the 0 u/s says
  rival velocity is not recorded for the Goby either, so add the Goby to the recorder item above before judging it
  stuck. They finished 8 of 13 and the person won 4 of 9 at I1.
- **No measure for `RampBoostAIPolicySO` yet:** Swordfish (Brood Rush 5, Spearline 4 races waiting) and Jacks (Decoy
  Run 1, Trawl 2).
- **To build:** a Tiger Moth learner (Tapestry, Waystation, Dustup, Sirocco), a Jewel Wasp learner (Gorge) and a
  Brittle Star measure (`BrittleStarSpeedModeAIPolicySO`, Flatline 4 races waiting).
