# Brood Rush: real Unity vs the Amoebius judge (2026-11-22)

Sources: Unity `origin/claude/brood-unity-audit` (`Tools/Build/brood_unity_audit/results.csv`, 42 matches, 378 seats);
judge refit `refit/res_finM2.json` + `res_it5.json` (152 matches). New run for this report: **the judge on the Unity
tree itself (acb8566721) with the same 42 Unity rosters**, Medium, 1 seed each, saved as
`refit/res_unitytree_unityrosters.json`. That isolates engine differences from lever and roster differences.

## Per hull

| Hull | Unity | Judge refit | Judge, Unity tree + rosters | Main cause | Evidence |
|---|---|---|---|---|---|
| Dolphin | 1.71 | 1.04 | 1.02 ± 0.03 | **frame-rate-dependent Dolphin loop** | speed 111 vs 72, blocks/s 15.9 vs 11.1, drift active 23 s vs 12 s. Judge at 30 fps: speed 119, index 2.06 |
| Sparrow | 1.36 | 1.13 | 1.56 ± 0.13 | roster draw, high variance | same tree and rosters put the judge above Unity |
| TigerMoth | 1.31 | 1.21 | 1.25 ± 0.10 | agrees | - |
| Orca | 0.77 | 1.01 | 0.78 ± 0.04 | lever 0.8 vs 0.978, Dolphin inflating the field | agrees once the tree matches |
| Stoat | 0.79 | 1.01 | 0.82 ± 0.04 | lever 1.937 vs 2.286 | agrees |
| Urchin | 0.79 | 0.61 | 0.81 ± 0.14 | roster draw (sd 0.6) | agrees |
| Rhino | 0.81 | 0.99 | 0.94 ± 0.04 | Dolphin field inflation; residual -0.08 | Unity blocks/s 16.9 vs 18.5 |
| Swordfish | 0.85 | 1.03 | 1.12 ± 0.08 | Dolphin field inflation; residual -0.2 | Unity VC 22.4k vs 26.2k |
| Shrike | 0.88 | 1.11 | 0.91 ± 0.07 | levers and rosters | agrees |
| Thresher, Manta, Wrasse, Grizzly, Meerkat, Scarab, Squirrel, Honeyguide | 0.92-1.02 | 0.92-1.04 | 0.87-1.08 | agree | - |

## Ranked causes

1. **The Dolphin's drift, charge and boost loop depends on frame rate. This is a game bug, not a port bug.** The same
   judge on the Unity tree with 8 Dolphin rosters gives:

   | Frame rate | Dolphin speed | Index | Left-stick active |
   |---|---|---|---|
   | 120 fps | 69 | 1.02 | 9.8 s |
   | 60 fps | 72 | 1.02 | 12.5 s |
   | 30 fps | 119 | 2.06 | 30.6 s |

   Unity's 111 / 1.71 / 23.3 s lies between 30 and 60 fps. The other hulls barely move with frame rate. Thresher
   (231 at 30 fps, 264 at 60, Unity 227) and Squirrel (103 / 125, Unity 115) also put the Unity batchmode run near
   30-40 fps. The harness never recorded frame rate. Every Unity hull's index is measured against a field mean that
   the Dolphin inflates (+17% field VC), which is most of why Orca, Stoat, Rhino, Swordfish and Shrike read low.

   Prime suspects, all frame-quantised:
   - `ChargeBoostActionExecutor.cs:131-179`: fixed charge per 0.1 s `UniTask.Delay` tick, rounded up to whole frames.
   - `ChargeBoostAIPolicySO.cs:72-73,146-149`: the peak re-drift test (within 3 u/s, 0.2 s minimum, 0.25 s grace,
     sampled once per frame).
   - `AIPilot.cs:1361-1400`: the commit drift ends on a per-frame `Dot >= .9`.

   The jump is sharp between 60 and 30 fps, which looks like a threshold rather than a slow drift.

2. **Lever and tree mismatch.**
   - The four held trail levers (Orca, Stoat, Thresher, Dolphin).
   - **A fifth: Manta's x0.856 was not in the Unity tree.** Trail reads 0.13 in Unity vs 0.111 in the refit; volume per
     block is 1.13x.

3. **Roster draw and noise.** n = 22 a hull with sd up to 0.6 covers Sparrow, Urchin and TigerMoth.

4. **Jade winning 19/42 is the roster draw, not the host seat.** The Dolphin sat on Jade in 11 of its 23 matches, and
   Dolphin teams won 17/23. Jade without a Dolphin won 10/31 (32%). Host seats index 1.06, the same as other Jade seats.

## Ruled out, with numbers

- **Match length.** Unity mean 119 s with 19/42 at 90 s; judge 113 s with 47/96 at 90 s.
- **Trail spawn framerate.** Blocks/s agree within 6% for 15 of 17 hulls. Only the Dolphin (speed) and the Urchin
  (heavy tail) differ.
- **The ability counter.** Both sources record zero for TigerMoth, Manta, Wrasse, Scarab, Urchin and Orca, which drive
  through autopilots or press nothing. Press counts match (Shrike 26/28, Grizzly 108/97, Stoat about 94/85).
- **Throttle rows.** Unity AIPilot reads Dolphin and TigerMoth throttle 1.0, the same as the judge (`AIPilot.cs:1100`).
- **Books, server-only.** The offline host is the server.
- **Shield netting (7f2529504c).** It is in both trees.

## Unity-side measurement issue (outside the index)

At match end, Unity's books do not balance for **Shrike** (VC − Lost − Held = 20.7k, 89% of VC; held 0.7k vs the
judge's 21.6k) or **Sparrow** (25.1k, 71%; held 2.9k vs 18.6k). The judge balances both. Their prisms are not in
`PrismSpatialIndex` when `BroodAuditDriver.cs:398` counts. `ReportBooksGaps` (`BroodRushController.cs:153`) logs the
named pilots in Unity's log. Until this is fixed, do not trust Unity's Held column for these two.

## What to change

1. **Game: make the Dolphin's loop frame-independent.** Its strength now depends on the device frame rate: 2x the
   field at 30 fps, par at 60. Owner: whoever holds the Dolphin lane (claude/ms-ai-dolphin), through `/approve`.
   - Express the charge and discharge as rate × `Time.deltaTime` each frame, instead of a fixed amount per 0.1 s
     delay tick.
   - Evaluate `AtPeak` on the tracked speed with a dt-scaled margin.
   - After the fix, re-run the judge at 30 and 60 fps; the Dolphin row should match.
2. **Port: let the judge run at Unity's frame rate.** In the scratch worktree:
   - `RaceJudge.cs:47` becomes `static readonly double TickSeconds = JudgeDt.Dt;`.
   - `Program.cs:325` becomes `boot.Tick(JudgeDt.Dt);`.
   - A new `JudgeDt` reads the `JUDGE_DT` environment variable (default 1/60).

   Fit levers at 30 and 60 fps both, until point 1 lands.
3. **Unity harness.**
   - Record frame count / mean `Time.deltaTime` per match, and pin `Application.targetFrameRate`.
   - Apply the five refit levers, including Manta x0.856, before the next audit, so the two trees are identical.
4. **Fix the end-count gap for Shrike and Sparrow.**

Raw data: scratch `uvj/` (`res_u60.json`, `work_d30/`, `work_d120/`, `cmp.py`, `an2.py`, `dd.py`).
