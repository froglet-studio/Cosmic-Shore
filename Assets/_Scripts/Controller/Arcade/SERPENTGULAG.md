# Serpent Gulag (`GameModes.SerpentGulag = 77`)

The Serpent's **CHARGE** game: a **sniper duel at range**. Asked for by Shombith on 2026-10-31:

> "Lets create the serpent's charge game. This is the spec we design for intensities for a sniper duel and a big
> enough cell for 4 players playing at a distance. I1: No environment in the first intensity players are spawned far
> apart with elemental crystal field in the middle, this field is common. I2, 3 and 4 can have environments like
> sniper nests etc to make a sniper duel fun. Name it as SerpentGulag or something like that."

Pilots spawn far apart in a 3,600 u cell. A field of elemental crystals floats in the middle at every intensity. A
**snipe** - a Sniper Shot that strips a rival pilot - is one point; the first **DOMAIN** to the target wins,
golf-timed. On the Serpent, **Charge is the rifle's recovery** (12 s at rest, 5.4 s at Charge 10), so the field is
where a sniper reloads faster: in the open, in every rival's sights. That is the whole risk / reward of the mode.

**It sits beside Garrett's Serpent Charge** (`GameModes.SerpentCharge = 67`, a sniper duel in Rampage's forest,
unmerged on `cece/vibrant-archimedes-n3xn3x` since 2026-10-16). This mode touches none of that branch's files and
claims a different id; its AI ladder borrows that branch's ladder SHAPE (four rungs of marksman skill, asserted monotone, the top rung's aim
error inside the cone) - keyed here on each seat's AI difficulty, not on the intensity as that branch does. If both land, the team picks which card the Serpent keeps.

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameSerpentGulag.unity` (cloned from `MinigameDustup.unity`) |
| Controller | `SerpentGulagController : MultiplayerDomainGamesController` |
| AI | `SerpentGulagMarksman`, a server driver (below) |
| Metric | `ScoringMetric.CombatPoints` (8), reused. A snipe is a `Debuff`-class combat hit |
| Rule | `SerpentGulagScoringRuleSO`: `snipePoints` **1**, every other hit class **0**; every result line an authored template |
| Turn monitor | `SerpentGulagSnipeTurnMonitor : CombatPointTurnMonitorBase` |
| Target | `EndConditionOverridesSO.serpentGulagSnipeTarget` (**3**; 6 until 2026-10-31, Shombith: "make the score to 3 shots not 6") |
| Comeback | rate **1.5**: a quarter behind (0.75 snipes) buys 1.125 element levels (0.8 at the first target of 6) |
| Arena | four `Serpent Gulag Cell Config`s (IntensityWise), Cleave's x3 membrane (radius 3,600) |
| Environment | `SpawnableSerpentGulag` (I2-I4), one prefab per rung, baked |
| Objective arrow | `BendsObjectiveProvider`: the nearest rival pilot |
| Toasts | 82 (a snipe), 128-130 (shared race beats via `DomainRaceToasts`), 157 (scope hint), 30 (comeback) |
| Generator | `Tools/Build/author_serpent_gulag_assets.py` (`--check`) |

## The loop

1. **Find a rival.** The arrow points at the nearest rival pilot. Spawns are on a 2,200 u ring, so adjacent pilots
   start ~3,100 u apart, just past the rifle's 3,000 u reach: nobody can snipe from the start line.
2. **Scope** (hold LT) and put the reticle on them; **fire** (RT while scoped). A rival inside the round's 1.5 deg cone
   is stripped (the shipped rule), and that is one snipe.
3. **Reload faster in the middle.** Charge crystals shorten the next recovery. The field is mixed elements (Space =
   the cloak's wall reach, Mass = walls, Time = pellets), so every pass through it buys something.
4. **Cloak** (RT unscoped) to vanish after a shot, as the shipped kit does everywhere.

## The intensity ladder

| | Name | Environment | Prisms |
|---|---|---|---|
| I1 | Open Ground | none - the far spawns and the field alone (the spec's I1) | 0 |
| I2 | Watchtowers | 8 sniper nests (ring 1,150, +-300) and 40 rubble clusters | 1,568 |
| I3 | Cell Blocks | 10 inner nests (1,150) + 6 outer (1,650), 12 barred screens (850), 80 rubble clusters, danger on every 8th frame plate and bar | 4,228 |
| I4 | The Gulag | 12 inner nests (1,150, +-400) + 10 outer (1,700, +-450), 16 screens (900), 140 rubble clusters, danger on every 4th frame plate and bar and every 5th rubble cube | 6,008 |

**Harder and more populated since 2026-10-31** (Shombith: "Make the intensity environment harder and more
populated"): the first ladder was 936 / 1,976 / 2,652 prisms. I1 still has no environment, as his first spec asks.

**Rubble** is a cluster of eight flush 9 u cubes, scattered evenly through the shell between the field and the spawn
ring (520-1,900 u), clear of every nest, screen and other cluster. It hides a pilot, and it is the loose mass a
stopped Serpent's wall grows from (the wall steals loose prisms near its seed, `SERPENT_SEED_WALL.md`): a sniper who
digs in beside rubble or behind a nest builds a thicker wall than one in the open.

**A nest** is a hollow pod (90 x 60 x 90 u) whose +z face is turned to the arena centre. That face has a 60 x 30 u
**firing slit**, the back has a 30 x 30 u **entry**. A Serpent parked inside (B stops it) sees the field through the
slit and is hidden from every other angle. **The round pierces everything and breaks super-shields**
(`SniperShotActionSO.pierceCount` 0), so a nest is concealment and a perch, not armour: every round through it
leaves a hole. **A cell block** is a screen of vertical bars between two rails (208 x 120 u): it breaks a long sight
line without closing it. Every prism is neutral (`Domains.Blue`, as the Boneyard's crust is), so every team's rifle
cuts them alike, and a Seed Wall can steal from them.

The layout reads only the prefab's serialized fields (never its intensity), so each rung is baked
(`bake_prism_layouts.py`), and it is flush: the clip audit measures **0 clipping pairs** on all three prefabs. The
generator's Python census of the geometry matches the C#'s run in the audit harness prism for prism and volume to the
unit, and PhaseThresholds ride that baseline (Cleave's deltas). No flora, no fauna, no nucleus. The omni crystal
spawns inside the field (`noNucleusSpawnRadius` 380).

## Reused, and why

- **The metric and the monitor are Dustup's shape.** The pilot strip was already a `Debuff`-class combat hit
  (`SniperShotAction.asset` `hitClass: 2`), so `CombatHitScoring.Credit` already counted it into `DebuffHitsLanded`
  in every mode. The generator asserts the class, the rifle's range (3,000) and cone (1.5 deg) are still what the
  spawn ring and the AI ladder were built for.
- **The scene** is Dustup's with the identity, the four cells, the four AI hulls (Tiger Moth -> Serpent), the spawn
  ring (700 -> 2,200) and the crystal volume (420 -> 380) swapped, adopted through `lib.committed_scene` once
  committed.
- **The crystal field is Dog Fight's recipe**: the elemental set's prefabs, provisioned with an
  `ElementalCrystalImpactor` and laid on every peer from `fieldSeed`. Each collected crystal grows back in its own
  place after 8 s. Collection is per-peer, as in Dog Fight; tolerable because a crystal scores nothing.
- **The membrane is Cleave's x3 shell** (radius 3,600) with the cytoplasm spacing scaled to 360 u, so the cell builds
  a standard cell's shard count (`check_cytoplasm_shards.py`).

## AI: the marksman (`SerpentGulagMarksman`)

`AIPilot` writes a stick and a throttle, and the rifle fires only while the scope is up, so without a driver every
AI Serpent would fly the match unable to score (the Tollway rule). The marksman is server-only and input-only:
it steers with `SetExternalTargetProvider` and presses LT / RT / B with `PerformShipControllerActionsReplicated`, the
press shape Serpent Time's AI uses, and releases every press on disarm. The cycler is kept off the cloak and the stop
toggle (`SetCycledAbilityFilter`): the marksman presses those itself.

**A sniper keeps its distance and digs in** (Shombith, 2026-10-31: "the AI starts coming close to the vessel the AI
should learn to keep distance as a sniper and try to build its defenses rather and not rush towards me"). Until then
it steered AT its rival, which read as rushing. Now:

- **Post**: it picks a post at its level's preferred range from the rival it hunts. Where a nest stands near that range,
  the post is 75 u BEHIND it, on the side away from the rival (the nest between them, its plates in the wall's reach);
  otherwise straight back along the line it holds; already within the band (350 u), it stays put. It never heads back
  to the nest it just left.
- **Dig in**: at the post it presses B. The stopped Serpent's latest trail prism becomes a held seed and the wall grows
  from the loose mass beside it (nest plates, rubble, its own trail) for as long as it stays stopped, and it is immune to
  elemental debuffs meanwhile - the shipped stance, nothing added. It turns in place to track (turning is tripled while
  stopped). The stance is read back off `IsTranslationRestricted` and re-pressed after 1 s if it did not take (a toggle).
- **Shoot**: with the rival within 15 deg of its nose it raises the scope, waits its level's settle time, and fires when
  its AIM POINT is inside the cone - so an error wider than the 1.5 deg cone is a real miss. After a shot, with its
  level's chance, it cloaks.
- **Relocate / retreat**: when its hold runs out it flies to a new post; the moment any rival comes inside its keep-away
  distance it lowers the scope, leaves the stance and backs off to its preferred range. It never turns to fight.
- **Forage**: while its rifle has at least its level's threshold of recovery left, and it is not dug in, it flies to
  the nearest crystal of the field nobody is waiting at.

**Difficulty is the seat's AI level, never the intensity** (intensity is the map, difficulty the opponent,
`AIDifficulty.cs`; each seat's own level through `GameDataSO.AIDifficultyFor`). The first cut keyed these ladders on the
intensity, against that rule. **Legendary plays aggressively** (Shombith: "Legendary AI can play aggressively for
sure"): it pushes to 900 u, only breaks off inside 350 u and digs in for 5 s before moving again - still a sniper,
never a rammer.

| Ladder (`SerpentGulagSettings.asset`) | Easy | Medium | Hard | Legendary |
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

The generator asserts the skill ladders are monotone toward harder, the three style ladders rise Easy to Hard with
Legendary below Easy, Legendary's aim is inside the cone and Easy's outside it, every preferred range is inside the hunt
range and the rifle's reach with a band above its keep-away, the marksman never reads the intensity, and the C#
initializers equal the asset.

## Platform changes

- `GameModes.SerpentGulag = 77` (no branch had claimed 77+; 67 and 75 are in flight), `EnumIntegrityTests` 70 -> 71.
- `EndConditionOverridesSO.serpentGulagSnipeTarget` (+ build twin, default, getter, target row, live / apply /
  capture) and the End Game Conditions window's field, "effective now" and build-summary rows.
- `GameToastSituation.SerpentGulagScopeHint = 157`.
- `MiniGameHUD`: SerpentGulag -> `BendsObjectiveProvider`.
- `author_preview_spawns.SCENE_FOR_MODE[77]`.
- `render_card_backgrounds.py`: `SpawnableSerpentGulag.cs` in `SOURCES`, a camera row and the accent (the field and a
  tracer across it striking a pilot). **It also adds `SwitchbackFunType.cs` to `SOURCES`**: the harness had stopped
  compiling on bleeding-edge since `SwitchbackCourse.cs` began naming that enum (2026-10-28), the break
  `audit_prism_clipping.py` had already fixed for its own harness.

## Verification status

**Authored headless; not yet flown in the editor.** Proved: the generator's `--check` (and a negative control that
named the mutated settings asset), the clip audit (0 clipping, 3 prefabs x 4 intensities), the bake gate, the card
render `--check`, and the standing gates (switch labels, enum references, console logging, conditional compilation,
using directives, game lists, preview spawns, cytoplasm shards), and a compile against real Unity references
(`unity_refcompile`, player and editor configs: 0 errors in project code). See `Docs/UNITY_VERIFICATION_CHECKLIST.md` for the
reference compile and the hand test. Not proved: how it plays.

## Known limitations

- **The AI sees cloaked pilots.** The cloak executor exposes no public "is cloaked" state and the kit is not changed
  for a mode (LOCKED, 2026-10-31). Garrett's Serpent Charge branch carries an `IsCloaked` accessor; when it lands, the
  marksman should skip a cloaked rival (`ChooseRival` / `IsLiveRival`).
- **The AI digs in behind a nest, not inside it.** Its post is 75 u behind the nest; flying into the pod through the
  back entry and holding the slit's bearing is the next step. It reaches the post by flying at it, so it may clip a
  plate on the way (a plain prism costs a Serpent nothing to ram).
- **The stance wall is the shipped one**: it grows only from loose mass near the seed, so in the open (I1) it is thin.
- **The target (3, Shombith's call after the first look) and the spawn ring (2,200) are first-pass numbers.** Re-tune after a playtest.
- **The arrow gives positions away.** It points at the nearest rival in every mode that uses it; a sniper duel may
  want it off at higher intensities - a playtest call.
- **Per-peer crystal field**: two peers can each collect "the same" crystal. Harmless while crystals score nothing.
