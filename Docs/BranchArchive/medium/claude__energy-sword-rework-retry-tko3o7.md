# Branch archive: `claude/energy-sword-rework-retry-tko3o7`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-08-12 by Claude
- **Unmerged commits:** 4
- **Forked from:** `814611529` (2026-08-12, Merge remote-tracking branch 'origin/claude/dog-fight-game-mode-it9xgy' into b)
- **Tip:** `bfefb8790`
- **Files touched (22):**
  - `Assets/_Graphics/Materials/Graphs/FresnelGraph.shadergraph`
  - `Assets/_SO_Assets/Effects/Effect Containers/SkimmerContainers/RhinoForceFieldSkimmerImpactorDataContainer.asset`
  - `Assets/_SO_Assets/Effects/Skimmer Crystal Effects/RhinoSwordCrystalBurstEffect.asset`
  - `Assets/_SO_Assets/Effects/Skimmer Crystal Effects/RhinoSwordCrystalBurstEffect.asset.meta`
  - `Assets/_SO_Assets/Effects/Vessel Prism Effects/RhinoSkimmerDamagePrismEffect.asset`
  - `Assets/_SO_Assets/VesselActions/Rhino/ShieldSkimmerScaleConfig.asset`
  - `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/ExplosionHelper.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/RhinoSkimmerDamagePrismEffectSO.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/RhinoSwordCrystalBurstEffectSO.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/RhinoSwordCrystalBurstEffectSO.cs.meta`
  - `Assets/_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs`
  - `Assets/_Scripts/Controller/Vessel/Prism.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/IRhinoSwordState.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/IRhinoSwordState.cs.meta`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordVisualizer.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordVisualizer.cs.meta`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ShieldSkimmerScaleConfigSO.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ShieldSkimmerScaleDriver.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md.meta`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/RHINO_SHIELD_SWIPE.md`
  - `Assets/_Scripts/Controller/Vessel/Skimmer.cs`

### `d6e890520` — feat(rhino): ungated energy sword — super-shield popping on contact, energy meter, crystal burst, blade heat FX

_Claude, 2026-07-28 00:04:14 +0000_

```text
Retry of the energy-sword feature (supersedes claude/rhino-energy-sword-kojj3s,
which gated all damage behind an energize stance + slash cooldown — rejected).
The sword is now UNGATED: it always damages prisms on contact and destroys
super-shielded prisms as a base capability via the sanctioned mass-conserving
teardown (DeactivateShields then Damage devastate:true — the
AstroLeagueArena.ClearEdgeLining precedent). Legacy bounce kept behind a
destroySuperShielded config toggle.

Energy (Shield resource idx 1, no passive decay) is pure reward: banked per
prism destroyed (0.04, super-shielded 0.12), read out as blade resting length
(same HUD meter) and a teal->white heat ramp; an elemental-crystal hit spends
it all on a 3D blade burst + AOE explosion + camera shake, all scaled by the
energy consumed. Burst only ever grows the blade (floored at current length,
capped at the debuff-aware MaxScale) and the HUD reports the honest energy
length during it.

FX: fixed FresnelGraph.shadergraph — its _Color property fed a Blend node
whose output connected to nothing, so every _Color write was invisible; the
graph now multiplies _Color into the animated Voronoi (only consumer is the
Rhino sword). Blade drives _Color via MaterialPropertyBlock (shared material
untouched): heat ramp by energy, decaying impact flash per kill (full flash +
local-pilot camera shake on super-shield pops), tip tracers from an authored
material instanced per sword.

SkimmerImpactor now gates skimmer crystal effects on collectable crystals
(IsEmbedded/IsExploding), mirroring ElementalCrystalImpactor's guards, so
living lifeforms' embedded hearts can't trigger burst/explosion spam.

Docs: RHINO_ENERGY_SWORD.md rewritten for the ungated design;
RHINO_SHIELD_SWIPE.md scale-ownership section updated; stale Prism.Damage
super-shield comment corrected.
```

```text
 Assets/_Graphics/Materials/Graphs/FresnelGraph.shadergraph            |  10 +-
 .../RhinoForceFieldSkimmerImpactorDataContainer.asset                 |   5 +-
 .../Skimmer Crystal Effects/RhinoSwordCrystalBurstEffect.asset        |  19 ++
 .../Skimmer Crystal Effects/RhinoSwordCrystalBurstEffect.asset.meta   |   8 +
 .../Effects/Vessel Prism Effects/RhinoSkimmerDamagePrismEffect.asset  |   3 +
 Assets/_SO_Assets/VesselActions/Rhino/ShieldSkimmerScaleConfig.asset  |  25 ++-
 .../Controller/ImpactEffects/EffectsSO/Helpers/ExplosionHelper.cs     |  13 ++
 .../Skimmer Prism Effects/RhinoSkimmerDamagePrismEffectSO.cs          |  82 ++++++--
 .../EffectsSO/Skimmer Prism Effects/RhinoSwordCrystalBurstEffectSO.cs |  60 ++++++
 .../Skimmer Prism Effects/RhinoSwordCrystalBurstEffectSO.cs.meta      |  11 +
 Assets/_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs |   8 +
 Assets/_Scripts/Controller/Vessel/Prism.cs                            |   8 +-
 .../Controller/Vessel/R_VesselActions/Executors/IRhinoSwordState.cs   |  33 +++
 .../Vessel/R_VesselActions/Executors/IRhinoSwordState.cs.meta         |  11 +
 .../Vessel/R_VesselActions/Executors/RhinoSwordVisualizer.cs          | 242 ++++++++++++++++++++++
 .../Vessel/R_VesselActions/Executors/RhinoSwordVisualizer.cs.meta     |  11 +
 .../Vessel/R_VesselActions/Executors/ShieldSkimmerScaleConfigSO.cs    | 129 +++++++++---
 .../Vessel/R_VesselActions/Executors/ShieldSkimmerScaleDriver.cs      | 343 ++++++++++++++++----------------
 .../_Scripts/Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md  | 182 +++++++++++++++++
 .../Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md.meta      |   7 +
 .../_Scripts/Controller/Vessel/R_VesselActions/RHINO_SHIELD_SWIPE.md  |  15 +-
 Assets/_Scripts/Controller/Vessel/Skimmer.cs                          |   9 +
 22 files changed, 997 insertions(+), 237 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 1178 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/ExplosionHelper.cs b/Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/ExplosionHelper.cs
index fb4fefe00..53ff0a6dd 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/ExplosionHelper.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/ExplosionHelper.cs
@@ -64,6 +64,19 @@ namespace CosmicShore.Gameplay
             SpawnAllAndDetonate(aoePrefabs, init, impactor.DIContainer);
         }
 
+        /// <summary>
+        /// Spawn + detonate a pre-built explosion at an arbitrary world position/scale. Used when
+        /// the caller isn't a vessel or projectile (e.g. the Rhino energy sword exploding a crystal
+        /// at the crystal's location, scaled by the energy consumed).
+        /// </summary>
+        public static void CreateExplosion(
+            AOEExplosion[] aoePrefabs,
+            AOEExplosion.InitializeStruct init,
+            Container container)
+        {
+            SpawnAllAndDetonate(aoePrefabs, init, container);
+        }
+
         // ---------- Internals ----------
 
         static void SpawnAllAndDetonate(IEnumerable<AOEExplosion> prefabs, AOEExplosion.InitializeStruct init, Container container)
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs
index 3c66afcd9..5baaf5e16 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs
@@ -192,6 +192,14 @@ namespace CosmicShore.Gameplay
                     break;
 
                 case ElementalCrystalImpactor elementalCrystalImpactor:
+                    // Mirror the crystal side's collectability guards (ElementalCrystalImpactor.
+                    // AcceptImpactee): a living lifeform's embedded heart enters the trigger but is
+                    // never skim-collectable — without this gate the skimmer's crystal effects
+                    // (e.g. the Rhino sword's crystal burst) would fire on it, repeatedly, since
+                    // the heart's collider never gets disabled by a collection.
+                    var crystal = elementalCrystalImpactor.Crystal;
+                    if (crystal == null || crystal.IsEmbedded || crystal.IsExploding) return;
+
                     var esc = skimmerImpactorDataContainer.SkimmerCrystalEffects;
                     if (!DoesEffectExist(esc)) return;
                     foreach (var effect in esc)
diff --git a/Assets/_Scripts/Controller/Vessel/Prism.cs b/Assets/_Scripts/Controller/Vessel/Prism.cs
index 53f912e49..c36145bba 100644
--- a/Assets/_Scripts/Controller/Vessel/Prism.cs
+++ b/Assets/_Scripts/Controller/Vessel/Prism.cs
@@ -865,10 +865,12 @@ namespace CosmicShore.Gameplay
         public void Damage(Vector3 impactVector, Domains domain, string playerName, bool devastate = false, bool byCreature = false)
         {
             if (destroyed) return;
-            // Super-shielded prisms are fully invulnerable. No damage source
-            // currently breaks them; ways to break them will be added later.
+            // Super-shielded prisms are invulnerable to Damage itself. A source that may
+            // break them (the Rhino energy sword, arena teardowns) must call
+            // DeactivateShields() first, then Damage(devastate: true) — the sanctioned
+            // animated sequence (see AstroLeagueArena.ClearEdgeLining / RHINO_ENERGY_SWORD.md).
             // The impactor's other effect SOs (sparks, sound) still fire on
-            // OnTriggerEnter, so the hit reads visually without state change.
+            // OnTriggerEnter, so an unbreaking hit reads visually without state change.
             if (prismProperties.IsSuperShielded) return;
             if (prismProperties.IsShielded && !devastate)
                 DeactivateShields();
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/IRhinoSwordState.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/IRhinoSwordState.cs
new file mode 100644
index 000000000..ec761d47c
--- /dev/null
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/IRhinoSwordState.cs
@@ -0,0 +1,33 @@
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// Per-vessel runtime state for the Rhino "energy sword". Implemented by
+    /// <see cref="ShieldSkimmerScaleDriver"/> (the sword's brain, one per Rhino) and exposed
+    /// through <see cref="Skimmer.SwordState"/> so the shared impact-effect ScriptableObjects
+    /// — which are singletons and cannot hold per-vessel state — can read and drive it via
+    /// <c>impactor.Skimmer.SwordState</c>. Null on every non-Rhino skimmer.
+    ///
+    /// The sword has NO damage gate: it always damages prisms on contact and always pops
+    /// super-shielded prisms. "Energy" is the Rhino's Shield resource (normalized 0..1),
+    /// banked per prism the sword destroys — it lengthens and heats the blade — and spent
+    /// all at once when the sword collects an elemental crystal (a 3D burst + explosion
+    /// scaled by the energy consumed). See <c>RHINO_ENERGY_SWORD.md</c>.
+    /// </summary>
+    public interface IRhinoSwordState
+    {
+        /// <summary>Current stored energy, normalized 0..1 (the Shield resource).</summary>
+        float Energy01 { get; }
+
+        /// <summary>Energy gained from destroying a prism, in normalized 0..1 units.</summary>
+        void AddEnergy(float amount01);
+
+        /// <summary>Called the instant the sword destroys a prism, for impact feedback:
+        /// a blade flash pulse, plus a local camera shake when a super-shield popped.</summary>
+        void NotifyPrismDestroyed(bool superShielded);
+
+        /// <summary>Elemental-crystal hit: burst the blade in all three dimensions scaled by
+        /// the current energy, then consume ALL of it (drain to 0). Full energy reproduces the
+        /// authored max scale; less energy scales the burst down proportionally.</summary>
+        void TriggerCrystalBurst();
+    }
+}
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordVisualizer.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordVisualizer.cs
new file mode 100644
index 000000000..4515614a8
--- /dev/null
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordVisualizer.cs
@@ -0,0 +1,242 @@
+using System.Collections.Generic;
+using UnityEngine;
+using UnityEngine.Rendering;
+
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// Drives the Rhino energy sword's LOOK, owned and ticked by <see cref="ShieldSkimmerScaleDriver"/>.
+    /// Three layers, all configured through <see cref="ShieldSkimmerScaleConfigSO"/>:
+    ///
+    ///  1. HEAT RAMP — the blade blends from its authored teal toward the full-energy colour and
+    ///     brightens as stored energy fills, so the sword's power is readable at a glance. The blade
+    ///     uses the SHARED FresnelMaterial, so this never touches <c>renderer.material</c> — it
+    ///     drives <c>_Color</c> through a MaterialPropertyBlock (per-renderer, no material leak;
+    ///     RGB above 1 feeds gameplay bloom — the AstroLeagueBall impact-flash precedent).
+    ///  2. IMPACT FLASH — a decaying white-out pulse on every prism the sword destroys (small for a
+    ///     normal pop, full for a super-shield pop or crystal burst), layered over the heat ramp.
+    ///  3. TIP TRACERS — two motion streaks seated at the blade tips (parented to the fuselage so
+    ///     their width doesn't scale with the growing blade), tinted with the live blade colour.
+    ///     The tracer material comes from the config (instanced per sword so tinting never mutates
+    ///     the shared asset); a runtime unlit fallback covers a missing reference.
+    ///
+    /// A plain class (not a MonoBehaviour) so no extra prefab component is needed; the driver
+    /// creates it, calls <see cref="Setup"/> once, <see cref="Tick"/> each frame, and
+    /// <see cref="Teardown"/> on disable. See <c>RHINO_ENERGY_SWORD.md</c>.
+    /// </summary>
+    public sealed class RhinoSwordVisualizer
+    {
+        static readonly int ColorId     = Shader.PropertyToID("_Color");
+        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
+        static readonly int Color0Id    = Shader.PropertyToID("_Color0");
+        static readonly int Color1Id    = Shader.PropertyToID("_Color1");
+
+        Transform _skimmerRoot;
+        ShieldSkimmerScaleConfigSO _config;
+
+        MeshRenderer _bodyRenderer;
+        MaterialPropertyBlock _mpb;
+        bool _hasColorProp;
+        Color _appliedBodyColor;
+        bool _bodyColorApplied;
+
```

</details>

_Also contains 3 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
