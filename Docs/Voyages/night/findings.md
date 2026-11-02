# Night findings log

## Baseline (bleeding-edge + compile fix, 3x Sparrow, 1 run each, time scale 3)
| d | result | hull min | where |
|---|---|---|---|
| 1 | survived | 95% | - |
| 2 | survived | 24% | The Maw |
| 3 | survived | 30% | The Maw |
| 4 | overrun | 27% | The Maw (leg 5) |
| 5 | overrun | 23% | The Maw (leg 5) |
| 6 | overrun | 32% | The Boneyard (leg 3) |
| 7 | overrun | 45% | The Boneyard |
| 8 | overrun | 51% | The Boneyard |
| 9 | overrun | 69% | Scarab Reef (leg 2) |

Kills outside The Swarm are 0-1 per world at every difficulty: Sparrow AI flies at hearts, which only kills for the Squirrel
(vessel_kits_ai.md). The escort does not decide outcomes; Boneyard threats peak 82-157 with 0 kills.

## Verify sims on voyage-night + verify (2026-10-31 evening, one run each)
- d4 Sparrow x3: survived at 4% hull, 91 kills on Ark attackers, 17 friendly-fire plates (Scarab Reef latchers took 119 plates).
- d3 Dolphin x3: survived at 88% hull, 194 kills on Ark attackers, 0 friendly fire, 1 world pacified.
- d6 Sparrow+Squirrel+Dolphin: survived at 2% hull, 270 kills on Ark attackers, 0 friendly fire; Scarab latches 85, Boneyard wave at 213 s.
- Worm lunges and leech sips still 0.
