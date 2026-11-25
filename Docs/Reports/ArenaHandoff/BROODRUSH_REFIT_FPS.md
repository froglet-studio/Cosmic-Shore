# Brood Rush refit at 60 and 30 fps (night of 2026-11-23), PROVISIONAL

**Branch** `claude/brood-night-refit` (scratch, pushed, not a PR), off `claude/brood-night-integration` (dab40e3cae):
a8e83e93e6 (the judge fit), 0769ae98b6, then 7bc00d9e76 (the committed TABLE, prefabs, Shrike nest, Thornbush comeback rate regenerated).

**Setup.** Amoebius judge, Medium, 3 teams of 3, the 17 card hulls rotating 9 to a match, `JUDGE_DT` 1/60 and 1/30.
The fit uses the mean of the two rates' VC and VD shares, with VC taken as proportional to lever^0.8 and a damping of 0.7.
Rounds: r0 at the integration tree, 152 + 152 matches; r1 at 100 + 100; r2 at the judge fit, 152 + 152.
The 0.4x-2.5x caps are lifted for TigerMoth and Urchin only, and only provisionally.
Rhino and Manta keep their PR levers: both stay inside 0.9-1.1 on the mean.
Unity numbers come from `origin/claude/brood-unity-audit` 5e5445abb5 (results_60/30.csv, 60 rosters, 31-32 seats a hull).

## 1. The integration tree (dab40e3cae), Medium 3x3, 152 matches per rate

| hull | judge 60 fps | judge 30 fps | Unity 60 fps | Unity 30 fps |
|---|---|---|---|---|
| TigerMoth | 1.27 ± 0.05 | 1.49 ± 0.07 | 1.27 | 1.31 |
| Dolphin | 1.02 ± 0.02 | 1.31 ± 0.03 | 1.45 | 1.49 |
| Grizzly | 1.08 ± 0.02 | 1.06 ± 0.03 | 0.95 | 0.93 |
| Swordfish | 1.05 ± 0.03 | 1.02 ± 0.03 | 0.93 | 0.86 |
| Meerkat | 1.02 ± 0.03 | 1.04 ± 0.03 | 0.89 | 0.98 |
| Manta | 1.01 ± 0.02 | 1.02 ± 0.02 | 0.90 | 1.00 |
| Sparrow | 0.99 ± 0.08 | 1.04 ± 0.08 | 1.54 | 1.55 |
| Honeyguide | 0.99 ± 0.03 | 1.03 ± 0.03 | 0.91 | 0.93 |
| Wrasse | 1.02 ± 0.05 | 0.97 ± 0.04 | 0.88 | 1.05 |
| Orca | 1.02 ± 0.03 | 0.96 ± 0.02 | 0.86 | 0.87 |
| Stoat | 0.98 ± 0.03 | 0.98 ± 0.03 | 0.90 | 1.00 |
| Shrike | 0.94 ± 0.04 | 1.02 ± 0.04 | 0.94 | 0.86 |
| Rhino | 0.95 ± 0.02 | 0.95 ± 0.02 | 0.91 | 0.90 |
| Squirrel | 1.01 ± 0.02 | 0.81 ± 0.02 | 0.95 | 0.87 |
| Thresher | 1.00 ± 0.02 | 0.81 ± 0.02 | 0.99 | 0.89 |
| Scarab | 0.97 ± 0.01 | 0.76 ± 0.01 | 1.00 | 0.78 |
| Urchin | 0.67 ± 0.06 | 0.78 ± 0.06 | 0.71 | 0.69 |

## 2. Levers, and the judge's final round at the judge fit (152 matches per rate)

| hull | lever: integration -> judge fit -> committed | judge 60 fps | judge 30 fps | mean | Legendary 60 fps |
|---|---|---|---|---|---|
| Sparrow | 0.4705 -> 0.459 -> 0.459 | 1.06 ± 0.08 | 1.15 ± 0.09 | 1.11 | 1.32 ± 0.10 |
| Grizzly | 0.4 -> 0.4 -> 0.4 | 1.03 ± 0.02 | 1.05 ± 0.03 | 1.04 | 0.85 ± 0.02 |
| Swordfish | 0.7268 -> 0.6766 -> 0.6766 | 1.05 ± 0.04 | 1.01 ± 0.03 | 1.03 | 0.94 ± 0.02 |
| Honeyguide | 1.5417 -> 1.5232 -> 1.5232 | 0.99 ± 0.03 | 1.04 ± 0.03 | 1.02 | 0.88 ± 0.03 |
| TigerMoth | 0.4 -> 0.2587 -> 0.2587 | 1.01 ± 0.06 | 1.01 ± 0.06 | 1.01 | 0.90 ± 0.07 |
| Dolphin | 1.6466 -> 1.3828 -> 0.977 | 0.85 ± 0.02 | 1.16 ± 0.03 | 1.01 | 0.77 ± 0.02 |
| Shrike | nest shell 1.2273 -> 1.2762 -> 1.2762 | 1.08 ± 0.04 | 0.93 ± 0.04 | 1.00 | 1.02 ± 0.03 |
| Scarab | 0.9953 -> 1.171 -> 1.171 | 1.11 ± 0.02 | 0.89 ± 0.02 | 1.00 | 0.95 ± 0.03 |
| Urchin | 2.5 -> 3.6596 -> 3.6596 | 0.90 ± 0.08 | 1.08 ± 0.08 | 0.99 | 1.28 ± 0.09 |
| Manta | kept (#2331, Min/Max 0.111/0.513) | 1.01 ± 0.02 | 0.97 ± 0.02 | 0.99 | 1.01 ± 0.03 |
| Squirrel | 2.1557 -> 2.3823 -> 2.3823 | 1.03 ± 0.02 | 0.94 ± 0.02 | 0.99 | 0.82 ± 0.02 |
| Stoat | 2.2858 -> 2.3317 -> 2.3317 | 1.00 ± 0.03 | 0.97 ± 0.03 | 0.99 | 1.36 ± 0.04 |
| Thresher | 0.9141 -> 1.0565 -> 1.0565 | 1.06 ± 0.02 | 0.92 ± 0.02 | 0.99 | 0.97 ± 0.02 |
| Wrasse | 0.4869 -> 0.4947 -> 0.4947 | 0.96 ± 0.04 | 1.01 ± 0.04 | 0.98 | 0.95 ± 0.06 |
| Orca | 0.9784 -> 0.9809 -> 0.9809 | 0.94 ± 0.02 | 0.98 ± 0.02 | 0.96 | 1.20 ± 0.03 |
| Rhino | kept (#2329) | 0.97 ± 0.02 | 0.94 ± 0.02 | 0.96 | 1.10 ± 0.03 |
| Meerkat | 1.9244 -> 1.8407 -> 1.8407 | 0.95 ± 0.03 | 0.96 ± 0.03 | 0.95 | 0.71 ± 0.02 |

The Legendary column is one run of 100 matches at 60 fps, at the judge fit (Dolphin 1.3828).

## 3. Judge against Unity

On the integration tree, 15 of the 17 hulls agree within noise. That includes the frame-rate gaps: Scarab 0.97 -> 0.76 in the judge and 1.00 -> 0.78 in Unity; Thresher and Squirrel fall at 30 fps in both. Two hulls disagree:
- **Dolphin**: 1.02 / 1.31 in the judge against 1.45 / 1.49 in Unity. In Unity it flies at **100 m/s at 60 fps and 104 at 30**. In the judge it flies at 72 at 60 and 87 at 30, so its VC share is 1.45 in Unity against 1.01 in the judge. unity-vs-judge traces this to the Dolphin's drift / charge / boost AI loop, which still depends on the frame rate: a 0.1 s UniTask charge tick, and the ChargeBoostAIPolicy and AIPilot commit-drift tests run once a frame. Unity's real dt is not the target dt (57.4 fps achieved). **The committed lever targets Unity**: 1.6466 -> **0.977**, fitted to Unity's shares (`ufit.py`, predicted 0.98 / 1.02 in Unity). The judge cannot check this value. It would read it at about 0.6 at 60 fps.
- **Sparrow**: 0.99 / 1.04 in the judge against 1.54 / 1.55 in Unity, at the same speed and the same trail_volume. Its VC share is 1.24 in Unity against 0.74 in the judge. unity-vs-judge found that Unity's volume books do not balance for the Sparrow: VC - Lost - Held is 71% of VC (89% for the Shrike), while the judge's books balance. So Unity's Sparrow VC is not trusted until the `ReportBooksGaps` log has been read. The Sparrow stays at the judge fit, **0.459**. If Unity turns out to be right, it needs about 0.21, which is below its 0.4 floor.
- The judge was started on Unity's exact 60 rosters at 60 and 30 fps (`u_60`, `u_30`) to settle this. Those results come after this report if they land.

## 4. Regatta (shipped shuffle, 9 seeds, alone), for the card hulls whose lever moved more than 10%

Card start, base against the lever: Dolphin 78.0 -> 77.7 (-0.4%), Scarab 73.7 -> 73.8 (+0.0%), Squirrel 71.4 -> 71.5 (+0.2%), Thresher 72.5 -> 72.3 (-0.3%), Urchin 71.4 -> 71.9 (+0.6%).
All of these are within noise, and nothing slows by more than 0.6%. The L5 lever run was cut off by a background time limit, and its re-run is queued. The Regatta Dolphin was judged at 1.3828, and a 0.977 run is queued.

## 5. Which hulls cannot hold both rates (judge, at the fit)
- **Dolphin**: 0.85 at 60, 1.16 at 30. Its gap is 0.31, which is AI frame-rate dependence, not something a lever can fix.
- **Scarab**: 1.11 / 0.89, gap 0.22. Unity shows the same gap.
- **Shrike**: 1.08 / 0.93. **Thresher**: 1.06 / 0.92. **Squirrel**: 1.03 / 0.94. **Urchin**: 0.90 / 1.08 (se 0.08).
- **Sparrow**: 1.06 / 1.15 (se 0.09). Its mean is 1.11, and the next step would take it to about 0.43.
- The other 10 hulls sit within 0.9-1.1 at both rates.
- Caps: TigerMoth needs **0.26** (floor 0.4) and then reads 1.01 at both rates. Urchin needs **3.66** (cap 2.5) and then reads 0.90 / 1.08.

## 6. Legendary 3x3 at 60 fps (judge fit, 100 matches)
**It does not hold: 9 of 17 hulls are inside 0.9-1.1.**
- Too high: Stoat 1.36, Sparrow 1.32, Urchin 1.28, Orca 1.20, Rhino 1.10.
- Too low: Meerkat 0.71, Dolphin 0.77, Squirrel 0.82, Grizzly 0.85, Honeyguide 0.88.
- This matches the previous refit: the AI rows behave differently at skill 1.0, so one trail value cannot serve both difficulties.

Data: res_r0/r1/r2_{60,30}.json, res_leg_60.json, state0/1/2/final.json, night.py, fit2.py, ufit.py, reg_*.

## 7. Addendum (09:01): the judge on Unity's exact 60 rosters, 60 fps, integration tree (res_u_60.json)
- **Sparrow 1.65 ± 0.13 in the judge, against 1.54 in Unity.** On these rosters the judge agrees with Unity. The judge's Sparrow reading depends on the rosters: it has the largest variance in the field, and these 60 rosters favour it. This weakens the books-leak explanation in section 3. The Sparrow may really be strong, and 0.459 may be too high. Re-check it with a larger deal.
- **Dolphin 0.95 in the judge (speed 70), against 1.45 in Unity (speed 100).** The gap is confirmed as an engine parity gap, not a roster effect, so the Unity-targeted 0.977 stands.
- The other 15 hulls fall within about 0.1 of Unity. TigerMoth reads 1.17 in the judge against 1.27 in Unity. Urchin reads 0.63 in the judge against 0.71 in Unity.
- u_30 and the L5 and Dolphin Regatta re-runs are still running.
