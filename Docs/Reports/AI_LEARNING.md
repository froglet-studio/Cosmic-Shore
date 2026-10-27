# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-10-27 13:34 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**1 new flight(s)** since the last learn.

## Breakwater

New flights: Sparrow 1.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/SparrowAIBoostPolicy_Breakwater.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Sparrow | I1 | 5 | 4 | 84 s | - | 2026-10-26 03:35 |
| Sparrow | I2 | 1 | 0 | - | - | 2026-10-27 18:29 |
| Sparrow | I3 | 3 | 3 | 128 s | 3 of 3 | 2026-10-26 22:04 |
| Sparrow | I4 | 2 | 2 | 89 s | - | 2026-10-20 16:35 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
sparrow       17     16    6.3    37      11%     2%    0/17   0: 22, 1: 13, 2: 9, 5: 8
```

## Files the learners changed

- `Assets/Resources/AIBoostPolicies/SparrowAIBoostPolicy_Breakwater.asset`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Breakwater.md`

## What the sweep judged (2026-10-27 13:33 UTC)

- **Breakwater (Sparrow), I2, 18:29 (12:59 UTC, `Ys-bleeding-edge` `b8d6ef57a4`, which has the new boost bands):**
  Yash left the race at 101 s with 20 of 29 stations (11.8 a minute); the AI Sparrows had 16 each (9.5 a minute),
  boosting 43-44% of the race with 1% circling, the same picture as the 2026-10-26 I3 race. Nothing to fix; the boost
  share remains the gap. The rocket learner moved `rocketMaxRange` to 679 u, still past the rocket's 229 u reach.
