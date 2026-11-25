# Brood Rush, night of 2026-11-23 (result)

Setup everywhere: Medium AI (the game's default), 3 teams of 3, the 17-hull card, shield shells netted.
Tree: `claude/brood-night-integration` = #2322 + #2329 (Rhino) + #2330 (Swordfish) + #2331 (Manta) + #2332 + #2333 (Mass-Space AIs, Dolphin frame-rate fix) + the five held refit levers. Refit on `claude/brood-night-refit` (7bc00d9e76, PROVISIONAL, not a PR).

## Balanced subset
- **Judge (Amoebius), refit levers, mean of 60 and 30 fps:** 16 of 17 within 0.9-1.1; Sparrow 1.11 (high variance).
  Holding BOTH rates separately: 12 of 17. Can't hold both: Dolphin 0.85/1.16, Scarab 1.11/0.89, Shrike 1.08/0.93, Thresher 1.06/0.92, Sparrow 1.06/1.15.
- **Real Unity (Gbox, 60 matches per rate, pre-refit levers):** 10 of 17 inside at 60 fps, 7 at 30. The judge agrees with Unity for 15 of 17 hulls on the same tree, including Scarab's 30 fps drop.
- Disagreements: **Dolphin** (Unity 1.45 at both rates; judge 1.02 at 60) - its AI loop still runs faster in Unity; levers fitted to Unity's number (1.6466 -> 0.977), unverified. **Sparrow** (Unity 1.54, judge 1.0 on the judge's own deal) - on Unity's exact rosters the judge reads 1.65 too, so it's a roster effect (highest-variance hull), not a parity gap; its 0.459 lever may still be too high and needs a bigger deal. On Unity's rosters the Dolphin reads 0.95 in the judge vs 1.45 in Unity: a real engine-parity gap.
- Seats: the card seats 17 hulls; a match seats 9 (3 a team), each hull once.

## Per-hull lever changes (refit, old -> new)
TigerMoth 0.4 -> 0.2587 (cap lifted, provisional), Urchin 2.5 -> 3.66 (cap lifted, provisional), Scarab 0.995 -> 1.171, Thresher 0.914 -> 1.057, Squirrel 2.156 -> 2.382, Dolphin 1.647 -> 0.977, Swordfish 0.677, Meerkat 1.841, Honeyguide 1.523, Stoat 2.332, Shrike nest shell 1.276; Orca, Wrasse, Sparrow within 2%; Grizzly at its 0.4 floor; Rhino and Manta keep their PR levers.
Regatta (card start, 9 seeds): every change within noise (max +0.6%, Urchin). L5 run unfinished.

## Fixed this pass (PR #2322 and the stack on it)
- Card seated 4 flagged hulls (Manta 2.9, Serpent 2.0, Swordfish 0.66, Rhino 0.19): now the balanced field; Manta/Rhino/Swordfish back with their own levers.
- Scoreboard credited every brood to one pilot: now each pilot's VC+VD.
- Shield churn counted every raise as new volume: netted.
- Judge flew Legendary, game flies Medium: judge now runs at any difficulty, team count and frame rate.
- Serpent wall (and 6 more AI abilities) ran on the host only: now sent to every peer. Unity host+client check: the client sees every wall (175/175 samples), with ~87-90% of the host's prisms (second wall lags).
- Dolphin AI drift blend was frame-rate dependent (30 fps gap 2.16x -> 1.31x, #2333).

## What needs Garrett
1. Held levers in occupied lanes (card): Orca, Dolphin (this project's AI threads), Stoat (Yash), Thresher (other branch).
2. Difficulty target (card): Legendary holds only 9 of 17 at these levers; that is AI-row work, not levers.
3. TigerMoth/Urchin caps (card): the refit lifted them provisionally.
4. Merge order once decided: #2330, #2332, #2333 into bleeding-edge; #2322, then #2329/#2331 on top; then the refit.

## Open
- Dolphin still faster in Unity than in the judge at 60 fps; Sparrow's Unity books don't balance; the second Serpent wall carries fewer prisms on the client.
- Detached judge runs on Unity's exact rosters (res_u_60/30.json) land here when done.
