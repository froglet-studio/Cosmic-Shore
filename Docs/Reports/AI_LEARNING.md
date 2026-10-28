# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-10-27 19:23 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**1 new flight(s)** since the last learn.

## SkimRace

New flights: Squirrel 1.

- `learn_from_flights --mode SkimRace --write`: OK. wrote Docs/AI_SYSTEM/LEARNED_FROM_FLIGHTS_SkimRace.md

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Squirrel | I1 | 6 | 0 | - | - | 2026-10-25 07:22 |
| Squirrel | I2 | 12 | 0 | - | 0 of 5 | 2026-10-28 00:05 |
| Squirrel | I3 | 1 | 0 | - | 0 of 1 | 2026-10-26 06:31 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
squirrel      39      0    0.0   183       1%     1%    0/39   0: 39
```

## Files the learners changed

- `Docs/AI_SYSTEM/LEARNED_FROM_FLIGHTS_SkimRace.md`

## What the sweep judged (2026-10-27 19:21 UTC)

- **Skim Race (Squirrel, report only):** one new I2 race; the AI Squirrels won it (Yash 0 of 5 at I2 with a result).
  Tuned by `author_skimrace_ai_config.py`; the skim line was re-measured.
