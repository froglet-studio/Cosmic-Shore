# Brood Rush in real Unity: 60 fps against 30 fps (night of 2026-11-23)

The question: does Brood Rush's per-hull balance depend on the frame rate? The same AI-only matches ran twice in
the real Unity game (6000.3.17f1, batchmode, one fresh Play session from Bootstrap per match), once with the frame
rate pinned at 60 and once at 30.

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
- **60 fps reaches 56.7 on average** (mean deltaTime 17.7 ms). Batchmode on this PC cannot hold 60: matches run
  56-60 fps with nothing else on the machine. The first 10 matches overlapped the D: client import (idle priority)
  and averaged 53.9 (min 47.3), against 57.6 for the other matches.
- timeScale is 1 apart from brief in-game dips to 0.1 in some matches (mean 0.998 or higher). The game does this;
  the harness does not.

## 60 against 30, per hull

Index = a seat's VC + VD over the mean of all nine seats in its match, averaged per hull (the Amoebius judge's
definition). "se" is the standard error of the difference between two independent means. With 17 hulls, about one
is expected to land outside 2 se by chance alone.

{{COMPARE}}

## What it says

{{FINDINGS}}

## Block tables

{{BLOCK_A}}

{{BLOCK_B}}

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
