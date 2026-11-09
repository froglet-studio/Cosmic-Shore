# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-11-09 22:24 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**1 new flight(s)** since the last learn.

## SkimRace

New flights: Squirrel 1.

- `learn_from_flights --mode SkimRace --write`: OK. wrote Docs/AI_SYSTEM/LEARNED_FROM_FLIGHTS_SkimRace.md

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Squirrel | I1 | 10 | 0 | - | 0 of 4 | 2026-11-02 16:04 |
| Squirrel | I2 | 13 | 0 | - | 0 of 5 | 2026-11-05 10:36 |
| Squirrel | I3 | 1 | 0 | - | 0 of 1 | 2026-10-26 06:31 |
| Squirrel | I4 | 1 | 0 | - | - | 2026-10-31 17:16 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
squirrel      54      0    0.0   160       2%     2%    0/54   0: 52
```

## Files the learners changed

- `Docs/AI_SYSTEM/LEARNED_FROM_FLIGHTS_SkimRace.md`

## What the sweep judged (2026-11-09 22:22 UTC)

- **Skim Race (Squirrel), 1 new I1 race from a new contributor (bugledger/will, flown 2026-10-28, sent now):** the
  Ruby team's AIs (WillOfFire and Andrew) won in 37.7 s; the person ("medium child", Jade) had 17 crystals left. Skim
  Race is report only; the AI wins. Nothing to fix.

## Waiting on

- **Serpent Gulag's AI (Shombith):** report only until the learner measures against the post the marksman steers to.
- **Is the 30 u shell (c459f4423f) a new Stoat kit?** If Yash says yes, `KIT_SINCE` moves to c459f4423f.
- **Keyboard squeeze (a known limit):** `autopilotHold01` learned from keyboard races would read high; nothing today.
- The Breakwater rocket max range (about 679 u, past its 229 u reach) waits on whoever owns the Breakwater rocket.
- A Tiger Moth learner (Tapestry, Waystation, Dustup) is still to build.
