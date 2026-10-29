# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-10-29 01:21 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**1 new flight(s)** since the last learn.

## Switchback

New flights: Dolphin 1.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback.asset, Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback_I1.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 10 | 7 | 58 s | 2 of 3 | 2026-10-28 21:57 |
| Dolphin | I2 | 7 | 6 | 80 s | 1 of 2 | 2026-10-29 05:11 |
| Dolphin | I4 | 6 | 4 | 106 s | 1 of 1 | 2026-10-28 21:41 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
dolphin       50     10    5.5    74       4%     1%    4/50   0: 47, 3: 11, 6: 11, 7: 10
```

## Files the learners changed

- `Docs/AI_SYSTEM/LEARNED_DRIVES_Switchback.md`

## What the sweep judged (2026-10-29 01:25 UTC)

- **Switchback (Dolphin): 1 new I2 race, Medium rivals, Caleb's.** His editor's only load report around the race
  names `bleeding-edge` 4244a38a51, which has the Dolphin's per-intensity drive (b1325fa576), so it counts.
  - An AI Dolphin won: 26 of 26 rings in 170 s.
  - Caleb was one ring short (25). The other AI got 17.
  - Across both AIs: 7.6 rings per minute, 3% circling, 1% stuck.
  - Medium AIs beat a person here, and at Legendary the best AI was 2 rings behind Yash: the learned numbers are the
    job, and there is nothing to fix in the AI layer.
  - Caleb's race did not finish (one gate left), so it does not count toward the I2 learner's 3 finished races: still
    1 of 3.
