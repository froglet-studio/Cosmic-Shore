# Hood (`GameModes.SerpentHood = 77`; was Serpent Gulag)

The Serpent's **CHARGE** game: a **sniper duel across a crystal heart**. Rebuilt in place from Serpent Gulag on
2026-11-09 by Shombith, after choosing it from three drawn ideas (Basilisk, Hood, Sidewinder) and two rounds of cover
shapes:

> "Lets do serpent gulag and change its names to a better one, I like the shapes that we have around the rings in its
> time game those can be covers not the weird bases of prisms. Remove the gulag idea entirely."
> (the cobra-faced first round of hoods) "nah too mythical" - (Serpentine's shapes bent into a hood) "go ahead".

Pilots spawn far apart on a 2,200 u sphere in a 3,600 u cell. A heart of 24 elemental crystals floats in the middle.
Round it, on a sphere, stand the **perches**: Serpentine's ring shapes (`SerpentineGate`, the shapes at Serpent Time's
rings) bent into **hoods**, every mouth turned to the heart. A sniper sits in a hood's **cup** and fires out through its
exits. A **snipe** (a Sniper Shot that strips a rival pilot) is one point; the first **domain** to the target wins,
golf-timed. On the Serpent, **Charge is the rifle's recovery** (12 s at rest, 5.4 s at Charge 10), so the heart is where
a sniper reloads faster: in the open, in every hood's sights.

The nests, barred cell blocks and rubble of Serpent Gulag are gone, with `SpawnableSerpentGulag`, its three prefabs and
their bakes. The id, the scene, the card, the settings and every guid are the same assets renamed (the generator keeps
each guid's Serpent Gulag seed), so no roster, preview, build-list or scene reference broke.

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameSerpentHood.unity` (was `MinigameSerpentGulag`, first cloned from `MinigameDustup.unity`) |
| Controller | `SerpentHoodController : MultiplayerDomainGamesController` (lays the hoods, runs the core rule) |
| Arena geometry | `SerpentHoodLayout` (pure, closed form); Python mirror `Tools/Build/serpent_hood_budget.py` |
| AI | `SerpentHoodMarksman`, a server driver (below) |
| Metric | `ScoringMetric.CombatPoints` (8), reused. A snipe is a `Debuff`-class combat hit |
| Rule | `SerpentHoodScoringRuleSO`: `snipePoints` **1**, every other hit class **0**; every result line an authored template |
| Turn monitor | `SerpentHoodSnipeTurnMonitor : CombatPointTurnMonitorBase` |
| Target | `EndConditionOverridesSO.serpentHoodSnipeTarget` (**3**; `[FormerlySerializedAs("serpentGulagSnipeTarget")]`) |
| Comeback | rate **1.5**: a quarter behind (0.75 snipes) buys 1.125 element levels |
| Arena | four `Serpent Hood Cell Config`s (IntensityWise), Cleave's x3 membrane (radius 3,600), no environment prefab |
| Objective arrow | `BendsObjectiveProvider`: the nearest rival pilot |
| Toasts | 82 (a snipe), 128-130 (shared race beats via `DomainRaceToasts`), 157 `SerpentHoodScopeHint`, 30 (comeback) |
| Generator | `Tools/Build/author_serpent_hood_assets.py` (`--check`) |
| Tests | `SerpentHoodLayoutTests` (9), also run outside Unity by `Tools/Build/serpent_hood_harness/run.sh` |

## The loop

1. **Find a perch.** Spawns are on a tetrahedron 2,200 u out (3,593 u apart, past the rifle's 3,000 u reach), outside
   the sphere of hoods. Fly round a hood's rim into its cup; its mouth (the danger weave) faces the heart.
2. **Scope** (hold LT) and put the reticle on a rival; **fire** (RT while scoped). A rival inside the round's 1.5 deg
   cone is stripped, and that is one snipe. The round pierces everything (`pierceCount` 0), so a hood hides you; it
   does not stop a round.
3. **Flush them out.** Every hood has a plain **core** at the centre of its mouth. Break a core block (a round, a ram)
   and the whole hood falls apart, core outward, 48 prisms a frame. It grows back 25 s later, once no hull is inside its
   reach (`SerpentHoodSettings.hood*`). Serpent Time's shape rule, at the perches.
4. **Reload in the open.** Charge crystals shorten the next recovery. The heart is mixed elements (Space = the cloak
   wall's reach, Mass = walls, Time = pellets), so every pass through it buys something.
5. **Cloak** (RT unscoped) to vanish after a shot, as the shipped kit does everywhere.

## The intensity ladder (`SerpentHoodLayout.RungFor`)

| | Name | Hoods (the Serpentine shape) | Perch sphere | Shutters | Hood flare / cup | Prisms |
|---|---|---|---|---|---|---|
| I1 | Wide Hood | 8 Coil (7 x 6 u core) | 1,500 u (shots across ~3,000) | - | 1.35x / 0.18 | 1,908 |
| I2 | Spread Hood | 12 Coil (3 x 5 u core) | 1,350 u | - | 1.45x / 0.24 | 2,862 |
| I3 | Fanged Hood | 18 Maw (1 x 5 u core) | 1,250 u | 6 at 700 u | 1.50x / 0.30 | 5,562 |
| I4 | Closed Hood | 20 Iris (1 x 3.5 u core) | 1,120 u | 6 at 660 u | 1.60x / 0.36 | 7,340 |

Harder up the ladder in three ways a pilot feels: the shots get shorter (more hoods closer in), the cups deeper and the
cores smaller (harder to flush a rival out), and at I3-I4 six **shutters** (the same hood, turned across) stand near
the heart and break the straight lines through it.

**I3 and I4 moved from the approved drawing** (1,200 / 520 and 1,050 / 560): there, a warped shutter reached into the
crystal heart and an I4 shutter sat on a perch. `SerpentHoodLayoutTests.No_two_covers_touch...` caught it. The new radii
are the nearest that keep every hood 60 u clear of the heart, 300 u inside the spawn sphere and 20 u from every other
hood.

## A hood (`SerpentHoodLayout.BuildLocal`, `Build`)

`SerpentineGate.Build` at a **72 u ring** (Serpentine's I1 port size at every rung, so the exits stay 26 u), unchanged:
the core first, then the danger weave across the mouth with its 2-3 exits, then the plain shell flaring back. Then
`Warp` bends it into a hood, in the shape's own frame:

- across (x) stretched by the rung's **wide** factor where the hood is widest, falling to **narrow** at the tapered end
  (y below 0, which is also drawn 1.25x longer);
- the sides and the crown curled back toward the cup (`z -= cup * x^2 / reach + 0.1 * max(0, y/reach)^2 * reach`).

Each prism's own axes follow the warp's derivative (a central difference), so the plates and bars bend with the shape.
Prism sizes are never changed. Closed form, zero random draws: every peer builds the same hoods from the intensity.

**The weave still closes.** Serpentine holds its flat weave to a 6.5 u hole away from the exits. Stretched 1.6x across,
the largest hole is held under **9.5 u** (`The_warped_weave_leaves_no_hull_sized_hole_but_the_exits`), and every exit
keeps at least 16 u clear (`Every_exit_stays_open_through_the_warp`). Whether 9.5 u admits a Serpent where it should
not is a playtest question.

## Hood owns the shape code

`SerpentineGate.cs` lives in `SerpentHood/` (moved from `SerpentTime/` on 2026-11-09, same class and guid, Shombith's
call). Serpentine still stands one at every ring. The move is there because the unmerged `claude/dazzling-ride-dpyvkx`
("no shapes - rings far apart on a switchback ladder of hairpins", 2026-10-31) deletes the file at its old path: had it
merged after Hood, git would have deleted it without a conflict and Hood would have stopped compiling. Now that merge
raises a rename/delete conflict; resolve it by keeping the file here, and take that branch's Serpent Time changes
around it.

## Why the controller lays the hoods (not a cell environment)

Serpent Gulag's nests were a `CellEnvironmentSpawnableBase`, baked. A baked environment's prisms are anonymous once
laid, and the core rule must know which prisms belong to which hood. So the hoods are laid the way Serpentine's ring
shapes are: by the controller, on every peer, through `PrismTrailBuilder.LayOne` with an owner id (`"SerpentHood"`),
behind the connecting panel (`watchForReveal`), and regrown with `CompletesAsLiveRibbon`. The cell configs carry no
environment; their PhaseThresholds still ride the hoods' census (Cleave's deltas) so the cell starts in the right phase.

A stopped Serpent's Seed Wall steals loose prisms near its seed, hood plates included (they are plain, unshielded,
neutral). A sniper dug in a cup eats its own cover, and a wall that takes a core block brings the hood down. That is
the shipped wall meeting the shipped prisms; watch it in the playtest.

## The crystal heart

24 crystals in a ball of radius 380 (`SerpentHoodLayout.FieldSlot`: Fibonacci directions, cube-root radii from the
golden sequence), so the slots are closed form and the drawings, the Python mirror and the game agree. The elements
are drawn from `fieldSeed` on every peer. Each collected crystal grows back in its own place after 8 s (Dog Fight's
recipe; collection is per peer, harmless while crystals score nothing). The omni crystal spawns inside it
(`noNucleusSpawnRadius` 380).

## AI: the marksman (`SerpentHoodMarksman`)

Server-only and input-only: it steers with `SetExternalTargetProvider` and presses LT / RT / B with
`PerformShipControllerActionsReplicated`, and releases every press on disarm. The cycler is kept off the cloak and the
stop toggle (`SetCycledAbilityFilter`).

- **Post**: a standing perch whose **cup** (45 u back from the mouth, `aiCupDepth`) is nearest its level's preferred
  range from the rival it hunts; otherwise straight back along the line it holds; already in the band (350 u), it stays.
  It never goes straight back to the perch it just left, and drops a post whose hood has fallen.
- **Round the rim**: coming at a perch from the heart side, it first steers to a point beside the hood, its reach plus
  80 u out (`aiRimClearance`), so it goes round the danger weave, not through it.
- **Dig in, shoot, relocate, retreat, forage**: unchanged from Serpent Gulag (the shipped stance; scope and fire when its
  aim point is inside the cone; relocate when its hold runs out; back off from anyone inside its keep-away; forage the
  heart while the rifle recovers).

**Difficulty is the seat's AI level, never the intensity** (`GameDataSO.AIDifficultyFor`). **Legendary plays
aggressively** (Shombith, 2026-10-31).

| Ladder (`SerpentHoodSettings.asset`) | Easy | Medium | Hard | Legendary |
|---|---|---|---|---|
| re-plan every (s) | 1.2 | 0.9 | 0.6 | 0.4 |
| aim error (deg, vs a 1.5 deg cone) | 3.0 | 2.0 | 1.2 | 0.5 |
| scope settle (s) | 1.0 | 0.6 | 0.3 | 0.15 |
| hunt range (u) | 1,500 | 2,000 | 2,500 | 3,000 |
| forage while recovery left >= (s) | never | 7 | 5 | 3.5 |
| preferred range (u) | 1,400 | 1,700 | 2,000 | **900** |
| keep-away (u) | 700 | 900 | 1,100 | **350** |
| hold a post (s) | 8 | 12 | 16 | **5** |
| cloak after a shot | 0 | 0.25 | 0.5 | 0.75 |

## The gates

- `python3 Tools/Build/author_serpent_hood_assets.py --check` - every asset, plus: the comeback, the rifle's class /
  range / cone, the C# default target, the ladders and their C# initializers, the hood settings, `RungFor`'s four rows
  against the mirror, every hood inside the spawn sphere and outside the heart, shots across inside the rifle's reach,
  the prism ladder rising and under 8,000, the tetrahedral spawns past the rifle, the core rule present, the marksman
  never reading the intensity, no file of the mode naming nests / rubble / cell blocks, and the scene's identity.
  Watched fail: a mutated settings asset is named; a mutated `RungFor` row fails validation.
- `python3 Tools/Build/serpent_hood_budget.py --self-test` - compiles the shipped C# and compares all 17,672 prisms
  (position, forward, up, size, kind, core) and the 24 heart slots to the mirror; a mirror with an I3 radius 10 u off
  must fail it (it does: 18 errors).
- `bash Tools/Build/serpent_hood_harness/run.sh --self-test` - runs the 9 shipped `SerpentHoodLayoutTests` outside Unity;
  I4's perches pulled to 600 u must fail them (they do).
- `python3 Tools/Build/render_card_backgrounds.py --check SerpentHood` - the card (MODEL tier, from the mirror).

## Platform changes (2026-11-09)

- `GameModes.SerpentGulag = 77` renamed `SerpentHood` (same value; Unity serializes the int). `EnumIntegrityTests`
  count unchanged.
- `EndConditionOverridesSO.serpentGulagSnipeTarget` (+ build twin, default, getter) renamed `serpentHood...`, with
  `[FormerlySerializedAs]`; the asset's keys migrated by the generator. End Game Conditions window rows renamed.
- `GameToastSituation.SerpentGulagScopeHint = 157` renamed `SerpentHoodScopeHint`.
- `MiniGameHUD`: SerpentHood -> `BendsObjectiveProvider`.
- `author_preview_spawns.SCENE_FOR_MODE[77]` -> `MinigameSerpentHood`; `EditorBuildSettings` path migrated.
- `prism_layout_bakes.json`: the three Serpent Gulag rows removed with their bakes.
- `render_card_backgrounds.py`: the SerpentHood MODEL recipe and accent; `SpawnableSerpentGulag.cs` out of `SOURCES`.
  **And the card harness compiles again**: Regatta's rails had begun naming `RegattaCourseFeatures`,
  `RegattaLifeformSiteSettings`, `ICellFaunaCorridorSource`, `PreparesPlantingSites` and `Trail.TrailList`, so the
  harness failed on bleeding-edge for every card (§3's "a harness is a second compile" trap); those three sources joined
  `SOURCES` and `PlatformShim.cs` gained the two members.
- `Tools/Analytics/server/mode_elements.json` regenerated (its `SerpentGulag` rows are `SerpentHood` now). Matches in
  the analytics history recorded as `SerpentGulag` keep that name.

## Verification status

**Authored headless; not yet flown in the editor.** Proved: the generator's `--check` and two negative controls; the
C#/Python parity (17,672 prisms) and its negative control; the 9 layout tests outside Unity and their negative control;
the card render `--check`; the standing gates (switch labels, enum references, console logging, conditional
compilation, self-referential locals, using directives, game lists, preview spawns, mode previews, cytoplasm shards,
analytics mode table); and a compile against real Unity references (`unity_refcompile`, player config: 0 errors in
project code). Not proved: how it plays, how the hoods look in the real renderer, the AI's rim route, the 9.5 u weave
holes. The hand test is in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## Known limitations

- **The AI sees cloaked pilots** (no public cloak state; Garrett's Serpent Charge branch carries `IsCloaked`).
- **The AI does not shoot cores.** It shoots pilots only; flushing a rival out of a hood is a human's play for now.
- **The rim route is one waypoint**, not a path; at I4 the AI may brush another hood on the way.
- **The arrow gives positions away** (nearest rival, as every `BendsObjectiveProvider` mode). A sniper duel may want it
  off at higher intensities - a playtest call.
- **Per-peer crystal heart and per-peer hood falls**: every peer sees the same shots break the same cores, but a hood a
  peer saw fall to a ram nobody else saw may differ for a while (Serpentine's shapes behave the same).
- **The preview shows the bare cell**: the hoods are the controller's, so Test Flight from the card shows no hoods.
