# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-10-26 20:23 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**12 new flight(s)** since the last learn.

## Scurry

New flights: Squirrel 1.

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

## SkimRace

New flights: Squirrel 5.

- `learn_from_flights --mode SkimRace --write`: OK. wrote Docs/AI_SYSTEM/LEARNED_FROM_FLIGHTS_SkimRace.md

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Squirrel | I1 | 6 | 0 | - | - | 2026-10-25 07:22 |
| Squirrel | I2 | 7 | 0 | - | - | 2026-10-25 18:22 |
| Squirrel | I3 | 1 | 0 | - | 0 of 1 | 2026-10-26 06:31 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
squirrel      29      0    0.0   174       1%     1%    0/29   0: 29
```

## Switchback

New flights: Dolphin 1.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback.asset, Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback_I1.asset, Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback_I2.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 9 | 7 | 58 s | 2 of 3 | 2026-10-26 05:27 |
| Dolphin | I2 | 5 | 5 | 80 s | - | 2026-10-26 01:46 |
| Dolphin | I4 | 5 | 4 | 106 s | 1 of 1 | 2026-10-26 22:00 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
dolphin       40     11    5.7    78       5%     1%    3/40   0: 40, 6: 10, 3: 9, 7: 9
```

## Warpline

New flights: Stoat 5.

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

## Files the learners changed

- `Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback.asset`
- `Assets/Resources/AIBoostPolicies/StoatDipoleConfig_Warpline.asset`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Scurry.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Switchback.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Warpline.md`

## What the sweep judged (2026-10-26 20:21 UTC)

- **Warpline (Stoat), I4: the retrain works.** Yash's four races at 01:22-01:28 (19:52-19:58 UTC) ran on
  `Ys-bleeding-edge` `e115d53769`, which carries `f442a7e0a` (the Regatta steering and the learned pair and sling
  numbers). **An AI Stoat won three of the four** (81-86 s; Yash won the first in 77 s, the AIs one and two gates
  short). The AIs threaded 9-10 gates at 6-7 a minute with no circling and no break-off; before the retrain they
  finished 0 of 30. Nothing to fix; the learners refreshed `StoatDipoleConfig_Warpline` from the new races (pairs from
  151 u, held 1.3-2.1 s, at most 83 deg round the sink).
- **Switchback (Dolphin), I4, 22:00:** one race; the mode-wide drive's cone moved to 31 deg (the per-intensity
  variants are unchanged).
- **Skim Race (Squirrel, report only):** four races, measured; tuned by `author_skimrace_ai_config.py`.
