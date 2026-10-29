# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-10-28 17:22 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**3 new flight(s)** since the last learn.

## Dustup

New flights: Butterfly 1.

- **Butterfly: measured, not written** - its AI's one choice, Mass or Dust, is made by each Butterfly mode's controller (ButterflyAutopilotModeDriver) in code, and the recorder logs no Dust-mode bit: nothing to learn into yet.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Butterfly | I1 | 1 | 0 | - | - | 2026-10-23 13:58 |
| Butterfly | I4 | 1 | 0 | - | 0 of 1 | 2026-10-28 22:11 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Switchback

New flights: Dolphin 2.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback.asset, Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback_I1.asset, Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback_I2.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 10 | 7 | 58 s | 2 of 3 | 2026-10-28 21:57 |
| Dolphin | I2 | 5 | 5 | 80 s | - | 2026-10-26 01:46 |
| Dolphin | I4 | 6 | 4 | 106 s | 1 of 1 | 2026-10-28 21:41 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
dolphin       46     10    4.3    74       4%     1%    3/46   0: 43, 6: 10, 3: 9, 7: 9
```

## Files the learners changed

- `Docs/AI_SYSTEM/LEARNED_DRIVES_Switchback.md`

## What the sweep judged (2026-10-28 17:25 UTC)

- **Switchback (Dolphin): 2 new races, neither one can be judged.** Both were Shombith's, on `bleeding-edge` 2d2d01ab0e
  (load report 16:27 UTC). That build has the per-intensity Dolphin drive (b1325fa576) and its variants. Neither race
  finished, so the learned numbers did not move; only the counts in `LEARNED_DRIVES_Switchback.md` did.
  - The I1 race lasted 5 s (he left before the end).
  - The I4 race was stopped after 116 s with 0 rings for everyone. Shombith and all three AI Dolphins each flew about
    7 km and ended about 6-7 km from the origin. None of them came within 270 u of any of the 26 recorded rings (the
    course spans about 1 km, rings are 20 u across). The recorded `ringList` (the flight has `courseSeed` 0 and
    `courseIntensity` 0) does not match the course that was flown, so the rival report's "0 rings" says nothing about
    the AI. This is a recording question, not an AI fault: see "Waiting on".
- **Dustup (Butterfly): one new I4 race, report only.** Shombith lost it (0 of 1). The Butterfly's one AI choice is
  made in code (`ButterflyAutopilotModeDriver`), so there is nothing to learn into yet; it stays under "To build".

## Waiting on

- **Switchback flight recording on `bleeding-edge` 2d2d01ab0e:** the 2026-10-28 21:41 I4 flight
  (`bugledger/shombith`) records a ring list nobody flew near. Whoever owns the flight recorder (`HumanFlightRecorder`)
  should check that a Switchback flight records the course actually spawned. Until then, Switchback races from that
  build are left out of judging.
- The earlier items still stand: the Thresher learner waits for a 3rd finished race; the Breakwater rocket max range
  (about 679 u, past its 229 u reach) waits on whoever owns the Breakwater rocket.
