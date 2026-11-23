# Brood Rush in real Unity: 60 fps against 30 fps (night of 2026-11-23)

The question: does Brood Rush's per-hull balance depend on the frame rate? The same AI-only matches ran twice in
the real Unity game (6000.3.17f1, batchmode, one fresh Play session from Bootstrap per match), once with the frame
rate pinned at 60 and once at 30: 60 matches per block, 540 seats per block, 31-32 seats per hull.

**Tree.** `claude/brood-night-integration` (#2322 + #2329 + #2330 + #2331 + #2332 + #2333 + the five held refit
levers) merged into this branch. Only the Editor-only harness (`Assets/_Scripts/Editor/Launch/BroodAudit/`) differs
from that branch, so the Unity tree matches the tree the Amoebius judge scores.

**Setup.** Medium difficulty on every seat, intensity 2, three teams of three (Jade, Ruby, Gold), with 9 of the 17
card hulls in each match (`rosters.py --matches 60 --seed 11`; both blocks play the same rosters in the same order,
so each hull's 60-against-30 difference is paired by roster). The host flies team A's first hull and hands it to the
AI at turn start. Cap 300 s; every match ended on broods.

**Frame-rate pin.** `-broodFps N` sets `QualitySettings.vSyncCount = 0` and `Application.targetFrameRate = N` at
turn start, and re-checks both every editor tick, because the game's boot and graphics settings set their own rate.
It never had to re-apply (pin resets 0 in every match). Each match records its frames, mean `Time.deltaTime`, mean
unscaled deltaTime, frames per game second and per wall second, and the timeScale seen.

- **30 fps is reached exactly**: 29.7-30.0 fps in every match, mean deltaTime 33.4 ms.
- **60 fps reaches 57.4 on average** (mean deltaTime 17.5 ms). Batchmode on this PC cannot hold 60: matches run
  56-60 fps with nothing else on the machine. The first 10 matches overlapped the D: client import (idle priority)
  and averaged 53.9 (min 47.3), against 57.6 for the other matches.
- timeScale is 1 apart from brief in-game dips to 0.1 in some matches (mean 0.998 or higher). The game does this;
  the harness does not.

## 60 against 30, per hull

Index = a seat's VC + VD over the mean of all nine seats in its match, averaged per hull (the Amoebius judge's
definition). "se" is the standard error of the difference between two independent means. With 17 hulls, about one
is expected to land outside 2 se by chance alone.

| Hull | Index 60 fps | n | Index 30 fps | n | 30 minus 60 | +- 1 se | Win 60 | Win 30 |
|---|---|---|---|---|---|---|---|---|
| Sparrow | 1.54 | 32 | 1.55 | 32 | +0.02 | 0.15 | 9% | 25% |
| Dolphin | 1.45 | 32 | 1.49 | 32 | +0.04 | 0.08 | 66% | 78% |
| TigerMoth | 1.27 | 32 | 1.31 | 32 | +0.04 | 0.14 | 56% | 53% |
| Scarab | 1.00 | 31 | 0.78 | 31 | -0.22 | 0.04 | 61% | 23% |
| Thresher | 0.99 | 32 | 0.89 | 32 | -0.09 | 0.06 | 22% | 16% |
| Squirrel | 0.95 | 32 | 0.87 | 32 | -0.08 | 0.05 | 41% | 16% |
| Grizzly | 0.95 | 32 | 0.93 | 32 | -0.02 | 0.05 | 16% | 31% |
| Shrike | 0.94 | 31 | 0.86 | 31 | -0.08 | 0.08 | 23% | 10% |
| Swordfish | 0.93 | 32 | 0.86 | 32 | -0.07 | 0.05 | 28% | 19% |
| Honeyguide | 0.91 | 31 | 0.93 | 31 | +0.02 | 0.05 | 23% | 29% |
| Rhino | 0.91 | 32 | 0.90 | 32 | -0.01 | 0.05 | 25% | 25% |
| Manta | 0.90 | 32 | 1.00 | 32 | +0.10 | 0.04 | 16% | 31% |
| Stoat | 0.90 | 32 | 1.00 | 32 | +0.10 | 0.06 | 28% | 38% |
| Meerkat | 0.89 | 32 | 0.98 | 32 | +0.09 | 0.05 | 38% | 41% |
| Wrasse | 0.88 | 32 | 1.05 | 32 | +0.17 | 0.08 | 31% | 53% |
| Orca | 0.86 | 32 | 0.87 | 32 | +0.01 | 0.05 | 34% | 41% |
| Urchin | 0.71 | 31 | 0.69 | 31 | -0.02 | 0.12 | 52% | 39% |

Outside 2 se: Manta, Scarab, Wrasse.
Mean absolute change 0.069; largest -0.22.

## What it says

- **Most of the field does not care about the frame rate.** 14 of 17 hulls move by less than 2 se. The mean
  absolute change is 0.07. The top three stay the top three at both rates (Sparrow 1.54 / 1.55, Dolphin 1.45 / 1.49,
  TigerMoth 1.27 / 1.31), and Urchin stays last (0.71 / 0.69).
- **Scarab is frame-rate sensitive: 1.00 at 60 fps, 0.78 at 30** (-0.22, about 5 se). Its team's win rate falls from
  61% to 23%. This is the one clear effect.
- **Wrasse (+0.17, 2.1 se) and Manta (+0.10, 2.5 se) do better at 30 fps**, and Stoat (+0.10) and Meerkat (+0.09)
  lean the same way, just inside 2 se. Thresher, Squirrel, Shrike and Swordfish lean the other way (-0.07 to -0.09).
- **Inside the 0.90-1.10 band**: 10 of 17 hulls at 60 fps, 7 of 17 at 30. The judged tree's balance is therefore
  best read at 60 fps, and a phone that drops to 30 shifts Brood Rush toward Wrasse and Manta and away from Scarab.
- Team wins are even at both rates (60 fps: Gold 23, Jade 19, Ruby 18; 30 fps: Gold 21, Ruby 20, Jade 19). The
  host-team lead seen in the first 42-match audit (Jade 19 of 42) did not appear on this tree.
- Not tested: why Scarab loses at 30 fps (frame-step steering, collision or ability timing). A paired run of Scarab at
  both rates with its ability and collision counts would narrow it down.

## Block tables

### Block A, pinned 60 fps: 60 matches, mean match 115 s

Frames: pinned 60; fps per game second mean 57.4 (min 47.3, max 59.8); per wall second 57.4; mean deltaTime 17.46 ms; timeScale 0.1-1; pin resets 0.

| Hull | n | Index | sd | VC share | VD share | Lost share | VC mean | VD mean | Team broods | Win | In nucleus | Speed | Trail vol | Ability starts | Host seats |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Sparrow | 32 | 1.54 | 0.53 | 1.24 | 0.30 | 0.30 | 37336 | 9074 | 0.62 | 9% | 74% | 59 | 0.47 | 62 | 3 |
| Dolphin | 32 | 1.45 | 0.27 | 1.45 | 0.00 | 0.28 | 41291 | 141 | 2.12 | 66% | 66% | 100 | 1.65 | 62 | 2 |
| TigerMoth | 32 | 1.27 | 0.47 | 1.08 | 0.20 | 0.25 | 34153 | 7146 | 2.09 | 56% | 65% | 60 | 0.40 | 0 | 1 |
| Scarab | 31 | 1.00 | 0.17 | 0.98 | 0.02 | 0.15 | 28382 | 675 | 2.00 | 61% | 58% | 205 | 0.99 | 0 | 2 |
| Thresher | 32 | 0.99 | 0.23 | 0.90 | 0.09 | 0.14 | 25925 | 2522 | 0.88 | 22% | 46% | 245 | 0.91 | 33 | 4 |
| Squirrel | 32 | 0.95 | 0.22 | 0.88 | 0.07 | 0.21 | 24764 | 2117 | 1.28 | 41% | 62% | 119 | 2.16 | 1 | 8 |
| Grizzly | 32 | 0.95 | 0.17 | 0.88 | 0.07 | 0.06 | 25563 | 2186 | 0.69 | 16% | 50% | 50 | 0.40 | 100 | 4 |
| Shrike | 31 | 0.94 | 0.29 | 0.88 | 0.05 | 0.06 | 23990 | 1617 | 0.81 | 23% | 68% | 18 | 0.07 | 91 | 5 |
| Swordfish | 32 | 0.93 | 0.23 | 0.82 | 0.12 | 0.57 | 23346 | 3179 | 1.03 | 28% | 72% | 45 | 0.73 | 3 | 4 |
| Honeyguide | 31 | 0.91 | 0.19 | 0.89 | 0.02 | 0.34 | 26808 | 515 | 1.13 | 23% | 67% | 36 | 1.54 | 5 | 4 |
| Rhino | 32 | 0.91 | 0.21 | 0.86 | 0.05 | 0.38 | 24430 | 1344 | 0.94 | 25% | 64% | 55 | 1.00 | 4 | 2 |
| Manta | 32 | 0.90 | 0.16 | 0.86 | 0.04 | 0.18 | 26033 | 1259 | 0.88 | 16% | 48% | 173 | 0.11 | 0 | 4 |
| Stoat | 32 | 0.90 | 0.20 | 0.74 | 0.16 | 0.25 | 22695 | 5691 | 1.31 | 28% | 70% | 104 | 2.29 | 88 | 5 |
| Meerkat | 32 | 0.89 | 0.18 | 0.84 | 0.05 | 0.17 | 22103 | 1473 | 1.28 | 38% | 60% | 66 | 1.92 | 41 | 5 |
| Wrasse | 32 | 0.88 | 0.24 | 0.49 | 0.39 | 0.13 | 14791 | 11568 | 1.38 | 31% | 73% | 60 | 0.49 | 0 | 1 |
| Orca | 32 | 0.86 | 0.21 | 0.68 | 0.18 | 0.27 | 20249 | 5388 | 1.31 | 34% | 73% | 36 | 0.98 | 0 | 4 |
| Urchin | 31 | 0.71 | 0.44 | 0.69 | 0.02 | 0.31 | 20463 | 526 | 2.00 | 52% | 86% | 150 | 2.50 | 0 | 2 |

Within 0.90-1.10 of the field: 10 of 17 (Grizzly, Honeyguide, Manta, Rhino, Scarab, Shrike, Squirrel, Stoat, Swordfish, Thresher).

### Block B, pinned 30 fps: 60 matches, mean match 114 s

Frames: pinned 30; fps per game second mean 29.9 (min 29.7, max 30.0); per wall second 29.9; mean deltaTime 33.43 ms; timeScale 0.1-1; pin resets 0.

| Hull | n | Index | sd | VC share | VD share | Lost share | VC mean | VD mean | Team broods | Win | In nucleus | Speed | Trail vol | Ability starts | Host seats |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Sparrow | 32 | 1.55 | 0.68 | 1.29 | 0.27 | 0.23 | 39365 | 9531 | 0.88 | 25% | 71% | 62 | 0.47 | 62 | 3 |
| Dolphin | 32 | 1.49 | 0.38 | 1.48 | 0.01 | 0.29 | 39762 | 199 | 2.47 | 78% | 71% | 104 | 1.65 | 57 | 2 |
| TigerMoth | 32 | 1.31 | 0.63 | 1.09 | 0.22 | 0.26 | 33552 | 8999 | 1.94 | 53% | 67% | 59 | 0.40 | 0 | 1 |
| Wrasse | 32 | 1.05 | 0.36 | 0.60 | 0.45 | 0.14 | 16381 | 13038 | 1.91 | 53% | 70% | 65 | 0.49 | 0 | 1 |
| Stoat | 32 | 1.00 | 0.26 | 0.71 | 0.29 | 0.23 | 18424 | 7591 | 1.38 | 38% | 69% | 106 | 2.29 | 78 | 5 |
| Manta | 32 | 1.00 | 0.19 | 0.96 | 0.04 | 0.22 | 25731 | 995 | 1.12 | 31% | 48% | 173 | 0.11 | 0 | 4 |
| Meerkat | 32 | 0.98 | 0.23 | 0.80 | 0.18 | 0.13 | 21901 | 4948 | 1.41 | 41% | 73% | 66 | 1.92 | 43 | 5 |
| Honeyguide | 31 | 0.93 | 0.20 | 0.91 | 0.02 | 0.35 | 26623 | 583 | 1.10 | 29% | 77% | 35 | 1.54 | 5 | 4 |
| Grizzly | 32 | 0.93 | 0.24 | 0.85 | 0.07 | 0.07 | 23322 | 1958 | 1.09 | 31% | 59% | 50 | 0.40 | 101 | 4 |
| Rhino | 32 | 0.90 | 0.21 | 0.86 | 0.04 | 0.34 | 22927 | 1113 | 1.09 | 25% | 71% | 61 | 1.00 | 5 | 2 |
| Thresher | 32 | 0.89 | 0.21 | 0.83 | 0.06 | 0.13 | 20318 | 1658 | 0.84 | 16% | 49% | 227 | 0.91 | 31 | 4 |
| Orca | 32 | 0.87 | 0.18 | 0.72 | 0.15 | 0.27 | 20814 | 4465 | 1.44 | 41% | 72% | 37 | 0.98 | 0 | 4 |
| Squirrel | 32 | 0.87 | 0.15 | 0.81 | 0.05 | 0.21 | 21423 | 1470 | 0.69 | 16% | 67% | 116 | 2.16 | 2 | 8 |
| Swordfish | 32 | 0.86 | 0.20 | 0.81 | 0.05 | 0.48 | 21712 | 1422 | 0.81 | 19% | 75% | 50 | 0.73 | 6 | 4 |
| Shrike | 31 | 0.86 | 0.32 | 0.84 | 0.03 | 0.04 | 20846 | 747 | 0.58 | 10% | 62% | 18 | 0.07 | 48 | 5 |
| Scarab | 31 | 0.78 | 0.16 | 0.76 | 0.02 | 0.14 | 19466 | 652 | 1.10 | 23% | 53% | 205 | 0.99 | 0 | 2 |
| Urchin | 31 | 0.69 | 0.49 | 0.67 | 0.01 | 0.33 | 20792 | 500 | 1.68 | 39% | 83% | 145 | 2.50 | 0 | 2 |

Within 0.90-1.10 of the field: 7 of 17 (Grizzly, Honeyguide, Manta, Meerkat, Rhino, Stoat, Wrasse).

## Host and client Serpent walls (clean client)

The client ran from a second checkout on D: (`D:\CS-brood-client`, the same commit, imported cleanly in 28 min with
0 compile errors). It played with 0 NullReferenceExceptions and 0 missing scripts, unlike the broken checkout used last
time. The host ran a 3x3 match with an AI Serpent (Sparrow, Serpent, Dolphin / Rhino, Manta, Stoat / Squirrel,
Wrasse, Orca) pinned at 60 fps. The client joined as a spectator over 127.0.0.1, followed the host into Brood Rush and
sampled every 0.5 s. Files are in `netcheck2/`.

- **The client sees the host's walls.** The wall count matches in all 175 samples (1, then 2, then 3, at the same
  moments once the client's 2.3 s scene-time offset is removed), where last night's broken client saw 0.
- **The client's walls hold fewer prisms**: a median of 87% of the host's count. It ends at 2,985 against the host's
  3,323 (90%). The gap opens on the second wall: from 20 s to 35 s the host's second wall reaches about 880 prisms
  while the client's stays near 340, and it closes again once the third wall appears. Whether the client's copy of a wall
  stopped growing (the "B's copy stops growing and stands" rule on the checklist) or prisms are lost in transit is
  not visible from counts alone.

## Files

- `results_60.csv`, `results_30.csv`: one row per seat, with a `run` column naming the chunk (n60_1 ... n30_6) and
  the frame columns `target_fps`, `frames`, `mean_dt`, `mean_unscaled_dt`, `fps_game`, `fps_wall`,
  `time_scale_mean/min/max` and `pin_resets`.
- `night/<chunk>.jsonl`: the full match records.
- `night_compare.py` (the comparison), `night_summary.py` (this page from `night_notes.md`), `night_combine.py`
  (chunks to one CSV).
