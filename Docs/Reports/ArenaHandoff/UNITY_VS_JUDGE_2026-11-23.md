# Night 4: does the Amoebius judge match real Unity? (Regatta 11 hulls, Broadside 8 hulls)

Run on Gbox, 2026-11-23. Unity 6000.3.17f1 batchmode, one editor, 07:30 to about 10:10 UTC, engine and game at bleeding-edge 910c2778a8.
**All 60 planned Unity matches ran and finished; none was skipped or faulted.** The editor exited on its own; no editor of mine is running.

## Verdict

- **Regatta: the judge matches Unity.** Per hull, over both courses, Unity / judge mean is 0.92 to 1.09 for all 11 hulls at both starts, inside the 1.10x bar. Many seed-1 races land within a second of the judge's own seed 1 (Urchin 63.5 vs 63.3, Sparrow 74.4 vs 74.4, Squirrel 58.5 vs 58.8, Sparrow Ref4 71.2 vs 71.1, Scarab Ref4 74.1 vs 73.9), which says it is the same simulation, not just the same averages.
- The single-race outliers sit inside the judge's own seed-to-seed spread: Shrike Ref1 rest 79.2 s against a judge range of 58.9 to 63.5 is the only one clearly outside it, and Shrike was the one race that dropped to 55 fps. Shrike Ref4 L5 went the other way (59.7 vs 68.4). One seed each, so worth a re-run, not a conclusion.
- **The fleet spread is the same in both**: one seed in Unity gives 1.22x (rest) and 1.27x (L5); one seed in the judge gives 1.26x and 1.34x. The judge's five-seed means give 1.15x and 1.22x. So the judge's balance verdict on Regatta holds in Unity.
- **Broadside: consistent but too noisy to call per hull with 6 matches.** On the same 6 lineups the order agrees at the ends: Orca top (Unity 1.50, judge 1.27) and Meerkat bottom (0.60, 0.70) at rest; Stoat bottom at L5 (0.40, 0.52). The biggest gap, Grizzly at L5 (0.65 vs 0.95), is about 1.2 standard errors. The judge's own two lineup sets disagree with each other by as much (Stoat rest 1.11 vs 0.71), so lineups, not engine, dominate these numbers. A per-hull Broadside call needs far more than 6 matches in either engine.
- **Frame rate**: Unity Broadside ran at 39 to 42 fps on Gbox, Regatta at 55 to 60. The tick was pinned to 1/60 s, so game time was unaffected. The unpinned probe ran at a steady 60 fps, so it could not reproduce the Dolphin frame-rate drift the Brood check found: Dolphin unpinned 57.6 s vs pinned 60.3 s, the other three within 1 s (Sparrow 64.6 vs 74.4 is the exception, one seed).

## What was compared

- Regatta: each hull alone, Legendary AI, courses Ref1 (Course3, I2) and Ref4 (Course8, I1, rails) from the shuffle pool, starts at rest and at the L5 card row. Unity ran seed 1; the judge ran seeds 1-5.
- Broadside: 4v4, 240 s on time (points per pilot raised to 100000), Legendary, the same 6 lineups and seeds per start in both engines (seeds 41 rest, 42 L5), plus 12 more judge matches per start.
- Differences to know: in Unity the Regatta seat raced as Ruby, in the judge as Blue (no element table at rest; at L5 the card row is the same). The host flew Squirrel in both. Unity was seeded the judge's way, but its RNG streams are not the judge's, so seed 1 is not the same race in both.

## Full tables

## Regatta (each hull alone, Legendary; Unity seed 1 at a pinned 1/60 s tick; judge seeds 1-5)

| Hull | Course | Start | Unity s | Judge seed 1 | Judge mean (1-5) | Unity / judge mean | Unity fps |
|---|---|---|---|---|---|---|---|
| Urchin | Ref1 | rest | 63.5 | 63.3 | 70.5 (5/5) | 0.901 | 60 |
| Scarab | Ref1 | rest | 67.9 | 67.3 | 67.4 (5/5) | 1.007 | 60 |
| Sparrow | Ref1 | rest | 74.4 | 74.4 | 66.8 (5/5) | 1.114 | 60 |
| Dolphin | Ref1 | rest | 60.3 | 48.0 | 55.4 (5/5) | 1.088 | 60 |
| Squirrel | Ref1 | rest | 58.5 | 58.8 | 58.5 (5/5) | 1.000 | 60 |
| Thresher | Ref1 | rest | 63.5 | 64.8 | 63.2 (5/5) | 1.005 | 60 |
| BrittleStar | Ref1 | rest | 65.3 | 65.9 | 65.4 (5/5) | 0.999 | 58 |
| Rhino | Ref1 | rest | 61.6 | 59.8 | 61.4 (5/5) | 1.003 | 58 |
| Swordfish | Ref1 | rest | 61.0 | 63.3 | 62.7 (5/5) | 0.973 | 59 |
| Goby | Ref1 | rest | 70.1 | 75.1 | 70.0 (5/5) | 1.002 | 60 |
| Shrike | Ref1 | rest | 79.2 | 59.2 | 61.6 (5/5) | 1.285 | 55 |
| Urchin | Ref1 | L5 | 58.8 | 58.0 | 58.1 (5/5) | 1.012 | 59 |
| Scarab | Ref1 | L5 | 58.0 | 57.7 | 57.6 (5/5) | 1.006 | 60 |
| Sparrow | Ref1 | L5 | 66.6 | 59.2 | 59.6 (5/5) | 1.117 | 60 |
| Dolphin | Ref1 | L5 | 47.0 | 41.4 | 50.7 (5/5) | 0.928 | 60 |
| Squirrel | Ref1 | L5 | 57.0 | 53.0 | 54.8 (5/5) | 1.039 | 60 |
| Thresher | Ref1 | L5 | 59.0 | 59.2 | 59.0 (5/5) | 1.001 | 60 |
| BrittleStar | Ref1 | L5 | 58.2 | 55.2 | 56.0 (5/5) | 1.040 | 60 |
| Rhino | Ref1 | L5 | 61.4 | 58.8 | 60.3 (5/5) | 1.018 | 60 |
| Swordfish | Ref1 | L5 | 56.1 | 55.3 | 55.1 (5/5) | 1.019 | 60 |
| Goby | Ref1 | L5 | 58.1 | 54.1 | 56.2 (5/5) | 1.035 | 60 |
| Shrike | Ref1 | L5 | 56.8 | 51.1 | 58.2 (5/5) | 0.976 | 60 |
| Urchin | Ref4 | rest | 81.1 | 82.1 | 80.8 (5/5) | 1.004 | 59 |
| Scarab | Ref4 | rest | 74.1 | 73.9 | 73.9 (5/5) | 1.003 | 59 |
| Sparrow | Ref4 | rest | 71.2 | 71.1 | 71.2 (5/5) | 1.001 | 60 |
| Dolphin | Ref4 | rest | 77.3 | 82.5 | 81.6 (5/5) | 0.948 | 60 |
| Squirrel | Ref4 | rest | 82.4 | 75.6 | 75.2 (5/5) | 1.095 | 60 |
| Thresher | Ref4 | rest | 75.2 | 76.4 | 74.7 (5/5) | 1.007 | 60 |
| BrittleStar | Ref4 | rest | 70.5 | 75.7 | 73.2 (5/5) | 0.962 | 59 |
| Rhino | Ref4 | rest | 83.5 | 71.2 | 81.9 (5/5) | 1.020 | 60 |
| Swordfish | Ref4 | rest | 77.3 | 79.0 | 76.8 (5/5) | 1.007 | 60 |
| Goby | Ref4 | rest | 80.2 | 83.1 | 83.9 (5/5) | 0.956 | 58 |
| Shrike | Ref4 | rest | 86.2 | 101.9 | 92.8 (5/5) | 0.929 | 59 |
| Urchin | Ref4 | L5 | 62.2 | 62.0 | 63.2 (5/5) | 0.985 | 60 |
| Scarab | Ref4 | L5 | 63.4 | 63.1 | 65.2 (5/5) | 0.972 | 60 |
| Sparrow | Ref4 | L5 | 66.4 | 65.3 | 65.4 (5/5) | 1.015 | 60 |
| Dolphin | Ref4 | L5 | 72.4 | 74.9 | 73.7 (5/5) | 0.982 | 60 |
| Squirrel | Ref4 | L5 | 74.0 | 82.5 | 81.1 (5/5) | 0.913 | 60 |
| Thresher | Ref4 | L5 | 66.8 | 64.8 | 67.2 (5/5) | 0.994 | 60 |
| BrittleStar | Ref4 | L5 | 62.7 | 62.7 | 64.1 (5/5) | 0.978 | 60 |
| Rhino | Ref4 | L5 | 89.3 | 95.1 | 88.3 (5/5) | 1.011 | 60 |
| Swordfish | Ref4 | L5 | 73.7 | 70.4 | 73.1 (5/5) | 1.007 | 59 |
| Goby | Ref4 | L5 | 64.5 | 66.0 | 64.2 (5/5) | 1.004 | 60 |
| Shrike | Ref4 | L5 | 59.7 | 67.4 | 68.4 (5/5) | 0.873 | 60 |

### Per hull, both courses (geometric mean), and the 1.10x bar

| Hull | Start | Unity | Judge seed 1 | Judge mean | Unity / judge |
|---|---|---|---|---|---|
| Urchin | rest | 71.8 | 72.1 | 75.5 | 0.951 |
| Scarab | rest | 70.9 | 70.5 | 70.6 | 1.005 |
| Sparrow | rest | 72.8 | 72.7 | 69.0 | 1.056 |
| Dolphin | rest | 68.3 | 62.9 | 67.2 | 1.016 |
| Squirrel | rest | 69.4 | 66.7 | 66.4 | 1.046 |
| Thresher | rest | 69.1 | 70.3 | 68.7 | 1.006 |
| BrittleStar | rest | 67.8 | 70.6 | 69.2 | 0.981 |
| Rhino | rest | 71.7 | 65.3 | 70.9 | 1.011 |
| Swordfish | rest | 68.7 | 70.7 | 69.4 | 0.990 |
| Goby | rest | 75.0 | 79.0 | 76.6 | 0.979 |
| Shrike | rest | 82.6 | 77.7 | 75.6 | 1.093 |
| Urchin | L5 | 60.5 | 59.9 | 60.6 | 0.998 |
| Scarab | L5 | 60.6 | 60.3 | 61.3 | 0.989 |
| Sparrow | L5 | 66.5 | 62.2 | 62.4 | 1.065 |
| Dolphin | L5 | 58.4 | 55.7 | 61.1 | 0.954 |
| Squirrel | L5 | 64.9 | 66.1 | 66.7 | 0.974 |
| Thresher | L5 | 62.8 | 61.9 | 62.9 | 0.998 |
| BrittleStar | L5 | 60.4 | 58.8 | 59.9 | 1.009 |
| Rhino | L5 | 74.0 | 74.8 | 72.9 | 1.015 |
| Swordfish | L5 | 64.3 | 62.4 | 63.5 | 1.013 |
| Goby | L5 | 61.2 | 59.7 | 60.1 | 1.019 |
| Shrike | L5 | 58.2 | 58.7 | 63.1 | 0.923 |
- rest, Unity: spread 1.218x (Shrike 82.6 / BrittleStar 67.8), n=11
- rest, judge seed 1: spread 1.255x (Goby 79.0 / Dolphin 62.9), n=11
- rest, judge mean: spread 1.154x (Goby 76.6 / Squirrel 66.4), n=11
- L5, Unity: spread 1.271x (Rhino 74.0 / Shrike 58.2), n=11
- L5, judge seed 1: spread 1.343x (Rhino 74.8 / Dolphin 55.7), n=11
- L5, judge mean: spread 1.218x (Rhino 72.9 / BrittleStar 59.9), n=11

### Frame-rate probe (wall clock, no pinned tick), Ref1 rest

| Hull | Unity wall-clock s | fps | mean dt ms | Unity pinned s | Judge seed 1 |
|---|---|---|---|---|---|
| Dolphin | 57.6 | 60 | 16.7 | 60.3 | 48.0 |
| Urchin | 63.5 | 60 | 16.8 | 63.5 | 63.3 |
| Sparrow | 64.6 | 59 | 16.8 | 74.4 | 74.4 |
| Squirrel | 57.9 | 60 | 16.7 | 58.5 | 58.8 |

## Broadside (4v4, 240 s timed, Legendary; the same 6 lineups and seeds per start in both)

| Hull | Start | Unity share (se) | Judge, same lineups (se) | Judge, 12 more matches (se) | Unity pts | Judge pts |
|---|---|---|---|---|---|---|
| Angler | rest | 1.00 (0.29, n6) | 0.97 (0.10, n6) | 1.06 (0.11, n12) | 136 | 116 |
| BrittleStar | rest | 0.96 (0.37, n6) | 0.84 (0.39, n6) | 1.04 (0.15, n12) | 120 | 105 |
| Grizzly | rest | 1.04 (0.22, n6) | 0.91 (0.08, n6) | 0.92 (0.18, n12) | 137 | 113 |
| Jacks | rest | 1.10 (0.26, n6) | 1.17 (0.26, n6) | 1.38 (0.18, n12) | 144 | 147 |
| Meerkat | rest | 0.60 (0.10, n6) | 0.70 (0.16, n6) | 0.71 (0.11, n12) | 78 | 82 |
| Orca | rest | 1.50 (0.21, n6) | 1.27 (0.22, n6) | 1.30 (0.23, n12) | 197 | 152 |
| Scarab | rest | 0.91 (0.22, n6) | 1.03 (0.23, n6) | 0.88 (0.09, n12) | 118 | 120 |
| Stoat | rest | 0.89 (0.18, n6) | 1.11 (0.14, n6) | 0.71 (0.14, n12) | 116 | 135 |

rest: 6 Unity matches, fps 39, mean dt 16.7 ms

| Angler | L5 | 1.07 (0.21, n6) | 1.13 (0.23, n6) | 1.39 (0.21, n12) | 101 | 120 |
| BrittleStar | L5 | 1.31 (0.31, n6) | 1.02 (0.19, n6) | 0.71 (0.15, n12) | 129 | 112 |
| Grizzly | L5 | 0.65 (0.16, n6) | 0.95 (0.18, n6) | 1.03 (0.19, n12) | 67 | 110 |
| Jacks | L5 | 1.06 (0.33, n6) | 0.93 (0.12, n6) | 1.37 (0.21, n12) | 107 | 104 |
| Meerkat | L5 | 1.18 (0.23, n6) | 1.15 (0.16, n6) | 0.70 (0.06, n12) | 112 | 128 |
| Orca | L5 | 0.90 (0.20, n6) | 1.11 (0.16, n6) | 1.00 (0.13, n12) | 85 | 121 |
| Scarab | L5 | 1.44 (0.23, n6) | 1.19 (0.19, n6) | 0.92 (0.15, n12) | 134 | 128 |
| Stoat | L5 | 0.40 (0.16, n6) | 0.52 (0.16, n6) | 0.87 (0.19, n12) | 42 | 61 |

L5: 6 Unity matches, fps 42, mean dt 16.7 ms


Unity notes: course: Ref1 via RegattaCourseGenome.Override | course: Ref4 via RegattaCourseGenome.Override | let in off the card: Grizzly | let in off the card: Orca | let in off the card: Squirrel | points_per_pilot: 100000

## Where things are

- Harness (not committed): `Assets/_Scripts/Editor/Launch/ArenaAudit/ArenaAuditLauncher.cs` and `ArenaAuditDriver.cs` in the worktree `D:\CS-judgecheck` (branch claude/project-thread-u6uwfq at 910c2778a8), modelled on the Brood audit's launcher. Raw results: `D:\arena-audit\unity\matches.jsonl`, judge results `D:\arena-audit\judge\`, scripts `make_plan.py`, `run_judge.py`, `compare.py`.
- One local, uncommitted Port patch so the judge builds on 910c2778a8: `Port/src/CosmicShore.Engine/UI/PackageComponents.cs`, `Shadow : MonoBehaviour` to `Shadow : UIBehaviour` (the UIThemeLive.cs(272) CS8121 failure). Not pushed, since the Amoebius lane is occupied.
- Garrett's main checkout was not touched. Nothing on C: was deleted.
