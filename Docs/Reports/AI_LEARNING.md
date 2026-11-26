# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-11-26 22:22 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**11 new flight(s)** since the last learn.

## Bloomrush

New flights: Manta 3.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/MantaSoarAITuning_Bloomrush.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Manta | I1 | 18 | 0 | - | 2 of 2 | 2026-11-22 23:16 |
| Manta | I3 | 2 | 0 | - | - | 2026-10-26 04:52 |

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
| JewelWasp | I1 | 1 | 0 | - | - | 2026-11-22 20:10 |
| JewelWasp | I4 | 1 | 0 | - | - | 2026-11-16 19:37 |

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
| Grizzly | I1 | 13 | 0 | - | 2 of 2 | 2026-11-26 07:37 |
| Unknown | I1 | 1 | 0 | - | - | 2026-10-17 06:02 |

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
| Squirrel | I1 | 2 | 0 | - | - | 2026-11-25 20:24 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Marionette

New flights: JewelWasp 1.

**No learner reads these yet** (the sweep's to-do): JewelWasp.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| JewelWasp | I1 | 1 | 0 | - | - | 2026-11-22 20:11 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Switchback

New flights: Dolphin 2.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 30 | 9 | 58 s | 9 of 18 | 2026-11-20 19:54 |
| Dolphin | I2 | 19 | 12 | 80 s | 7 of 9 | 2026-11-16 21:10 |
| Dolphin | I3 | 3 | 0 | - | 0 of 2 | 2026-10-29 07:13 |
| Dolphin | I4 | 20 | 4 | 106 s | 1 of 9 | 2026-11-04 15:41 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
dolphin      139     14    7.2    83       4%     3%   28/139  0: 132, 3: 38, 7: 32, 6: 31
```

## Trawl

New flights: Jacks 1.

- **Jacks: measured, not written** - no measure for RampBoostAIPolicySO yet (its policy RhinoAIBoostPolicy).

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Jacks | I1 | 3 | 0 | - | - | 2026-11-22 20:08 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Files the learners changed

- `Docs/AI_SYSTEM/LEARNED_DRIVES_Bloomrush.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Switchback.md`

## What the sweep judged (2026-11-26)

11 new flights: 3 Bloomrush (Manta), 2 Switchback (Dolphin), 2 Grizzly Charge, 1 Gorge and 1 Marionette (Jewel Wasp),
1 Joust (Squirrel), 1 Trawl (Jacks). **No asset under `Assets/Resources/AIBoostPolicies` changed**; only the two
learned-drive docs were refreshed.

- **Bloomrush (Manta, 3 new):** the relearn wrote the same `MantaSoarAITuning_Bloomrush` numbers (`BoostStickBand` 0.03,
  `YawsteryStickBand` 0.97), so the asset is unchanged. The AI still never soars (`meanSoar` 0.00 of 576 samples
  against the person's 0.13): the Waiting on item stands; numbers will not fix it.
- **Switchback (Dolphin, 2 new, both from 2026-11-20, 1 finished at I1):** I1 alone has 2 finished races, under the 3
  that write; the mode-wide variant keeps its written values (`alignDegrees` 30.23, `engageRunwaySeconds` 1.30,
  `spendAtCharge01` 0.92, `stickBand` 0.23). Rivals finished 28 of 139 races and stall at ring 0 (new Waiting on item).
- **Grizzly Charge (2), Joust (Squirrel, switched off there), Trawl (Jacks, no `RampBoostAIPolicySO` measure):** report only.
- **Gorge and Marionette (Jewel Wasp):** no learner yet (To build).
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
  2026-11-20; again 0.00 of 576 samples on 2026-11-26): diagnose with `/vessel-ai` in the Manta's Soar autopilot and the learner's Manta measure together;
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
  Run 1, Trawl 3).
- **Switchback's Dolphin AI stalls at ring 0** (132 stalls at index 0 over 139 rival races, 28 finished, 2026-11-26):
  look at the start leg with `/vessel-ai` before more numbers are written; whoever owns the Dolphin AI in Switchback.
- **To build:** a Tiger Moth learner (Tapestry, Waystation, Dustup, Sirocco), a Jewel Wasp learner (Gorge 2, Marionette 1) and a
  Brittle Star measure (`BrittleStarSpeedModeAIPolicySO`, Flatline 4 races waiting).
