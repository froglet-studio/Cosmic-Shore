# AI learning: what the last races taught

Written by `Tools/Build/ai_learn.py --run` (the scheduled AI learning sweep, `Docs/AI_SYSTEM/AUTO_LEARNING.md`) on 2026-10-29 03:21 UTC. Nobody runs it by hand: every race flown in Unity reaches it on its own.

**4 new flight(s)** since the last learn.

## Switchback

New flights: Dolphin 4.

- `learn_drives_from_flights --write`: OK. Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback.asset, Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback_I1.asset, Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback_I2.asset.
- `learn_drives_from_flights --check`: OK. check: OK

The person, every recorded race in this mode:

| Hull | Intensity | Races | Finished | Fastest | You won (matches with a result) | Last raced |
|---|---|---:|---:|---:|---:|---|
| Dolphin | I1 | 12 | 7 | 58 s | 2 of 5 | 2026-10-29 04:18 |
| Dolphin | I2 | 9 | 8 | 80 s | 3 of 4 | 2026-10-29 08:19 |
| Dolphin | I3 | 3 | 0 | - | 0 of 2 | 2026-10-29 07:13 |
| Dolphin | I4 | 8 | 4 | 106 s | 1 of 3 | 2026-10-29 04:28 |

The AI rivals in those races (`regatta_rival_report.py`; judge only races flown after the AI last changed: `git log -1 --format=%cI -- <the AI's files>`):

```
hull       races  rings  r/min   u/s  circling  stuck finished  worst rings (index: stalls)
dolphin       68     12    6.3    76       5%     1%   10/68   0: 65, 7: 20, 3: 19, 6: 16
```

## Files the learners changed

- `Assets/Resources/AIBoostPolicies/DolphinAIBoostPolicy_Switchback_I2.asset`
- `Docs/AI_SYSTEM/LEARNED_DRIVES_Switchback.md`

## What the sweep judged (2026-10-29 03:30 UTC)

- **Switchback (Dolphin): 4 new races, all Yash's.** They were flown on `Ys-bleeding-edge` fe42ee7e95 and 2d1e8726c6.
  Both have the per-intensity drive.
  - **I1, Legendary, 2 races:** AIs won both, 26 rings to Yash's 17 and 20.
  - **I3, Legendary, 1 race:** an AI won, 26 rings to Yash's 24.
  - **I2, Legendary, 1 race (08:19 IST):** Yash won, 26 rings in 288 s. The AIs threaded only 8 and 7 rings, 1.6 a
    minute, where they thread 7-9 on the shipped course. Yash's own I2 races take 120-144 s.
- **That I2 race was a Blast Run, not the shipped course,** and the learner had just learned the I2 drive from it.
  2d1e8726c6 carries Yash's Blast Run rewrite (6b1bd2f5d1: block fields in front of the blocked gates, 2026-10-29),
  and the flight could not say which fun type it was. Written into I2, it moved `alignDegrees` from 29.8 to 52.6 and
  `spendAtCharge01` from 0.71 to 0.92: numbers for weaving round blocks, flown on the plain course.
  - **Fixed (AI-learning layer):**
    - The learner now learns a Switchback drive from Baseline races only, since every fun type is its own world
      (`SwitchbackFunType`'s doc). Left-out races are counted in `LEARNED_DRIVES_Switchback.md`.
    - `HumanFlightRecorder` now writes `"funType"` for Switchback races.
    - The one Blast Run race flown before the recorder wrote the field is named in the learner
      (`NOT_SHIPPED_FAMILY`), with the evidence above.
  - The I2 drive stays as it was (2 of 3 finished Baseline races on the current course). No other Switchback asset
    changed, and `--check` passes.
- **Waiting on:** the AIs' 1.6 rings a minute in a Blast Run is the Blast Run's own question: does the Dolphin's AI
  find the key crystal and the bore through the block field? It belongs to whoever builds the Blast Run (Yash's
  6b1bd2f5d1), not to the Baseline drive. Unity check: `Docs/UNITY_VERIFICATION_CHECKLIST.md`, "Flight recordings name
  the Switchback fun type".
