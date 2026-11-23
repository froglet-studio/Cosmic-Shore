# Voyage integration (DONE)

Branch `claude/voyage-integration` in `D:\Voyage\wt-a`, pushed (head 8ea1cafa4).

Note: `origin/claude/voyage-night` does not exist on the remote; `claude/voyage-night` is a LOCAL branch (checked out in
another worktree, head 9102753eb "Merge claude/voyage-ark-core"). Integration was cut from that local branch.

## Merges, in order

1. `claude/voyage-pacify` (contains fauna-params): clean (a421dd7db).
2. `origin/claude/voyage-escort-roles` (22c3cac3a): conflicts in `ArkEscortReport.cs` and `ArkEscortRun.cs`, plus a
   silent semantic clash in `Ark.cs` (both branches declared `HullPrismLost`; git auto-merged both declarations).

## Conflicts resolved

- `Ark.HullPrismLost`: one event, the slot-diffed `Action<Prism, PrismKind>` (catches losses before a regrow reuses the
  slot; hold-taken plates are not losses). Escort-roles' `Action<Prism>` declaration and its `DestroyedAt`-window scan
  were removed.
- `ArkEscortRun`: one subscription (metrics partial `HookArk`). Escort-roles' handler became `CountFriendlyFire(prism)`,
  called from the unified handler, so `FriendlyFire` (run and leg) is still counted (a loss not `DestroyedByCreature`).
  Their other edits (context install/tick, prefab check in Deploy, per-seat leash in BringWingmates, OnLegChanged) are kept.
- `ArkEscortReport`: both sides' fields kept (`FriendlyFire` run + leg alongside plates/kills/pacify fields); one summary
  line carries both. `ark_escort_report.py` gains an `ff` column.
- Genes: `ArkEscortParams.ExternalSetter` now defaults to `EscortGeneRegistry.TrySet`; every gene key is listed in
  `ParamKeys` (80 keys in the sim line); `EscortGeneRegistry.ResetAll()` runs at each escort's start (before overrides
  apply) and at its finish, so ai.* overrides never leak between runs or into play after a sim.

## Verification

- Compile clean (0 `error CS`). EditMode `ArkModelTests|ArkFallTests|ArkPacingTests|Escort`: 40/40 pass.
- Sim: `run_sim.ps1 -Plan "4:Sparrow,Sparrow,Sparrow" -Tag integ_check -Proj D:\Voyage\wt-a` -> **sailed and Survived**,
  hull min 3% of 108, 853 s under way (705 s in cells, 115 s out, mean 15.9 u/s), plates lost 293 (28 shielded, 9 super,
  26 to friendly fire), 192 regrown. Per world plates lost: Garland 0, Scarab 131, Boneyard 0, Swarm 0, Maw 162.
  Seats: within 900 u ~100% of samples, within 300 u 14-18%. Output `D:\VoyageLogs\integ_check.jsonl`, log
  `D:\VoyageLogs\sim_integ_check.log`.
- Pacification fired in 4 of 5 worlds: Garland at 70 s (share 1.0), Scarab 169 s (0.89), Boneyard 163 s BEFORE the Ark
  entered (0.6), Swarm 164 s (1.0); The Maw stayed Ruby. That looks too easy for three Sparrows: the control volume of
  a lightly populated hostile nucleus is small, so the squad's trails reach the share fast. Tune `escort.pacifyShare`
  up, or add a minimum squad control volume, before reading pacify numbers.

## Sail-failure diagnosis (C:\CosmicShoreVoyage)

- "[ArkEscortSim] no harbour after 30s in the menu - entering freestyle" is NOT a failure: the passing baseline
  (`D:\VoyageLogs\sim_baseline_sparrow3b.log`) logs the same line before every successful plan.
- On `D:\Voyage\wt-a`, with ark-core + fauna-params + pacify + escort-roles merged and no other Unity.exe running
  (checked with Get-CimInstance before launch), the escort sailed and completed. So ark-core is not a sail regression.
- The C:\CosmicShoreVoyage failures ("the escort is not out") most likely came from a concurrent editor sim on the same
  Unity/online account (lobby/relay collision: this log shows `[RelayService] Bus validation failed` and a Vivox 403
  even on a solo run, so the online services are shared and contended), or from that checkout's own state. Not
  reproduced here; recommend running one sim at a time per account, or re-running there with nothing else open.

## Left open

- `Kills` (267) counts every attributed creature death in the worlds, predators included; the per-seat kills (~42
  total) are the squad's. Read `RunSeats` for squad kills.
- 26 friendly-fire plates on a three-Sparrow squad: the roles' lane tests still let rockets through the hull.
