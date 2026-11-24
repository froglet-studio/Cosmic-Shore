# Flatline (`GameModes.Flatline = 84`)

The Brittle Star's **CHARGE** game: a duel through an **open field** of thin-paned structures scattered through the
cell: loose panes, tunnels to fly through and windowed cages to fly into. Picked by Shombith on 2026-11-09 from three
drawn ideas (Saber Ring, Flatline, Tangle), with the rule "only the bullets and chords should score points".

**Redesigned 2026-11-10** (Shombith, on the first build of crossing planes: "These shapes should be scattered or open
designed where we can go inside and more, right now brittlestar game is not fun, also make the score to win at 90").
The four crossing-plane arenas (Three Planes, Five Pages, Icosa Panes, Pane Geode) were replaced by the scattered
field below, and the target became 90 at every intensity.

> **Status.** Authored headless on 2026-11-09, re-laid as the open field on 2026-11-10 (`claude/great-rubin-rur3xs`). The arena's C# geometry was compiled
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
| **Crystals** (every crystal list on the hull's impactor adds one spare ring, up to two) | Crystals float **inside the rooms** first (a tunnel's bore, a cage's middle), then in front of loose panes. Re-arming means flying in, where the cords reach through the walls. |

**Ribs.** Every 4th interior grid line of every wall is **super-shielded** (nothing breaks it). The plain tiles between
the ribs are what cords and rings wear away, so a wall the match has worked over becomes a lattice that still reads as
a plane, not nothing. Danger rims (I3, I4) cost a sloppy pass.

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

## The target (`EndConditionOverridesSO.flatlinePointTargetByIntensity`)

**90 points to win at every intensity** (Shombith, 2026-11-10: "make the score to win at 90"). About eleven cord
strikes, or ninety rounds, or any mix. Scalar fallback 90 (`DefaultFlatlinePointTarget`); the per-intensity list
stays (Cleave's shape, all four rungs 90) so one rung can be re-tuned on its own in FrogletTools > Game Modes > End
Game Conditions once timed matches say so. Resolved on the server by `FlatlinePointTurnMonitor` and replicated.

**Comeback** rate **0.05**: a quarter of the target behind (22.5 points, about three strikes) buys 1.125 element
levels.

## The ladder (`SpawnableFlatline`, `FlatlinePanes`, census `Tools/Build/flatline_budget.py`)

A tile is 10 × 10 × 2 u, laid a hair under its cell. All tiles neutral (Blue). Standard 1,200 u membrane, no nucleus,
no flora, no fauna. Spawn ring 680 u (every structure reaches 549 u at most), facing the centre.

Three kinds of structure, each with its own frame (an axis and two across it):
- **Loose pane**: one square plate, (2h+1)² tiles. Cover from rounds, no cover from cords.
- **Tunnel**: four walls of a hollow cube, open at both ends: fly through it. Room half-size 4 tiles, walls half a tile
  beyond it, so the bore is 80 u clear.
- **Cage**: six walls, each with a 3 × 3 tile **window** in the middle: fly into it from any side.

**Open by construction.** Structures are placed rooms first (cages, then tunnels), then panes. Each takes the first of
up to 64 hashed draws whose bounding sphere clears every earlier one by **`Gap` 40 u**, spread evenly by volume through
the shell between the inner and outer radius. So no two structures touch or interpenetrate, there is always a way
round, and nothing needs the old seam carving. The generator fails if any structure finds no room
(`FlatlineTests.Field_IsOpen_EveryStructurePlacedWithAGapRoundIt` checks the same on the C#).

| Rung | Field | Structures | Danger | Holes | Crystals | Prisms (plain / danger / super) |
|---|---|---|---|---|---|---|
| I1 Scattered Panes | 80-400 u | 24 loose panes (half 3-4) | none | none | 6, in front of panes | 1,560 (1,200 / 0 / 360) |
| I2 Tunnels | 80-420 u | 5 tunnels, 20 panes | none | none | 6: one in every tunnel, one at a pane | 2,280 (1,720 / 0 / 560) |
| I3 Cages | 80-440 u | 4 cages, 4 tunnels, 18 panes | rims of every 3rd pane | none | 6, all inside rooms | 2,914 (2,100 / 168 / 646) |
| I4 Maze | 60-460 u | 6 cages, 4 tunnels, 22 panes (half 2-4) | every 2nd pane, every 3rd room | 20% of 2×2 cells on panes | 5, all inside rooms | 3,194 (1,884 / 640 / 670) |

**Exact twin.** `flatline_budget.py` mirrors `FlatlinePanes.cs` in float32 in the C#'s order (the placement hashes,
the cube-root radius, the sphere test, the frame rule, the wall rule, the hole hash). Measured 2026-11-10: the C#
compiled against the card-art Unity shim and run, and the twin, agree on every count, every placement and every
crystal site on all four rungs. The generator imports the twin for PhaseThresholds and asserts the C# constants it
counts; `FlatlineTests` pins the C# census. `audit_prism_clipping.py SpawnableFlatline`: 0 clipping.

## AI (`FlatlineHunter`, server-only)

The Brittle Star already flies its own weapons with the replicated controls a human presses
(`BrittleStarExecutor.Autopilot`): it holds RT when a rival hull is on the nose, takes Charge Mode against a dense wall
inside cord reach (a wall is one), Space Mode against one further out, and throws its ring into a big cluster. The
hunter therefore only **steers** (`AIPilot.SetExternalTargetProvider`), and never presses:

- no spare ring and a crystal within `aiCrystalDetourRange` → that crystal;
- else the chosen rival, led by `aiLeadSeconds` of its flight, so it ends on the nose and inside cord reach;
- no rival → the centre.

Ladders keyed on each seat's AI difficulty, never the intensity (Easy → Legendary): retarget 1.6 / 1.1 / 0.7 / 0.4 s,
lead 0 / 0.25 / 0.5 / 0.75 s, crystal detour 120 / 180 / 260 / 340 u; humans count 1.5× nearer.

**Known limits.** The hunter does not dodge a ring (it cannot see one in flight without reading the executor's
private state), and the executor picks Charge Mode from prism density, not from a rival in reach, so most AI Strikes
will come from fights near walls. Both are AI-side follow-ups that need no vessel change.

## Files

| Role | Path |
|---|---|
| Controller | `Arcade/Flatline/FlatlineController.cs` (sibling of `SerpentHoodController`, Hood, formerly Serpent Gulag; the crystal field inside the rooms) |
| Rule / monitor / settings / AI | `FlatlineScoringRuleSO.cs`, `TurnMonitors/FlatlinePointTurnMonitor.cs`, `FlatlineSettingsSO.cs`, `FlatlineHunter.cs` |
| Arena | `Environment/MiniGameObjects/SpawnableFlatline.cs`, `FlatlinePanes.cs` (pure) |
| Census twin | `Tools/Build/flatline_budget.py` |
| Generator | `Tools/Build/author_flatline_assets.py` (`--check`) |
| Assets | `_SO_Assets/Games/ArcadeGameFlatline.asset`, `FlatlineSettings.asset`, `Scoring Rules/FlatlineScoringRule.asset`, `Cell Configs/Flatline Cell/`, `_Prefabs/Spawnables/SpawnableFlatline1-4.prefab`, `Game Toasts/GameToastConfig_Flatline.asset`, `Mode Previews/ModePreview_Flatline.asset`, `_Scenes/Multiplayer Scenes/MinigameFlatline.unity` (cloned from Serpent Gulag's) |
| Tests | `Tests/Editor/FlatlineTests.cs` |
| Platform rows | `GameModes.Flatline = 84` and `GameToastSituation.FlatlineHint = 159` (Jackstraw claimed the same two the same evening; Shombith gave them to Flatline and Jackstraw moved to 85 / 160, #1866); a cord or saber Strike posts the shared `StrikeLanded` 86 toast, `EndConditionOverridesSO` (+ window), `MiniGameHUD` (the Bends' provider: the nearest rival), `author_preview_spawns.py` |
