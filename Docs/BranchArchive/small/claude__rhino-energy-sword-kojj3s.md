# Branch archive: `claude/rhino-energy-sword-kojj3s`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-07-23 by Claude
- **Unmerged commits:** 2
- **Forked from:** `98c36ba78` (2026-07-23, Update MinigameJoust_Gameplay.unity)
- **Tip:** `61499370d`
- **Files touched (23):**
  - `Assets/_SO_Assets/Effects/Effect Containers/SkimmerContainers/RhinoForceFieldSkimmerImpactorDataContainer.asset`
  - `Assets/_SO_Assets/Effects/Skimmer Crystal Effects/RhinoSwordCrystalBurstEffect.asset`
  - `Assets/_SO_Assets/Effects/Skimmer Crystal Effects/RhinoSwordCrystalBurstEffect.asset.meta`
  - `Assets/_SO_Assets/Effects/Vessel Prism Effects/RhinoSkimmerDamagePrismEffect.asset`
  - `Assets/_SO_Assets/VesselActions/Rhino/RhinoShieldSwipeConfig.asset`
  - `Assets/_SO_Assets/VesselActions/Rhino/ShieldSkimmerScaleConfig.asset`
  - `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/ExplosionHelper.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/RhinoSkimmerDamagePrismEffectSO.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/RhinoSwordCrystalBurstEffectSO.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/RhinoSwordCrystalBurstEffectSO.cs.meta`
  - `Assets/_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Data Containers/RhinoShieldSwipeConfigSO.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/IRhinoSwordState.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/IRhinoSwordState.cs.meta`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordVisualizer.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordVisualizer.cs.meta`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ShieldSkimmerScaleConfigSO.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ShieldSkimmerScaleDriver.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ShieldSwipeActionExecutor.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md.meta`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/RHINO_SHIELD_SWIPE.md`
  - `Assets/_Scripts/Controller/Vessel/Skimmer.cs`

### `a3450303b` — feat(rhino): energy sword — prism-fueled energize stance, crystal 3D burst, slash cooldown

_Claude, 2026-07-23 02:02:50 +0000_

```text
The Rhino's sword now banks "energy" (the Shield resource) by destroying prisms and
spends it two ways:

- Crystal hit: bursts the blade in all three dimensions and spawns an explosion, both
  scaled by the energy consumed (full energy = authored max scale + max explosion; less
  scales down), then drains all energy.
- Energize: holding the lower/chop stance >1s costs 1/10 of max energy and lights the
  blade white with recolouring edge tracers, pops super-shielded prisms, and drops the
  slash cooldown to 0. Stays energized 5s after leaving the stance, then a 5s lockout
  (10s total). Otherwise slashes are gated to one pop per second.

Routed through IRhinoSwordState on the existing ShieldSkimmerScaleDriver (registered on
Skimmer.SwordState) so shared effect SOs can read per-vessel state — code + flat asset
edits only, no new prefab components. Blade visuals use a MaterialPropertyBlock (never
the shared FresnelMaterial) and runtime TrailRenderer tracers. Super-shield popping uses
the sanctioned DeactivateShields+devastate teardown; a slash rising-edge re-applies prism
effects to already-overlapping prisms so it can't miss. See RHINO_ENERGY_SWORD.md.
```

```text
 .../RhinoForceFieldSkimmerImpactorDataContainer.asset                 |   5 +-
 .../Skimmer Crystal Effects/RhinoSwordCrystalBurstEffect.asset        |  19 ++
 .../Skimmer Crystal Effects/RhinoSwordCrystalBurstEffect.asset.meta   |   8 +
 .../Effects/Vessel Prism Effects/RhinoSkimmerDamagePrismEffect.asset  |   1 +
 Assets/_SO_Assets/VesselActions/Rhino/RhinoShieldSwipeConfig.asset    |   3 +
 Assets/_SO_Assets/VesselActions/Rhino/ShieldSkimmerScaleConfig.asset  |  15 ++
 .../Controller/ImpactEffects/EffectsSO/Helpers/ExplosionHelper.cs     |  13 ++
 .../Skimmer Prism Effects/RhinoSkimmerDamagePrismEffectSO.cs          |  59 ++++-
 .../EffectsSO/Skimmer Prism Effects/RhinoSwordCrystalBurstEffectSO.cs |  60 +++++
 .../Skimmer Prism Effects/RhinoSwordCrystalBurstEffectSO.cs.meta      |  11 +
 Assets/_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs |  40 ++++
 .../R_VesselActions/Data Containers/RhinoShieldSwipeConfigSO.cs       |  15 ++
 .../Controller/Vessel/R_VesselActions/Executors/IRhinoSwordState.cs   |  48 ++++
 .../Vessel/R_VesselActions/Executors/IRhinoSwordState.cs.meta         |  11 +
 .../Vessel/R_VesselActions/Executors/RhinoSwordVisualizer.cs          | 211 ++++++++++++++++++
 .../Vessel/R_VesselActions/Executors/RhinoSwordVisualizer.cs.meta     |  11 +
 .../Vessel/R_VesselActions/Executors/ShieldSkimmerScaleConfigSO.cs    |  79 +++++++
 .../Vessel/R_VesselActions/Executors/ShieldSkimmerScaleDriver.cs      | 376 ++++++++++++++++++--------------
 .../Vessel/R_VesselActions/Executors/ShieldSwipeActionExecutor.cs     |  39 ++++
 .../_Scripts/Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md  | 151 +++++++++++++
 .../Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md.meta      |   7 +
 .../_Scripts/Controller/Vessel/R_VesselActions/RHINO_SHIELD_SWIPE.md  |   6 +
 Assets/_Scripts/Controller/Vessel/Skimmer.cs                          |   9 +
 23 files changed, 1030 insertions(+), 167 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 1196 lines)</summary>

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
index 3c66afcd9..431aeddea 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs
@@ -33,6 +33,13 @@ namespace CosmicShore.Gameplay
 
         // runtime state (moved from Skimmer)
         readonly Dictionary<string, float> _skimStartTimes = new();
+
+        // Prisms currently inside this skimmer's trigger. Maintained on enter/exit so the Rhino
+        // energy sword can, on a slash rising-edge, re-run its prism effects against a prism that
+        // was already overlapping the blade before the slash began (no fresh OnTriggerEnter fires
+        // for it). Generic + cheap; only the Rhino path calls ReapplyPrismEffectsToOverlapping.
+        readonly HashSet<PrismImpactor> _overlappingPrisms = new();
+        readonly List<PrismImpactor> _reapplyBuffer = new();
         //private int ActivelySkimmingBlockCount;
         //[HideInInspector]
         public float CombinedWeight; // exposed for effects that need it
@@ -138,6 +145,9 @@ namespace CosmicShore.Gameplay
 
             if (!other.TryGetComponent<PrismImpactor>(out var prismImpactor)) return;
             var prism = prismImpactor.Prism;
+
+            _overlappingPrisms.Remove(prismImpactor);
+
             if (!skimmer.AffectSelf && prism.Domain == skimmer.VesselStatus.Domain) return;
 
             if (!_skimStartTimes.Remove(prism.ownerID)) return;
@@ -176,6 +186,7 @@ namespace CosmicShore.Gameplay
                 case PrismImpactor prismImpactor:
                     var prism = prismImpactor.Prism;
                     var esp = skimmerImpactorDataContainer.SkimmerPrismEffects;
+                    _overlappingPrisms.Add(prismImpactor);
                     skimmer.ExecuteImpactOnPrism(prism); // secondary call (booster viz, etc.)
                     if (!DoesEffectExist(esp)) return;
 
@@ -224,5 +235,34 @@ namespace CosmicShore.Gameplay
             _skimStartTimes.Add(ownerId, Time.time);
             //ActivelySkimmingBlockCount++;
         }
+
+        /// <summary>
+        /// Re-runs this skimmer's prism effects against every prism currently inside the trigger.
+        /// The Rhino energy sword calls this on a slash rising-edge so a slash pops a prism that
+        /// was already overlapping the blade before the slash began (no new OnTriggerEnter fires
+        /// for a prism that never left the trigger). The effects themselves gate on the sword's
+        /// slash state, so this is a no-op when the sword is not slashing. Stale entries (prisms
+        /// pooled/destroyed without an OnTriggerExit) are pruned as they are encountered.
+        /// </summary>
+        public void ReapplyPrismEffectsToOverlapping()
+        {
+            if (!isInitialized || _overlappingPrisms.Count == 0) return;
+            var esp = skimmerImpactorDataContainer.SkimmerPrismEffects;
+            if (!DoesEffectExist(esp)) return;
+
+            _reapplyBuffer.Clear();
+            _reapplyBuffer.AddRange(_overlappingPrisms);
+            for (int i = 0; i < _reapplyBuffer.Count; i++)
+            {
+                var prismImpactor = _reapplyBuffer[i];
+                if (!prismImpactor || prismImpactor.Prism == null)
+                {
+                    _overlappingPrisms.Remove(prismImpactor);
+                    continue;
+                }
+                foreach (var effect in esp)
+                    effect.Execute(this, prismImpactor);
+            }
+        }
     }
 }
\ No newline at end of file
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/IRhinoSwordState.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/IRhinoSwordState.cs
new file mode 100644
index 000000000..1844c5e88
--- /dev/null
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/IRhinoSwordState.cs
@@ -0,0 +1,48 @@
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// Per-vessel runtime state for the Rhino "energy sword". Implemented by
+    /// <see cref="ShieldSkimmerScaleDriver"/> (the sword's brain, one per Rhino) and exposed
+    /// through <see cref="Skimmer.SwordState"/> so the shared impact-effect ScriptableObjects
+    /// — which are singletons and cannot hold per-vessel state — can read and drive it via
+    /// <c>impactor.Skimmer.SwordState</c>. Null on every non-Rhino skimmer.
+    ///
+    /// "Energy" is the Rhino's Shield resource (normalized 0..1). It is gained when a slash
+    /// destroys a prism and spent two ways: a crystal hit (a 3D size burst + explosion scaled
+    /// by energy, draining all of it) and energizing the blade (holding the lower/chop stance,
+    /// costing 1/10 of max energy). See <c>RHINO_ENERGY_SWORD.md</c>.
+    /// </summary>
+    public interface IRhinoSwordState
+    {
+        /// <summary>True while the blade is energized — white, edge tracers lit, pops
+        /// super-shielded prisms, and the slash cooldown is 0 (frenzy).</summary>
+        bool IsEnergized { get; }
+
+        /// <summary>Current stored energy, normalized 0..1 (the Shield resource).</summary>
+        float Energy01 { get; }
+
+        /// <summary>True when a slash may deal damage this frame: a single trigger is being
+        /// pulled AND (the blade is energized OR the slash cooldown has elapsed).</summary>
+        bool CanSlashDamage { get; }
+
+        /// <summary>Called the instant a slash lands damage on a prism — starts the slash
+        /// cooldown (0 while energized, the configured cooldown otherwise).</summary>
+        void NotifySlashLanded();
+
+        /// <summary>Energy gained from destroying a prism, in normalized 0..1 units.</summary>
+        void AddEnergy(float amount01);
+
+        /// <summary>Crystal hit: burst the blade in all three dimensions scaled by the current
+        /// energy, then consume ALL of it (drain to 0). The magnitude at full energy reproduces
+        /// the authored max scale; less energy scales the burst down proportionally.</summary>
+        void TriggerCrystalBurst();
+
+        /// <summary>Fed each frame by the swipe executor: true while the both-triggers
+        /// "lower"/chop stance (the energize gesture) is held.</summary>
+        void SetInStance(bool inStance);
+
+        /// <summary>Fed each frame by the swipe executor: true while a single-trigger swipe
+        /// (a slash) is being pulled.</summary>
+        void SetSlashing(bool slashing);
+    }
+}
```

</details>

### `61499370d` — fix(rhino): keep sword MaxScale >= BaseScale to avoid resting-clamp inversion

_Claude, 2026-07-23 02:06:29 +0000_

```text
BaseScale is the skimmer's live elemental scale, MaxScale is from config; guard so an
unusually large elemental scale can't make the resting-length clamp run min > max.
```

```text
 Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ShieldSkimmerScaleDriver.cs | 4 +++-
 1 file changed, 3 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ShieldSkimmerScaleDriver.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ShieldSkimmerScaleDriver.cs
index 441f89afd..798bcf9e3 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ShieldSkimmerScaleDriver.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ShieldSkimmerScaleDriver.cs
@@ -71,7 +71,9 @@ namespace CosmicShore.Gameplay
         // Resting size is the skimmer's live elemental (Space-driven) scale — element levels
         // lengthen the sword and energy growth composes on top. Falls back to the config value.
         float BaseScale => _skimmer ? Mathf.Max(0.01f, _skimmer.LiveElementalScale) : config.BaseScale;
-        float MaxScale  => config.MaxScale;
+        // Keep max ≥ base: the two come from different sources (base = skimmer's live elemental
+        // scale, max = config), so a large elemental scale must never invert the resting clamp.
+        float MaxScale  => Mathf.Max(config.MaxScale, BaseScale);
         float Range     => Mathf.Max(0.0001f, MaxScale - BaseScale);
 
         public float MinScale => BaseScale;
```

</details>
