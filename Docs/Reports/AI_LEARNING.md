# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-10-29 00:22 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**1 new flight(s)** since the last learn.

## Switchback

New flights: Dolphin 1.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback.asset, Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback_I1.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 10 | 7 | 58 s | 2 of 3 | 2026-10-28 21:57 |
| Dolphin | I2 | 6 | 6 | 80 s | 1 of 1 | 2026-10-29 05:11 |
| Dolphin | I4 | 6 | 4 | 106 s | 1 of 1 | 2026-10-28 21:41 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
dolphin       48     10    5.2    74       4%     1%    3/48   0: 45, 3: 10, 7: 10, 6: 10
```

## Files the learners changed

- `Docs/AI_SYSTEM/LEARNED_DRIVES_Switchback.md`

## What the sweep judged (2026-10-29 00:25 UTC)

- **Switchback (Dolphin): 1 new I2 race, Legendary rivals.** Yash flew it on `Ys-bleeding-edge` e055988407 (load report
  21:00 UTC), which has the Dolphin's per-intensity drive (b1325fa576), so it counts.
  - Yash won: 26 of 26 rings in 120 s (13 rings per minute).
  - The best AI Dolphin was 2 rings short (24). The other got 11.
  - Across both AIs: 8.8 rings per minute, 2% circling, 1% stuck, no long stalls.
  - The AI races well. The learned numbers are the whole job; there is no AI-layer fix to make.
  - It is the first I2 race on the current course: the 5 earlier I2 races were on the retired course. The I2 variant
    stays as written until 3 finished races on this course exist (2 to go).
