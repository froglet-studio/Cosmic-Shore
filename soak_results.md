# Switchback AI prism-hit soak — real Unity results

**What was measured:** Unity 6000.3.17f1 run with `-batchmode -nographics` (FMOD set to NOSOUND, offline local host). The Switchback scene had 3 AI Dolphins, 1 race per combo. Hits come from the in-game counter (`[Switchback AI] race summary` / `aidiag hits`) and are split by source: course prisms, moving prisms, rival trails and own trail. Only intensity 1 was measured, at D1, D2 and D4. All other combos were not run because the box kept suspending.

- **before** = code at 8dc0cbc02.
- **after** = code at 9a03fe6f7: the emergency sweep runs every frame, the sweep radius is wider (×0.4 instead of ×0.3), the strike limit applies only while the key crystal exists, and EmergencySeconds is longer.

### before

| I | D | pilot | course | mover | rival | own | total | emergencies | avoids | blasts | gates | race s |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 1 | Mark | 4 | 0 | 0 | 1 | **5** | 15 | 11 | 0 | 26 | 191 |
| 1 | 1 | WillOfFire | 10 | 0 | 5 | 0 | **15** | 29 | 28 | 0 | 19 | 191 |
| 1 | 1 | Pilot5350 | 8 | 0 | 6 | 1 | **15** | 27 | 29 | 0 | 19 | 191 |
| 1 | 2 | PlanetDestroyer6 | 4 | 0 | 5 | 0 | **9** | 36 | 44 | 0 | 23 | 215 |
| 1 | 2 | Iggy | 13 | 0 | 1 | 0 | **14** | 33 | 35 | 0 | 24 | 215 |
| 1 | 2 | Pilot5350 | 12 | 0 | 2 | 4 | **18** | 29 | 37 | 1 | 26 | 215 |
| 1 | 4 | Shomboss | 5 | 0 | 3 | 2 | **10** | 39 | 36 | 0 | 26 | 251 |
| 1 | 4 | Andrew | 5 | 0 | 2 | 1 | **8** | 34 | 60 | 3 | 20 | 251 |
| 1 | 4 | Pilot5350 | 3 | 0 | 2 | 1 | **6** | 34 | 51 | 1 | 20 | 251 |

before totals per race (3 AIs summed):

| I | D | course | mover | rival | own | total | hits/min (per AI) |
|---|---|---|---|---|---|---|---|
| 1 | 1 | 22 | 0 | 11 | 2 | **35** | 3.67 |
| 1 | 2 | 29 | 0 | 8 | 4 | **41** | 3.81 |
| 1 | 4 | 13 | 0 | 7 | 4 | **24** | 1.91 |

### after

| I | D | pilot | course | mover | rival | own | total | emergencies | avoids | blasts | gates | race s |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 1 | Iggy | 7 | 0 | 0 | 3 | **10** | 24 | 25 | 0 | 19 | 177 |
| 1 | 1 | Cassian | 11 | 0 | 0 | 2 | **13** | 19 | 14 | 0 | 26 | 177 |
| 1 | 1 | Pilot5350 | 9 | 0 | 2 | 1 | **12** | 26 | 22 | 0 | 22 | 177 |
| 1 | 2 | Ian | 6 | 0 | 0 | 0 | **6** | 30 | 25 | 0 | 26 | 220 |
| 1 | 2 | FriendlyAI | 6 | 0 | 3 | 2 | **11** | 35 | 50 | 1 | 22 | 220 |
| 1 | 2 | Pilot5350 | 12 | 0 | 2 | 3 | **17** | 38 | 43 | 0 | 21 | 220 |
| 1 | 4 | Ian | 5 | 0 | 0 | 2 | **7** | 59 | 58 | 1 | 18 | 276 |
| 1 | 4 | FriendlyAI | 3 | 0 | 1 | 0 | **4** | 53 | 46 | 0 | 18 | 276 |
| 1 | 4 | Pilot5350 | 10 | 0 | 0 | 3 | **13** | 45 | 32 | 0 | 26 | 276 |

after totals per race (3 AIs summed):

| I | D | course | mover | rival | own | total | hits/min (per AI) |
|---|---|---|---|---|---|---|---|
| 1 | 1 | 27 | 0 | 2 | 6 | **35** | 3.96 |
| 1 | 2 | 24 | 0 | 5 | 5 | **34** | 3.09 |
| 1 | 4 | 18 | 0 | 1 | 5 | **24** | 1.74 |

## Verdict
With N=1 race per combo, the after soak only got through I1D1. The total there was the same as before (35 vs 35). Rival-trail hits fell from 11 to 2, but course-prism hits rose from 22 to 27. **The 9a03fe6f7 fixes have not shown an overall reduction yet.** Course prisms are the main source (about 65% of hits). Next step: make course-prism avoidance look further ahead, then run a full I1–4×D1–4 soak with N≥3.

## Per-hit diagnosis (real Unity, I1 D1, 3 AI Dolphins, 1 race, code 9a03fe6f7 + per-hit log)
The log line is `[Switchback AI] hit pilot=… src=… speed drifting boosting emerg sinceEmerg sinceSense avoid plan phase angle dist scale`. That race had 56 hits: course 38, rival 7, own 11.
- **Course (38):** in 35 of the 38 the emergency dodge was already active (`emerg=True`), with an avoid running and a Thread plan. Drift was on in only 1, and no blast plan was involved. Contact angles were 40–110° off the nose, so these are side-swipes, not head-on hits. About half were the long 3×3×23–54 rods. The hard emergency break (up to about 90° lateral) swings the hull into the prism next to the one it is dodging. The sweep also skips prisms already beside the hull (start overlap), and the sense pass sizes a rod from its centre with an extent capped at 12, so a 54 u rod looks small.
- **Repeat stall hits:** many course hits come in pairs 0.2–0.6 s apart, the second at a speed of 1–35 after the first impact.
- **Own trail (11):** every one came at a speed of 1–9 and 100–170° off the nose. The ship had already crashed and slowed, then backed into its own fresh trail. These come from crashes, not from the self-trail filter.
- **Fix written, not yet measured in Unity:** see `soak_SwitchbackBlastAI.fix.diff`. It sizes rods from their closest point, uses the contact point and not the collider centre, pushes off prisms beside the hull, and picks a clear escape direction. The run failed because the disk filled up (125 of 125 GB, "No space left on device").

## Before (committed code bd653b110), real Unity, 3 AI Dolphins, hits summed over the 3 AIs per race
| Combo | Races | Per-race totals | Course / Mover / Rival / Own (all races) | Mean per race |
|---|---|---|---|---|
| I1 D1 | 2 | 33, 36 | 50 / 0 / 8 / 11 | **34.5** |
| I1 D4 | 2 | 38, 9 | 24 / 0 / 13 / 10 | **23.5** |
| I3 D4 | 1 (stopped at 420 s) | 34 | 18 / 0 / 5 / 11 | **34** |
| I3 D1 | 1 (Unity crashed at ~347 s) | ~60 (per-hit log) | — | **~60** |

## After: side-swipe fix (real Unity, I1 D1, 2 races per row)
| Version | Course | Mover | Rival | Own | Total (2 races) | Mean per race | vs before 34.5 |
|---|---|---|---|---|---|---|---|
| fix 1: closest-point rods, contact-point escape, clear-escape search, beside-hull push | 76 | 0 | 10 | 16 | 102 | **51.0** | worse |
| fix 2: fix 1 without the beside-hull push | 39 | 0 | 18 | 20 | 77 | **38.5** | worse |

In fix 1's per-hit log, 21 of 76 course hits came at a speed under 30. The beside-hull push fired the whole time a ship threaded an eye, so ships stalled and bounced. Fix 2 cut course hits from 50 to 39 (−22%), but rival and own-trail hits rose, so the total still went up. **Neither fix is applied to the game code.** I stopped after I1 D1 and did not run I1 D4, I3 D1 or I3 D4, because both versions were already worse there. Fix 2's source is kept in `soak_SwitchbackBlastAI.fix2.cs.txt` for the next attempt. Its lead: keep fix 2's course-prism changes and look at why own-trail and rival hits rise when the escape direction changes.
