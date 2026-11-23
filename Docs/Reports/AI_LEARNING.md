# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-11-23 22:21 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**39 new flight(s)** since the last learn.

## Bloomrush

New flights: Manta 4.

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

## Broadside

New flights: Scarab 1.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

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

New flights: Dolphin 2, Manta 1.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_BroodRush.asset, Assets/Resources/AIBoostPolicies/MantaSoarAITuning_BroodRush.asset.
- `learn_drives_from_flights --check`: OK. check: OK

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

## GrizzlyCharge

New flights: Grizzly 2.

- **Grizzly: measured, not written** - its AI bombs through GrizzlyTriggerBombConfigSO's autopilot fields, which do not load per mode, and jtgjones's grizzly-v3 is reworking the bomb triggers (15 unmerged commits): learn it once that lands.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Grizzly | I1 | 10 | 0 | - | - | 2026-11-23 13:47 |
| Unknown | I1 | 1 | 0 | - | - | 2026-10-17 06:02 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
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

## Regatta

New flights: Dolphin 3, Manta 1, Squirrel 11, Thresher 2.

**On hold, not written:** the AIs train in their own minigames; Regatta is rebuilt from those skills later (Yash, 2026-10-27).


The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 3 | 0 | - | 0 of 1 | 2026-11-03 11:44 |
| Dolphin | I2 | 13 | 1 | 49 s | 0 of 8 | 2026-11-23 23:25 |
| Dolphin | I3 | 1 | 0 | - | 0 of 1 | 2026-10-30 20:08 |
| Dolphin | I4 | 4 | 0 | - | 0 of 4 | 2026-11-06 15:10 |
| Manta | I1 | 9 | 5 | 29 s | 0 of 1 | 2026-11-23 05:00 |
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
| Squirrel | I1 | 48 | 2 | 104 s | 0 of 1 | 2026-11-23 05:09 |
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
thresher      12     24   24.6   216       0%     4%    7/12   4: 2, 0: 2, 6: 2, 1: 1
urchin        73     24   23.3   300       0%     3%   41/73   2: 5, 3: 5, 5: 5, 1: 4
shrike         6     21   21.9     0       1%    66%    0/6    3: 2, 5: 2, 6: 2
goby           8     19   19.2   101       2%    15%    0/8    5: 1
brittlestar     7     20   19.1   204       2%     8%    0/7    1: 3, 4: 2
swordfish      4     14   14.3   124       2%     0%    0/4    4: 2, 5: 2, 6: 2
squirrel      78     17   13.9   150       0%     2%   15/78   2: 10, 4: 9, 3: 8, 1: 7
scarab        13     23   12.9   323       1%     1%    6/13   1: 11, 7: 9, 6: 9, 0: 7
stoat         26     19   11.9   138       1%     1%    8/26   7: 15, 6: 15, 3: 14, 4: 13
serpent       32     14   11.3   158       1%     0%    2/32   6: 11, 7: 8, 0: 8, 3: 6
tigermoth     10      9   11.1    67       2%    11%    0/10   5: 4, 0: 2, 6: 2
sparrow       28     14   11.1   110       0%     5%    3/28   1: 14, 5: 13, 4: 11, 2: 11
rhino         69     11   10.8   124       1%     1%    8/69   5: 37, 4: 33, 1: 31, 2: 29
manta        103     13    9.3   238       0%     1%    7/103  3: 53, 1: 41, 4: 35, 2: 33
dolphin       89     13    8.0    95       0%     8%    5/89   5: 63, 4: 49, 3: 46, 2: 37
```

## Skein

New flights: Urchin 10.

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

## Files the learners changed

- `Docs/AI_SYSTEM/LEARNED_DRIVES_Bloomrush.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Broadside.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_BroodRush.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Hedgerow.md`

The learner also wrote `DolphinAIBoostPolicy_BroodRush.asset` and a new `MantaSoarAITuning_BroodRush.asset`; the sweep
kept neither (below), so `LEARNED_DRIVES_BroodRush.md`'s "Written" column shows what was proposed, not what is in the game.

## What the sweep judged (2026-11-23 22:21 UTC)

39 new flights since the 2026-11-22 sweep: 20 Regatta (on hold), 10 Skein, 4 Bloomrush, 3 Brood Rush, 2 Grizzly Charge
and one each of Broadside, Hedgerow and Tollway. **No asset under `Assets/Resources/AIBoostPolicies` changed**: the
learner proposed two Brood Rush changes and the sweep kept neither.

- **Brood Rush, the Manta's first variant (not kept):** the learner wrote `MantaSoarAITuning_BroodRush` with
  `BoostStickBand` 0.025 and `YawsteryStickBand` 0.375 from 3 complete matches (Caleb's I2 win today on bleeding-edge
  f4f6930898, and two from 2026-11-02 and 2026-11-13). 0.025 is the value Bloomrush's variant carries, where the AI
  soars in 0.00 of its samples: the Soar autopilot holds its triggers only while its stick is inside that band
  (`MantaAnalogTurnBoostExecutor`, default 0.35), and an AI that steers all match is never inside 2.5%. A first variant
  for a mode is checked against how the mode's AI aims before it is kept, and this one would switch the Manta's Soar
  off in Brood Rush. File and meta deleted.
- **Brood Rush, the Dolphin's variant (not kept):** the mode now has 3 WON matches, so the learner dropped from 6
  finished matches to those 3 and proposed `stickBand` 0.291 to 0.909, `engageRunwaySeconds` 1.795 to 0.659,
  `alignDegrees` 90 to 87.24 and `spendAtCharge01` 0.92 to 0.96, from 15 drift episodes over 16.8 s (84 over 70.3 s
  before). That is a threefold swing on a fifth of the evidence, in an arena game. Restored to the committed numbers.
- **Brood Rush is an arena game and the learner still writes into it.** The AI plan says no AI is trained inside an
  arena game, and `ai_learn.py`'s `HOLD` names only Regatta. Under "Waiting on" for Yash; the sweep did not change the
  tool.
- **Brood Rush, the matches themselves (I2, f4f6930898, which has the committed Dolphin variant):** Garrett's Dolphin
  match (Medium rivals, 89 s) and Caleb's Manta match (Legendary rivals, 90 s) both ended Jade 3 broods, every AI seat
  on Gold and Ruby 0. Two matches; the person's team took every brood in both, at Legendary too. Garrett's other
  Dolphin flight (169 s) was left with no result.
- **Bloomrush (Manta I1), 4 flights of 10 to 31 s:** all left early, none names a build, nothing learned (17 flights,
  still 7 finished; the variant's numbers did not move). Nothing new on "never soars".
- **Hedgerow (Serpent I1, 91 s, first flight of the mode):** a solo match, 12306 volume, no rival. Nothing written
  below 3 complete matches.
- **Broadside (Scarab I2, 12 s, Caleb):** left early, no result. Nothing written.
- **Skein (Urchin I1, 10 flights):** report only. The person won 9 of 9 with a result; the Urchin AI rows read 19.8
  rings a minute, 1% circling, 0% stuck, 10 of 26 finished. It flies the course; the scene fields are not the sweep's.
- **Grizzly Charge (2 flights)** and **Tollway (Scarab, 1 flight):** report only (the Grizzly's bomb triggers are being
  reworked; the Scarab's policy is switched off in Tollway).
- **Regatta (20 flights: Squirrel 11, Dolphin 3, Thresher 2, Manta 1 and 3 more Squirrel):** on hold, not learned from.
- `promote_trained_drives.py --check` OK; no promoted trained drive was touched.

## Waiting on

- **Brood Rush and Broadside are arena games the learner writes into** (2026-11-23, Yash's call on `ai_learn.py`,
  Garrett's on the plan): add them to `HOLD` beside Regatta, or say the committed `DolphinAIBoostPolicy_BroodRush`
  variant stays. Until then the sweep keeps no new Brood Rush or Broadside numbers.
- **The Manta learner writes a Soar band the AI cannot fly** (`BoostStickBand` 0.025 in Bloomrush, proposed again for
  Brood Rush, 2026-11-23): the person's band is measured on the person's stick, and the AI's stick is never that
  still. This is most likely why **Bloomrush's Manta AI never soars** (0.00 of 545 samples, 76 vs 6074 volume at I1,
  2026-11-20): diagnose with `/vessel-ai` in the Manta's Soar autopilot and the learner's Manta measure together;
  whoever owns the Manta AI.
- **Corona's Manta AI does not finish** (9 blooms left at Medium I1 when the person finished in 472 s, 2026-11-21, one
  match): look at it together with the Bloomrush item; more matches before anything is written.
- **Brood Rush's AI seats took 0 broods in both matches with a result** (2026-11-23, Medium and Legendary, I2): two
  matches, so watch; if it holds, it is a `massspace-arena` balance finding for Garrett, not a number to learn.
- **The Shrike and the Meerkat carry a borrowed boost policy that cannot act** (2026-11-22): the Shrike's
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
- **No measure for `RampBoostAIPolicySO` yet:** Swordfish (Brood Rush 5, Spearline 4 races waiting) and Jacks (Decoy
  Run 1, Trawl 2).
- **To build:** a Tiger Moth learner (Tapestry, Waystation, Dustup, Sirocco), a Jewel Wasp learner (Gorge) and a
  Brittle Star measure (`BrittleStarSpeedModeAIPolicySO`, Flatline 4 races waiting).
