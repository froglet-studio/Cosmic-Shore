# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-10-25 23:40 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**167 new flight(s)** since the last learn.

## Bloomrush

New flights: Manta 7.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/MantaSoarAITuning_Bloomrush.asset.
- `learn_drives_from_flights --check`: OK. check: OK

## Breakwater

New flights: Sparrow 7.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | Last raced |
|---|---|---:|---:|---:|---|
| Sparrow | I1 | 5 | 4 | 84 s | 2026-10-26 03:35 |
| Sparrow | I4 | 2 | 2 | 89 s | 2026-10-20 16:35 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
sparrow        9     12    6.1    37      41%     3%    0/9    0: 11, 1: 6, 3: 3, 5: 2
```

## Broadside

New flights: Manta 2.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

## DogFight

New flights: Sparrow 4.

- **Sparrow: measured, not written** - SparrowAIBoostPolicy is switched off in DogFight (disabledInModes).

## Dustup

New flights: Butterfly 1.

- **Butterfly: measured, not written** - its AI's one choice, Mass or Dust, is made by each Butterfly mode's controller (ButterflyAutopilotModeDriver) in code, and the recorder logs no Dust-mode bit: nothing to learn into yet.

## GrizzlyCharge

New flights: Grizzly 7.

- **Grizzly: measured, not written** - its AI bombs through GrizzlyTriggerBombConfigSO's autopilot fields, which do not load per mode, and jtgjones's grizzly-v3 is reworking the bomb triggers (15 unmerged commits): learn it once that lands.

## Hijack

New flights: Urchin 1.

- **Urchin: measured, not written** - the Urchin's riding is its modes' scene fields, not a drive asset (reported).

## Joust

New flights: Squirrel 1.

- **Squirrel: measured, not written** - SquirrelAIBoostPolicy is switched off in Joust (disabledInModes).

## Regatta

New flights: Dolphin 1, Manta 13, Rhino 6, Scarab 3, Serpent 2, Sparrow 6, Squirrel 49, Stoat 2, Urchin 6.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/MantaSoarAITuning_Regatta.asset.
- `learn_drives_from_flights --check`: OK. check: OK
- `author_race_lines --check`: OK. author_race_lines --check: OK (7 hulls, 4 learned)
- `learn_from_flights --write`: OK. wrote Docs/AI_SYSTEM/LEARNED_FROM_FLIGHTS.md

- **Urchin: measured, not written** - the Urchin's riding is its modes' scene fields, not a drive asset (reported).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | Last raced |
|---|---|---:|---:|---:|---|
| Dolphin | I1 | 1 | 0 | - | 2026-10-22 23:55 |
| Manta | I1 | 6 | 5 | 29 s | 2026-10-20 17:33 |
| Manta | I3 | 2 | 1 | 51 s | 2026-10-20 17:48 |
| Manta | I4 | 5 | 3 | 75 s | 2026-10-20 17:45 |
| Rhino | I1 | 1 | 1 | 36 s | 2026-10-20 17:54 |
| Rhino | I3 | 5 | 1 | 61 s | 2026-10-20 18:02 |
| Scarab | I2 | 3 | 0 | - | 2026-10-25 21:56 |
| Serpent | I3 | 2 | 1 | 164 s | 2026-10-17 04:54 |
| Sparrow | I1 | 3 | 1 | 64 s | 2026-10-23 00:24 |
| Sparrow | I3 | 1 | 0 | - | 2026-10-23 00:14 |
| Sparrow | I4 | 2 | 0 | - | 2026-10-23 00:10 |
| Squirrel | I1 | 38 | 2 | 104 s | 2026-10-17 01:34 |
| Squirrel | I3 | 11 | 4 | 117 s | 2026-10-17 01:44 |
| Stoat | I2 | 2 | 0 | - | 2026-10-26 00:13 |
| Unknown | I1 | 4 | 0 | - | 2026-10-16 17:39 |
| Urchin | I1 | 2 | 2 | 55 s | 2026-10-17 03:18 |
| Urchin | I3 | 4 | 1 | 76 s | 2026-10-20 17:51 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

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

New flights: Scarab 2.

- **Scarab: measured, not written** - ScarabAIBoostPolicy is switched off in ScarabScramble (disabledInModes).

## Scurry

New flights: Squirrel 9.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

## Skein

New flights: Urchin 1.

- **Urchin: measured, not written** - the Urchin's riding is its modes' scene fields, not a drive asset (reported).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | Last raced |
|---|---|---:|---:|---:|---|
| Unknown | I1 | 1 | 0 | - | 2026-10-16 13:09 |
| Unknown | I3 | 1 | 0 | - | 2026-10-16 18:29 |
| Urchin | I3 | 1 | 0 | - | 2026-10-16 18:35 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
urchin         3      4    1.7    64      21%     1%    0/3    1: 1, 2: 1, 3: 1, 4: 1
```

## SkimRace

New flights: Squirrel 7.

- `learn_from_flights --mode SkimRace --write`: OK. No Skim Race Squirrel flights with their track yet (the recorder copies Skim Race's track since 2026-10-26).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | Last raced |
|---|---|---:|---:|---:|---|
| Squirrel | I1 | 1 | 0 | - | 2026-10-19 10:27 |
| Squirrel | I2 | 6 | 0 | - | 2026-10-22 22:37 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
squirrel      15      0    0.0   183       8%     1%    0/15   0: 15
```

## Switchback

New flights: Dolphin 15.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback.asset, Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback_I1.asset, Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback_I2.asset, Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback_I4.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | Last raced |
|---|---|---:|---:|---:|---|
| Dolphin | I1 | 6 | 5 | 60 s | 2026-10-26 04:06 |
| Dolphin | I2 | 5 | 5 | 80 s | 2026-10-26 01:46 |
| Dolphin | I4 | 4 | 3 | 120 s | 2026-10-26 04:09 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
dolphin       32     10    3.7    75      19%     1%    2/32   0: 32, 6: 8, 3: 7, 7: 7
```

## Tapestry

New flights: Butterfly 1.

- **Butterfly: measured, not written** - its AI's one choice, Mass or Dust, is made by each Butterfly mode's controller (ButterflyAutopilotModeDriver) in code, and the recorder logs no Dust-mode bit: nothing to learn into yet.

## ThresherTime

New flights: Thresher 2.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | Last raced |
|---|---|---:|---:|---:|---|
| Thresher | I1 | 2 | 1 | 127 s | 2026-10-22 23:29 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
thresher       2      8    6.6   116      16%     0%    0/2    2: 2, 1: 1, 0: 1, 4: 1
```

## Warpline

New flights: Stoat 6.

**On hold, not written:** the Stoat's AI waits for Garrett's configuration (issue 1198).


The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | Last raced |
|---|---|---:|---:|---:|---|
| Stoat | I1 | 4 | 4 | 87 s | 2026-10-25 07:33 |
| Stoat | I2 | 2 | 2 | 86 s | 2026-10-25 07:38 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
stoat         12      4    2.7   144      41%     0%    0/12   0: 13, 2: 8, 3: 7, 1: 6
```

## Waystation

New flights: Butterfly 2.

- **Butterfly: measured, not written** - its AI's one choice, Mass or Dust, is made by each Butterfly mode's controller (ButterflyAutopilotModeDriver) in code, and the recorder logs no Dust-mode bit: nothing to learn into yet.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | Last raced |
|---|---|---:|---:|---:|---|
| Butterfly | I1 | 2 | 1 | 205 s | 2026-10-21 15:32 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
butterfly      2      0    0.0    50      44%     3%    0/2    0: 2
```

## WrasseRun

New flights: Wrasse 3.

- **Wrasse: measured, not written** - its boost is the crew's: KiteNavigator spends a saved boost while the next ring is aiBoostRingDistance away (AIPilotWantsBoost, KiteNavigatorConfig), which does not load per mode; its AIPilot names no policy.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | Last raced |
|---|---|---:|---:|---:|---|
| Wrasse | I1 | 3 | 1 | 94 s | 2026-10-22 23:13 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
wrasse         3     16    8.4   113       7%     1%    2/3    0: 4, 7: 3, 3: 1
```

## WreckingBall

New flights: Scarab 1.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

## Files the learners changed

- `Assets/Resources/AIBoostPolicies/MantaSoarAITuning_Bloomrush.asset`
- `Assets/Resources/AIBoostPolicies/MantaSoarAITuning_Bloomrush.asset.meta`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Bloomrush.md`

## What the sweep judged (2026-10-25 23:30-00:15 UTC)

Only races flown after the AI last changed count. Flight times are the recorder's local clock (+05:30).

- **Switchback (Dolphin): fixed what numbers could not.** The three races after the steering fix and the hard turn
  (04:04-04:09, 22:34-22:39 UTC): AI Dolphins 8.8 rings/min, 0 of 6 finished, circling 23 s at I4; Yash 17-20 at I1.
  The learner's one Switchback drive was I1's numbers, which miss I4's 20 u mouths (offline: 232 s, 31 misses; with
  I4's own numbers 148 s, 12). Now one drive per intensity: `DolphinAIBoostPolicy_Switchback_I1` / `_I2` / `_I4`
  (`AIBoostPolicySO.ForMode(policy, mode, intensity)`; `MINIGAME_TRAINING_PLAN.md` §3.6). Not run in Unity.
- **Breakwater (Sparrow):** its newest races (03:23, 03:35) came before the Sparrow AI changed (22:12 UTC): nothing to
  judge yet.
- **Bloomrush (Manta):** two new I3 races carry real Soaring; the Manta's Bloomrush bands are written.
- **Regatta, Skim Race, Thresher Time, Warpline, Waystation, Wrasse Run, Skein:** every race in them came before their
  AI's last change (or the hull is on hold or report only): nothing to judge yet.

## Waiting on

- Warpline: the Stoat's configuration (Garrett, issue 1198).
- Grizzly Charge: jtgjones's `grizzly-v3` bomb rework.

## To build

- Butterfly (Dustup, Tapestry, Waystation): the recorder logs no Dust-mode bit, so its one choice cannot be learned.
- Urchin, Wrasse: their riding / crew boost do not load per mode.
