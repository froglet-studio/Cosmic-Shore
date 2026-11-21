# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-11-21 22:19 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**18 new flight(s)** since the last learn.

## Breakwater

New flights: Sparrow 1.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Sparrow | I1 | 7 | 4 | 84 s | 0 of 1 | 2026-11-20 23:22 |
| Sparrow | I2 | 1 | 0 | - | - | 2026-10-27 18:29 |
| Sparrow | I3 | 4 | 3 | 128 s | 3 of 4 | 2026-11-20 16:00 |
| Sparrow | I4 | 2 | 2 | 89 s | - | 2026-10-20 16:35 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
sparrow       25     17    8.7    65      10%     7%    2/25   0: 27, 1: 17, 9: 9, 6: 9
```

## BroodRush

New flights: Dolphin 2, Squirrel 2.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_BroodRush.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 2 | 0 | - | 2 of 2 | 2026-11-21 06:19 |
| Dolphin | I2 | 7 | 0 | - | 0 of 2 | 2026-11-20 06:47 |
| Manta | I1 | 3 | 0 | - | 1 of 1 | 2026-11-02 16:18 |
| Manta | I2 | 1 | 0 | - | 0 of 1 | 2026-11-13 12:44 |
| Rhino | I1 | 1 | 0 | - | 1 of 1 | 2026-11-02 16:21 |
| Sparrow | I2 | 2 | 0 | - | 1 of 2 | 2026-11-14 21:35 |
| Squirrel | I1 | 2 | 0 | - | 2 of 2 | 2026-11-21 06:19 |
| Swordfish | I2 | 5 | 0 | - | 0 of 2 | 2026-11-19 20:17 |

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

## Landslide

New flights: Rhino 1.

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

## Scurry

New flights: Squirrel 1.

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

## Slingshot

New flights: Stoat 9.

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

## Files the learners changed

- `Docs/AI_SYSTEM/LEARNED_DRIVES_Breakwater.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_BroodRush.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Corona.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Landslide.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Scurry.md`

## What the sweep judged (2026-11-21 22:19 UTC)

18 new flights since the 2026-11-20 sweep; the learners did not change, so only these were learned from. No asset
under `Assets/Resources/AIBoostPolicies` changed; the learners' reports did.

- **Breakwater (Sparrow I1, Yash, Ys-bleeding-edge fbb1961a48):** the AI won in 108.3 s with Yash 2 stations short
  (27 of 29). Nothing to fix. Not a finished race, so the Sparrow variant was not rewritten.
- **Brood Rush (Dolphin and Squirrel I1, Yash, Ys-bleeding-edge 923711842b), 2 matches, 4 flights:** both seats were
  local players on Jade (ys1 and YS2), each won one 3 broods to 0, and there was no AI rival row: they say nothing
  about AI against person. The Dolphin's Brood Rush variant now reads 6 complete matches and its written numbers did
  not move (`alignDegrees` 90, `engageRunwaySeconds` 1.79, `spendAtCharge01` 0.92, `stickBand` 0.29). The Squirrel's
  first Brood Rush flights: 2 complete matches, nothing written below 3.
- **Corona (Manta I1, Andrew, bleeding-edge 9b57d5606f), the mode's first flight:** Andrew won in 472.4 s with the
  Medium AI 9 blooms short. The AI flew the whole time (mean 179 u/s, never stationary), so it is not stuck; it does
  not finish the mode's job. One match, so nothing was written (first `LEARNED_DRIVES_Corona.md`), and there is no
  Corona Soar variant to check. Same shape as Bloomrush's Manta: under "Waiting on".
- **Landslide (Rhino I1, 73 s) and Scurry (Squirrel I2, 14 s), Andrew, bleeding-edge 9b57d5606f:** both left before
  the end, no result; reported only.
- Reported only, not judged: Slingshot (Stoat, 9 races at I3 and I4, Yash; the kit is still being made) and Railrush
  (Urchin, 1, Andrew; its riding is the mode's scene fields).
- `promote_trained_drives.py --check` OK; no promoted trained drive was touched.

## Waiting on

- **Bloomrush's Manta AI never soars** (0.00 of 545 samples, 76 vs 6074 volume at I1, 2026-11-20): diagnose with
  `/vessel-ai` in the Manta's Soar autopilot before the learned numbers mean anything; whoever owns the Manta AI.
- **Corona's Manta AI does not finish** (9 blooms left at Medium I1 when the person finished in 472 s, 2026-11-21, one
  match): look at it together with the Bloomrush item; more matches before anything is written.
- **Headlong's Rhino AI (Garrett, the AI plan's promoted drive):** 4 of 40 rings in 146 s at Legendary I4 in the real
  game (2026-11-13); diagnose in the real game before the next round grows from this genome.
- **Hood and Jackstraw's AIs (Shombith):** report only while their controllers steer the AI.
- **Trained drives against races (Garrett, the AI plan):** when a promoted variant should learn from people again.
- The Breakwater rocket max range (about 679 u, past its 229 u reach) waits on whoever owns the Breakwater rocket.
- **Swordfish:** no measure for `RampBoostAIPolicySO` yet (Brood Rush 5, Spearline 1 races waiting).
- **To build:** a Tiger Moth learner (Tapestry, Waystation, Dustup, Sirocco) and a Jewel Wasp learner (Gorge).
