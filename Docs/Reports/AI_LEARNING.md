# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-10-26 01:21 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**9 new flight(s)** since the last learn.

## Warpline

New flights: Stoat 5.

**On hold, not written:** the Stoat's AI waits for Garrett's configuration (issue 1198).


The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Stoat | I1 | 4 | 4 | 87 s | - | 2026-10-25 07:33 |
| Stoat | I2 | 2 | 2 | 86 s | - | 2026-10-25 07:38 |
| Stoat | I3 | 5 | 5 | 79 s | 5 of 5 | 2026-10-26 06:19 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
stoat         22      4    2.6   141      42%     0%    0/22   0: 23, 2: 13, 1: 12, 3: 10
```

## WrasseRun

New flights: Wrasse 4.

- **Wrasse: measured, not written** - its boost is the crew's: KiteNavigator spends a saved boost while the next ring is aiBoostRingDistance away (AIPilotWantsBoost, KiteNavigatorConfig), which does not load per mode; its AIPilot names no policy.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Wrasse | I1 | 7 | 1 | 94 s | 0 of 3 | 2026-10-26 05:56 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
wrasse        11     15    8.4   108       5%     1%    5/11   0: 12, 3: 6, 7: 3, 2: 2
```

## Files the learners changed

- none (no hull had three finished races yet, or nothing moved)

## What the sweep judged (2026-10-26 01:20 UTC)

- **Wrasse Run (Wrasse, report only):** the AI crews race well enough: they won all 3 of Yash's matches with a result
  and finished 5 of 11 at 8.4 rings/min with 5% circling. Nothing to fix.
- **Warpline (Stoat, on hold):** five new I3 races, all won by Yash (79 s best); the AI Stoats threaded 4 rings at
  2.6 a minute, circled 42% of the race and finished none of 22. Their AI waits for Garrett's configuration (issue
  1198), so the sweep does not re-tune it: these races are the evidence for that work.
