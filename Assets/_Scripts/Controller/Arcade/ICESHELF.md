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

## Platform changes (all additive)

- `GameModes.IceShelf = 100`, `GameToastSituation.IceShelfHint = 175`, the roster's reserved ids.
- `EndConditionOverridesSO.iceShelfRoundSeconds` (+ `Build` twin, getter, the three build-value rows; no
  `TryGetAuthoredTurnTarget` row, the mode is timed) and its row in the End Game Conditions window.
- `MiniGameHUD`: Ice Shelf reuses `RampageObjectiveProvider`.

## Verification status

Authored headless; **not yet run in the Unity editor**. See the PR and `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## Known limitations

- **The AI helm raises its shield only against an incoming missile** (the crew AI's rule), so an AI builds its shelf
  from its trail and pen, and drops shields only when shot at. A mode cannot change that without changing the vessel.
- The census walks every live prism each sample. Fine for a 90 s, four-hull match; never measured.
- The comeback rate rests on an estimated reference volume, not a measured match.
