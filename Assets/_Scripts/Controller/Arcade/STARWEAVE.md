# Starweave (`GameModes.Starweave = 92`)

The Brittle Star's **MASS** game: a timed weaving war. Row 2 of `Docs/ElementalLoop/GAME_KIT_ROSTER.md` (Garrett,
2026-11-15: "make all the missing games you can"). It sits beside the Brittle Star's other cards: Brittle Star Time,
Flatline (Charge) and Shatterplane (Space).

Creation Mode (B) lays a trail behind each of the ring's five corners at half cruise, and Mass fattens every block
(`R_VesselActions/BRITTLE_STAR.md` 3.2). **The score is the volume standing in your colour when the clock runs out.**
So the slowest mode is the one that scores, and every rival's guns, cords and ring are aimed at what you laid: weave, or
raid, never both.

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameStarweave.unity` (cloned from `MinigameGlasshouse.unity`: identity, monitor, cells and the four AI hulls swapped) |
| Controller | `StarweaveController : MultiplayerDomainGamesController` (Tapestry's shape: crystal scatter, lead sampler, whistle snapshot; a steering-only AI) |
| AI | the controller's weavers steer; the Brittle Star's own input-only autopilot presses the modes (below). Plan step `mass-arcade` |
| Metric | `ScoringMetric.VolumeRemaining` (12), reused. Genre **Mass** through `ModeGenre`'s metric fallback (no mode row) |
| Rule | `StarweaveScoringRuleSO`: timed, highest wins, `IsObjectiveReached` never fires; every line an authored template |
| Turn monitor | `StarweaveTimeTurnMonitor : NetworkTimeBasedTurnMonitor` |
| Round length | `EndConditionOverridesSO.starweaveRoundSeconds` (**90**, P-modes-02) |
| Comeback | derived from the shipped hull: 5 corners x 22.5 u/s / 6 u = 18.75 blocks/s x 6 volume = **112.5 volume/s** per weaving pilot; a quarter of a reference domain (two pilots weaving half the round, 10,125) behind buys 1.5 element levels (the generator asserts >= 1) |
| Arena | four `Starweave Cell Config`s (IntensityWise): the standard 1,200 u cell with Barren's nucleus (~392 u), no environment, no flora, no fauna |
| Crystals | elemental scatter 24 / 16 / 10 / 6 (the controller, peer-local and seeded); omni 4 / 3 / 3 / 2 inside the nucleus (the scene's CrystalManager) |
| Objective arrow | `BendsObjectiveProvider` (shared with Tapestry): the nearest rival, whose freshest weave is behind them |
| Toasts | 167 `StarweaveHint` (new), 130 lead change (the controller's own sampler), 30 comeback |
| Generator | `Tools/Build/author_starweave_assets.py` (`--check`) |
| Card art | `Assets/_Graphics/ARCADE/CardBackgrounds/Starweave.png` (`/cardart`) |

## Design brief: Starweave, 2026-11-15, claude/brittlestar-mass

```
The fundamental it serves      Mass: prisms a Domain lays and still holds. The score is the volume standing in your
                               colour at the whistle
The principles it obeys        P-modes-08 (the mode changes nothing about the vessel), P-modes-13 / P-modes-16 (the Cell
                               is the environment: a bare nucleus cell, the crystal scatter), P-modes-02 (90 s)
The one it comes closest to    P-modes-12 (ecology present). The cell is deliberately bare, for Tapestry's reason: grazers
                               eat the colour a cell does not hold, which is the trailing team's fresh mass, an
                               anti-comeback current in a mode whose economy is contested mass
What it changes for a player   The Brittle Star's Mass card becomes a game: slow down to weave five trails at once, fly
                               fast to fetch crystals, switch to guns and cords to raid a rival's weave, and guard yours
The cheapest prototype         none: built straight in Unity, because every mechanic is already shipped on the Brittle
                               Star; the Vessel Studio was offered as an option
Who judges it, and where       Garrett (the Brittle Star's designer), in the editor
The retire condition           in a playtest Creation Mode does not matter: cruising and raiding beat weaving, or a weave
                               is gone to the guns faster than it is laid
```

## The intensity ladder

| | Name | Elemental crystals | Omni crystals | Starting Space |
|---|---|---:|---:|---|
| I1 | Loom | 24 | 4 | 0 |
| I2 | Weave | 16 | 3 | 0 |
| I3 | Crossfire | 10 | 3 | 3 (the guns reach further) |
| I4 | Siege | 6 | 2 | 5 |

- A Mass crystal fattens every block (x1 -> x2 on x and y at Mass 10); a Space crystal lengthens the guns (x1 -> x2).
- The starting Space is the card's `StartingElements` (Brittle Star Time's precedent), never a kit change. It makes a
  raid reach further, so a weave must be guarded harder: intensity is scarcity of fattening and reach of the raiders.
- Phase thresholds: the cell grows nothing, so Frenzy sits 25 % above the most four pilots can weave in a round at
  Mass 10 and never fires.

## AI: steering only

`StarweaveController` arms every AI Brittle Star with `AIPilot.SetExternalTargetProvider` and presses **nothing**. The
Brittle Star's own autopilot (`BrittleStarExecutor.Autopilot`, BRITTLE_STAR.md 6) already picks **Creation** when no
rival mass lies ahead and its target is near the nose, and **Space** or **Charge** when rival mass is ahead. So:

1. **Weave** (its domain is not trailing by more than `aiRaidDeficitFraction`): a slow orbit of the cell on its own
   great circle (`aiWeaveOrbitFraction` 0.55 of 900 u, 4 deg/s), detouring for an elemental crystal within
   `aiCrystalReach`.
2. **Raid** (trailing, or in the last `aiLateRaidSeconds`): straight at the densest opposing mass
   (`Cell.GetExplosionTarget`).

Difficulty is the seat's AI level, never the intensity:

| Ladder (Easy, Medium, Hard, Legendary) | |
|---|---|
| `aiRaidDeficitFraction` | 1 (never), 0.25, 0.1, 0.05 |
| `aiLateRaidSeconds` | 0 (never), 15, 25, 35 |
| `aiCrystalReach` | 250, 400, 550, 750 |

Known AI caveat: when the fleet's boost policy (`BrittleStarSpeedModeAIPolicySO`) wants Speed, the autopilot takes Speed
over Creation; the mode does not override it (the vessel rule). Froglet Compute is paused, so no search was queued.

## Platform changes

- `GameModes.Starweave = 92` (reserved by `GAME_KIT_ROSTER.md` row 2), `EnumIntegrityTests` 84 -> 85.
- `GameToastSituation.StarweaveHint = 167`.
- `EndConditionOverridesSO.starweaveRoundSeconds` (+ `Build` twin, getter, `LiveMatchesBuild` / `Apply` / `Capture`
  rows) and the window's field, label and build-summary rows.
- `MiniGameHUD.CreateObjectiveProviderForGameMode`: Starweave shares Tapestry's `BendsObjectiveProvider`.
- `author_preview_spawns.SCENE_FOR_MODE` 92; `render_card_backgrounds.py` a Starweave recipe and camera.

## Verification status

**Authored headless, not yet run in the editor.** What ran is listed in the PR. The edit-mode tests
(`StarweaveScoringTests`, `EnumIntegrityTests`) compile in the reference compile but have not been run in Unity. The
editor steps are in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

What only the editor can judge:

- whether Creation Mode's weave outscores cruising (45 volume/s) by enough to be worth flying at half speed;
- how fast a rival's five guns strip a weave, against how fast it is laid;
- whether the AI's autopilot really holds Creation on its orbit, or flips to Speed;
- the six-figure volume columns on the HUD (Tapestry's known limitation).

## Known limitations

- **The weave model assumes Mass 0 and a straight path.** Boost, Mass and turns are not modelled; the comeback is
  conservative against Mass, which only makes volume bigger.
- **The raid target is the densest opposing mass, not the nearest** (`Cell.GetExplosionTarget`, Tapestry's limitation).
