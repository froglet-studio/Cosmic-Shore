# Branch archive: `claude/vessel-jet-fx-audit-rdze50`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

## What this branch is

**Fleet-wide jet FX plus Dolphin/Sparrow tuning**

Gave every vessel the Squirrel's two-layer engine jet effect, tinted to the player's team colour, with a config asset, an audit editor tool and tests. It also carries earlier work: the Dolphin's Echo Sight ability and passive crystal seeding, a Dolphin ram cost, prism-collision slowdowns for the Sparrow, Dolphin and Manta on the Squirrel's numbers, a Sparrow bullet-growth change, and a four-intensity rework of the Rampage game mode.

- **Status:** Partly landed
- **Areas:** Vessel jet FX, Dolphin vessel, Sparrow vessel, Manta vessel, Rampage game mode, Editor tooling
- **Already in bleeding-edge:** Echo Sight landed (EchoSightHalo.shader; 59 files; commit a2abbd248 'feat(dolphin): Echo Sight paints super-shields in the danger colour'). The jet FX system is NOT in bleeding-edge: VesselJetFX.cs, VesselJetFXConfigSO.cs, Resources/VesselJetFXConfig.asset, Editor/VesselJetFXAudit.cs, VesselJetFXMountResolutionTests.cs and Docs/VESSEL_JET_FX.md are all absent.
- **Risk if deleted:** high
- **Suggestion (2026-10-08):** keep — The fleet-wide jet FX system, its tool, tests and doc exist only on this branch.

## Evidence

- **Last commit:** 2026-08-15 by Claude
- **Unmerged commits:** 48
- **Forked from:** `d32f26839` (2026-08-12, Merge pull request #710 from froglet-studio/claude/charge-crystal-shader-9u2ik)
- **Tip:** `99eb84ada`
- **Files touched (180):**
  - `.claude/skills/asset-surgery/SKILL.md`
  - `.claude/skills/ecology/SKILL.md`
  - `.claude/skills/ship/SKILL.md`
  - `.claude/skills/vessel/SKILL.md`
  - `.claude/skills/vessel/references/CONTRACT.md`
  - `Assets/Resources/ElementalAbilityMaps/Dolphin.asset`
  - `Assets/Resources/ElementalAbilityMaps/Sparrow.asset`
  - `Assets/Resources/EndConditionOverrides.asset`
  - `Assets/Resources/VesselJetFXConfig.asset`
  - `Assets/Resources/VesselJetFXConfig.asset.meta`
  - `Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph`
  - `Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph`
  - `Assets/_Graphics/Materials/Graphs/PrismDestructionSight.hlsl`
  - `Assets/_Graphics/Materials/Graphs/PrismDestructionSight.hlsl.meta`
  - `Assets/_Prefabs/Projectile/SparrowProjectile.prefab`
  - `Assets/_Prefabs/Spacevessels/Dolphin.prefab`
  - `Assets/_Prefabs/Spacevessels/Sparrow.prefab`
  - `Assets/_Prefabs/Trails/Prisms With Pools/PrismExplosion.prefab`
  - `Assets/_Prefabs/Trails/Prisms With Pools/Sparrow Projectile Prism.prefab`
  - `Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Cacti Flora Config Data.asset`
  - `Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Cacti Flora Config Data.asset.meta`
  - `Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Cell Config 1.asset`
  - `Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Cell Config 1.asset.meta`
  - `Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Cell Config 2.asset`
  - `Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Cell Config 2.asset.meta`
  - `Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Cell Config 3.asset`
  - `Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Cell Config 3.asset.meta`
  - `Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Cell Config 4.asset`
  - `Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Cell Config 4.asset.meta`
  - `Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Cell Config.asset`
  - `Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Coral Flora Config Data.asset`
  - `Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Coral Flora Config Data.asset.meta`
  - `Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Pine Flora Config Data.asset`
  - `Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Pine Flora Config Data.asset.meta`
  - `Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Rosette Flora Config Data.asset`
  - `Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Rosette Flora Config Data.asset.meta`
  - `Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Spawn Profile 1.asset`
  - `Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Spawn Profile 1.asset.meta`
  - `Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Spawn Profile 2.asset`
  - `Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Spawn Profile 2.asset.meta`
  - … and 140 more

### `e1bb8ed83` — fix(dogfight): turret muzzle, AI break-off, target 90, four crystals

_Claude, 2026-08-12 18:59:06 +0000_

```text
Five playtest findings. Two of them are real bugs, and neither was in the
code they looked like they were in.

1. TURRET FIRE DID NOTHING — AND IT WAS THE MUZZLE.

   The Sparrow carries TWO pairs of gun transforms, one per fire mode,
   and they had drifted 13.8 units apart:

       FullAutoActionExecutor/Guns       bullets   z =  1.30
       FullAutoBlockActionExecutor/Guns  turret    z = 15.13

   A shot is born at its muzzle, so every turret round spawned 15 units
   ahead of the nose and the first 15 units of its path did not exist.
   Dog Fight is built for close passes through a wreck field, so the
   enemy is routinely inside that gap and the round appeared already
   past them. No damage, therefore no points, no matter how correctly
   the scoring was wired — which it was.

   The reporter's own guess ("the point of origin of bullets for the
   sparrow is too far away from the model") was exactly right.

   Both pairs are bare Transforms — no renderer, no VFX, no children —
   so this only moves where the shot starts. Range is unaffected:
   anchor = muzzle + forward * range, so the anchor moves back with the
   muzzle and the path length is identical. The prism now emerges from
   the barrels instead of materialising ahead of the ship.

   NOT mode-specific. This was the turret stance, everywhere, for its
   whole life; Dog Fight only made it visible. Guarded in the generator
   (four transforms on the bullets' position, none left at z=15.13) —
   it is a SHARED vessel prefab, so a silent drift breaks every mode.

2. MASS NOW GROWS WHAT YOU HIT WITH, NOT JUST WHAT YOU SEE.

   Mass stretched the fired prism's z-axis but the flying collider was a
   fixed 1.65 / 2.475 sphere, so a Mass-buffed pilot fired visibly
   bigger rounds that connected exactly as often — a cosmetic buff on
   the ONE element this vessel's guns are wired to.

   hitDiameter now rides sqrt(massMultiplier). The square root is
   deliberate: the prism grows on one axis and the hit volume is a
   sphere, so the full multiplier would outrun the silhouette. At Mass
   10 (x2.5) the prism is 2.5x longer and the sphere 1.58x wider.

   This is the honest answer to "make the comeback with mass": all four
   elements still rise together (equal-elements is law), and Mass is
   simply the only one wired to the Sparrow's gun output — so it is the
   one that changes how your shots behave. It just had to actually do
   something first.

3. THE AI WOULD NOT LEAVE.

   The break-off had no state. It aimed at the quarry, and inside the
   break-off radius aimed at a point derived from the CURRENT geometry
   instead — recomputed every frame. So the instant the AI slipped past
   its target the vector flipped and the "escape" point landed back
   behind it. It turned straight round. Two ships welded together,
   grinding in a circle.

   Replaced with a committed PURSUE -> EXTEND -> PURSUE loop: at the
   merge the AI latches ONE escape point (through the target, out
   3x the break-off distance) and flies that fixed point until it
   arrives or the extend times out. An escape vector the target can
   steer is not an escape vector.

   This is also why the missiles were invisible. The skyburst was always
   on the AI's ability list and always fired; welded to a target at zero
   range a rocket has no room to fly and its blast lands nowhere useful.
   Separation is the geometry a skyburst is for. NO weapon or ability
   wiring changed — the loop drives SetExternalTargetProvider and
   nothing else, so it cannot leak into another mode.

4. POINT TARGET 120 -> 90. Comeback rate re-checked against it (a
   quarter-of-target deficit still buys 2.7 element levels, over the
   generator's one-level floor). Milestones follow automatically at 22
   and 45. Also fixed the generator's end-condition patch, which only
   INSERTED the key when absent — so a target change updated the C#
   default and every doc while leaving the asset the game actually reads
   on the old number.

5. FOUR OMNI CRYSTALS instead of one. A single crystal in a 520-unit
   arena is a needle nobody detours for. Kept on FixedCount rather than
   Scurry's PlayerCountPlusExtra+5, which would put nine in a full
   lobby — a handful, not a field. All four still need the authored
   anchorlessSpawnRadius or they stack on the origin.
```

```text
 Assets/Resources/EndConditionOverrides.asset                          |   4 +-
 Assets/_Prefabs/Spacevessels/Sparrow.prefab                           |   4 +-
 Assets/_Scenes/Multiplayer Scenes/MinigameDogFight.unity              |   4 +-
 Assets/_Scripts/Controller/Arcade/DOGFIGHT.md                         | 169 ++++++++++++++++++++++++--------
 Assets/_Scripts/Controller/Arcade/DogFightController.cs               | 105 ++++++++++++++++----
 .../R_VesselActions/Executors/FullAutoBlockShootActionExecutor.cs     |  24 ++++-
 .../Controller/Vessel/R_VesselActions/SPARROW_TURRET_STANCE.md        |  38 +++++++
 Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs          |   6 +-
 CLAUDE.md                                                             |  28 ++++--
 Tools/Build/author_dogfight_assets.py                                 |  68 ++++++++++++-
 10 files changed, 364 insertions(+), 86 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 746 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md b/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md
index 6f900460e..edd547b2f 100644
--- a/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md
+++ b/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md
@@ -12,7 +12,7 @@ Dog Fight is the **Sparrow-only gun duel**. Two to four pilots hunt each other t
 **Boneyard** — a wrecked world of hollow hulks, leaning spires and rubble canyons built for
 close encounters and hiding places. A **bullet hit scores 1**, a **missile hit scores 50**
 (direct strike *or* caught in the blast), and the first **DOMAIN** to the point target
-(default **120**) wins.
+(default **90**) wins.
 
 **One axis, and it is gunnery.** The scored stat is `IRoundStats.CombatPoints` — a weighted sum
 of landed vessel-vs-vessel hits. Nothing else scores: not the wreckage, not crystals, not
@@ -39,7 +39,7 @@ scoreboard anywhere before this.
   winning domain's pilots score their finish time, everyone else the `GolfScoreSentinels`
   sentinel (displayed "N Points Left")
 - **Turn monitor**: `DogFightPointTurnMonitor` — resolves the target from
-  `EndConditionOverridesSO.GetDogFightPointTarget()` (default **120**, FrogletTools ▸ Game Modes
+  `EndConditionOverridesSO.GetDogFightPointTarget()` (default **90**, FrogletTools ▸ Game Modes
   ▸ End Game Conditions — never a per-scene field), syncs it via NetworkVariable →
   `GameDataSO.CombatPointTargetCount`
 - **Players**: **2–4** with AI backfill. `MinDomainsAllowed = 2`, `MaxDomainsAllowed = 3`
@@ -49,7 +49,7 @@ scoreboard anywhere before this.
   `GameLists/OrganicRematchGames.asset`, `ProgressionConfig.alwaysUnlockedModes`)
 - **Objective marker**: `DogFightObjectiveProvider` — the off-screen arrow points at the nearest
   vessel you can actually shoot (see below)
-- **Crystals**: the scene's omni crystal on platform-normal settings (with an authored
+- **Crystals**: **four** omni crystals on platform-normal settings (with an authored
   `anchorlessSpawnRadius`, see below) **plus** elemental pickups scattered by `DogFightController`
 - **Comeback**: `ScoreDifferenceSource.CombatPoints`, rate **0.12** (see below)
 - **Environment**: `SpawnableBoneyard` at all four intensities, 9,043 → 34,654 prisms
@@ -376,6 +376,40 @@ The cell has **no nucleus**, so the ring has nothing to measure off and uses
 not a territorial claim, and a node-control zone would be a second silent objective nobody is
 playing for.)
 
+## The turret muzzles — why turret fire scored nothing
+
+Wiring the scoring effect into `SparrowPrismProjectileImpactContainer` was necessary and not
+sufficient. Turret shots still did no damage and scored no points, and the cause was not in the
+scoring path at all:
+
+**The Sparrow carries two pairs of gun transforms, one per fire mode, and they had drifted 13.8
+units apart.**
+
+| executor | fire mode | `LeftGun` / `RightGun` local position |
+|---|---|---|
+| `FullAutoActionExecutor` | bullets | `(±3.2, 0.4, `**`1.30`**`)` |
+| `FullAutoBlockActionExecutor` | turret prism rounds | `(±3.0, 0.4, `**`15.13`**`)` |
+
+A shot is **born at its muzzle**, so every turret round spawned 15 units ahead of the nose and the
+first 15 units of its path simply did not exist. This mode is built for close passes through a
+wreck field, so the enemy is routinely *inside* that gap — the round appeared already past them
+and hit nothing, no matter how correctly the scoring was wired. Playtest, and exactly right:
+*"maybe because the point of origin of bullets for the sparrow is too far away from the model."*
+
+Both pairs are bare `Transform`s — no renderer, no VFX, no children — so the position is purely
+where the shot starts. The turret's pair is moved onto the bullets' position, which is also the
+documented rule for this weapon (`SPARROW_TURRET_STANCE.md`: *"a turret shot **is** a bullet —
+you just see a prism flying"*).
+
+**Range is unaffected.** The executor computes `anchor = muzzle + forward × range`, so moving the
+muzzle back moves the anchor back with it; the path length is identical and nothing needs
+retuning. The prism now visibly emerges from the gun barrels instead of materialising ahead of
+the ship.
+
+The generator asserts this on every run — four gun transforms on the bullets' position, and no
+transform left at `z = 15.13`. It is authored on a **shared vessel prefab**, so a silent drift
+here breaks the Sparrow in every mode, not just this one.
+
 ## AI dogfighters
 
 **The AI's guns need no wiring.** The Sparrow prefab's `AIPilot` already runs `FullAutoAction`
@@ -387,12 +421,32 @@ what *"in front of it"* means.
   its target forever and flies through on arrival — so an AI aimed at an opponent's *current*
   position permanently trails them and only ever fires where they were. The aim point is
   `aiLeadSeconds` (0.6) ahead along the quarry's own course.
-- **Break off on the merge.** Inside `aiBreakOffDistance` (120) the aim point flips to a spot
-  *beyond* the quarry, so the AI commits to an overshoot and comes back around instead of
-  grinding hull-to-hull. Without it, "steer at the enemy forever" degenerates into a ramming
-  contest neither pilot can shoot their way out of — the same class of mistake as Wildlife
```

</details>

### `80eb7567c` — feat(rampage): rebuild as the Dolphin's demolition race

_Claude, 2026-08-13 13:55:10 +0000_

```text
Rampage becomes Dolphin-only and is arranged so the vessel's own economy IS
the game: energy is banked only by skimming, discharged only on a crystal,
and the blast's gape is the energy you brought. So the arena grows a belt of
breakable flora to graze and carries exactly ONE contested crystal.

Mode:
- ArcadeGameRampage.Vessels -> Dolphin only (platform two-place clamp
  enforces it: SyncFromArcadeGame + ResolveSpawnVesselType); scene AI
  templates -> Dolphin.
- Cell spawn ring on (Symmetric, ring radius 700) instead of four hulls in a
  +/-50 box at the arena centre.
- Crystal roam volume authored (anchorlessSpawnRadius 900) so the single
  neutral crystal ranges the core and belt fringe instead of rattling around
  the nucleus.
- RampageController: AI runs the mode's two-phase loop - graze the densest
  hostile mass while Energy < 0.6, break for the crystal once charged.
  Single-phase mass hunting banks a meter it can never fire; the AIPilot
  default dumps an empty meter on arrival.
- RampageObjectiveProvider: the arrow points at the contested crystal
  (HexRace's provider filters by domain and would reject a neutral one).

Arena: five species on staggered planting shells at 0.76-0.94 of the membrane
radius - cacti (hero, 5x5x3 leaf prisms), rosette, spire, pine, coral - all
three domains, elements and levels spread; core left open as the crystal's
ground. Phase ladder authored in volume against the belt's real prism sizes.

Ecology fixes this exposed (platform-wide, Docs/ECOSYSTEM.md 27):
- A planting shell is measured from the CELL CENTRE, not the crystal. All
  three Flora.Plant implementations dispersed around cellData.CrystalTransform
  while ResolvePlantRadius and every docstring said "fraction of the cell's
  membrane radius" - agreeing only while crystals sit in the core. A roaming
  crystal would have planted outside the membrane, where ContainsPosition
  rejects the prisms and neither the volume ladder nor the fauna grids can see
  them. Also removes an unguarded null deref in a crystal-less cell.
- FloraVariantTuning.MaxTotalSpawnedObjects now works on branching and
  phyllotactic flora, not only assembled. 45 authored assets were writing into
  an inert field; branching species were silently taking the prefab's 5000.
- New cell-level overrides (PlantRadiusCellFractionOverride,
  MaxTotalSpawnedObjectsOverride, default off) applied after the variant roll,
  so a cell can use the canonical per-element assets AND keep its own layout.
  The element owns identity; the cell owns layout.
```

```text
 .../Cell Configs/Rampage Cell/Rampage Cacti Flora Config Data.asset   |  45 ++++
 .../Rampage Cell/Rampage Cacti Flora Config Data.asset.meta           |   8 +
 Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Cell Config.asset |  16 +-
 .../Cell Configs/Rampage Cell/Rampage Coral Flora Config Data.asset   |  45 ++++
 .../Rampage Cell/Rampage Coral Flora Config Data.asset.meta           |   8 +
 .../Cell Configs/Rampage Cell/Rampage Pine Flora Config Data.asset    |  45 ++++
 .../Rampage Cell/Rampage Pine Flora Config Data.asset.meta            |   8 +
 .../Cell Configs/Rampage Cell/Rampage Rosette Flora Config Data.asset |  45 ++++
 .../Rampage Cell/Rampage Rosette Flora Config Data.asset.meta         |   8 +
 .../_SO_Assets/Cell Configs/Rampage Cell/Rampage Spawn Profile.asset  |   9 +-
 .../Cell Configs/Rampage Cell/Rampage Spire Flora Config Data.asset   |  45 ++++
 .../Rampage Cell/Rampage Spire Flora Config Data.asset.meta           |   8 +
 Assets/_SO_Assets/Games/ArcadeGameRampage.asset                       |   7 +-
 Assets/_Scenes/Multiplayer Scenes/MinigameRampage.unity               |  14 +-
 Assets/_Scripts/Controller/Arcade/RAMPAGE.md                          | 385 +++++++++++++++++++++++++-------
 Assets/_Scripts/Controller/Arcade/RampageController.cs                | 122 ++++++++--
 Assets/_Scripts/Controller/Arcade/RampageObjectiveProvider.cs         | 131 +++++++++++
 Assets/_Scripts/Controller/Arcade/RampageObjectiveProvider.cs.meta    |   2 +
 Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs         |   8 +
 .../_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs   |   4 +-
 .../_Scripts/Controller/Environment/FloraAndFauna/BranchingFlora.cs   |  32 ++-
 Assets/_Scripts/Controller/Environment/FloraAndFauna/Flora.cs         |  25 +++
 .../Controller/Environment/FloraAndFauna/PhyllotacticFlora.cs         |  17 +-
 Assets/_Scripts/UI/MiniGameHUD.cs                                     |   2 +
 Assets/_Scripts/Utility/DataContainers/FloraConfigurationSO.cs        |  49 +++-
 CLAUDE.md                                                             |   6 +-
 Docs/ECOSYSTEM.md                                                     | 131 +++++++++++
 27 files changed, 1101 insertions(+), 124 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1191 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/RAMPAGE.md b/Assets/_Scripts/Controller/Arcade/RAMPAGE.md
index 2aef95630..0d11f27e7 100644
--- a/Assets/_Scripts/Controller/Arcade/RAMPAGE.md
+++ b/Assets/_Scripts/Controller/Arcade/RAMPAGE.md
@@ -2,32 +2,46 @@
 
 ## Overview
 
-Rampage is the **destructive analog of Crystal Capture ("Scurry")**: a multiplayer
-party game where every domain races to be the first to DESTROY the prism target.
-Simple destructive fun — fly hard, smash mass, watch the counter fall.
+Rampage is the **Dolphin-only demolition race**, and the destructive analog of Crystal
+Capture ("Scurry"): every domain races to be the first to DESTROY **2,000 hostile
+prisms**. A belt of cacti and other breakable flora rings the membrane, the arena core
+is left open, and a **single contested crystal** roams it.
+
+**The loop is the Dolphin's own economy, made into a sport.** Nothing here is scripted —
+the mode simply arranges the arena so the vessel's existing spine becomes the game:
+
+| the vessel already does this | Rampage makes it the game |
+|---|---|
+| Energy is banked **only by skimming** (+0.006667/skim, 150 skims fills it) | a cactus forest is the charging ground — and every prism you clip on the way through scores |
+| Touching a **crystal** spends the whole meter as one conic jaw blast | the arena carries exactly **one** crystal, so cashing out is contested |
+| Energy owns the blast's **GAPE** (4.76° empty → 23.43° full) | arriving charged is worth ~5× the swath of arriving empty |
+| The cone reaches **2,400 units** down-range | from anywhere in the core, a blast aimed outward sweeps the whole belt |
+| Ramming a prism **halves** the meter | flying *through* the thicket instead of *into* it is the skill |
+
+So a round reads: **graze the belt to charge → break for the crystal → aim at the
+thickest part of the forest → fire.** See `DOLPHIN_ENERGY_ECONOMY.md` §1 for the
+economy itself; this file only arranges around it.
 
 - **Only hostile mass scores.** The metric is `IRoundStats.HostilePrismsDestroyed`.
   "Hostile" means everything except your own team's **player-laid** mass: ALL
-  environment mass scores regardless of color (flora and fauna carry non-roster
-  owner names — `DefaultPlayer`/`FaunaPrefab` — so `StatsManager` classifies their
-  destruction hostile), and opponents' trails score; your own and your teammates'
-  trails never do (trails ARE rostered, so the domain check filters them).
+  environment mass scores regardless of colour (flora and fauna carry non-roster
+  owner names — `DefaultPlayer`/`FaunaPrefab`/`flora` — so `StatsManager` classifies
+  their destruction hostile), and opponents' trails score; your own and your
+  teammates' trails never do (trails ARE rostered, so the domain check filters them).
   Shattering your own trail is worthless *by construction*, so there is no
   lay-and-smash farming loop — but every wild prism in the arena is fair game.
 - **Destruction is the sanctioned mass sink.** The conserved-mass law says prisms are
   removed only by an *active* force — vessel abilities or fauna consumption. Rampage
   is that law played as a sport: every scoring act is a vessel ability consuming mass.
   No decay, no timers, no cullers anywhere in the mode.
-- **The arena restocks itself.** The Rampage Cell is flora-rich (Blob-class profile,
-  all three domains seeded per the no-domain-asymmetry invariant). As players carve
-  the prismscape down, the cell drops below its phase thresholds and flora growth
-  resumes — the food web and the demolition derby feed each other.
+- **The arena restocks itself.** As players carve the belt down, the cell drops below
+  its phase thresholds and flora planting + growth resume — the food web and the
+  demolition derby feed each other.
 
 **Key architectural facts:**
 
 - **Scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameRampage.unity` (single unified
-  scene, cloned from Brood Rush's skeleton — no separate singleplayer variant; solo
-  play is a party of one + AI backfill)
+  scene — no separate singleplayer variant; solo play is a party of one + AI backfill)
 - **GameMode enum**: `GameModes.Rampage = 2` — repurposed from the legacy
   single-player arcade entry (whose `MinigameRampage` scene never shipped; nothing
   playable depended on the old meaning)
@@ -41,29 +55,55 @@ Simple destructive fun — fly hard, smash mass, watch the counter fall.
   individual prisms smashed on the secondary line); TEAM-major by construction
 - **Turn monitor**: `RampagePrismTurnMonitor` — resolves the prism target from
   `EndConditionOverridesSO.GetRampagePrismTarget()` at StartMonitor (default **2000**,
-  FrogletTools ▸ Game Modes ▸ End Game Conditions— never a per-scene field), syncs it via
+  FrogletTools ▸ Game Modes ▸ End Game Conditions — never a per-scene field), syncs it via
   NetworkVariable → `GameDataSO.PrismTargetCount`, ends the turn via
   `rule.IsObjectiveReached`
 - **Domains**: free-for-all like Scurry (`MinDomainsAllowed`/`MaxDomainsAllowed`
   defaults 1/3); players 1–4 with AI backfill
-- **Vessels**: Sparrow (guns + missiles), Rhino (ram), Dolphin
-- **AI opponents**: all-Rhino mass hunters. The scene's four AI backfill templates
-  spawn Rhinos, and `RampageController.ArmMassHunters()` (server, at countdown end
-  — mirroring Astro League's `ArmStrikers`) points each `AIPilot` at
-  `Cell.GetExplosionTarget(aiDomain)` — the densest region of mass hostile to the
-  AI's domain, the same density-grid query aggression-1 fauna use (no physics
```

</details>

### `7f8454981` — feat(sparrow): spray accuracy — 2x fire rate, decaying cone, rising haptic

_Claude, 2026-08-13 13:58:36 +0000_

```text
Holding the full-auto trigger now opens a stochastic cone instead of firing a
line: 0.12 s of perfect accuracy, then linear growth to a 4 deg half-angle cap
over ~1.4 s, with every round deflected to a hash-sampled point inside it. The
cap sits just past where the spread would cost you the target you wanted, so a
held burst saturates a growing danger zone rather than becoming a worse gun.
Releasing the trigger resets accuracy completely, so tapped bursts stay
surgical.

Rate of fire 30 -> 60 volleys/s (120 rounds/s). Round 6 shrank the bullet hit
sphere 8x after finding nothing had authored the old 12 diameter; this is the
sanctioned other half of that trade — aim forgiveness returns as volume of fire
x cone coverage, not a bigger invisible ball.

Both fire loops move from UniTask.Delay(1/rate) to a time accumulator. A
frame-quantized delay caps at one volley per frame, so the authored rate was
silently min(rate, framerate) and 60/s would have halved on a 30 fps device.
Capped at 4 volleys/tick with excess dropped, so a hitch never discharges as a
burst.

Turret Stance inherits all of it through the existing bulletAction parity — a
turret shot IS a bullet, so it walks off aim identically and the prism stays
where the deflected round died. The deflection composes onto the muzzle pose
(FromToRotation) rather than rebuilding it with LookRotation, preserving roll:
the prism's long axis is the shot.

The escalating buzz is a deliberate fourth haptic feel per Docs/HAPTICS.md
"Adding / changing a feel" — dedicated PlaySpray() with the gate extended. As
the only continuous feel it sits at the BOTTOM of the priority order
(alert > punish > skim > spray): everything interrupts it, it interrupts
nothing, so a thud still cuts cleanly through a held burst. Local human pilot
only.

The cone math uses an integer hash, never UnityEngine.Random: the global stream
is shared state deterministic systems seed (HexRace track), and a gun drawing
from it 120x/s would couple their output to trigger-hold time.

Knock-on effects recorded, not hidden: turret stance now lays ~120 prisms/s of
permanent mass (firingRate remains the single lever — no turret-only divisor),
and Dog Fight's pace roughly doubles (its point target is authored, so retuning
needs no code change). Pools resized for both.
```

```text
 Assets/_Prefabs/Spacevessels/Sparrow.prefab                           |  70 +++++++--
 Assets/_SO_Assets/VesselActions/Sparrow/FullAutoAction.asset          |  10 +-
 Assets/_Scripts/Controller/IO/HapticController.cs                     |  77 +++++++++-
 Assets/_Scripts/Controller/Projectiles/Gun.cs                         |   8 +-
 .../Vessel/R_VesselActions/Data Containers/FullAutoActionSO.cs        |  24 ++-
 .../R_VesselActions/Data Containers/FullAutoBlockShootActionSO.cs     |  14 +-
 .../Vessel/R_VesselActions/Data Containers/GunSpreadProfile.cs        |  83 +++++++++++
 .../Vessel/R_VesselActions/Data Containers/GunSpreadProfile.cs.meta   |  11 ++
 .../Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs        | 132 +++++++++++------
 .../R_VesselActions/Executors/FullAutoBlockShootActionExecutor.cs     | 149 ++++++++++++-------
 .../Controller/Vessel/R_VesselActions/Executors/GunSprayAccuracy.cs   | 158 ++++++++++++++++++++
 .../Vessel/R_VesselActions/Executors/GunSprayAccuracy.cs.meta         |  11 ++
 .../Controller/Vessel/R_VesselActions/SPARROW_SPRAY_ACCURACY.md       | 249 ++++++++++++++++++++++++++++++++
 .../Controller/Vessel/R_VesselActions/SPARROW_SPRAY_ACCURACY.md.meta  |   7 +
 .../Controller/Vessel/R_VesselActions/SPARROW_TURRET_STANCE.md        |  60 ++++++--
 Assets/_Scripts/Tests/Editor/GunSpreadMathTests.cs                    | 228 +++++++++++++++++++++++++++++
 Assets/_Scripts/Tests/Editor/GunSpreadMathTests.cs.meta               |  11 ++
 Assets/_Scripts/Utility/GunSpreadMath.cs                              | 105 ++++++++++++++
 Assets/_Scripts/Utility/GunSpreadMath.cs.meta                         |  11 ++
 CLAUDE.md                                                             |   2 +-
 Docs/HAPTICS.md                                                       |  62 ++++++--
 Docs/UNITY_VERIFICATION_CHECKLIST.md                                  |  61 ++++++++
 22 files changed, 1392 insertions(+), 151 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1698 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/IO/HapticController.cs b/Assets/_Scripts/Controller/IO/HapticController.cs
index c6a733086..77a6842d3 100644
--- a/Assets/_Scripts/Controller/IO/HapticController.cs
+++ b/Assets/_Scripts/Controller/IO/HapticController.cs
@@ -26,19 +26,25 @@ namespace CosmicShore.Gameplay
     /// <summary>
     /// The whole game's haptic policy in one place.
     ///
-    /// Cosmic Shore ships exactly TWO feels, both local-human-pilot-only:
+    /// Cosmic Shore ships TWO everyday feels, both local-human-pilot-only:
     ///   • <see cref="PlaySkim"/>   — the reward: a bright, sharp, proximity-scaled pulse; many in
     ///     sequence read as a rapid, continuously rewarding pulse train (Squirrel skim).
     ///   • <see cref="PlayPunish"/> — the mistake: one heavy, low-frequency thud when the vessel
     ///     body slams a prism.
     ///
+    /// …plus two fenced additions, each a deliberate exercise of the "adding a feel" clause:
+    ///   • <see cref="PlayAlert"/>  — a long rattle for RARE match-changing events.
+    ///   • <see cref="PlaySpray"/>  — a rising buzz while a full-auto trigger is held.
+    ///
     /// Everything else is deliberately silent — the legacy <see cref="PlayHaptic"/> /
     /// <see cref="PlayConstant"/> entry points (UI, drift, boost, jousts, explosions, overtake,
     /// elemental debuffs …) are no-ops.
     ///
     /// NiceVibrations keeps only ONE loaded clip — every <c>Load()</c> evicts whatever is playing —
-    /// so a tiny priority/rate-limit gate arbitrates the two feels: punish outranks skim (punish
-    /// always interrupts the skim train; the skim train never interrupts a thud).
+    /// so a tiny priority/rate-limit gate arbitrates them. Priority, top to bottom:
+    /// <b>alert &gt; punish &gt; skim &gt; spray</b>. Punish always interrupts the skim train and
+    /// the skim train never interrupts a thud; the spray is a texture, so it yields to all three
+    /// and interrupts none of them.
     /// </summary>
     public class HapticController : MonoBehaviour
     {
@@ -73,11 +79,19 @@ namespace CosmicShore.Gameplay
         const float AlertMinIntervalSec = 1.500f;  // can't stack or retrigger into a drone
         const float AlertDurationSec = 1.200f;     // ~1.2 s of shaking
 
+        // The spray buzz is a TEXTURE, not an event: it repeats for as long as a trigger is
+        // held, so it sits at the BOTTOM of the priority order and never suppresses anything.
+        const float SprayMinIntervalSec = 0.035f;  // backstop floor; the caller sets the real cadence
+        const float SprayDurationSec = 0.050f;     // one short buzz per pulse
+        const float SkimDurationSec = 0.070f;      // the skim clip's length — read only by spray
+
         static float s_lastSkimTime = -999f;
+        static float s_skimBusyUntil = -999f;      // spray is suppressed until here (skim outranks it)
         static float s_lastPunishTime = -999f;
         static float s_punishBusyUntil = -999f;    // skim is suppressed until here (punish owns the motor)
         static float s_lastAlertTime = -999f;
         static float s_alertBusyUntil = -999f;     // skim AND punish are suppressed until here
+        static float s_lastSprayTime = -999f;
 
         /// <summary>
         /// The reward pulse. <paramref name="strength01"/> (0..1) is how close the prism passed to
@@ -93,6 +107,7 @@ namespace CosmicShore.Gameplay
             if (now < s_punishBusyUntil) return;                 // punish outranks skim — don't interrupt it
             if (now - s_lastSkimTime < SkimMinIntervalSec) return;
             s_lastSkimTime = now;
+            s_skimBusyUntil = now + SkimDurationSec;   // spray must not cut the reward short
 
             EnsureClips();
             LofeltHaptics.Load(s_skimJson, s_skimRumble);
@@ -150,6 +165,44 @@ namespace CosmicShore.Gameplay
             LofeltHaptics.Play();
         }
 
+        /// <summary>
+        /// The SPRAY buzz — the fourth feel, added deliberately (Docs/HAPTICS.md ▸ "Adding /
+        /// changing a feel", which requires a dedicated method and an extended gate rather than
+        /// the silenced legacy API). A short mid-frequency buzz with no transient: neither the
+        /// bright skim, the dull thud, nor the long rattle.
+        ///
+        /// It is the game's only CONTINUOUS feel, and that is precisely why it sits at the
+        /// BOTTOM of the priority order — alert, punish and skim all suppress it, and it
+        /// suppresses nothing. A texture that could cut off an event would make the two feels
+        /// the policy is built around less legible, not more; being interruptible costs the
+        /// spray nothing because the very next pulse is milliseconds away.
+        ///
+        /// <paramref name="strength01"/> is how far the gun's accuracy has decayed. The CALLER
+        /// owns the cadence (it tightens with the same quantity, so the buzz climbs in rate as
+        /// well as strength); the interval floor here is only a backstop against a second caller.
```

</details>

### `fc37bfd08` — fix(prisms): death visuals wear the dying prism's tier, not just its domain

_Claude, 2026-08-13 14:02:30 +0000_

```text
Danger prisms exploded with the base-domain palette. Debris colour was resolved
from the dying prism's domain alone, at the PLAIN tier, so a danger prism - a
frosty shielded base under the hot domain-independent danger rim - shattered
into ordinary domain-coloured debris and read as a plain prism dying. Shielded
and super-shielded mass had the same defect (visible when a devastating hit
explodes shielded mass instead of shedding its shield).

The tier composition now lives in ONE place, SO_ColorSet.GetPrismKindColors,
with two consumers that must keep reading it: ThemeManager paints the live
block materials from it (via the new PaintPrismTier) and PrismFactory tints the
death visuals from it. The dying prism's PrismKind rides PrismEventData.Kind,
stamped in Prism.Explode/Implode from the new PrismKinds.Of *before* the
destruction pass, and both routes honour it - the batched pure-entity debris
and the pooled fallback.

Free on the batch: debris colour is already a per-entity override, so a
mixed-tier burst is still one em.Instantiate and one draw. No new material, no
new prototype, no extra draw call.

Danger additionally detonates harder - PrismExplosion.DetonationGain, authored
as dangerDetonationMultiplier on PrismExplosion.prefab (1.6; set 1 for
palette-only). It scales debris speed, shatter rate and the clamp band as one
quantity, per the AOE-impulse contract.

Adds PrismDeathVisualTierTests (edit-mode, pure) covering the tier
composition, the domain-independent danger rim, the plain-palette regression
guard, the fail-closed unauthored-domain path, PrismKinds.Of precedence, and
the detonation gain. Docs: PALETTE.md 2.1, PRISM_ANIMATION.md 4.6, CLAUDE.md.
```

```text
 Assets/_Prefabs/Trails/Prisms With Pools/PrismExplosion.prefab        |   1 +
 Assets/_Scripts/Controller/Environment/Spawning/PrismKinds.cs         |  25 ++++
 Assets/_Scripts/Controller/Managers/ThemeManager.cs                   |  63 +++++-----
 Assets/_Scripts/Controller/Prisms/PrismFactory.cs                     |  45 +++----
 Assets/_Scripts/Controller/Vessel/Prism.cs                            |  10 ++
 .../SOAP/ScriptableEventWithReturn/PrismEventChannelWithReturnSO.cs   |   9 ++
 Assets/_Scripts/ScriptableObjects/SO_ColorSet.cs                      |  57 +++++++++
 Assets/_Scripts/Tests/Editor/PrismDeathVisualTierTests.cs             | 217 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/Tests/Editor/PrismDeathVisualTierTests.cs.meta        |   2 +
 Assets/_Scripts/Utility/Effects/PrismDebris.cs                        |  27 ++--
 Assets/_Scripts/Utility/Effects/PrismExplosion.cs                     |  44 ++++++-
 CLAUDE.md                                                             |   2 +
 Docs/PALETTE.md                                                       |  37 ++++++
 Docs/PRISM_ANIMATION.md                                               |  15 +++
 14 files changed, 491 insertions(+), 63 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 850 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Spawning/PrismKinds.cs b/Assets/_Scripts/Controller/Environment/Spawning/PrismKinds.cs
index 6bc524220..43c0da1be 100644
--- a/Assets/_Scripts/Controller/Environment/Spawning/PrismKinds.cs
+++ b/Assets/_Scripts/Controller/Environment/Spawning/PrismKinds.cs
@@ -62,5 +62,30 @@ namespace CosmicShore.Gameplay
             Clear(prism);
             Apply(prism, kind);
         }
+
+        /// <summary>
+        /// The kind a LIVE prism is currently wearing - the read half of <see cref="Apply"/>.
+        /// Read off <c>prismProperties</c> rather than <c>PrismStateManager.CurrentState</c>
+        /// because the flags are the authoritative record: spawners set them pre-Initialize and
+        /// <c>Prism.Initialize</c> re-engages the state machine from them on every pool reuse.
+        ///
+        /// The three flags are mutually exclusive by construction (<c>MakeDangerous</c> clears
+        /// both shields; <c>ActivateSuperShield</c> clears danger and shield), so the ordering
+        /// only decides what a CORRUPT prism reports. It matches gameplay precedence:
+        /// super-shield first, because that is the flag that makes a prism invulnerable and
+        /// stops an AOE dead regardless of anything else set alongside it.
+        /// </summary>
+        public static PrismKind Of(Prism prism) => Of(prism ? prism.prismProperties : null);
+
+        /// <summary>Kind of a bare property bag - the pure, testable half of
+        /// <see cref="Of(Prism)"/>. Null (a prism that has not run Awake) reads Plain.</summary>
+        public static PrismKind Of(PrismProperties props)
+        {
+            if (props == null) return PrismKind.Plain;
+            if (props.IsSuperShielded) return PrismKind.SuperShielded;
+            if (props.IsDangerous) return PrismKind.Danger;
+            if (props.IsShielded) return PrismKind.Shielded;
+            return PrismKind.Plain;
+        }
     }
 }
diff --git a/Assets/_Scripts/Controller/Managers/ThemeManager.cs b/Assets/_Scripts/Controller/Managers/ThemeManager.cs
index 4fe81a9ed..c69d556d7 100644
--- a/Assets/_Scripts/Controller/Managers/ThemeManager.cs
+++ b/Assets/_Scripts/Controller/Managers/ThemeManager.cs
@@ -55,12 +55,21 @@ namespace CosmicShore.Gameplay
             materialSet.SpikeMaterial = new Material(_dataContainer.BaseMaterialSet.SpikeMaterial);
             materialSet.SkimmerMaterial = new Material(_dataContainer.BaseMaterialSet.SkimmerMaterial);
 
-            // Set colors for materials that use domain-specific colors
-            materialSet.BlockMaterial.SetColor("_BrightColor", colorSet.InsideBlockColor);
-            materialSet.BlockMaterial.SetColor("_DarkColor", colorSet.OutsideBlockColor);
-
-            materialSet.TransparentBlockMaterial.SetColor("_BrightColor", colorSet.InsideBlockColor);
-            materialSet.TransparentBlockMaterial.SetColor("_DarkColor", colorSet.OutsideBlockColor);
+            // Set colors for materials that use domain-specific colors.
+            //
+            // The four prism TIERS are painted from SO_ColorSet.GetPrismKindColors - the single
+            // definition of "what is a prism of this kind wearing". PrismFactory tints the death
+            // debris from the same method, so a prism's debris can never disagree with the prism
+            // (a danger prism exploding into plain-domain-coloured debris was exactly that
+            // disagreement). Do not re-inline a tier's colour pair here.
+            PaintPrismTier(materialSet.BlockMaterial, materialSet.TransparentBlockMaterial,
+                           colorSet, PrismKind.Plain);
+            PaintPrismTier(materialSet.DangerousBlockMaterial, materialSet.TransparentDangerousBlockMaterial,
+                           colorSet, PrismKind.Danger);
+            PaintPrismTier(materialSet.ShieldedBlockMaterial, materialSet.TransparentShieldedBlockMaterial,
+                           colorSet, PrismKind.Shielded);
+            PaintPrismTier(materialSet.SuperShieldedBlockMaterial, materialSet.TransparentSuperShieldedBlockMaterial,
+                           colorSet, PrismKind.SuperShielded);
 
             materialSet.CrystalMaterial.SetColor("_BrightCrystalColor", colorSet.BrightCrystalColor);
             materialSet.CrystalMaterial.SetColor("_DullCrystalColor", colorSet.DullCrystalColor);
@@ -71,33 +80,13 @@ namespace CosmicShore.Gameplay
             materialSet.CrystalMaterial3.SetColor("_BrightCrystalColor", colorSet.BrightCrystalColor);
             materialSet.CrystalMaterial3.SetColor("_DullCrystalColor", colorSet.DullCrystalColor);
             
+            // The pooled debris prefab's own shared material is the one the batched debris path
+            // actually draws with (PrismDebris reads mesh/material off it) and its colours arrive
+            // as PER-ENTITY overrides keyed on the dying prism's kind - so this per-domain copy is
+            // never consumed. Kept painted at the PLAIN tier for parity with the other materials.
             materialSet.ExplodingBlockMaterial.SetColor("_BrightColor", colorSet.InsideBlockColor);
             materialSet.ExplodingBlockMaterial.SetColor("_DarkColor", colorSet.OutsideBlockColor);
 
-            // Danger prisms take the domain's SHIELDED base face (_DarkColor) rather than its
-            // plain one, so a danger prism reads as a distinct, frostier tier of that domain at
```

</details>

### `b7541714e` — fix(rampage): couple crystals to the nucleus, band the flora, fix AI drift

_Claude, 2026-08-13 16:26:27 +0000_

```text
Four corrections, three of them platform-wide (Docs/ECOSYSTEM.md 27.5-27.7).

1. The omni-crystal respawn volume IS the nucleus, everywhere.
   CrystalManager.GetAnchorlessSpawnRadius resolved serialized-override ->
   nucleus, so any scene could decouple its crystals from its core with one
   field - and Rampage did (a 900u roam radius). Inverted: nucleus wins and
   nothing may override it; the serialized radius is now noNucleusSpawnRadius,
   the fallback for a cell with genuinely no core (Dog Fight's Boneyard, 420).
   The nucleus is the visible marker of the middle; a crystal that respawns
   elsewhere makes that marker a lie. A mode that wants a different crystal
   volume resizes its NUCLEUS, which moves both together.

2. Flora plant in a BAND, not on a shell, and they are much bigger.
   Flora.plantRadiusCellFractionMin + a volume-uniform draw
   (r = cbrt(lerp(inner^3, outer^3, u))) - uniform-in-radius would crowd the
   inner edge and leave most of the cell empty. The inner edge is clamped
   outside the nucleus in code, so an author can write 0 and get "from the
   nucleus outward": nucleus mass is the territorial claim, is excluded from
   the fauna targeting grids, and shares its volume with the crystal respawn.
   Default min 0 = legacy single shell, so no existing cell changes.
   Rampage's forest now runs 0.17-0.97 of the membrane radius with per-plant
   budgets ~3x larger, LeafScalePerLevel 1.25-1.30 and RarityFalloff 1.6 - a
   1.0x-2.9x linear size range with big plants common rather than rare.
   Phase ladder re-authored to the resulting volume (~1.62M).

3. The AI flies to the crystal, and drifts onto mass instead of flipping.
   AIPilot's drift look-direction was desiredDirection *= -1, a flat 180 away
   from the objective that aims at nothing and reads as spinning on the spot.
   It now points at a cluster of hostile mass via Cell.GetExplosionTarget -
   the same Burst density-grid query aggression-1 fauna hunt with, sampled on
   a 1.5s cadence. Falls back to the flip with no cell/mass, or when the
   cluster lies along the objective. Platform-wide, so every drifting AI
   benefits. Rampage's mode-local two-phase target provider is removed: an
   external provider overrides crystal seeking outright, which is the one
   thing an AI must not stop doing in a mode whose objective is a crystal.

4. The objective arrow can no longer inherit another mode's provider.
   MiniGameHUD.Start can run before the config ClientRpc lands, resolving the
   provider against a stale GameMode - which silently hands a crystal mode an
   arrow that points at other PLAYERS. Re-resolved (and the stale provider
   object destroyed) once the mode is authoritative.
```

```text
 .../Cell Configs/Rampage Cell/Rampage Cacti Flora Config Data.asset   |  14 +-
 Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Cell Config.asset |  20 ++-
 .../Cell Configs/Rampage Cell/Rampage Coral Flora Config Data.asset   |  12 +-
 .../Cell Configs/Rampage Cell/Rampage Pine Flora Config Data.asset    |  12 +-
 .../Cell Configs/Rampage Cell/Rampage Rosette Flora Config Data.asset |  12 +-
 .../Cell Configs/Rampage Cell/Rampage Spire Flora Config Data.asset   |  12 +-
 Assets/_Scenes/Menu_Main.unity                                        |   2 +-
 Assets/_Scenes/Multiplayer Scenes/MinigameDogFight.unity              |   2 +-
 Assets/_Scenes/Multiplayer Scenes/MinigameRampage.unity               |   3 -
 Assets/_Scripts/Controller/AI/AIPilot.cs                              |  67 +++++++-
 Assets/_Scripts/Controller/Arcade/CRYSTAL_CAPTURE.md                  |   2 +-
 Assets/_Scripts/Controller/Arcade/DOGFIGHT.md                         |   6 +-
 Assets/_Scripts/Controller/Arcade/DogFightController.cs               |   2 +-
 Assets/_Scripts/Controller/Arcade/RAMPAGE.md                          | 281 +++++++++++++++++++-------------
 Assets/_Scripts/Controller/Arcade/RampageController.cs                | 125 ++------------
 Assets/_Scripts/Controller/Environment/FloraAndFauna/Flora.cs         |  62 +++++--
 Assets/_Scripts/Controller/Environment/FlowField/CrystalManager.cs    |  39 +++--
 Assets/_Scripts/UI/MiniGameHUD.cs                                     |  38 +++++
 Assets/_Scripts/Utility/DataContainers/FloraConfigurationSO.cs        |  37 +++--
 CLAUDE.md                                                             |   2 +-
 Docs/ECOSYSTEM.md                                                     |  80 +++++++++
 21 files changed, 516 insertions(+), 314 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1126 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/AI/AIPilot.cs b/Assets/_Scripts/Controller/AI/AIPilot.cs
index 318ad5d24..fcd210af9 100644
--- a/Assets/_Scripts/Controller/AI/AIPilot.cs
+++ b/Assets/_Scripts/Controller/AI/AIPilot.cs
@@ -66,6 +66,12 @@ namespace CosmicShore.Gameplay
         [Tooltip("Faster re-scan cadence (seconds) used while the AI has NO opponent locked, so it re-acquires promptly (e.g. a 1v1 opponent mid-respawn).")]
         [SerializeField] float playerReacquireInterval = 0.1f;
 
+        [Tooltip("Seconds between refreshes of the mass cluster the AI looks at while drifting " +
+                 "away from a lined-up crystal. The query is the cell's Burst density grid " +
+                 "(Cell.GetExplosionTarget - the same one aggression-1 fauna hunt with), so it is " +
+                 "sampled on this cadence and the cached point is flown at in between.")]
+        [SerializeField, Min(0.25f)] float massClusterRetargetInterval = 1.5f;
+
         /// <summary>
         /// Configure AI behavior at runtime (called after spawning for solo-play AI opponents).
         /// </summary>
@@ -110,6 +116,11 @@ namespace CosmicShore.Gameplay
         Vector3 _distance;
         bool LookingAtCrystal;
 
+        // Cached mass-cluster goal for the drift look-direction (see ResolveDriftLookDirection),
+        // refreshed on massClusterRetargetInterval so the Burst grid query is never per-frame.
+        Vector3 _massClusterPosition;
+        float _nextMassClusterSample;
+
         // Optional external steering hook. When set, the provider is sampled every
         // frame and overrides crystal/player seeking entirely. Used by game modes
         // that need bespoke AI objectives (e.g. Astro League ball striking).
@@ -204,6 +215,57 @@ namespace CosmicShore.Gameplay
                 _targetPosition = cellData.Cell.transform.position;
         }
 
+        /// <summary>
+        /// Where the AI POINTS while it drifts away from a crystal it has already lined up.
+        ///
+        /// <para>The drift is the interesting half of AI flight: <c>VesselStatus.Course</c> stays
+        /// locked on the crystal (so the vessel keeps travelling toward it) while the nose swings
+        /// somewhere else, which is how a drifting vessel lays trail, skims, and fires along an
+        /// axis that is not its heading. What it points AT is therefore a real decision, and it
+        /// used to be <c>-desiredDirection</c> — a flat 180° flip away from the objective, which
+        /// aims at nothing in particular and reads as the AI spinning on the spot.</para>
+        ///
+        /// <para>It now aims at a CLUSTER OF MASS, resolved through the cell's Burst density grid
+        /// (<see cref="Cell.GetExplosionTarget"/>) — the exact query aggression-1 fauna use to hunt
+        /// prey, so "go where the mass is" is one system on this platform rather than a per-mode
+        /// re-derivation. The grid is keyed so <c>GetExplosionTarget(myDomain)</c> returns the
+        /// densest region of mass HOSTILE to this pilot, and it already excludes nucleus-interior
+        /// and shielded mass — i.e. it can only ever point at mass the AI is allowed to attack.</para>
+        ///
+        /// <para>Falls back to the legacy flip when there is no cell, no mass to find, or the
+        /// cluster happens to lie in the same direction as the crystal (in which case the drift
+        /// would not turn the vessel at all).</para>
+        /// </summary>
+        Vector3 ResolveDriftLookDirection(Vector3 towardTarget)
+        {
+            return TryGetMassClusterDirection(towardTarget, out var towardMass)
+                ? towardMass
+                : -towardTarget;
+        }
+
+        bool TryGetMassClusterDirection(Vector3 towardTarget, out Vector3 direction)
+        {
+            direction = default;
+
+            var cell = cellData != null ? cellData.Cell : null;
+            if (cell == null) return false;
+
+            // Burst density query on a cadence; the cached point is flown at in between.
+            if (Time.time >= _nextMassClusterSample)
+            {
+                _nextMassClusterSample = Time.time + massClusterRetargetInterval;
+                _massClusterPosition = cell.GetExplosionTarget(VesselStatus.Domain);
+            }
+
+            var offset = _massClusterPosition - transform.position;
+            if (offset.sqrMagnitude < 1f) return false;
+
+            direction = offset.normalized;
+
```

</details>

### `b7299f402` — docs(claude): require every sound to be an exposed FMOD EventReference

_Claude, 2026-08-13 19:02:19 +0000_

```text
Adds an "Audio (FMOD)" convention section under Architecture Patterns:

- Every noise must be an inspector-exposed EventReference on the
  prefab/component (or SO) that makes it, editable in the component view
  without a code change.
- Never plug in a temp/borrowed event to make something audible — ship the
  field empty (EventReference.IsNull is a clean no-op) so an unwired slot
  reads as a visible TODO instead of an accidental shipped sound.
- Every ship ability gets its own dedicated event field (boost, gun fire,
  drift, shield, ...), not a shared GameplaySFXCategory. Documents the
  per-prefab-field vs central-category tiers and flags the existing
  BoostActionSO/DriftActionSO category calls as the legacy shape.
- Play through AudioSystem/FMODOneShotVolumeHelper, never
  RuntimeManager.PlayOneShot (no per-instance volume, ignores the SFX
  slider when the bus fails to resolve).

Also corrects the stale Wwise references: FMOD is the live middleware,
Assets/Wwise is inert with no first-party call sites. Updates the Tech
Stack entry, project-structure comments, the Key Systems audio row, and
adds matching bullets to Anti-Patterns and "Never Do".
```

```text
 CLAUDE.md | 90 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++----
 1 file changed, 86 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 143 lines)</summary>

```diff
diff --git a/CLAUDE.md b/CLAUDE.md
index bf79d3576..b989e817a 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -229,7 +229,7 @@ Do not snapshot domain at component-creation time. Either subscribe to `Player.N
 - **Camera**: Custom plain-transform rigs — `CustomCameraController` (gameplay) + `MainMenuCameraController`/`MenuCameraConfigSO` (menu) — with per-vessel `CameraSettingsSO` assets. Cinemachine 3.1.2 remains installed for tool scenes only (Recording Studio); the menu and gameplay cameras do not use it
 - **VFX**: VFX Graph 17.0.4, custom HLSL shaders, Shader Graph
 - **Input**: Unity Input System 1.14.2 with strategy pattern (`IInputStrategy` → platform-specific implementations)
-- **Audio**: Wwise integration
+- **Audio**: FMOD Studio (`Assets/Plugins/FMOD`, `FMODUnity`) — every sound is an inspector-exposed `EventReference`, never a hardcoded/temp event. See "Audio (FMOD)" under Architecture Patterns. (An `Assets/Wwise/` folder survives from an earlier middleware evaluation and is **inert** — no first-party code references `AkSoundEngine`; do not author new audio against it.)
 - **Haptics**: NiceVibrations for mobile/gamepad haptics. **Two everyday feels**, both local-human-pilot-only (skim-pulse reward + prism-punish thud), plus **one rare alert shake** fenced to match-changing events (only Ribcage's two progress-milestone rungs today); everything else is silent. See `Docs/HAPTICS.md`.
 - **Animation**: Timeline 1.8.9, DOTween for procedural animation
 - **DI**: Reflex (`com.gustavopsantos.reflex` 14.1.0) for dependency injection
@@ -269,7 +269,7 @@ Assets/
 │   │   ├── Instrumentation/   # AnalyticsServiceFacade (UGS Analytics, single writer)
 │   │   ├── Runtime/           # Dialogue runtime (DialogueManager, models, views, helpers)
 │   │   ├── RewindSystem/      # Rewind/replay functionality
-│   │   ├── Audio/             # Wwise audio management
+│   │   ├── Audio/             # AudioSystem (FMOD events + legacy music AudioSources)
 │   │   ├── LoadOut/           # Vessel loadout configuration
 │   │   ├── CallToAction/      # Promotional/CTA system
 │   │   ├── Squads/            # Squad management
@@ -310,7 +310,7 @@ Assets/
 ├── _Graphics/, _Models/, _Audio/, _Animations/
 ├── FTUE/                      # First-Time User Experience / Tutorial system
 ├── Plugins/                   # Obvious.Soap, Demigiant (DOTween), NativeShare, etc.
-├── Wwise/                     # Audio middleware
+├── Wwise/                     # Legacy middleware evaluation — INERT, no first-party refs (audio is FMOD, at Plugins/FMOD)
 ├── PlayFabSDK/                # Backend SDK (legacy)
 ├── NiceVibrations/            # Haptic feedback
 └── SerializeInterface/        # Custom [RequireInterface] attribute support
@@ -995,6 +995,85 @@ mass at the same speed with or without the spatial index. Detail: `Docs/SPATIAL_
 
 **Forcefield Crackle (Skimmer)**: `SkimmerForcefieldCracklePrismEffectSO` (at `_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/`) is a shader-driven alternative to `SkimmerFXPrismEffectSO` that visualizes the Skimmer's invisible sphere collider on prism impacts. It computes the impact point via `Collider.ClosestPoint` between the prism box and skimmer sphere, projects it onto the sphere surface, and forwards the event (position + duration + intensity + radius) to a `ForcefieldCrackleController` MonoBehaviour on the vessel (`_Scripts/Controller/Vessel/ForcefieldCrackleController.cs`). The controller owns all visual parameters (colors, arc density/sharpness, ring thickness, ripple speed, fresnel) as serialized fields and feeds a ring buffer of up to 16 simultaneous impacts to the shader via MaterialPropertyBlock arrays each frame. `[ExecuteAlways]` allows edit-mode preview via `ForcefieldCrackleControllerEditor` (at `_Scripts/Editor/`). The shader's custom-function HLSL file `ForcefieldCrackle.hlsl` (at `Assets/Materials/Graphs/`) uses FBM-based electrical arcs with expanding wavefronts on a geodesic distance metric so arcs follow the sphere's curvature. All three code files use the `CosmicShore.Gameplay` namespace.
 
+### Audio (FMOD) — every sound is an exposed, editable field (LOCKED convention)
+
+FMOD Studio is the audio middleware (`FMODUnity`, `Assets/Plugins/FMOD`). The rule below is not a
+style preference — it is what makes the game's audio *authorable by whoever owns audio*, without a
+programmer, a recompile, or a merge.
+
+> **Every noise anything makes must be an inspector-exposed `EventReference` on the prefab/component
+> (or SO) that makes it.** If a sound exists, an audio designer must be able to find it in the
+> component view of the thing that produces it, and swap it — without touching code, and without
+> hunting for which shared category it borrowed.
+
+**Corollaries — all three are load-bearing:**
+
+1. **Never plug in a "temp" event.** Do not point a new sound at a borrowed/placeholder FMOD event
+   just to hear something. Ship the `[SerializeField] EventReference` **empty** and let it be
+   silent — an empty slot is a visible, greppable TODO in the inspector; a temp event is an
+   invisible one that survives to release and gets mistaken for an intentional sound. FMOD's
+   `EventReference.IsNull` makes an empty slot a clean no-op, and `AudioSystem` already warns once
+   per unwired category (`warnOnUnwiredCategory`) rather than failing. Follow that pattern: check
+   `IsNull`, return, optionally warn once — never substitute another event.
+2. **Every ship ability gets its own dedicated FMOD event field** — boost, gun fire, drift, shield,
+   turret, missile, ability start/stop, whatever. One field per ability per distinct sound (a
+   start/stop or charge/release ability gets a field for each). Do **not** route a new ability
+   through an existing `GameplaySFXCategory` because it is "close enough" — sharing a category means
+   two abilities can never be tuned independently, which is exactly what the audio owner needs.
+3. **The sound is a trigger's payload, not an implicit side effect.** When something should sound on
+   contact, the collider/trigger that detects the contact is where the `EventReference` lives and is
+   played from. Same for a state change: the component that owns the state plays its own field.
+
+**How to add a sound (the shape to copy):**
+
+```csharp
+[Header("Audio")]
+[SerializeField, Tooltip("FMOD event played when this ability fires. Leave empty for silence.")]
+EventReference fireEvent;
+
+// at the trigger / state change:
+if (!fireEvent.IsNull)
+    audioSystem.PlaySFXEvent(fireEvent, transform.position);   // spatialized
+```
+
+Play through `AudioSystem` (`PlaySFXEvent` / `PlaySFXEventAttached`) or
+`FMODOneShotVolumeHelper` — **never** `RuntimeManager.PlayOneShot` directly, which has no
+per-instance volume and therefore ignores the SFX slider when the bus fails to resolve
+(`_Scripts/Controller/FX/FMODOneShotVolumeHelper.cs` documents why). For a **looping/continuous**
```

</details>

### `4d86b3771` — fix(projectiles): sweep the path for prism hits — bullets were missing 74% of it

_Claude, 2026-08-13 19:43:16 +0000_

```text
The report was "far too difficult to destroy all the prisms in a small area;
increasing the projectile radius fixes it, but giant bullets from a small
vessel is silly". The radius workaround was the diagnosis: the bullet was
missing most of its own flight path, and no fire rate or spread can compensate
for a weapon that is structurally blind between its samples.

A projectile is a TELEPORT, not a sweep. MoveProjectileAsync writes
position += Velocity*dt and PhysX samples the discrete trigger once per physics
step, so collisions are only ever tested at the points a round lands on:

  SPACE 0   375 u/s   6.25 u/frame @60fps   1.65 hit dia -> 26% of path tested
  SPACE 5  1875 u/s  31.25 u/frame                       ->  5%
  SPACE 10 3375 u/s  56.25 u/frame                       ->  3%

Halve the frame rate and it halves again. This also explains the collider
history: round 6 shrank the hit sphere 12 -> 1.65 on correct geometry, and
silently removed the accident that had been papering over the tunneling — a
12-diameter ball closes a 6.25 u step. It was load-bearing.

PrismSpatialIndex.QuerySegment is the swept counterpart of QuerySphere — the
fix SPARROW_TURRET_STANCE.md named in its follow-ups, and the one CLAUDE.md
requires (never Physics.OverlapSphere against prisms; new query shapes go on
the index). It is the effect of a huge bullet with none of the appearance.

Projectile.sweptPrismDetection (opt-in; on for the two Sparrow projectiles
only) makes the sweep the SOLE owner of prism contact — ProjectileImpactor
suppresses the trigger's prism case, so nothing double-dispatches. The trigger
was never a second chance; it is the thing that was missing 74%. The prism side
of that contact was already inert (its effect arrays are unserialized and
always null), so exactly one dispatch is removed and no behaviour is lost.

Hits dispatch nearest-first, which is what makes the sub-SPACE-5 "destroyed on
its first prism impact" rule mean the first prism ALONG THE PATH; and the round
is moved to each contact point before its impact fires, so effects and the
turret's anchor see where the shot actually met the prism. Dispatch reuses
ImpactorBase.AcceptImpacteeFromSweep, the exact analogue of the shell tier's
entry point.

Tuning as requested now that the rounds connect: growth 3.2 -> 1.0 deg/s, max
half-angle 4 -> 1.5 deg, firingRate 60 -> 90 (180 rounds/s). The cone is a
texture on the stream rather than a scatter. Pools resized; turret stance now
lays ~180 prisms/s and Dog Fight's 120-point target will likely need raising.

Vessels and mines still use the trigger and still tunnel — recorded as a
follow-up, deliberately not widened here.
```

```text
 Assets/_Prefabs/Projectile/SparrowProjectile.prefab                   |   1 +
 Assets/_Prefabs/Spacevessels/Sparrow.prefab                           |  24 ++---
 .../_Prefabs/Trails/Prisms With Pools/Sparrow Projectile Prism.prefab |   1 +
 Assets/_SO_Assets/VesselActions/Sparrow/FullAutoAction.asset          |   6 +-
 Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs    |  36 +++++++
 .../_Scripts/Controller/ImpactEffects/Impactors/ProjectileImpactor.cs |   7 ++
 Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs              |  82 ++++++++++++++++
 Assets/_Scripts/Controller/Projectiles/Projectile.cs                  | 160 ++++++++++++++++++++++++++++++++
 .../Controller/Vessel/R_VesselActions/SPARROW_SPRAY_ACCURACY.md       | 154 +++++++++++++++++++++++-------
 .../Controller/Vessel/R_VesselActions/SPARROW_TURRET_STANCE.md        |  31 ++++---
 Assets/_Scripts/Tests/Editor/PrismSweptQueryTests.cs                  |  90 ++++++++++++++++++
 Assets/_Scripts/Tests/Editor/PrismSweptQueryTests.cs.meta             |  11 +++
 Docs/SPATIAL_INDEX.md                                                 |   7 +-
 Docs/UNITY_VERIFICATION_CHECKLIST.md                                  |  72 ++++++++++----
 14 files changed, 599 insertions(+), 83 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 913 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
index d780a0ee0..2c0ba1e6e 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
@@ -79,6 +79,42 @@ namespace CosmicShore.Gameplay
         /// </summary>
         internal virtual void NotifyShellContactExit(PrismImpactor prismImpactor) { }
 
+        /// <summary>
+        /// True while <see cref="AcceptImpactee"/> is running for a SWEPT contact — one
+        /// found by querying the segment an object crossed this frame rather than by a
+        /// PhysX trigger overlap at its landing point. Lets a subclass suppress the
+        /// trigger path for a contact class the sweep has taken ownership of, without
+        /// suppressing the sweep's own dispatch of it.
+        /// </summary>
+        protected bool IsSweepDispatch { get; private set; }
+
+        /// <summary>
+        /// Swept-tier entry point, the exact analogue of
+        /// <see cref="AcceptImpacteeFromShellContact"/>: same isInitialized gate, same
+        /// profiler marker, same AcceptImpactee chain, with <see cref="IsSweepDispatch"/>
+        /// raised. A discrete trigger moved by transform writes only ever tests the points
+        /// it lands on; this is how the path BETWEEN them gets to land impacts too.
+        /// </summary>
+        internal void AcceptImpacteeFromSweep(IImpactor impactee)
+        {
+            if (!isInitialized)
+                return;
+
+            EnsureAcceptMarker();
+            using (_acceptMarker.Auto())
+            {
+                IsSweepDispatch = true;
+                try
+                {
+                    AcceptImpactee(impactee);
+                }
+                finally
+                {
+                    IsSweepDispatch = false;
+                }
+            }
+        }
+
         void EnsureAcceptMarker()
         {
             if (_acceptMarkerInit)
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ProjectileImpactor.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ProjectileImpactor.cs
index 853b93d9f..07b7cbc3a 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ProjectileImpactor.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ProjectileImpactor.cs
@@ -42,6 +42,13 @@ namespace CosmicShore.Gameplay
                     break;
                 
                 case PrismImpactor prismImpactee:
+                    // When this projectile sweeps, the swept segment query OWNS prism
+                    // contact and the PhysX trigger is suppressed for it. The trigger is
+                    // not a second chance — it samples one point per physics step, so at
+                    // these muzzle speeds it sees a few percent of the path — and letting
+                    // both run would double-dispatch every prism the sweep already found.
+                    if (Projectile.UsesSweptPrismDetection && !IsSweepDispatch)
+                        break;
                     if (Projectile.DisallowImpactOnPrism(prismImpactee.Prism))
                         break;
                     if(!DoesEffectExist(projectileImpactorDataContainer.ProjectilePrismEffects)) return;
diff --git a/Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs b/Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs
index 627853cad..a0f079f57 100644
--- a/Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs
+++ b/Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs
@@ -923,6 +923,88 @@ namespace CosmicShore.Gameplay
             return results.Count;
         }
 
+        /// <summary>
+        /// The SWEPT counterpart of <see cref="QuerySphere"/>: gathers every LIVE prism whose
+        /// centre lies within <paramref name="radius"/> of the SEGMENT
+        /// <paramref name="a"/>→<paramref name="b"/>, i.e. inside a capsule.
+        ///
+        /// This exists because a fast projectile is a **teleport, not a sweep**:
+        /// <c>Projectile.MoveProjectileAsync</c> advances the transform by
```

</details>

### `353305dd3` — docs(prisms): record the detonation gain's reach and fix two stale palette claims

_Claude, 2026-08-13 19:59:32 +0000_

```text
Ship-deep findings, written down at the site rather than left in a review:

- DetonationGain applies on the true-velocity (proportionalDebris) path as well
  as the legacy one. That is a deliberate, narrow deviation from "the vector IS
  the debris velocity" - a danger prism carries its own stored energy - and the
  alternative would make danger detonate harder on a hull ram but not on a
  Dolphin cone. Named at the method.
- dangerDetonationMultiplier is cached by PrismDebris.Configure and only
  re-read when the prefab reference changes (same as minSpeed/maxSpeed), so a
  play-mode edit is a no-op. Said so in the tooltip, where it gets tuned.
- PALETTE.md still claimed the danger pair is composed in ThemeManager, and its
  follow-up pointed a future DangerOutsideBlockColor at ThemeManager. Both now
  name SO_ColorSet.GetPrismKindColors (section 2.1).
```

```text
 Assets/_Scripts/Utility/Effects/PrismExplosion.cs | 13 ++++++++++++-
 Docs/PALETTE.md                                   |  8 +++++---
 2 files changed, 17 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Effects/PrismExplosion.cs b/Assets/_Scripts/Utility/Effects/PrismExplosion.cs
index 42857c4e9..9047795b4 100644
--- a/Assets/_Scripts/Utility/Effects/PrismExplosion.cs
+++ b/Assets/_Scripts/Utility/Effects/PrismExplosion.cs
@@ -29,7 +29,11 @@ namespace CosmicShore.Utility
                  "number, because on this contract debris speed and shatter rate are one quantity " +
                  "(see CLAUDE.md > AOE blast impulse); splitting them makes a blast that finishes " +
                  "shattering while the debris crawls, or the reverse. 1 = a danger prism dies " +
-                 "exactly like a plain one and only its palette differs.")]
+                 "exactly like a plain one and only its palette differs. TUNING NOTE: the batched " +
+                 "debris path caches this off the prefab in PrismDebris.Configure and only " +
+                 "re-reads it when the prefab reference itself changes (same as minSpeed/maxSpeed), " +
+                 "so edit it in edit mode - a play-mode edit will not take effect until the next " +
+                 "domain reload.")]
         [SerializeField]
         private float dangerDetonationMultiplier = 1.6f;
 
@@ -120,6 +124,13 @@ namespace CosmicShore.Utility
         /// ordinary mass coming apart. Shielded/super-shielded mass gets its own PALETTE
         /// (which is the part that was wrong) but plain dynamics; a shielded prism only
         /// ever explodes to a devastating hit, whose own force already carries that read.
+        ///
+        /// It applies on BOTH impulse paths — the legacy inertia gain and the true-velocity
+        /// one (<c>proportionalDebris</c>, where "the vector IS the debris velocity"). That
+        /// is a deliberate, narrow deviation from that contract: a danger prism carries its
+        /// own stored energy, so what leaves it is not only the impactor's momentum, and the
+        /// alternative — scaling only the legacy path — would make danger detonate harder
+        /// when a hull rams it but not when a Dolphin cone does, which reads as a bug.
         /// </summary>
         public static float DetonationGain(PrismKind kind, float dangerMultiplier) =>
             kind == PrismKind.Danger ? Mathf.Max(0.01f, dangerMultiplier) : 1f;
diff --git a/Docs/PALETTE.md b/Docs/PALETTE.md
index 9e6e60731..90b47efec 100644
--- a/Docs/PALETTE.md
+++ b/Docs/PALETTE.md
@@ -209,7 +209,8 @@ other two domains, whose bases are likewise muted mid-tones (`#5386B9`, `#9C71B7
 ### The danger tier borrows the shielded base
 
 A danger prism is painted from a **fourth** pair that has no fields of its own — it is
-composed in `ThemeManager` out of two existing colours:
+composed in `SO_ColorSet.GetPrismKindColors` (§2.1) out of two existing colours, and both
+the live material and the death debris read it from there:
 
 | | |
 |---|---|
@@ -389,8 +390,9 @@ Machine validation covers structure and colorimetry; only a playtest covers *loo
 - **The danger tier has no base fields of its own** — it borrows each domain's shielded
   base. That coupling is why Gold's danger separation (ΔE00 34.2) cannot be raised to
   Jade's (49.8) without moving the shared rim and distorting the other two domains. If the
-  tier ever needs per-domain control, adding `DangerOutsideBlockColor` to `SO_ColorSet` +
-  `ThemeManager` is the clean way, and it is a structural change, not a tune.
+  tier ever needs per-domain control, adding `DangerOutsideBlockColor` to `SO_ColorSet`
+  and reading it in `GetPrismKindColors` (§2.1) is the clean way — one edit, and the live
+  material and the death debris both follow. It is a structural change, not a tune.
 - **The unshielded tier is still not equalised across domains** (ΔL\* 32.2 / 27.1 / 32.0;
   rim `L*` 76.2 / 54.5 / 76.2). §4.2 brought Gold into the band rather than imposing a
   contract, because Ruby's dark rim is load-bearing for its look. If that tier is ever
```

</details>

### `86b0370d8` — fix(scoring): credit environment kills per-simulator, and by domain

_Claude, 2026-08-14 01:03:53 +0000_

```text
The 2-player symptom: the host scored off everything, the client could only
ever score off the other pilot's TRAIL - never off a cactus it flew through
and shattered.

Root cause, and it is platform-wide. StatsManager records prism destruction
server-only, on an assumption its own doc comments state twice: "a prism sits
at the same place on the server, so the server's own physics sees a client's
ram and records it." That holds for a TRAIL prism - laid from replicated
vessel motion, so both peers have one in the same place, which is exactly why
trail kills were the one thing that worked. It is false for flora and fauna,
and CellNetworkSync's class doc has said so all along: every peer runs its own
life spawner off local Random rolls and the populations diverge. The server's
copy of the cactus a client just shredded is somewhere else entirely, so
nothing was recorded for the whole living world.

- Player.ReportEnvironmentPrismDestroyed_ServerRpc - the third instance of the
  same owner-detects -> server-records round-trip as ReportFaunaKill_ServerRpc
  (fauna have no NetworkObject) and ReportCombatHit_ServerRpc (projectiles are
  not networked). Identity comes from RPC ownership, not a name string.
- StatsManager.OwnsAttacker splits who credits so nothing counts twice: the
  server credits only players it simulates (its own + every AI), and drops
  environment kills it observed a REMOTE player make. Rostered victims are
  untouched and stay server-recorded exactly as before.

Hostility is now COLOUR, not roster membership. The only test was the
owner-name comparison, and a cactus has no roster entry, so every prism in the
world was hostile to every player including the third of a forest wearing
their own colour. PrismStats carries the prism's OwnDomain and
StatsManager.IsFriendlyEnvironmentPrism applies to the world the rule trails
always had - your own colour is worth nothing - with Domains.Blue (the "no
team" sentinel) staying hostile to everyone so neutral structure still scores.
Ribcage rides the same metric and is unaffected in practice: its cage is
painted across the full triad plus Blue joints, so a team still reaches a 2000
target out of ~10,620 prisms.

Corollary found while chasing it: OmniCrystalImpactor.AcceptImpactee opens
with "if (IsNetworkClient()) return", so a collection resolves server-only for
every vessel - including one a remote client is flying. Collection SHOULD be
server-authoritative, but the effects of a pickup are what the pilot sees and
feels, and they were landing only on the server: a client's Dolphin collected
the crystal and the jaw blast, the spent energy meter and the elemental level
all happened on a machine that pilot was not looking at. Their meter never
emptied, no cone ever appeared, and - being the mode's only damage verb - they
had almost nothing to report above either.
CrystalManager.ReplayVesselCrystalEffects (no-op) -> NetworkCrystalManager's
targeted ClientRpc replays the same shared effect list on the vessel's OWNER.
Targeted, not broadcast: these effects mutate one vessel's state and spawn its
blast. The server keeps sole authority over collection, respawn and stats.

Rampage: nucleus halved to HalfNucleus.prefab (world radius 200 -> 100), which
halves the omni crystal's respawn volume with it since the two are coupled -
the platform's one sanctioned way to resize a Cell-owned visual, same move
Scurry makes. The three innermost flora bands are pulled in to match (the
runtime clamp still keeps every plant outside the core).
```

```text
 .../Cell Configs/Rampage Cell/Rampage Cacti Flora Config Data.asset   |   2 +-
 Assets/_SO_Assets/Cell Configs/Rampage Cell/Rampage Cell Config.asset |   2 +-
 .../Cell Configs/Rampage Cell/Rampage Coral Flora Config Data.asset   |   2 +-
 .../Cell Configs/Rampage Cell/Rampage Pine Flora Config Data.asset    |   2 +-
 Assets/_Scripts/Controller/Arcade/RAMPAGE.md                          |  59 +++++++++-----
 Assets/_Scripts/Controller/Environment/FlowField/CrystalManager.cs    |  14 ++++
 .../Controller/Environment/FlowField/NetworkCrystalManager.cs         |  36 +++++++++
 .../Controller/ImpactEffects/Impactors/OmniCrystalImpactor.cs         |  47 +++++++++--
 Assets/_Scripts/Controller/Managers/StatsManager.cs                   | 134 +++++++++++++++++++++++++++-----
 Assets/_Scripts/Controller/Player/Player.cs                           |  41 ++++++++++
 Assets/_Scripts/Controller/Vessel/Prism.cs                            |   1 +
 CLAUDE.md                                                             |   2 +-
 Docs/ECOSYSTEM.md                                                     |  67 ++++++++++++++++
 13 files changed, 355 insertions(+), 54 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 569 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/RAMPAGE.md b/Assets/_Scripts/Controller/Arcade/RAMPAGE.md
index 84267cc14..36177f85c 100644
--- a/Assets/_Scripts/Controller/Arcade/RAMPAGE.md
+++ b/Assets/_Scripts/Controller/Arcade/RAMPAGE.md
@@ -23,14 +23,16 @@ So a round reads: **graze the forest to charge → dive to the crystal in the nu
 aim back out at the thickest stand → fire.** See `DOLPHIN_ENERGY_ECONOMY.md` §1 for the
 economy itself; this file only arranges around it.
 
-- **Only hostile mass scores.** The metric is `IRoundStats.HostilePrismsDestroyed`.
-  "Hostile" means everything except your own team's **player-laid** mass: ALL
-  environment mass scores regardless of colour (flora and fauna carry non-roster
-  owner names — `DefaultPlayer`/`FaunaPrefab`/`flora` — so `StatsManager` classifies
-  their destruction hostile), and opponents' trails score; your own and your
-  teammates' trails never do (trails ARE rostered, so the domain check filters them).
-  Shattering your own trail is worthless *by construction*, so there is no
-  lay-and-smash farming loop — but every wild prism in the arena is fair game.
+- **Only hostile mass scores, and hostile means COLOUR.** The metric is
+  `IRoundStats.HostilePrismsDestroyed`. Anything wearing one of the two domains that
+  are not yours scores — **flora, fauna bodies, rival trails, laid structure, no
+  distinction** — and anything wearing your own colour scores nothing, whether it is
+  your teammate's trail or a cactus that happens to have grown Jade. Neutral
+  (`Domains.Blue`) mass is hostile to everyone and always scores. Since the forest
+  seeds uniformly across all three domains, roughly a third of it is worthless to you
+  at any moment, which makes reading colour part of choosing a target rather than
+  decoration. Shattering your own trail is worthless *by construction*, so there is
+  still no lay-and-smash farming loop.
 - **Destruction is the sanctioned mass sink.** The conserved-mass law says prisms are
   removed only by an *active* force — vessel abilities or fauna consumption. Rampage
   is that law played as a sport: every scoring act is a vessel ability consuming mass.
@@ -109,10 +111,15 @@ Dolphin blast / ram destroys a prism
       └─ SetupDestruction → onTrailBlockDestroyed.Raise(PrismStats{OwnName, Volume, AttackerName})
               │  (SOAP channel — StatsManager.prefab listener)
               ▼
-StatsManager.PrismDestroyed                        [server-only via _allowRecord]
-  ├─ attacker.BlocksDestroyed++ / TotalVolumeDestroyed += v
-  ├─ victim rostered + same domain? → Friendly… stats (NEVER scores: own/teammate trails)
-  └─ else (other domain OR environment) → HostilePrismsDestroyed++  (NetworkVariable → peers)
+StatsManager.PrismDestroyed
+  ├─ victim ROSTERED (a trail — exists on every peer) → server records, as always
+  │    same domain? → Friendly… stats (never scores)   else → HostilePrismsDestroyed++
+  └─ victim UNROSTERED (environment — flora/fauna/structure, per-peer positions)
+       ├─ credited by whoever SIMULATES the attacker (StatsManager.OwnsAttacker):
+       │    server for its own player + every AI; the owning client via
+       │    Player.ReportEnvironmentPrismDestroyed_ServerRpc for its own kills
+       └─ hostile iff the prism's colour is not the attacker's
+            (StatsManager.IsFriendlyEnvironmentPrism; Blue is hostile to all)
               │
               ▼
 ScoringMetrics.Read(stats, PrismsDestroyed) → SumByDomain
@@ -135,6 +142,14 @@ pilot's `HostilePrismsDestroyed`. (A blast constructed with a null vessel is *an
 and credits `🔥GuyFawkes🔥` instead — that is the failure mode to check first if a mode
 ever reports blasts scoring nothing.)
 
+**A client's kills count on the client's own screen.** Flora and fauna are spawned per-peer
+from local `Random` rolls, so the server's copy of a cactus is somewhere else entirely —
+recorded server-only, a client scored nothing for the entire living world and could only ever
+score off the other pilot's trail (which IS laid identically on both peers). Environment mass
+is now credited by whichever machine simulates the attacker, and the collecting pilot runs
+their own crystal effects so the blast exists on their machine at all. Full record:
+`Docs/ECOSYSTEM.md §27.8`–`§27.9`.
+
 ## The arena — a forest filling the cell, a clear nucleus
 
 `_SO_Assets/Cell Configs/Rampage Cell/`. Membrane radius **1200** (`CapsuleMembrane`),
@@ -148,11 +163,11 @@ is even through the whole volume rather than crowded onto one radius:
 
 | species | script | band | world radii | plants seeded | prisms/plant | leaf prism vol | scale/level |
 |---|---|---|---|---|---|---|---|
-| **Cacti** (hero) | `BranchingFlora` | 0.17–0.95 | 204–1140 | 26 | 160 | 5×5×3 = **75** | **1.30** |
+| **Cacti** (hero) | `BranchingFlora` | 0.10–0.95 | 120–1140 | 26 | 160 | 5×5×3 = **75** | **1.30** |
 | Spire | `PhyllotacticFlora` | 0.30–0.97 | 360–1164 | 10 | 190 | ~15 | 1.25 |
-| Pine | `BranchingFlora` | 0.20–0.90 | 240–1080 | 10 | 150 | 4×4×1 = 16 | 1.25 |
+| Pine | `BranchingFlora` | 0.14–0.90 | 168–1080 | 10 | 150 | 4×4×1 = 16 | 1.25 |
 | Rosette | `PhyllotacticFlora` | 0.40–0.96 | 480–1152 | 7 | 170 | ~17 | 1.25 |
-| Coral | `PhyllotacticFlora` | 0.17–0.80 | 204–960 | 6 | 180 | ~10.6 | 1.25 |
+| Coral | `PhyllotacticFlora` | 0.10–0.80 | 120–960 | 6 | 180 | ~10.6 | 1.25 |
 
 Seeded total ≈ **9,830 prisms** across 59 plants, and planting continues past the seed
 batch until the cell tops out (below).
@@ -165,7 +180,7 @@ real handful sit in close.
```

</details>

### `0aade737d` — feat(dolphin): hold velocity magnitude for the duration of a drift

_Claude, 2026-08-14 01:29:35 +0000_

```text
The Dolphin's drift already locked the velocity's DIRECTION (driftDamping 0
stops MoveShip re-pointing Course at transform.forward), but its MAGNITUDE
kept tracking the throttle every frame, so the slide stretched and shrank
under a locked heading.

Add VesselTransformer.holdSpeedWhileDrifting (authored on for the Dolphin,
off for every other vessel): BeginDrift latches the smoothed cruise speed on
the rising edge of the hold and AdvanceSpeed pins speed to it until EndDrift
releases on the trigger release. The throttle target is still computed, it
just never reaches speed - which is what disabling the throttle for the drift
means mechanically. The pin lives in AdvanceSpeed, the one path every
transformer's MoveShip runs through, so SingleStickVesselTransformer's
overridden target is covered without knowing drift exists.

Deliberately outside the hold: throttleMultiplier (a danger prism still slows
a drifting vessel), velocityShift, and _speedTrackingRate (a ramp boost
resumes on release). The manual-throttle channel is silenced alongside the
target, with its value at capture folded into the held speed.
```

```text
 Assets/_Prefabs/Spacevessels/Dolphin.prefab                           |  2 +
 .../Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md       | 55 +++++++++++++++++++++++++
 Assets/_Scripts/Controller/Vessel/SingleStickVesselTransformer.cs     |  4 +-
 Assets/_Scripts/Controller/Vessel/VesselTransformer.cs                | 72 ++++++++++++++++++++++++++++++++-
 Docs/UNITY_VERIFICATION_CHECKLIST.md                                  | 40 ++++++++++++++++++
 5 files changed, 171 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 271 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md b/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md
index 62c06d9c5..b37bf53ff 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md
@@ -155,6 +155,57 @@ discharge, and cancelling that task only throws *inside* the loop — it never r
 that restores the speed. Without the clear, anyone who drifted twice in a row kept a partial
 boost multiplier permanently.
 
+### The drift is a momentum-preserving slide — the whole velocity is frozen, not just its direction
+
+The Dolphin authors `driftDamping: 0` (`DolphinDriftAction.asset`), so its drift already froze the
+velocity's **direction**: `MoveShip` stops re-pointing `Course` at `transform.forward` and flies the
+heading the vessel carried in while the hull rotates freely on top of it. Its **magnitude** kept
+moving, though — `AdvanceSpeed` went on tracking `ComputeThrottleTarget()` every frame, so the
+throttle stick (and any boost state change) still stretched and shrank the slide underneath the
+locked heading. Half a lock reads as a bug, not a mechanic.
+
+`VesselTransformer.holdSpeedWhileDrifting` (authored **on** for the Dolphin, off for every other
+vessel) closes the other half:
+
+| | before | now |
+|---|---|---|
+| velocity direction | locked at drift start (`driftDamping: 0`) | unchanged |
+| velocity magnitude | throttle-driven, live | **latched at drift start, held for the drift** |
+| throttle during drift | drives speed | **inert** — the target is still computed, it just never reaches `speed` |
+
+Mechanically: `BeginDrift` → `RefreshDriftSpeedHold()` latches the current smoothed cruise `speed`
+on the **rising edge** of the hold, and `AdvanceSpeed` pins `speed` to that value until `EndDrift`
+releases it. The pin sits in `AdvanceSpeed` rather than in `ComputeThrottleTarget` because
+`AdvanceSpeed` is the one path *every* transformer's `MoveShip` runs through — a subclass that
+overrides the target (`SingleStickVesselTransformer`) is covered without knowing drift exists.
+
+Four things are deliberately **outside** the hold:
+
+- **`throttleMultiplier`** (the `ModifyThrottle` channel) stays live, so a danger prism's full-stop
+  slow bites a drifting Dolphin exactly as hard as a flying one. Danger prisms are not safe to
+  anybody (locked design) and a drift is not a shield.
+- **`velocityShift`** (the `ModifyVelocity` channel) stays live — knockback, dodges and AOE impulses
+  still displace a drifting vessel.
+- **`_speedTrackingRate`** is untouched, so a ramp boost mid-ramp resumes on release instead of
+  being silently swallowed by the pinned value.
+- **The release**, not the ease-out. `EndDrift` hands the throttle back the instant the pilot lets
+  go — the same instant `BeginDischarge` starts, which has to be able to accelerate immediately.
+  (The non-gamepad course ease-out keeps easing after that; only the speed unlocks early.)
+
+The hold is **binary**, while the course lock is analog (`driftAmount = clamp01(triggerSum)`): on a
+gamepad the speed latches the moment the left trigger crosses the deadzone, at which point the
+course is only fractionally locked. That is the deliberate simple reading of "lock the magnitude";
+if a feathered trigger ends up wanting a feathered lock, the blend point is
+`RefreshDriftSpeedHold` → `AdvanceSpeed` (`Lerp(target, held, driftAmount)`), not a new field.
+
+**Known consequence — the drift now carries boost speed.** `BeginCharge` kills `BoostMultiplier` /
+`IsBoosting` at the top of every drift, so before this change re-drifting during a discharge bled
+the boost speed away over the next second. Now that speed is what gets latched: drift → release →
+re-drift *at the peak of the discharge* pins the vessel near **357** for as long as the drift is
+held, while banking the next boost. If that reads as a ratchet in play, the fix is a ceiling on the
+captured value (clamp `_heldDriftSpeed` to the unboosted cruise target, 78), not the removal of the
+hold — but it is a real balance change and wants a play-test before it is decided.
+
 ---
 
 ## 3. The hull reads out the blast
@@ -321,6 +372,10 @@ Play Menu_Main, enter freestyle on the Dolphin.
 | hit a crystal | blast fires, gape snaps back to the 4.76° rest, Space icon flashes with a prism count |
 | blast at full energy | destruction is a FAN — wide across the jaw plane, narrow across the beam |
 | full throttle, no boost | `VesselStatus.Speed` settles at **78** (was 60) |
+| drift at cruise, then work the throttle stick | speed does **not** move — heading swings, magnitude is pinned at the value it had when the drift began |
+| drift from a slow crawl | it stays a slow crawl for the whole drift (the lock is "hold what you had", not "hold top speed") |
+| release the drift | throttle authority returns immediately and speed resumes tracking (into the boost discharge) |
+| ram a danger prism mid-drift | the vessel still slows — `throttleMultiplier` is outside the hold |
 | hold drift | boost ring steps up; release → speed rises then decays; ring empties |
 | hold drift from empty to full | ring fills in **~3.6 s** (was 4) |
 | release a full meter | speed peaks near **357** and takes **~2.5 s** to fall back (was 210 / 2 s) |
diff --git a/Assets/_Scripts/Controller/Vessel/SingleStickVesselTransformer.cs b/Assets/_Scripts/Controller/Vessel/SingleStickVesselTransformer.cs
index ab8a1a479..0928bd2de 100644
--- a/Assets/_Scripts/Controller/Vessel/SingleStickVesselTransformer.cs
+++ b/Assets/_Scripts/Controller/Vessel/SingleStickVesselTransformer.cs
@@ -71,7 +71,9 @@ namespace CosmicShore.Gameplay
             // saturates every sub-1 modifier to a near-stop.
             float effectiveSpeed = speed * throttleMultiplier;
```

</details>

### `e55358d0b` — fix(rampage): objective arrow tracks only the managed omni crystal

_Claude, 2026-08-14 01:46:33 +0000_

```text
Crystal.Active holds every live crystal on the machine, and in this arena most
of them are not the objective: every flora and fauna carries a heart and drops
it on death, and the mode's whole verb is killing flora - so the arena rains
elemental crystals continuously - plus every team crystal a Dolphin seeds on
its 30s Charge cooldown. A nearest-live-crystal scan therefore spent the match
swinging onto whichever cactus just died, which is worse than no arrow: it
points the pilot away from the thing they are racing for.

Filter on Crystal.CrystalManager, non-null only for a crystal the cell's
CrystalManager spawned (SpawnWithDomain -> InjectDependencies is its single
writer). Hearts and seeded crystals are plain Instantiates and carry none, so
one test separates them all - and it is the same test that means "this is the
crystal that respawns inside the nucleus forever". CanBeCollected follows it so
a future variant spawning per-domain managed crystals still only ever names one
this pilot may take.

Also stops blanking the arrow on Crystal.IsExploding: that flag stays true for
0.5s AFTER the respawn has already repositioned the crystal, so honouring it
hid the arrow for half a second while the crystal sat at exactly the place it
was pointing to. A collection does not invalidate the target at all - the
manager moves the same Crystal object, so the cached transform follows it home.
```

```text
 Assets/_Scripts/Controller/Arcade/RAMPAGE.md                  |  8 ++++-
 Assets/_Scripts/Controller/Arcade/RampageObjectiveProvider.cs | 75 +++++++++++++++++++++++++++++------------
 Docs/ECOSYSTEM.md                                             | 21 ++++++++++++
 3 files changed, 81 insertions(+), 23 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 168 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/RAMPAGE.md b/Assets/_Scripts/Controller/Arcade/RAMPAGE.md
index 36177f85c..e0be9e9f9 100644
--- a/Assets/_Scripts/Controller/Arcade/RAMPAGE.md
+++ b/Assets/_Scripts/Controller/Arcade/RAMPAGE.md
@@ -64,7 +64,13 @@ economy itself; this file only arranges around it.
 - **Domains**: free-for-all like Scurry (`MinDomainsAllowed`/`MaxDomainsAllowed`
   defaults 1/3); players 1–4 with AI backfill
 - **Vessels**: **Dolphin only** — see "Why Dolphin-only" below
-- **Objective arrow**: `RampageObjectiveProvider` — points at the contested crystal
+- **Objective arrow**: `RampageObjectiveProvider` — points at the contested omni crystal and
+  **nothing else, ever**. The filter is the point: `Crystal.Active` also holds every
+  lifeform heart the food web is constantly dropping (this mode's whole verb is killing
+  flora) and every team crystal a Dolphin seeds, so a nearest-live-crystal scan would
+  spend the match swinging onto whichever cactus just died. Only a MANAGER-SPAWNED
+  crystal (`Crystal.CrystalManager != null`, set solely by `CrystalManager.SpawnWithDomain`)
+  is the arena's; hearts and seeded crystals are plain `Instantiate`s and carry no manager.
 - **Config**: `_SO_Assets/Games/ArcadeGameRampage.asset` (registered in
   `GameLists/OrganicRematchGames.asset` + the pre-existing arcade lists)
 
diff --git a/Assets/_Scripts/Controller/Arcade/RampageObjectiveProvider.cs b/Assets/_Scripts/Controller/Arcade/RampageObjectiveProvider.cs
index 9cd23681b..8a3eb1502 100644
--- a/Assets/_Scripts/Controller/Arcade/RampageObjectiveProvider.cs
+++ b/Assets/_Scripts/Controller/Arcade/RampageObjectiveProvider.cs
@@ -1,3 +1,4 @@
+using CosmicShore.Data;
 using CosmicShore.UI;
 using CosmicShore.Utility;
 using Reflex.Attributes;
@@ -7,7 +8,8 @@ using UnityEngine;
 namespace CosmicShore.Gameplay
 {
     /// <summary>
-    /// Objective provider for Rampage: the arena's single contested crystal.
+    /// Objective provider for Rampage: the arena's single contested OMNI crystal, and nothing
+    /// else, ever.
     ///
     /// Rampage is the one mode where the crystal is not a pickup but a TRIGGER - a Dolphin banks
     /// skim energy in the forest and the crystal is the only thing that discharges it as the jaw
@@ -15,17 +17,37 @@ namespace CosmicShore.Gameplay
     /// right now" is the question the whole match is played around, and it is the one thing a
     /// pilot deep in a cactus thicket cannot answer by looking.
     ///
-    /// Deliberately NOT <see cref="HexRaceObjectiveProvider"/>, which filters to crystals in the
-    /// local player's own domain. HexRace gives every player their own crystal; Rampage spawns
-    /// ONE neutral crystal (<c>spawnCrystalWithPlayerDomain: 0</c> ⇒ <c>Domains.Blue</c>) that
-    /// everybody may collect, so a domain filter here would reject the only objective in the
-    /// match and the arrow would never appear at all.
+    /// <para><b>Why the filter is the whole point.</b> <see cref="Crystal.Active"/> holds EVERY
+    /// live crystal, and this arena is full of ones that are not the objective:</para>
+    /// <list type="bullet">
+    ///   <item><b>Lifeform hearts.</b> Every flora and fauna carries one and drops it on death
+    ///   (the every-lifeform-drops-a-crystal invariant), and this mode's whole verb is killing
+    ///   flora - so the arena is constantly raining elemental crystals.</item>
+    ///   <item><b>Seeded TEAM crystals.</b> The Dolphin's Charge ability plants one every 30 s,
+    ///   and only its own domain may collect it.</item>
+    /// </list>
+    /// <para>A nearest-live-crystal scan would therefore spend the match swinging onto whatever
+    /// cactus just died two hundred units away, which is worse than no arrow: it actively points
+    /// the pilot away from the thing they are racing for.</para>
     ///
-    /// Event-driven, same shape as the HexRace provider: the scan runs on demand (initial call +
-    /// each <see cref="ElementalCrystalImpactor.OnCrystalCollected"/> + whenever the cached
-    /// target goes null or starts exploding), steady-state <see cref="TryGetObjective"/> is an
-    /// O(1) cache read, and a recompute walks the in-memory <see cref="Crystal.Active"/> registry
-    /// - never a FindObjectsByType scene scan, never a per-frame allocation.
+    /// <para>The discriminator is <see cref="Crystal.CrystalManager"/>, set ONLY by
+    /// <see cref="CrystalManager.SpawnWithDomain"/>. A manager-spawned crystal is the arena's -
+    /// it respawns inside the nucleus forever and is replicated to every peer. Hearts and seeded
+    /// crystals are plain <c>Instantiate</c>s and carry no manager, so one test separates them
+    /// all. <see cref="Crystal.CanBeCollected"/> is then applied so that if a future variant ever
+    /// spawns per-domain managed crystals (as HexRace does), the arrow still only names one this
+    /// pilot may actually take.</para>
+    ///
+    /// Deliberately NOT <see cref="HexRaceObjectiveProvider"/>, which filters to the local
+    /// player's own DOMAIN: Rampage's crystal is neutral (<c>spawnCrystalWithPlayerDomain: 0</c> ⇒
+    /// <c>Domains.Blue</c>) so a strict domain-equality test would reject the only objective in
+    /// the match and the arrow would never appear at all.
+    ///
+    /// Event-driven: the scan runs on demand (initial call + each
+    /// <see cref="ElementalCrystalImpactor.OnCrystalCollected"/> + whenever the cached target goes
```

</details>

### `6ea08d993` — docs(dolphin): sharpen the re-drift verification row for the speed hold

_Claude, 2026-08-14 02:29:34 +0000_

```text
The 'drift, release, drift again' check read 'speed returns to normal',
which the velocity hold makes ambiguous - the second drift now latches
whatever the discharge had reached. State what the row actually tests:
nothing is stuck once you stop drifting.
```

```text
 Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md b/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md
index b37bf53ff..64ab3e954 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md
@@ -380,7 +380,7 @@ Play Menu_Main, enter freestyle on the Dolphin.
 | hold drift from empty to full | ring fills in **~3.6 s** (was 4) |
 | release a full meter | speed peaks near **357** and takes **~2.5 s** to fall back (was 210 / 2 s) |
 | fly straight without drifting | ring does **not** climb |
-| drift, release, drift again | speed returns to normal — no stuck multiplier |
+| drift, release, drift again, then release and fly straight | speed settles back to the ordinary 78 cruise — no stuck boost multiplier. (Note the second drift now HOLDS whatever the discharge had reached; the thing under test is that nothing is stuck once you stop drifting.) |
 | Charge to level 5 | second crystal pip appears; two crystals plantable back to back |
 
 The **vessel silhouette** that used to sit in this HUD is gone — it had been dead since its driver
```

</details>

### `fd717485e` — docs(skills): record the flight-model choke point and the freeze-side writer rule

_Claude, 2026-08-14 02:29:35 +0000_

```text
Two lessons from the Dolphin drift velocity hold, into the vessel skill:

- New rule 16: intervene at VesselTransformer.AdvanceSpeed, not
  ComputeThrottleTarget. Two transformers override the target (the
  single-stick one is what the Sparrow and Serpent run), so a change
  written there reaches only the class you edited. Names the two
  companions of `speed` a naive edit misses - the second manual-throttle
  channel and the latched ramp rate.
- Rule 12 gains its freeze-side clause: enumerating every writer is
  required to FREEZE a quantity too, and the answer is per-writer -
  freezing the impact-slow channel alongside the throttle would have made
  a drifting vessel immune to danger prisms.
```

```text
 .claude/skills/vessel/SKILL.md | 23 +++++++++++++++++++++--
 1 file changed, 21 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/.claude/skills/vessel/SKILL.md b/.claude/skills/vessel/SKILL.md
index fbda04de5..9449dd63e 100644
--- a/.claude/skills/vessel/SKILL.md
+++ b/.claude/skills/vessel/SKILL.md
@@ -103,7 +103,7 @@ un-implemented until Garrett marks them up. If your task requires a mapping that
 STOP and ask (AskUserQuestion), presenting the FLEET_MAPS proposal for that row. The same gate
 applies to new abilities, new resources on the meter list, and anything that adds a fundamental.
 
-## 4. Implement — the fifteen rules that keep getting relearned
+## 4. Implement — the sixteen rules that keep getting relearned
 
 1. **Ability SOs are shared and stateless.** Per-vessel state lives in executors / vessel-root
    MonoBehaviours; SOs receive `(registry, status)` per call. Never bind state to an SO asset.
@@ -153,7 +153,13 @@ applies to new abilities, new resources on the meter list, and anything that add
     an executor's own cooldown can block its path entirely — so deleting the passive trickle
     "because gain should come from the ability" left the Dolphin's boost with no working fill
     path at all. The trickle and `rechargeCooldownSeconds` had to move together. Grep every
-    writer, then change the set.
+    writer, then change the set. **The same enumeration is required to FREEZE a quantity**, and
+    there the answer is per-writer rather than all-or-nothing: the Dolphin's drift speed hold
+    pins the throttle-derived cruise `speed` but deliberately leaves `throttleMultiplier`
+    (impact slows) and `velocityShift` (knockback/AOE) live — freezing those too would have
+    quietly made a drifting vessel immune to danger prisms, which is a LOCKED-design violation
+    hiding inside a feel change. List every writer, then say per writer whether the freeze
+    covers it, and record that list in the doc.
 13. **A cancelled UniTask never runs its tail.** `catch (OperationCanceledException) { }` means
     any status the routine set *before* its loop stays set forever. Interrupting a discharge left
     `BoostMultiplier`/`IsBoosting` frozen — a permanent free speed bonus. Restore that state in
@@ -176,6 +182,19 @@ applies to new abilities, new resources on the meter list, and anything that add
     plus a transition); a partial fill on a pip reads as a meter and reopens the question you just
     closed. Drive it from a sibling image, never the ability icon itself, or you collide with the
     four-icon upgrade tint/badge (rule 9).
+16. **Intervene in the flight model at `VesselTransformer.AdvanceSpeed`, not at
+    `ComputeThrottleTarget`.** Four transformers exist (`VesselTransformer`,
+    `SingleStickVesselTransformer` — what the Sparrow and Serpent actually run —
+    `GunVesselTransformer`, `CommandVesselTransformer`) and the first two carry their own
+    `MoveShip` AND their own `ComputeThrottleTarget`, so a change written into the target reaches
+    only the vessels running the class you edited (the single-stick override ignores `XDiff` and
+    the throttle-scaler multiplier entirely). `AdvanceSpeed` is the one line both `MoveShip`s call
+    — the choke point where anything that must hold for EVERY vessel belongs, and where the
+    Dolphin's drift speed hold sits. Two companions of `speed` need the same treatment when you
+    touch it: the `toggleManualThrottle` lerp is a SECOND throttle channel living in each
+    `MoveShip` (no shipped prefab enables it — check before assuming your change covered it), and
+    `_speedTrackingRate` is a latched ramp state (the Rhino's ramp boost) that a naive early-return
+    can silently consume.
 
 ## 5. Audit, then hand back verification (you cannot run Unity; the human is the gate)
 
```

</details>

### `9411ef1d1` — feat(rampage): four intensities, and fix the sticky cell-config race

_Claude, 2026-08-14 03:22:05 +0000_

```text
Rampage's analogue of Ribcage's "shells added inward from a fixed outer
radius" is the forest THICKENING inside a fixed shell. Intensity moves forest
mass and nothing else: membrane, nucleus, crystal, prism target and fauna are
one constant at all four levels, so only the arena's density changes.

  I1  30 plants / 3,500 prisms / ~569k volume
  I2  41 plants / 5,464 prisms / ~896k volume
  I3  51 plants / 7,650 prisms / ~1.24M volume
  I4  59 plants / 9,830 prisms / ~1.62M volume   <- today's shipped arena

The ladder runs DOWN from intensity 4 on purpose: that is the arena that has
actually been played, and Rampage already sits at 2.8x the Blob collider
envelope as documented headroom, so scaling up would put the top intensity
somewhere nobody has measured. Net collider impact is zero at the top and
strictly negative below it - and since ProgressionConfig caps a fresh account
at intensity 2, the arena most players meet drops from 9,830 to 5,464 prisms.

Authored the platform way: CellTypeChoiceOptions.IntensityWise over four
CellConfigDataSOs (list order = intensity), each with its own PhaseThresholds
and its own SpawnProfileSO. The profiles differ in exactly two new fields.

New general capability - SpawnProfileSO.FloraPopulationScale and
FloraPlantBudgetScale. A SpawnProfile is referenced FROM a CellConfig, so it
already forks per intensity for free; scaling there lets the per-species assets
keep owning what each plant IS while the cell owns how much arena there is.
Forking the five flora configs four ways would have been 20 assets whose only
deltas are two integers each. Three rules, each load-bearing: applied in BOTH
spawners (IntensityWise swaps RandomLifeSpawner for IntensityWiseLifeSpawner,
so a one-sided scalar is dead code in the very modes that need it); the budget
scalar rides the existing Flora.ApplyVariantTuning path as a MULTIPLIER (the
three flora families ship budgets an order of magnitude apart); and rounding is
explicit half-up, since Mathf.RoundToInt is banker's rounding and would send an
authored 10 x 0.85 to 8 on one species and 9 on the next.

The ladders are GENERATED, not hand-authored. Tools/Build/rampage_intensity.py
computes each intensity's prism count and full-grown volume from the same
numbers the game reads, derives the eight thresholds, emits all eight assets,
and self-tests by reproducing intensity 4's shipped ladder to the digit.

Platform bug fixed on the way, and it was ALREADY LIVE in every IntensityWise
scene (Dog Fight, Ribcage, Wildlife Liberation, both Wildlife Blitz cells):
Cell.AssignConfig is sticky by design, its IntensityWise arm reads
SelectedIntensity, and that value reaches a client ONLY in the config ClientRpc
- but a client's cell bootstraps off its FIRST CRYSTAL (~400ms) rather than
OnInitializeGame (1000ms). Lose that race and the SOAP default 0 clamps to
index 0: the client builds intensity 1's arena while the host builds the chosen
one, for the whole match, silently. Fixed with GameDataSO.GameConfigSynced
gating the choice, AssignConfig returning WITHOUT latching when a client cannot
yet know, and - the part that makes it safe - a retryable deferral:
InitilizePostFirstCellItem used to latch postInitilized on its first line, so a
deferred bootstrap would have left the cell with no cytoplasm and no spawner at
all. OnInitializeGame fires on every peer, so the retry always lands.

Also corrects four stale numbers in RAMPAGE.md (nucleus 200 -> 100, "136
seeded flora" -> 59, "~700-radius" spawn ring -> 600, and a reference to
RampageController.arenaCell, a field that no longer exists).
```

```text
 .../Cell Configs/Rampage Cell/Rampage Cell Config 2.asset.meta        |   8 +
 .../_SO_Assets/Cell Configs/Rampage Cell/Rampage Cell Config 3.asset  |  42 ++++
 .../Cell Configs/Rampage Cell/Rampage Cell Config 3.asset.meta        |   8 +
 .../{Rampage Cell Config.asset => Rampage Cell Config 4.asset}        |  18 +-
 ...ampage Cell Config.asset.meta => Rampage Cell Config 4.asset.meta} |   0
 .../{Rampage Spawn Profile.asset => Rampage Spawn Profile 1.asset}    |   4 +-
 .../Cell Configs/Rampage Cell/Rampage Spawn Profile 1.asset.meta      |   8 +
 .../Cell Configs/Rampage Cell/Rampage Spawn Profile 2.asset           |  36 ++++
 .../Cell Configs/Rampage Cell/Rampage Spawn Profile 2.asset.meta      |   8 +
 .../Cell Configs/Rampage Cell/Rampage Spawn Profile 3.asset           |  36 ++++
 .../Cell Configs/Rampage Cell/Rampage Spawn Profile 3.asset.meta      |   8 +
 .../Cell Configs/Rampage Cell/Rampage Spawn Profile 4.asset           |  36 ++++
 ...ge Spawn Profile.asset.meta => Rampage Spawn Profile 4.asset.meta} |   0
 Assets/_Scenes/Multiplayer Scenes/MinigameRampage.unity               |   5 +-
 .../_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs   |  11 +
 Assets/_Scripts/Controller/Arcade/RAMPAGE.md                          |  88 +++++++-
 Assets/_Scripts/Controller/Environment/Cell.cs                        |  82 +++++++-
 Assets/_Scripts/Controller/Environment/CellLifeSpawnerBase.cs         |   9 +-
 .../_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs   |   6 +
 .../_Scripts/Controller/Environment/FloraAndFauna/BranchingFlora.cs   |   6 +
 .../Controller/Environment/FloraAndFauna/PhyllotacticFlora.cs         |   6 +
 Assets/_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs    |   6 +
 Assets/_Scripts/Controller/Environment/RandomLifeSpawner.cs           |   6 +
 Assets/_Scripts/Utility/DataContainers/FloraConfigurationSO.cs        |  41 ++++
 Assets/_Scripts/Utility/DataContainers/GameDataSO.cs                  |  21 ++
 Assets/_Scripts/Utility/DataContainers/SpawnProfileSO.cs              |  22 ++
 CLAUDE.md                                                             |   2 +-
 Docs/ECOSYSTEM.md                                                     |  82 ++++++++
 Tools/Build/rampage_intensity.py                                      | 349 ++++++++++++++++++++++++++++++++
 32 files changed, 1024 insertions(+), 22 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1009 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs b/Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs
index b054e67ef..1f2740127 100644
--- a/Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs
+++ b/Assets/_Scripts/Controller/Arcade/MultiplayerMiniGameControllerBase.cs
@@ -42,6 +42,11 @@ namespace CosmicShore.Gameplay
 
                 StampMatchEnvelope();
 
+                // The server IS the authority, so its config is synced by definition. Set before
+                // the broadcast: Cell.AssignConfig gates its (sticky) IntensityWise choice on this
+                // flag, and on the host that choice can happen at any point after this frame.
+                gameData.GameConfigSynced = true;
+
                 // Sync game config to all clients now that we're in the game scene.
                 // Previously this was done by SceneLoader via ClientRpc before scene load,
                 // but SceneLoader is now a plain MonoBehaviour (no RPCs).
@@ -572,6 +577,12 @@ namespace CosmicShore.Gameplay
             LoadInsights.SetGameContext(
                 sceneName, ((GameModes)gameMode).ToString(), intensity, playerCount,
                 Mathf.Max(0, playerCount - aiBackfillCount), aiBackfillCount, isMultiplayer);
+
+            // LAST: everything above is now authoritative on this client. Cell.AssignConfig
+            // refuses to make its sticky IntensityWise choice until this is true, because the
+            // intensity it reads arrives in this very RPC and a cell that latched before it would
+            // build a different arena than the host for the whole match.
+            gameData.GameConfigSynced = true;
         }
     }
 }
\ No newline at end of file
diff --git a/Assets/_Scripts/Controller/Arcade/RAMPAGE.md b/Assets/_Scripts/Controller/Arcade/RAMPAGE.md
index e0be9e9f9..93a9941ff 100644
--- a/Assets/_Scripts/Controller/Arcade/RAMPAGE.md
+++ b/Assets/_Scripts/Controller/Arcade/RAMPAGE.md
@@ -63,6 +63,8 @@ economy itself; this file only arranges around it.
   `rule.IsObjectiveReached`
 - **Domains**: free-for-all like Scurry (`MinDomainsAllowed`/`MaxDomainsAllowed`
   defaults 1/3); players 1–4 with AI backfill
+- **Intensity**: **4 levels**, `CellTypeChoiceOptions.IntensityWise` over four cell configs —
+  the forest thickens from 3,500 to 9,830 seeded prisms. See "Four intensities" below.
 - **Vessels**: **Dolphin only** — see "Why Dolphin-only" below
 - **Objective arrow**: `RampageObjectiveProvider` — points at the contested omni crystal and
   **nothing else, ever**. The filter is the point: `Crystal.Active` also holds every
@@ -156,10 +158,77 @@ is now credited by whichever machine simulates the attacker, and the collecting
 their own crystal effects so the blast exists on their machine at all. Full record:
 `Docs/ECOSYSTEM.md §27.8`–`§27.9`.
 
+## Four intensities — the forest thickens inside a fixed shell
+
+Ribcage's intensity adds rinds inward from a fixed outer radius; Rampage's **thickens the
+forest inside a fixed shell**. Intensity moves forest mass and nothing else — every number
+that defines the arena's silhouette and its rules is one constant at all four levels:
+
+| held constant at every intensity | value |
+|---|---|
+| `MembranePrefab` | `CapsuleMembrane`, r **1200** |
+| `NucleusPrefab` | `HalfNucleus`, world r **100** — and therefore the crystal respawn volume, the flora band's inner clamp, and the 600 spawn ring |
+| crystal | `fixedCrystalCount: 1`, neutral |
+| prism target | 2000 — vary the arena, not the finish line |
+| fauna | Blob tadpole + shark, unforked |
+| the five flora species assets | unforked |
+
+| | I1 | I2 | I3 | I4 |
+|---|---|---|---|---|
+| seeded plants | 30 | 41 | 51 | **59** |
+| seeded prisms | 3,500 | 5,464 | 7,650 | **9,830** |
+| full-grown volume | ~569k | ~896k | ~1.24M | **~1.62M** |
+| `FrenzyEnterVolume` | 570,000 | 900,000 | 1,250,000 | 1,630,000 |
+| `FrenzyExitVolume` | 440,000 | 700,000 | 970,000 | 1,260,000 |
+| `RestlessEnterVolume` | 40,000 | 62,000 | 87,000 | 113,000 |
+| `RestlessExitVolume` | 29,000 | 44,000 | 62,000 | 81,000 |
+| `FrenzyEnter` (count backstop) | 3,750 | 5,750 | 8,000 | 10,000 |
+| vs Blob's 3,600 envelope | 0.97× | 1.5× | 2.1× | 2.8× |
+
+**Intensity 4 IS today's shipped, play-tested arena, prism for prism** — the ladder runs
+*down* from it, not up. Rampage already sits at 2.8× the Blob collider envelope as documented
+headroom, so scaling upward would put the top intensity somewhere nobody has measured. Net
+collider impact: zero at the top, strictly negative below it. Since `ProgressionConfig` caps a
+fresh account at intensity 2, the arena most players actually meet drops from 9,830 to 5,464
+seeded prisms.
```

</details>

### `41febdbb6` — docs(skills): capture the intensity-ladder model and five traps this branch paid for

_Claude, 2026-08-14 03:29:06 +0000_

```text
Extends the ecology skill with the per-intensity threshold generator pattern
(model in a script, self-tested against an already-shipped ladder) and four
traps: the IntensityWise spawner swap that makes one-sided features dead code,
a FloraVariantTuning field reaching only the families that read it, a sticky
cell choice derived from replicated state, and Mathf.RoundToInt's banker's
rounding. Extends asset-surgery with the serialized-field rename sweep that
has to include Tools/**.py generators, and the donor-scene rot a one-shot
generator suffers once its donor moves on.
```

```text
 .claude/skills/asset-surgery/SKILL.md | 15 +++++++++++++++
 .claude/skills/ecology/SKILL.md       | 47 +++++++++++++++++++++++++++++++++++++++++++++++
 2 files changed, 62 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 88 lines)</summary>

```diff
diff --git a/.claude/skills/asset-surgery/SKILL.md b/.claude/skills/asset-surgery/SKILL.md
index 6cc16875f..0d2b34dd8 100644
--- a/.claude/skills/asset-surgery/SKILL.md
+++ b/.claude/skills/asset-surgery/SKILL.md
@@ -723,6 +723,21 @@ that would otherwise cost a round-trip to a human at the editor:
 
 ## 5. Traps learned the hard way (check these BEFORE debugging for an hour)
 
+- **Renaming a Unity SERIALIZED FIELD must sweep `Tools/**.py` too, not just C# + scenes +
+  prefabs.** This repo authors scene/prefab YAML from Python generators
+  (`Tools/Build/author_*_assets.py`), and several of them both WRITE and VALIDATE a field by
+  literal name. Rename the C# field, migrate every scene, and the generator still emits the OLD
+  key — so the next person who re-runs it silently reverts your change, and the generator's own
+  `--check` "passes" while validating a name nothing reads any more. Sweep:
+  `grep -rn '<oldFieldName>' Assets/ Tools/ Docs/`, and treat a hit in `Tools/` as a caller, not
+  a comment. (Cost here: `anchorlessSpawnRadius` → `noNucleusSpawnRadius` was clean in the C#
+  and all three scenes, and left `author_dogfight_assets.py` writing the dead name.)
+- **A generator that CLONES a live scene as its donor rots the moment the donor changes.**
+  `author_dogfight_assets.py` clones `MinigameRampage.unity` and asserts on the donor's exact
+  field blocks; a rework of Rampage made it permanently un-runnable. That is the correct end
+  state for a one-shot migration — but say so **in the file**, or the next reader spends an hour
+  trying to satisfy asserts that describe a scene that no longer exists.
+
 - **Unity's FBX importer derives subasset fileIDs from OBJECT NAMES, so two different FBX
   files that share object names mint the SAME local fileIDs.** Two consequences, one good,
   one a false-alarm generator. Good: a prefab's `m_Modifications` against model A's instance
diff --git a/.claude/skills/ecology/SKILL.md b/.claude/skills/ecology/SKILL.md
index bbc80ce44..c6da1b363 100644
--- a/.claude/skills/ecology/SKILL.md
+++ b/.claude/skills/ecology/SKILL.md
@@ -59,6 +59,29 @@ what the carve-out silently broke — see the traps below.
 
 ## 2.6 Prism / trail traps (each of these cost real time)
 
+- **`CellTypeChoiceOptions.IntensityWise` silently swaps the SPAWNER class.**
+  `Cell.StartSpawnerForMode` picks `IntensityWiseLifeSpawner` for every IntensityWise cell and
+  `RandomLifeSpawner` for everyone else. Any spawn-loop feature (a density scalar, a gate, a
+  new roll) implemented in only one of them is **dead code in exactly the modes that asked for
+  it**. Implement in both, or state why one is deliberately excluded.
+- **A tuning field on `FloraVariantTuning` reaches only the flora families that READ it.**
+  `MaxTotalSpawnedObjects` was honoured by `AssembledFlora` alone for a long time, so 45
+  authored assets were writing a per-plant budget that did nothing on branching and
+  phyllotactic species — and the silent fallback was the prefab's own 5000. A field that
+  appears on every flora config has to mean the same thing on every flora; check all three
+  `ApplyVariantTuning` overrides when you add one.
+- **A cell choice that is STICKY and derived from REPLICATED state must be gated on
+  replication.** `Cell.AssignConfig` latches `runtime.Config` on its first pass and reads
+  `SelectedIntensity`, which reaches a client only in the game-config ClientRpc — while a
+  client's cell bootstraps off its FIRST CRYSTAL, ~600 ms earlier. The client then builds a
+  different intensity's arena than the host, for the whole match, with no error (the SOAP
+  default 0 clamps to a legal index). Gate on `GameDataSO.GameConfigSynced`, and make the
+  deferral RETRYABLE — the bootstrap used to latch its "done" flag on its first line, which
+  would have left a deferred cell with no cytoplasm and no spawner at all.
+- **`Mathf.RoundToInt` is banker's rounding.** For any authoring-facing scalar (a density
+  multiplier, a per-intensity count), use explicit `Mathf.FloorToInt(x + 0.5f)` — otherwise
+  `10 x 0.85` lands on 8 for one species and 9 for the next and nobody can explain why.
+
 - **A vessel lays TWO ribbons.** `VesselPrismController.Trail` is only half the trail; the
   double-trail spawn pattern puts every other prism in `SecondaryTrail` (`Trail2`). Anything
   reasoning about "the vessel's whole trail" — length, mass, cleanup, recycling — must walk both,
@@ -190,3 +213,27 @@ and put Restless somewhere the fauna start hunting a partly-grown cell.
 
 Always hand the numbers back as ESTIMATES with the measurer step attached — analytic
 counts are exact, but only the editor proves the generator runs at all.
+
+### 7.1 A ladder of intensities: put the MODEL in a script, and self-test it
+
+A cell with per-intensity configs needs one `PhaseThresholds` block per intensity, each riding
+its own forest volume. Authoring four by hand is how four ladders drift apart. Write the model
+as a Python script under `Tools/Build/` that:
+
+- holds the species table (plants, budget, leaf prism volume, `LeafScalePerLevel`) and the
+  per-intensity scalars, and computes prisms + volume from them;
+- derives all eight thresholds from ONE set of ratios (Frenzy just above full growth, exit
+  ~77%, Restless ~7%), so every intensity is the same shape;
+- **emits the assets** and supports `--check` so CI can catch a hand-edit;
+- **self-tests by reproducing an already-shipped, play-tested ladder to the digit.** That
+  assert is the whole difference between a model and a fresh guess: if the formula cannot
+  reproduce the arena a human already approved, it is wrong, and you find out at authoring
+  time instead of in a play test.
```

</details>

### `41cc607ea` — docs(scoring): record B17/B18 - the environment-mass credit and domain rules

_Claude, 2026-08-14 03:29:44 +0000_

```text
 Docs/ScoringSystem/BUGS.md | 53 +++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 53 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/ScoringSystem/BUGS.md b/Docs/ScoringSystem/BUGS.md
index a46a620b2..728de0a8f 100644
--- a/Docs/ScoringSystem/BUGS.md
+++ b/Docs/ScoringSystem/BUGS.md
@@ -464,3 +464,56 @@ and wrong; anything that must survive into the next game has to come from the se
 and compiled: the model reproduces the bug (client stuck at 842 while the host reads 0), shows
 server re-writes failing to heal it, and shows `SyncLocalMirrorsFromNetwork` fixing it without
 clobbering a live mid-game value. Engine verification pending.
+
+---
+
+## B17 — a client scored nothing for ENVIRONMENT mass (flora, fauna, laid structure)
+
+**Symptom.** 2-player Rampage: the host scored off everything; the client could only ever score
+off the **other pilot's trail**, never off a cactus it flew through and shattered. In a mode whose
+entire score is destroyed environment mass, the client was effectively playing a slot machine —
+whatever the *server's* own physics happened to knock over got credited to them instead.
+
+**Root cause, and it is platform-wide.** `StatsManager` records prism destruction **server-only**
+(`_allowRecord`), and its own doc comments state the justification twice:
+
+> "a prism sits at the same place on the server, so the server's own physics sees a client's ram
+> and records it"
+
+That is true of a TRAIL prism — laid from replicated vessel motion, so both peers have one in the
+same place, which is exactly why trail kills were the one thing that worked. It is **false** of
+flora and fauna, and `CellNetworkSync`'s class doc has always said so: *"Flora and fauna spawning
+is non-deterministic per-side (each client runs its own IntensityWiseLifeSpawner with local
+Random.value rolls)."* The server's copy of the cactus the client just shredded is somewhere else
+entirely, so nothing was recorded anywhere.
+
+**Fix.** `Player.ReportEnvironmentPrismDestroyed_ServerRpc` — the third instance of the same
+owner-detects → server-records round-trip as `ReportFaunaKill_ServerRpc` (fauna have no
+NetworkObject) and `ReportCombatHit_ServerRpc` (projectiles are not networked). Identity comes from
+RPC ownership, never a name string.
+
+The other half is **who must NOT credit**: `StatsManager.OwnsAttacker` lets the server credit only
+players it simulates (its own + every AI, both server-owned NetworkObjects) and DROP environment
+kills it observed a remote player make. Rostered victims are untouched and stay server-recorded.
+Each kill lands exactly once on both paths.
+
+**Rule.** Server-only stat recording is correct only for things that exist identically on every
+peer. Before adding one, ask what the stat's SOURCE is: a trail (replicated motion — fine), or a
+per-peer simulation (flora, fauna, projectiles — needs the owner round-trip).
+
+## B18 — environment mass was hostile to EVERY domain, including its own colour
+
+**Symptom / cause.** The only hostility test in `StatsManager.PrismDestroyed` was the owner-name /
+roster comparison. Flora, fauna bodies and laid cell structure carry non-roster owner names, so
+they fell to the `else` branch and counted as hostile to everyone — including the third of a
+mixed-domain forest wearing the destroying pilot's own colour. Domain was decoration.
+
+**Fix.** `PrismStats` carries the destroyed prism's `OwnDomain`, and
+`StatsManager.IsFriendlyEnvironmentPrism` applies to the world the rule trails always had — **your
+own colour is worth nothing** — with `Domains.Blue` (the "no team" sentinel) staying hostile to
+everyone so neutral structure still scores. Ribcage rides the same metric and is unaffected in
+practice: its cage is painted across the full triad plus Blue joints, so a team still reaches a
+2,000 target out of ~10,620 prisms.
+
+**Verification.** Both are compile-by-inspection + traced call paths; engine verification pending
+(MPPM, 1 host + 1 client — see RAMPAGE.md's checklist).
```

</details>

### `924dcc6ac` — feat(rampage): intensity is scarcity — fewer crystals, more wildlife, one forest

_Claude, 2026-08-14 17:43:46 +0000_

```text
Rampage's intensity ladder thinned the FOREST (I1 grew half of I4's plants). It
now leaves the forest alone — every level grows I4's shipped, play-tested arena,
prism for prism — and moves the two things the mode's loop is actually made of,
in opposite directions:

  crystals  2x players / players / players-1 (min 1) / exactly 1
  wildlife  1x / 2x / 3x / 4x the authored population

The crystal is the Dolphin's only blast trigger, so its count is how contested
cashing out is; thinning the forest only made a smaller arena.

Two platform capabilities, both defaulting to no-ops:

SpawnProfileSO.FaunaPopulationScale — the fauna twin of FloraPopulationScale.
Multiplies InitialSpawnCount, PopulationSize AND MaxLivePopulation. The cap is
the load-bearing half: it is what bounds a standing population, so a scalar that
moved only the floors is clamped away above ~1.5x and reads as doing nothing.
Rampage forces the issue — its two species are the SHARED Blob assets, so there
was no per-mode asset to tune at all.

Fauna has FOUR producers, not two (both spawners, Fauna.TryReproduce, the
freestyle Microscene conveyor), and splitting a cap between them is incoherent,
not merely incomplete. So resolution lives on the Cell — ResolveFaunaPopulation
/ ResolveFaunaCap / IsFaunaAtCap — the one object every producer already holds.
No direct read of cfg.MaxLivePopulation survives outside the config and profile.

CrystalManager.CrystalCountMode.IntensityScaled — max(1, round(players x
CrystalsPerPlayer) + ExtraCrystals) per intensity, list order = intensity. Needs
no GameConfigSynced gate (unlike the sticky cell-config choice): both intensity
readers are server-side and clients receive the count as the replicated
slot-list length. CurrentIntensity also unifies the two intensity reads on that
class, so the anchor lookup no longer silently falls back to intensity 1 when a
scene leaves the SOAP field unwired.

Invariants: production gating only, nothing culled; no domain asymmetry; mass
still conserved; volume still the spine (one ladder now, riding the one forest);
crystals still respawn in the nucleus. Collider budget: worst case unchanged
(I4's forest was already the shipped one at 2.8x the Blob envelope); I1-I3 rise
to it; fauna 8 -> 32 creatures is tens of prisms against 9,830, sensed on the
Burst density grid, not physics; at most 8 crystal triggers at I1.

rampage_intensity.py regenerates all eight assets and self-tests that all four
intensities reproduce the shipped I4 ladder. RampageIntensityLadderTests guards
the other end — the two formulas plus the authored data in the scene and the
profiles, which --check cannot see.
```

```text
 .../_SO_Assets/Cell Configs/Rampage Cell/Rampage Cell Config 1.asset  |  26 ++--
 .../_SO_Assets/Cell Configs/Rampage Cell/Rampage Cell Config 2.asset  |  27 ++--
 .../_SO_Assets/Cell Configs/Rampage Cell/Rampage Cell Config 3.asset  |  25 ++--
 .../_SO_Assets/Cell Configs/Rampage Cell/Rampage Cell Config 4.asset  |  13 +-
 .../Cell Configs/Rampage Cell/Rampage Spawn Profile 1.asset           |   5 +-
 .../Cell Configs/Rampage Cell/Rampage Spawn Profile 2.asset           |   5 +-
 .../Cell Configs/Rampage Cell/Rampage Spawn Profile 3.asset           |   5 +-
 .../Cell Configs/Rampage Cell/Rampage Spawn Profile 4.asset           |   1 +
 Assets/_Scenes/Multiplayer Scenes/MinigameRampage.unity               |  11 +-
 Assets/_Scripts/Controller/Arcade/RAMPAGE.md                          | 213 +++++++++++++++++++++-----------
 Assets/_Scripts/Controller/Arcade/RampageObjectiveProvider.cs         |   5 +-
 Assets/_Scripts/Controller/Environment/Cell.cs                        |  41 ++++++
 Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs         |   7 +-
 Assets/_Scripts/Controller/Environment/FlowField/CrystalManager.cs    | 100 ++++++++++++++-
 Assets/_Scripts/Controller/Environment/IntensityWiseLifeSpawner.cs    |  18 +--
 Assets/_Scripts/Controller/Environment/RandomLifeSpawner.cs           |  14 ++-
 Assets/_Scripts/Controller/Toys/Microscene.cs                         |   5 +-
 Assets/_Scripts/Tests/Editor/RampageIntensityLadderTests.cs           | 172 ++++++++++++++++++++++++++
 Assets/_Scripts/Tests/Editor/RampageIntensityLadderTests.cs.meta      |  11 ++
 Assets/_Scripts/Utility/DataContainers/SpawnProfileSO.cs              |  41 ++++++
 CLAUDE.md                                                             |   2 +-
 Docs/ECOSYSTEM.md                                                     | 105 ++++++++++++++++
 Tools/Build/rampage_intensity.py                                      | 159 +++++++++++++++++++-----
 23 files changed, 833 insertions(+), 178 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1216 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/RAMPAGE.md b/Assets/_Scripts/Controller/Arcade/RAMPAGE.md
index 93a9941ff..85f1b049a 100644
--- a/Assets/_Scripts/Controller/Arcade/RAMPAGE.md
+++ b/Assets/_Scripts/Controller/Arcade/RAMPAGE.md
@@ -5,8 +5,9 @@
 Rampage is the **Dolphin-only demolition race**, and the destructive analog of Crystal
 Capture ("Scurry"): every domain races to be the first to DESTROY **2,000 hostile
 prisms**. A forest of big cacti and other breakable flora fills the cell from just
-outside the nucleus out to the membrane, and a **single contested crystal** respawns
-inside the nucleus at the centre of it all.
+outside the nucleus out to the membrane, and the arena's **contested crystals** respawn
+inside the nucleus at the centre of it all — **how many is what intensity means here**,
+falling from twice the roster at intensity 1 to a single one at intensity 4.
 
 **The loop is the Dolphin's own economy, made into a sport.** Nothing here is scripted —
 the mode simply arranges the arena so the vessel's existing spine becomes the game:
@@ -14,7 +15,7 @@ the mode simply arranges the arena so the vessel's existing spine becomes the ga
 | the vessel already does this | Rampage makes it the game |
 |---|---|
 | Energy is banked **only by skimming** (+0.006667/skim, 150 skims fills it) | a cactus forest is the charging ground — and every prism you clip on the way through scores |
-| Touching a **crystal** spends the whole meter as one conic jaw blast | the arena carries exactly **one** crystal, so cashing out is contested |
+| Touching a **crystal** spends the whole meter as one conic jaw blast | the arena carries **fewer crystals than pilots** at the top intensities, so cashing out is contested |
 | Energy owns the blast's **GAPE** (4.76° empty → 23.43° full) | arriving charged is worth ~5× the swath of arriving empty |
 | The cone reaches **2,400 units** down-range | taking the crystal at the nucleus and turning outward sweeps a full radius of forest |
 | Ramming a prism **halves** the meter | flying *through* the thicket instead of *into* it is the skill |
@@ -63,11 +64,12 @@ economy itself; this file only arranges around it.
   `rule.IsObjectiveReached`
 - **Domains**: free-for-all like Scurry (`MinDomainsAllowed`/`MaxDomainsAllowed`
   defaults 1/3); players 1–4 with AI backfill
-- **Intensity**: **4 levels**, `CellTypeChoiceOptions.IntensityWise` over four cell configs —
-  the forest thickens from 3,500 to 9,830 seeded prisms. See "Four intensities" below.
+- **Intensity**: **4 levels** — **fewer crystals, more wildlife**, over a forest that is
+  identical at every level. `CellTypeChoiceOptions.IntensityWise` over four cell configs.
+  See "Four intensities" below.
 - **Vessels**: **Dolphin only** — see "Why Dolphin-only" below
-- **Objective arrow**: `RampageObjectiveProvider` — points at the contested omni crystal and
-  **nothing else, ever**. The filter is the point: `Crystal.Active` also holds every
+- **Objective arrow**: `RampageObjectiveProvider` — points at the **nearest** contested omni
+  crystal and **nothing else, ever**. The filter is the point: `Crystal.Active` also holds every
   lifeform heart the food web is constantly dropping (this mode's whole verb is killing
   flora) and every team crystal a Dolphin seeds, so a nearest-live-crystal scan would
   spend the match swinging onto whichever cactus just died. Only a MANAGER-SPAWNED
@@ -95,10 +97,11 @@ The scene's four AI backfill templates are `vesselClass: 2` (Dolphin) so the AI
 the same ship — an AI class comes from `aiInitializeDatas`, not from the clamp.
 
 **The mode is Dolphin-only because a mixed roster would break the premise, not to be
-exclusive.** The single crystal is only a contested object if it is the only way to
+exclusive.** A scarce crystal is only a contested object if it is the only way to
 discharge a blast. A Rhino or Sparrow in the arena would ignore it entirely and shoot
 the forest down on its own clock, so the crystal would stop being worth fighting over
-for anyone.
+for anyone — and the whole intensity ladder, which is *made of* that scarcity, would
+stop meaning anything.
 
 **The Dolphin can still make its own crystals, and that is deliberate.** Crystal
 Seeding (its Charge ability) plants a TEAM crystal only the pilot's domain can collect,
@@ -158,72 +161,102 @@ is now credited by whichever machine simulates the attacker, and the collecting
 their own crystal effects so the blast exists on their machine at all. Full record:
 `Docs/ECOSYSTEM.md §27.8`–`§27.9`.
 
-## Four intensities — the forest thickens inside a fixed shell
+## Four intensities — fewer crystals, more wildlife, one forest
 
-Ribcage's intensity adds rinds inward from a fixed outer radius; Rampage's **thickens the
-forest inside a fixed shell**. Intensity moves forest mass and nothing else — every number
-that defines the arena's silhouette and its rules is one constant at all four levels:
+Ribcage's intensity adds rinds inward from a fixed outer radius. Rampage's used to thicken the
+forest; **it no longer touches the forest at all.** Every intensity grows intensity 4's arena,
+prism for prism — the one that was play-tested — and intensity instead moves the two things the
+mode's loop is actually made of, in opposite directions:
+
+| | I1 | I2 | I3 | I4 |
+|---|---|---|---|---|
+| **omni crystals** | 2 × players | players | players − 1 (min 1) | **1** |
+| …for a 4-player lobby | 8 | 4 | 3 | **1** |
+| **wildlife** (`FaunaPopulationScale`) | 1× | 2× | 3× | **4×** |
+| …tadpoles / sharks at cap | 6 / 2 | 12 / 4 | 18 / 6 | **24 / 8** |
+| seeded forest | 9,830 prisms | 9,830 | 9,830 | 9,830 |
+| phase ladder | identical at all four (frenzy 1,630,000 / 1,260,000 vol) | | | |
+
```

</details>

### `f7c8b9aa1` — feat(sparrow): rounds grow as they fly; shield returns to MASS 5

_Claude, 2026-08-14 18:18:25 +0000_

```text
"The only thing that has felt fun was huge projectiles." Round 2 gave every
round its whole path back, but it did not change the SHAPE of what a round
deletes: a thread. A huge projectile deletes a tunnel, and that — not the hit
rate — is what was fun.

So keep huge projectiles and remove the thing that made them silly. Rounds now
leave the muzzle at their authored size and SWELL as they travel, and MASS
decides how much: 3x over a flight at resting Mass, 6x at Mass 10, linear in
level and extrapolated across the element system's full band (1.5x starved,
7.5x at full overcharge). Bullets and turret shots alike — the turret adopts
the factor through bulletAction like cadence, speed and spread.

Footprint goes as the SQUARE of the radius, so resting Mass is already ~9x the
swath and Mass 10 is ~36x. For scale, the accidental oversized collider that
felt good was 6.0 world radius; resting Mass now ends its flight at 2.47 and
Mass 10 reaches 4.95. The fun size is back as something earned.

Sized honestly the whole way: the visual and the swept hit radius are scaled by
the same factor every frame, so the round-6 rule (hit radius = visible
cross-section +10%) is invariant through the flight. Cross-section ONLY — the
tracer is a 20-long dart, so uniform scaling at 6x would draw a 120-unit needle
across a 72-unit range. The hit radius is therefore scaled explicitly rather
than re-derived from lossyScale, since a SphereCollider takes the largest lossy
component and that stays the untouched z-stretch.

This settles where the two elements divide, and the map now says so:
MASS owns the SUBSTANCE of what you fire, SPACE owns its REACH. So the
Shielded Prisms upgrade returns from SPACE 5 to MASS 5 (ShieldedAtSpace5 ->
ShieldedAtMass5, same enum value so the asset is untouched), leaving SPACE 5 as
pierce only, on both fire modes. The Sparrow's map is 4/4 upgrades again and
the open MASS-5 slot is closed by explicit sign-off rather than invention.

Deliberately NOT done, so it is not re-derived: impact shatter and pierce
depth. Both break the one-round-one-prism ceiling too; growth was chosen
instead. Growth also does not touch the vessel/mine PhysX path — bigger bullets
against pilots is a Dog Fight balance change, not a prism-clearing one.
```

```text
 Assets/Resources/ElementalAbilityMaps/Sparrow.asset                   |  20 +++---
 Assets/_SO_Assets/VesselActions/Sparrow/FullAutoAction.asset          |   2 +
 Assets/_Scripts/Controller/Projectiles/Gun.cs                         |  12 +++-
 Assets/_Scripts/Controller/Projectiles/Projectile.cs                  |  49 ++++++++++++++
 .../Vessel/R_VesselActions/Data Containers/FullAutoActionSO.cs        |  44 +++++++++++++
 .../R_VesselActions/Data Containers/FullAutoBlockShootActionSO.cs     |  28 +++++---
 .../Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs        |   8 ++-
 .../R_VesselActions/Executors/FullAutoBlockShootActionExecutor.cs     |  31 +++++----
 .../Controller/Vessel/R_VesselActions/SPARROW_SPRAY_ACCURACY.md       |  73 ++++++++++++++++++++-
 .../Controller/Vessel/R_VesselActions/SPARROW_TURRET_STANCE.md        |  23 ++++++-
 Assets/_Scripts/Tests/Editor/SparrowRoundGrowthTests.cs               | 111 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/Tests/Editor/SparrowRoundGrowthTests.cs.meta          |  11 ++++
 CLAUDE.md                                                             |   2 +-
 Docs/ElementalAbilitySystem/FLEET_MAPS.md                             |   4 +-
 Docs/UNITY_VERIFICATION_CHECKLIST.md                                  |  43 +++++++++++++
 15 files changed, 423 insertions(+), 38 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 600 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Projectiles/Gun.cs b/Assets/_Scripts/Controller/Projectiles/Gun.cs
index b7f0b21d4..d8bbbf89c 100644
--- a/Assets/_Scripts/Controller/Projectiles/Gun.cs
+++ b/Assets/_Scripts/Controller/Projectiles/Gun.cs
@@ -44,7 +44,7 @@ namespace CosmicShore.Gameplay
             FiringPatterns firingPattern = FiringPatterns.Default,
             int energy = 0, bool detachAfterSpawn = false,
             bool stopOnFirstPrismImpact = false, bool spareOwnDomain = false,
-            Vector3? aimDirection = null)
+            Vector3? aimDirection = null, float flightGrowthFactor = 1f)
         {
             if (_onCooldown && !ignoreCooldown) return;
 
@@ -61,7 +61,7 @@ namespace CosmicShore.Gameplay
                     // itself owns no spread policy and rolls no dice: it is handed a direction.
                     FireSingle(containerTransform, speed, inheritedVelocity,
                         projectileScale, Vector3.zero, projectileTime, charge, energy, aimDirection, detachAfterSpawn,
-                        stopOnFirstPrismImpact, spareOwnDomain);
+                        stopOnFirstPrismImpact, spareOwnDomain, flightGrowthFactor);
                     break;
             }
 
@@ -154,7 +154,8 @@ namespace CosmicShore.Gameplay
             Vector3? customDirection = null,
             bool detachAfterSpawn = false,
             bool stopOnFirstPrismImpact = false,
-            bool spareOwnDomain = false)
+            bool spareOwnDomain = false,
+            float flightGrowthFactor = 1f)
         {
             if (_vesselStatus == null)
             {
@@ -183,6 +184,11 @@ namespace CosmicShore.Gameplay
                 stopOnFirstPrismImpact, spareOwnDomain);
 
             projectile.transform.localScale = projectileScale * projectile.InitialScale;
+
+            // MASS in-flight growth: set BEFORE launch, which is where the round captures the
+            // scale it will grow from. The gun owns no growth policy - it is handed a factor.
+            projectile.SetFlightGrowth(flightGrowthFactor);
+
             projectile.Velocity = direction * speed + inheritedVelocity;
             projectile.LaunchProjectile(projectileTime);
 
diff --git a/Assets/_Scripts/Controller/Projectiles/Projectile.cs b/Assets/_Scripts/Controller/Projectiles/Projectile.cs
index 0792a66be..bc3c9b0eb 100644
--- a/Assets/_Scripts/Controller/Projectiles/Projectile.cs
+++ b/Assets/_Scripts/Controller/Projectiles/Projectile.cs
@@ -177,6 +177,10 @@ namespace CosmicShore.Gameplay
             // end-of-flight handler, and the once-only latch must re-arm.
             FlightEnded = null;
             _flightEndRaised = false;
+
+            // Likewise the previous shot's MASS growth — a caller that does not set it gets
+            // the un-grown default rather than whoever fired this instance last.
+            _flightGrowthFactor = 1f;
         }
 
         public void SetType(ProjectileType type) => Type = type;
@@ -242,6 +246,12 @@ namespace CosmicShore.Gameplay
             // per shot, so this cannot be cached at Awake.
             if (sweptPrismDetection) CacheSweepRadius();
 
+            // The growth baseline is whatever this shot actually launched at (the gun applies
+            // projectileScale, the turret sizes its carried collider per shot), never the
+            // prefab's InitialScale.
+            _launchScale = transform.localScale;
+            _launchSweepRadius = _sweepRadius;
+
             _moveCts = CancellationTokenSource.CreateLinkedTokenSource(
                 this.GetCancellationTokenOnDestroy());
             MoveProjectileAsync(projectileTime, _moveCts.Token).Forget();
@@ -288,6 +298,11 @@ namespace CosmicShore.Gameplay
                     float deltaTime = Time.deltaTime;
                     float factor = Mathf.Cos(elapsedTime * Mathf.PI / (2f * projectileTime));
 
+                    // Grow BEFORE the step is swept, so this frame's hit volume is the size the
+                    // round has actually reached rather than the one it left the muzzle at.
+                    if (_flightGrowthFactor != 1f)
+                        ApplyFlightGrowth(elapsedTime / projectileTime);
```

</details>

### `6a6cb5af0` — docs(skills): capture the four-producer rule and the floor-vs-cap trap

_Claude, 2026-08-14 20:39:04 +0000_

```text
Three learnings from the Rampage intensity rework, all of which the ecology
skill's existing advice would have got WRONG:

- "implement it in BOTH spawners" is insufficient for fauna. There are four
  producers, and reproduction is the one that matters most - splitting a cap
  between them is incoherent, not just incomplete. The resolution belongs on the
  Cell, the one object all four hold.
- a population scalar that moves the seed floor but not MaxLivePopulation is
  clamped away above ~1.5x and reads as doing nothing.
- the mirror of the sticky-config replication trap: a value the SERVER computes
  and the client RECEIVES needs no GameConfigSynced gate. The test is which
  machine derives it, not whether it depends on intensity.

Plus the shared-asset check that made the profile scalar necessary rather than
merely tidy.
```

```text
 .claude/skills/ecology/SKILL.md | 26 ++++++++++++++++++++++++++
 1 file changed, 26 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/.claude/skills/ecology/SKILL.md b/.claude/skills/ecology/SKILL.md
index c6da1b363..4cdcdfc42 100644
--- a/.claude/skills/ecology/SKILL.md
+++ b/.claude/skills/ecology/SKILL.md
@@ -64,6 +64,26 @@ what the carve-out silently broke — see the traps below.
   `RandomLifeSpawner` for everyone else. Any spawn-loop feature (a density scalar, a gate, a
   new roll) implemented in only one of them is **dead code in exactly the modes that asked for
   it**. Implement in both, or state why one is deliberately excluded.
+- **"Both spawners" is not enough for FAUNA — there are FOUR producers.** `RandomLifeSpawner`,
+  `IntensityWiseLifeSpawner`, **`Fauna.TryReproduce`** (reproduction is the actual population
+  driver, not the spawner) and the freestyle **`Microscene`** conveyor all read the per-species
+  population numbers. Splitting a modifier across them is not merely incomplete, it is
+  *incoherent*: a seeder filling to 24 while reproduction stops at 6 is two ceilings for one
+  number. So a per-cell modifier of a per-species number resolves on the **`Cell`** — the one
+  object all four already hold (`Cell.ResolveFaunaPopulation` / `ResolveFaunaCap` /
+  `IsFaunaAtCap`) — and the raw config field then has **no direct reader** outside the config
+  and the profile. Write the comparison once too (`IsFaunaAtCap`), or a caller will test the
+  unscaled number correctly-looking-ly. Generalizes to any future per-cell modifier.
+- **A population scalar that moves the FLOOR but not the CAP is inert.** `MaxLivePopulation` is
+  documented as "a performance backstop, not the primary control", which makes it easy to leave
+  alone — but it is what actually bounds a standing population. The Blob tadpole floors at 4 and
+  caps at 6, so a floor-only scalar is clamped away above ~1.5× and reads as *doing nothing*.
+  Move floor and cap together. (Scaling either is production gating, which §0 permits; culling
+  to meet a lowered scale is not.)
+- **A shared species asset is the reason the scalar belongs on the PROFILE.** Rampage's two
+  species are referenced straight out of `Blob Cell/`, so stocking its arena by editing them
+  would have restocked Menu_Main's lava lamp too. Before tuning any lifeform config, grep who
+  else references it — a per-mode number on a shared asset is a cross-mode bug.
 - **A tuning field on `FloraVariantTuning` reaches only the flora families that READ it.**
   `MaxTotalSpawnedObjects` was honoured by `AssembledFlora` alone for a long time, so 45
   authored assets were writing a per-plant budget that did nothing on branching and
@@ -78,6 +98,12 @@ what the carve-out silently broke — see the traps below.
   default 0 clamps to a legal index). Gate on `GameDataSO.GameConfigSynced`, and make the
   deferral RETRYABLE — the bootstrap used to latch its "done" flag on its first line, which
   would have left a deferred cell with no cytoplasm and no spawner at all.
+- **…but do NOT over-apply that gate: a value the client RECEIVES needs none.** The test is not
+  "does this depend on intensity", it is "does a CLIENT derive it?". `CrystalManager`'s
+  per-intensity crystal count reads the same late-arriving `SelectedIntensity`, yet needs no
+  `GameConfigSynced` gate — it is resolved only inside `NetworkCrystalManager`'s `IsServer`
+  paths and reaches clients as the replicated slot-list LENGTH. Gating it would add a race for
+  nothing. Ask which machine computes the value before reaching for the gate.
 - **`Mathf.RoundToInt` is banker's rounding.** For any authoring-facing scalar (a density
   multiplier, a per-intensity count), use explicit `Mathf.FloorToInt(x + 0.5f)` — otherwise
   `10 x 0.85` lands on 8 for one species and 9 for the next and nobody can explain why.
```

</details>

### `02095c8a5` — docs(skills): capture the tunneling trap, the frame-quantized rate, and the ceiling rule

_Claude, 2026-08-14 21:26:20 +0000_

```text
Four things this branch paid for that would otherwise be re-derived:

- A fast projectile is a TELEPORT, not a sweep — it only tests the points it
  lands on, and the tell is 'making the projectile bigger fixes it'. With the
  measured numbers, because 26%-of-path is not a thing anyone guesses.
- A SphereCollider takes the LARGEST lossy-scale component. This has now bitten
  the same vessel twice, from opposite directions.
- UniTask.Delay(1/rate) quantizes to whole frames, so an authored rate is
  silently min(rate, framerate) — and looks correct whenever the interval
  happens to straddle two frames.
- Weapon feel complaints are usually a CEILING, not a tuning value: find what
  caps output per unit of input before proposing numbers.
```

```text
 .claude/skills/vessel/SKILL.md               | 18 ++++++++++++++++++
 .claude/skills/vessel/references/CONTRACT.md | 19 +++++++++++++++++++
 Docs/ElementalAbilitySystem/ARCHITECTURE.md  |  4 ++--
 Docs/UNITY_VERIFICATION_CHECKLIST.md         |  4 +++-
 4 files changed, 42 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 90 lines)</summary>

```diff
diff --git a/.claude/skills/vessel/SKILL.md b/.claude/skills/vessel/SKILL.md
index 9449dd63e..7200e419a 100644
--- a/.claude/skills/vessel/SKILL.md
+++ b/.claude/skills/vessel/SKILL.md
@@ -196,6 +196,24 @@ applies to new abilities, new resources on the meter list, and anything that add
     `_speedTrackingRate` is a latched ramp state (the Rhino's ramp boost) that a naive early-return
     can silently consume.
 
+16. **A `UniTask.Delay(1/rate)` fire loop quantizes to WHOLE FRAMES**, so an authored rate is
+    silently `min(rate, framerate)` — a 60 fps client fires twice as fast as a 30 fps one, and
+    the rate simply cannot exceed the frame rate. It looks correct at any rate whose interval
+    happens to straddle two frames (30/s at 60 fps was right by luck for a year). Owe fire in
+    SECONDS and pay it off in whole volleys (`owed += Time.deltaTime`; fire `floor(owed/interval)`),
+    capping the per-tick catch-up and DROPPING the excess so a hitch never discharges as a burst.
+17. **Never draw from `UnityEngine.Random` in a per-shot hot path.** It is global state that
+    deterministic systems seed (`Random.InitState` for the HexRace track), so a gun rolling it
+    120×/s makes their output depend on how long someone held a trigger. Use a pure integer hash
+    of a per-shot serial: no global state, and peers that agree on the shot count agree on the
+    result — which matters wherever the spawned object is local and unreplicated.
+18. **Weapon "feel" complaints are usually a CEILING, not a tuning value.** Before re-tuning,
+    find what caps output per unit of input: prisms have no HP (one hit = one kill) and a
+    sub-upgrade round dies on its first impact, so a Sparrow's ceiling is exactly *rounds/s*.
+    Rate, spread and accuracy all multiply a 1:1 relationship and cannot break it — only pierce
+    depth, chain effects, or **size** can, and size wins because destruction footprint goes as the
+    SQUARE of the radius. Say which ceiling you found before proposing numbers.
+
 ## 5. Audit, then hand back verification (you cannot run Unity; the human is the gate)
 
 - State which auditors to run and the expected result: **Audit Vessel Ability Rows**,
diff --git a/.claude/skills/vessel/references/CONTRACT.md b/.claude/skills/vessel/references/CONTRACT.md
index 1b800ff66..9c5519d94 100644
--- a/.claude/skills/vessel/references/CONTRACT.md
+++ b/.claude/skills/vessel/references/CONTRACT.md
@@ -305,6 +305,25 @@ warning). Be exhaustive here; this is the contract's least-guarded clause.
   — leave per-vessel `skimmerCrystalEffectsSO` empty; do not duplicate pickup logic.
 - **Crackle opt-in = two halves**: the `ForcefieldCrackleController` on the skimmer GO +
   `SkimmerForcefieldCracklePrismEffect` in the skimmer container.
+- **A fast projectile is a TELEPORT, not a sweep — it only tests the points it LANDS on.**
+  `Projectile.MoveProjectileAsync` writes `position += Velocity·Δt` and PhysX samples the
+  discrete trigger once per physics step, so the path BETWEEN samples is never tested. Measured
+  on the Sparrow: at its base 375 u/s a round covers 6.25 u per 60 fps frame behind a 1.65
+  hit sphere — **26% of its own path**, ~3% at high SPACE, and it halves again at 30 fps. The
+  symptom is a gun that cannot clear a dense patch no matter how much you shoot, with no misses
+  to see; the tell is *"making the projectile bigger fixes it"*, because a big enough ball closes
+  the per-frame gap. Fix with `PrismSpatialIndex.QuerySegment` +
+  `Projectile.sweptPrismDetection` (dispatch nearest-first, and have the sweep OWN the contact
+  class so the trigger cannot double-fire) — never by inflating the collider, and never with
+  `Physics.SphereCast` (CLAUDE.md forbids physics queries against prisms; a transform teleport
+  also bypasses CCD entirely). Rate and spread cannot compensate: they multiply a path the
+  weapon is structurally blind to.
+- **A `SphereCollider`'s world radius is `m_Radius × the LARGEST lossy-scale component`** — this
+  trap has now bitten the same vessel twice. Once as the 12-diameter hit sphere nobody authored
+  (a `0.3` radius on a tracer stretched ×20 in z), and again when growing a round's
+  cross-section only: the untouched z-stretch stays the max, so a radius re-derived from
+  `lossyScale` never moves. Author it as `desiredWorldRadius / maxScaleComponent`, and when a
+  size must track a non-uniform scale, carry the factor EXPLICITLY rather than re-deriving.
 - **Hygiene**: renaming container fields without `[FormerlySerializedAs]` silently strips
   authored effects (Sparrow lost all elemental-crystal feedback this way); an effect asset that
   exists but sits in no container executes never (several orphans exist); fork shared effect SOs
diff --git a/Docs/ElementalAbilitySystem/ARCHITECTURE.md b/Docs/ElementalAbilitySystem/ARCHITECTURE.md
index a87a758cf..bc516b0b7 100644
--- a/Docs/ElementalAbilitySystem/ARCHITECTURE.md
+++ b/Docs/ElementalAbilitySystem/ARCHITECTURE.md
@@ -145,9 +145,9 @@ consumers scale symmetrically down through debuffs — codebase-consistent behav
 
 | Element | Quantitative (continuous) | Attach point | Level-5 upgrade | Attach point |
 |---|---|---|---|---|
-| **Space** | Gun range (projectile speed and/or lifetime; range = v·T·2/π) | `FullAutoActionExecutor` fire tick + `FireGunActionExecutor.Fire` — live `Multiplier(Space)` on speed/lifetime (the authored `speedValue` Min 1500→Max 4000 becomes the tuning range) | **Piercing bullets** (new default below L5: destroy on first prism impact — today's bullets already pierce, see AUDIT §4) **+ shielded turret prisms with a wider hit sphere** (moved here from Mass 5, 2026-08 — one gate, both fire modes) | Per-shot `piercing` flag through `Gun.FireGun → Projectile.Initialize`; prism-impact flow returns the projectile to the factory after the damage effect when not piercing. Turret side: `FiredPrismState.ShieldedAtSpace5` sets `prismProperties.IsShielded` before `Initialize` off the same `IsUpgradeActive(Space)` snapshot. Must not reuse `DisableColliderNow` until the dud bug is fixed |
+| **Space** | Gun range (projectile speed and/or lifetime; range = v·T·2/π) | `FullAutoActionExecutor` fire tick + `FireGunActionExecutor.Fire` — live `Multiplier(Space)` on speed/lifetime (authored `speedValue.Value` **375** with `MultiplierAtFullLevel` **9**, so SPACE 0 ≈ 72 u and SPACE 15 ≈ 931 u) | **Piercing bullets** — and ONLY that, on **both** fire modes (bullets and turret prism rounds). SPACE owns REACH; the armour on fired prisms is **MASS 5** (it spent 2026-08 rounds 4–6 here and was returned by sign-off on 2026-08-13) | Per-shot `piercing` flag through `Gun.FireGun → Projectile.Initialize`; prism-impact flow returns the projectile to the factory after the damage effect when not piercing. Must not reuse `DisableColliderNow` until the dud bug is fixed |
 | **Time** | Boost speed, on an **indefinite** boost (no heat, no meter) | `VesselTransformer.CurrentBoostAmount()` — live `Multiplier(Time)` on top of `VesselStatus.BoostMultiplier`; the shared field is never mutated | **Elemental Ward**: while boosting, negative `ResourceSystem.ApplyElementalEffect` calls are dropped — buffs still land, live debuffs still decay, non-elemental danger punishments (slow, input mute) still apply | The general `ResourceSystem` immunity state + the shared `VesselElementalImmunity` driver (`WhileBoosting`, gated `Element.Time`). The **strafing roll is now BASE kit**, ungated, on `BarrelRollController` (left stick at perimeter + boost). Detail: `_Scripts/Controller/Vessel/R_VesselActions/SPARROW_AFTERBURNER.md` |
-| **Mass** | Turret prism stretch (long z-axis) | `FullAutoBlockShootActionExecutor` — multiply `BlockScale.z` by `Multiplier(Mass)` at fire time, routed through `TargetScale` + `Prism.Initialize` (prereq fix), curve in the SO | **OPEN DESIGN SLOT** (2026-08) — the former *Shielded turret prisms* upgrade moved to **Space 5** so one gate transforms both fire modes. Do not refill without sign-off | — |
+| **Mass** | Turret prism stretch (long z-axis) **+ in-flight round growth on BOTH fire modes** (rounds swell across their flight: 3× at resting Mass, 6× at Mass 10, linear in level over [-5, 15]) | `FullAutoBlockShootActionExecutor` — multiply `BlockScale.z` by `Multiplier(Mass)` at fire time, routed through `TargetScale` + `Prism.Initialize`. Growth: `FullAutoActionSO.ResolveGrowthFactor` → `Projectile.SetFlightGrowth`, scaling the drawn cross-section and the swept hit radius by the same factor every frame | **Shielded Prisms** — turret-fired prisms arrive with one-hit ablative octahedron armour and a wider hit sphere. Returned here from Space 5 by design sign-off (2026-08-13): **MASS owns the SUBSTANCE of what you fire, SPACE owns its REACH** | `FiredPrismState.ShieldedAtMass5` sets `prismProperties.IsShielded` before `Initialize`, off an `IsUpgradeActive(Mass)` snapshot taken per volley |
 | **Charge** | Skyburst blast radius | `FireGunActionExecutor.Fire`: replace the literal `0` with `Clamp01(GetLevel(Charge)/10)`; author real min/max on the three skyburst effect assets (the `Lerp(MinScale, MaxScale, Charge)` pipe already exists in `ProjectileDetonatorSO`) | **Skybursts spare the shooter's own domain** | Gate the direct-hit damage in `SkyBurstProjectileDamagePrismEffectSO` on domain when unlocked (per-shot flag plumbed like piercing). **The AOE follows the same per-shot flag** since 2026-08: `ProjectileDetonatorSO` passes `AffectSelfOverride = !SpareOwnDomain`, because the prefabs' authored `affectSelf: 0` had the blast sparing own domain at EVERY level — half the upgrade was pre-unlocked. Prereq: wire steal → `PrismSpatialIndex.UpdateDomain` so "own domain" is live |
 
 Presentation: `ElementalAbilityMaps/Sparrow.asset` re-authors the abandoned branch's verified
diff --git a/Docs/UNITY_VERIFICATION_CHECKLIST.md b/Docs/UNITY_VERIFICATION_CHECKLIST.md
index cc206356e..80e510ff9 100644
--- a/Docs/UNITY_VERIFICATION_CHECKLIST.md
+++ b/Docs/UNITY_VERIFICATION_CHECKLIST.md
@@ -332,7 +332,9 @@ tightening as it flies; ordinary prisms (trail/environment) must render unchange
```

</details>

### `b29c1533a` — feat(dolphin): make crystal seeding passive, freeing the right trigger

_Claude, 2026-08-14 21:53:20 +0000_

```text
Charge's ability no longer takes an input. A cooldown runs continuously and
each time it completes the Dolphin seeds a TEAM crystal at a random point in
the containing cell's CYTOPLASM - the shell between nucleus and membrane -
then restarts immediately. The seeded crystal is the Dolphin's own ammunition:
what it later flies into to release Echo Obliteration, so the seeding rate is
the blast's tempo.

- Radius is drawn VOLUME-uniformly across the band, not uniformly in radius:
  a shell's space grows as r-squared, so a uniform draw would crowd every
  seeding against the nucleus and leave the outer cytoplasm empty. Same rule
  the flora planting band follows.
- The inner edge is clamped outside the nucleus whatever the band fractions
  ask for - nucleus mass is the cell's territorial claim, so ability crystals
  must not seed into the sanctuary. This is NOT the omni-crystal respawn
  volume, which stays locked to the nucleus.
- At the live-crystal cap the seed clock PAUSES; it never culls a planted
  crystal. Not creating mass is allowed, aging it out is not.
- The SO is wired directly on the executor because a passive ability is bound
  to no input event, so the action handler's binding maps can never resolve it.
  The binding sweep survives only as a fallback.
- Twin Seed (Charge L5) reinterpreted from a carry limit to a per-cycle yield:
  each seeding plants two crystals. The HUD keeps its art - the icon becomes a
  pure recharge fill and the pips show the cycle's yield, so the mini crystal
  appearing still IS the upgrade becoming visible.

Seeding runs for the local pilot only, preserving the previous owner-only
scope: TeamCrystal.prefab carries no NetworkObject, so letting every peer run
the clock would have each roll its own placement and desync the field.
```

```text
 Assets/Resources/ElementalAbilityMaps/Dolphin.asset                   |  16 +-
 Assets/_Prefabs/Spacevessels/Dolphin.prefab                           |   4 +-
 Assets/_SO_Assets/VesselActions/Dolphin/DeployTeamCrystalAction.asset |  15 +-
 .../R_VesselActions/Data Containers/DeployTeamCrystalActionSO.cs      |  79 ++++---
 .../R_VesselActions/Data Containers/DolphinVesselHUDController.cs     |  24 ++-
 .../R_VesselActions/Executors/DeployTeamCrystalActionExecutor.cs      | 372 +++++++++++++++-----------------
 Assets/_Scripts/UI/View/DolphinVesselHUDView.cs                       |  69 +++---
 7 files changed, 288 insertions(+), 291 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 626 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/DeployTeamCrystalActionExecutor.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/DeployTeamCrystalActionExecutor.cs
index 7f7c4fce3..b7f11a838 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/DeployTeamCrystalActionExecutor.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/DeployTeamCrystalActionExecutor.cs
@@ -1,60 +1,71 @@
 using System;
 using System.Collections.Generic;
 using CosmicShore.Data;
-using CosmicShore.Utility;
-using DG.Tweening;
 using Obvious.Soap;
 using UnityEngine;
 
 namespace CosmicShore.Gameplay
 {
     /// <summary>
-    /// The Dolphin's crystal seeding. Hold the trigger and a preview crystal blooms out in front of
-    /// the nose; release and it is planted.
+    /// The Dolphin's crystal seeding — a <b>PASSIVE</b> ability with no input of its own. A cooldown
+    /// runs continuously; each time it completes the Dolphin seeds a TEAM crystal somewhere in the
+    /// containing cell's CYTOPLASM (the shell between nucleus and membrane) and the cooldown
+    /// restarts immediately.
     ///
     /// What gets planted is a TEAM crystal — only the pilot's own domain can collect it, exactly
     /// like the crystals Skim Race lays along its track. That gate is structural rather than
     /// conventional: TeamCrystal.prefab drops the base <see cref="OmniCrystalImpactor"/> in favour
     /// of a <see cref="TeamCrystalImpactor"/>, whose <c>IsDomainMatching</c> rejects every vessel
-    /// outside the crystal's domain in the impact chain itself. The PREVIEW says so too — it wears
-    /// the domain's crystal colours from the moment it appears, because crystal colour is already
-    /// the game's language for "who may collect this" (see <see cref="Crystal.ApplyColorSetTint"/>).
+    /// outside the crystal's domain in the impact chain itself.
+    ///
+    /// This is the Dolphin's own ammunition supply: the crystal it seeds is the crystal it later
+    /// flies into to release Echo Obliteration, so the seeding rate IS the blast's tempo.
     ///
     /// Element → parameter: CHARGE owns this ability. Its multiplier divides the recharge, and its
-    /// level-5 upgrade lets the Dolphin carry a second crystal so two can be planted back to back.
-    /// The HUD reads <see cref="ChargesAvailable"/> / <see cref="MaxCharges"/> for the slot pips and
-    /// <see cref="CooldownRemaining01"/> for the fill.
+    /// level-5 upgrade ("Twin Seed") doubles the yield per cycle. The HUD reads
+    /// <see cref="SeedsPerCycle"/> for the pip row, <see cref="CooldownRemaining01"/> for the fill,
+    /// and edge-detects <see cref="SeedCount"/> for the planted beat.
+    ///
+    /// <para><b>Locally simulated only.</b> <c>TeamCrystal.prefab</c> carries no NetworkObject, so a
+    /// seeded crystal has always been a local instantiate — the previous hold-to-plant version ran
+    /// on the owner's machine behind the action handler's <c>IsSpawned &amp;&amp; IsOwner</c> gate and
+    /// produced an owner-only crystal too. The clock therefore runs for the LOCAL PILOT's Dolphin
+    /// and no other, which preserves exactly that scope; letting every peer run it would have each
+    /// peer roll its own placement and desync the field outright. Networked seeding is a follow-up
+    /// and wants crystal network sync first (see DOLPHIN_CRYSTAL_SEEDING.md ▸ Follow-ups).</para>
     /// </summary>
     public sealed class DeployTeamCrystalActionExecutor : ShipActionExecutorBase
     {
-        static readonly int Opacity = Shader.PropertyToID("_opacity");
-
-        [Header("Scene Refs")]
+        [Header("Setup")]
+        [Tooltip("The TEAM crystal planted by each seeding. TeamCrystal.prefab.")]
         [SerializeField] private Crystal crystalPrefab;
 
+        [Tooltip("Tuning for the seeding. Wired directly because the ability is PASSIVE - it is " +
+                 "bound to no input event, so the action handler's binding maps can never resolve " +
+                 "it. Leave empty only if this vessel should not seed.")]
+        [SerializeField] private DeployTeamCrystalActionSO config;
+
         [Header("Events")]
         [SerializeField] private ScriptableEventNoParam OnMiniGameTurnEnd;
 
-        Crystal _ghostCrystal;
-        Vector3 _ghostRestScale = Vector3.one;
-        Tween _ghostTween;
-        MaterialPropertyBlock _fadeBlock;
-
         IVesselStatus _status;
 
-        // The SO carries the tuning, but it only reaches us through Begin/Commit. The HUD polls
-        // from frame zero, so resolve it lazily off the action handler the first time anything asks.
-        DeployTeamCrystalActionSO _so;
-        static readonly List<ShipActionSO> s_boundScratch = new();
+        // Live crystals this Dolphin has seeded. Compacted lazily; entries go null when a crystal
```

</details>

### `ee19169fd` — feat(dolphin): add Echo Sight on the freed right trigger

_Claude, 2026-08-14 22:02:44 +0000_

```text
Hold the right trigger and the view eases into a zoomed first-person shot down
the blast axis while every prism standing inside the next crystal blast's
destruction volume lights up. It fires nothing - the blast still goes off on a
crystal strike; the sight only makes the gape the pilot has been banking with
every skim legible as actual mass instead of an angle on the HUD.

SPACE owns it, alongside the blast it previews: Space already carries the cone
further down-range, and the sight is how that reach becomes readable.

Three view surfaces, three owners, and the split is load-bearing:
- Camera POSE is the executor's (it lerps the follow offset).
- FOV is NOT. It is pushed through the new VesselSpeedTunnel.SetHomeFovOverride
  so the speed tunnel stays the single FOV writer. An ability that writes
  Camera.fieldOfView itself is broken two ways and both are silent: the tunnel
  overwrites it every frame while engaged, and when the tunnel ENGAGES it
  captures whatever FOV it finds as the home to restore later - baking the zoom
  in permanently. This does not weaken the law: the speed->effect mapping is
  untouched and still absolute, only the home it measures down from moves, which
  is the same thing the player's own FOV slider already does mid-effect.
- The prism HIGHLIGHT is PrismDestructionSight's: three global uniforms per
  frame and zero per-prism work, the sanctioned shape for a view-dependent prism
  visual. A spatial-index sweep per frame purely to tint would be exactly the
  per-prism CPU pass the clock-material law exists to prevent.

The previewed volume is not re-derived. ExplosionHelper.TryResolveConicVolume
builds it from the same authored scales, the same energy read and the same Space
multiplier the detonation uses, and returns it in the form the Burst sweep tests
against - so preview and damage are the same shape by construction rather than
by two authors agreeing. The HLSL containment test is a literal transcription of
AOEConicSweepQueryJob, capsule-segment clamp included.
```

```text
 Assets/Resources/ElementalAbilityMaps/Dolphin.asset                   |  16 ++-
 Assets/_Graphics/Materials/Graphs/PrismDestructionSight.hlsl          | 138 +++++++++++++++++++++++
 Assets/_Graphics/Materials/Graphs/PrismDestructionSight.hlsl.meta     |   7 ++
 Assets/_Prefabs/Spacevessels/Dolphin.prefab                           |  50 +++++++++
 Assets/_SO_Assets/VesselActions/Dolphin/EchoSightAction.asset         |  18 +++
 Assets/_SO_Assets/VesselActions/Dolphin/EchoSightAction.asset.meta    |   8 ++
 .../Controller/ImpactEffects/EffectsSO/Helpers/ExplosionHelper.cs     |  92 +++++++++++++++
 .../Vessel Crystal Effects/VesselExplosionByCrystalEffectSO.cs        |  27 +++++
 Assets/_Scripts/Controller/Projectiles/AOEConicExplosion.cs           |   7 ++
 .../Vessel/R_VesselActions/Data Containers/EchoSightActionSO.cs       |  48 ++++++++
 .../Vessel/R_VesselActions/Data Containers/EchoSightActionSO.cs.meta  |  11 ++
 .../Vessel/R_VesselActions/Executors/EchoSightActionExecutor.cs       | 192 ++++++++++++++++++++++++++++++++
 .../Vessel/R_VesselActions/Executors/EchoSightActionExecutor.cs.meta  |  11 ++
 Assets/_Scripts/Utility/PrismDestructionSight.cs                      | 111 ++++++++++++++++++
 Assets/_Scripts/Utility/PrismDestructionSight.cs.meta                 |  11 ++
 Assets/_Scripts/Utility/VesselSpeedTunnel.cs                          |  71 +++++++++++-
 16 files changed, 809 insertions(+), 9 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 708 lines)</summary>

```diff
diff --git a/Assets/_Graphics/Materials/Graphs/PrismDestructionSight.hlsl b/Assets/_Graphics/Materials/Graphs/PrismDestructionSight.hlsl
new file mode 100644
index 000000000..ab2b2b196
--- /dev/null
+++ b/Assets/_Graphics/Materials/Graphs/PrismDestructionSight.hlsl
@@ -0,0 +1,138 @@
+// PrismDestructionSight.hlsl — the GPU side of the Dolphin's Echo Sight
+// (Docs/PRISM_ANIMATION.md §4.7, the "global uniform" shape for a view-dependent prism visual).
+//
+// PURPOSE. While the pilot holds the sight, every prism standing inside the volume the next
+// crystal blast would sweep lights up, so the gape they have been banking with every skim stops
+// being an abstract angle on the HUD and becomes the actual mass it is about to remove.
+//
+// WHY IT LIVES HERE AND NOT ON THE CPU. "Is this prism inside the blast" is live, per-frame,
+// per-prism data: the answer changes as the ship turns and as the energy meter fills. It can
+// therefore never be a per-prism stamp, and running the spatial index's conic sweep every frame
+// purely to tint would be exactly the per-prism CPU pass the clock-material law exists to
+// prevent. The law's sanctioned shape for this case is a GLOBAL uniform — one O(1) write per
+// frame that every prism reads, with zero per-prism CPU work, zero material swaps and zero
+// per-instance overrides. Same contract as PrismOcclusionCorridor.hlsl, which is its sibling.
+//
+// THE UNIFORMS (published by PrismDestructionSight.cs once per frame):
+//   float4 _PrismSightApex   — xyz = the blast's apex in world space,
+//                              w   = the cone's axial reach. w <= 0 means "sight off", which the
+//                                    very first branch below returns untouched.
+//   float4 _PrismSightAxis   — xyz = the sweep axis (unit),
+//                              w   = the capsule's RADIUS per unit depth.
+//   float4 _PrismSightGape   — xyz = the gape axis (unit, perpendicular to the sweep axis),
+//                              w   = the capsule's HALF-LENGTH per unit depth.
+//   float  _PrismSightStrength — highlight fade, 0-1, so the sight never pops on or off.
+//
+// THE VOLUME. Not a circular cone: the blast opens the way the jaws open. At axial depth s the
+// cross-section is a 2D STADIUM — a disc of radius (_PrismSightAxis.w · s) dragged along the gape
+// axis for ±(_PrismSightGape.w · s). So the shape is narrow across the beam at every charge and
+// wide across the gape in proportion to the energy banked. This is a literal transcription of
+// AOEConicSweepQueryJob.Execute (PrismSpatialIndex.cs): clamp onto the cross-section's segment
+// first, then measure distance to that point, which is what makes the ends round and is the same
+// point-to-segment distance the CapsuleCollider trigger uses. The preview and the damage volume
+// are the same shape BY CONSTRUCTION rather than by two authors agreeing.
+//
+// COST CONTRACT. A fragment with the sight off executes one compare (_PrismSightApex.w > 0) and
+// returns. With the sight on it costs one dot for the axial band, one reject, then ~12 ALU for the
+// segment distance — no texture, no extra varying beyond world position, no branch that diverges
+// across a prism (the whole prism is on one side of the test at typical prism sizes, and near the
+// boundary the branch is still coherent across a screen tile). Nothing here changes the render
+// queue, the batch, or the draw call count.
+//
+// WHY IT ADDS RATHER THAN TINTS. The highlight has to read against every prism tier and both
+// domains without being mistaken for one of them. REPLACING colour on a Jade prism lands in the
+// same space as the domain palette and says "this prism changed team"; ADDING light says "this one
+// is lit up", which is not a thing any tier's palette means, so the sight can never be confused
+// with mass state (Docs/PALETTE.md - the tier colours are the language, do not borrow their space).
+// The prism graphs are UNLIT and carry no Emission block, so on them additive-into-BaseColor IS
+// emission - which is also why this splices exactly like PrismOcclusionFade does: it takes the
+// graph's own colour in and hands the final colour back, so it composes instead of overwriting.
+//
+// It composes with the occlusion corridor for free: the corridor dissolves COVERAGE, not colour,
+// so a highlighted prism standing in the corridor thins out exactly like its neighbours instead of
+// punching through the ship.
+
+#ifndef PRISM_DESTRUCTION_SIGHT_INCLUDED
+#define PRISM_DESTRUCTION_SIGHT_INCLUDED
+
+// How much of the emission is a flat fill vs. an edge-weighted rim. A pure flat fill turns the
+// zone into a slab of solid colour and hides which prisms are which; weighting toward the volume's
+// BOUNDARY draws the blast's silhouette onto the mass instead.
+#ifndef PRISM_SIGHT_EDGE_POWER
+#define PRISM_SIGHT_EDGE_POWER 2.0
+#endif
+
+// Floor on the fill so mass deep inside the zone is still obviously marked, not just its rim.
+#ifndef PRISM_SIGHT_CORE_FILL
+#define PRISM_SIGHT_CORE_FILL 0.35
+#endif
+
+// The light the sight adds. Deliberately NOT a domain or tier colour (see the note above): a warm
+// white-hot cast that no palette tier owns, so "lit by the sight" can never be misread as "this
+// mass is shielded / danger / another team". Kept a #define rather than a uniform for the same
+// reason the occlusion kernel's dials are - it is a look decision, not a per-frame quantity.
+#ifndef PRISM_SIGHT_COLOR
```

</details>

### `070182d62` — feat(prisms): splice the Echo Sight highlight into the prism graphs

_Claude, 2026-08-14 22:06:13 +0000_

```text
The GPU half of the Dolphin's sight. PrismDestructionSight.hlsl transcribes
AOEConicSweepQueryJob's containment test literally - capsule-segment clamp
included - so the highlighted volume and the damaged volume are the same shape
by construction rather than by two authors agreeing.

wire_prism_destruction_sight.py splices it into BlockGraph and
ExplodingBlockGraph, the same census the occlusion corridor covers: coverage is
the point, because a prism material that cannot light up is a hole in a
targeting aid, and a targeting aid with holes is worse than none.

It ADDS light rather than tinting. Replacing colour on a Jade prism lands in the
domain palette's space and reads as "this prism changed team"; adding says "this
one is lit up", which no tier's palette means, so the sight can never be
confused with mass state. The prism graphs are Unlit and carry no Emission
block, so additive-into-BaseColor is how emission is expressed there - which is
why this splices exactly like PrismOcclusionFade: it takes the graph's own
colour in and hands the final colour back.

Uniforms are Vector3 rather than Vector4 because that is what the graphs can
clone a donor for exactly; packing scalars into w channels would have meant
synthesising a property type neither graph contains.

Verified: both graphs re-validate, the splice is idempotent, and the corridor,
flight-clock and backface-fade wirings all still pass their own --check.
```

```text
 Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph          | 873 +++++++++++++++++++++++++++++++++++-
 Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph | 873 +++++++++++++++++++++++++++++++++++-
 Assets/_Graphics/Materials/Graphs/PrismDestructionSight.hlsl      |  47 +-
 Assets/_Scripts/Utility/PrismDestructionSight.cs                  |  34 +-
 Tools/Shaders/wire_prism_destruction_sight.py                     | 423 +++++++++++++++++
 5 files changed, 2209 insertions(+), 41 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 593 lines)</summary>

```diff
diff --git a/Assets/_Graphics/Materials/Graphs/PrismDestructionSight.hlsl b/Assets/_Graphics/Materials/Graphs/PrismDestructionSight.hlsl
index ab2b2b196..3a8cfaeda 100644
--- a/Assets/_Graphics/Materials/Graphs/PrismDestructionSight.hlsl
+++ b/Assets/_Graphics/Materials/Graphs/PrismDestructionSight.hlsl
@@ -14,25 +14,29 @@
 // per-instance overrides. Same contract as PrismOcclusionCorridor.hlsl, which is its sibling.
 //
 // THE UNIFORMS (published by PrismDestructionSight.cs once per frame):
-//   float4 _PrismSightApex   — xyz = the blast's apex in world space,
-//                              w   = the cone's axial reach. w <= 0 means "sight off", which the
-//                                    very first branch below returns untouched.
-//   float4 _PrismSightAxis   — xyz = the sweep axis (unit),
-//                              w   = the capsule's RADIUS per unit depth.
-//   float4 _PrismSightGape   — xyz = the gape axis (unit, perpendicular to the sweep axis),
-//                              w   = the capsule's HALF-LENGTH per unit depth.
+//   float3 _PrismSightApex     — the blast's apex in world space.
+//   float3 _PrismSightAxis     — the sweep axis (unit).
+//   float3 _PrismSightGape     — the gape axis (unit, perpendicular to the sweep axis).
+//   float3 _PrismSightParams   — (height, coreRadiusPerUnitDepth, halfLengthPerUnitDepth).
+//                                height <= 0 means "sight off", which the very first branch
+//                                below returns untouched.
 //   float  _PrismSightStrength — highlight fade, 0-1, so the sight never pops on or off.
 //
+// All four vectors are Vector3 rather than Vector4 because that is what the prism graphs can
+// clone a donor for exactly — packing the scalars into w channels would have meant synthesising
+// a property type neither graph contains, which is precisely the kind of hand-authored schema the
+// asset-surgery protocol says not to invent.
+//
 // THE VOLUME. Not a circular cone: the blast opens the way the jaws open. At axial depth s the
-// cross-section is a 2D STADIUM — a disc of radius (_PrismSightAxis.w · s) dragged along the gape
-// axis for ±(_PrismSightGape.w · s). So the shape is narrow across the beam at every charge and
+// cross-section is a 2D STADIUM — a disc of radius (_PrismSightParams.y · s) dragged along the
+// gape axis for ±(_PrismSightParams.z · s). So it is narrow across the beam at every charge and
 // wide across the gape in proportion to the energy banked. This is a literal transcription of
 // AOEConicSweepQueryJob.Execute (PrismSpatialIndex.cs): clamp onto the cross-section's segment
 // first, then measure distance to that point, which is what makes the ends round and is the same
 // point-to-segment distance the CapsuleCollider trigger uses. The preview and the damage volume
 // are the same shape BY CONSTRUCTION rather than by two authors agreeing.
 //
-// COST CONTRACT. A fragment with the sight off executes one compare (_PrismSightApex.w > 0) and
+// COST CONTRACT. A fragment with the sight off executes one compare (_PrismSightParams.x > 0) and
 // returns. With the sight on it costs one dot for the axial band, one reject, then ~12 ALU for the
 // segment distance — no texture, no extra varying beyond world position, no branch that diverges
 // across a prism (the whole prism is on one side of the test at typical prism sizes, and near the
@@ -82,9 +86,10 @@
 
 void PrismDestructionSight_float(
     float3 PositionWS,
-    float4 Apex,        // xyz apex, w height
-    float4 Axis,        // xyz sweep axis, w core radius per unit depth
-    float4 Gape,        // xyz gape axis, w half-length per unit depth
+    float3 Apex,        // blast apex, world space
+    float3 Axis,        // sweep axis (unit)
+    float3 Gape,        // gape axis (unit, perpendicular to Axis)
+    float3 Params,      // (height, core radius per unit depth, half-length per unit depth)
     float  Strength,
     float3 BaseColor,
     out float3 Color)
@@ -97,27 +102,27 @@ void PrismDestructionSight_float(
 
     // Sentinel: the publisher zeroes every uniform when the sight is released, so an unheld
     // trigger costs exactly this compare.
-    float height = Apex.w;
+    float height = Params.x;
     if (height <= 0.0 || Strength <= 0.0)
         return;
 
-    float3 rel = PositionWS - Apex.xyz;
+    float3 rel = PositionWS - Apex;
 
     // Axial band. Outside [0, height] there is no blast at all - note the near clip is at the
     // apex, so mass BEHIND the vessel is never highlighted even though the cone's axis extends
     // backwards mathematically.
-    float s = dot(rel, Axis.xyz);
+    float s = dot(rel, Axis);
     if (s <= 0.0 || s > height)
         return;
 
     // Distance from the cross-section's SEGMENT, not from the axis: clamp onto the segment first
     // so the ends are round. Mirrors AOEConicSweepQueryJob exactly.
```

</details>

### `68964131a` — docs(dolphin): record the seeding/sight rework across the paper trail

_Claude, 2026-08-14 22:09:45 +0000_

```text
- DOLPHIN_CRYSTAL_SEEDING.md: the co-located design doc - what changed and why,
  the placement rules and which are load-bearing, the three view surfaces and
  why FOV is not the executor's, files, tuning knobs, the unverified in-editor
  checklist, and the follow-ups.
- FLEET_MAPS.md: Dolphin table + the 2026-08-14 swap note.
- SPEED_TUNNEL.md 2.1: the HOME override - why an ability must never write
  Camera.fieldOfView, and why moving home does not weaken the law.
- PRISM_ANIMATION.md 4.7.1: the sight as the second citizen of the
  global-uniform shape, and the three properties worth carrying forward.
- CLAUDE.md: fleet-status note with both general lessons.
- UNITY_VERIFICATION_CHECKLIST.md: a red entry, since none of this has been run.
```

```text
 .../Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_SEEDING.md      | 243 ++++++++++++++++++++++++++++++++
 CLAUDE.md                                                             |  18 +++
 Docs/ElementalAbilitySystem/FLEET_MAPS.md                             |  21 ++-
 Docs/PRISM_ANIMATION.md                                               |  34 +++++
 Docs/SPEED_TUNNEL.md                                                  |  38 +++++
 Docs/UNITY_VERIFICATION_CHECKLIST.md                                  |  44 ++++++
 6 files changed, 392 insertions(+), 6 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 471 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_SEEDING.md b/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_SEEDING.md
new file mode 100644
index 000000000..aef5049fd
--- /dev/null
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_SEEDING.md
@@ -0,0 +1,243 @@
+# Dolphin — passive crystal seeding, and the Echo Sight that aims the blast
+
+Design owner: Garrett. Map: `Assets/Resources/ElementalAbilityMaps/Dolphin.asset` — that asset and
+this file are the record. The energy economy, the drift boost and the four gauges are the sibling
+document, `DOLPHIN_ENERGY_ECONOMY.md`; this one covers the two abilities that changed when the
+right trigger was freed.
+
+---
+
+## 0. What changed and why
+
+The Dolphin held the right trigger to preview and plant a team crystal. That spent the vessel's
+only free input on an ability whose interesting part — *where the crystal is* — the pilot was
+already choosing by flying there. Meanwhile its signature ability, the crystal-impact cone
+("Echo Obliteration"), had no input at all and no way to be aimed: the pilot banked its gape with
+every skim and could read that gape only as an ANGLE, off the hull's jaws or the HUD's jaw icon,
+with no way to know what the angle actually covered out in the world.
+
+So the two swapped places:
+
+| element | before | after |
+|---|---|---|
+| **Charge** | Crystal Seeding — hold RT to preview, release to plant | Crystal Seeding — **passive**, seeds into the cytoplasm on a loop |
+| **Space** | Cone Blast — passive, fires on crystal impact | Echo Obliteration — the same blast, **plus the sight on RT** |
+
+Charge still owns the recharge; Space still owns the reach. Neither element→ability binding moved —
+only which of them carries an input.
+
+---
+
+## 1. Charge: passive crystal seeding
+
+A cooldown runs continuously. Each time it completes the Dolphin seeds a **team** crystal at a
+random point in the containing cell's **cytoplasm** — the shell between the nucleus surface and
+the membrane — and the cooldown restarts immediately. There is no input, no preview, and nothing
+carried.
+
+The seeded crystal is the Dolphin's own ammunition: it is what the vessel later flies into to
+release Echo Obliteration. **So the seeding rate is the blast's tempo**, which is what makes
+Charge's cooldown scaling matter.
+
+### The placement rules, and which are load-bearing
+
+| rule | why |
+|---|---|
+| Radius drawn **volume-uniformly** across the band (`cbrt(lerp(inner³, outer³, u))`) | A shell's available space grows as r². A uniform-in-radius draw crowds every seeding against the nucleus and leaves the outer cytoplasm — most of the actual volume — nearly empty. Same rule the flora planting band follows (CLAUDE.md ▸ Rampage §27.2). |
+| Inner edge **clamped outside the nucleus**, whatever `bandInnerFraction` says | Nucleus mass is the cell's territorial claim and a fauna sanctuary. Seeding ability crystals into it would make the sanctuary the place to farm. |
+| At the live cap the clock **pauses**, never culls | Not creating mass is allowed; aging it out is not (CLAUDE.md ▸ *Mass is conserved*). `maxLiveSeeded` bounds the field by declining to add, and a planted crystal is only ever removed by being collected. |
+| No cell to measure → seed in a ball around the vessel | Freestyle transit and tool scenes have no membrane. The ability degrades to doing something rather than silently stopping. |
+
+**This is not the omni-crystal respawn volume.** `CrystalManager.GetAnchorlessSpawnRadius` is
+LOCKED to the nucleus (CLAUDE.md ▸ Rampage §27.3) because the nucleus is the visible marker of "the
+middle" that every mode teaches players to contest. That governs the **cell's** own respawning
+crystal. This is a vessel ability planting its own team-locked crystal, and it deliberately seeds
+outside the nucleus for the reason in the table above. The two rules agree; they are about
+different crystals.
+
+### The trap this ability walks into
+
+**A passive ability is bound to no input event, so `CollectBoundActions` can never resolve its SO.**
+The executor's original lazy resolution swept every `InputEvents` value looking for its own action —
+correct while the ability was on the right trigger, and dead the moment it became passive. The SO is
+now wired **directly** on the executor (`config`), with the binding sweep kept only as a fallback for
+a vessel that still lists the action against an input. A missing wire is visible in the inspector.
+
+### Twin Seed (Charge L5)
+
+The upgrade used to raise a **carry** limit — meaningless once nothing is carried. It is now a
+**yield**: each seeding plants two crystals instead of one. The HUD art is unchanged; see §3.
+
+---
+
+## 2. Space: Echo Obliteration, and the Echo Sight
+
```

</details>

### `10db177f7` — fix(dolphin): zoom the Echo Sight from the tunnel's home, not the live camera

_Claude, 2026-08-14 22:10:31 +0000_

```text
Capturing Camera.fieldOfView at engage reads the value the speed tunnel has
ALREADY narrowed, so raising the sight at speed anchored the zoom to whatever
speed the pilot happened to be doing - and restored to that value on release.
Capture VesselSpeedTunnel.HomeFov instead, falling back to the live camera only
when the driver has never taken a camera over (nothing has narrowed it yet, so
the two agree).
```

```text
 Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_SEEDING.md          |  9 ++++++---
 .../_Scripts/Controller/Vessel/R_VesselActions/Executors/EchoSightActionExecutor.cs   | 17 +++++++++++++----
 Assets/_Scripts/Utility/VesselSpeedTunnel.cs                                          | 11 +++++++++++
 3 files changed, 30 insertions(+), 7 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_SEEDING.md b/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_SEEDING.md
index aef5049fd..e471afd1e 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_SEEDING.md
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_SEEDING.md
@@ -106,9 +106,12 @@ Two consequences worth knowing:
 - The tunnel now keeps **applying** at zero speed effect while an override is in force
   (`_effect01 > 0.001f || HasHomeFovOverride`). Releasing would restore the true home and cancel the
   sight.
-- `_capturedHomeFov` is captured **once at engage** and never re-read. Once the override is in force
-  the tunnel is writing the camera *from* that override, so reading the camera back each frame would
-  feed the zoom into its own input and run away.
+- `_capturedHomeFov` is captured **once at engage, from `VesselSpeedTunnel.HomeFov` — not from the
+  live camera**. While the tunnel is engaged the camera is already narrowed by the speed effect, so
+  reading it would anchor the zoom to whatever speed the pilot happened to be doing when they raised
+  the sight, and restore to that value on release. It is not re-read live either: once the override
+  is in force the tunnel is writing the camera *from* that override, so reading it back would feed
+  the zoom into its own input and run away.
 
 ### The highlighted volume is not re-derived
 
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/EchoSightActionExecutor.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/EchoSightActionExecutor.cs
index 16c317f21..d29a65e21 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/EchoSightActionExecutor.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/EchoSightActionExecutor.cs
@@ -44,9 +44,10 @@ namespace CosmicShore.Gameplay
         Vector3 _neutralFollowOffset;
         CustomCameraController _camera;
 
-        // The FOV the zoom measures DOWN from, captured at engage. It must NOT be re-read live:
-        // once the override is in force the tunnel is writing the camera from that override, so
-        // reading the camera back would feed the zoom into its own input and run away.
+        // The FOV the zoom measures DOWN from, captured at engage from the TUNNEL's home rather
+        // than the live camera (see CaptureView). It must NOT be re-read live either: once the
+        // override is in force the tunnel is writing the camera from that override, so reading the
+        // camera back would feed the zoom into its own input and run away.
         float _capturedHomeFov;
 
         public override void Initialize(IVesselStatus shipStatus)
@@ -140,7 +141,15 @@ namespace CosmicShore.Gameplay
             if (_camera)
             {
                 _neutralFollowOffset = _camera.GetFollowOffset();
-                _capturedHomeFov = _camera.Camera != null ? _camera.Camera.fieldOfView : 0f;
+
+                // The TUNNEL's home, not the live camera. While the tunnel is engaged the camera is
+                // already narrowed by the speed effect, so reading it here would anchor the zoom to
+                // whatever speed the pilot happened to be doing when they raised the sight - and
+                // restore to that value on release. Falls back to the live camera only when the
+                // driver has never taken a camera over (nothing has narrowed it yet, so they agree).
+                float home = VesselSpeedTunnel.HomeFov;
+                if (home <= 0f && _camera.Camera != null) home = _camera.Camera.fieldOfView;
+                _capturedHomeFov = home;
             }
 
             _cameraCaptured = true;
diff --git a/Assets/_Scripts/Utility/VesselSpeedTunnel.cs b/Assets/_Scripts/Utility/VesselSpeedTunnel.cs
index 1c12ba150..4cdb721fd 100644
--- a/Assets/_Scripts/Utility/VesselSpeedTunnel.cs
+++ b/Assets/_Scripts/Utility/VesselSpeedTunnel.cs
@@ -82,6 +82,17 @@ namespace CosmicShore.Utility
         /// <summary>True while a sighting ability is holding the home FOV somewhere else.</summary>
         public static bool HasHomeFovOverride => _homeFovOverride > 0f;
 
+        /// <summary>
+        /// The TRUE home field of view — what the camera would be running at with the tunnel idle.
+        /// 0 before the driver has ever taken a camera over.
+        ///
+        /// A sighting ability must zoom from THIS, never from <c>Camera.fieldOfView</c>: while the
+        /// tunnel is engaged the live camera is already narrowed by the speed effect, so capturing
+        /// it would anchor the zoom to whatever speed the pilot happened to be doing at the moment
+        /// they raised the sight — and would then restore to that value on release.
+        /// </summary>
+        public static float HomeFov => _homeFov;
+
         /// <summary>
         /// The FOV the effect currently measures DOWN from: an ability's override when one is in
         /// force, otherwise the true home captured off the live camera.
```

</details>

### `43c5617b1` — refactor(dolphin): cut the Echo Sight's zoom, keep the highlight

_Claude, 2026-08-14 23:54:43 +0000_

```text
The sight now touches nothing but photons: hold RT and the prisms inside the
blast's destruction volume light up, with no camera pose write and no FOV
change. The highlight was always the ability; the zoom was framing around it.

This also reverts the speed tunnel to its pre-branch state, byte for byte.
SetHomeFovOverride existed for exactly one caller, so with the zoom gone it
would have been a new public FOV surface on a LOCKED platform law with nothing
calling it - dead API is worse on a law than anywhere else, because the next
reader takes its existence as permission. Docs/SPEED_TUNNEL.md 2.1 goes with it.

What the zoom taught is kept in DOLPHIN_CRYSTAL_SEEDING.md 2 rather than in
code: an ability that writes Camera.fieldOfView directly is overwritten every
frame while the tunnel is engaged, and when the tunnel ENGAGES it captures
whatever FOV it finds as the home to restore later - so a live zoom is baked in
permanently. A zoom must move the tunnel's HOME, never the camera. Recorded as
an open idea, not a rejected one; nobody has played it.
```

```text
 Assets/_SO_Assets/VesselActions/Dolphin/EchoSightAction.asset         |   2 -
 .../Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_SEEDING.md      |  89 ++++++++++--------------
 .../Vessel/R_VesselActions/Data Containers/EchoSightActionSO.cs       |  29 +++-----
 .../Vessel/R_VesselActions/Executors/EchoSightActionExecutor.cs       | 119 +++++---------------------------
 Assets/_Scripts/Utility/VesselSpeedTunnel.cs                          |  82 +---------------------
 CLAUDE.md                                                             |  22 +++---
 Docs/ElementalAbilitySystem/FLEET_MAPS.md                             |   7 +-
 Docs/SPEED_TUNNEL.md                                                  |  38 ----------
 Docs/UNITY_VERIFICATION_CHECKLIST.md                                  |  19 +++--
 9 files changed, 92 insertions(+), 315 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 603 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_SEEDING.md b/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_SEEDING.md
index e471afd1e..72cf97faa 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_SEEDING.md
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_SEEDING.md
@@ -23,6 +23,10 @@ So the two swapped places:
 | **Charge** | Crystal Seeding — hold RT to preview, release to plant | Crystal Seeding — **passive**, seeds into the cytoplasm on a loop |
 | **Space** | Cone Blast — passive, fires on crystal impact | Echo Obliteration — the same blast, **plus the sight on RT** |
 
+The Echo Sight originally also pushed the camera into a zoomed first-person view. **That half was
+cut** (2026-08-14, same day) — the highlight alone carries the ability, and dropping the zoom also
+dropped the only reason to touch the speed tunnel's FOV at all. See §2.
+
 Charge still owns the recharge; Space still owns the reach. Neither element→ability binding moved —
 only which of them carries an input.
 
@@ -72,46 +76,30 @@ The upgrade used to raise a **carry** limit — meaningless once nothing is carr
 
 ## 2. Space: Echo Obliteration, and the Echo Sight
 
-Hold the right trigger and the view eases into a zoomed first-person shot down the blast axis, with
-every prism standing inside the destruction volume lit up. Release and it eases back. **It fires
-nothing** — the blast still goes off when the Dolphin strikes a crystal. The sight only makes the
-shape legible so the pilot can choose which way to be pointing when they take the crystal.
+Hold the right trigger and every prism standing inside the blast's destruction volume lights up.
+Release and the highlight fades away. **It fires nothing and it moves nothing** — the blast still
+goes off when the Dolphin strikes a crystal, and the camera is left entirely alone. The sight only
+makes the shape legible so the pilot can choose which way to be pointing when they take the
+crystal.
 
-### Three view surfaces, three owners — and the split is load-bearing
+### It touches nothing but photons
 
-| surface | owner | why |
-|---|---|---|
-| Camera **pose** | `EchoSightActionExecutor` | It lerps `CustomCameraController`'s follow offset. Ordinary, no law involved — the speed tunnel is explicitly a no-camera-distance-change effect. |
-| Camera **FOV** | **`VesselSpeedTunnel`**, never the executor | See below. |
-| Prism **highlight** | `PrismDestructionSight` | Global uniforms, zero per-prism work. |
-
-**Why the sight must not write `Camera.fieldOfView`.** It is broken two ways and both are silent:
-
-1. While the tunnel is engaged it overwrites the ability every frame — the zoom simply does nothing
-   above walking pace.
-2. When the tunnel *engages*, it captures whatever FOV it finds as the home to restore later
-   (`Apply`: `if (cam != _appliedCamera) _homeFov = cam.fieldOfView`). A zoom active at that instant
-   is **baked in permanently** and the player never gets their FOV back.
-
-So the sight pushes a **home** through `VesselSpeedTunnel.SetHomeFovOverride` and the tunnel stays
-the single writer. **This does not weaken the law** (`Docs/SPEED_TUNNEL.md` §1): the speed→effect
-mapping is untouched and still absolute — the drop is still `fovDrop × effect01`, fleet-wide, with
-no per-vessel number anywhere. What moves is the home it measures down from, and home was always a
-live value rather than a constant: the player's own FOV slider moves it mid-effect through the same
-path (`OnHomeFieldOfViewChanged`). A sighting zoom is that same class of thing. A zoomed-in Dolphin
-at speed sits exactly as deep in the tunnel as an un-zoomed one.
-
-Two consequences worth knowing:
-
-- The tunnel now keeps **applying** at zero speed effect while an override is in force
-  (`_effect01 > 0.001f || HasHomeFovOverride`). Releasing would restore the true home and cancel the
-  sight.
-- `_capturedHomeFov` is captured **once at engage, from `VesselSpeedTunnel.HomeFov` — not from the
-  live camera**. While the tunnel is engaged the camera is already narrowed by the speed effect, so
-  reading it would anchor the zoom to whatever speed the pilot happened to be doing when they raised
-  the sight, and restore to that value on release. It is not re-read live either: once the override
-  is in force the tunnel is writing the camera *from* that override, so reading it back would feed
-  the zoom into its own input and run away.
+The whole ability is `PrismDestructionSight`'s global uniforms, published while the trigger is
+held. No camera write of any kind, no speed change, no input mute, nothing replicated.
+
+That is a deliberate narrowing. The first cut of this ability also eased the camera into a zoomed
+first-person shot down the blast axis, which meant moving the field of view — and FOV is owned
+fleet-wide by the speed tunnel (`Docs/SPEED_TUNNEL.md`), a LOCKED law with exactly one sanctioned
+hold. Composing with it cleanly was possible (the ability declared a *home* and the tunnel stayed
+the single writer), but it cost the law a new public surface for one vessel's view effect. **The
+zoom was cut instead**, and the tunnel is untouched.
+
+Worth keeping if a zoom is ever revisited, because the failure modes are silent: an ability that
+writes `Camera.fieldOfView` directly is overwritten every frame while the tunnel is engaged, and
+when the tunnel *engages* it captures whatever FOV it finds as the home to restore later
+(`Apply`: `if (cam != _appliedCamera) _homeFov = cam.fieldOfView`) — so a zoom live at that instant
+is baked in permanently and the player never gets their FOV back. A zoom must therefore move the
```

</details>

### `fbd991d08` — feat(dolphin): a prism ram costs half the banked BOOST as well as half the energy

_Claude, 2026-08-14 23:57:21 +0000_

```text
Energy already halved on a ram (DolphinVesselChangeResourceByPrismEffect on
resource slot 0). Boost did not: the meter the Dolphin fills by drifting
survived a collision intact, so ramming mass cost the pilot one bank and not
the other.

Adds VesselChangeBoostByPrismEffectSO, authored as
DolphinVesselChangeBoostByPrismEffect (slot 1) and appended to
DolphinImpactorDataContainer.vesselPrismEffects.

Halving the meter is not by itself "half the boost". VesselTransformer's
CurrentBoostAmount multiplies two terms during a discharge and only one
re-reads the meter: BoostMultiplier is recomputed every 0.1 s discharge tick
(so the meter write covers it), while ChargedBoostCharge is pinned at the
value the charge ended on and never read again. Left alone that snapshot
keeps paying full price on half the product and a ram mid-boost is barely
felt, so it is scaled by the same fraction - exact without touching the
action SO, since the term is 1 + (max - 1) x meter. Scoped to
IsChargedBoostDischarging, the only state that reads it.

VesselStatus.BoostMultiplier is deliberately left alone: it is a SERIALIZED,
authored field (4 on the Dolphin) that boost sources fall back to when they
do not write it themselves (BoostActionSO only flips IsBoosting;
VesselResetBoostPrismEffectSO restores it to an authored base). Scaling it in
place would ratchet that authored number toward 1 on every ram, permanently,
with nothing to restore it - a creeping nerf wearing a punish's costume.

Also promotes the hardcoded 0.5 in VesselChangeResourceByPrismEffectSO to an
authored retainedFraction (written as 0.5, no behaviour change) so the two
halves of the ram punish can be tuned apart after a play-test.

Docs: DOLPHIN_ENERGY_ECONOMY.md gains the boost half of the ram, the reason
BoostMultiplier is off limits, and three verification rows.
```

```text
 .../VesselContainers/DolphinImpactorDataContainer.asset               |  1 +
 .../Vessel Prism Effects/DolphinVesselChangeBoostByPrismEffect.asset  | 16 +++++++++
 .../DolphinVesselChangeBoostByPrismEffect.asset.meta                  |  8 +++++
 .../DolphinVesselChangeResourceByPrismEffect.asset                    |  1 +
 .../Vessel Prism Effects/VesselChangeBoostByPrismEffectSO.cs          | 57 +++++++++++++++++++++++++++++++++
 .../Vessel Prism Effects/VesselChangeBoostByPrismEffectSO.cs.meta     |  2 ++
 .../Vessel Prism Effects/VesselChangeResourceByPrismEffectSO.cs       |  8 +++--
 .../Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md       | 53 ++++++++++++++++++++++++++++--
 8 files changed, 141 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 85 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md b/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md
index 64ab3e954..2f2d31c07 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md
@@ -15,7 +15,14 @@ The Dolphin has **two** resources and they are not the same thing:
 | slot | name | who writes it | passive gain |
 |---|---|---|---|
 | 0 | **Energy** | skim / prism ram / crystal impact | none |
-| 1 | **Boost** | `ChargeBoostActionExecutor` only | none |
+| 1 | **Boost** | `ChargeBoostActionExecutor` + the prism ram | none |
+
+**A prism ram costs HALF of BOTH meters.** Energy and Boost are separate resources with separate
+sinks, but they share one punish: fly into mass and you lose half of everything you banked. The
+two halves are authored as two effects in `DolphinImpactorDataContainer.vesselPrismEffects`
+(`DolphinVesselChangeResourceByPrismEffect` on slot 0, `DolphinVesselChangeBoostByPrismEffect`
+on slot 1), each with its own `retainedFraction` (0.5) so they can be tuned apart if the ram
+turns out to bite harder on one than the other.
 
 **Energy** is banked by skimming and spent in ONE shot on a crystal:
 
@@ -155,6 +162,42 @@ discharge, and cancelling that task only throws *inside* the loop — it never r
 that restores the speed. Without the clear, anyone who drifted twice in a row kept a partial
 boost multiplier permanently.
 
+### A ram halves the boost — and the meter is only half of "the boost"
+
+`DolphinVesselChangeBoostByPrismEffect` scales resource slot 1 by `retainedFraction`, and that
+alone would barely be felt mid-boost, because the meter is not the only thing driving the speed.
+`CurrentBoostAmount()` above multiplies **two** terms during a discharge and only one of them
+re-reads the meter:
+
+| term | who writes it | re-reads the meter? |
+|---|---|---|
+| `BoostMultiplier` | the discharge loop, every 0.1 s tick | **yes** — self-corrects |
+| `ChargedBoostCharge` | pinned at the value the CHARGE ended on | **no** — never read again |
+
+`BoostMultiplier` therefore needs nothing: halving the meter halves it on the next tick, for
+free. The **pinned snapshot does** — left alone it keeps paying full price on half the product,
+and nothing ever re-reads it, so a ram mid-boost would barely be felt. The effect scales it by
+the same fraction, which is exact without any reference to `ChargeBoostActionSO`: the term is
+`1 + (maxBoostMultiplier − 1) × meter`, so scaling the meter by `f` is scaling its distance
+above 1 by `f`. It is scaled **only while `IsChargedBoostDischarging`** — the one state
+`CurrentBoostAmount` reads it in; outside a discharge it is stale bookkeeping that the next
+`BeginCharge` overwrites anyway.
+
+**`BoostMultiplier` is deliberately never written by this effect**, and that is not an
+optimization — it is a serialized, *authored* field on `VesselStatus` (4 on the Dolphin) that
+boost sources fall back to when they don't write it themselves (`BoostActionSO` only flips
+`IsBoosting`; `VesselResetBoostPrismEffectSO` restores it to an authored base). Scaling it in
+place would ratchet that authored number toward 1 a little further on every ram, permanently,
+with nothing in the game to restore it — a creeping nerf disguised as a punish. The meter is
+the only durable thing a ram may touch.
+
+Concretely, ramming at the peak of a full discharge: meter 1 → 0.5, `ChargedBoostCharge`
+2.259 → 1.630 immediately, `BoostMultiplier` 2.259 → 1.630 within one 0.1 s tick — speed factor
+5.10 → 2.66, and the discharge runs out in half the time it had left. The HUD's boost ring
+follows for free: `Resource.CurrentAmount`'s setter always raises `OnResourceChange`, which is
+what `DolphinVesselHUDController.PushDriftBoost` binds to, so the ring drops on the ram whether
+the executor or an impact effect wrote the meter.
+
 ### The drift is a momentum-preserving slide — the whole velocity is frozen, not just its direction
 
 The Dolphin authors `driftDamping: 0` (`DolphinDriftAction.asset`), so its drift already froze the
@@ -369,6 +412,10 @@ Play Menu_Main, enter freestyle on the Dolphin.
 | cross ~85% energy | Time icon's jaws start blending white → lime; solid lime at full |
 | ram a prism at full | gape halves AND the jaws drop back to white |
 | ram a prism | gape halves |
+| bank a full boost ring, then ram a prism before releasing | ring drops to half a step-for-step; the following release peaks near cruise+half, not 357 |
+| ram a prism at the PEAK of a discharge | speed drops within a tick, and the boost runs out in half the time it had left |
+| ram a prism with an empty boost ring | nothing happens to speed — half of zero is zero |
+| ram prisms repeatedly, then trigger any OTHER boost source | it is as strong as it ever was — a ram scales the meter, never the vessel's authored `boostMultiplier` |
 | hit a crystal | blast fires, gape snaps back to the 4.76° rest, Space icon flashes with a prism count |
 | blast at full energy | destruction is a FAN — wide across the jaw plane, narrow across the beam |
 | full throttle, no boost | `VesselStatus.Speed` settles at **78** (was 60) |
@@ -392,7 +439,9 @@ family (`GameCanvas.prefab`, `Panels/MiniGameHUD.prefab`, `Panels/VesselHUD.pref
 place: they hold no renderers, and `GameCanvas` is the shared prefab of `Docs/GAMECANVAS.md`.
 
 Knobs, in order of likely tuning: `DolphinSkimmerChangeResourceByPrismEffect._resourceAmount`
-(skim gain), `ChargeBoostAction.chargeTimeToFull` / `dischargeTimeToEmpty` /
+(skim gain), `DolphinVesselChangeResourceByPrismEffect.retainedFraction` /
```

</details>

### `c27d310e7` — fix(dolphin): Unity fake-null guard on the boost-ram effect, document the drift case

_Claude, 2026-08-14 23:59:02 +0000_

```text
Two follow-ups from a verification sweep of the previous commit.

`vesselImpactor?.Vessel` used C# null-propagation on a UnityEngine.Object,
which bypasses Unity's fake-null operator - a destroyed impactor is not C#
null and walks straight through. Guards with `if (!vesselImpactor) return;`
instead, matching VesselResetBoostPrismEffectSO. Cosmetic today (the
dispatcher always has a live `this`) but it is the house shape.

Also records the one state the effect's comment did not cover: ramming while
STILL DRIFTING is repaid, because the charge loop is running and refills the
halved meter. That is intended - the ram costs drift-seconds rather than a
bank, and the pilot is still doing the thing that banks boost. The punish
stays durable mid-discharge and between boosts, where nothing refills it.

The sweep also confirmed: the effect compiles against the real API surface;
the only other writer of the Dolphin's boost slot (ChargeBoostActionExecutor
.SetUnits) re-reads the meter every tick, so a mid-discharge halving is
respected and the discharge correctly ends sooner; VesselResetBoostPrismEffect
and the skimmer boost effects that write BoostMultiplier on prism contact are
wired to the SQUIRREL only, not the Dolphin; VesselTransformer.DecayBoost is
off on the Dolphin prefab; and nothing mutates or length-validates
vesselPrismEffects at runtime, so appending is safe.
```

```text
 .../ImpactEffects/EffectsSO/Vessel Prism Effects/VesselChangeBoostByPrismEffectSO.cs        | 11 ++++++++++-
 Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md                 |  8 ++++++++
 2 files changed, 18 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md b/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md
index 2f2d31c07..8777089cb 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md
@@ -191,6 +191,13 @@ place would ratchet that authored number toward 1 a little further on every ram,
 with nothing in the game to restore it — a creeping nerf disguised as a punish. The meter is
 the only durable thing a ram may touch.
 
+**Ramming while still DRIFTING is repaid, and that is intended.** The charge loop is running, so
+it refills the halved meter from where the ram left it and re-derives `ChargedBoostCharge` along
+the way — a ram taken mid-drift costs the pilot drift-*seconds*, not a bank. The pilot is still
+doing the thing that banks boost; there is no reason for the meter to stay punched while they do
+it. The punish is durable in the two states that matter: mid-discharge (below) and between
+boosts, where nothing refills it.
+
 Concretely, ramming at the peak of a full discharge: meter 1 → 0.5, `ChargedBoostCharge`
 2.259 → 1.630 immediately, `BoostMultiplier` 2.259 → 1.630 within one 0.1 s tick — speed factor
 5.10 → 2.66, and the discharge runs out in half the time it had left. The HUD's boost ring
@@ -415,6 +422,7 @@ Play Menu_Main, enter freestyle on the Dolphin.
 | bank a full boost ring, then ram a prism before releasing | ring drops to half a step-for-step; the following release peaks near cruise+half, not 357 |
 | ram a prism at the PEAK of a discharge | speed drops within a tick, and the boost runs out in half the time it had left |
 | ram a prism with an empty boost ring | nothing happens to speed — half of zero is zero |
+| ram a prism WHILE holding the drift | ring drops, then climbs again from there — the ram cost drift-seconds, not the bank |
 | ram prisms repeatedly, then trigger any OTHER boost source | it is as strong as it ever was — a ram scales the meter, never the vessel's authored `boostMultiplier` |
 | hit a crystal | blast fires, gape snaps back to the 4.76° rest, Space icon flashes with a prism count |
 | blast at full energy | destruction is a FAN — wide across the jaw plane, narrow across the beam |
```

</details>

### `8371eebeb` — chore(dolphin): ship-deep review pass — doc drift, meta, and a cap ratchet

_Claude, 2026-08-15 00:48:00 +0000_

```text
Findings from /ship-deep, all fixed here:

- D1 (adversarial): OnTurnEndOfMiniGame cleared the live-crystal roster. That is
  cap accounting, not turn state, and the crystals it counts are still standing -
  a mode running several turns without a scene reload would hand the next turn a
  fresh budget on top of last turn's crystals and the cap would ratchet open a
  turn at a time. Stop clearing it; CompactLive already frees the budget as
  crystals are collected, which is the only correct way for it to free.
- D4 (asset integrity): DOLPHIN_CRYSTAL_SEEDING.md was the only .md in its folder
  with no .meta. Added.
- D6 (doc drift): RAMPAGE.md described the ability as hold-to-plant with a carry
  limit. It is Dolphin-only and its whole intensity ladder is crystal scarcity, so
  it now records that seeding went passive (which SHARPENS the tension - a seeded
  crystal costs flight time to reach, while the arena crystal is the one you can
  already be standing on), names maxLiveSeeded as the knob most able to dilute
  that scarcity, and records that its objective arrow is already correct because
  it filters on CrystalManager == null. DOLPHIN_ENERGY_ECONOMY.md's gauge table
  and the superseded Charge-L5 checklist row were still describing carried
  crystals.
- Verification honesty: the Dolphin checklist entry goes from RED/UNVERIFIED to
  VERIFIED after Garrett's play-test, with the two rows one editor cannot reach
  (MPPM, and the ~4-minute live-cap fill) still called out as unverified.

Skill capture (/ship 3.5) - vessel skill:
- Amended rule 6, which told you to resolve an executor's SO via
  CollectBoundActions. That is wrong for a passive ability and this branch is how
  we found out.
- Added rule 20 (a passive ability is in no binding map, so wire its config
  directly) and rule 21 (an ability wanting FOV must move the speed tunnel's HOME,
  never Camera.fieldOfView - and check it still earns the surface without the
  zoom first).
- Fixed a pre-existing merge collision on bleeding-edge: the Sparrow branch's
  fire-rate rule and the flight-model rule were BOTH numbered 16. Renumbered
  16-18 to 17-19; the header said sixteen rules and there are now twenty-one.
```

```text
 .claude/skills/vessel/SKILL.md                                          | 27 ++++++++++++++++++++++-----
 Assets/_Scripts/Controller/Arcade/RAMPAGE.md                            | 31 +++++++++++++++++++++++++------
 .../Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_SEEDING.md        |  7 +++++--
 .../Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_SEEDING.md.meta   |  7 +++++++
 .../Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md         | 14 +++++++++-----
 .../Vessel/R_VesselActions/Executors/DeployTeamCrystalActionExecutor.cs | 12 +++++++++---
 Docs/UNITY_VERIFICATION_CHECKLIST.md                                    | 21 ++++++++++++++-------
 7 files changed, 91 insertions(+), 28 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 241 lines)</summary>

```diff
diff --git a/.claude/skills/vessel/SKILL.md b/.claude/skills/vessel/SKILL.md
index 7200e419a..5b2f0d65c 100644
--- a/.claude/skills/vessel/SKILL.md
+++ b/.claude/skills/vessel/SKILL.md
@@ -103,7 +103,7 @@ un-implemented until Garrett marks them up. If your task requires a mapping that
 STOP and ask (AskUserQuestion), presenting the FLEET_MAPS proposal for that row. The same gate
 applies to new abilities, new resources on the meter list, and anything that adds a fundamental.
 
-## 4. Implement — the sixteen rules that keep getting relearned
+## 4. Implement — the twenty-one rules that keep getting relearned
 
 1. **Ability SOs are shared and stateless.** Per-vessel state lives in executors / vessel-root
    MonoBehaviours; SOs receive `(registry, status)` per call. Never bind state to an SO asset.
@@ -126,7 +126,9 @@ applies to new abilities, new resources on the meter list, and anything that add
    shared SOAP channels. This exact bug shipped three times on one branch.
 6. **Executor→SO resolution retries until success** — `R_VesselActionHandler.Initialize` runs
    executors *before* populating its binding maps, so a first-frame query that latches on
-   attempt (not success) pins null forever. Resolve lazily via `CollectBoundActions`.
+   attempt (not success) pins null forever. Resolve lazily via `CollectBoundActions` — **but
+   only for an ability that HAS an input.** See rule 20: a passive ability is in no binding
+   map, so that sweep can never find its SO.
 7. **One authored number per displayed quantity.** A HUD readout adopts the gameplay component's
    value (`RiptideAnimation.MaxJawAngleDegrees` pattern); never author a "keep in step" copy.
    Bind HUD gauges **by name** with index fallback, and only to resources whose writers raise
@@ -196,23 +198,38 @@ applies to new abilities, new resources on the meter list, and anything that add
     `_speedTrackingRate` is a latched ramp state (the Rhino's ramp boost) that a naive early-return
     can silently consume.
 
-16. **A `UniTask.Delay(1/rate)` fire loop quantizes to WHOLE FRAMES**, so an authored rate is
+17. **A `UniTask.Delay(1/rate)` fire loop quantizes to WHOLE FRAMES**, so an authored rate is
     silently `min(rate, framerate)` — a 60 fps client fires twice as fast as a 30 fps one, and
     the rate simply cannot exceed the frame rate. It looks correct at any rate whose interval
     happens to straddle two frames (30/s at 60 fps was right by luck for a year). Owe fire in
     SECONDS and pay it off in whole volleys (`owed += Time.deltaTime`; fire `floor(owed/interval)`),
     capping the per-tick catch-up and DROPPING the excess so a hitch never discharges as a burst.
-17. **Never draw from `UnityEngine.Random` in a per-shot hot path.** It is global state that
+18. **Never draw from `UnityEngine.Random` in a per-shot hot path.** It is global state that
     deterministic systems seed (`Random.InitState` for the HexRace track), so a gun rolling it
     120×/s makes their output depend on how long someone held a trigger. Use a pure integer hash
     of a per-shot serial: no global state, and peers that agree on the shot count agree on the
     result — which matters wherever the spawned object is local and unreplicated.
-18. **Weapon "feel" complaints are usually a CEILING, not a tuning value.** Before re-tuning,
+19. **Weapon "feel" complaints are usually a CEILING, not a tuning value.** Before re-tuning,
     find what caps output per unit of input: prisms have no HP (one hit = one kill) and a
     sub-upgrade round dies on its first impact, so a Sparrow's ceiling is exactly *rounds/s*.
     Rate, spread and accuracy all multiply a 1:1 relationship and cannot break it — only pierce
     depth, chain effects, or **size** can, and size wins because destruction footprint goes as the
     SQUARE of the radius. Say which ceiling you found before proposing numbers.
+20. **A PASSIVE ability is bound to no input event, so `CollectBoundActions` can never resolve
+    its SO.** The binding maps are keyed by `InputEvents`; an ability with no input is in none
+    of them, so the lazy sweep of rule 6 returns null forever and the executor silently runs on
+    its field initializers — an ability that looks wired, logs nothing, and is tuned by an asset
+    nobody is reading. Wire the config **directly on the executor** as a `[SerializeField]`, so a
+    missing wire is visible in the inspector, and keep the sweep only as a fallback for a vessel
+    that still lists the action against an input. (Dolphin crystal seeding, 2026-08-14.)
+21. **An ability that wants the camera's FOV must move the speed tunnel's HOME, never
+    `Camera.fieldOfView`.** `VesselSpeedTunnel` owns FOV fleet-wide and is the only writer. A
+    direct write fails two ways, both silent: while the tunnel is engaged it is overwritten every
+    frame, and when the tunnel ENGAGES it captures whatever FOV it finds as the home to restore
+    later — so a live zoom is baked in permanently and the player never gets their FOV back.
+    Camera POSE is free (the law is explicitly a no-camera-distance-change effect); FOV is not.
+    And before adding a public FOV surface to that law for one vessel, check the ability still
+    earns it without the zoom — the Dolphin's Echo Sight did, and the surface was reverted.
 
 ## 5. Audit, then hand back verification (you cannot run Unity; the human is the gate)
 
diff --git a/Assets/_Scripts/Controller/Arcade/RAMPAGE.md b/Assets/_Scripts/Controller/Arcade/RAMPAGE.md
index 85f1b049a..a5e61576e 100644
--- a/Assets/_Scripts/Controller/Arcade/RAMPAGE.md
+++ b/Assets/_Scripts/Controller/Arcade/RAMPAGE.md
@@ -104,12 +104,31 @@ for anyone — and the whole intensity ladder, which is *made of* that scarcity,
 stop meaning anything.
 
 **The Dolphin can still make its own crystals, and that is deliberate.** Crystal
-Seeding (its Charge ability) plants a TEAM crystal only the pilot's domain can collect,
-on a **30 s** cooldown (→ ~15 s at Charge 10; two charges at Charge 5). So the arena
-crystal is not the *only* trigger — it is the **free, immediate, uncontested-by-cooldown**
-one, which is what makes taking it a tempo play rather than a necessity. Do not nerf the
-seeding ability for this mode; the tension between "my crystal on a timer" and "the
-crystal, right now, if I can get there first" is the interesting half.
```

</details>

### `2bed5b249` — feat(dolphin): prism and danger-prism collisions slow the Dolphin, on the Squirrel's numbers

_Claude, 2026-08-15 00:50:25 +0000_

```text
The Dolphin shipped with NO VesselChangeSpeedByPrismEffectSO in its chain, so
a prism collision - danger prism included - did nothing at all to its speed.
DolphinVesselChangeSpeedByPrism.asset existed but was referenced by no
container: authored once, never wired, and invisible in play because a vessel
that simply does not slow reads as a vessel that is fast.

Wires it into DolphinImpactorDataContainer.vesselPrismEffects at the
Squirrel's relative position (after VesselDamagePrismEffect, before
VesselElementalDebuffByDangerPrismEffect) and re-authors it to the Squirrel's
exact values, since a prism is a prism and the collision read should not
depend on which hull hit it:

  speedModifierDuration       0.5 -> 1
  maxSlowStrength             0.8 -> 0.5
  massScaling                 0.1  (already matched)
  dangerSlowMultiplier        3    (already matched)
  dangerSlowDurationMultiplier 3   (already matched)

Net feel: a normal opposing prism halves the throttle and recovers linearly
over 1 s; a danger prism clamps to a dead stop and recovers over 3 s. Own
non-danger trail is skipped by the shared effect, and danger still bites its
own domain (locked design).

The Dolphin deliberately does NOT take the Squirrel's
VesselResetBoostPrismEffect - its boost punish is the halving added in
fbd991d0, a gentler design for a meter bought with drift-seconds.

Verified the slow reaches a DRIFTING Dolphin: MoveShip applies
speed * throttleMultiplier after AdvanceSpeed's _driftSpeedHeld early-return,
so the hold pins cruise speed while the modifier still scales frame output.
The drift-hold doc already claimed this, but it was aspirational - nothing was
pushing anything through that channel until now.

Docs: DOLPHIN_ENERGY_ECONOMY.md gains the ram's speed cost with the measured
numbers, the correction to the drift-hold clause, and three verification rows.
```

```text
 .../VesselContainers/DolphinImpactorDataContainer.asset               |  1 +
 .../Vessel Prism Effects/DolphinVesselChangeSpeedByPrism.asset        |  6 ++---
 .../Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md       | 46 ++++++++++++++++++++++++++++++++-
 3 files changed, 49 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md b/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md
index 8777089cb..8318a2408 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md
@@ -24,6 +24,39 @@ two halves are authored as two effects in `DolphinImpactorDataContainer.vesselPr
 on slot 1), each with its own `retainedFraction` (0.5) so they can be tuned apart if the ram
 turns out to bite harder on one than the other.
 
+### A ram also costs SPEED — on the fleet's terms, not the Dolphin's
+
+The Dolphin shipped with **no `VesselChangeSpeedByPrismEffectSO` in its chain at all**, so a
+prism collision — danger prism included — did nothing whatsoever to its speed. The asset existed
+(`DolphinVesselChangeSpeedByPrism`, authored at `duration 0.5` / `maxSlowStrength 0.8`) and was
+referenced by no container: authored once, never wired, and invisible because a vessel that
+simply doesn't slow reads as a vessel that's fast.
+
+It now carries the **Squirrel's exact numbers**, because a prism is a prism and the collision
+read should not depend on which hull hit it:
+
+| | normal prism | danger prism |
+|---|---|---|
+| slow strength | `min(volume × 0.1, 0.5)` | `0.5 × 3` → clamps to a **full stop** |
+| recovery | **1 s**, linear back to full throttle | **3 s**, linear |
+
+`massScaling: 0.1` against `maxSlowStrength: 0.5` means anything of volume ≥ 5 saturates, so in
+practice a normal prism halves the throttle for a second and a danger prism parks you for three.
+Both recover linearly from full strength (`VesselTransformer.ApplyThrottleModifiers` lerps the
+modifier back to 1 across its duration) — the bite is instant, the climb out is not.
+
+Two properties come free with the shared effect and are the reason to use it rather than author
+a Dolphin-specific slow:
+
+- **Your own trail doesn't brake you** — `VesselChangeSpeedByPrismEffectSO` skips non-danger
+  prisms of your own domain. You skim your own mass, you don't plow through it.
+- **Danger is not safe to its own domain** (locked design), so the full stop lands on the owner
+  of the danger trail exactly as hard as on anyone else.
+
+The Dolphin does **not** take the Squirrel's `VesselResetBoostPrismEffect` (which zeroes boost
+outright). Its boost punish is the halving above — a deliberately different, gentler design for
+a vessel whose boost is bought with drift-seconds rather than picked up.
+
 **Energy** is banked by skimming and spent in ONE shot on a crystal:
 
 | event | effect on Energy | authored in |
@@ -233,7 +266,13 @@ Four things are deliberately **outside** the hold:
 
 - **`throttleMultiplier`** (the `ModifyThrottle` channel) stays live, so a danger prism's full-stop
   slow bites a drifting Dolphin exactly as hard as a flying one. Danger prisms are not safe to
-  anybody (locked design) and a drift is not a shield.
+  anybody (locked design) and a drift is not a shield. Mechanically this is `MoveShip` applying
+  `speed * throttleMultiplier` *after* `AdvanceSpeed`'s `_driftSpeedHeld` early-return, so the
+  hold pins the cruise speed and the modifier still scales the frame's output.
+  **This clause was aspirational until the speed effect was wired (§1).** The hold was built to
+  leave the channel live, but nothing on the Dolphin was calling `ModifyThrottle` on a prism
+  collision, so "the vessel still slows mid-drift" could not have been observed — a good reminder
+  that a correctly-designed passthrough proves nothing if no one is pushing anything through it.
 - **`velocityShift`** (the `ModifyVelocity` channel) stays live — knockback, dodges and AOE impulses
   still displace a drifting vessel.
 - **`_speedTrackingRate`** is untouched, so a ramp boost mid-ramp resumes on release instead of
@@ -431,6 +470,9 @@ Play Menu_Main, enter freestyle on the Dolphin.
 | drift from a slow crawl | it stays a slow crawl for the whole drift (the lock is "hold what you had", not "hold top speed") |
 | release the drift | throttle authority returns immediately and speed resumes tracking (into the boost discharge) |
 | ram a danger prism mid-drift | the vessel still slows — `throttleMultiplier` is outside the hold |
+| ram an opposing normal prism | throttle drops to ~half instantly, climbs back over **1 s** — same feel as the Squirrel |
+| ram a DANGER prism | **dead stop**, climbing back over **3 s** — same feel as the Squirrel, and it lands on the danger trail's owner too |
+| ram your OWN (non-danger) trail | no braking at all — own-domain prisms are skipped |
 | hold drift | boost ring steps up; release → speed rises then decays; ring empties |
 | hold drift from empty to full | ring fills in **~3.6 s** (was 4) |
 | release a full meter | speed peaks near **357** and takes **~2.5 s** to fall back (was 210 / 2 s) |
@@ -449,6 +491,8 @@ place: they hold no renderers, and `GameCanvas` is the shared prefab of `Docs/GA
 Knobs, in order of likely tuning: `DolphinSkimmerChangeResourceByPrismEffect._resourceAmount`
 (skim gain), `DolphinVesselChangeResourceByPrismEffect.retainedFraction` /
 `DolphinVesselChangeBoostByPrismEffect.retainedFraction` (how hard a ram bites each meter),
+`DolphinVesselChangeSpeedByPrism.maxSlowStrength` / `speedModifierDuration` (**currently pinned
+to the Squirrel's values on purpose — moving either un-shares the fleet's collision read**),
 `ChargeBoostAction.chargeTimeToFull` / `dischargeTimeToEmpty` /
 `maxBoostMultiplier`, `DeployTeamCrystalAction.cooldown` / `minCooldown`,
 `DolphinVesselExplosionByCrystalEffect._min/_max/_coreExplosionScale` (**then `MinJawAngle` /
```

</details>

### `0d3db63ec` — feat(sparrow): prism and danger-prism collisions slow the Sparrow, on the Squirrel's numbers

_Claude, 2026-08-15 00:54:33 +0000_

```text
Same gap as the Dolphin: SparrowImpactorDataContainer carried no
VesselChangeSpeedByPrismEffectSO, so the Sparrow took no speed penalty from
any prism - danger ribs included. Unlike the Dolphin there was not even an
orphaned asset, so this authors SparrowVesselChangeSpeedByPrism on the
Squirrel's values and wires it after VesselDamagePrismEffect, before the two
danger effects.

  speedModifierDuration 1, massScaling 0.1, maxSlowStrength 0.5,
  dangerSlowMultiplier 3, dangerSlowDurationMultiplier 3

No double-apply on danger: SparrowDebuffByRhinoDangerPrismEffectSO never
slowed anything despite its vesselSlowedByRhinoDangerPrismEvent field and its
"Slow Viewer Integration" header - it mutes an input and raises events. The
two are complementary, not overlapping.

The elemental ward is unaffected: IsElementallyImmune gates one branch of
ResourceSystem.ApplyElementalEffect and never touched ModifyThrottle, so a
Time-5 boosting Sparrow still takes the slow - which is what its own doc
always claimed.

Two shipped docs asserted this behaviour and were describing something that
could not happen; both are corrected in place rather than left reading as
tested:

- SPARROW_AFTERBURNER.md step 6 ("You are still slowed ... only the elemental
  drain is denied") - structurally right about the gate, but there was no slow
  for the ward to leave standing.
- DOGFIGHT.md ("volume-independent full-stop slow") - the only vessel that
  mode flies took no speed penalty at all.

Second consequence DOGFIGHT.md now records: the Boneyard becomes TERRAIN.
Its wreckage is environment-owned (Domains.Blue, so the own-domain skip never
applies) and saturates maxSlowStrength at volume >= 5, so clipping a hulk
halves throttle for a second. Flagged for the first playtest - it makes cover
costly to hug, which is the point, but it also slows disengages through debris.

Rhino and Serpent still have no speed effect, left alone by request.
```

```text
 .../Effects/Effect Containers/VesselContainers/SparrowImpactorDataContainer.asset   |  1 +
 .../_SO_Assets/Effects/Vessel Prism Effects/SparrowVesselChangeSpeedByPrism.asset   | 19 +++++++++++++++++++
 .../Effects/Vessel Prism Effects/SparrowVesselChangeSpeedByPrism.asset.meta         |  8 ++++++++
 Assets/_Scripts/Controller/Arcade/DOGFIGHT.md                                       | 14 ++++++++++++++
 Assets/_Scripts/Controller/Vessel/R_VesselActions/SPARROW_AFTERBURNER.md            | 10 ++++++++++
 5 files changed, 52 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md b/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md
index 3d2981695..7658ab8fe 100644
--- a/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md
+++ b/Assets/_Scripts/Controller/Arcade/DOGFIGHT.md
@@ -335,6 +335,20 @@ to pull before the structure counts.
 - **Danger** (43–203) rides only the **torn end ribs** of hulks and the reactor's hot inner ribs
   — telegraphed by the geometry rather than hidden in it. Contact costs the standard danger
   punishment (volume-independent full-stop slow, 4 s all-element debuff, boost reset).
+
+  > **The full-stop slow did not exist here until 2026-08-15.** The Sparrow's
+  > `SparrowImpactorDataContainer` carried no `VesselChangeSpeedByPrismEffectSO`, so the *only*
+  > vessel this mode flies took no speed penalty from any prism — danger ribs included. The
+  > danger punishment was really only the debuff and the input mute. `SparrowVesselChangeSpeedByPrism`
+  > is now wired on the Squirrel's numbers, which makes this paragraph true and has a second
+  > consequence the mode wants: **the wreckage is now terrain.** A normal Boneyard prism is
+  > environment-owned (`Domains.Blue`, hostile to everyone, so the own-domain skip never applies)
+  > and at `massScaling 0.1` against `maxSlowStrength 0.5` anything of volume ≥ 5 saturates — so
+  > clipping a hulk halves your throttle for a second and recovers linearly. Flying the canyons
+  > cleanly is now a skill the arena rewards rather than a line you can ignore. Worth a look in
+  > the first playtest: it makes cover genuinely costly to hug, which is the point, but it also
+  > slows disengages through debris — if it over-punishes, `maxSlowStrength` on that asset is the
+  > dial, and moving it un-shares the fleet's collision read.
 - **Shielded / super-shielded** is the reactor core ring (24) plus one beacon per spire — **30–51
   always-on convex mesh colliders, 0.15–0.33 % of the structure**. Beacons are shielded rather
   than plain so they *survive* a match: a landmark a stray rocket can delete is not a landmark.
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/SPARROW_AFTERBURNER.md b/Assets/_Scripts/Controller/Vessel/R_VesselActions/SPARROW_AFTERBURNER.md
index 9423d197b..9b5b9d5cc 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/SPARROW_AFTERBURNER.md
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/SPARROW_AFTERBURNER.md
@@ -246,6 +246,16 @@ Not editor-verified — I cannot run Unity. Every step below is unrun. Mirrored
    - **while boosting**, hit the same danger prism → flowers do **not** dip. You are still slowed
      and still take the input mute; only the elemental drain is denied.
    - **not boosting**, hit it → flowers dip normally.
+
+   > **"You are still slowed" was aspirational until 2026-08-15.** The immunity gate is, and always
+   > was, a single check on the negative branch of `ResourceSystem.ApplyElementalEffect` — it never
+   > touched `ModifyThrottle`, so the *statement* was structurally right. But the Sparrow had **no
+   > `VesselChangeSpeedByPrismEffectSO` in its impact chain at all**, so nothing was slowing it in
+   > the first place and there was no slow for the ward to leave standing. Fixed by wiring
+   > `SparrowVesselChangeSpeedByPrism` (the Squirrel's numbers); this step is now actually
+   > falsifiable. `SparrowDebuffByRhinoDangerPrismEffectSO` was never the slow despite its
+   > `vesselSlowedByRhinoDangerPrismEvent` field and its "Slow Viewer Integration" header — it
+   > mutes an input and raises events, nothing more.
 7. **Serpent, ungated.** Serpent in the same scene, Time at any level. Stopped (turret/weave
    stance) + danger prism → no flower dip. Moving → normal dip.
 8. **No stuck immunity.** Boost into a vessel swap / turn end while immune, then take a danger hit
```

</details>

### `9347cb6f1` — tune(manta): bring the prism-collision slow in line with the rest of the fleet

_Claude, 2026-08-15 01:19:12 +0000_

```text
The Manta was already wired but was the last vessel on the old numbers, which
made it the only one whose collision read differed once the Dolphin and
Sparrow were brought onto the Squirrel's values.

  speedModifierDuration  0.5 -> 1
  maxSlowStrength        0.8 -> 0.5

massScaling, dangerSlowMultiplier and dangerSlowDurationMultiplier already
matched. Net: the Manta's normal-prism slow is now shallower (half throttle
rather than a fifth) but twice as long, and its danger stop recovers over 3 s
like everyone else's.

Every vessel that slows on prism contact - Squirrel, Dolphin, Sparrow, Manta -
now carries identical tuning, so a prism reads the same whichever hull hits it.
Rhino and Serpent still have no speed effect at all, left alone by request.
```

```text
 Assets/_SO_Assets/Effects/Vessel Prism Effects/MantaVesselChangeSpeedByPrism.asset | 6 +++---
 1 file changed, 3 insertions(+), 3 deletions(-)
```

### `75e88ce02` — docs: correct the platform-level danger-slow claim; capture the session's lessons in skills

_Claude, 2026-08-15 01:24:10 +0000_

```text
CLAUDE.md stated the danger punishment's "volume-independent full-stop slow"
as a platform given. It is per-vessel WIRING: the effect only runs for a
vessel whose container actually lists a VesselChangeSpeedByPrismEffectSO, and
for most of the fleet's life most vessels did not. Records the wiring status
(Squirrel/Dolphin/Sparrow/Manta carry it on one shared tuning; Rhino and
Serpent still have none), why three shipped docs asserted it anyway, and that
an identifier's NAME is not evidence of a behaviour.

Skill capture (/ship §3.5):

- /vessel gains rule 22 (a shared impact effect is per-vessel wiring - audit
  which containers list it via the GUID cross-reference sweep; an orphaned
  effect asset is the tell) and rule 23 (an impact effect must not scale a
  SERIALIZED authored VesselStatus field in place - it ratchets permanently).
  Its §5 no longer hands container wiring back as play-mode-only: the static
  sweep catches the whole "authored but never wired" class first.
- /ship §2 gains a review gate: a doc asserting a consequence is not evidence
  the consequence happens - find the producer. A correct passthrough reads as
  verified when nothing is pushing through it, which is exactly how four such
  claims survived review.
```

```text
 .claude/skills/ship/SKILL.md   | 11 +++++++++++
 .claude/skills/vessel/SKILL.md | 33 ++++++++++++++++++++++++++++++---
 CLAUDE.md                      | 18 ++++++++++++++++++
 3 files changed, 59 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 108 lines)</summary>

```diff
diff --git a/.claude/skills/ship/SKILL.md b/.claude/skills/ship/SKILL.md
index 6dd50d183..0beb62734 100644
--- a/.claude/skills/ship/SKILL.md
+++ b/.claude/skills/ship/SKILL.md
@@ -58,6 +58,17 @@ Walk every changed file against these gates:
   migrated? Renamed/deleted assets - every GUID reference updated?
 - **Verification honesty**: list what was actually verified (in-editor play, tests) vs.
   what only compiles-by-inspection. Unverified risk goes in the PR body, not under the rug.
+- **A doc that asserts a consequence is not evidence the consequence happens — find the
+  PRODUCER.** When a doc (or a verification step, or CLAUDE.md) says "X still lands", "contact
+  costs Y", or "the gate leaves Z alone", grep for who actually *calls* the thing that produces
+  X/Y/Z for that vessel/mode/path. A passthrough that is genuinely correct — the gate really
+  does leave the channel alone — reads as verified even when nothing upstream is pushing
+  anything through it, so the claim survives review indefinitely. Four such claims shipped
+  across three docs plus CLAUDE.md describing a prism-collision slow on vessels that had no
+  slow effect wired at all. The same shape hides behind NAMES: a serialized field called
+  `vesselSlowedByRhinoDangerPrismEvent` under a `"Slow Viewer Integration"` header belonged to
+  an effect that only muted an input. Treat "the docs say so" and "the identifier says so" as
+  hypotheses to check, never as the check.
 
 ## 2.5 Tool-output gate — NEVER SKIPPED, IN EVERY MODE
 
diff --git a/.claude/skills/vessel/SKILL.md b/.claude/skills/vessel/SKILL.md
index 5b2f0d65c..e373dd872 100644
--- a/.claude/skills/vessel/SKILL.md
+++ b/.claude/skills/vessel/SKILL.md
@@ -103,7 +103,7 @@ un-implemented until Garrett marks them up. If your task requires a mapping that
 STOP and ask (AskUserQuestion), presenting the FLEET_MAPS proposal for that row. The same gate
 applies to new abilities, new resources on the meter list, and anything that adds a fundamental.
 
-## 4. Implement — the twenty-one rules that keep getting relearned
+## 4. Implement — the twenty-three rules that keep getting relearned
 
 1. **Ability SOs are shared and stateless.** Per-vessel state lives in executors / vessel-root
    MonoBehaviours; SOs receive `(registry, status)` per call. Never bind state to an SO asset.
@@ -230,14 +230,41 @@ applies to new abilities, new resources on the meter list, and anything that add
     Camera POSE is free (the law is explicitly a no-camera-distance-change effect); FOV is not.
     And before adding a public FOV surface to that law for one vessel, check the ability still
     earns it without the zoom — the Dolphin's Echo Sight did, and the surface was reverted.
+22. **A shared impact effect is PER-VESSEL WIRING. Audit which containers list it — never infer
+    it from the class existing, from an asset existing, or from a doc saying it happens.** An
+    effect only runs for a vessel whose `VesselImpactorDataContainerSO` array actually contains
+    it, and a missing entry is *totally silent*: no null, no warning, just a consequence that
+    never occurs. `VesselChangeSpeedByPrismEffectSO` shipped absent from the Dolphin (whose
+    `DolphinVesselChangeSpeedByPrism` asset existed and was referenced by **no** container) and
+    from the Sparrow (no asset at all, in the one vessel Dog Fight flies) — so neither slowed on
+    any prism, danger included, for the fleet's whole life. **An orphaned effect asset is the
+    tell**, and it is one sweep: map every `*.asset.meta` GUID to its name, then check which
+    GUIDs appear inside the six `VesselContainers/*.asset` arrays. Anything of that script type
+    that appears in none is authored-but-dead. Do the same sweep for TUNING once wired —
+    per-vessel instances drift apart silently, and a prism should read the same whichever hull
+    hits it. (Dolphin/Sparrow/Manta prism slow, 2026-08-15.)
+23. **An impact effect must not scale a SERIALIZED authored field on `VesselStatus` in place.**
+    Check whether the property is runtime bookkeeping or a serialized value with an authored
+    default before writing it. `BoostMultiplier` is `[SerializeField] boostMultiplier = 4` and is
+    what boost sources that don't write it fall back to (`BoostActionSO` only flips `IsBoosting`;
+    `VesselResetBoostPrismEffectSO` restores it to an authored base) — so "halve the boost on a
+    ram" applied to it ratchets the vessel's authored number toward 1 a little further on every
+    collision, permanently, with nothing in the game to restore it. Scale the RESOURCE METER
+    instead and let the executor re-derive; a creeping, unrecoverable nerf is indistinguishable
+    from a tuning problem for as long as anyone will look. (Dolphin boost ram, 2026-08-14.)
 
 ## 5. Audit, then hand back verification (you cannot run Unity; the human is the gate)
 
 - State which auditors to run and the expected result: **Audit Vessel Ability Rows**,
   **Audit Vessel Skimmers**, **Audit Vessel Elemental Morphs**, plus **Wire Elemental Petal
   Bars** (or **Bake Elemental Petal Bars Into All Vessel HUDs**) and **Plan Vessel Rig Swap**
-  where relevant. Vessel-impactor container wiring still has no auditor — hand back explicit
-  play-mode checks for that half (prism hit, crystal collect ×1, no NREs).
+  where relevant. Vessel-impactor container wiring still has no in-editor auditor, but do NOT
+  hand that half back as play-mode-only: run the rule-22 sweep yourself first (GUID → name over
+  `*.asset.meta`, then cross-reference the six `VesselContainers/*.asset` arrays) and print the
+  per-vessel table — which vessels carry the effect, which are missing it, and whether the wired
+  ones share tuning. That is a static, seconds-long check that catches the entire "authored but
+  never wired" class before a human ever opens Unity; play-mode checks (prism hit, crystal
+  collect ×1, no NREs) then confirm the wiring you already proved exists.
 - **Check that the feedback you are asking a human to judge is OBSERVABLE before you ask.** A
   skim's three signals are each individually invisible on a desktop editor: the haptic is a
   NO-OP (NiceVibrations does nothing there), the beam VFX only draws if the skimmed prism
diff --git a/CLAUDE.md b/CLAUDE.md
```

</details>

### `625da6e99` — feat(vessel): give every vessel the Squirrel's two-layer domain-tinted jet FX

_Claude, 2026-08-15 05:10:12 +0000_

```text
The Squirrel was the only vessel in the fleet with jet FX, and the only prefab
in the project carrying VesselTrailCustomization - the component that repaints
a vessel's trails on every domain change. Its two layers are tuned for two
different viewers: a long beacon ribbon (TrailEmpty, m_Time 2.0 / width 1.0)
so other players can find the vessel, and short engine plumes (jet.prefab,
trail m_Time 0.5 / width 0.3) as feedback for its own pilot. Both are
TrailRenderer-bearing, which is what lets one tint pass repaint both.

Audit of the other ten: the Dolphin and Sparrow had a ribbon that never took
their domain colour; the Rhino's only jet FX were three disabled experiments;
seven vessels had nothing at all.

Adds VesselJetFX + VesselJetFXConfigSO (Resources/VesselJetFXConfig), bound in
VesselController.Initialize alongside the occlusion-corridor and speed-tunnel
laws - deliberately NOT gated on IsLocalPilot, since the beacon exists to be
seen by other players, and ordered BEFORE SetShipProperties so the spawned
trails are caught by the vessel's first domain paint.

Mounts resolve by name (as VesselAnimation.ResolvePart already does) plus a
structural bone-or-active-renderer test. Name matching alone is not enough:
the Sparrow's "ExhaustBarrage" is an ability executor and would have fired an
engine out of the cockpit. Hand-authoring onto bones is not an option - the
vessel FBX metas ship an empty internalIDToNameTable, so bones have no stable
fileID to author against.

Authored FX always wins, detected from the trails a vessel already has, so the
Squirrel's hand-tuned jets are never doubled and the Dolphin/Sparrow never gain
a second ribbon. Plumes are sized in world units with the mount's lossyScale
divided out (Dolphin engines sit at 0.01, Urchin jets at 1.75) and aligned to
the vessel once, so there is no per-frame cost at all.

VesselTrailCustomization now discovers trails live rather than caching at
Awake - jets arrive during Initialize, so an Awake-cached set would leave every
runtime jet wearing its prefab colour forever. Authored alpha curves are cached
per trail instead of in an index-parallel array, which mis-pairs as the set grows.

Also adds FrogletTools > Vessels > Audit Vessel Jet FX (asset-only, reuses the
shipped predicates) and VesselJetFXMountResolutionTests, which pins the real
fleet name corpus. That test exists because "rig", tried as an exclusion token
for the Serpent's EngineRig, silently deleted every RIGHT-side engine in the
fleet - "right" contains "rig".

Docs/VESSEL_JET_FX.md records the law, the audit, the per-model mount tables and
the tuning knobs; UNITY_VERIFICATION_CHECKLIST.md carries the in-editor steps.
```

```text
 Assets/Resources/VesselJetFXConfig.asset                             |  38 ++++
 Assets/Resources/VesselJetFXConfig.asset.meta                        |   8 +
 Assets/_Scripts/Controller/Vessel/IVesselStatus.cs                   |   7 +
 Assets/_Scripts/Controller/Vessel/VesselController.cs                |  36 +++-
 Assets/_Scripts/Controller/Vessel/VesselJetFX.cs                     | 346 +++++++++++++++++++++++++++++++++
 Assets/_Scripts/Controller/Vessel/VesselJetFX.cs.meta                |   2 +
 Assets/_Scripts/Controller/Vessel/VesselStatus.cs                    |  33 ++++
 Assets/_Scripts/Controller/Vessel/VesselTrailCustomization.cs        |  87 ++++++---
 Assets/_Scripts/Editor/VesselJetFXAudit.cs                           | 180 +++++++++++++++++
 Assets/_Scripts/Editor/VesselJetFXAudit.cs.meta                      |   2 +
 Assets/_Scripts/ScriptableObjects/VesselJetFXConfigSO.cs             | 151 ++++++++++++++
 Assets/_Scripts/ScriptableObjects/VesselJetFXConfigSO.cs.meta        |   2 +
 Assets/_Scripts/Tests/Editor/VesselJetFXMountResolutionTests.cs      | 171 ++++++++++++++++
 Assets/_Scripts/Tests/Editor/VesselJetFXMountResolutionTests.cs.meta |   2 +
 CLAUDE.md                                                            |   1 +
 Docs/UNITY_VERIFICATION_CHECKLIST.md                                 |  53 +++++
 Docs/VESSEL_JET_FX.md                                                | 278 ++++++++++++++++++++++++++
 17 files changed, 1365 insertions(+), 32 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1480 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/IVesselStatus.cs b/Assets/_Scripts/Controller/Vessel/IVesselStatus.cs
index 248d5fb7e..3a300743d 100644
--- a/Assets/_Scripts/Controller/Vessel/IVesselStatus.cs
+++ b/Assets/_Scripts/Controller/Vessel/IVesselStatus.cs
@@ -130,6 +130,13 @@ namespace CosmicShore.Gameplay
         IVesselHUDController VesselHUDController { get; }
 
         VesselCustomization Customization { get; }
+
+        /// <summary>Fleet-wide jet FX (beacon ribbon + engine plumes). See Docs/VESSEL_JET_FX.md.</summary>
+        VesselJetFX JetFX { get; }
+
+        /// <summary>Domain tint for every TrailRenderer under the vessel, jet FX included.</summary>
+        VesselTrailCustomization TrailCustomization { get; }
+
         R_VesselActionHandler ActionHandler { get; }
 
         R_ShipElementStatsHandler ElementalStatsHandler { get; }
diff --git a/Assets/_Scripts/Controller/Vessel/VesselController.cs b/Assets/_Scripts/Controller/Vessel/VesselController.cs
index 501c10943..f3f787106 100644
--- a/Assets/_Scripts/Controller/Vessel/VesselController.cs
+++ b/Assets/_Scripts/Controller/Vessel/VesselController.cs
@@ -154,6 +154,18 @@ namespace CosmicShore.Gameplay
                 VesselSpeedTunnel.SetTarget(VesselStatus, transform);
             }
 
+            // Jet FX (Docs/VESSEL_JET_FX.md) — the beacon ribbon that lets other players find
+            // this vessel and the engine plumes that give its pilot throttle feedback. Bound
+            // here for the same reason the laws above are: Initialize is the one method every
+            // vessel calls on every spawn path, so no vessel and no mode can be authored
+            // without them. NOT gated on IsLocalPilot — the beacon exists precisely to be seen
+            // by OTHER players, so a local-only binding would invert the feature.
+            //
+            // Ordering is load-bearing: this must run BEFORE SetShipProperties below, because
+            // that call is the vessel's FIRST domain paint. Spawn the trails after it and they
+            // keep their prefab colour until the player happens to change domain.
+            VesselStatus.JetFX.Initialize(VesselStatus);
+
             if (gameData != null)
                 ShipHelper.SetShipProperties(gameData.ThemeManagerData, this);
             else
@@ -195,13 +207,16 @@ namespace CosmicShore.Gameplay
         public virtual void SetSkimmerMaterial(Material material) =>
                 VesselStatus.SkimmerMaterial = material;
 
-        VesselTrailCustomization _trailCustomization;
-        public virtual void SetTrailColors(Color highlightColor, Color coreColor)
-        {
-            if (_trailCustomization == null)
-                _trailCustomization = GetComponentInChildren<VesselTrailCustomization>(includeInactive: true);
-            _trailCustomization?.SetTrailColors(highlightColor, coreColor);
-        }
+        /// <summary>
+        /// Repaints EVERY trail the vessel draws for its current domain — the long beacon
+        /// ribbon and the engine plumes alike (Docs/VESSEL_JET_FX.md). Resolved through
+        /// VesselStatus so the component is created if a prefab is missing it: the previous
+        /// null-conditional lookup meant a vessel without the component silently kept its
+        /// prefab-coloured jets forever, which is the one failure this law cannot tolerate —
+        /// a jet wearing the wrong domain actively misinforms other players.
+        /// </summary>
+        public virtual void SetTrailColors(Color highlightColor, Color coreColor) =>
+            VesselStatus.TrailCustomization.SetTrailColors(highlightColor, coreColor);
 
         public virtual void BindElementalFloat(string name, Element element) =>
             VesselStatus.ElementalStatsHandler.BindElementalFloat(name, element);
@@ -260,6 +275,13 @@ namespace CosmicShore.Gameplay
         {
             VesselStatus.Player = player;
 
+            // Jet FX is idempotent, so this is insurance rather than a second spawn: it covers a
+            // vessel that reached ChangePlayer without ever having run Initialize. The domain
+            // repaint that follows an ownership swap is handled by the caller's
+            // ShipHelper.SetShipProperties, and VesselTrailCustomization discovers trails live,
+            // so the inherited jets take the new owner's colour with no extra call here.
+            VesselStatus.JetFX.Initialize(VesselStatus);
+
             // Re-evaluate BOTH platform laws: ChangePlayer hands a LIVE vessel to a different
             // player (the Cellular Duel round-boundary ownership swap), which Initialize never
             // sees. Without this the tunnel would keep driving the local camera from a vessel
diff --git a/Assets/_Scripts/Controller/Vessel/VesselJetFX.cs b/Assets/_Scripts/Controller/Vessel/VesselJetFX.cs
```

</details>

### `00fba953d` — fix(vessel): place jet FX the way the Squirrel does — beacon behind the camera, plumes to the sides

_Claude, 2026-08-15 06:06:47 +0000_

```text
The Squirrel's jet placement is design, not decoration, and the first pass
missed both halves of it.

The beacon exists for OTHER players, so it must never obstruct its own pilot:
the Squirrel authors a PAIR at (+/-4, 0, -12) against a camera at z -17 — a
laterally-offset pair starting back at the camera plane, not one ribbon on the
centreline near the hull. The first pass spawned a single ribbon at
-0.55 x hull radius, which is neither.

Beacon depth is now measured against THIS VESSEL'S OWN camera follow distance,
which is the quantity that actually decides whether the ribbon is in the
pilot's face. That reference is essential across this fleet, not a refinement:
camera distance runs 17 (Squirrel), 20 (Dolphin), 30 (Manta), 51 (Sparrow),
120 (Rhino), 250 (Serpent). A hull-relative offset tuned on the Squirrel puts
the Serpent's ribbon 200+ units short of its camera, directly in view.
ResolveCameraDistance uses the full offset magnitude (the Sparrow's camera is
lifted 10 as well as set back 50) and a dynamic camera's CLOSEST approach,
since that is the worst case for obstruction.

Plumes belong out to the sides, emerging from the vessel's obvious engines —
already true wherever the model has engine geometry, since mounts resolve
against it. Derived mounts (Manta family, Sparrow) were too far inboard and
too far forward at 0.28 / -0.275 x hull radius; widened to 0.5 and moved back
to -0.6 so they read as engines rather than one centreline exhaust.

beaconDepthPerCameraDistance defaults to 1.0 (ribbon head exactly at the camera
plane, guaranteed non-obstructing at every camera distance in the fleet's 15x
range). The Squirrel's authored pair measures 12/17 = 0.71 — slightly in front
of its camera, which works because of the lateral offset and lets the pilot
glimpse their own ribbon. Documented as the knob to pull if the beacon reads as
invisible to its own pilot. The Squirrel itself is untouched either way: its
authored FX is detected and never replaced.

Adds pure, tested placement helpers (BeaconLateralOffset, ResolveCameraDistance)
including a guard that no ribbon lands on the centreline for an even count, and
the auditor now prints each vessel's beacon depth and the camera distance it was
measured against — plus a warning for a vessel with no CameraSettingsSO to
measure, whose beacon would fall back to a hull-relative guess.
```

```text
 Assets/Resources/VesselJetFXConfig.asset                        |  8 +++-
 Assets/_Scripts/Controller/Vessel/VesselJetFX.cs                | 54 +++++++++++++++++++-----
 Assets/_Scripts/Editor/VesselJetFXAudit.cs                      | 15 +++++++
 Assets/_Scripts/ScriptableObjects/VesselJetFXConfigSO.cs        | 88 +++++++++++++++++++++++++++++++++++----
 Assets/_Scripts/Tests/Editor/VesselJetFXMountResolutionTests.cs | 76 +++++++++++++++++++++++++++++++++
 Docs/UNITY_VERIFICATION_CHECKLIST.md                            |  8 ++++
 Docs/VESSEL_JET_FX.md                                           | 43 +++++++++++++++----
 7 files changed, 261 insertions(+), 31 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 437 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/VesselJetFX.cs b/Assets/_Scripts/Controller/Vessel/VesselJetFX.cs
index 93eb50d6c..f049d757c 100644
--- a/Assets/_Scripts/Controller/Vessel/VesselJetFX.cs
+++ b/Assets/_Scripts/Controller/Vessel/VesselJetFX.cs
@@ -129,7 +129,7 @@ namespace CosmicShore.Gameplay
             bool beaconAuthored = preexistingTrails.Length > 0;
             bool plumesAuthored = AnyTrailUnderMounts(preexistingTrails, cfg);
 
-            if (spawnBeaconRibbon && !beaconAuthored) SpawnBeacon(cfg, hullRadius);
+            if (spawnBeaconRibbon && !beaconAuthored) SpawnBeacons(cfg, hullRadius);
             if (spawnEnginePlumes && !plumesAuthored)
                 SpawnPlumes(cfg, hullRadius, ResolveMounts(cfg, hullRadius));
 
@@ -139,15 +139,41 @@ namespace CosmicShore.Gameplay
             GetComponentInChildren<VesselTrailCustomization>(includeInactive: true)?.Refresh();
         }
 
-        void SpawnBeacon(VesselJetFXConfigSO cfg, float hullRadius)
+        /// <summary>
+        /// Places the beacon ribbons the way the Squirrel authors them: a symmetric PAIR, offset
+        /// laterally, starting at (or behind) the pilot's own camera.
+        ///
+        /// Both parts of that are deliberate and were the Squirrel's design, not decoration:
+        /// - the ribbons are OFF the centreline (Squirrel: +/-4) so nothing hangs down the middle
+        ///   of the pilot's view;
+        /// - the depth is measured against THIS VESSEL'S CAMERA, not its hull, so the ribbon
+        ///   starts behind the camera and cannot obstruct the pilot. That reference matters
+        ///   enormously across this fleet: camera follow distance runs from 17 on the Squirrel to
+        ///   250 on the Serpent, so a hull-relative offset tuned on one is in the other's face.
+        /// The ribbon is for OTHER players; the pilot's engine feedback is the plume layer.
+        /// </summary>
+        void SpawnBeacons(VesselJetFXConfigSO cfg, float hullRadius)
         {
             if (cfg.BeaconRibbonPrefab == null) return;
 
-            var beacon = Instantiate(cfg.BeaconRibbonPrefab, _spawnedRoot);
-            beacon.name = "BeaconRibbon";
-            beacon.transform.localPosition = new Vector3(0f, 0f, cfg.BeaconOffsetPerHullRadius * hullRadius);
-            beacon.transform.localRotation = Quaternion.identity;
-            CollectTrails(beacon);
+            float cameraDistance = VesselJetFXConfigSO.ResolveCameraDistance(
+                GetComponent<VesselCameraCustomizer>()?.Settings);
+
+            float depth = cameraDistance > Mathf.Epsilon
+                ? cameraDistance * cfg.BeaconDepthPerCameraDistance
+                : hullRadius * cfg.BeaconFallbackDepthPerHullRadius;
+
+            float lateral = hullRadius * cfg.BeaconLateralPerHullRadius;
+
+            for (int i = 0; i < cfg.BeaconCount; i++)
+            {
+                var beacon = Instantiate(cfg.BeaconRibbonPrefab, _spawnedRoot);
+                beacon.name = $"BeaconRibbon_{i}";
+                beacon.transform.localPosition = new Vector3(
+                    VesselJetFXConfigSO.BeaconLateralOffset(i, cfg.BeaconCount, lateral), 0f, -depth);
+                beacon.transform.localRotation = Quaternion.identity;
+                CollectTrails(beacon);
+            }
         }
 
         /// <summary>
@@ -307,6 +333,13 @@ namespace CosmicShore.Gameplay
             return mounts;
         }
 
+        /// <summary>
+        /// Plumes for a model with no engine geometry. Placed OUT TO THE SIDES at the rear, not
+        /// on the centreline: the plume layer exists to read as engines from the pilot's chase
+        /// camera, and on every vessel that HAS jets they emerge from the hull's flanks. A
+        /// centreline pair would read as one exhaust and lose the vessel's sense of width.
+        /// This is a stand-in for art, not art direction — see Docs/VESSEL_JET_FX.md §7.
+        /// </summary>
         List<Transform> DeriveRearMounts(VesselJetFXConfigSO cfg, float hullRadius)
         {
             var derived = new List<Transform>();
@@ -314,15 +347,14 @@ namespace CosmicShore.Gameplay
             if (count <= 0) return derived;
 
             float spread = hullRadius * cfg.DerivedMountSpreadPerHullRadius;
-            float back = hullRadius * cfg.BeaconOffsetPerHullRadius * 0.5f;
```

</details>

### `99eb84ada` — fix(tests): Assert.AreNotEqual has no tolerance overload

_Claude, 2026-08-15 06:31:02 +0000_

```text
The centreline guard passed a float delta to Assert.AreNotEqual, which has no
(expected, actual, delta) overload — only AreEqual does. The delta bound to the
string message parameter and the message to params object[], giving CS1503 on
both. Assert the magnitude is greater than the tolerance instead, which is also
a more direct statement of what the test means: no ribbon lands on the
centreline.

Also replaces a `?.` on a GetComponent result with an implicit-bool check, per
CLAUDE.md — a UnityEngine.Object's null is overloaded and a destroyed component
slips past a reference-null check.
```

```text
 Assets/_Scripts/Controller/Vessel/VesselJetFX.cs                | 5 ++++-
 Assets/_Scripts/Tests/Editor/VesselJetFXMountResolutionTests.cs | 4 +++-
 2 files changed, 7 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/VesselJetFX.cs b/Assets/_Scripts/Controller/Vessel/VesselJetFX.cs
index f049d757c..874392ce3 100644
--- a/Assets/_Scripts/Controller/Vessel/VesselJetFX.cs
+++ b/Assets/_Scripts/Controller/Vessel/VesselJetFX.cs
@@ -156,8 +156,11 @@ namespace CosmicShore.Gameplay
         {
             if (cfg.BeaconRibbonPrefab == null) return;
 
+            // Implicit bool rather than `?.` — a UnityEngine.Object's null is overloaded, and a
+            // destroyed component would slip past a reference-null check (CLAUDE.md).
+            var cameraCustomizer = GetComponent<VesselCameraCustomizer>();
             float cameraDistance = VesselJetFXConfigSO.ResolveCameraDistance(
-                GetComponent<VesselCameraCustomizer>()?.Settings);
+                cameraCustomizer ? cameraCustomizer.Settings : null);
 
             float depth = cameraDistance > Mathf.Epsilon
                 ? cameraDistance * cfg.BeaconDepthPerCameraDistance
diff --git a/Assets/_Scripts/Tests/Editor/VesselJetFXMountResolutionTests.cs b/Assets/_Scripts/Tests/Editor/VesselJetFXMountResolutionTests.cs
index c2ce2f15a..7f9395bb8 100644
--- a/Assets/_Scripts/Tests/Editor/VesselJetFXMountResolutionTests.cs
+++ b/Assets/_Scripts/Tests/Editor/VesselJetFXMountResolutionTests.cs
@@ -179,9 +179,11 @@ namespace CosmicShore.Tests
         [Test]
         public void BeaconLateral_NoRibbonSitsOnTheCentrelineForAnEvenCount()
         {
+            // Assert.Greater on the magnitude rather than Assert.AreNotEqual: AreNotEqual has no
+            // tolerance overload, so a delta passed to it binds to the message parameter.
             for (int count = 2; count <= 8; count += 2)
                 for (int i = 0; i < count; i++)
-                    Assert.AreNotEqual(0f, VesselJetFXConfigSO.BeaconLateralOffset(i, count, 4f), 1e-4f,
+                    Assert.Greater(Mathf.Abs(VesselJetFXConfigSO.BeaconLateralOffset(i, count, 4f)), 1e-4f,
                         $"ribbon {i} of {count} landed on the centreline — it would hang down the " +
                         "middle of the pilot's view, which is the thing this offset exists to avoid.");
         }
```

</details>

_Also contains 12 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
