# The Brittle Star's Space game: the plan (**Shatterplane**)

**Built 2026-11-10 as `GameModes.Shatterplane = 90`** on `claude/fervent-edison-ol600q`: see
`Assets/_Scripts/Controller/Arcade/SHATTERPLANE.md` for what shipped. This page is the plan and the decisions that led there. This belongs to
`Docs/MassAndSpaceGames/PLAN.md`, which is on `claude/serene-euler-g7crrw` and not yet on bleeding-edge.

## 0. What was checked before designing

- **The lane is open.** `/swimlanes` on 2026-11-10 lists the Brittle Star as open, not occupied. The last work there
  was Garrett's icon recut (#1735, merged). `who_works_where.py` on `R_VesselActions` and `_SO_Assets/Games` shows no
  unmerged branch touching Brittle Star files. Only the plan branch above mentions it.
- **The seed still matches what ships** (`R_VesselActions/BRITTLE_STAR.md` §3.4 and §3.5):
  - **Space Mode (X).** Five guns fire 90 rounds/s each at a full RT pull. Each round flies 135 u/s for 4.5 s
    (387 u range, ×2 at Space 10) and is stopped by the first prism it meets. The guns overheat after about 6 s at a
    full pull (about 20 s at Space 10), then lock out for about 5.4 s.
  - **The omni crystal (Ring Charge).** Each crystal adds one spare ring, up to two. LT throws a spare forward at
    240 u/s; it blows when it touches mass or a hull, or after 1.1 s. The blast is a **flat disk** in the ring's plane
    at that moment, perpendicular to the flight line. The disk's radius is the cell's nucleus radius (about 392 u in a
    standard cell), but it is only **±0.88 u thick** (±3.5 u at Charge 10). It destroys every prism it sweeps, of any
    domain; super-shielded prisms refuse. It also strips petals from rival hulls.
- **Genre.** `ModeGenre.TryElementForMetric` maps `PrismsDestroyed`, `VolumeDestroyed` and `LifeformsKilled` to Space
  (`Assets/_Scripts/Data/ModeGenre.cs`). Scoring on `PrismsDestroyed` needs no mode row. A `ModeGenreTests` row still
  pins it.
- **The ids.** The highest `GameModes` id is `Corona = 87`. No in-flight branch claims 88 or above yet. The Thresher's
  Space session will be claiming ids at the same time, so the id is taken at commit, not now.
- **The studio already has a lesson.** In the Vessel Studio's Demolition Derby scorecard (`Docs/Studios/BRITTLE_STAR.md`
  §5.1), "Hard, no ring" equals "Hard" on every rung. There, the ring never had a reason to fire. **This game exists to
  give it one.**

## 1. The number that shapes the design

The ring's disk is about 780 u across but under 2 u thick. It is a knife, not a bomb.

| Where the ring blows | What it takes |
|---|---|
| Square to a plate of mass (the disk lies in the plate) | The whole plate, out to the nucleus radius |
| Tilted θ from square, Charge 0 | A band about 4.8 / sin θ u wide across the plate: about 275 u at 1°, 55 u at 5°, 14 u at 20° |
| Inside a 3D clump | Only the single thin slice the disk passes through |

Meanwhile the five guns can fire up to about 2,700 rounds per overheat cycle, and every round that lands breaks one
prism. **Against a dense clump the guns win by count.** The ring only wins when the mass is **flat and the pilot is
square to it.** That is the game's verb.

## 2. Design brief (`Docs/claude/DESIGN_BRIEF.md`)

```
## Design brief: Shatterplane (the Brittle Star's Space game), 2026-11-10, claude/fervent-edison-ol600q

The fundamental it serves      mass destroyed is the score (Space genre); the hull's own destruction, guns
                               then ring, turned into a race, as Rampage turns the Dolphin's blast into one
The principles it obeys        P-modes-08 (the kit is never changed), P-modes-13/16 (the Cell owns the arena),
                               P-modes-02 (the target is set for about 90 s), P-modes-18 (no hull-specific prices)
The one it comes closest to    P-modes-08: the arena chooses the cell's nucleus size, and the ring's disk radius is
                               read from that nucleus. That is the environment, not a kit edit, but it moves how far
                               the ring reaches, so it is asked of Shombith first (decision D2)
What it changes for a player   wide flat plates of mass hang in the cell; line up square and the thrown ring erases a
                               whole plate in one star-shaped blast, while the guns chew what the ring missed
The cheapest prototype         the Brittle Star's Vessel Studio page (it already flies the Unity kit, 35 numbers
                               mapped with 0 drift, and draws the same blast); a third minigame beside the Derby,
                               to measure the ring's share of the score before any Unity work
Who judges it, and where       Shombith (asked for the plan) and Garrett (the hull's designer), first in the
                               studio on PC and phone, then in the editor
The retire condition           the ring still scores under a third of an experienced player's prisms, or squaring
                               up proves too fiddly to read on a phone (the same verdict as the Derby's)
```

**One sentence for the player:** *Break the drifting plates; fly a crystal to load a ring, square up to a plate, and
the ring erases it whole. The first domain to break the target wins.*

## 3. The game

- **What you break.** Wide, thin **plates** of neutral Blue prisms: one prism thick, built as a lattice (about 4 u
  prisms on an 8 u grid). They read clearly as walls, and they are cheap. The mass is Blue, so it is hostile to every
  domain and the field is contested, as in Salvo.
- **The loop.** These three steps are Rampage's charge, crystal and blast:
  1. The guns are the steady income: they chip any plate, from any angle.
  2. A crystal loads a ring.
  3. A square throw is the spike.

  Crystals are neutral omni crystals placed **on a plate's axis, about 200 u in front of it**. Taking one with a long,
  straight approach leaves you square to the plate, ready to throw.
- **What makes you break more.** Squareness, which the hole in the plate shows at once. The hull's own elements help:
  Charge thickens the disk (more forgiving), and Space extends gun range and heat. Charge's cords also slice prisms,
  and those count.
- **How it is won.** The first domain to destroy N hostile prisms wins: golf-timed, `PrismsDestroyed`, with Rampage's
  rule family (`RampageScoringRuleSO` as the base, as Salvo, Sirocco and Wrecking Ball use it).
- **Mass stays out of the score.** Creation Mode still lays prisms; they never score. The ring destroys allied mass,
  and that never scores either.

### The ladder (one question per rung)

| Rung | Name | The question | Content (first cut, to be modelled) |
|---|---|---|---|
| I1 | Billboards | Can you square up at all? | About 6 big plates (radius ~200 u) facing the spawn ring; crystals 2× players, **every one on a plate's axis** (two plates carry two) |
| I2 | Gallery | Can you swing round to square up? | About 8 plates at mixed orientations, so most need a turn to face square; crystals = players |
| I3 | Shelves | Front plate first | Stacks of 2-3 parallel plates **60-80 u apart** (at 40 u a stack reads as one blob from most angles). Rounds stop at the first prism, so the guns eat the front plate; a square ring takes one plate at a time. Peel the stack. Crystals = players |
| I4 | Reliquary | Find the square line through the frame | Plates braced by super-shielded frames (Flatline's ribs: neither guns nor ring break them, and they never score), **a rib every 6th lattice line** (every 4th hid most of the plate), danger rims, some plates half behind frames; crystals = players − 1 |

**On every rung, a clear zone of at least 250 u round each spawn**: no plate, frame or rim inside it. A randomly placed
I2 plate landed almost on a spawn in the first render.

## 4. What gets built (the `/arcadegame` recipe, once the design is agreed)

| Piece | Plan |
|---|---|
| Id | The highest free id at commit, after checking bleeding-edge and in-flight branches (the Thresher session is racing for one); `check_switch_label_collisions.py`; bump `EnumIntegrityTests` |
| Rule + monitor | `ShatterplaneScoringRuleSO : RampageScoringRuleSO`, `PrismsDestroyed`. The monitor copies `SiroccoPrismTurnMonitor` |
| Target | `EndConditionOverridesSO.shatterplanePrismTargetByIntensity` (Cleave's per-intensity pattern), with every row in `/arcadegame` §1. The value comes from the model (§6), never typed in |
| Arena | Fork a cell (`Cell Configs/Shatterplane Cell/`). The reason the doc can state: no shipped cell holds flat plates. Standard 1,200 membrane. Four `SpawnableShatterplane1-4` prefabs from a pure `ShatterplanePlates.cs` geometry (Flatline's pattern) |
| Ecology | Thin by default (P-modes-12): sparse light fauna at I1-I2, none at I3-I4, so creatures do not hide behind plates. Decision D3 |
| Controller | `ShatterplaneController : MultiplayerDomainGamesController`, cloned from Salvo / Sirocco, with `DomainRaceToasts` ticked |
| AI | The executor's own autopilot does all the pressing: it fires when 2+ rival prisms lie ahead, picks the mode, and throws when 18+ lie in its sphere ahead (Blue counts, `CountRivalPrisms` tests `Domain != own`). The mode adds only `SetExternalTargetProvider`: with no spare ring, the nearest crystal; with a spare, a point on the nearest standing plate's normal about 250 u out, so the approach is square before the autopilot's throw. **The limit:** the AI cannot time a throw to squareness, because the executor decides when to throw; the approach is the only lever. Plan step **`space-arcade`**. No compute job (Compute is paused) |
| Text and sound | Card text and toasts are authored fields in `GameToastConfig_Shatterplane`; there are no new sounds. Any slot added ships as an empty `EventReference` |
| Card art | `/cardart` (I2 arena, rendered offline) |
| Tests | A `ModeGenreTests` row (Space); edit-mode tests for the rule and the plate geometry (counts per rung, plates square to their crystal axis, the I4 frames super-shielded) |
| Generator | `Tools/Build/author_shatterplane_assets.py` on `arcade_mode_lib`, with `--check`, watched to fail once |
| Budget | `Tools/Build/shatterplane_budget.py`: prisms per rung under Flatline's / Rampage I4's envelope (about 12k), plus the worst case: one square ring kills a whole plate (about 2k prisms) in its 0.7 s sweep. Profile that kill burst in the editor |

## 5. Decisions (all decided 2026-11-10)

- **D1, the verb. DECIDED 2026-11-10 (Shombith): plates.** "Square up and the ring erases the plate" is the Brittle
  Star's Space game. The alternative was a pure gun race on dense wreckage (Salvo's game on another hull). It was
  rendered and dropped: on a solid clump the ring takes one thin slice, about 11%, whatever the angle (§7).
- **D2, the disk's reach. DECIDED (Shombith): "you can make it a little bigger".** The arena's nucleus is 470 u (a
  standard cell's is about 392 u), sized by the controller on every peer; the ring reads its reach from it.
- **D3, ecology. DECIDED: none at all; then revised the same day (Shombith): I4 carries live elemental fauna.** I3's
  crystals became Space, one per team present (`SHATTERPLANE.md` §3).
- **D4, the name. DECIDED: Shatterplane.**
- **D5, prototype first? DECIDED: straight to Unity.** The model in `Tools/Build/shatterplane_budget.py` set the targets.

## 6. Next steps (done 2026-11-10 except the editor run)

1. Model the yields per rung in `shatterplane_budget.py`: the ring's yield against tilt, the census, and the targets
   (about 30% of each rung's breakable mass, an estimate until a timed match). Done; the gun yield waits on the editor.
2. A studio minigame: skipped (D5).
3. Build to §4, run the `/arcadegame` §4 gates, `unity_refcompile` and the `/ship` gates.
4. Write the mode doc and the `PLAN.md` §7 row, and add the mode to `GAME_MODES_AND_CONTROLLERS.md`.

## 7. What the concept renders showed (2026-11-10)

These are offline renders of the planned geometry (Three.js, lit boxes), not the game. They are not committed. The
geometry used the plan's numbers: 4 u prisms on an 8 u lattice, a ±0.88 u disk at Charge 0, and a nucleus radius of
392 u.

| | Prisms | Ring, square | Ring, off square |
|---|---|---|---|
| Plates (this plan) | I1 11,766 · I2 8,808 · I3 9,948 · I4 6,606 + 4,245 frame | one plate: 1,961 of 1,961 (100%) | 443 (23%) at 5°, 149 (8%) at 20° |
| Wreckage clumps (the alternative) | I1 8,468 · I2 6,330 · I3 6,880 · I4 5,192 + 3,682 cage | one clump: 151 of 1,408 (11%) | 100 (7%) at 30°, 153 (11%) at 90° |

The fixes in §3 came from these renders: the spawn clear zone, the wider I3 shelves, the sparser I4 ribs and the I1
crystal placement.

**What changed in the build** (measured by the twin, `shatterplane_budget.py --ring`): one degree off square on a 19-tile
plate already lost 18% of it, and the big I1 plates did not all fit, so the shipped plates are smaller and more numerous
(radius 12-13 tiles, 10 / 12 / 5 stacks of 3 / 8). Within 1 degree of square a ring now takes the whole plate, at 2
degrees about 70%, at 5 degrees about a third. The shelves are 70 u apart and the spawn ring is 780 u with a 250 u clear
zone.
