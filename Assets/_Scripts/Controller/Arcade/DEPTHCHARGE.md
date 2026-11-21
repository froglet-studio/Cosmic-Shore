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

## The AI

The crew's own gunners (`OrcaCrewAI`) and `OrcaArcadeHelm` (goal Raid). The shield rule is the same as Ice Shelf's,
measured in both games together (`ICESHELF.md`, The helm's shield trained here).

### The helm's shield trained here (2026-11-21, elemental loop step 3)

The AI helm raised its shield only against a closing rival missile (the lab's helmAI), so in the Orca's own Mass and
Space games it almost never pressed its Mass ability. Raced on the Mass-Space volume books (each pilot's VC + VD,
`brood_judge.py --mode IceShelf` / `--mode DepthCharge`, Amoebius headless, Orca against Orca), the crew's other AI
knobs did nothing useful (seeds 1-2, I2: no burn -20% / -11%, `aiRamRange` 900 +2% / -22%, gunner
`aiRetargetSeconds` 1.5 -9% / +89%, the last noise-level in one game). The knob that moved it is a new AI field on
`OrcaCrewConfig.asset`, **`aiShieldWhenReady` on**: the helm also raises the shield whenever its cooldown is up. It
applies only where the mode's arcade category is Mass-Space (`ModePreviewLibrarySO.CategoryOf`: Ice Shelf, Depth
Charge, Brood Rush); in a race the shell catches the course (Breach, 3 seeds: 104.7 s -> 112.6 s ungated), so Breach
and every other mode keep the lab's rule (gated: 103.2 s, seeds 1 and 3 identical to the shipped AI). The shield's
cooldown, shell and linger are the kit's, untouched.

| Mean seat VC + VD (seeds 11-12, 4 seats) | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| Ice Shelf shipped -> trained | 157k -> 211k | 149k -> 269k | 152k -> 226k | 158k -> 254k |
| Depth Charge shipped -> trained | 135k -> 462k | 127k -> 466k | 163k -> 463k | 183k -> 466k |

**Read this before using those numbers.** Most of the gain is the books' shield column: a shell going up books its
volume as created and coming off books it as lost (`StatsManager.PrismVolumeModified`), so VC is a gross figure and
Lost rose by nearly as much. What stands at the end moved the other way in Ice Shelf: `held` (volume standing at the
whistle, the game's own VolumeRemaining) fell 18.7k -> 13.5k, 22.6k -> 15.3k, 17.5k -> 13.6k, 17.4k -> 14.7k on I1-I4
(on seeds 1-2 at I2 it was 13.9k -> 14.4k). In Depth Charge held rose on I1-I3 (25.6k -> 30.5k, 24.1k -> 26.1k,
20.6k -> 23.2k) and fell on I4 (25.7k -> 22.2k), and VD (rival volume taken) fell slightly (5.0k -> 2.2k, 7.4k -> 6.4k,
7.0k -> 5.9k, 9.2k -> 7.8k). Raw runs: `/mnt/project-files/regatta-flywheel/mass7/orca/`.

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
- The AI helm shields on cooldown here and in Ice Shelf; every mode outside Mass-Space keeps the crew AI's rule (incoming missiles only).
