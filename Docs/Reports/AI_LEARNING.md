# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-11-01 23:22 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**5 new flight(s)** since the last learn.

## Switchback

New flights: Dolphin 1.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback.asset, Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback_I2.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 12 | 7 | 58 s | 2 of 5 | 2026-10-29 04:18 |
| Dolphin | I2 | 18 | 12 | 80 s | 7 of 8 | 2026-11-01 17:43 |
| Dolphin | I3 | 3 | 0 | - | 0 of 2 | 2026-10-29 07:13 |
| Dolphin | I4 | 12 | 4 | 106 s | 1 of 3 | 2026-11-02 04:09 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
dolphin       97     11    5.1    73       5%     1%   10/97   0: 94, 3: 30, 7: 27, 1: 25
```

## Tapestry

New flights: TigerMoth 3.

**No learner reads these yet** (the sweep's to-do): TigerMoth.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Butterfly | I1 | 1 | 0 | - | - | 2026-10-23 14:03 |
| TigerMoth | I1 | 3 | 0 | - | 1 of 1 | 2026-11-01 17:42 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
```

## Waystation

New flights: TigerMoth 1.

**No learner reads these yet** (the sweep's to-do): TigerMoth.

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Butterfly | I1 | 2 | 1 | 205 s | - | 2026-10-21 15:32 |
| TigerMoth | I1 | 1 | 1 | 170 s | 1 of 1 | 2026-11-01 17:45 |
| TigerMoth | I4 | 2 | 1 | 143 s | 1 of 2 | 2026-10-29 17:59 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
tigermoth      5     15    6.3    66       6%     1%    1/5    0: 5, 1: 4, 12: 4, 7: 3
butterfly      2      0    0.0    50      24%     3%    0/2    0: 2
```

## Files the learners changed

- `Docs/AI_SYSTEM/LEARNED_DRIVES_Switchback.md`

## What the sweep judged (2026-11-01 23:30 UTC)

- **Switchback (Dolphin), Blast Run I4, Legendary, 1 race:** Yash left after 68 s with 5 rings; the AIs had 4 and 4.
  Even pace for that minute; too short to judge, and no learned number moved. The Blast Run stall at the blocked gates
  (2026-11-01 19:45 UTC) is still the open question.
- **Tapestry and Waystation (Tiger Moth), 4 flights from `bugledger/owner`:** no learner covers the Tiger Moth yet.
  To build: a Tiger Moth learner, in a normal session (`AUTO_LEARNING.md` §3).

## Waiting on

- **Switchback Blast Run AI (Yash, in progress):** the AI Dolphins stall at the blocked gates (the key crystal and the
  door) and reach about half the rings Yash does at Legendary I2. Numbers alone will not close that.
- The Breakwater rocket max range (about 679 u, past its 229 u reach) waits on whoever owns the Breakwater rocket.
