# Shatterplane - the Brittle Star's Space game

`GameModes.Shatterplane = 90` (it took 88 first; Harvest, the Thresher's Space game, landed with it). Brittle Star only. Built 2026-11-10 on `claude/fervent-edison-ol600q`, from the plan in
[`Docs/MassAndSpaceGames/BRITTLE_STAR_SPACE.md`](../../../../Docs/MassAndSpaceGames/BRITTLE_STAR_SPACE.md), which holds
the design brief, the decisions and the concept renders.

**Status: authored headless, NOT yet run in the Unity editor.** It was checked by the reference compile, the generator's
`--check`, the twin-versus-C# census, the clip audit and the card render. Nobody has played it yet.

> *Break the drifting plates. Fly a crystal to load a ring, square up to a plate, and the ring erases it whole. The first
> domain to break the target wins.*

## 1. Why plates

This is the Brittle Star's **Space** card. The score is mass taken off the board (`ScoringMetric.PrismsDestroyed`,
Rampage's rule), so `ModeGenre` reads it as Space through its metric, with no row of its own.

The ring explosion (`R_VesselActions/BRITTLE_STAR.md` §3.5) is a flat disk lying in the plane of the thrown ring:

- **Width:** as wide as the nucleus of the cell it blows in.
- **Thickness:** under 2 u at Charge 0.
- **What it takes:** a prism is taken when its own half-extent along the disk's normal overlaps the disk
  (`BrittleStarRules.DiskReaches`).

So the ring is a knife, not a bomb.

| Where it blows | What it takes |
|---|---|
| Square to a plate (the disk lies in the plate) | The whole plate |
| A few degrees off square | One band across the plate |
| Inside a 3D clump | One thin slice, about 11% of the clump at any angle (the dropped alternative, `BRITTLE_STAR_SPACE.md` §7) |

The guns break one prism per round, from any angle, at up to 450 rounds/s. Against clumps they always out-score the ring.
Plates are what make the ring the spike of the match, as Rampage's crystal is the Dolphin's. **Squaring up is the
skill.** The Vessel Studio's Demolition Derby scorecard had found that "Hard, no ring" played exactly like "Hard": the
ring never had a reason to fire. Here it does.

Measured on the shipped plates (`shatterplane_budget.py --ring`):

| Rung | Plate | Square | 1° | 2° | 5° | 10° | 20° | Charge 10 at 5° |
|---|---|---|---|---|---|---|---|---|
| I1 | r 13 tiles | 529 | 529 | 365 | 177 | 77 | 27 | 323 |
| I2, I3 | r 12 | 441 | 441 | 331 | 163 | 71 | 25 | 293 |
| I4 (ribs excluded) | r 13 | 404 | 404 | 262 | 134 | 46 | 2 | 226 |

What the table shows:

- **Within 1° of square, the ring takes everything.** A tile's 1 u half-thickness plus the disk's 0.88 u covers the whole
  plate.
- **At 2° a square ring still takes about 70% of the plate. At 5° it takes about a third.**
- **Charge thickens the disk**, through the hull's own `blastScale` (×1 → ×4 at Charge 10), so a Charge pilot is
  forgiven more.

## 2. The kit stays exactly as it is

The mode never changes the vessel (LOCKED, Shombith 2026-10-31; principle P-modes-08). Nothing under
`R_VesselActions/**`, the prefab or the action SOs was edited. The mode has three levers:

- **The arena.** The plates, and the cell's **nucleus**. The ring reads its radius from the nucleus
  (`BrittleStarExecutor.NucleusRadiusAt`), so the controller sizes this cell's nucleus to `nucleusRadius` = **470 u**
  on every peer (`Cell.SetNucleusWorldRadius`). That is decision D2, Shombith 2026-11-10: "you can make it a little
  bigger" than a standard cell's ~392 u. The nucleus doubles as the in-world sign of how far a ring reaches.
  `NucleusIsControlZone` is cleared, because the nucleus is play geometry, not a claim (Wrecking Ball's rule).
- **The crystal field.**
- **The AI.**

**Mass stays out of the score.** Creation Mode still lays prisms, but they are the pilot's own colour and never score.
The ring destroys allied mass too, and that never scores either. So the game cannot quietly become a Mass game.

## 3. The arena (`SpawnableShatterplane`, geometry in `ShatterplanePlates`)

**What a plate is.**

- Each plate is a round disc of neutral (`Domains.Blue`) prisms, one tile thick.
- The tiles form a sparse lattice: 5 × 5 × 2 u tiles on an 8 u grid (`Fill` 0.625). A stray round can pass a gap, while
  the disk still takes every tile in its plane.
- A plate's axis always points away from the arena centre: the side a pilot comes in from.

**How the field is built.**

- Each structure is a lone plate, or (at I3) a stack of plates on one axis.
- Structures are placed by hashed draws (Flatline's method), each clear of every earlier one by a 40 u gap.
- Everything stays inside a **250 u clear zone** round the 780 u spawn ring.

**The crystals.**

- The sites float on each plate's axis, 200 u in front of its front plate.
- If a rung has more crystals than plates, the extra ones go in a second round, 120 u farther out on the same axes.
- Taking a crystal leaves the pilot lined up on the plate behind it.

| Rung | Question | Plates | Prisms | Breakable | Crystals | Target |
|---|---|---|---|---|---|---|
| I1 Billboards | Can you square up at all? | 10, radius 13 tiles, facing straight out | 5,290 | 5,290 | 10 | 1,600 |
| I2 Gallery | Can you swing round to square up? | 12, radius 12, mixed angles | 5,292 | 5,292 | 8 | 1,600 |
| I3 Shelves | Front plate first | 5 stacks of 3, radius 12, 70 u apart | 6,615 | 6,615 | 5 | 2,000 |
| I4 Reliquary | Find the square line through the frame | 8, radius 13; a rib on every 6th line, danger rims; every other plate half behind a grille | 4,588 | 3,232 | 4 | 1,000 |

Notes on the ladder:

- **I3.** The stack spacing is 70 u because at 40 u the concept render read as one blob. A round stops at the first
  prism, so the guns eat the front plate from the face, and a square ring takes one plate at a time.
- **I4.** A **screen** is a super-shielded grille 60 u in front of the plate, shifted half a plate sideways. Nothing
  breaks it, and a ring blows on it: thrown through the screen, the disk lies 60 u in front of the plate and takes
  nothing. The square line runs beside the screen. The ribs and screens are super-shielded and never score.

**How the target was set.** The target is about 30% of a rung's breakable mass. Three domains share one field, so the
first to a third of it wins before the plates run out. This is an **ESTIMATE pending a timed match** (P-modes-02: an
experienced player should finish in about 90 s). The four values live in `EndConditionOverridesSO`
(`shatterplanePrismTargetByIntensity`; FrogletTools > Game Modes > End Game Conditions).

**Cost.**

- 4,588–6,615 prisms per rung, under Flatline's 12,000 envelope.
- Every rung is baked (`bake_prism_layouts.py`, 0.2–0.4 ms to load).
- The clip audit reports 0 clipping on all 16 runs.
- **The burst to profile:** a square ring kills a whole plate (up to 529 prisms) in its 0.7 s sweep, in ten queries.
  That is well under a Rampage blast, but it has not been profiled.

**I3's crystals are Space, one per team** (Shombith, 2026-11-10: "for intensities 3 make elemental crystals of space
just for teams present there"):
- Every crystal site holds one Space crystal for each team present in the match, standing 30 u apart across the plate's
  axis, each tinted to its team.
- A crystal can be skimmed up only by its own team (`ElementalCrystalImpactor.RestrictTo`, the opt-in gate Harvest's
  I3 added the same day for the same request; Harvest and Shatterplane are its only two callers).
- So reaching a plate first never denies another team its ring, and Space lengthens the guns' range and heat capacity.
- The AI forages only crystals its own team may take.
- These are `fieldElementByIntensity` / `fieldPerTeamByIntensity` on `ShatterplaneSettings`, per intensity, authored
  by the generator.
- The other rungs keep one crystal per site with a random element, anyone's.

**I4 carries live elemental fauna**, and I1-I3 carry none:
- I4's cell has its own spawn profile (`Shatterplane Spawn Profile 4`). It carries the Blob tadpole and shark, the
  species Rampage uses, referenced read-only. Each spreads its creatures over the four elements (`SpreadElements` and
  a four-element palette), so every creature rolls its own element.
- The amount is the profile's `FaunaPopulationScale` 2, which leaves the shared species assets untouched.
- With no flora, the plates are the food: herbivores graze the neutral plates (opposing environment volume), and
  sharks hunt the herbivores. Nothing scripts it.
- Creatures spawn in the controlling team's colour. Their body prisms are hostile to the other teams, so breaking them
  scores for those teams; this is Rampage's rule, not a new one.
- A dying creature drops its heart as an elemental crystal anyone can take, and every crystal is a spare ring.
- **Cost:** a handful of fauna. Tadpole and shark at 2x their authored populations, a tadpole capped at 12, add tens
  of colliders against I4's 4,588 prisms.

## 4. The AI (`ShatterplaneSquarer`, server-only)

**It steers; it never presses.** The Brittle Star's own autopilot (`BrittleStarExecutor.Autopilot`) presses every
replicated control a human presses. It already:

- holds RT while 2 or more rival prisms lie on the line ahead (neutral plates count, because their domain is not its
  own);
- picks Space Mode against a wall further out, and Charge Mode against a near one;
- throws its ring when 18 or more prisms fill the sphere ahead of it.

So the mode only answers where to fly, through `SetExternalTargetProvider`:

- **With no spare ring and a crystal within `aiCrystalDetourRange`:** to that crystal. Every crystal is on a plate's
  axis, so the pickup is also the line-up.
- **Otherwise:** to a standing plate, square. First to a point `aiApproachDistance` out on its axis, then, once within
  `aiSquareDegrees` of the axis, straight in.

The difficulty ladders (Easy, Medium, Hard, Legendary) key on the seat's AI level, never the intensity. The generator
fails if the squarer presses anything or reads the intensity.

**What the AI cannot do.**

- **It cannot time the throw.** The executor throws when its own count says so. Its sphere reaches about 390 u ahead,
  but a ring at cruise flies 285 u/s for its 1.1 s fuse, about 313 u. A throw from further out fuses short: its disk
  then lies in front of the plate and takes little. The approach distance is the only lever the mode has. **The editor
  run must measure what share of AI throws land square.**
- It never uses Speed Mode to close the distance (the executor's boost policy decides).
- It cannot read I4's screens: it may throw into one.

The AI plan step is **`space-arcade`** (`Docs/AI_SYSTEM/ai_plan.json`: each vessel's universal Space AI, trained on its
Space arcade game). Froglet Compute is paused, so no job was queued.

## 5. What is reused, what is the mode's

**Reused:**

| What | From |
|---|---|
| Rule | Rampage's (`ShatterplaneScoringRuleSO : RampageScoringRuleSO`), with every word an authored field |
| Monitor | Sirocco's, with Flatline's per-intensity target |
| Crystal field | Flatline's per-peer field |
| Scene | Cloned from Flatline's, whose AI templates are already four Brittle Stars |
| Objective arrow | Sirocco's and Cleave's (`HostileMassObjectiveProvider`) |
| Race toasts | The shared 128-130 |

**The mode's own:**

- the plates (`ShatterplanePlates`, `SpawnableShatterplane`, twinned in `Tools/Build/shatterplane_budget.py`);
- the nucleus size;
- the squarer AI;
- the hint toast (`ShatterplaneHint` = 165);
- the prism-milestone toast (83, every 200).

**Sounds:** the mode adds no sound slot. Everything a player hears is the vessel's and the platform's.

## 6. Files

| Role | Path |
|---|---|
| Controller | `Arcade/Shatterplane/ShatterplaneController.cs` |
| Settings | `Arcade/Shatterplane/ShatterplaneSettingsSO.cs`, `_SO_Assets/Games/ShatterplaneSettings.asset` |
| Rule | `Arcade/Shatterplane/ShatterplaneScoringRuleSO.cs`, `_SO_Assets/Scoring Rules/ShatterplaneScoringRule.asset` |
| AI | `Arcade/Shatterplane/ShatterplaneSquarer.cs` |
| Monitor | `Arcade/TurnMonitors/ShatterplanePrismTurnMonitor.cs` |
| Arena | `Environment/MiniGameObjects/ShatterplanePlates.cs`, `SpawnableShatterplane.cs`, `_Prefabs/Spawnables/SpawnableShatterplane1-4.prefab`, `_SO_Assets/Cell Configs/Shatterplane Cell/` |
| Card, toasts, preview | `_SO_Assets/Games/ArcadeGameShatterplane.asset`, `Game Toasts/GameToastConfig_Shatterplane.asset`, `Mode Previews/ModePreview_Shatterplane.asset` |
| Scene | `_Scenes/Multiplayer Scenes/MinigameShatterplane.unity` |
| Card art | `_Graphics/ARCADE/CardBackgrounds/Shatterplane.png` (`/cardart`, I2) |
| Generator, twin | `Tools/Build/author_shatterplane_assets.py` (`--check`), `Tools/Build/shatterplane_budget.py` (`--ring`, `--sites`) |
| Tests | `Tests/Editor/ShatterplaneTests.cs`, a row in `ModeGenreTests` and `EnumIntegrityTests` |

## 7. Verification

**Run headless:**

- `author_shatterplane_assets.py --check`, and watched fail once on both a drift and a validation.
- `unity_refcompile`.
- The bake.
- The clip audit (0).
- The card render, whose compiled C# laid exactly the twin's counts.
- The preview and arcade gates.

**Waiting on the editor** (`Docs/UNITY_VERIFICATION_CHECKLIST.md`):

- the EditMode tests;
- a match at each intensity: the nucleus at 470 u, a square ring erasing a plate, the target reachable in about 90 s;
- the AI's share of square throws;
- the screens at I4;
- the kill-burst profile.

## 8. Changes

| Date | Change |
|---|---|
| 2026-11-10 | I3: Space crystals, one per team present, each its team's alone (`ElementalCrystalImpactor.RestrictTo`, shared with Harvest). I4: live elemental fauna (Blob tadpole and shark, each creature rolling its element) on its own spawn profile at 2x (Shombith). |
| 2026-11-10 | Built (Shombith's session): D1 plates, D2 nucleus 470 u, D3 no wildlife, D4 the name, D5 straight to Unity. |
