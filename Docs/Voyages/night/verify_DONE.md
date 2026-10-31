# Verify pass - DONE

Branch `claude/voyage-verify` (from `origin/claude/voyage-night` f0f1410a3), worktree `D:\Voyage\wt-d`, 2026-10-31.

## 1. Code
- Merged `claude/voyage-escort-roles` tip (`49839f640`, the round-3 follow-ups voyage-night did not yet have): Sparrow rockets ignore the Ark lane when Domain-Safe Skybursts (Charge 5) are active; no strafing pass at a heart inside the hull sphere (fire from the tangent point); the sim seats the Dolphin in **Mass** (Crystal Seeding supplies crystals; the role's segment hull veto covers the lost Clean Blast); `ai.dolphin.crystalSearchRadius` 2500; `ArkEscortPilot.Disarm` always restores the home cell data. The one conflict (both branches skipping destroyed items in `AIPilot.UpdateCellContent`) kept voyage-night's wording.
- **Silent sims**: `ArkEscortSim.Boot` sets `AudioListener.volume = 0f; AudioListener.pause = true;` right after the plan check (ported from `claude/voyage-sim-farm` d4a6827fd). Commit `db00a1431`.
- Batchmode compile clean (0 `error CS`). Pushed.

## 2. Fauna-threats recommendations
Already on voyage-night (`1704c873b`): `Escort Wildlife Worm Colony Config` AggroRadius 600 / HuntIntervalSeconds 12; `Escort Swarm Leech Fauna Config` band 300-1100 (wider than the suggested 900). Nothing to change; the d6 mixed run below is the confirmation.

## 3. Sims: NOT RUN
Plan: `4:Sparrow,Sparrow,Sparrow;3:Dolphin,Dolphin,Dolphin;6:Sparrow,Squirrel,Dolphin` in one editor via `D:\VoyageLogs\run_sim_gated.ps1` (waits while any other GUI Unity.exe is open, 10-min retries up to 60 min, then `run_sim2.ps1` with the sim lock).

The gate waited the full hour (16:45-17:45) on a GUI Unity editor that is not a sim: the owner's own `Star-Writer2` editor (PID 29876, `-projectpath C:\Users\gradi\source\repos\Star-Writer2`), open the whole time. Per the rule (no editor sim beside another GUI Unity.exe; retry every 10 min; give up after about an hour) nothing was launched, so the Sparrow aim / Domain-Safe verification, the Dolphin-in-Mass blast check and the d6 fauna confirmation are still owed. Everything they need is committed and compiled. When the owner's editor is closed (or the rule is relaxed for a non-sim editor), run:

```
powershell -File D:\VoyageLogs\run_sim.ps1 -Plan "4:Sparrow,Sparrow,Sparrow;3:Dolphin,Dolphin,Dolphin;6:Sparrow,Squirrel,Dolphin" -Tag verify -Proj D:\Voyage\wt-d -Out D:\VoyageLogs\verify.jsonl
```

Read per run: `RoleNotes` (Sparrow: rockets in the tens per seat, "domain-safe on", guns tens of seconds; Dolphin: "blasts > 0", "picked up > 0", candidates rising with Mass seeding), `FriendlyFireBy` (single digits), and for the d6 mixed run the leg fields `Latches`, `WormLunges` (> 0 in The Maw with AggroRadius 600 / hunt 12 s), `ArkSips` (> 0 in The Swarm with the 300-1100 band), `WaveReleasedAt`, plus `PacifiedLegs` under default pacification.

## 4. Round-3 results this pass builds on (`D:\VoyageLogs\round3.jsonl`, before the fixes above)
- d4 Sparrow x3: Survived 5 %, 305 kills, friendly fire 26 (ramming hull-grazers on the pass); rockets 6-7 per seat with 13-17 held by the lane; guns 15-20 s of 520 s on a target. -> Domain-Safe rockets + no pass on the hull (unverified).
- d3 Dolphin x3: 2 blasts at energy 0.12, "no usable crystal" about 700 choices, 0 vetoed looks: a crystal-supply problem, not an aim problem. -> Dolphin seated in Mass (unverified).
- d3 mixed: never sailed (a destroyed Crystal in a dead world's CellItems threw in `AIPilot.UpdateCellContent` every frame). -> fixed on both branches.
