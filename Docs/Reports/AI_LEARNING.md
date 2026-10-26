# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-10-26 05:22 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**1 new flight(s)** since the last learn.

## Cleave

New flights: Rhino 1.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Rhino | I1 | 1 | 0 | - | - | 2026-10-25 20:50 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Files the learners changed

- `Docs/AI_SYSTEM/LEARNED_DRIVES_Cleave.md`

## What the sweep judged (2026-10-26 05:21 UTC)

- **Cleave (Rhino):** one unfinished I1 race with no AI rivals in it. Nothing to judge; the learner needs three
  finished races.
