# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-10-29 02:21 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**5 new flight(s)** since the last learn.

## Switchback

New flights: Dolphin 5.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback.asset, Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback_I1.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 10 | 7 | 58 s | 2 of 3 | 2026-10-28 21:57 |
| Dolphin | I2 | 8 | 7 | 80 s | 2 of 3 | 2026-10-29 05:11 |
| Dolphin | I3 | 2 | 0 | - | 0 of 1 | 2026-10-29 07:13 |
| Dolphin | I4 | 8 | 4 | 106 s | 1 of 3 | 2026-10-29 04:28 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
dolphin       60     11    5.9    74       5%     1%    7/60   0: 57, 7: 17, 3: 15, 6: 14
```

## Files the learners changed

- `Docs/AI_SYSTEM/LEARNED_DRIVES_Switchback.md`

## What the sweep judged (2026-10-29 02:25 UTC)

- **Switchback (Dolphin): 5 new races, all Yash's.** They were flown on `Ys-bleeding-edge` 70e0f05481 (22:52 UTC) and
  fe42ee7e95 (01:31 UTC). Both builds have the Dolphin's per-intensity drive (b1325fa576), so all five count.
  - **I4, Legendary, 2 races:** an AI won both, with 26 rings to Yash's 23 and 24.
  - **I3, Legendary, 1 race:** an AI won, with 26 rings to Yash's 24 (177 s).
  - **I2, Legendary, 1 race:** Yash won, 26 rings in 144 s. The AIs got 18 and 16. I2 is the one intensity where Yash
    keeps winning (2 of 2 since the drive changed), and the only one whose variant was learned on the retired course.
    This race is the 2nd finished I2 race on the current course: one more and the learner rewrites the I2 variant
    from it.
  - **I3, Medium, 1 race:** play was stopped after 453 s. Yash had 10 rings and the AIs 12 and 8, so it was slow for
    everyone, not an AI fault.
  - Across all 10 AI seats: 7.7 rings per minute, 6% circling, 1% stuck.
  - The AI wins at I3 and I4 against Legendary-level play. The I2 gap is a numbers gap that the next finished I2 race
    closes, so there is nothing to fix in the AI layer.
- **Branch:** `Ys-bleeding-edge` merged into `bleeding-edge` (#1633) and was deleted on the remote. The sweep restarted
  it from `bleeding-edge` under the same name, so Yash's editor keeps its branch.
