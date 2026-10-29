# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-10-29 07:22 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**12 new flight(s)** since the last learn.

## Dustup

New flights: Butterfly 3.

- **Butterfly: measured, not written** - its AI's one choice, Mass or Dust, is made by each Butterfly mode's controller (ButterflyAutopilotModeDriver) in code, and the recorder logs no Dust-mode bit: nothing to learn into yet.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Butterfly | I1 | 1 | 0 | - | - | 2026-10-23 13:58 |
| Butterfly | I2 | 3 | 0 | - | - | 2026-10-29 01:46 |
| Butterfly | I4 | 1 | 0 | - | 0 of 1 | 2026-10-28 22:11 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Switchback

New flights: Dolphin 9.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback.asset, Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback_I1.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 12 | 7 | 58 s | 2 of 5 | 2026-10-29 04:18 |
| Dolphin | I2 | 16 | 8 | 80 s | 3 of 4 | 2026-10-29 08:19 |
| Dolphin | I3 | 3 | 0 | - | 0 of 2 | 2026-10-29 07:13 |
| Dolphin | I4 | 10 | 4 | 106 s | 1 of 3 | 2026-10-29 04:28 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
dolphin       95      7    1.6    68       4%     1%   10/95   0: 92, 7: 20, 3: 19, 6: 16
```

## Files the learners changed

- `Docs/AI_SYSTEM/LEARNED_DRIVES_Switchback.md`

## What the sweep judged (2026-10-29 07:25 UTC)

- **12 new flights, all Shombith's, recorded 2026-10-28 21:15-21:46 (his editor's clock) and uploaded only now.** None
  of them can be judged, and no learned number moved (no race finished).
  - **Switchback (Dolphin), 9 flights:** 7 at I2 and 2 at I4, all Medium. Every one ended "left before the end" or
    "play stopped" with 0 rings for him and for every AI. The seven I2 flights start 34 s apart and each reports
    103 s of race. That is the same picture as his 21:41 I4 flight judged on 2026-10-28 17:25 UTC: on `bleeding-edge`
    2d2d01ab0e, the recorded ring list matches no path flown. It is still under "Waiting on" (flight recording).
  - **Dustup (Butterfly), 3 flights:** I2, 50-111 s, left or stopped, no score for anyone. Report only: the Butterfly
    has no learner yet.
