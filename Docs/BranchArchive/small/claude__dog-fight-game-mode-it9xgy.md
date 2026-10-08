# Branch archive: `claude/dog-fight-game-mode-it9xgy`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-08-12 by Claude
- **Unmerged commits:** 1
- **Forked from:** `d32f26839` (2026-08-12, Merge pull request #710 from froglet-studio/claude/charge-crystal-shader-9u2ik)
- **Tip:** `e1bb8ed83`
- **Files touched (10):**
  - `Assets/Resources/EndConditionOverrides.asset`
  - `Assets/_Prefabs/Spacevessels/Sparrow.prefab`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameDogFight.unity`
  - `Assets/_Scripts/Controller/Arcade/DOGFIGHT.md`
  - `Assets/_Scripts/Controller/Arcade/DogFightController.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoBlockShootActionExecutor.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/SPARROW_TURRET_STANCE.md`
  - `Assets/_Scripts/ScriptableObjects/EndConditionOverridesSO.cs`
  - `CLAUDE.md`
  - `Tools/Build/author_dogfight_assets.py`

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

<details><summary>Patch (code/doc/text files, first 150 of 746 lines)</summary>

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
-  Liberation's AI orbiting a cage wall, and the exact inverse of Rampage, where ramming **is**
-  the scoring verb.
+- **A COMMITTED break-off, latched at the merge.** Inside `aiBreakOffDistance` (120) the AI
+  switches to an `Extend` phase, latches one escape point — straight through the quarry and out
+  `aiExtendDistanceMultiplier` (3) × the break-off distance beyond it — and flies *that fixed
+  point* until it arrives or `aiMaxExtendSeconds` (4) expires. Only then does it look for a
+  quarry again.
+
+  > **This is a rewrite of a version that did not work, and the reason is worth keeping.** The
+  > first attempt had no state: it aimed at the quarry, and inside the break-off radius aimed at
+  > a point derived from the *current* geometry instead. That point is recomputed every frame, so
+  > the instant the AI slipped past its target the vector to it flipped and the "escape" point
+  > landed back behind the AI — it turned straight round. Two ships welded together, grinding in
+  > a circle. Playtest: *"the AI always try to be close to the player but not run away a bit."*
+  > **A break-off has to be a decision the pilot commits to, not a function of where the enemy is
+  > this instant** — an escape vector the target can steer is not an escape vector.
+
+- **Separation is what makes the missiles visible.** The skyburst was always on the AI's ability
+  list and always fired, on its own timer, whether or not anyone was in front of it. Welded to a
+  target at zero range a rocket has no room to fly and its blast has nowhere useful to land, so
+  the missiles read as absent. With a real extend the AI comes back in from a few hundred units
+  with the target ahead — the geometry a skyburst is for. Turret stance (`ModeSwitchingFire`,
+  2 s every 12 s) gets the same benefit. **No weapon or ability wiring was changed**; the loop
+  drives `SetExternalTargetProvider` and nothing else, so it cannot leak into another mode.
+
+  Without any of this, "steer at the enemy forever" degenerates into a ramming contest neither
+  pilot can shoot their way out of — the same class of mistake as Wildlife Liberation's AI
+  orbiting a cage wall, and the exact inverse of Rampage, where ramming **is** the scoring verb.
 - **Quarry selection** re-runs every `aiRetargetSeconds` (1.5) and takes the nearest live
   opponent, so a pilot who flies into a brawl is picked up by whoever is closest rather than
   every AI converging on one victim. Between samples the AI keeps flying lead pursuit on the
@@ -428,9 +482,15 @@ section below for why Mass in particular pays here.
 
 ### The omni crystal runs exactly as it does everywhere else
 
-`crystalCountMode: 0`, `fixedCrystalCount: 1`, `spawnOnClientReady: 1` — identical to Ribcage.
-Crystals are a platform fundamental; a mode that switches one off is a mode where a whole
-economy silently does nothing.
+`crystalCountMode: 0`, `fixedCrystalCount: **4**`, `spawnOnClientReady: 1`. Crystals are a
+platform fundamental; a mode that switches one off is a mode where a whole economy silently does
+nothing.
+
+**Four, not one.** A single crystal in a 520-unit arena is a needle nobody detours for; four means
+there is usually one worth breaking off toward, which is the entire point of having them in a mode
+where crystals score nothing. Scurry reaches a similar density by a different route
+(`PlayerCountPlusExtra` + 5, i.e. **nine** in a full lobby) — too many here, so this stays on
+`FixedCount`.
 
 The scene authors **one** thing the donor did not: **`anchorlessSpawnRadius: 420`**. This is the
 whole fix for the bug that made the omni crystal read as an Astro League ball —
@@ -470,7 +530,7 @@ Two implementation notes, both forced rather than chosen:
 > (`Docs/ECOSYSTEM.md` §7 caveat 4), and it is tolerable here **only because crystals score
 > nothing in this mode**. If they ever do, this must become server-authoritative.
 
-## Comeback — all four elements, sized to a 120-point race
+## Comeback — all four elements, sized to a 90-point race
 
 `ElementalComebackSystem` runs here on `ScoreDifferenceSource.CombatPoints`, per **domain** like
 every other team source: a pilot's deficit is their side's deficit behind the leading colour.
@@ -481,21 +541,32 @@ per-vessel/per-element weights are retired (equal-elements is the law)."* So
 `ComebackRatePerScoreDeficit` is the entire tuning surface, and a Mass-only weighting would be a
 fundamentals change requiring sign-off, not a mode setting.
 
-That said, **Mass is the element this mode's buff is felt through**, because of what the Sparrow
-does with it: Mass stretches its fired prisms (`SPARROW_TURRET_STANCE.md`), so a trailing pilot's
-turret rounds get visibly bigger and are correspondingly harder to miss with. Charge / Space /
-Time rise alongside it and pay in their own currencies.
+That said, **Mass is the element this mode's buff is actually felt through**, because of what the
+Sparrow does with each one: Space scales muzzle speed, Time and Charge pay in their own
+currencies, and **Mass is the only one wired to the guns' output** — it stretches the fired prisms
```

</details>
