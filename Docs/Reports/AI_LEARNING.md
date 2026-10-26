# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-10-26 16:22 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**3 new flight(s)** since the last learn.

## SkimRace

New flights: Squirrel 1.

- `learn_from_flights --mode SkimRace --write`: OK. wrote Docs/AI_SYSTEM/LEARNED_FROM_FLIGHTS_SkimRace.md

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Squirrel | I1 | 1 | 0 | - | - | 2026-10-19 10:27 |
| Squirrel | I2 | 7 | 0 | - | - | 2026-10-25 18:22 |
| Squirrel | I3 | 1 | 0 | - | 0 of 1 | 2026-10-26 06:31 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
squirrel      19      0    0.0   183       1%     1%    0/19   0: 19
```

## ThresherTime

New flights: Thresher 1.

- `learn_drives_from_flights --write`: OK. nothing.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Thresher | I1 | 3 | 2 | 127 s | 1 of 1 | 2026-10-26 05:42 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
thresher       4     12    5.0   138       1%     0%    0/4    1: 4, 2: 4, 0: 3, 5: 2
```

## WrasseRun

New flights: Wrasse 1.

- **Wrasse: measured, not written** - its boost is the crew's: KiteNavigator spends a saved boost while the next ring is aiBoostRingDistance away (AIPilotWantsBoost, KiteNavigatorConfig), which does not load per mode; its AIPilot names no policy.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Wrasse | I1 | 8 | 1 | 94 s | 0 of 4 | 2026-10-26 05:56 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
wrasse        13     15    8.4   108       1%     1%    6/13   0: 15, 3: 6, 7: 3, 1: 3
```

## Files the learners changed

- `Docs/AI_SYSTEM/LEARNED_DRIVES_ThresherTime.md`

## What the sweep judged (2026-10-26 16:21 UTC)

Three races flown earlier reached the ledger late.

- **Thresher Time (Thresher), I1, 05:42 (00:12 UTC, on a build with the sling AI `3745db91d`):** Yash finished in 180 s
  (8 rings a minute) and won. The AI Threshers flew fast (160-177 u/s) but threaded 11-13 rings at about 4 a minute,
  planted 31-34% of the race and broke off 21-23%. Yash's own numbers name the cause: he lets go of the plant at a
  median 47 deg off the ring and holds at most 1.2 s, while the AI holds until 15 deg (or 3 s). That is what the learner
  writes (`releaseConeDegrees`, the hold), and it writes at 3 finished races; there are 2. The next finished Thresher
  race writes them; the sling code is not changed on a guess.
- **Wrasse Run (Wrasse, report only):** the AI crews keep winning (Yash 0 of 4 with a result; the AIs finished 6 of 13).
- **Skim Race (Squirrel, report only):** tuned by `author_skimrace_ai_config.py`; measured, nothing to write.
