# Ice Shelf (`GameModes.IceShelf = 100`)

The Orca's **MASS** game (the game kit roster, `Docs/ElementalLoop/GAME_KIT_ROSTER.md` row 10, built 2026-11-15): a
timed hold. The score is the mass standing in your colour when the clock runs out: the helm's trail, the fast gun's
pen strokes, and the helm's **danger shield**, 144 prisms carried round the hull and then dropped where they stand,
lingering 20 s. A shield dropped in the last half minute is still standing at the whistle, so the closing seconds are
where the shelf is built. Rival gunners break your mass; wild herds graze it outside the nucleus. Highest wins.

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameIceShelf.unity` (cloned from `MinigameGlasshouse.unity`) |
| Controller | `IceShelfController : MultiplayerDomainGamesController` |
| AI | the crew's own gunners (`OrcaCrewAI`), plus `OrcaArcadeHelm` (goal Shelf), which flies each AI hull round its own loop |
| Metric | `ScoringMetric.VolumeRemaining` (12), reused, and SET by the census (below) |
| Rule | `IceShelfScoringRuleSO : GlasshouseScoringRuleSO` (timed, highest wins, every line an authored template). Its second column is the shelf: the part of a pilot's volume still in their shield |
| Turn monitor | `IceShelfTimeTurnMonitor : NetworkTimeBasedTurnMonitor` |
| Round | `EndConditionOverridesSO.iceShelfRoundSeconds` (**90**, `Docs/ARCADE_MATCH_LENGTH.md`) |
| Comeback | rate **0.0004** against a reference domain of ~13,200 volume (one Orca's 90 s trail plus two standing shields): a quarter behind buys 1.3 element levels |
| Arena | Glasshouse's four cells, REFERENCED: the standard cell with a nucleus, its grazing herds, nothing planted wild |
| Spawn | Glasshouse's: ring floor 450 at every rung |
| Objective arrow | `RampageObjectiveProvider` |
| Toasts | 130 (the lead, posted by the controller's own sampler), 175 (the hint, new), 30 (comeback) |
| Settings | `IceShelfSettings.asset` (`OrcaArcadeSettingsSO`) |
| Generator | `Tools/Build/author_ice_shelf_assets.py` (`--check`), on `orca_games_common.py` |
| Card art | `Assets/_Graphics/ARCADE/CardBackgrounds/IceShelf.png` (`/cardart`) |

## How the score is kept: the census

`StatsManager` moves a pilot's VolumeRemaining by events: up when a prism finishes creating, down when one is
destroyed. Two of the Orca's three producers retire prisms another way. A dropped shield expires after
`shieldLinger`, and the fast gun's pen recycles its oldest stroke past `fastGunPenCap`; both hand the prism straight
back to its pool (`Prism.ReturnToPool`), which raises no destroyed event (read in `OrcaCrew` and
`StatsManager.PrismDestroyed`; not run). Left to the events, every shield ever raised would still count at the
whistle.

So every `sampleSeconds` the server walks the live prisms (`IceShelfCensus`, over
`PrismSpatialIndex.CopyLivePrisms`), sums each one's volume under its owner's name, and SETS that pilot's
VolumeRemaining to it. Prisms whose owner id carries `::OrcaShield::` are also summed into the shelf column. One last
census runs at the whistle, and the snapshot ClientRpc carries both columns, because only the server measured.

## AI

The helm (`OrcaArcadeHelm`, goal Shelf) gives each AI hull a home spread round the nucleus at 0.6 of its radius and
flies it round a six-point loop there (`aiShelfLoopRadius` 160), so its trail closes a ring. The gunners shoot rival
mass and hulls on their own. When every magazine is low it fetches an omni crystal.

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

- `GameModes.IceShelf = 100`, `GameToastSituation.IceShelfHint = 175`, the roster's reserved ids.
- `EndConditionOverridesSO.iceShelfRoundSeconds` (+ `Build` twin, getter, the three build-value rows; no
  `TryGetAuthoredTurnTarget` row, the mode is timed) and its row in the End Game Conditions window.
- `MiniGameHUD`: Ice Shelf reuses `RampageObjectiveProvider`.

## Verification status

Authored headless; **not yet run in the Unity editor**. See the PR and `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## Known limitations

- The AI helm now shields on cooldown in this game (above), but it does not time its shells for the whistle, so the
  shelf standing at the end is no bigger for it.
- The census walks every live prism each sample. Fine for a 90 s, four-hull match; never measured.
- The comeback rate rests on an estimated reference volume, not a measured match.
