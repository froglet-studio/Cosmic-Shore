# Haul (`GameModes.Haul = 93`)

The Thresher's **MASS** game: a timed steal. Row 3 of `Docs/ElementalLoop/GAME_KIT_ROSTER.md` (Garrett, 2026-11-15:
"make all the missing games you can"). It sits beside the Thresher's other cards: Thresher Time, Jackstraw (Charge) and
Harvest (Space).

Bales of loose neutral prisms hang round the core. Every Thresher spawns in **Mass Mode**, where the ball steals every
prism it touches that is not its colour, shields every one that is, and lays a trail of its own (`R_VesselActions/
THRESHER.md` 2b). **The score is the volume standing in your colour when the clock runs out.** Swing your ball through
the bales to haul them home, sweep your hoard to armour it, swing through a rival's hoard to steal it back. Press A and
the ball smashes instead.

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameHaul.unity` (cloned from `MinigameGlasshouse.unity`: identity, monitor, cells, the controller's field fields and the four AI hulls swapped) |
| Controller | `HaulController : MultiplayerDomainGamesController` (lays the field on every peer; lead sampler; steering-only AI; whistle snapshot) |
| Field | `HaulField`: the ladder, laid on Chum's reef geometry (`ChumReef.Lay`: a centre prism and Fibonacci shells of 7 u cubes, 15 u apart) |
| AI | the controller's haulers steer; an autopiloted Thresher never presses A, so it stays in Mass Mode. Plan step `mass-arcade` |
| Metric | `ScoringMetric.VolumeRemaining` (12), reused. Genre **Mass** through `ModeGenre`'s metric fallback (no mode row) |
| Score keeping | the platform's: `StatsManager.CreditPrismSteal` credits every steal to its thief (and debits a rival victim). The mode adds nothing |
| Rule | `HaulScoringRuleSO`: timed, highest wins, `IsObjectiveReached` never fires; every line an authored template |
| Turn monitor | `HaulTimeTurnMonitor : NetworkTimeBasedTurnMonitor` |
| Round length | `EndConditionOverridesSO.haulRoundSeconds` (**90**, P-modes-02) |
| Comeback | a quarter of a reference domain (a quarter of the I2 field, 224 prisms x 343 volume = 76,832) behind buys 1.5 element levels (the generator asserts >= 1) |
| Arena | four `Haul Cell Config`s (IntensityWise): the standard 1,200 u cell with Barren's nucleus (~392 u), no environment, no flora, no fauna |
| Omni crystals | the scene's `NetworkCrystalManager` (4 / 3 / 3 / 2), respawning inside the nucleus; each is a 12 s Windmill (THRESHER.md 2c), which in Mass Mode steals everything it sweeps |
| Objective arrow | `RampageObjectiveProvider`: the nearest omni crystal, the next Windmill |
| Toasts | 168 `HaulHint` (new), 130 lead change (the controller's own sampler), 30 comeback |
| Generator | `Tools/Build/author_haul_assets.py` (`--check`) |
| Card art | `Assets/_Graphics/ARCADE/CardBackgrounds/Haul.png` (`/cardart`) |

## Design brief: Haul, 2026-11-15, claude/thresher-mass-haul

```
The fundamental it serves      Mass: prisms a Domain takes and still holds. Steal is the Thresher's Mass verb
The principles it obeys        P-modes-08 (the mode changes nothing about the vessel), P-modes-13 / P-modes-16 (the Cell
                               is the environment: a bare nucleus cell, the field it lays), P-modes-02 (90 s)
The one it comes closest to    P-modes-12 (ecology present). The cell is bare for Tapestry's reason: grazers eat the
                               colour a cell does not hold (the trailing team's fresh mass), and they would eat the bales
What it changes for a player   The Thresher's Mass card becomes a game: swing the ball through bales to steal them, sweep
                               your hoard to armour it, raid a rival's hoard, and grab a Windmill to steal in a circle
The cheapest prototype         none: built straight in Unity, because Mass Mode already steals, shields and lays
Who judges it, and where       Garrett, in the editor
The retire condition           in a playtest the bales do not matter: the trail alone decides matches, or a hoard is
                               stolen back faster than it can be armoured
```

## The intensity ladder

| | Name | Bales | Shells | Prisms | Band | Crust | Omni crystals |
|---|---|---:|---:|---:|---:|---|---:|
| I1 | Hayfield | 10 | 2 | 640 | 0.30 | no | 4 |
| I2 | Harvest | 14 | 2 | 896 | 0.45 | no | 3 |
| I3 | Granary | 14 | 3 | 2,478 | 0.45 | outer shell shielded | 3 |
| I4 | Stronghold | 18 | 3 | 3,186 | 0.60 | outer shell shielded | 2 |

- A bale prism is a 7 u cube (343 volume); the ball's Mass Mode trail prism is 8 x 0.75 x 5.5 (33 volume). One
  stolen bale prism is worth ten trail prisms, so the bales, not the trail, decide a match.
- **The crust**: a steal on a shielded prism only sheds the shield (`PrismTeamManager.Steal`), so a crusted bale takes
  two passes. Your own sweep shields what you hold, so a hoard you tend is crusted too.

## AI: steering only

`HaulController` arms every AI Thresher with `AIPilot.SetExternalTargetProvider` and presses **nothing**: an
autopiloted Thresher never presses A, so its ball stays in Mass Mode.

1. **Haul** (its domain is not trailing by more than `aiRaidDeficitFraction`): the nearest bale with unclaimed prisms,
   kept until it is hauled or `aiRetargetSeconds` pass, circled at `aiOrbitRadius` (160 u) so the ball on its chain
   swings through it.
2. **Raid** (trailing): circling the densest opposing mass (`Cell.GetExplosionTarget`), where the ball steals the
   rival's hoard back.

| Ladder (Easy, Medium, Hard, Legendary) | |
|---|---|
| `aiRaidDeficitFraction` | 1 (never), 0.3, 0.15, 0.05 |
| `aiRetargetSeconds` | 12, 9, 7, 5 |

Froglet Compute is paused, so no search was queued.

## Platform changes

- `GameModes.Haul = 93` (reserved by `GAME_KIT_ROSTER.md` row 3), `EnumIntegrityTests` 85 -> 86.
- `GameToastSituation.HaulHint = 168`.
- `EndConditionOverridesSO.haulRoundSeconds` (+ `Build` twin, getter, `LiveMatchesBuild` / `Apply` / `Capture` rows)
  and the window's field, label and build-summary rows.
- `MiniGameHUD.CreateObjectiveProviderForGameMode`: Haul -> `RampageObjectiveProvider`.
- `author_preview_spawns.SCENE_FOR_MODE` 93; `render_card_backgrounds.py` a Haul recipe and camera.

## Verification status

**Authored headless, not yet run in the editor.** What ran is listed in the PR. The edit-mode tests (`HaulTests`,
`EnumIntegrityTests`) compile in the reference compile but have not been run in Unity. The editor steps are in
`Docs/UNITY_VERIFICATION_CHECKLIST.md`.

What only the editor can judge:

- whether circling at 160 u really swings the ball through a bale (the chain's length and the swing depend on speed);
- how fast a ball strips a bale, and whether the crust's second pass reads;
- whether a hoard can be armoured faster than a rival steals it back (the retire condition).

## Known limitations

- **A client's steals** reach the score through the platform's client round-trip (`Player.ReportPrismStolen_ServerRpc`),
  as in every mode.
- **The raid target is the densest opposing mass, not the nearest** (`Cell.GetExplosionTarget`, Tapestry's limitation).
