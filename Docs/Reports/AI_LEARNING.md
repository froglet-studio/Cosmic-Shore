# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-10-26 17:21 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**3 new flight(s)** since the last learn.

## Breakwater

New flights: Sparrow 1.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/SparrowAIBoostPolicy_Breakwater.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Sparrow | I1 | 5 | 4 | 84 s | - | 2026-10-26 03:35 |
| Sparrow | I3 | 3 | 3 | 128 s | 3 of 3 | 2026-10-26 22:04 |
| Sparrow | I4 | 2 | 2 | 89 s | - | 2026-10-20 16:35 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
sparrow       15     17    6.1    37      12%     2%    0/15   0: 20, 1: 13, 2: 9, 5: 8
```

## Cleave

New flights: Rhino 2.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Rhino | I1 | 2 | 0 | - | - | 2026-10-26 13:02 |
| Rhino | I4 | 1 | 0 | - | 1 of 1 | 2026-10-26 12:59 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Files the learners changed

- `Assets/Resources/AIBoostPolicies/SparrowAIBoostPolicy_Breakwater.asset`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Breakwater.md`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Cleave.md`

## What the sweep judged (2026-10-26 17:21 UTC)

- **Breakwater (Sparrow), I3, 22:04 (16:34 UTC): the first race on a build with the new boost bands** (Yash's editor ran
  `bleeding-edge` `c734a8871d`, which carries `f8782e92f`, the rocket learner `0bcb0525b` and the waypoint steering
  `00f2a4d41`). The AI Sparrows boosted 43-44% of the race (offline prediction for these bands: 42%; the old bands: 10%),
  threaded 22 of 29 stations at 10.3 a minute (the old bands: 6.0) and circled 3%, with no break-off. Yash finished in
  128 s (13.6 a minute, 133 u/s median, the boost held nearly all race) and won; both AIs had 7 stations left. The
  steering is fine; the remaining gap is the boost share (AI median about 70 u/s), which the next races teach.
  The rocket learner moved `rocketMaxRange` to 687 u: still past the rocket's 229 u reach (see "Waiting on").
- **Cleave (Rhino):** two of Garrett's races (`bugledger/gradi`), neither finished and no AI rivals in them: nothing
  to judge.
