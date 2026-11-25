# Brood Rush lever refit at the real setup (2026-11-22)

**Committed** on claude/project-thread-nqfbgz as 2adc902632 (not pushed): `Tools/Build/brood_trail_levers.py`, 8 prefabs and ShrikeConfig.asset.

**Setup**
- Medium AI with its mistake model, 3 teams of 3.
- The 17-hull card: the 14, plus Swordfish (#2330 afterimages 1.7x), Rhino (#2329) and Manta (#2331).
- AI rows from #2332 (origin/claude/project-thread-d1elkw at 477ab6b29c): Meerkat, Sparrow, Stoat, Urchin, Dolphin (throttle x1.25) and TigerMoth (throttle 0.6 -> 1.0). Every other hull flies its shipped AI. The coordinator called this the complete set of rows that reach Brood Rush.
- Shield shells netted (7f2529504c).
- Judge: Amoebius, `brood_judge.py --difficulty`, scratch worktree.
- Medium's mistakes are re-seeded per match in the judge, but a Brood match is still not repeatable: two runs of the same seed differ, at Legendary too.

**Fit**
- 5 proportional rounds, then a final round of 96 matches.
- The final round's levers differ by at most 5% from the final levers, so the Medium column pools it with the round at the final levers (152 matches, n 76-86 a hull).
- Legendary: 56 matches, on the levers before the last ±5% step.
- Caps: trail 0.4x-2.5x of the pre-#2261 value; Shrike nest shell thickness 0.28-1.75.

| hull | lever | Medium (pooled) | Medium, final round | Legendary 3x3 | Regatta card / L5 |
|---|---|---|---|---|---|
| TigerMoth | 0.4 -> 0.4 | 1.21 ± 0.05 | 1.17 | 1.28 | - |
| Sparrow | 0.61 -> 0.4705 | 1.13 ± 0.09 | 1.16 | 1.37 | -0.1% / +0.2% |
| Shrike | nest shell 0.7 -> 1.227 | 1.11 ± 0.04 | 1.04 | 0.99 | - |
| Grizzly | 0.4 -> 0.4 | 1.04 ± 0.02 | 1.09 | 0.83 | - |
| Dolphin | 1.871 -> 1.647 HELD | 1.04 ± 0.02 | 1.00 | 0.91 | - |
| Swordfish | 1 -> 0.7268 | 1.03 ± 0.03 | 1.10 | 0.98 | - |
| Honeyguide | 1.419 -> 1.542 | 1.02 ± 0.03 | 1.03 | 0.89 | - |
| Stoat | 1.937 -> 2.286 HELD | 1.01 ± 0.03 | 0.94 | 1.47 | - |
| Orca | 0.8 -> 0.9784 HELD | 1.01 ± 0.02 | 0.97 | 1.28 | - |
| Rhino | fixed (#2329 4x3x4) | 0.99 ± 0.03 | 1.04 | 1.13 | - |
| Scarab | 1.189 -> 0.9953 | 0.99 ± 0.02 | 1.01 | 0.78 | -0.0% / -0.5% |
| Thresher | 1.147 -> 0.9141 HELD | 0.99 ± 0.02 | 1.00 | 1.00 | +0.1% / +1.1% |
| Manta | x0.856 on Min/Max (to #2331) | 0.98 ± 0.02 | 1.00 | 0.88 | 0.0% / -1.0% |
| Squirrel | 2.768 -> 2.156 | 0.97 ± 0.02 | 1.03 | 0.78 | -1.2% / +0.9% |
| Meerkat | 2.214 -> 1.924 | 0.94 ± 0.02 | 0.94 | 0.69 | - |
| Wrasse | 0.594 -> 0.4869 | 0.92 ± 0.04 | 0.88 | 0.94 | - |
| Urchin | 2.1 -> 2.5 | 0.61 ± 0.05 | 0.57 | 0.73 | -0.1% / +0.1% |

The Regatta column is the change in mean finish time: shipped shuffle, 8 courses, 9 seeds, alone, for the changed hulls the card seats. It moves by -1.2% to +1.1%, which is within noise; nothing slows.

## Outside 0.9-1.1 at Medium
- **Urchin 0.61:** its trail is at the 2.5x cap. Flag it: its Mass kit needs a lever of its own.
- **TigerMoth 1.21:** its trail is at the 0.4x cap. #2332's throttle row lifted it from about 1.0 to 1.2-1.4. Flag it: it needs a lever on its dust, or a lower throttle.
- **Sparrow 1.13 ± 0.09:** it has the highest variance in the field. Its lever is free, so take it down about 8% on the next refit.
- **Shrike 1.11 pooled:** 1.04 at its final nest.

## Held, not written: their lanes are occupied
| hull | fitted value | current value | lane occupied by |
|---|---|---|---|
| Orca | 0.9784 | 0.8 | claude/ms-ai-orca |
| Stoat | 2.2858 | 1.9373 | Yash, Ys-bleeding-edge |
| Dolphin | 1.6466 | 1.871 | claude/ms-ai-dolphin |
| Thresher | 0.9141 | 1.1475 | claude/fervent-pascal-o28ftt |

- The balance above assumes these four fitted values. As committed, the shipped game is not yet the measured field.
- Manta: x0.856 on its trail Min and Max (0.111 / 0.513), for #2331's branch.

## Legendary
**Legendary does not hold.** 7 of 17 are within the bar.
- Too high: Stoat 1.47, Sparrow 1.37, TigerMoth 1.28, Orca 1.28.
- Too low: Meerkat 0.69, Scarab and Squirrel 0.78.

The Stoat, Sparrow, Meerkat and TigerMoth have the learned rows: those rows fly very differently at skill 1.0 with no mistakes. One trail value cannot hold both difficulties, so Medium is favoured. The Legendary gap belongs to the AI rows (train or check them at Legendary too), not to the levers.

Data: refit/ (res_it1-5, res_finM2, res_finL2, final_rows.json, reg_*, fit.py, pool.py, state_final3.json).
