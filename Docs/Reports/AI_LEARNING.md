# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-11-20 22:23 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**626 new flight(s)** since the last learn.

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

New flights: Manta 8.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/MantaSoarAITuning_Bloomrush.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Manta | I1 | 6 | 0 | - | 1 of 1 | 2026-11-20 21:47 |
| Manta | I3 | 2 | 0 | - | - | 2026-10-26 04:52 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Breakwater

New flights: Sparrow 13.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Sparrow | I1 | 6 | 4 | 84 s | - | 2026-10-31 19:21 |
| Sparrow | I2 | 1 | 0 | - | - | 2026-10-27 18:29 |
| Sparrow | I3 | 4 | 3 | 128 s | 3 of 4 | 2026-11-20 16:00 |
| Sparrow | I4 | 2 | 2 | 89 s | - | 2026-10-20 16:35 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
sparrow       22     16    8.2    54      11%     5%    1/22   0: 26, 1: 16, 2: 9, 5: 8
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

New flights: Dolphin 2, Manta 9, Sparrow 3, Squirrel 1, Urchin 2.

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
| Sparrow | I2 | 3 | 0 | - | - | 2026-11-13 20:25 |
| Squirrel | I2 | 1 | 0 | - | - | 2026-11-10 06:22 |
| Urchin | I2 | 2 | 0 | - | - | 2026-11-13 20:13 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## BroodRush

New flights: Dolphin 7, Manta 4, Rhino 1, Sparrow 2, Swordfish 5.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_BroodRush.asset.
- `learn_drives_from_flights --check`: OK. check: OK

- **Swordfish: measured, not written** - no measure for RampBoostAIPolicySO yet (its policy SwordfishAIBoostPolicy).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I2 | 7 | 0 | - | 0 of 2 | 2026-11-20 06:47 |
| Manta | I1 | 3 | 0 | - | 1 of 1 | 2026-11-02 16:18 |
| Manta | I2 | 1 | 0 | - | 0 of 1 | 2026-11-13 12:44 |
| Rhino | I1 | 1 | 0 | - | 1 of 1 | 2026-11-02 16:21 |
| Sparrow | I2 | 2 | 0 | - | 1 of 2 | 2026-11-14 21:35 |
| Swordfish | I2 | 5 | 0 | - | 0 of 2 | 2026-11-19 20:17 |

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

New flights: Sparrow 6.

- **Sparrow: measured, not written** - SparrowAIBoostPolicy is switched off in DogFight (disabledInModes).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Sparrow | I1 | 6 | 0 | - | 1 of 1 | 2026-11-20 15:56 |

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

New flights: BrittleStar 3.

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

New flights: Grizzly 8.

- **Grizzly: measured, not written** - its AI bombs through GrizzlyTriggerBombConfigSO's autopilot fields, which do not load per mode, and jtgjones's grizzly-v3 is reworking the bomb triggers (15 unmerged commits): learn it once that lands.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Grizzly | I1 | 8 | 0 | - | - | 2026-11-02 11:44 |
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

New flights: Rhino 3.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Rhino | I1 | 2 | 0 | - | - | 2026-11-17 18:25 |
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

## Rampage

New flights: Dolphin 2.

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

## Regatta

New flights: Dolphin 18, Manta 21, Rhino 10, Scarab 3, Serpent 2, Sparrow 65, Squirrel 57, Stoat 14, Urchin 7.

**On hold, not written:** the AIs train in their own minigames; Regatta is rebuilt from those skills later (Yash, 2026-10-27).


- **Stoat: measured, not written** - its ability is still being made (Yash, 2026-11-10: "The stoat is not ready, ignore its AI, I'm trying to make the Stoat ability better"): reported, never written, until Yash says the kit is ready.
- **Urchin: measured, not written** - the Urchin's riding is its modes' scene fields, not a drive asset (reported).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 3 | 0 | - | 0 of 1 | 2026-11-03 11:44 |
| Dolphin | I2 | 10 | 0 | - | 0 of 5 | 2026-11-20 06:35 |
| Dolphin | I3 | 1 | 0 | - | 0 of 1 | 2026-10-30 20:08 |
| Dolphin | I4 | 4 | 0 | - | 0 of 4 | 2026-11-06 15:10 |
| Manta | I1 | 8 | 5 | 29 s | 0 of 1 | 2026-11-03 10:32 |
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
thresher       9     24   26.5   283       0%     0%    7/9    4: 2, 1: 1
urchin        70     24   23.3   300       0%     3%   39/70   2: 5, 3: 5, 1: 4, 4: 4
shrike         1     20   19.1     0       0%    66%    0/1    
brittlestar     1     20   19.1   220       0%     0%    0/1    
swordfish      1     17   16.3   127       0%     0%    0/1    
squirrel      72     17   13.7   150       0%     1%   15/72   2: 10, 3: 8, 1: 7, 4: 7
scarab        13     23   12.9   323       1%     1%    6/13   1: 11, 7: 9, 6: 9, 0: 7
tigermoth      8     10   12.1    68       3%    14%    0/8    5: 2, 0: 2
stoat         26     19   11.9   138       1%     1%    8/26   7: 15, 6: 15, 3: 14, 4: 13
serpent       32     14   11.3   158       1%     0%    2/32   6: 11, 7: 8, 0: 8, 3: 6
sparrow       24     14    9.9   106       0%     4%    1/24   1: 14, 5: 13, 2: 11, 4: 10
rhino         63     11    9.4   119       1%     1%    7/63   5: 32, 4: 31, 2: 29, 1: 29
manta         99     13    9.1   238       0%     1%    7/99   3: 53, 1: 39, 4: 35, 2: 33
dolphin       83     13    7.9    89       0%     8%    4/83   5: 63, 4: 49, 3: 46, 2: 37
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

New flights: Squirrel 11.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Squirrel | I1 | 10 | 0 | - | - | 2026-10-25 07:03 |
| Squirrel | I2 | 1 | 0 | - | - | 2026-11-13 19:27 |

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

New flights: Squirrel 30.

- `learn_from_flights --mode SkimRace --write`: OK. wrote Docs/AI_SYSTEM/LEARNED_FROM_FLIGHTS_SkimRace.md

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Squirrel | I1 | 13 | 0 | - | 0 of 6 | 2026-11-19 00:33 |
| Squirrel | I2 | 15 | 0 | - | 0 of 7 | 2026-11-17 02:02 |
| Squirrel | I3 | 1 | 0 | - | 0 of 1 | 2026-10-26 06:31 |
| Squirrel | I4 | 1 | 0 | - | - | 2026-10-31 17:16 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
squirrel      65      0    0.0   178       2%     2%    0/65   0: 63
```

## Slingshot

New flights: Stoat 36.

- **Stoat: measured, not written** - its ability is still being made (Yash, 2026-11-10: "The stoat is not ready, ignore its AI, I'm trying to make the Stoat ability better"): reported, never written, until Yash says the kit is ready.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Stoat | I1 | 1 | 0 | - | 0 of 1 | 2026-10-31 13:13 |
| Stoat | I2 | 15 | 1 | 75 s | 1 of 15 | 2026-11-03 04:33 |
| Stoat | I3 | 19 | 1 | 90 s | 1 of 17 | 2026-11-19 00:23 |
| Stoat | I4 | 1 | 0 | - | 0 of 1 | 2026-11-02 17:50 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
stoat         70     15    9.5   103       0%     0%   32/70   0: 71, 1: 25, 5: 25, 2: 20
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

New flights: Swordfish 1.

- **Swordfish: measured, not written** - no measure for RampBoostAIPolicySO yet (its policy SwordfishAIBoostPolicy).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Swordfish | I1 | 1 | 0 | - | - | 2026-11-16 19:48 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
swordfish      3      9    6.8    52       0%     1%    0/3    0: 4, 4: 2, 2: 1, 1: 1
```

## Switchback

New flights: Dolphin 76.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 27 | 8 | 58 s | 7 of 16 | 2026-11-20 19:54 |
| Dolphin | I2 | 19 | 12 | 80 s | 7 of 9 | 2026-11-16 21:10 |
| Dolphin | I3 | 3 | 0 | - | 0 of 2 | 2026-10-29 07:13 |
| Dolphin | I4 | 20 | 4 | 106 s | 1 of 9 | 2026-11-04 15:41 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
dolphin      136     14    7.1    78       4%     2%   27/136  0: 130, 3: 38, 7: 32, 6: 31
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

## ThresherTime

New flights: Thresher 22.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Thresher | I1 | 11 | 5 | 65 s | 4 of 7 | 2026-11-07 03:27 |
| Thresher | I2 | 8 | 2 | 106 s | 3 of 7 | 2026-11-19 02:36 |
| Thresher | I4 | 3 | 1 | 120 s | 1 of 3 | 2026-10-31 23:26 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
thresher      39     11    5.1   132       1%     0%    9/39   0: 37, 1: 32, 2: 30, 3: 25
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

- `Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_BroodRush.asset`
- `Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_BroodRush.asset.meta`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Bloomrush.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Breakwater.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Broadside.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_BroodRush.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Cleave.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Harvest.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Headlong.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Landslide.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_ScarabRally.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Scurry.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_SerpentTime.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Switchback.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Tandava.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_ThresherTime.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Windrow.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_WreckingBall.md`
- `Docs/AI_SYSTEM/LEARNED_FROM_FLIGHTS_SkimRace.md`

## What the sweep judged (2026-11-20 22:17 UTC)

The learners changed on 2026-11-19 (`author_race_lines.py`, the Angler nose follow and the Shrike seat), so every
flight was learned from again; 41 of the 626 are new since the 2026-11-16 sweep.

- **Switchback (Dolphin), 8 new I1 races (Yash, Ys-bleeding-edge 7788ad036d / 950fffd9a2):** all co-op with one Jade
  AI teammate and no rival, so the person and the AI share the time (98-117 s); they say nothing about AI against
  person. The one race on `bleeding-edge` d623b76387 (I1) the AI won in 92.9 s.
- **Skim Race (Squirrel), 3 new I1 races (Yash, bleeding-edge d623b76387):** the AIs won both that ended (49.2 s,
  47.7 s).
- **Thresher Time, 2 I2 races (Yash, Ys-bleeding-edge 698940f6e3):** one each - the AI won in 151.2 s with Yash 14
  gates short; Yash won the next in 132.1 s. Even; nothing to fix.
- **Serpent Time I1 and Breakwater I3 (Yash, Ys-bleeding-edge 7788ad036d):** the AI won both (73.0 s; 129.9 s).
- **Brood Rush (Dolphin I2, Yash, bleeding-edge 99f24a9bb8):** the AI won 3 broods to Yash's 0. The learner wrote the
  Dolphin's FIRST Brood Rush variant (`DolphinAIBoostPolicy_BroodRush.asset`, 4 complete matches): kept. Brood Rush's
  controller does not steer the AI (no `SetExternalTargetProvider`; the AI seeks crystals as the learner measures),
  and the Dolphin drive is not disabled in mode 38. Its `alignDegrees` is clamped to 90 (the person discharges 111 deg
  off the nearest crystal, i.e. aims at the nucleus, not crystals), so the AI discharges more freely than in races.
- **Bloomrush (Manta I1, Yash, Ys-bleeding-edge c60a3f413a): the AI lost 76 volume to 6074** and soared 0.00 of its
  545 samples (the person 0.13). The learned Soar numbers already equal the person's and did not change: numbers will
  not fix an AI that never soars. Under "Waiting on".
- **Landslide (3) and Windrow (1), Rhino, Garrett's and belac's runs on bleeding-edge:** the person won the two that
  ended; the learner reported them (first `LEARNED_DRIVES_` docs) and wrote no asset (too few complete matches).
- Reported only: Grizzly Time (4), Jackstraw, Slingshot (Stoat), Dog Fight (Sparrow drive off there), Spearline and
  Brood Rush Swordfish (no ramp measure), and 9 Regatta races (on hold).
- `promote_trained_drives.py --check` OK; no promoted trained drive was touched. Only one asset changed (the new
  Brood Rush Dolphin variant); the rest are the learners' reports.

## Waiting on

- **Bloomrush's Manta AI never soars** (0.00 of 545 samples, 76 vs 6074 volume at I1, 2026-11-20): diagnose with
  `/vessel-ai` in the Manta's Soar autopilot before the learned numbers mean anything; whoever owns the Manta AI.
- **Headlong's Rhino AI (Garrett, the AI plan's promoted drive):** 4 of 40 rings in 146 s at Legendary I4 in the real
  game (2026-11-13); diagnose in the real game before the next round grows from this genome.
- **Hood and Jackstraw's AIs (Shombith):** report only while their controllers steer the AI.
- **Trained drives against races (Garrett, the AI plan):** when a promoted variant should learn from people again.
- The Breakwater rocket max range (about 679 u, past its 229 u reach) waits on whoever owns the Breakwater rocket.
- **Swordfish:** no measure for `RampBoostAIPolicySO` yet (Brood Rush 5, Spearline 1 races waiting).
- **To build:** a Tiger Moth learner (Tapestry, Waystation, Dustup, Sirocco) and a Jewel Wasp learner (Gorge).
