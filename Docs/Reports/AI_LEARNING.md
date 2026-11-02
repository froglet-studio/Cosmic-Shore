# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-11-02 00:23 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**1 new flight(s)** since the last learn.

## SkimRace

New flights: Squirrel 1.

- `learn_from_flights --mode SkimRace --write`: OK. wrote Docs/AI_SYSTEM/LEARNED_FROM_FLIGHTS_SkimRace.md

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Squirrel | I1 | 7 | 0 | - | 0 of 1 | 2026-11-02 05:24 |
| Squirrel | I2 | 12 | 0 | - | 0 of 5 | 2026-10-28 00:05 |
| Squirrel | I3 | 1 | 0 | - | 0 of 1 | 2026-10-26 06:31 |
| Squirrel | I4 | 1 | 0 | - | - | 2026-10-31 17:16 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
squirrel      44      0    0.0   178       1%     1%    0/44   0: 44
```

## Files the learners changed

- `Docs/AI_SYSTEM/LEARNED_FROM_FLIGHTS_SkimRace.md`

## What the sweep judged (2026-11-02 00:25 UTC)

- **Skim Race (Squirrel), 1 new I1 race:** the skim line was relearned (`LEARNED_FROM_FLIGHTS_SkimRace.md`). Skim Race
  has no rings, so the rival table's 0 rings means nothing there. Yash has not won a Skim Race with a result yet
  (0 of 8 across I1-I3): the AI Squirrels still win. Nothing to fix in the AI layer.

## Waiting on

- **Switchback Blast Run AI (Yash, in progress):** the AI Dolphins stall at the blocked gates (the key crystal and the
  door) and reach about half the rings Yash does at Legendary I2. Numbers alone will not close that.
- The Breakwater rocket max range (about 679 u, past its 229 u reach) waits on whoever owns the Breakwater rocket.
- A Tiger Moth learner (Tapestry, Waystation) is still to build.
