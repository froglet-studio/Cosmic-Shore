# Flatline (`GameModes.Flatline = 84`)

The Brittle Star's **CHARGE** game: a duel through thin **panes** of prisms. Picked by Shombith on 2026-11-09 from
three drawn ideas (Saber Ring, Flatline, Tangle), with the rule "only the bullets and chords should score points".

> **Status.** Authored headless on 2026-11-09 (`claude/great-rubin-rur3xs`). The arena's C# geometry was compiled
> and RUN outside the editor and matches its Python twin prism for prism. **Not run in the Unity editor; nobody has
> flown it.** The hand tests are in `Docs/UNITY_VERIFICATION_CHECKLIST.md` § Flatline.

## The idea, in the vessel's own terms

Nothing about the Brittle Star changes (the LOCKED vessel rule). Every pane has a job for the kit it already has
(`R_VesselActions/BRITTLE_STAR.md`):

| The kit | What a pane does to it |
|---|---|
| **Rounds** (one turret, or Space Mode's five; the Sparrow's round, stopped by the first prism) | A pane is **cover**: a pilot behind one cannot be shot. |
| **Cords** (Charge Mode; 120 u whips, 80 u sabers while RT is held; they slice every prism they sweep) | A pane is **no cover at all**: the cords reach through it and strike the pilot hiding behind it. |
| **The ring** (LT with a spare; flies ~264 u, bursts in a flat disk across its own line of flight: 400 u across here, because the cell has no nucleus and `BrittleStarExecutor.NucleusRadiusAt` falls back to `ringBlastFallbackRadius` 200) | A ring thrown **face-on** into a pane bursts on contact, flat along the pane: it erases the pane's plain tiles within 200 u of the burst and catches whoever hugs it there. The tell is the spinning ring coming at you; the answer is to **get off the pane's plane**. |
| **Crystals** (every crystal list on the hull's impactor adds one spare ring, up to two) | Crystals sit on the **seams** where panes cross: the places a ring thrown along either pane reaches. Re-arming is the risk. |

**Ribs.** Every Nth grid line of every pane is **super-shielded** (nothing breaks it). The plain tiles between the ribs
are what cords and rings wear away, so a pane the match has worked over becomes a lattice that still reads as a plane,
not nothing. Danger rims (I3, I4) cost a sloppy pass.

## Scoring (`FlatlineScoringRuleSO`)

`ScoringMetric.CombatPoints` (reused), golf-timed, first **DOMAIN** to the target wins. Only hull-on-hull hits score, at
the fleet price (`CombatHitDrain.PointsFor`, the list Broadside pays, ten points to the petal):

| Hit | Class (reported by the vessel, not chosen here) | Latch per victim | Points |
|---|---|---|---|
| A round on a rival hull | `Bullet` (`SparrowProjectile` → `SparrowFullAutoProjectileImpactContainer` → `VesselCombatHitByBullet`) | 0.05 s | **1** |
| A cord, or a thrown saber (Charge L5), across a rival hull | `Strike` (`BrittleStarExecutor.StrikeHullsWithCords` / `StrikeHullsWithSegment`) | 0.5 s | **8** |
| The ring blast | `MissileBlast` (`BrittleStarConfig.ringBlastHitClass` 3) | 0.5 s | **0** |
| Anything else | | | 0 |

The ring still bursts, still breaks panes and still takes petals and silences (the vessel is unchanged); it just pays
nothing here. The generator fails if the fleet price, the round's class or the cords' class moves.

The results column shows each pilot's `{strikes} strikes, {rounds} rounds` (authored template).

## The targets, per intensity (`EndConditionOverridesSO.flatlinePointTargetByIntensity`)

| Intensity | I1 Three Planes | I2 Five Pages | I3 Icosa Panes | I4 Pane Geode |
|---|---|---|---|---|
| Points to win | **180** | **200** | **220** | **240** |

Scalar fallback 200 (`DefaultFlatlinePointTarget`); a rung at 0 falls back to it (Cleave's chain). Resolved on the
server by `FlatlinePointTurnMonitor` and replicated.

**These are estimates, aimed at a 1v1 near four minutes** (a 2v2 domain sums two pilots and runs about half that).
The model, per pilot: points/s = engaged share × ((1 − cord share) × line-of-sight share × 3 rounds/s + cord share ×
one Strike per 3 s pass × 8):

| | engaged | cord share | line of sight | points/min | × 4 min |
|---|---|---|---|---|---|
| I1 | 0.35 | 0.25 | 0.70 | ~47 | ~190 |
| I2 | 0.45 | 0.35 | 0.55 | ~54 | ~215 |
| I3 | 0.50 | 0.45 | 0.45 | ~59 | ~235 |
| I4 | 0.55 | 0.55 | 0.35 | ~65 | ~260 |

Denser panes put more of each fight inside cord reach, where a Strike pays eight rounds, so points come faster up the
ladder and the target rises with it (rounded down to 180 / 200 / 220 / 240). Every input above is a guess until the
per-attacker petal ledger (`SILENCE.md` §6) measures a real match; re-tune in FrogletTools > Game Modes > End Game
Conditions after the first timed matches (checklist).

**Comeback** rate **0.03**: a quarter of the smallest target behind (45 points) buys 1.35 element levels, a quarter
of the largest (60) 1.8.

## The ladder (`SpawnableFlatline`, `FlatlinePanes`, census `Tools/Build/flatline_budget.py`)

A tile is 10 × 10 × 2 u, laid a hair under its cell. All tiles neutral (Blue). Standard 1,200 u membrane, no nucleus,
no flora, no fauna. Spawn ring 420 u (every pane reaches 240 u at most), facing the centre.

| Rung | Panes | Radius | Ribs | Danger | Holes | Crystals | Prisms (plain / danger / super) |
|---|---|---|---|---|---|---|---|
| I1 Three Planes | 3, on the axes: eight open rooms | 230 u | every 6th line | none | none | 6, ±110 u on the 3 seams | 4,819 (3,456 / 0 / 1,363) |
| I2 Five Pages | 5 round the y spine, 36° apart: ten narrow rooms | 240 u | every 6th | none | none | 5 up the spine, 60 u apart | 8,393 (6,000 / 0 / 2,393) |
| I3 Icosa Panes | 6, normals on the icosahedral axes, crossing at every angle | 210 u | every 6th | 2 outer rings | none | 6 on 6 of the 15 seams, 110 u out | 7,217 (4,032 / 1,372 / 1,813) |
| I4 Pane Geode | 10, standing 150 u off the centre: a hollow shell with two open faces | 150 u | every 5th | 2 outer rings | 25% of 4×4 cells | 4 in the cracks (~176 u out) | 5,440 (2,162 / 1,568 / 1,710) |

**Flush.** Where two panes cross, the later pane leaves a slot 8.5 u either side of the earlier pane's plane (a tile
reaches at most 7.14 u along any direction, plus the earlier tile's 1 u half-thickness). `FlatlineTests` checks every
nearby cross-pane pair is separated along a normal; `Seams_CarveTheLaterPanes` is its negative control.

**Exact twin.** `flatline_budget.py` mirrors `FlatlinePanes.cs`: the per-pane tile set is integer arithmetic, the seam
test runs in float32 in the C#'s order, the holes are `Hash01` of the grid cell. Measured 2026-11-09: the C# compiled
against the card-art Unity shim and run, and the twin, agree on every count and every crystal site on all four rungs.
The generator imports the twin for PhaseThresholds and asserts the C# constants it counts; `FlatlineTests` pins the
C# census.

## AI (`FlatlineHunter`, server-only)

The Brittle Star already flies its own weapons with the replicated controls a human presses
(`BrittleStarExecutor.Autopilot`): it holds RT when a rival hull is on the nose, takes Charge Mode against a dense wall
inside cord reach (a pane is one), Space Mode against one further out, and throws its ring into a big cluster. The
hunter therefore only **steers** (`AIPilot.SetExternalTargetProvider`), and never presses:

- no spare ring and a crystal within `aiCrystalDetourRange` → that crystal;
- else the chosen rival, led by `aiLeadSeconds` of its flight, so it ends on the nose and inside cord reach;
- no rival → the centre.

Ladders keyed on each seat's AI difficulty, never the intensity (Easy → Legendary): retarget 1.6 / 1.1 / 0.7 / 0.4 s,
lead 0 / 0.25 / 0.5 / 0.75 s, crystal detour 120 / 180 / 260 / 340 u; humans count 1.5× nearer.

**Known limits.** The hunter does not dodge a ring (it cannot see one in flight without reading the executor's
private state), and the executor picks Charge Mode from prism density, not from a rival in reach, so most AI Strikes
will come from fights near panes. Both are AI-side follow-ups that need no vessel change.

## Files

| Role | Path |
|---|---|
| Controller | `Arcade/Flatline/FlatlineController.cs` (sibling of `SerpentGulagController`; the crystal field on the seams) |
| Rule / monitor / settings / AI | `FlatlineScoringRuleSO.cs`, `TurnMonitors/FlatlinePointTurnMonitor.cs`, `FlatlineSettingsSO.cs`, `FlatlineHunter.cs` |
| Arena | `Environment/MiniGameObjects/SpawnableFlatline.cs`, `FlatlinePanes.cs` (pure) |
| Census twin | `Tools/Build/flatline_budget.py` |
| Generator | `Tools/Build/author_flatline_assets.py` (`--check`) |
| Assets | `_SO_Assets/Games/ArcadeGameFlatline.asset`, `FlatlineSettings.asset`, `Scoring Rules/FlatlineScoringRule.asset`, `Cell Configs/Flatline Cell/`, `_Prefabs/Spawnables/SpawnableFlatline1-4.prefab`, `Game Toasts/GameToastConfig_Flatline.asset`, `Mode Previews/ModePreview_Flatline.asset`, `_Scenes/Multiplayer Scenes/MinigameFlatline.unity` (cloned from Serpent Gulag's) |
| Tests | `Tests/Editor/FlatlineTests.cs` |
| Platform rows | `GameModes.Flatline = 84`, `GameToastSituation.FlatlineHint = 159`, `EndConditionOverridesSO` (+ window), `MiniGameHUD` (the Bends' provider: the nearest rival), `author_preview_spawns.py` |
