# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-11-07 22:19 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**2 new flight(s)** since the last learn.

## Switchback

New flights: Dolphin 2.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback.asset, Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback_I2.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 18 | 7 | 58 s | 2 of 9 | 2026-11-04 04:31 |
| Dolphin | I2 | 18 | 12 | 80 s | 7 of 8 | 2026-11-01 17:43 |
| Dolphin | I3 | 3 | 0 | - | 0 of 2 | 2026-10-29 07:13 |
| Dolphin | I4 | 20 | 4 | 106 s | 1 of 9 | 2026-11-04 15:41 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
dolphin      123     12    6.8    77       5%     1%   20/123  0: 120, 3: 36, 7: 31, 6: 28
```

## Files the learners changed

- `Docs/AI_SYSTEM/LEARNED_DRIVES_Switchback.md`

## What the sweep judged (2026-11-07 22:17 UTC)

- **Switchback (Dolphin), Blast Run, 2 new I4 races by Yash (2026-11-04 01:12 and 01:14 IST, sent late):** an AI
  Dolphin won both, all 26 rings in 128 s and 124 s (the I4 winners of 2026-11-04 took 142-202 s); the second AI had
  4 and 7 gates left, Yash 20 and 17. No stall. Nothing written: the person finished neither race.

## Waiting on

- **Is the 30 u shell (c459f4423f) a new Stoat kit?** If Yash says yes, `KIT_SINCE` moves to c459f4423f.
- **Keyboard squeeze (a known limit):** `autopilotHold01` learned from keyboard races would read high; Yash flies a
  gamepad, nothing is affected today.
- The Breakwater rocket max range (about 679 u, past its 229 u reach) waits on whoever owns the Breakwater rocket.
- A Tiger Moth learner (Tapestry, Waystation, Dustup) is still to build.
