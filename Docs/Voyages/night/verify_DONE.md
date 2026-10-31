# Verify pass - DONE

Branch `claude/voyage-verify` (from `origin/claude/voyage-night` f0f1410a3), worktree `D:\Voyage\wt-d`, 2026-10-31.

## 1. Code
- Merged `claude/voyage-escort-roles` tip (`49839f640`, the round-3 follow-ups that voyage-night did not yet have): Sparrow rockets ignore the Ark lane when Domain-Safe Skybursts (Charge 5) are active; no strafing pass at a heart inside the hull sphere (fire from the tangent point); the sim seats the Dolphin in **Mass** (Crystal Seeding supplies crystals; the role's segment hull veto covers the lost Clean Blast); `ai.dolphin.crystalSearchRadius` 2500; `ArkEscortPilot.Disarm` always restores the home cell data. The one conflict (both branches skipping destroyed items in `AIPilot.UpdateCellContent`) kept voyage-night's wording.
- **Silent sims**: `ArkEscortSim.Boot` sets `AudioListener.volume = 0f; AudioListener.pause = true;` right after the plan check (ported from `claude/voyage-sim-farm` d4a6827fd). Commit `db00a1431`.
- Batchmode compile clean (0 `error CS`). Pushed.

## 2. Fauna-threats recommendations
Already on voyage-night (`1704c873b`): `Escort Wildlife Worm Colony Config` AggroRadius 600 / HuntIntervalSeconds 12; `Escort Swarm Leech Fauna Config` band 300-1100 (wider than the suggested 900). Nothing to change; the d6 mixed run below is the confirmation.

## 3. Sims
Plan: `4:Sparrow,Sparrow,Sparrow;3:Dolphin,Dolphin,Dolphin;6:Sparrow,Squirrel,Dolphin` in one editor via `D:\VoyageLogs\run_sim_gated.ps1` (waits while any other GUI Unity.exe is open, 10-min retries up to 60 min, then `run_sim2.ps1` with the sim lock). Output `D:\VoyageLogs\verify.jsonl`, log `sim_verify.log`.

(results below)
