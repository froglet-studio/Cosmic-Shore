# Depth Charge (`GameModes.DepthCharge = 101`)

The Orca's **SPACE** game (the game kit roster, `Docs/ElementalLoop/GAME_KIT_ROSTER.md` row 11, built 2026-11-15): a
raid on the rival forest. Rampage's arena is a forest of plants in all three colours; every **hostile prism** your
domain breaks - with a missile, the sniper, the fast gun or the bomb - counts. The first **domain** to the target
wins, golf-timed.

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameDepthCharge.unity` (cloned from `MinigameHarvest.unity`) |
| Controller | `DepthChargeController : MultiplayerDomainGamesController` |
| AI | the crew's own gunners (`OrcaCrewAI`), plus `OrcaArcadeHelm` (goal Raid), which flies each AI hull to the nearest plant not in its colour |
| Metric | `ScoringMetric.PrismsDestroyed` (5), reused (Rampage's and Harvest's) |
| Rule | `DepthChargeScoringRuleSO : HarvestScoringRuleSO` (golf, Harvest's own words on its own asset) |
| Turn monitor | `DepthChargePrismTurnMonitor` (Harvest's shape with its own target) |
| Target | `EndConditionOverridesSO.depthChargePrismTarget` (**1,500**, a first cut) |
| Comeback | rate **0.006**: a quarter behind (375 prisms) buys 2.25 element levels |
| Arena | Rampage's four cells, REFERENCED: a grown forest from just outside the nucleus to the membrane, all three domains, ~59,600 prisms at I1 thinning to ~11,900 at I4 |
| Spawn | Rampage's: 500 u outside the nucleus, no floor |
| Crystals | Harvest's ladder (2 / 1 / 1 per player, then 1) |
| Objective arrow | `RampageObjectiveProvider` |
| Toasts | 83 (every 250 prisms), 128-130 (shared race beats), 176 (the hint, new), 30 (comeback) |
| Settings | `DepthChargeSettings.asset` (`OrcaArcadeSettingsSO`) |
| Generator | `Tools/Build/author_depth_charge_assets.py` (`--check`), on `orca_games_common.py` |
| Card art | `Assets/_Graphics/ARCADE/CardBackgrounds/DepthCharge.png` (`/cardart`) |

## The loop

The Orca's Space seat is the sniper, the long gun. The forest is the
target and the cover at once: a missile flown into a rival plant breaks a cluster, the bomb takes a whole one, and
the guns pick at whatever is left. Rival gunners are doing the same to your colour, which is not your score but is
your cover. Intensity thins the forest, so at I4 every hostile prism is further from the last.

## Platform changes (all additive)

- `GameModes.DepthCharge = 101`, `GameToastSituation.DepthChargeHint = 176`, the roster's reserved ids.
- `EndConditionOverridesSO.depthChargePrismTarget` (+ `Build` twin, getter, `TryGetAuthoredTurnTarget` row, the
  three build-value rows) and its row in the End Game Conditions window.
- `MiniGameHUD`: Depth Charge reuses `RampageObjectiveProvider`.

## Verification status

Authored headless; **not yet run in the Unity editor**. See the PR and `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## Known limitations

- The target (1,500) assumes the crew breaks rival forest at Rampage's pace; nothing has measured it.
- The forest thins with intensity (Rampage's ladder), so I4 is a longer hunt for the same target. That is the
  intended difficulty, but the target may want a per-intensity scale once someone plays it.
- The AI helm's shield rule is the crew AI's (incoming missiles only), as in the other two Orca games.
