# Branch archive: `claude/swordfish-flagship-fauna-q234zd`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

<!-- SUMMARY -->

- **Last commit:** 2026-09-01 by Claude
- **Unmerged commits:** 15
- **Forked from:** `10f96acb8` (2026-08-31, Merge pull request #821 from froglet-studio/claude/scarab-vessel-polish-k9mds6)
- **Tip:** `61ac4c950`
- **Files touched (78):**
  - `.claude/skills/ecology/SKILL.md`
  - `Assets/DefaultNetworkPrefabs.asset`
  - `Assets/Resources/Toybox.asset`
  - `Assets/_Models/Fauna/SwordFish_A_Parts.controller`
  - `Assets/_Models/Fauna/SwordFish_A_Parts.controller.meta`
  - `Assets/_Models/Fauna/SwordFish_A_Parts.fbx`
  - `Assets/_Models/Fauna/SwordFish_A_Parts.fbx.meta`
  - `Assets/_Prefabs/FloraAndFauna/SwordfishFauna.prefab`
  - `Assets/_Prefabs/FloraAndFauna/SwordfishFauna.prefab.meta`
  - `Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Cell Spawn Profile.asset`
  - `Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Swordfish Fauna Config Data.asset`
  - `Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Swordfish Fauna Config Data.asset.meta`
  - `Assets/_SO_Assets/Host Connection Data/HostConnectionData.asset`
  - `Assets/_SO_Assets/Lifeforms/Swordfish Fauna Charge.asset`
  - `Assets/_SO_Assets/Lifeforms/Swordfish Fauna Charge.asset.meta`
  - `Assets/_SO_Assets/Lifeforms/Swordfish Fauna Mass.asset`
  - `Assets/_SO_Assets/Lifeforms/Swordfish Fauna Mass.asset.meta`
  - `Assets/_SO_Assets/Lifeforms/Swordfish Fauna Space.asset`
  - `Assets/_SO_Assets/Lifeforms/Swordfish Fauna Space.asset.meta`
  - `Assets/_SO_Assets/Lifeforms/Swordfish Fauna Time.asset`
  - `Assets/_SO_Assets/Lifeforms/Swordfish Fauna Time.asset.meta`
  - `Assets/_SO_Assets/Light Fauna Data/SwordfishFaunaDataSO.asset`
  - `Assets/_SO_Assets/Light Fauna Data/SwordfishFaunaDataSO.asset.meta`
  - `Assets/_SO_Assets/Light Fauna Data/SwordfishStrikeData.asset`
  - `Assets/_SO_Assets/Light Fauna Data/SwordfishStrikeData.asset.meta`
  - `Assets/_SO_Assets/Toys/Toy_Arkway.asset`
  - `Assets/_SO_Assets/Toys/Toy_Arkway.asset.meta`
  - `Assets/_Scripts/Controller/Environment/Ark.cs`
  - `Assets/_Scripts/Controller/Environment/Ark.cs.meta`
  - `Assets/_Scripts/Controller/Environment/Cell.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/SwordfishChargeDriver.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/SwordfishChargeDriver.cs.meta`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/SwordfishFauna.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/SwordfishFauna.cs.meta`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/SwordfishStrikeDataSO.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/SwordfishStrikeDataSO.cs.meta`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/WormFauna.cs`
  - `Assets/_Scripts/Controller/Managers/CameraManager.cs`
  - … and 38 more

### `bfb198821` — feat(ecology): make the swordfish the flagship fauna (Blob apex slot)

_Claude, 2026-07-07 15:31:15 +0000_

```text
Add Tools > Cosmic Shore > Build Swordfish Flagship Fauna, an editor tool
that assembles MassSwordfishFauna.prefab from the new SwordFish_A model,
mirroring the MassSharkFauna assembly it succeeds as the Blob (menu) apex
predator:

- FBX import pass: loops the SwrdFsh_Move take, imports SwrdFsh_Charge
- SwordFish_A.controller with a default looping Swim state (+ Charge,
  unwired, for future attack juice)
- Root LightFauna: diet Predator, 45s starvation clock, Runtime Cell Data,
  new MassSwordfishFaunaDataSO (30/45 speed - out-swims the shark's 25/35)
- Prism body: 5 DynamicHealthBlocks along the body + 3 DangerBlocks on the
  bill (the sword is the weapon; danger prisms hit everyone, per the locked
  design), parented under the nearest skeleton bones so they ride the swim
  animation; blocks bloom in via PrismScaleAnimator
- One Spindle per renderer (SpindleMaterial body) so death runs the sealed
  extremity-first wither dissolve and spawn gets the condense-in
- Dormant authored CrystalMass child (Crystal + pickup collider disabled,
  shark parity) - the lifeform->elemental-crystal invariant stays authored
- Creates Blob Swordfish Fauna Config Data at the shark's exact apex
  numbers (seed floor 1, cap 2, births @6 kills) and swaps the shark entry
  for it in Blob Cell Spawn Profile; shark assets stay authored for other
  biomes

Invariants: no invariant is touched adversely - continuity (bloom-in /
wither-out), no imposed death (starvation only), no domain asymmetry
(spawner assigns controlling color), wither-to-crystal + mass conservation
(sealed Fauna.Die + authored crystal), volume spine untouched, territorial
permanence untouched, endogenous selection untouched.

Collider budget: 8 body-prism colliders per individual vs the shark's 10,
same apex cap (2) in the same slot -> worst case ~16 active body colliders
vs ~20 before; net reduction, no new physics queries (senses ride
PrismSpatialIndex exactly as LightFauna already does).

Docs: ECOSYSTEM.md section 7/7.2 species table + apex-status callout updated.
```

```text
 Assets/_Scripts/Editor/SwordfishFaunaSetupTool.cs      | 506 +++++++++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Editor/SwordfishFaunaSetupTool.cs.meta |  11 ++
 Docs/ECOSYSTEM.md                                      |  53 ++---
 3 files changed, 548 insertions(+), 22 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 627 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/SwordfishFaunaSetupTool.cs b/Assets/_Scripts/Editor/SwordfishFaunaSetupTool.cs
new file mode 100644
index 000000000..246d3802a
--- /dev/null
+++ b/Assets/_Scripts/Editor/SwordfishFaunaSetupTool.cs
@@ -0,0 +1,506 @@
+using System.Collections.Generic;
+using System.Linq;
+using CosmicShore.Data;
+using CosmicShore.Gameplay;
+using CosmicShore.Utility;
+using UnityEditor;
+using UnityEditor.Animations;
+using UnityEngine;
+
+namespace CosmicShore.Editor
+{
+    /// <summary>
+    /// Builds the flagship swordfish fauna (MassSwordfishFauna) from the raw SwordFish_A model,
+    /// mirroring the MassSharkFauna assembly it succeeds as the Blob (menu) apex predator:
+    ///
+    ///   • FBX import pass — loops the SwrdFsh_Move take (continuous swim) and imports the
+    ///     SwrdFsh_Charge take alongside it.
+    ///   • Animator controller (SwordFish_A.controller) — default looping Swim state (+ a Charge
+    ///     state, unwired, for future attack juice).
+    ///   • Root LightFauna — diet Predator, 45s starvation clock, Runtime Cell Data + a new
+    ///     MassSwordfishFaunaDataSO (fastest fauna in the sea: the swordfish out-swims the shark).
+    ///   • Prism body — DynamicHealthBlocks along the body and DangerBlocks along the bill (the
+    ///     sword IS the weapon; danger prisms are dangerous to everyone, per the locked design),
+    ///     parented under the nearest skeleton bones so they ride the swim animation. Body prisms
+    ///     bloom in via PrismScaleAnimator (continuity: nothing pops in) and are registered mass
+    ///     (Fauna.NotifyBodyPrismsMoved keeps the spatial index honest).
+    ///   • One Spindle per SkinnedMeshRenderer (RenderedObject wired) so death runs the sealed
+    ///     wither path — extremity-first spindle dissolve, never a pop — and spawn gets the
+    ///     dissolve-in (Spindle.CondenseCoroutine).
+    ///   • A dormant authored CrystalMass child (Crystal + SphereCollider disabled, exactly like
+    ///     the shark's) — the locked "every lifeform drops one elemental crystal" invariant;
+    ///     the sealed Fauna.Die activates it on any death path.
+    ///   • Blob Swordfish Fauna Config Data (FaunaConfigurationSO, apex-tier numbers mirroring
+    ///     the shark: seed floor 1, cap 2, births on 6 kills) and swaps the shark entry for the
+    ///     swordfish in the Blob Cell Spawn Profile. The shark assets stay authored for other
+    ///     biomes; only the menu flagship slot changes hands.
+    ///
+    /// Run via Tools ▸ Cosmic Shore ▸ Build Swordfish Flagship Fauna. Idempotent: the prefab is
+    /// rebuilt in place (same GUID), existing SO assets keep their human-tuned values and only
+    /// re-point at the rebuilt prefab. After running, run Tools ▸ Cosmic Shore ▸ Validate
+    /// Lifeform Crystals.
+    /// </summary>
+    public static class SwordfishFaunaSetupTool
+    {
+        // --- Source / output paths ------------------------------------------------------------
+        const string FbxPath = "Assets/_Models/Fauna/SwordFish_A.fbx";
+        const string PrefabPath = "Assets/_Models/Fauna/MassSwordfishFauna.prefab";
+        const string ControllerPath = "Assets/_Models/Fauna/SwordFish_A.controller";
+        const string FaunaDataPath = "Assets/_SO_Assets/Light Fauna Data/MassSwordfishFaunaDataSO.asset";
+        const string BlobConfigPath = "Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Swordfish Fauna Config Data.asset";
+        const string BlobSpawnProfilePath = "Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Cell Spawn Profile.asset";
+        const string BlobSharkConfigPath = "Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Shark Fauna Config Data.asset";
+        const string CellDataPath = "Assets/_SO_Assets/Cell Data/Runtime Cell Data.asset";
+        const string HealthBlockPrefabPath = "Assets/_Prefabs/Trails/DynamicHealthBlock.prefab";
+        const string DangerBlockPrefabPath = "Assets/_Prefabs/Trails/DangerBlock.prefab";
+        const string CrystalPrefabPath = "Assets/_Prefabs/Environment/CrystalMass.prefab";
+        const string BodyMaterialPath = "Assets/_Graphics/Materials/SpindleMaterial.mat";
+        const string AccentMaterialPath = "Assets/_Graphics/Materials/CrystalMaterials/BlueMassCrystalMaterial.mat";
+
+        // --- Body layout ------------------------------------------------------------------------
+        // Shark-scale creature. Stations are fractions of body length measured from the NOSE
+        // (bill tip): the sword carries DangerBlocks, the body carries DynamicHealthBlocks.
+        // 3 + 5 = 8 body prisms vs the shark's 10 — collider budget strictly ≤ the slot it takes.
+        const float TargetBodyLength = 30f;
+        static readonly float[] BillStations = { 0.03f, 0.10f, 0.18f };
+        static readonly float[] BodyStations = { 0.32f, 0.45f, 0.58f, 0.71f, 0.85f };
+        const float SlabHalfWidthFraction = 0.05f; // vertex slab sampled around each station
+
+        [MenuItem("Tools/Cosmic Shore/Build Swordfish Flagship Fauna")]
+        public static void Build()
+        {
+            if (!AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath))
+            { Debug.LogError($"[SwordfishFauna] Missing model at {FbxPath}"); return; }
+
```

</details>

### `98c65819c` — fix(ecology): fit swordfish prisms to the skeleton, not axis-aligned boxes

_Claude, 2026-07-09 04:50:46 +0000_

```text
The first pass placed body prisms as root-aligned boxes clamped to guessed
cross-sections: uniform boring orientations, oversized, and danger needles
scaled so they overlapped at the nose. Rewrite the placement to mirror how
the hand-authored shark actually sits prisms on its rig.

The editor tool runs inside Unity, so it now reads the live
SkinnedMeshRenderer (bones + bindposes + boneWeights) and, per bone:
- reconstructs the rest-pose world vertices that bone skins
  (bone.localToWorld * bindpose * vertex)
- PCA-fits an oriented box (Jacobi 3x3 eigensolve) to that cluster, so each
  prism inherits its body part's real principal axes (dorsal stands
  vertical, pectorals cant out) instead of one shared root rotation
- sizes the slab to the real local silhouette (x0.72), thinned on the minor
  axis, so nothing is oversized
- parents the prism to the bone so it rides the swim animation, like the
  shark's blocks

The bill (forward-most cluster) is tiled with DangerBlock needles laid
end-to-end along the sword (each needle spans one segment x0.85) so they no
longer overlap. Body clusters use DynamicHealthBlock slabs; long clusters
split into two for coverage.

Also neutralises the PrismScaleAnimator min/max clamp per instance: the
swordfish rig bakes globalScale 100 into the bones, so a body-parented
prism's local scale can fall outside [0.5,10] and get clamped - both bounds
are now bracketed around the fitted value so the size survives verbatim.

Collider budget unchanged in character (one BoxCollider per body prism,
apex cap 2 in the shark's old slot). Invariants untouched.
```

```text
 Assets/_Scripts/Editor/SwordfishFaunaSetupTool.cs | 621 +++++++++++++++++++++++++++++-----------------------
 1 file changed, 343 insertions(+), 278 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 790 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/SwordfishFaunaSetupTool.cs b/Assets/_Scripts/Editor/SwordfishFaunaSetupTool.cs
index 246d3802a..f605c08fe 100644
--- a/Assets/_Scripts/Editor/SwordfishFaunaSetupTool.cs
+++ b/Assets/_Scripts/Editor/SwordfishFaunaSetupTool.cs
@@ -11,34 +11,39 @@ namespace CosmicShore.Editor
 {
     /// <summary>
     /// Builds the flagship swordfish fauna (MassSwordfishFauna) from the raw SwordFish_A model,
-    /// mirroring the MassSharkFauna assembly it succeeds as the Blob (menu) apex predator:
+    /// mirroring how the hand-authored MassSharkFauna sits its prisms on the skeleton — the shark's
+    /// blocks are parented to bones (Wing_2.L, MouthTop_2, Body_3, …) and each is oriented + sized
+    /// to the body part it wraps (a flat 2×12×6 wing slab, a needle-thin 1×1×15 tooth on the jaw),
+    /// NOT axis-aligned boxes clamped to arbitrary sizes. We reproduce that from geometry instead of
+    /// by hand:
     ///
-    ///   • FBX import pass — loops the SwrdFsh_Move take (continuous swim) and imports the
-    ///     SwrdFsh_Charge take alongside it.
-    ///   • Animator controller (SwordFish_A.controller) — default looping Swim state (+ a Charge
-    ///     state, unwired, for future attack juice).
-    ///   • Root LightFauna — diet Predator, 45s starvation clock, Runtime Cell Data + a new
-    ///     MassSwordfishFaunaDataSO (fastest fauna in the sea: the swordfish out-swims the shark).
-    ///   • Prism body — DynamicHealthBlocks along the body and DangerBlocks along the bill (the
-    ///     sword IS the weapon; danger prisms are dangerous to everyone, per the locked design),
-    ///     parented under the nearest skeleton bones so they ride the swim animation. Body prisms
-    ///     bloom in via PrismScaleAnimator (continuity: nothing pops in) and are registered mass
-    ///     (Fauna.NotifyBodyPrismsMoved keeps the spatial index honest).
+    ///   • Per bone-cluster fitting — the tool runs INSIDE Unity, so it reads the live
+    ///     SkinnedMeshRenderer (bones + bindposes + boneWeights) and, for each bone, PCA-fits an
+    ///     oriented box to the rest-pose vertices that bone actually skins. The prism inherits that
+    ///     box's principal axes (so the dorsal slab stands vertical, the pectorals cant outward —
+    ///     real per-part orientation, not one boring root rotation) and is scaled to the real local
+    ///     silhouette, thinned to a slab, so nothing is oversized.
+    ///   • Bill = danger needles — the forward-most cluster (the sword) is tiled with thin DangerBlock
+    ///     needles laid END-TO-END along the bill so they don't overlap (each needle spans one
+    ///     segment of the bill length). The sword IS the weapon and danger prisms hit everyone, per
+    ///     the locked design.
+    ///   • Body = DynamicHealthBlock slabs on the other clusters (long clusters split in two for
+    ///     coverage), parented to their bone so they ride the swim animation.
+    ///   • Prisms bloom in via PrismScaleAnimator (continuity: nothing pops in) and are registered
+    ///     mass (Fauna.NotifyBodyPrismsMoved keeps the spatial index honest).
     ///   • One Spindle per SkinnedMeshRenderer (RenderedObject wired) so death runs the sealed
-    ///     wither path — extremity-first spindle dissolve, never a pop — and spawn gets the
-    ///     dissolve-in (Spindle.CondenseCoroutine).
-    ///   • A dormant authored CrystalMass child (Crystal + SphereCollider disabled, exactly like
-    ///     the shark's) — the locked "every lifeform drops one elemental crystal" invariant;
-    ///     the sealed Fauna.Die activates it on any death path.
-    ///   • Blob Swordfish Fauna Config Data (FaunaConfigurationSO, apex-tier numbers mirroring
-    ///     the shark: seed floor 1, cap 2, births on 6 kills) and swaps the shark entry for the
-    ///     swordfish in the Blob Cell Spawn Profile. The shark assets stay authored for other
-    ///     biomes; only the menu flagship slot changes hands.
+    ///     extremity-first wither dissolve and spawn gets the condense-in.
+    ///   • A dormant authored CrystalMass child (Crystal + pickup collider disabled, shark parity) —
+    ///     the locked "every lifeform drops one elemental crystal" invariant; Fauna.Die activates it.
+    ///   • Root LightFauna — diet Predator, 45s starvation clock, Runtime Cell Data + a new
+    ///     MassSwordfishFaunaDataSO (fastest fauna in the sea: out-swims the shark).
+    ///   • Blob Swordfish Fauna Config Data (apex numbers 1:1 with the shark slot) swapped into the
+    ///     Blob Cell Spawn Profile. The shark assets stay authored for other biomes.
     ///
-    /// Run via Tools ▸ Cosmic Shore ▸ Build Swordfish Flagship Fauna. Idempotent: the prefab is
-    /// rebuilt in place (same GUID), existing SO assets keep their human-tuned values and only
-    /// re-point at the rebuilt prefab. After running, run Tools ▸ Cosmic Shore ▸ Validate
-    /// Lifeform Crystals.
+    /// Run via Tools ▸ Cosmic Shore ▸ Build Swordfish Flagship Fauna, then run
+    /// Tools ▸ Cosmic Shore ▸ Validate Lifeform Crystals. Idempotent (rebuilds in place, keeps
+    /// human-tuned SO values). Placement is geometry-driven; open the prefab and nudge any prism —
+    /// they're parented to the bones exactly like the shark's, so hand-tuning is straightforward.
     /// </summary>
     public static class SwordfishFaunaSetupTool
     {
@@ -55,16 +60,19 @@ namespace CosmicShore.Editor
         const string DangerBlockPrefabPath = "Assets/_Prefabs/Trails/DangerBlock.prefab";
         const string CrystalPrefabPath = "Assets/_Prefabs/Environment/CrystalMass.prefab";
         const string BodyMaterialPath = "Assets/_Graphics/Materials/SpindleMaterial.mat";
-        const string AccentMaterialPath = "Assets/_Graphics/Materials/CrystalMaterials/BlueMassCrystalMaterial.mat";
 
-        // --- Body layout ------------------------------------------------------------------------
-        // Shark-scale creature. Stations are fractions of body length measured from the NOSE
-        // (bill tip): the sword carries DangerBlocks, the body carries DynamicHealthBlocks.
-        // 3 + 5 = 8 body prisms vs the shark's 10 — collider budget strictly ≤ the slot it takes.
-        const float TargetBodyLength = 30f;
```

</details>

### `2c2a41a65` — feat(toys): the Arkway — a corridor of cells an Ark sails (the cellular Wanderway)

_Claude, 2026-09-01 03:00:47 +0000_

```text
The Arkway is the Wanderway's proposition raised one level: instead of a belt
of prism assemblies, it recycles whole CELLS. Fly the toy and a voyage begins —
three real satellite Cells stand at once (previous / current / next), drawn
shuffle-bag from the cell selector's own rotation, thinned by
SatellitePrismStride — and an Ark sails the corridor at its own unhurried pace.
A stepping stone toward faction missions.

The Ark is a new FUNDAMENTAL (added at the prompter's explicit request, per the
CLAUDE.md curation process): a prism-bodied mothership that wears a domain,
travels the hypersea, and lives or dies by the food web. Its hull is ordinary
grazeable conserved mass laid through the canonical PrismTrailBuilder path, so
the whole protect-the-Ark mechanic is composition, not construction: traversal
cells set NucleusIsControlZone = false (whole-cell VOLUME control + the legacy
opposing-domain diet), fauna waves spawn in the controlling colour, and
therefore taking a cell's volume IS protecting the Ark — its own domain's fauna
cannot eat it, opposing waves hunt it. Last hull prism lost = the voyage resets.
Players are leashed to a cell radius of the Ark (telegraphed countdown, then a
recall to its side); exits are the disembark dinghy trailing the Ark, a second
toy pass, leaving freestyle, or the Ark falling.

Three small platform capabilities carry it:
- Cell.SatelliteEcologyEnabled — the one opt-in through the mode preview's
  structure-only satellite gate (a traversal cell RUNS its life spawner).
- Cell.RuntimePopulationScale — runtime population multiplier composed into
  ResolveFauna/FloraPopulation on the profile scaler's own contract.
  Production gating only; identity at 1; never culls.
- PrismSpatialIndex.NotifyCellChanged — the mover's cell re-bind (UpdatePosition
  re-buckets but never re-bound the cell; nothing that moved crossed a cell
  before the Ark).

The Ark moves the way fauna move (container transform + the
Prism.NotifyPositionChanged mover contract per frame, cell re-bind on a coarse
cadence). Cell recycling is gated on the retiring cell's whole membrane sphere
being outside the camera frustum (the microscene conveyor's own removal gate);
voyage end reposes the player home first, withers the Ark back to its pool, and
strikes the corridor pool-safely with the 150-per-frame drain.

Budget: three cells at stride 4 ≈ ≤30k prisms — the Wanderway-stock envelope —
against a bare-canvas home world (the voyage opens with the Wanderway's own
host-cell revert). Record: Docs/ECOSYSTEM.md §41, Docs/ToySystem/ARCHITECTURE.md
§ Arkway, CLAUDE.md fundamentals list.

Verification status: authored and reviewed headless (Roslyn parse +
check_conditional_compilation clean); not yet opened in the Unity editor —
needs an in-editor pass (Setup Freestyle Toybox re-run is idempotent; the
Toy_Arkway.asset + Toybox.asset registration are authored in this commit).
```

```text
 Assets/Resources/Toybox.asset                                        |   1 +
 Assets/_SO_Assets/Toys/Toy_Arkway.asset                              |  36 +++
 Assets/_SO_Assets/Toys/Toy_Arkway.asset.meta                         |   8 +
 Assets/_Scripts/Controller/Environment/Ark.cs                        | 348 +++++++++++++++++++++++
 Assets/_Scripts/Controller/Environment/Ark.cs.meta                   |  11 +
 Assets/_Scripts/Controller/Environment/Cell.cs                       |  52 +++-
 Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs             |  24 ++
 Assets/_Scripts/Controller/Toys/ArkwayRun.cs                         | 497 +++++++++++++++++++++++++++++++++
 Assets/_Scripts/Controller/Toys/ArkwayRun.cs.meta                    |  11 +
 Assets/_Scripts/Controller/Toys/ArkwayToy.cs                         | 192 +++++++++++++
 Assets/_Scripts/Controller/Toys/ArkwayToy.cs.meta                    |  11 +
 Assets/_Scripts/Controller/Toys/ArkwayVoyageHud.cs                   |  91 ++++++
 Assets/_Scripts/Controller/Toys/ArkwayVoyageHud.cs.meta              |  11 +
 Assets/_Scripts/Controller/Toys/CellConveyor.cs                      | 410 +++++++++++++++++++++++++++
 Assets/_Scripts/Controller/Toys/CellConveyor.cs.meta                 |  11 +
 Assets/_Scripts/Controller/Toys/ToyContext.cs                        |   8 +
 Assets/_Scripts/Controller/Toys/ToyboxController.cs                  |   8 +
 Assets/_Scripts/Editor/Codex/ToolCodexHarvester.cs                   |  41 +++
 Assets/_Scripts/Editor/ToyboxSetupTool.cs                            |  34 ++-
 Assets/_Scripts/ScriptableObjects/Toys/ArkwayToyDefinitionSO.cs      | 146 ++++++++++
 Assets/_Scripts/ScriptableObjects/Toys/ArkwayToyDefinitionSO.cs.meta |  11 +
 CLAUDE.md                                                            |  22 ++
 Docs/ECOSYSTEM.md                                                    | 110 ++++++++
 Docs/ToySystem/ARCHITECTURE.md                                       |  59 +++-
 Docs/ToySystem/BACKLOG.md                                            |  36 +++
 25 files changed, 2175 insertions(+), 14 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 2321 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Ark.cs b/Assets/_Scripts/Controller/Environment/Ark.cs
new file mode 100644
index 000000000..40eb8481a
--- /dev/null
+++ b/Assets/_Scripts/Controller/Environment/Ark.cs
@@ -0,0 +1,348 @@
+using System;
+using System.Collections.Generic;
+using System.Threading;
+using CosmicShore.Data;
+using CosmicShore.Utility;
+using Cysharp.Threading.Tasks;
+using UnityEngine;
+
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// An <b>Ark</b> - a mothership: a prism-bodied home that travels the hypersea, wears a
+    /// domain, and lives or dies by the food web. The Ark is a first-class fundamental (added at
+    /// the prompter's request - CLAUDE.md, "The fundamentals"): it is the anchor of the faction-
+    /// mission arc, and its first vehicle is the Arkway toy, where it sets the pace of a voyage
+    /// through a corridor of cells.
+    ///
+    /// Everything about it composes with the shipped fundamentals instead of adding parallel
+    /// systems:
+    ///
+    ///   • Its HULL is ordinary conserved prism mass, laid through the canonical
+    ///     <see cref="PrismTrailBuilder"/> path in its owner's domain - so it registers with the
+    ///     spatial index, binds to the cell that contains it, feeds that cell's volume books, and
+    ///     is GRAZEABLE: in a nucleus-less cell, herbivores of another domain eat it and
+    ///     herbivores of its own never do. Protecting an Ark is therefore controlling the cell -
+    ///     no aggro system, no scripted threat.
+    ///   • It MOVES the way a creature moves: the hull prisms ride one container transform, and
+    ///     every frame each prism honours the mover contract
+    ///     (<see cref="Prism.NotifyPositionChanged"/> - spatial index + shell + render entity),
+    ///     exactly as fauna body prisms do. On a coarse cadence each prism also re-binds to the
+    ///     cell that actually contains it (<see cref="PrismSpatialIndex.NotifyCellChanged"/>),
+    ///     so the food web that can see it is always the local one. Between cells it binds to
+    ///     nothing - open water is nobody's feeding ground.
+    ///   • It DIES the way a creature dies - when its last hull prism is destroyed - but it is
+    ///     deliberately NOT a <see cref="LifeForm"/>: no elemental heart (the lifeform-crystal
+    ///     invariant governs lifeforms; an Ark is a vessel-like home, not a creature), no
+    ///     starvation clock, no reproduction. Its only deaths are active forces: fauna
+    ///     consumption and player abilities.
+    ///
+    /// The Ark itself never removes mass and never runs a timer over anyone else's - the one
+    /// clock it owns is its own unhurried course.
+    /// </summary>
+    public sealed class Ark : MonoBehaviour
+    {
+        // ── Hull proportions ─────────────────────────────────────────────────
+        // The hull is a spindle: rings of plates along the keel axis with a lens radius profile,
+        // staggered ring to ring so the plating reads as a shell rather than a stack of hoops.
+        const float PlateSpacing = 9f;                    // arc length per plate around a ring
+        const float RingSpacing = 9.5f;                   // keel distance between rings
+        const float RadiusFactor = 0.22f;                 // max hull radius = length × this
+        static readonly Vector3 PlateScale = new(2.6f, 2.6f, 4.8f);
+        static readonly Vector3 CapScale = new(3.4f, 3.4f, 6.4f);
+
+        /// <summary>Scale a retiring hull prism withers to before returning to the pool
+        /// (the Wanderway tether's own exit - continuity of existence is not waived).</summary>
+        static readonly Vector3 RetiredScale = new(0.02f, 0.02f, 0.02f);
+        const float WitherSeconds = 0.8f;
+
+        const float AliveScanSeconds = 0.5f;              // hull-integrity scan cadence
+        const float CellRebindSeconds = 2.5f;             // cell re-bind + grid re-file cadence
+        const float TurnDegreesPerSecond = 40f;           // how fast the bow swings onto course
+
+        readonly List<Prism> _prisms = new();
+        Trail _trail;
+        float _speed;
+        Vector3 _destination;
+        bool _hasDestination;
+        bool _laying;
+        bool _layComplete;
+        bool _hullLost;
+        bool _retiring;
+        float _nextAliveScanAt;
+        float _nextRebindAt;
+        TMPro.TMP_Text _label;
```

</details>

### `4c0b18468` — fix(toys): ArkwayToyDefinitionSO missing 'using CosmicShore.Data' for ToyCategory

_Claude, 2026-09-01 03:06:43 +0000_

```text
 Assets/_Scripts/ScriptableObjects/Toys/ArkwayToyDefinitionSO.cs | 1 +
 1 file changed, 1 insertion(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/ScriptableObjects/Toys/ArkwayToyDefinitionSO.cs b/Assets/_Scripts/ScriptableObjects/Toys/ArkwayToyDefinitionSO.cs
index b4da949fb..1f0e04f21 100644
--- a/Assets/_Scripts/ScriptableObjects/Toys/ArkwayToyDefinitionSO.cs
+++ b/Assets/_Scripts/ScriptableObjects/Toys/ArkwayToyDefinitionSO.cs
@@ -1,4 +1,5 @@
 using System.Collections.Generic;
+using CosmicShore.Data;
 using CosmicShore.Gameplay;
 using CosmicShore.Utility;
 using UnityEngine;
```

</details>

### `c3b555da5` — fix(toys): apply the Arkway adversarial-review findings

_Claude, 2026-09-01 07:00:28 +0000_

```text
Eight findings from the change-set review (5 confirmed by two-skeptic
verification, 3 verified by hand after their verifiers hit the session limit):

- BLOCKER: the leash recall teleported the player exactly onto the disembark
  dinghy's armed trigger (recall point == dinghy point), ending the voyage as
  punishment for straying. Recall now lands on the Ark's FLANK, ~1.46 hull
  lengths from the dinghy.
- Cell.gridTracked now remembers each prism's FILED position and RemoveBlock
  decrements the density grids there, never at a re-read transform.position:
  mass that MOVES between add and remove (the Ark's hull, gyroid bonding)
  decremented the wrong bucket and stranded permanent phantom counts in the
  fauna steering grids — and a destroyed ref skipped grid removal entirely
  (the same leak from another door, now also closed).
- Voyage-end corridor retirement is QUEUED behind the off-screen frustum gate
  (RetireAllWhenUnseen) instead of force-struck: the corridor sits a few
  thousand units out, not the preview's 120k, so an ungated strike could pop
  whole worlds out in view. The next voyage's Begin force-strikes any
  remainder only after its veil is up (unseen by construction), and awaits
  conveyor idle first.
- CellConveyor drain bookkeeping: _draining bool → _drains counter (two
  overlapping drains both cleared the bool, reopening the one-at-a-time
  gate), and Begin refuses while the previous corridor is still retiring.
- TickCorridor's cannot-stand-a-next-cell End no longer falls through into
  TickLeash on the same frame (NRE on the nulled _ark).
- Leash hysteresis band no longer freezes the countdown while silently
  spending the grace: a breach clears only at genuine re-entry, and the
  countdown keeps displaying across the band.
- Ark.RetireAsync's pool-return was dead code (environment-pool prisms carry
  no OnReturnToPool handler): the retire is now honestly documented as the
  environment-mass destroy-drain, the defensive pool branch detaches first,
  and the per-frame sync loops skip devoured prisms (Consume leaves the
  GameObject ACTIVE with destroyed=true).
- Toy_Arkway placement angle 180 → 210: Toy_LifeformMatrix already owns 180,
  and two toys on one angle stack at the same point of the membrane ring.
```

```text
 Assets/_SO_Assets/Toys/Toy_Arkway.asset         |  2 +-
 Assets/_Scripts/Controller/Environment/Ark.cs   | 28 ++++++++++++++++++-----
 Assets/_Scripts/Controller/Environment/Cell.cs  | 44 +++++++++++++++++++++----------------
 Assets/_Scripts/Controller/Toys/ArkwayRun.cs    | 66 ++++++++++++++++++++++++++++++++++++++++++-------------
 Assets/_Scripts/Controller/Toys/CellConveyor.cs | 53 ++++++++++++++++++++++++++++++++++++--------
 Assets/_Scripts/Editor/ToyboxSetupTool.cs       |  4 +++-
 Docs/ECOSYSTEM.md                               | 12 ++++++----
 Docs/ToySystem/ARCHITECTURE.md                  | 11 ++++++----
 8 files changed, 161 insertions(+), 59 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 443 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Ark.cs b/Assets/_Scripts/Controller/Environment/Ark.cs
index 40eb8481a..7178e99c9 100644
--- a/Assets/_Scripts/Controller/Environment/Ark.cs
+++ b/Assets/_Scripts/Controller/Environment/Ark.cs
@@ -232,11 +232,15 @@ namespace CosmicShore.Gameplay
             // each prism's spatial-index position, shell pose and render-entity matrix follow the
             // transform (Prism.NotifyPositionChanged is cheap when the occupancy bucket is
             // unchanged).
+            // NOTE the !destroyed gate: a devoured environment prism never deactivates - Consume
+            // → SetupDestruction leaves the GameObject ACTIVE with destroyed=true, hidden and
+            // collider-less - so activeInHierarchy alone would keep paying sync for every prism
+            // the food web has already taken.
             if (moved)
                 for (int i = 0; i < _prisms.Count; i++)
                 {
                     var prism = _prisms[i];
-                    if (prism && prism.gameObject.activeInHierarchy)
+                    if (prism && !prism.destroyed && prism.gameObject.activeInHierarchy)
                         prism.NotifyPositionChanged();
                 }
 
@@ -251,7 +255,8 @@ namespace CosmicShore.Gameplay
                     for (int i = 0; i < _prisms.Count; i++)
                     {
                         var prism = _prisms[i];
-                        if (prism && prism.gameObject.activeInHierarchy && prism.SpatialIndexId >= 0)
+                        if (prism && !prism.destroyed && prism.gameObject.activeInHierarchy
+                            && prism.SpatialIndexId >= 0)
                             index.NotifyCellChanged(prism.SpatialIndexId);
                     }
             }
@@ -293,10 +298,15 @@ namespace CosmicShore.Gameplay
 
         /// <summary>
         /// End-of-voyage exit: the surviving hull withers out (one grow-clock re-stamp per prism,
-        /// the Wanderway tether's own retirement) and returns to the pool it was laid from, then
-        /// the Ark destroys itself. This is the voyage apparatus being struck by the explicit,
-        /// player-initiated end of the toy - the same event class as a satellite world's strike
-        /// (Docs/ECOSYSTEM.md §19) - never a decay: a live voyage never calls it.
+        /// the Wanderway tether's own retirement) and is then retired the way environment mass
+        /// is retired everywhere - hull prisms come from the environment pool and carry NO
+        /// pool-return handler (the strike partition test is <c>OnReturnToPool != null</c>), so
+        /// they are destroy-drained with the Ark's own root, exactly like a swapped world's
+        /// environment. A prism that DOES wear a return handler is handed back to its pool
+        /// instead - defensive, for a future trail-pooled hull prefab. This is the voyage
+        /// apparatus being struck by the explicit, player-initiated end of the toy - the same
+        /// event class as a satellite world's strike (Docs/ECOSYSTEM.md §19) - never a decay:
+        /// a live voyage never calls it.
         /// </summary>
         public async UniTask RetireAsync(CancellationToken ct)
         {
@@ -334,11 +344,17 @@ namespace CosmicShore.Gameplay
                 cancelled = true; // still hand the pool its prisms back below
             }
 
+            // Pool-carrying prisms (none today - see the summary) go home; everything else is
+            // instantiated-class mass and dies with the root below, ~150 prisms in one frame
+            // (well under the 150-per-frame drain slice a 10-20k world needs).
             for (int i = 0; i < _prisms.Count; i++)
             {
                 var prism = _prisms[i];
                 if (prism && prism.gameObject.activeInHierarchy && prism.OnReturnToPool != null)
+                {
+                    prism.transform.SetParent(null, false);
                     prism.ReturnToPool();
+                }
             }
             _prisms.Clear();
 
diff --git a/Assets/_Scripts/Controller/Environment/Cell.cs b/Assets/_Scripts/Controller/Environment/Cell.cs
index ec92b8782..a92edb458 100644
--- a/Assets/_Scripts/Controller/Environment/Cell.cs
+++ b/Assets/_Scripts/Controller/Environment/Cell.cs
@@ -262,7 +262,14 @@ namespace CosmicShore.Gameplay
         // be led to mass they cannot eat - so RemoveBlock has to know which
         // prisms the grids really hold (the nucleus radius can change between
         // Add and Remove; re-deriving membership would desync bucket counts).
-        readonly HashSet<Prism> gridTracked = new();
+        // Grid-registered prisms → the POSITION their grid entries were filed at. The value is
+        // what keeps Add/Remove symmetric for mass that MOVES (the Ark's hull, gyroid bonding):
```

</details>

### `19a93b27c` — docs(skills): two ecology traps from the Arkway session — mover grid symmetry, environment-prism lifecycle

_Claude, 2026-09-01 15:18:24 +0000_

```text
 .claude/skills/ecology/SKILL.md | 20 +++++++++++++++++++-
 1 file changed, 19 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/.claude/skills/ecology/SKILL.md b/.claude/skills/ecology/SKILL.md
index 55d372e64..559a8a6e9 100644
--- a/.claude/skills/ecology/SKILL.md
+++ b/.claude/skills/ecology/SKILL.md
@@ -76,7 +76,25 @@ what the carve-out silently broke — see the traps below.
   restores the envelope exactly - §35's "fit the PRISM, never the pattern"). Full table + the
   clearance and hinge consequences: the `asset-surgery` skill, "Trap: a SHIELD's size is not the
   prism's size".
-- **Static bookkeeping outlives the world it describes.** A `static` registry/claim book/
+- **A positional Add/Remove pair is asymmetric for anything that MOVES.** `Cell.AddBlock` files
+  the fauna density grids at the position read at add time; a `RemoveBlock` that re-reads
+  `transform.position` decrements whichever bucket the prism has since wandered into, stranding
+  a permanent phantom count in the bucket it was actually filed under — fauna then steer at
+  empty space, forever, with nothing logging. Movers exist (fauna bodies are exempt as
+  volume-only, but the Ark's hull and gyroid bonding are grid-tracked movers), so the cell now
+  stores each grid entry's FILED position (`gridTracked` is a `Dictionary<Prism, Vector3>`) and
+  removes there. The general rule: any add/remove bookkeeping keyed on a re-read position is a
+  leak for movers AND for destroyed refs (whose transform is unreadable at remove time) —
+  remember what you filed, remove what you remembered. Docs/ECOSYSTEM.md §41.
+- **An environment prism is not "pooled", and a devoured one never deactivates.** The
+  pooled-vs-instantiated partition everywhere (cell swap, satellite strike, tether recycle) is
+  `Prism.OnReturnToPool != null` — and `EnvironmentPrismPool` never wires it, so every
+  environment-laid prism is INSTANTIATED-class mass (destroy-drained on retirement; the pool's
+  own `TryRelease` is called only by the swap drain). And `Prism.Consume` → `SetupDestruction`
+  leaves the GameObject ACTIVE — `destroyed = true`, render hidden, collider off — so an
+  aliveness test on `activeInHierarchy` alone counts eaten mass as alive, and a per-frame loop
+  gated on it keeps paying for corpses. Test `!prism.destroyed` too, and never write a
+  retirement that waits for a pool return that structurally cannot come. A `static` registry/claim book/
   frontier that coordinates a population survives every cell teardown — `Cell.ResetCell`,
   `Initialize`, and the Cell-Selector world swap all destroy the lifeforms and leave the
   static state standing, so the NEXT world inherits the dead one's entries and acts on
```

</details>

### `4fcc597b1` — fix(party): a party is always x/4, and a guest follows the host onto the arcade screen

_Claude, 2026-09-01 17:26:24 +0000_

```text
Two reported defects, plus a test that was already red on bleeding-edge.

1. "Why is the lobby showing 1/6" - maxPartySlots is TRANSPORT CAPACITY
   (raised 4->6 in 83ada380 on purpose, one spare seat of anti-flicker
   headroom so a transient double-count cannot throw the fourth invite
   out as "party full"), and its own comment says "Capacity, NOT the
   UI's party size". But it was ALSO what the lobby rendered, what
   PARTY_MAX_KEY published to every peer, and what presence carried -
   so everyone read 1/6, and the LOBBY FULL badge waited for a fifth
   and sixth member the design never seats. Split the two questions:
   HostConnectionDataSO.PartyDisplaySlots (4, clamped to capacity) is
   now what the UI, the published property and presence use, and
   HasOpenDisplaySlots gates the invite affordance while HasOpenSlots
   stays the looser transport check. The remote row IGNORES a peer's
   published max and draws the local display size, so a peer on an
   older build still publishing 6 can never put "x/6" on screen.

2. "Once the host clicks a card the client should be transferred to
   that card in the app shell" - ScreenSwitcher.NavigateTo refuses ARK
   outright for a guest ("Arcade is host-only in multiplayer
   sessions"), so the card modal opened over whatever screen the guest
   was on and closing it left them somewhere unrelated. Being PULLED by
   the host is a different act from browsing, so it gets its own entry
   point (FollowHostToArcadeScreen) rather than a hole in the guard -
   nothing on a guest's own UI calls it - and the client's config-open
   handler moves the shell before opening the modal.

3. HostConnectionDataSOTests asserted MaxPartySlots == 4 against a
   shipped default of 6 (83ada380 changed the field, not the test), so
   the edit-mode suite was failing before this branch. Now asserts the
   capacity, the display size, and display <= capacity.

Verification: Roslyn parse-check clean on all nine touched files;
conditional-compilation gate OK (1857 files); every renamed/added
member checked against its declaring file. NOT verified in the Unity
editor - re-test: the lobby and every online row read x/4; a guest is
carried to the arcade screen when the host opens a card.
```

```text
 Assets/_SO_Assets/Host Connection Data/HostConnectionData.asset   |  1 +
 Assets/_Scripts/Controller/Party/FriendsInitializer.cs            |  6 ++++--
 Assets/_Scripts/Controller/Party/HostConnectionService.cs         |  4 +++-
 Assets/_Scripts/Controller/Party/Services/PartySessionService.cs  |  3 ++-
 Assets/_Scripts/Controller/Party/Services/PresenceLobbyService.cs |  4 +++-
 Assets/_Scripts/Tests/Editor/HostConnectionDataSOTests.cs         | 24 ++++++++++++++++++++----
 Assets/_Scripts/UI/Elements/FriendsListPanel.cs                   | 15 ++++++++++++---
 Assets/_Scripts/UI/Modals/ArcadeGameConfigureModal.cs             |  8 ++++++++
 Assets/_Scripts/UI/Modals/ModalWindowManager.cs                   |  4 ++++
 Assets/_Scripts/UI/ScreenSwitcher.cs                              | 17 +++++++++++++++++
 Assets/_Scripts/Utility/DataContainers/HostConnectionDataSO.cs    | 24 ++++++++++++++++++++++++
 11 files changed, 98 insertions(+), 12 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 237 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Party/FriendsInitializer.cs b/Assets/_Scripts/Controller/Party/FriendsInitializer.cs
index cc4740e9f..8c6e4bd1c 100644
--- a/Assets/_Scripts/Controller/Party/FriendsInitializer.cs
+++ b/Assets/_Scripts/Controller/Party/FriendsInitializer.cs
@@ -152,7 +152,8 @@ namespace CosmicShore.Gameplay
             var partySessionId = _partyQuery?.ActivePartySessionId ?? "";
             int memberCount = hostConnectionData != null && hostConnectionData.PartyMembers != null
                 ? hostConnectionData.PartyMembers.Count : 0;
-            int maxSlots = hostConnectionData != null ? hostConnectionData.MaxPartySlots : 0;
+            // The party size players SEE (4), never the transport capacity (6).
+            int maxSlots = hostConnectionData != null ? hostConnectionData.PartyDisplaySlots : 0;
 
             await friendsService.SetPresenceAsync(
                 Availability.Online,
@@ -172,7 +173,8 @@ namespace CosmicShore.Gameplay
             var partySessionId = _partyQuery?.ActivePartySessionId ?? "";
             int memberCount = hostConnectionData != null && hostConnectionData.PartyMembers != null
                 ? hostConnectionData.PartyMembers.Count : 0;
-            int maxSlots = hostConnectionData != null ? hostConnectionData.MaxPartySlots : 0;
+            // The party size players SEE (4), never the transport capacity (6).
+            int maxSlots = hostConnectionData != null ? hostConnectionData.PartyDisplaySlots : 0;
 
             await friendsService.SetPresenceAsync(
                 Availability.Busy,
diff --git a/Assets/_Scripts/Controller/Party/HostConnectionService.cs b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
index 23536be2d..93998a1ea 100644
--- a/Assets/_Scripts/Controller/Party/HostConnectionService.cs
+++ b/Assets/_Scripts/Controller/Party/HostConnectionService.cs
@@ -1922,8 +1922,10 @@ namespace CosmicShore.Gameplay
             {
                 lobby.CurrentPlayer.SetProperty(PARTY_COUNT_KEY,
                     new PlayerProperty(currentCount.ToString(), VisibilityPropertyOptions.Public));
+                // Displayed party size (4), not transport capacity (6) - publishing the
+                // capacity is what made every remote row read "1/6".
                 lobby.CurrentPlayer.SetProperty(PARTY_MAX_KEY,
-                    new PlayerProperty(connectionData.MaxPartySlots.ToString(), VisibilityPropertyOptions.Public));
+                    new PlayerProperty(connectionData.PartyDisplaySlots.ToString(), VisibilityPropertyOptions.Public));
                 lobby.CurrentPlayer.SetProperty(MATCH_NAME_KEY,
                     new PlayerProperty(currentMatch ?? string.Empty, VisibilityPropertyOptions.Public));
                 // Identity reconciliation: rides the same single save so a rename
diff --git a/Assets/_Scripts/Controller/Party/Services/PartySessionService.cs b/Assets/_Scripts/Controller/Party/Services/PartySessionService.cs
index e12a10721..5963ff38d 100644
--- a/Assets/_Scripts/Controller/Party/Services/PartySessionService.cs
+++ b/Assets/_Scripts/Controller/Party/Services/PartySessionService.cs
@@ -349,7 +349,8 @@ namespace CosmicShore.Gameplay
         private Dictionary<string, PlayerProperty> BuildLocalPlayerProperties()
         {
             int partyCount = _connectionData.PartyMembers != null ? _connectionData.PartyMembers.Count : 0;
-            int partyMax   = _connectionData.MaxPartySlots;
+            // Displayed party size, not transport capacity - see PresenceLobbyService.
+            int partyMax   = _connectionData.PartyDisplaySlots;
 
             return new Dictionary<string, PlayerProperty>
             {
diff --git a/Assets/_Scripts/Controller/Party/Services/PresenceLobbyService.cs b/Assets/_Scripts/Controller/Party/Services/PresenceLobbyService.cs
index c701e3b69..d79449512 100644
--- a/Assets/_Scripts/Controller/Party/Services/PresenceLobbyService.cs
+++ b/Assets/_Scripts/Controller/Party/Services/PresenceLobbyService.cs
@@ -340,7 +340,9 @@ namespace CosmicShore.Gameplay
         internal Dictionary<string, PlayerProperty> BuildLocalPlayerProperties()
         {
             int partyCount = _connectionData.PartyMembers != null ? _connectionData.PartyMembers.Count : 0;
-            int partyMax   = _connectionData.MaxPartySlots;
+            // The DISPLAYED party size, never the transport capacity: this value is what every
+            // other peer renders as "N/M" and what their LOBBY FULL badge compares against.
+            int partyMax   = _connectionData.PartyDisplaySlots;
 
             var props = new Dictionary<string, PlayerProperty>
             {
diff --git a/Assets/_Scripts/Tests/Editor/HostConnectionDataSOTests.cs b/Assets/_Scripts/Tests/Editor/HostConnectionDataSOTests.cs
index 1066ea9da..648e4c733 100644
--- a/Assets/_Scripts/Tests/Editor/HostConnectionDataSOTests.cs
+++ b/Assets/_Scripts/Tests/Editor/HostConnectionDataSOTests.cs
@@ -176,13 +176,29 @@ namespace CosmicShore.Tests
 
         #endregion
 
-        #region MaxPartySlots
+        #region Party slots
 
```

</details>

### `934218c2f` — fix(party): the joining client's vessel spawn could never retry, so every slow join bounced

_Claude, 2026-09-01 17:32:39 +0000_

```text
Reported: an invite is sent and accepted, the HOST can see the joining
player's object - and then that player is kicked back to their own menu
before they ever land in the party.

The join's success signal is GameDataSO.OnClientReady, raised when the
guest's own vessel pair initialises; PartyInviteController waits 30s for
it and bounces on timeout. So the guest is bounced whenever the HOST
never spawns their vessel.

That is exactly what happens on a slow link. Player.OnNetworkSpawn
raises the spawn event behind a ONE-SHOT latch (_spawnEventRaised).
ServerPlayerVesselInitializer receives it, waits ~2s for the
owner-written NetName / vessel type to replicate, and on timeout drops
the player from _processedPlayers and returns - logging "Will retry on
deferred event". But the deferred re-raise is guarded by that same
latch, which THIS event already spent: the branch is only ever reached
after the raise that started the handler. So nothing retried, no vessel
was ever spawned, OnClientReady never fired, and the watchdog bounced a
player the host could see perfectly well. On a LAN the values land
inside 2s and it never fires - it is latency-shaped, like the five
defects in 83ada380.

Fix: Player.ReArmDeferredSpawnEvent() (server-only) clears the latch and
immediately re-tests, since the values may have landed during the
spawner's own retry loop, leaving no future replication callback to ride.
The spawner calls it in the give-up branch, making its own comment true.

Re-arming re-enters the handler, so it is BOUNDED: MaxSpawnReArms (6)
per player, ~2.2s each, so ~13s of extra replication budget on top of
the first pass, then one loud error naming the cause. The counter clears
on spawn success and on despawn.

Verification: Roslyn parse-check clean on both files;
conditional-compilation gate OK. NOT verified in the Unity editor -
re-test: invite + accept across a real link; the guest should land in
the party with a vessel instead of bouncing, and a slow join should log
the re-arm rather than the bounce.
```

```text
 .../_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs  | 41 +++++++++++++++++++++++++++++++--
 Assets/_Scripts/Controller/Player/Player.cs                           | 29 +++++++++++++++++++++++
 2 files changed, 68 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 113 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs
index b988b6d20..15423267f 100644
--- a/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs
+++ b/Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializer.cs
@@ -124,6 +124,16 @@ namespace CosmicShore.Gameplay
         /// </summary>
         readonly HashSet<ulong> _preparedForScene = new();
 
+        /// <summary>
+        /// How many times the spawn event has been re-armed for a player whose owner-written
+        /// values had not replicated yet, keyed by NetworkObjectId. Bounds the re-raise loop.
+        /// </summary>
+        readonly Dictionary<ulong, int> _spawnReArms = new();
+
+        /// <summary>Re-arm budget per player. Each round costs the ~2.2s readiness wait below,
+        /// so this covers roughly 13 further seconds of replication delay before giving up.</summary>
+        const int MaxSpawnReArms = 6;
+
         protected virtual void Awake()
         {
             _netcodeHooks = GetComponent<NetcodeHooks>();
@@ -213,6 +223,7 @@ namespace CosmicShore.Gameplay
                 clientPlayerVesselInitializer.OnRosterRequested = null;
             _processedPlayers.Clear();
             _preparedForScene.Clear();
+            _spawnReArms.Clear();
             _cellSpawnRingBuilt = false; // a replay re-spawns the cell; rebuild against the new nucleus
 
             _cts?.Cancel();
@@ -329,15 +340,41 @@ namespace CosmicShore.Gameplay
 
                 if (!IsReadyToSpawn(player))
                 {
-                    // Still not ready after retries - remove from processed so the
-                    // deferred spawn event (Player.TryRaiseDeferredSpawnEvent) can retry.
+                    // Still not ready after retries - remove from processed so the deferred spawn
+                    // event can retry, and RE-ARM that event. Dropping the processed entry alone
+                    // was not enough and silently could not work: the spawn-event latch is
+                    // one-shot and this branch is only ever reached AFTER it was spent (the raise
+                    // is what started this handler), so the "will retry" below was a promise
+                    // nothing could keep. A joining client then never got a vessel, its
+                    // OnClientReady never fired, and its 30s join watchdog bounced it back to its
+                    // own menu while the host sat there seeing the player object just fine.
                     _processedPlayers.Remove(player.NetworkObjectId);
+
+                    // BOUNDED: re-arming re-raises the event, which re-enters this handler, so an
+                    // owner whose values never arrive at all would spin here forever. Each round
+                    // costs ~2.2s of real waiting, so a handful of them covers a long link
+                    // (~13s on top of the 2s first pass) and then stops. Cleared when the player
+                    // finally spawns or despawns, so a later scene starts fresh.
+                    _spawnReArms.TryGetValue(player.NetworkObjectId, out int reArms);
+                    if (reArms < MaxSpawnReArms)
+                    {
+                        _spawnReArms[player.NetworkObjectId] = reArms + 1;
+                        player.ReArmDeferredSpawnEvent();
+                    }
+                    else
+                    {
+                        Debug.LogError($"[FLOW-5] [ServerVesselInit] Player {ownerClientId} never became " +
+                                       $"spawn-ready after {MaxSpawnReArms} re-arms - giving up. That client " +
+                                       "will bounce: its owner-written NetName / vessel type never replicated.");
+                    }
                     Debug.LogWarning($"<color=#FFA500>[FLOW-5] [ServerVesselInit] Player {ownerClientId} NOT ready after {maxRetries * retryIntervalMs}ms - VesselType={player.NetDefaultVesselType.Value}, Name='{player.NetName.Value}'. Will retry on deferred event.</color>");
                     return;
                 }
             }
 
             CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#00FF00>[FLOW-5] [ServerVesselInit] Player ready! Spawning vessel for {player.NetName.Value} (type={player.NetDefaultVesselType.Value})</color>");
+            // Readiness reached: this player owes no more re-arms.
+            _spawnReArms.Remove(player.NetworkObjectId);
             await OnPlayerReadyToSpawnAsync(player, ct);
         }
 
diff --git a/Assets/_Scripts/Controller/Player/Player.cs b/Assets/_Scripts/Controller/Player/Player.cs
index e8c345bbf..35e42fe14 100644
--- a/Assets/_Scripts/Controller/Player/Player.cs
+++ b/Assets/_Scripts/Controller/Player/Player.cs
@@ -808,6 +808,35 @@ namespace CosmicShore.Gameplay
         /// replicate, check if we can now raise the spawn event that was deferred
         /// in OnNetworkSpawn because the owner block was skipped.
```

</details>

### `81a6e4b3e` — fix(analytics): adopt sign-in/network state that was announced before the facade existed

_Claude, 2026-09-01 18:00:31 +0000_

```text
Garrett's crash log ends with, at app quit:

  [Analytics] DROPPING EVENTS - UGS sign-in has not completed.
  Nothing will reach UGS or PostHog until this is resolved.

That message reports the FIRST failed condition, so it proves the age
gate, eligibility, consent-decided and consent-granted all passed and it
stopped exactly at _signedIn - while the same session was demonstrably
signed in (it was sitting in a presence lobby as a guest). So the whole
play session produced no telemetry, to either sink.

Cause: AnalyticsServiceFacade is a LAZY DI singleton, constructed
whenever something first injects it - routinely AFTER sign-in and the
network probe have completed. It only ever subscribed to
AuthData.OnSignedIn / NetworkData.OnNetworkFound, with no
already-in-that-state fallback, so those one-shots had already fired,
nothing raises them again, and _signedIn / _isConnected stayed false for
the session. StartCollectionIfReady then never starts and every event is
dropped. This is the exact anti-pattern CLAUDE.md records against
AuthenticationSceneController ("a fast path that skips the
ANNOUNCEMENT"), one layer over.

Both flags mirror independently readable state (AuthenticationData
.IsSignedIn, NetworkMonitorData.IsOnline), so the constructor now
reconciles against it after subscribing. Idempotent: the handlers only
set a flag and re-run the guarded StartCollectionIfReady, so a real
event arriving later is a no-op.

Note PlayerDataService is NOT exposed to this - it polls
AuthenticationService.IsSignedIn directly.

Verification: Roslyn parse-check clean; conditional-compilation gate OK.
NOT verified in the Unity editor - re-test: play, quit, and confirm the
DROPPING EVENTS warning is gone and events reach UGS/PostHog.
```

```text
 Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs | 34 ++++++++++++++++++++++++++++++++++
 1 file changed, 34 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs b/Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs
index a18c92eb2..c15234dbe 100644
--- a/Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs
+++ b/Assets/_Scripts/System/Instrumentation/AnalyticsServiceFacade.cs
@@ -171,6 +171,40 @@ namespace CosmicShore.Core
             GameSetting.OnChangeHapticsLevel            += v => HandleSettingChanged("haptics_level", v);
             FavoriteSystem.OnFavoriteChanged            += HandleFavoriteChanged;
             UGSCloudSaveProvider.OnSaveFailed           += HandleCloudSaveFailed;
+
+            // Adopt what the one-shot events have ALREADY announced. This facade is a LAZY DI
+            // singleton, so it is constructed whenever something first injects it - which is
+            // routinely AFTER sign-in and after the network probe have completed. Subscribing
+            // is then not enough: OnSignedIn / OnNetworkFound have already fired, nothing will
+            // raise them again this session, and _signedIn / _isConnected stay false forever -
+            // so StartCollectionIfReady never starts and EVERY event is dropped, to UGS and
+            // PostHog alike. Observed in the field: a session that was demonstrably signed in
+            // (it was in a presence lobby) still logged "DROPPING EVENTS - UGS sign-in has not
+            // completed" at quit, leaving that whole play session with no telemetry at all.
+            //
+            // Both flags mirror state that is independently readable, so the correct fix is to
+            // reconcile with it rather than to hope for the raise. Idempotent by construction:
+            // the handlers only set a flag and re-run the guarded StartCollectionIfReady, so a
+            // genuine event arriving afterwards is a no-op.
+            ReconcileWithAlreadyAnnouncedState();
+        }
+
+        /// <summary>
+        /// Catch up on <see cref="HandleSignedIn"/> / <see cref="HandleNetworkFound"/> when the
+        /// events that would have called them fired before this facade existed.
+        /// </summary>
+        void ReconcileWithAlreadyAnnouncedState()
+        {
+            var auth = AuthData;
+            if (auth != null && auth.IsSignedIn && !_signedIn)
+            {
+                Log("Sign-in had already completed before this facade was constructed - adopting it.");
+                HandleSignedIn();
+            }
+
+            var network = NetworkData;
+            if (network != null && network.IsOnline && !_isConnected)
+                HandleNetworkFound();
         }
 
         #region Consent & collection lifecycle
```

</details>

### `f1c5556c0` — fix(camera): windowed-camera teardown survives a destroyed controller

_Claude, 2026-09-01 18:07:07 +0000_

```text
From Garrett's host log, on scene close:

  [ModePreview] Unwinding the ScarabScramble preview hit: The object of
  type 'CosmicShore.Gameplay.CustomCameraController' has been destroyed
  but you are still trying to access it.

EndWindowedPlayerCamera guards with `_playerCamera?.` - but
_playerCamera is an ICameraController, an INTERFACE reference, and the
null-conditional does not route through Unity's overloaded ==. A
DESTROYED CustomCameraController is therefore still non-null there, the
call goes through, and it throws. This path runs exactly while things
are being destroyed (play-mode exit, scene close), so it has to be
destroy-safe.

The throw aborted the mode preview's unwind partway, leaving the rest of
that teardown unrun - which is the likely source of the "Some objects
were not cleaned up when closing the scene" error logged one second
later in the same session.

Fixed with the project's documented idiom - test the OBJECT, never the
interface ref (the same rule ModePreviewSession.Alive and
Cell.SetVesselTrailsDetached already follow).

Verification: Roslyn parse-check clean; conditional-compilation gate OK.
NOT verified in the Unity editor - re-test: enter a preview, exit play
mode while it is up, and confirm neither the destroyed-object error nor
the scene-cleanup error appears.
```

```text
 Assets/_Scripts/Controller/Managers/CameraManager.cs | 14 ++++++++++++--
 1 file changed, 12 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Managers/CameraManager.cs b/Assets/_Scripts/Controller/Managers/CameraManager.cs
index d9e4cc8c5..335d5d7e7 100644
--- a/Assets/_Scripts/Controller/Managers/CameraManager.cs
+++ b/Assets/_Scripts/Controller/Managers/CameraManager.cs
@@ -341,14 +341,24 @@ namespace CosmicShore.Gameplay
             }
 
             _playerFollowTarget = _windowedPreviousTarget;
-            _playerCamera?.SetFollowTarget(_windowedPreviousTarget);
+
+            // Test the OBJECT, never the interface reference. ICameraController is an interface,
+            // and `?.` on an interface ref does NOT route through Unity's overloaded == - so a
+            // DESTROYED CustomCameraController is still non-null here and every call below throws
+            // "has been destroyed but you are still trying to access it". That is what aborted the
+            // mode-preview unwind on scene close, leaving the rest of the teardown unrun
+            // (reported as "[ModePreview] Unwinding the ScarabScramble preview hit: ..."). This
+            // path runs precisely when things are being destroyed, so it must be destroy-safe.
+            var playerCamera = _playerCamera is UnityEngine.Object pcObj && pcObj ? _playerCamera : null;
+
+            playerCamera?.SetFollowTarget(_windowedPreviousTarget);
             _windowedPreviousTarget = null;
 
             // Only stand it down if it is not the view the game is actually using: in a gameplay
             // scene this same rig IS the screen camera, and a preview must never be able to
             // switch it off there.
             if (_activeController != _playerCamera)
-                _playerCamera?.Deactivate();
+                playerCamera?.Deactivate();
         }
 
         public ICameraController GetActiveController() => _activeController;
```

</details>

### `4b8ca36af` — merge: bring bleeding-edge into the swordfish branch; retire the editor tool

_Claude, 2026-09-01 20:17:22 +0000_

```text
Docs/ECOSYSTEM.md resolved in upstream's favour: the branch's §7 edits
described a Blob-era apex slot and predate §40 (levels retired, four
elemental variants), §7.3 (mouth-driven predator) and the fauna network
sync. The swordfish will get its own section on the corrected work.

SwordfishFaunaSetupTool.cs is removed: it used the retired
Tools/Cosmic Shore menu root, never shipped any output, and the
corrected approach authors the prefab and its SO family from a
committed generator (asset-surgery), not from an in-editor tool.
```

```text
 Assets/_Scripts/Editor/SwordfishFaunaSetupTool.cs      | 571 -----------------------------------------------
 Assets/_Scripts/Editor/SwordfishFaunaSetupTool.cs.meta |  11 -
 2 files changed, 582 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 577 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/SwordfishFaunaSetupTool.cs b/Assets/_Scripts/Editor/SwordfishFaunaSetupTool.cs
deleted file mode 100644
index f605c08fe..000000000
--- a/Assets/_Scripts/Editor/SwordfishFaunaSetupTool.cs
+++ /dev/null
@@ -1,571 +0,0 @@
-using System.Collections.Generic;
-using System.Linq;
-using CosmicShore.Data;
-using CosmicShore.Gameplay;
-using CosmicShore.Utility;
-using UnityEditor;
-using UnityEditor.Animations;
-using UnityEngine;
-
-namespace CosmicShore.Editor
-{
-    /// <summary>
-    /// Builds the flagship swordfish fauna (MassSwordfishFauna) from the raw SwordFish_A model,
-    /// mirroring how the hand-authored MassSharkFauna sits its prisms on the skeleton — the shark's
-    /// blocks are parented to bones (Wing_2.L, MouthTop_2, Body_3, …) and each is oriented + sized
-    /// to the body part it wraps (a flat 2×12×6 wing slab, a needle-thin 1×1×15 tooth on the jaw),
-    /// NOT axis-aligned boxes clamped to arbitrary sizes. We reproduce that from geometry instead of
-    /// by hand:
-    ///
-    ///   • Per bone-cluster fitting — the tool runs INSIDE Unity, so it reads the live
-    ///     SkinnedMeshRenderer (bones + bindposes + boneWeights) and, for each bone, PCA-fits an
-    ///     oriented box to the rest-pose vertices that bone actually skins. The prism inherits that
-    ///     box's principal axes (so the dorsal slab stands vertical, the pectorals cant outward —
-    ///     real per-part orientation, not one boring root rotation) and is scaled to the real local
-    ///     silhouette, thinned to a slab, so nothing is oversized.
-    ///   • Bill = danger needles — the forward-most cluster (the sword) is tiled with thin DangerBlock
-    ///     needles laid END-TO-END along the bill so they don't overlap (each needle spans one
-    ///     segment of the bill length). The sword IS the weapon and danger prisms hit everyone, per
-    ///     the locked design.
-    ///   • Body = DynamicHealthBlock slabs on the other clusters (long clusters split in two for
-    ///     coverage), parented to their bone so they ride the swim animation.
-    ///   • Prisms bloom in via PrismScaleAnimator (continuity: nothing pops in) and are registered
-    ///     mass (Fauna.NotifyBodyPrismsMoved keeps the spatial index honest).
-    ///   • One Spindle per SkinnedMeshRenderer (RenderedObject wired) so death runs the sealed
-    ///     extremity-first wither dissolve and spawn gets the condense-in.
-    ///   • A dormant authored CrystalMass child (Crystal + pickup collider disabled, shark parity) —
-    ///     the locked "every lifeform drops one elemental crystal" invariant; Fauna.Die activates it.
-    ///   • Root LightFauna — diet Predator, 45s starvation clock, Runtime Cell Data + a new
-    ///     MassSwordfishFaunaDataSO (fastest fauna in the sea: out-swims the shark).
-    ///   • Blob Swordfish Fauna Config Data (apex numbers 1:1 with the shark slot) swapped into the
-    ///     Blob Cell Spawn Profile. The shark assets stay authored for other biomes.
-    ///
-    /// Run via Tools ▸ Cosmic Shore ▸ Build Swordfish Flagship Fauna, then run
-    /// Tools ▸ Cosmic Shore ▸ Validate Lifeform Crystals. Idempotent (rebuilds in place, keeps
-    /// human-tuned SO values). Placement is geometry-driven; open the prefab and nudge any prism —
-    /// they're parented to the bones exactly like the shark's, so hand-tuning is straightforward.
-    /// </summary>
-    public static class SwordfishFaunaSetupTool
-    {
-        // --- Source / output paths ------------------------------------------------------------
-        const string FbxPath = "Assets/_Models/Fauna/SwordFish_A.fbx";
-        const string PrefabPath = "Assets/_Models/Fauna/MassSwordfishFauna.prefab";
-        const string ControllerPath = "Assets/_Models/Fauna/SwordFish_A.controller";
-        const string FaunaDataPath = "Assets/_SO_Assets/Light Fauna Data/MassSwordfishFaunaDataSO.asset";
-        const string BlobConfigPath = "Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Swordfish Fauna Config Data.asset";
-        const string BlobSpawnProfilePath = "Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Cell Spawn Profile.asset";
-        const string BlobSharkConfigPath = "Assets/_SO_Assets/Cell Configs/Blob Cell/Blob Shark Fauna Config Data.asset";
-        const string CellDataPath = "Assets/_SO_Assets/Cell Data/Runtime Cell Data.asset";
-        const string HealthBlockPrefabPath = "Assets/_Prefabs/Trails/DynamicHealthBlock.prefab";
-        const string DangerBlockPrefabPath = "Assets/_Prefabs/Trails/DangerBlock.prefab";
-        const string CrystalPrefabPath = "Assets/_Prefabs/Environment/CrystalMass.prefab";
-        const string BodyMaterialPath = "Assets/_Graphics/Materials/SpindleMaterial.mat";
-
-        // --- Tuning ---------------------------------------------------------------------------
-        // Apex-sized creature. The model is scaled so its long axis spans TargetBodyLength; every
-        // prism is then fitted to the real local silhouette (never clamped to a guessed size).
-        const float TargetBodyLength = 34f;      // shark-class length
-        const float SlabFitFactor = 0.72f;       // fill ~0.72 of the local silhouette (leaves the mesh proud)
-        const float SlabFlatnessFraction = 0.32f;// thinnest axis capped to this fraction of the mid axis → a slab
-        const float SplitAspect = 2.6f;          // a non-bill cluster longer than this (major/mid) becomes 2 slabs
-        const float MinPrismDim = 0.6f;          // matches DynamicHealthBlock minScale
-        const int MinClusterVerts = 8;           // ignore near-empty bones
-        const float BillNeedleLength = 9f;        // target length per bill needle → count = billLen / this
-        const float BillNeedleGap = 0.85f;        // needle length = segment × this, so consecutive needles don't touch
```

</details>

### `61ac4c950` — feat(ecology): the Swordfish flagship — a drill with a sword, generated end to end

_Claude, 2026-09-01 21:15:44 +0000_

```text
The new SwordFish_A model becomes the apex of the freestyle worlds, and the
whole creature is authored by one generator (Tools/Build/author_swordfish_fauna.py,
--check in CI) from what the FILE says the animal is:

- The artist's one skinned mesh is split into eight re-centred bone-parts
  (SwordFish_A_Parts.fbx) — bill, trunk, sail, anal fin, two pectorals, two
  tail lobes — with one Spindle each, so starvation withers extremity-first
  (tail, sword, fins, then trunk around the heart) and a shot-off fin
  dissolves alone. Weights, polygons and the bind pose are conserved by
  assertion; the artist's file is untouched.
- The model is a DRILL (its trunk+bill bone rolls a full turn per second
  under fixed fins, read off the animation curves), so the twelve prisms are
  three on-axis danger needles laid end to end along the bill (a monotone
  taper anchored on the measured radius), three radial trunk flutes at 120°,
  and one blade per fin/lobe in its PCA frame — every pose from the bind
  matrices, under a bone mount scaled 1/armature, non-overlapping by a
  separating-axis check.
- SwordfishFauna : LightFauna keeps the shark's whole ecology and adds a
  vessel strike inside hunt windows (Cruise → Pursue → Telegraph → Lunge →
  Recover); the bill's danger prisms are its only damage. LightFauna grew
  four protected hooks; Fauna.FindNearestVessel is now the one vessel scan
  (the worm colony's private copy is hoisted), with an opposing-domain
  filter — a cell's fauna are its guardians.
- Four element variants with distinct bodies, hearts (author_lifeform_heart_
  sizes.py, which now measures a nested-FBX body — union of geometries,
  import-scale aware — opt-in per species) and strike profiles; a Blob
  deployment config (SpreadElements, NetworkSynced) takes the shark's apex
  slot; NetworkObject/NetworkTransform/FaunaNetworkSync + DefaultNetworkPrefabs.
- Animator (Swim/Pursue/Tuck/ChargeHold/Flare cut from the charge take's
  measured phases) driven by SwordfishChargeDriver; two FMOD slots shipped empty.
- References into the nested FBX are pinned through internalIDToNameTable;
  FrogletTools > Ecology > Swordfish Flagship validates every binding after
  import and can rebind to Unity's real ids (recorded for the generator).

Docs/ECOSYSTEM.md §42 is the record; CLAUDE.md gains the Key Systems row.
```

```text
 .../Cell Configs/Blob Cell/Blob Swordfish Fauna Config Data.asset     |   29 +
 .../Blob Cell/Blob Swordfish Fauna Config Data.asset.meta             |    8 +
 Assets/_SO_Assets/Lifeforms/Swordfish Fauna Charge.asset              |   28 +
 Assets/_SO_Assets/Lifeforms/Swordfish Fauna Charge.asset.meta         |    8 +
 Assets/_SO_Assets/Lifeforms/Swordfish Fauna Mass.asset                |   28 +
 Assets/_SO_Assets/Lifeforms/Swordfish Fauna Mass.asset.meta           |    8 +
 Assets/_SO_Assets/Lifeforms/Swordfish Fauna Space.asset               |   28 +
 Assets/_SO_Assets/Lifeforms/Swordfish Fauna Space.asset.meta          |    8 +
 Assets/_SO_Assets/Lifeforms/Swordfish Fauna Time.asset                |   28 +
 Assets/_SO_Assets/Lifeforms/Swordfish Fauna Time.asset.meta           |    8 +
 Assets/_SO_Assets/Light Fauna Data/SwordfishFaunaDataSO.asset         |   36 +
 Assets/_SO_Assets/Light Fauna Data/SwordfishFaunaDataSO.asset.meta    |    8 +
 Assets/_SO_Assets/Light Fauna Data/SwordfishStrikeData.asset          |   47 +
 Assets/_SO_Assets/Light Fauna Data/SwordfishStrikeData.asset.meta     |    8 +
 Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs         |   30 +
 Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs    |   62 +-
 .../Controller/Environment/FloraAndFauna/SwordfishChargeDriver.cs     |   85 +
 .../Environment/FloraAndFauna/SwordfishChargeDriver.cs.meta           |   11 +
 .../_Scripts/Controller/Environment/FloraAndFauna/SwordfishFauna.cs   |  233 +++
 .../Controller/Environment/FloraAndFauna/SwordfishFauna.cs.meta       |   11 +
 .../Controller/Environment/FloraAndFauna/SwordfishStrikeDataSO.cs     |   86 +
 .../Environment/FloraAndFauna/SwordfishStrikeDataSO.cs.meta           |   11 +
 Assets/_Scripts/Controller/Environment/FloraAndFauna/WormFauna.cs     |   24 -
 Assets/_Scripts/Editor/SwordfishFlagshipTool.cs                       |  293 ++++
 Assets/_Scripts/Editor/SwordfishFlagshipTool.cs.meta                  |   11 +
 CLAUDE.md                                                             |    1 +
 Docs/ECOSYSTEM.md                                                     |  217 +++
 Tools/Build/author_lifeform_heart_sizes.py                            |  129 +-
 Tools/Build/author_swordfish_fauna.py                                 | 1460 ++++++++++++++++
 37 files changed, 6617 insertions(+), 33 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 2781 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs
index 238dea533..0142d6658 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/Fauna.cs
@@ -649,6 +649,36 @@ namespace CosmicShore.Gameplay
                 ? s_nonPrismOverlapMask
                 : s_nonPrismOverlapMask = ~LayerMask.GetMask("TrailBlocks", "Mound");
 
+        /// <summary>
+        /// Nearest PILOT within <paramref name="radius"/> of <paramref name="origin"/>, via the
+        /// shared physics scratch masked to non-prism layers — the sanctioned vessel-sensing
+        /// path (prisms never go through physics). Shared by every creature that hunts vessels
+        /// (the worm colony, the swordfish) so there is one copy of the rule.
+        /// <paramref name="opposingDomainsOnly"/> skips pilots wearing this creature's own
+        /// colour — a cell's fauna spawn in its controlling colour and are its guardians, not a
+        /// hazard to the pilots of that colour. Null when nothing is in range.
+        /// </summary>
+        protected Transform FindNearestVessel(Vector3 origin, float radius, bool opposingDomainsOnly = false)
+        {
+            if (radius <= 0f) return null;
+            int hits = Physics.OverlapSphereNonAlloc(origin, radius, OverlapScratch, NonPrismOverlapMask);
+            Transform best = null;
+            float bestSqr = float.PositiveInfinity;
+            for (int i = 0; i < hits; i++)
+            {
+                var collider = OverlapScratch[i];
+                if (!collider || !collider.TryGetComponent(out IVesselStatus vessel)) continue;
+                if (opposingDomainsOnly && vessel.Domain == domain) continue;
+                float sqr = (collider.transform.position - origin).sqrMagnitude;
+                if (sqr < bestSqr)
+                {
+                    bestSqr = sqr;
+                    best = collider.transform;
+                }
+            }
+            return best;
+        }
+
         // --- Body prisms (the movers contract with PrismSpatialIndex) -------
         // Fauna bodies are HealthPrisms - registered prism mass that MOVES every
         // frame. The index stores positions, so the mover must keep them honest
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
index ecbea4540..21ad6c95e 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs
@@ -63,6 +63,49 @@ namespace CosmicShore.Gameplay
         public bool IsActivelyHunting =>
             diet == FaunaDiet.Predator && !_withering && data && IsHuntWindow;
 
+        // ── Subclass hooks ───────────────────────────────────────────────────
+        // A species with a behaviour the base does not have (the swordfish's vessel strike)
+        // layers it on top of the ordinary predator instead of forking the class: it reads
+        // the same data, rides the same behavior tick, and may take the BODY for a frame.
+        // Nothing here changes what a plain LightFauna does.
+
+        /// <summary>The species data - read-only to subclasses; tuning stays on the asset.</summary>
+        protected LightFaunaDataSO Data => data;
+
+        /// <summary>True once death has begun the wither; movement and behaviour are frozen.</summary>
+        protected bool IsWithering => _withering;
+
+        /// <summary>The integrated velocity. A subclass that owns steering writes it directly.</summary>
+        protected Vector3 CurrentVelocity
+        {
+            get => currentVelocity;
+            set => currentVelocity = value;
+        }
+
+        /// <summary>The facing the per-frame rotation lerp chases.</summary>
+        protected Quaternion DesiredRotation
+        {
+            get => desiredRotation;
+            set => desiredRotation = value;
+        }
+
+        /// <summary>
+        /// True while a subclass owns the body: the behavior tick then skips its own velocity
+        /// and facing write (starvation, goal resolution and prey selection still run).
+        /// </summary>
+        protected virtual bool SubclassOwnsSteering => false;
```

</details>

_Also contains 3 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
