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
