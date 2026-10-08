# Branch archive: `claude/documentation-audit-p5ov1g`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

<!-- SUMMARY -->

- **Last commit:** 2026-08-15 by Claude
- **Unmerged commits:** 106
- **Forked from:** `d32f26839` (2026-08-12, Merge pull request #710 from froglet-studio/claude/charge-crystal-shader-9u2ik)
- **Tip:** `cb638c9d7`
- **Files touched (277):**
  - `.claude/skills/asset-surgery/SKILL.md`
  - `.claude/skills/ecology/SKILL.md`
  - `.claude/skills/ship/SKILL.md`
  - `.claude/skills/vessel/SKILL.md`
  - `.claude/skills/vessel/references/CONTRACT.md`
  - `Assets/KeyboardInputStrategy.cs`
  - `Assets/KeyboardInputStrategy.cs.meta`
  - `Assets/Resources/CrystalCaptureConfig.asset`
  - `Assets/Resources/CrystalCaptureConfig.asset.meta`
  - `Assets/Resources/ElementalAbilityMaps/Dolphin.asset`
  - `Assets/Resources/ElementalAbilityMaps/Sparrow.asset`
  - `Assets/Resources/EndConditionOverrides.asset`
  - `Assets/Resources/PrismSuperShieldJiggleConfig.asset`
  - `Assets/Resources/PrismSuperShieldJiggleConfig.asset.meta`
  - `Assets/_Graphics/Materials/BlockMaterials/BlueBlockMateral.mat`
  - `Assets/_Graphics/Materials/BlockMaterials/ExplodingBlockMaterial.mat`
  - `Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph`
  - `Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph`
  - `Assets/_Graphics/Materials/Graphs/ForcefieldCrackle.hlsl`
  - `Assets/_Graphics/Materials/Graphs/ForcefieldCrackleCapsule.shader`
  - `Assets/_Graphics/Materials/Graphs/ForcefieldCrackleCapsule.shader.meta`
  - `Assets/_Graphics/Materials/Graphs/FresnelGraph.shadergraph`
  - `Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl`
  - `Assets/_Graphics/Materials/Graphs/PrismDestructionSight.hlsl`
  - `Assets/_Graphics/Materials/Graphs/PrismDestructionSight.hlsl.meta`
  - `Assets/_Graphics/Materials/RhinoBladeCrackleMaterial.mat`
  - `Assets/_Graphics/Materials/RhinoBladeCrackleMaterial.mat.meta`
  - `Assets/_Graphics/Materials/RhinoSwordTracerMaterial.mat`
  - `Assets/_Graphics/Materials/RhinoSwordTracerMaterial.mat.meta`
  - `Assets/_Models/Fauna/MassBrittlestarFauna.prefab`
  - `Assets/_Models/Fauna/MassSharkFauna.prefab`
  - `Assets/_Prefabs/FloraAndFauna/ArborFlora.prefab`
  - `Assets/_Prefabs/FloraAndFauna/BranchingFlora.prefab`
  - `Assets/_Prefabs/FloraAndFauna/CactiFlora.prefab`
  - `Assets/_Prefabs/FloraAndFauna/CoralFlora.prefab`
  - `Assets/_Prefabs/FloraAndFauna/FrondFlora.prefab`
  - `Assets/_Prefabs/FloraAndFauna/GyroidFlora.prefab`
  - `Assets/_Prefabs/FloraAndFauna/LanternFlora.prefab`
  - `Assets/_Prefabs/FloraAndFauna/ReedFlora.prefab`
  - `Assets/_Prefabs/FloraAndFauna/RosetteFlora.prefab`
  - … and 237 more

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

<details><summary>Patch (code/doc/text files, first 80 of 1178 lines)</summary>

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
```

</details>

### `df5b526a1` — feat(rhino): energize ritual as the supershield key + authored blade FX pass

_Claude, 2026-08-12 04:30:30 +0000_

```text
The sword's ordinary cutting stays ungated (normal prisms explode, shielded
prisms lose their shield on contact). The ONE gated act is popping a
SUPER-shielded prism: hold the both-triggers lower/chop stance for 1s to
ENERGIZE the blade (costs 0.1 energy); it stays lit 5s after leaving the
stance, then cools for 5s. A non-energized blade bounces off with a dim
denied-spark. On the ignition edge the standing blade contacts re-dispatch
through both tiers (SkimmerImpactor box overlaps + a new
PrismShellContactManager.RedispatchPairsForOwner for shell-owned pairs), so
a super-shielded prism already resting against the blade pops immediately.

FX ownership moves off the driver onto the prefab-authored
RhinoSwordFXController (RhinoSwordVisualizer deleted): heat ramp teal->cyan
by energy, white-hot energized blend, ignition crackle burst along the whole
blade, contact sparks at the exact blade point per kill, denied sparks,
authored tip tracers (TrailRenderers + RhinoSwordTracerMaterial, no
Shader.Find fallback), local-pilot camera shake. The forcefield crackle is
adapted to the blade: ForcefieldCrackleCapsule shader + HLSL entry measure
ripples in world units on the stretched capsule (impacts stored as
object-space positions), driven through the existing
ForcefieldCrackleController in a new Capsule surface mode.

Verified: mcs compile (2 stub passes), clang compile+run of the shipped
HLSL, field-parity on all hand-authored YAML, prefab fileID resolution.
```

```text
 Assets/_Graphics/Materials/Graphs/ForcefieldCrackle.hlsl              | 167 ++++++++++++++++
 Assets/_Graphics/Materials/Graphs/ForcefieldCrackleCapsule.shader     | 119 +++++++++++
 .../_Graphics/Materials/Graphs/ForcefieldCrackleCapsule.shader.meta   |  10 +
 Assets/_Graphics/Materials/RhinoBladeCrackleMaterial.mat              |  43 ++++
 Assets/_Graphics/Materials/RhinoBladeCrackleMaterial.mat.meta         |   8 +
 Assets/_Graphics/Materials/RhinoSwordTracerMaterial.mat               | 129 ++++++++++++
 Assets/_Graphics/Materials/RhinoSwordTracerMaterial.mat.meta          |   8 +
 .../_Prefabs/Spacevessels/Components/ForceFieldSkimmer Variant.prefab |  11 +
 Assets/_Prefabs/Spacevessels/Rhino.prefab                             | 331 +++++++++++++++++++++++++++++++
 .../Effects/Vessel Prism Effects/RhinoSkimmerDamagePrismEffect.asset  |   2 +-
 Assets/_SO_Assets/VesselActions/Rhino/RhinoShieldSwipeConfig.asset    |   2 +
 Assets/_SO_Assets/VesselActions/Rhino/ShieldSkimmerScaleConfig.asset  |  21 +-
 .../Skimmer Prism Effects/RhinoSkimmerDamagePrismEffectSO.cs          |  38 ++--
 Assets/_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs |  53 +++++
 Assets/_Scripts/Controller/Managers/PrismShellContactManager.cs       |  36 ++++
 Assets/_Scripts/Controller/Vessel/ForcefieldCrackleController.cs      |  45 ++++-
 .../R_VesselActions/Data Containers/RhinoShieldSwipeConfigSO.cs       |  11 +
 .../Controller/Vessel/R_VesselActions/Executors/IRhinoSwordState.cs   |  48 ++++-
 .../Vessel/R_VesselActions/Executors/RhinoSwordFXController.cs        | 342 ++++++++++++++++++++++++++++++++
 .../{RhinoSwordVisualizer.cs.meta => RhinoSwordFXController.cs.meta}  |   8 +-
 .../Vessel/R_VesselActions/Executors/RhinoSwordVisualizer.cs          | 242 ----------------------
 .../Vessel/R_VesselActions/Executors/ShieldSkimmerScaleConfigSO.cs    |  84 ++++++--
 .../Vessel/R_VesselActions/Executors/ShieldSkimmerScaleDriver.cs      | 196 ++++++++++++++----
 .../Vessel/R_VesselActions/Executors/ShieldSwipeActionExecutor.cs     |  58 +++++-
 24 files changed, 1667 insertions(+), 345 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1718 lines)</summary>

```diff
diff --git a/Assets/_Graphics/Materials/Graphs/ForcefieldCrackle.hlsl b/Assets/_Graphics/Materials/Graphs/ForcefieldCrackle.hlsl
index 0aca95758..21b75a4fd 100644
--- a/Assets/_Graphics/Materials/Graphs/ForcefieldCrackle.hlsl
+++ b/Assets/_Graphics/Materials/Graphs/ForcefieldCrackle.hlsl
@@ -230,4 +230,171 @@ void ForcefieldCrackle_half(
     Alpha = (half)aOut;
 }
 
+// ─── Capsule variant (the Rhino energy-sword blade) ─────────────────────────
+//
+// The sphere function parameterizes the surface by unit DIRECTION and measures
+// ripples as great-circle ANGLES — on the blade's stretched capsule (built-in
+// capsule mesh, object y ∈ [-1, 1], under a strongly non-uniform ~(1.5, 30, 4.8)
+// scale) that collapses the whole blade length into the two poles. This variant
+// measures in WORLD UNITS instead:
+//
+//   • _ImpactPositions[i].xyz = the impact's OBJECT-SPACE POSITION (not direction),
+//     stored by ForcefieldCrackleController in Capsule surface mode, so arcs stay
+//     glued to the blade through swings and stretch with it as energy grows it.
+//   • _ImpactParams[i].y      = the ripple's REACH in world units.
+//
+// ScaleOS is the object→world scale per axis (from the vertex shader's model
+// matrix); multiplying object positions by it component-wise yields world-metric
+// distances without caring about the blade's world rotation. All visual params
+// (_ArcDensity, _RingThickness, _CenterFillAmount, colors, fresnel) keep the same
+// meaning and proportions as the sphere version — ring width and center fill are
+// fractions of the reach, arcs radiate in a surface-tangent frame at the impact.
+void ForcefieldCrackleCapsule_float(
+    float3 ObjectPosition,
+    float3 ObjectNormal,
+    float3 ViewDirOS,
+    float3 ScaleOS,
+    out float3 EmissionColor,
+    out float Alpha)
+{
+    EmissionColor = float3(0, 0, 0);
+    Alpha = 0;
+
+    // Fresnel rim - same view-dependent rim as the sphere version.
+    float3 N = normalize(ObjectNormal);
+    float3 V = normalize(ViewDirOS);
+    float NdotV = saturate(dot(N, V));
+    float fresnel = pow(1.0 - NdotV, _FresnelRimPower) * _FresnelRimIntensity;
+
+    Alpha = fresnel;
+    EmissionColor = _FresnelRimColor.rgb * fresnel;
+
+    if (_ImpactCount <= 0) return;
+
+    // World-metric position on the capsule (rotation dropped: it preserves distance).
+    float3 fragSC = ObjectPosition * ScaleOS;
+
+    float totalContribution = 0;
+    float3 totalColor = float3(0, 0, 0);
+
+    for (int i = 0; i < 16; i++)
+    {
+        float4 impactPos = _ImpactPositions[i];
+        float4 impactParam = _ImpactParams[i];
+
+        float maxLifetime = impactParam.z;
+        if (maxLifetime <= 0) continue;
+
+        float intensity = impactParam.x;
+        float reach = max(impactParam.y, 0.01); // world units
+        float elapsed = impactPos.w;
+
+        float lifeRatio = saturate(elapsed / maxLifetime);
+        float timeFade = pow(1.0 - lifeRatio, 1.5);
+
+        float3 impSC = impactPos.xyz * ScaleOS;
+        float3 d = fragSC - impSC;
+
+        // Normalized surface distance: 0 at the impact, 1 at full reach — the direct
+        // analog of the sphere version's angle / (angularRadius * PI).
+        float nd = length(d) / reach;
+
+        // ── Expanding wavefront (fractions of the reach; 1/PI keeps the sphere
+        //    version's ring-width : reach proportion for the same _RingThickness) ──
+        float front = saturate(lifeRatio * _RippleSpeed);
```

</details>

### `eba0bd458` — docs(rhino): RHINO_ENERGY_SWORD.md v3 — energize ritual, FX pass, verification list

_Claude, 2026-08-12 04:34:08 +0000_

```text
Rewrites the sword doc to the v3 semantics (energize = the supershield key;
the recorded rejection covers NORMAL damage gating only), updates the
RHINO_SHIELD_SWIPE.md pointer + event-path both-held note, and files the
in-editor verification entry in Docs/UNITY_VERIFICATION_CHECKLIST.md.
```

```text
 .../_Scripts/Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md  | 340 ++++++++++++++++++++++----------
 .../_Scripts/Controller/Vessel/R_VesselActions/RHINO_SHIELD_SWIPE.md  |  19 +-
 Docs/UNITY_VERIFICATION_CHECKLIST.md                                  |  33 ++++
 3 files changed, 280 insertions(+), 112 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 489 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md b/Assets/_Scripts/Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md
index 915a8bb11..a689df572 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md
@@ -1,26 +1,71 @@
-# Rhino Energy Sword — ungated blade, super-shield popping, energy meter, crystal burst
+# Rhino Energy Sword — ungated blade, the energize ritual, energy meter, crystal burst
 
-Layers an **energy meter** and a **super-shield-popping edge** onto the Rhino's swordsmanship
-(the pose/analog-swipe rig is in `RHINO_SHIELD_SWIPE.md` — read it first). The blade is
-**ungated**: no stance, no cooldown, no energize requirement. It always damages prisms on
-contact and always destroys super-shielded prisms. A first iteration gated damage behind an
-"energize" stance + a slash cooldown; that shipped a sword that mostly didn't cut and was
-**rejected — do not reintroduce damage gates on the sword.**
+Layers an **energy meter**, the **energize ritual** (the supershield key), and a full authored
+**FX pass** onto the Rhino's swordsmanship (the pose/analog-swipe rig is in
+`RHINO_SHIELD_SWIPE.md` — read it first). Ordinary cutting is **ungated**: no stance, no
+cooldown — the sword always damages prisms on contact. The ONE gated act is popping a
+SUPER-shielded prism, which requires the blade to be **ENERGIZED**.
+
+**Design history (do not relitigate):** a first iteration gated ALL damage behind an energize
+stance + a slash cooldown; that shipped a sword that mostly didn't cut and was **rejected — never
+reintroduce a slash cooldown or stance gate on ordinary cutting.** A second iteration removed the
+ritual entirely (always-pops, ungated supershield). The user's ruling for v3: the base sword
+stays ungated for normal and shielded prisms, and **energize returns as the supershield key
+only** — the rejection covers NORMAL damage gating, not the hardened-target ritual.
 
 ## What the sword does on contact
 
-| Target | Result |
-|---|---|
-| Normal prism | Explodes, debris thrown at the **contact velocity** (below) |
-| Shielded prism | Shield pops, prism survives (standard `Prism.Damage` semantics) |
-| **Super-shielded prism** | **POPPED**: `DeactivateShields()` (stellation shatter + SFX) then `Damage(devastate: true)` (animated explode-out, unrestorable) — the sanctioned mass-conserving teardown, same sequence as `AstroLeagueArena.ClearEdgeLining`. `Prism.Damage` alone hard-ignores super-shielded prisms, which is why the shields must drop first. |
+| Target | Blade state | Result |
+|---|---|---|
+| Normal prism | any | Explodes, debris thrown at the **contact velocity** (below) |
+| Shielded prism | any | Shield pops, prism survives (standard `Prism.Damage` semantics) |
+| **Super-shielded prism** | **ENERGIZED** | **POPPED**: `DeactivateShields()` (stellation shatter + SFX) then `Damage(devastate: true)` (animated explode-out, unrestorable) — the sanctioned mass-conserving teardown, same sequence as `AstroLeagueArena.ClearEdgeLining`. `Prism.Damage` alone hard-ignores super-shielded prisms, which is why the shields must drop first. |
+| **Super-shielded prism** | not energized | **BounceBack** recoil + a dim **denied spark** at the contact point (teaches the ritual without rewarding the hit) |
+
+`popRequiresEnergizedBlade` (default **on** — the ritual IS the design) replaces v2's
+`destroySuperShielded`: flip it off on the asset to restore the v2 ungated pop as a designer
+A/B. With no sword state present (a non-Rhino skimmer reusing the asset) the blade can never be
+energized, so super-shielded prisms always bounce — the pre-sword baseline.
 
-`destroySuperShielded` (default **on** — popping IS the feature) preserves the legacy
-bounce-off as a config fallback: flip it off on the asset to restore the old recoil.
 Consequence to be aware of: super-shielded prisms are used as track/arena lining (Skim Race,
-Astro League edge). A Rhino can now carve those. That is the intended universality — one rule
-set — but if a mode must protect its lining, that's a follow-up (per-mode effect container or
-a prism-level carve-out decided then, not a silent re-gate of the sword).
+Astro League edge). An energized Rhino can carve those — deliberately a *paid, windowed* act now
+rather than free on contact. If a mode must protect its lining outright, that's a follow-up
+(per-mode effect container or a prism-level carve-out decided then, not a silent re-gate of
+ordinary cutting).
+
+## The energize ritual (the supershield key)
+
+Hold the **lower/chop stance** — both triggers pulled (sum ≥ `stanceSumThreshold`, 1.5) and even
+(|difference| ≤ `stanceCenterEpsilon`, 0.4) — for `energizeHoldSeconds` (1 s):
+
+```
+Idle ── stance held, energy ≥ cost, off cooldown ──► Charging (anticipation arcs, blade leans white)
+Charging ── stance broken or energy dips ──► Idle (no cost)
+Charging ── hold met ──► ENERGIZED (spends energizeCostFraction = 0.1; IGNITION burst)
+Energized ── stance held ──► stays lit indefinitely
+Energized ── stance left ──► tail: stays lit energizedTailSeconds (5 s)
+tail elapsed ──► Cooldown (energizeCooldownSeconds, 5 s) ──► Idle
+```
+
+- **Gesture source:** `ShieldSwipeActionExecutor.FeedSwordStance` feeds the RAW trigger targets
+  (not the smoothed pose) into `IRhinoSwordState.SetInStance` every frame — the same
+  reparameterized signals that pose the blade, so the energize gesture IS the chop pose the
+  player already knows. Thresholds live on `RhinoShieldSwipeConfig.asset`.
+- **Binary inputs energize too:** the event-driven path (touch bindings, remote replay)
+  synthesizes the stance when BOTH swipe holds are down (`diff 0, sum 2`) — which also fixes the
+  remote pose: peers now see the owner's centered chop instead of a one-sided swipe. AI never
+  pulls triggers, so AI Rhinos never energize (same limitation class as the analog swipe pose).
+- **A turn end / despawn / autopilot takeover mid-hold** drops the stance
```

</details>

### `77de3cb9c` — fix(rhino): review pass — canonical supershield flag, fixed bounce window, convergent stance

_Claude, 2026-08-12 06:13:59 +0000_

```text
Three confirmed findings from the adversarial review of the v3 diff:

1. IsSuperShield keyed on PrismStateManager.CurrentState while the canonical
   invulnerability gate is prismProperties.IsSuperShielded (what Prism.Damage/
   Consume early-return on and the shell tier owns contact by). SegmentSpawner's
   track super-shielding sets the flag with state left Normal — so the energized
   pop, the bounce, AND the denied spark were all silently dead on Skim-Race/
   HexRace lining — and pool reuse clears the flag without resetting state, so
   ordinary reborn mass would bounce (an ungated-cutting violation) and over-bank.
   Now keys on the flag.

2. BounceBack passed Time.deltaTime * accelScale as ModifyVelocity's DURATION on
   a once-per-contact dispatch — recoil ~4x stronger at 30fps than 120fps, and v3
   makes the bounce the default supershield outcome. Replaced with a fixed
   bounceDurationSeconds (0.35) on the effect SO + asset.

3. The energize stance diverged between the owner (analog thresholds) and remote
   peers (binary both-held synthesis, sum pinned to 2) — and the pop runs in each
   client's local prism sim, so peers could destroy a super-shield the owner's
   world keeps. FeedSwordStance now evaluates the replicated InputStatus trigger
   mirrors (Owner-write/Everyone-read) on every machine — identical thresholds on
   identical values — with an owner-side autopilot guard; the frozen-mirror remote
   residual is documented against the replication follow-up. The both-held event
   synthesis remains for the POSE only.
```

```text
 .../Effects/Vessel Prism Effects/RhinoSkimmerDamagePrismEffect.asset  |  2 +-
 .../Skimmer Prism Effects/RhinoSkimmerDamagePrismEffectSO.cs          | 32 +++++++++++---------
 .../R_VesselActions/Data Containers/RhinoShieldSwipeConfigSO.cs       |  5 ++--
 .../Vessel/R_VesselActions/Executors/ShieldSwipeActionExecutor.cs     | 30 +++++++++++++++----
 .../_Scripts/Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md  | 52 +++++++++++++++++++++++----------
 .../_Scripts/Controller/Vessel/R_VesselActions/RHINO_SHIELD_SWIPE.md  | 11 +++----
 6 files changed, 89 insertions(+), 43 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 152 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ShieldSwipeActionExecutor.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ShieldSwipeActionExecutor.cs
index 6753b44e6..dc846c361 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ShieldSwipeActionExecutor.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ShieldSwipeActionExecutor.cs
@@ -119,7 +119,7 @@ namespace CosmicShore.Gameplay
                 }
             }
 
-            FeedSwordStance(diffTarget, sumTarget);
+            FeedSwordStance();
 
             if (_diff == 0f && _sum == 0f && diffTarget == 0f && sumTarget == 0f) return;
 
@@ -147,16 +147,34 @@ namespace CosmicShore.Gameplay
 
         /// <summary>
         /// The energize gesture: both triggers pulled (sum high) and even (difference
-        /// near zero) — the lower/chop stance. Fed from the RAW targets, not the
-        /// smoothed pose, so the stance clock starts the frame the fingers commit.
+        /// near zero) — the lower/chop stance. Evaluated from the replicated trigger
+        /// MIRRORS (`InputStatus` n_lTrig/n_rTrig — Owner-write, Everyone-read), NOT the
+        /// local pose signals: the stance gates the supershield pop, which every client
+        /// executes in its own local prism sim, so the verdict must be computable
+        /// identically on every machine or one peer's energized blade pops a prism the
+        /// owner's world keeps (a divergent conserved prismscape). The owner writes the
+        /// mirrors from the same fingers that drive the pose; every peer runs the same
+        /// thresholds on the same values. Autopilot drops the stance on the owner's
+        /// machine (a paused InputController freezes the mirrors rather than zeroing
+        /// them; the remote-side residual of that freeze is the replication follow-up
+        /// in RHINO_ENERGY_SWORD.md).
         /// </summary>
-        void FeedSwordStance(float diffTarget, float sumTarget)
+        void FeedSwordStance()
         {
             var sword = Sword;
             if (sword == null) return;
 
-            bool inStance = sumTarget >= config.StanceSumThreshold
-                            && Mathf.Abs(diffTarget) <= config.StanceCenterEpsilon;
+            var input = _status?.InputStatus;
+            if (input == null || (_status.IsLocalUser && _status.AutoPilotEnabled))
+            {
+                sword.SetInStance(false);
+                return;
+            }
+
+            float lt = ApplyDeadzone(input.LeftTriggerAnalog);
+            float rt = ApplyDeadzone(input.RightTriggerAnalog);
+            bool inStance = lt + rt >= config.StanceSumThreshold
+                            && Mathf.Abs(rt - lt) <= config.StanceCenterEpsilon;
             sword.SetInStance(inStance);
         }
 
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md b/Assets/_Scripts/Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md
index a689df572..f9995307c 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md
@@ -47,16 +47,28 @@ Energized ── stance left ──► tail: stays lit energizedTailSeconds (5 s
 tail elapsed ──► Cooldown (energizeCooldownSeconds, 5 s) ──► Idle
 ```
 
-- **Gesture source:** `ShieldSwipeActionExecutor.FeedSwordStance` feeds the RAW trigger targets
-  (not the smoothed pose) into `IRhinoSwordState.SetInStance` every frame — the same
-  reparameterized signals that pose the blade, so the energize gesture IS the chop pose the
-  player already knows. Thresholds live on `RhinoShieldSwipeConfig.asset`.
-- **Binary inputs energize too:** the event-driven path (touch bindings, remote replay)
-  synthesizes the stance when BOTH swipe holds are down (`diff 0, sum 2`) — which also fixes the
-  remote pose: peers now see the owner's centered chop instead of a one-sided swipe. AI never
-  pulls triggers, so AI Rhinos never energize (same limitation class as the analog swipe pose).
-- **A turn end / despawn / autopilot takeover mid-hold** drops the stance
-  (`ResetImmediate` → `SetInStance(false)`), so the sword can't be left charging forever.
+- **Gesture source — the replicated trigger MIRRORS, on every machine:**
+  `ShieldSwipeActionExecutor.FeedSwordStance` evaluates the stance from
+  `InputStatus.LeftTriggerAnalog`/`RightTriggerAnalog` (the `n_lTrig`/`n_rTrig`
+  NetworkVariables — Owner-write, Everyone-read) rather than the local pose signals. This is
+  load-bearing for the conserved prismscape: the stance gates the supershield pop, every client
+  executes that pop in its own local prism sim, and the owner's analog thresholds vs a remote's
+  binary event replay would give DIFFERENT verdicts (owner at half-pull: sum ~0.95, below 1.5;
+  remote both-held synthesis: sum 2) — one machine pops a prism the other keeps. The mirrors
+  make every peer run the identical thresholds on the identical values (deadzone-renormalized
```

</details>

### `8a55d0a94` — docs(skills): asset-surgery — extend the mcs desugar list with the traps this branch hit

_Claude, 2026-08-12 06:16:55 +0000_

```text
Type-pattern switch statements (mcs dies with an internal 'type pattern
matching' error), property patterns, ??=, switch expressions, and async
UniTaskVoid all needed throwaway-copy desugaring before the harness compiled.
```

```text
 .claude/skills/asset-surgery/SKILL.md | 11 ++++++++---
 1 file changed, 8 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/.claude/skills/asset-surgery/SKILL.md b/.claude/skills/asset-surgery/SKILL.md
index 81d5af714..5a5deeef0 100644
--- a/.claude/skills/asset-surgery/SKILL.md
+++ b/.claude/skills/asset-surgery/SKILL.md
@@ -331,9 +331,14 @@ of which compiled clean and shipped):
    fixed, prove it by deleting the `using` again and watching it fail.
 2. **Desugar what mcs 6.8 (C# 7.x) cannot parse but Unity (C# 9) can** — in a
    THROWAWAY COPY, never the real file: target-typed `new(...)` → `new T(...)`,
-   `x is A or B` → `(x == A || x == B)`. Assert zero bare `new(` remain, or the
-   parse dies at the first one and every later error is cascade noise that will
-   waste your time.
+   `x is A or B` → `(x == A || x == B)`, `x ??= y` → `x = x ?? y`, property
+   patterns `x is { Y: true }` → `x != null && x.Y`, switch EXPRESSIONS → ternary
+   chains, `async UniTaskVoid` → `async void` (mcs lacks the AsyncMethodBuilder
+   plumbing), and — the one that says "Internal compiler error … type pattern
+   matching" instead of a sane message — **type-pattern `switch` STATEMENTS**
+   (`case VesselImpactor shipImpactor:`) → an `as`-cast if/else chain (mcs never
+   implemented them at all). Assert zero bare `new(` remain, or the parse dies at
+   the first one and every later error is cascade noise that will waste your time.
 3. `mcs -target:library -langversion:latest -out:/dev/null Stubs.cs <files>`.
 4. Ignore a `CS0436` warning about a type you stubbed that Mono's BCL also has
    (e.g. `System.HashCode`) — harness artifact, not a finding.
```

</details>

### `bd991451d` — feat(rhino): hilt-anchor the sword, one blade-spanning tracer, danger-colour energize

_Claude, 2026-08-12 18:15:00 +0000_

```text
Four feel changes on the energy sword:

- HILT ANCHOR. The blade mesh is centred on its transform, so growth extended the
  sword equally both ways from its mount - 120 units past the grip in each
  direction at full energy, a quarterstaff worn through the hull.
  ApplyShieldPose now offsets the centre by the blade's own half-extent along its
  local +Y, pinning the hilt to the mount so all growth goes out the tip. Reads as
  a sword at every length the meter produces.
- The geometry forced two corrections. lengthScale 1 -> 2 on the variant: Unity's
  capsule spans local +/-1, so the swing model had been describing the middle HALF
  of the visible blade (tip contacts clamped to mid-blade and reported a fraction
  of the lever arm they rode, and ClosestBladePoint-anchored FX landed wrong).
  This raises tip debris speed - swingVelocityScale is the dial if it reads hot.
  And a hilt-anchored blade's centre translates as it grows, which the sampler
  would read as a 600 u/s swing during a crystal burst: RemoveGrowthTranslation
  (pure, unit-tested both signs) strips it, holding the same rule
  includeElongation encodes.
- ONE tracer, spanning the blade. The hilt-side streak is gone with the staff
  geometry that justified it; the survivor rides the blade at a configurable
  anchor with its width driven each frame to the blade's live length, so a swing
  lays a ribbon from hilt to tip at every size instead of a thread off one end.
- Colour states. The blade no longer starts in a team-ish tint - it friendly-fires,
  so a domain-coloured blade reads as safe to allies. It rests WHITE-hot (what v2
  showed only when energized) with energy read as brightness, and ENERGIZED turns
  the shared SO_ColorSet.Danger red, speaking the platform's existing 'this hurts'
  language. The resting colour is authored in the config rather than read off the
  shared FresnelMaterial, so it cannot drift with that material's tint.
```

```text
 Assets/_Graphics/Materials/RhinoBladeCrackleMaterial.mat              |   6 +-
 .../_Prefabs/Spacevessels/Components/ForceFieldSkimmer Variant.prefab |   2 +-
 Assets/_Prefabs/Spacevessels/Rhino.prefab                             | 149 +-------------------------------
 Assets/_SO_Assets/VesselActions/Rhino/ShieldSkimmerScaleConfig.asset  |   7 +-
 .../Vessel/R_VesselActions/Executors/RhinoSwordFXController.cs        | 124 ++++++++++++++------------
 .../Vessel/R_VesselActions/Executors/ShieldSkimmerScaleConfigSO.cs    |  38 ++++++--
 .../Vessel/R_VesselActions/Executors/ShieldSwipeActionExecutor.cs     |  44 +++++++++-
 .../_Scripts/Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md  |  96 +++++++++++++++-----
 .../_Scripts/Controller/Vessel/R_VesselActions/RHINO_SHIELD_SWIPE.md  |  13 ++-
 Assets/_Scripts/Controller/Vessel/SkimmerSwingKinematics.cs           |  36 +++++++-
 Assets/_Scripts/Controller/Vessel/SkimmerSwingKinematicsConfigSO.cs   |   9 ++
 Assets/_Scripts/Tests/Editor/SkimmerSwingKinematicsTests.cs           |  46 +++++++++-
 Docs/UNITY_VERIFICATION_CHECKLIST.md                                  |  30 +++++--
 13 files changed, 346 insertions(+), 254 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 777 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordFXController.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordFXController.cs
index a13e5df49..3607937bd 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordFXController.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordFXController.cs
@@ -9,23 +9,29 @@ namespace CosmicShore.Gameplay
     /// and tuned entirely through <see cref="ShieldSkimmerScaleConfigSO"/> plus the
     /// authored sibling assets. Four layers:
     ///
-    ///  1. HEAT RAMP — the blade blends from its authored teal toward the full-energy
-    ///     cyan and brightens as stored energy fills. The blade uses the SHARED
-    ///     FresnelMaterial, so this never touches <c>renderer.material</c> — it drives
-    ///     <c>_Color</c> through a MaterialPropertyBlock (RGB above 1 feeds gameplay
+    ///  1. HEAT RAMP — the blade sits at the config's RESTING colour (white-hot, never the
+    ///     pilot's domain colour: the sword friendly-fires, so a team-tinted blade would
+    ///     read as safe to allies) and brightens as stored energy fills. The blade uses the
+    ///     SHARED FresnelMaterial, so this never touches <c>renderer.material</c> — it
+    ///     drives <c>_Color</c> through a MaterialPropertyBlock (RGB above 1 feeds gameplay
     ///     bloom; the AstroLeagueBall impact-flash precedent).
-    ///  2. ENERGIZE — the centerpiece. While CHARGING the blade leans toward white with
-    ///     escalating anticipation arcs; the IGNITION instant blends it white-hot over
-    ///     <c>ColorTransitionSeconds</c> and detonates a crackle burst along the whole
-    ///     blade through the authored <see cref="ForcefieldCrackleController"/> (the
-    ///     capsule-adapted forcefield-crackle overlay).
+    ///  2. ENERGIZE — the centerpiece, and the only thing that moves the blade's HUE:
+    ///     while CHARGING it leans toward the danger colour with escalating anticipation
+    ///     arcs; the IGNITION instant blends it fully to <c>EnergizedColor</c> — the shared
+    ///     <c>SO_ColorSet.Danger</c> red — over <c>ColorTransitionSeconds</c> and detonates
+    ///     a crackle burst along the whole blade through the authored
+    ///     <see cref="ForcefieldCrackleController"/> (the capsule-adapted overlay). Energy
+    ///     is brightness, state is hue; the two signals can never be confused.
     ///  3. IMPACT FEEDBACK — a decaying white-out flash per prism destroyed plus a
     ///     crackle spark at the exact blade point that made contact
     ///     (<see cref="SkimmerSwingKinematics.ClosestBladePoint"/>); a dim DENIED spark
     ///     when a non-energized blade bounces off a super-shielded prism.
-    ///  4. TIP TRACERS — two authored TrailRenderers seated at the blade tips each frame
-    ///     (parented to the fuselage in the prefab so their width doesn't scale with the
-    ///     growing blade), tinted with the live blade colour via MaterialPropertyBlock.
+    ///  4. BLADE TRACER — ONE authored TrailRenderer (fuselage-parented in the prefab so
+    ///     the ribbon's shape never inherits the blade's scale) riding the blade at
+    ///     <c>TracerBladeAnchor01</c>, with its width driven each frame to the blade's LIVE
+    ///     world length: a swing lays a ribbon that spans hilt-to-tip rather than a thread
+    ///     off one end, at every size the energy meter produces. Tinted with the live blade
+    ///     colour via MaterialPropertyBlock.
     ///
     /// Camera shake (super-shield pop, crystal burst) fires for the LOCAL human pilot
     /// only. See <c>RHINO_ENERGY_SWORD.md</c>.
@@ -45,10 +51,10 @@ namespace CosmicShore.Gameplay
         [SerializeField] private MeshRenderer bodyRenderer;
         [Tooltip("The crackle overlay driver on the blade (capsule surface mode). Falls back to this GameObject's controller.")]
         [SerializeField] private ForcefieldCrackleController crackle;
-        [Tooltip("Authored tracer streak seated at the blade's TIP each frame (a fuselage child, so width ignores blade growth).")]
-        [SerializeField] private TrailRenderer tipTracer;
-        [Tooltip("Authored tracer streak seated at the blade's HILT-side tip each frame.")]
-        [SerializeField] private TrailRenderer hiltTracer;
+        [Tooltip("Authored swing ribbon. A fuselage child (so the blade's scale never distorts it), " +
+                 "re-seated on the blade each frame at ShieldSkimmerScaleConfig.TracerBladeAnchor01 " +
+                 "with its width driven to the blade's live length.")]
+        [SerializeField] private TrailRenderer bladeTracer;
 
         Skimmer _skimmer;
         SkimmerSwingKinematics _swing;
@@ -56,11 +62,11 @@ namespace CosmicShore.Gameplay
         MaterialPropertyBlock _bodyMpb;
         MaterialPropertyBlock _tracerMpb;
         bool _hasColorProp;
-        Color _authored;          // FresnelMaterial's authored teal, read once from sharedMaterial
         Color _appliedBodyColor;
         bool _bodyColorApplied;
         Color _appliedTracerColor;
         bool _tracerColorApplied;
+        float _appliedTracerWidth = float.NaN;
 
         float _flash;             // 0 = none, 1 = full white-out; decays each Tick
         float _energizedBlend;    // 0 = heat ramp, 1 = white-hot; eased by ColorTransitionSeconds
@@ -77,23 +83,19 @@ namespace CosmicShore.Gameplay
             if (!bodyRenderer) TryGetComponent(out bodyRenderer);
             if (!crackle) TryGetComponent(out crackle);
 
-            _authored = new Color(0.055f, 0.755f, 0.712f, 1f); // FresnelMaterial teal fallback
-            _hasColorProp = false;
```

</details>

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

### `e0e46e99d` — fix(rhino): tone the sword tracer back to a tip streak, and the blade out of bloom

_Claude, 2026-08-12 20:37:34 +0000_

```text
The previous pass sized the tracer to the WHOLE blade (mid-blade anchor, width =
blade length), reasoning that a TrailRenderer lays width across its path so the
ribbon would span hilt-to-tip. It does - and at a 240-unit blade that is a
240-unit-wide white sheet, on a blade about 10 units thick. Reverted to a tip
trace: anchored at the tip, width 0.05 of the blade, reaching a quarter of the
way back down it before the authored width curve and alpha gradient grade it to
nothing. Both dimensions still track the blade's live length so the proportion
survives the energy meter, and the streak's LIFETIME is solved from that length
against a calibration speed rather than from live speed - driving it from live
speed would retroactively expire points and pop the streak at the start of every
swing. It stays tinted from the same live blade colour, so it changes with the
sword through every state.

Also toned the blade out of bloom: visibilityMultiplier 2 -> 1.2 and
fullEnergyBrightness 2.5 -> 1.8 (the pair compounded to a 5x HDR white), and the
impact flash 3 -> 2.
```

```text
 Assets/_SO_Assets/VesselActions/Rhino/ShieldSkimmerScaleConfig.asset  | 13 ++++---
 .../Vessel/R_VesselActions/Executors/RhinoSwordFXController.cs        | 39 +++++++++++++-------
 .../Vessel/R_VesselActions/Executors/ShieldSkimmerScaleConfigSO.cs    | 63 ++++++++++++++++++++++++---------
 .../_Scripts/Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md  | 40 ++++++++++++---------
 Docs/UNITY_VERIFICATION_CHECKLIST.md                                  | 16 +++++----
 5 files changed, 112 insertions(+), 59 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 280 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordFXController.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordFXController.cs
index 3607937bd..8c9bf7513 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordFXController.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordFXController.cs
@@ -26,12 +26,13 @@ namespace CosmicShore.Gameplay
     ///     crackle spark at the exact blade point that made contact
     ///     (<see cref="SkimmerSwingKinematics.ClosestBladePoint"/>); a dim DENIED spark
     ///     when a non-energized blade bounces off a super-shielded prism.
-    ///  4. BLADE TRACER — ONE authored TrailRenderer (fuselage-parented in the prefab so
-    ///     the ribbon's shape never inherits the blade's scale) riding the blade at
-    ///     <c>TracerBladeAnchor01</c>, with its width driven each frame to the blade's LIVE
-    ///     world length: a swing lays a ribbon that spans hilt-to-tip rather than a thread
-    ///     off one end, at every size the energy meter produces. Tinted with the live blade
-    ///     colour via MaterialPropertyBlock.
+    ///  4. TIP TRACER — ONE authored TrailRenderer (fuselage-parented in the prefab so the
+    ///     streak's shape never inherits the blade's scale) riding the sword's tip, slim, and
+    ///     reaching about a quarter of the way back down the blade before the authored width
+    ///     curve and alpha gradient grade it to nothing. Width and lifetime are both driven
+    ///     from the blade's live length so that proportion survives the energy meter. Tinted
+    ///     with the live blade colour via MaterialPropertyBlock, so the streak changes with
+    ///     the sword through every state.
     ///
     /// Camera shake (super-shield pop, crystal burst) fires for the LOCAL human pilot
     /// only. See <c>RHINO_ENERGY_SWORD.md</c>.
@@ -67,6 +68,7 @@ namespace CosmicShore.Gameplay
         Color _appliedTracerColor;
         bool _tracerColorApplied;
         float _appliedTracerWidth = float.NaN;
+        float _appliedTracerSeconds = float.NaN;
 
         float _flash;             // 0 = none, 1 = full white-out; decays each Tick
         float _energizedBlend;    // 0 = heat ramp, 1 = white-hot; eased by ColorTransitionSeconds
@@ -272,25 +274,36 @@ namespace CosmicShore.Gameplay
         }
 
         /// <summary>
-        /// Ride the blade and size the ribbon to it. A TrailRenderer lays its width
-        /// PERPENDICULAR to the path it travels — during a swipe that is across the blade's
-        /// length — so anchoring the emitter mid-blade and driving the width to the blade's
-        /// live length makes the streak stretch from hilt to tip instead of trailing off one
-        /// end as a thread. Driving it every frame is what keeps that true "at all sizes":
-        /// the ribbon grows with the energy meter and with the crystal burst.
+        /// Ride the sword's TIP and keep the streak proportioned to the blade: a slim trace
+        /// reaching about <see cref="ShieldSkimmerScaleConfigSO.TracerLengthBladeFraction"/> of
+        /// the way back down the blade, graded to nothing by the TrailRenderer's authored width
+        /// curve and alpha gradient. Both dimensions are driven from the blade's LIVE length so
+        /// the proportion holds as the energy meter grows the sword — width as a small fraction
+        /// of it, length by solving the trail's lifetime against a calibration speed (driving
+        /// lifetime from live speed instead would retroactively expire points and pop the streak
+        /// at the start of every swing).
         /// </summary>
         void SeatTracer()
         {
             if (!bladeTracer || config == null) return;
             bladeTracer.transform.position = PointAlongBlade(config.TracerBladeAnchor01);
 
-            float width = BladeWorldLength * config.TracerWidthLengthFraction;
+            float length = BladeWorldLength;
+
+            float width = length * config.TracerWidthBladeFraction;
             if (!Mathf.Approximately(width, _appliedTracerWidth))
             {
                 _appliedTracerWidth = width;
                 // widthMultiplier scales the authored width CURVE, so the taper stays designed.
                 bladeTracer.widthMultiplier = width;
             }
+
+            float seconds = config.TracerSecondsFor(length);
+            if (!Mathf.Approximately(seconds, _appliedTracerSeconds))
+            {
+                _appliedTracerSeconds = seconds;
+                bladeTracer.time = seconds;
+            }
         }
 
         void ApplyTracerColor(Color color)
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ShieldSkimmerScaleConfigSO.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ShieldSkimmerScaleConfigSO.cs
index 5cf9cb593..baee17ad5 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ShieldSkimmerScaleConfigSO.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ShieldSkimmerScaleConfigSO.cs
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

### `309f5af2d` — refactor(rhino): hand the tracer's size back to the inspector, keep its top edge on the tip

_Claude, 2026-08-14 22:31:11 +0000_

```text
SeatTracer no longer writes widthMultiplier or time - width, time, taper curve,
gradient and material are all authored on the RhinoSwordTipTracer TrailRenderer
and tuned there. The controller owns placement only: a TrailRenderer lays its
width symmetrically about the emitter path, so an emitter parked on the tip hangs
half the band past the point of the sword. It now sits half a head-width back
down the blade (head width = widthMultiplier x the width curve at t=0, read again
only when the multiplier moves, since the curve getter allocates), which keeps the
band's top edge on the tip at any authored width - widening grows the streak down
the blade instead of through the point.

The five driven dials (tracerBladeAnchor01 / LengthBladeFraction /
WidthBladeFraction / ReferenceSpeed / SecondsRange) and TracerSecondsFor are
removed from the config SO and asset rather than left as knobs that do nothing.
Prefab seeded at widthMultiplier 6 / time 0.12 as a starting point.
```

```text
 Assets/_Prefabs/Spacevessels/Rhino.prefab                             |  4 +-
 Assets/_SO_Assets/VesselActions/Rhino/ShieldSkimmerScaleConfig.asset  |  5 ---
 .../Vessel/R_VesselActions/Executors/RhinoSwordFXController.cs        | 80 +++++++++++++++++----------------
 .../Vessel/R_VesselActions/Executors/ShieldSkimmerScaleConfigSO.cs    | 38 ----------------
 .../_Scripts/Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md  | 41 ++++++++++-------
 Docs/UNITY_VERIFICATION_CHECKLIST.md                                  | 16 ++++---
 6 files changed, 76 insertions(+), 108 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 275 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordFXController.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordFXController.cs
index 8c9bf7513..d2e3fc9f0 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordFXController.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordFXController.cs
@@ -27,12 +27,12 @@ namespace CosmicShore.Gameplay
     ///     (<see cref="SkimmerSwingKinematics.ClosestBladePoint"/>); a dim DENIED spark
     ///     when a non-energized blade bounces off a super-shielded prism.
     ///  4. TIP TRACER — ONE authored TrailRenderer (fuselage-parented in the prefab so the
-    ///     streak's shape never inherits the blade's scale) riding the sword's tip, slim, and
-    ///     reaching about a quarter of the way back down the blade before the authored width
-    ///     curve and alpha gradient grade it to nothing. Width and lifetime are both driven
-    ///     from the blade's live length so that proportion survives the energy meter. Tinted
-    ///     with the live blade colour via MaterialPropertyBlock, so the streak changes with
-    ///     the sword through every state.
+    ///     streak's shape never inherits the blade's scale). Its whole SHAPE is authored on the
+    ///     component — width, time, taper curve, gradient, material — and this controller never
+    ///     writes any of it; all it does is PLACE the emitter half a head-width back down the
+    ///     blade so the band's top edge lands on the sword's tip at whatever width is dialled
+    ///     in. Tinted with the live blade colour via MaterialPropertyBlock, so the streak
+    ///     changes with the sword through every state.
     ///
     /// Camera shake (super-shield pop, crystal burst) fires for the LOCAL human pilot
     /// only. See <c>RHINO_ENERGY_SWORD.md</c>.
@@ -52,9 +52,10 @@ namespace CosmicShore.Gameplay
         [SerializeField] private MeshRenderer bodyRenderer;
         [Tooltip("The crackle overlay driver on the blade (capsule surface mode). Falls back to this GameObject's controller.")]
         [SerializeField] private ForcefieldCrackleController crackle;
-        [Tooltip("Authored swing ribbon. A fuselage child (so the blade's scale never distorts it), " +
-                 "re-seated on the blade each frame at ShieldSkimmerScaleConfig.TracerBladeAnchor01 " +
-                 "with its width driven to the blade's live length.")]
+        [Tooltip("The sword's tip streak. A fuselage child (so the blade's scale never distorts it). " +
+                 "TUNE IT ON THE COMPONENT — width, time, taper curve, gradient and material are all " +
+                 "yours and nothing here overwrites them; this only re-seats the emitter each frame so " +
+                 "the top edge of the band stays on the blade's tip at whatever width you set.")]
         [SerializeField] private TrailRenderer bladeTracer;
 
         Skimmer _skimmer;
@@ -67,8 +68,8 @@ namespace CosmicShore.Gameplay
         bool _bodyColorApplied;
         Color _appliedTracerColor;
         bool _tracerColorApplied;
-        float _appliedTracerWidth = float.NaN;
-        float _appliedTracerSeconds = float.NaN;
+        float _appliedTracerWidth = float.NaN;   // last widthMultiplier seen, for the curve re-read
+        float _tracerHeadWidthFactor = 1f;       // authored width curve evaluated at the emitting end
 
         float _flash;             // 0 = none, 1 = full white-out; decays each Tick
         float _energizedBlend;    // 0 = heat ramp, 1 = white-hot; eased by ColorTransitionSeconds
@@ -249,11 +250,6 @@ namespace CosmicShore.Gameplay
             return transform.position + transform.up * Mathf.Lerp(-half, half, Mathf.Clamp01(t01));
         }
 
-        /// <summary>The blade's live world length, hilt to tip.</summary>
-        float BladeWorldLength => 2f * (_swing && _swing.IsReady
-            ? _swing.HalfLength
-            : Mathf.Abs(transform.lossyScale.y));
-
         Vector3 BladePointNear(Vector3 worldPoint)
         {
             if (_swing && _swing.IsReady) return _swing.ClosestBladePoint(worldPoint);
@@ -274,36 +270,42 @@ namespace CosmicShore.Gameplay
         }
 
         /// <summary>
-        /// Ride the sword's TIP and keep the streak proportioned to the blade: a slim trace
-        /// reaching about <see cref="ShieldSkimmerScaleConfigSO.TracerLengthBladeFraction"/> of
-        /// the way back down the blade, graded to nothing by the TrailRenderer's authored width
-        /// curve and alpha gradient. Both dimensions are driven from the blade's LIVE length so
-        /// the proportion holds as the energy meter grows the sword — width as a small fraction
-        /// of it, length by solving the trail's lifetime against a calibration speed (driving
-        /// lifetime from live speed instead would retroactively expire points and pop the streak
-        /// at the start of every swing).
+        /// Seat the streak so its top edge sits on the sword's TIP, whatever width is authored.
+        ///
+        /// The streak's SHAPE is yours: width, time, taper curve, gradient and material are all
+        /// authored on the TrailRenderer and this never writes them. All it owns is placement —
+        /// and because a TrailRenderer lays its width symmetrically about the emitter's path, an
+        /// emitter parked on the tip would hang half the band out past the point of the sword.
+        /// So it sits half a head-width back down the blade, which puts the band's top edge on
+        /// the tip and the rest of it running down the blade, at any width you dial in.
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

### `f97ab47e5` — feat(input): dual-WASD keyboard flight strategy

_josephzhou124, 2026-08-14 22:51:53 -0400_

```text
 Assets/KeyboardInputStrategy.cs                             | 298 ------------------------------------------
 Assets/KeyboardInputStrategy.cs.meta                        |   2 -
 Assets/_Scripts/Controller/IO/KeyboardInputStrategy.cs      | 274 ++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Controller/IO/KeyboardInputStrategy.cs.meta |  11 ++
 4 files changed, 285 insertions(+), 300 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 585 lines)</summary>

```diff
diff --git a/Assets/KeyboardInputStrategy.cs b/Assets/KeyboardInputStrategy.cs
deleted file mode 100644
index 3e080ae8e..000000000
--- a/Assets/KeyboardInputStrategy.cs
+++ /dev/null
@@ -1,298 +0,0 @@
-using UnityEngine;
-using UnityEngine.InputSystem;
-using CosmicShore.Data;
-
-namespace CosmicShore.Gameplay
-{
-    public class KeyboardInputStrategy : BaseInputStrategy
-    {
-        // Smoothing for analog-like feel
-        private const float RAMP_SPEED = 8f;
-        private const float DEAD_ZONE = 0.05f;
-
-        // State tracking for effects
-        private bool fullSpeedStraightEffectsStarted;
-        private bool minimumSpeedStraightEffectsStarted;
-
-        // Virtual stick positions (smoothed)
-        private Vector2 leftStickCurrent;
-        private Vector2 rightStickCurrent;
-
-        // Target positions (raw key input)
-        private Vector2 leftStickTarget;
-        private Vector2 rightStickTarget;
-
-        // Button state tracking for press/release detection
-        private bool wasButton1Pressed;
-        private bool wasButton2Pressed;
-        private bool wasButton3Pressed;
-        private bool wasLeftTriggerPressed;
-        private bool wasRightTriggerPressed;
-        private bool wasFlipPressed;
-
-        public override void Initialize(IInputStatus inputStatus)
-        {
-            base.Initialize(inputStatus);
-            ResetInput();
-            ResetSmoothingState();
-        }
-
-        private void ResetSmoothingState()
-        {
-            leftStickCurrent = Vector2.zero;
-            rightStickCurrent = Vector2.zero;
-            leftStickTarget = Vector2.zero;
-            rightStickTarget = Vector2.zero;
-
-            wasButton1Pressed = false;
-            wasButton2Pressed = false;
-            wasButton3Pressed = false;
-            wasLeftTriggerPressed = false;
-            wasRightTriggerPressed = false;
-            wasFlipPressed = false;
-
-            fullSpeedStraightEffectsStarted = false;
-            minimumSpeedStraightEffectsStarted = false;
-        }
-
-        public override void ProcessInput()
-        {
-            var keyboard = Keyboard.current;
-            if (keyboard == null) return;
-
-            ProcessStickInput(keyboard);
-            ProcessButtonInput(keyboard);
-            Reparameterize();
-            PerformSpeedAndDirectionalEffects();
-        }
-
-        private void ProcessStickInput(Keyboard keyboard)
-        {
-            // Read target positions from keys (WASD for left stick)
-            leftStickTarget = new Vector2(
-                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
-                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f)
```

</details>

### `3332f226b` — feat(input): dual-WASD keyboard flight strategy

_josephzhou124, 2026-08-14 22:59:36 -0400_

```text
 Assets/_Scripts/Controller/IO/KeyboardInputStrategy.cs | 48 +++++++++++++++++++-----------------------------
 1 file changed, 19 insertions(+), 29 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/IO/KeyboardInputStrategy.cs b/Assets/_Scripts/Controller/IO/KeyboardInputStrategy.cs
index e02596f2d..472988423 100644
--- a/Assets/_Scripts/Controller/IO/KeyboardInputStrategy.cs
+++ b/Assets/_Scripts/Controller/IO/KeyboardInputStrategy.cs
@@ -10,11 +10,16 @@ namespace CosmicShore.Gameplay
     /// No mouse. No vessel-class special cases — drift and tube bind through
     /// the existing stick/trigger events.
     ///
-    /// Left stick WASD:  W +Y, S -Y, A -X, D +X
-    /// Right stick P L ; ':  P +Y, ; -Y, L -X, ' +X
-    /// Mix: yaw = XSum, pitch = YSum (W+P stacks), speed = XDiff (A+' fast, L+D slow),
-    ///      roll = YDiff (P+S left, W+; right). Neutral horizontals → XDiff 0.5 cruise.
-    /// Left Shift hold = left trigger (drift). Right Shift press = right trigger (tube).
+    /// Key → stick → mix → Squirrel feel
+    ///   W / S     left  +Y / -Y     YSum pitch (W+P = big pitch up, S+; = big pitch down)
+    ///   A / D     left  -X / +X     XSum yaw; with ' / L → XDiff speed (A+' fast, L+D slow)
+    ///   P / ;     right +Y / -Y     YDiff roll vs left Y (P+S roll left, W+; roll right)
+    ///   L / '     right -X / +X     (same mix as A / D)
+    ///   Left Shift  left trigger analog 1, LeftStickAction / OnlyLeftStickAction
+    ///               → Squirrel drift (prefab singleTriggerDrift; gamepad binds LeftStickAction)
+    ///   Right Shift right trigger press, RightStickAction / OnlyRightStickAction
+    ///               → Squirrel tube / prism ring (Begin on press; Commit is a no-op)
+    /// Neutral A/D/L/' → XDiff 0.5 cruise. InvertY / InvertThrottle apply after mix.
     /// </summary>
     public class KeyboardInputStrategy : BaseInputStrategy
     {
@@ -176,33 +181,18 @@ namespace CosmicShore.Gameplay
 
         private void Reparameterize()
         {
-            inputStatus.EasedLeftJoystickPosition = new Vector2(
-                Ease(2 * leftStickRaw.x),
-                Ease(2 * leftStickRaw.y)
-            );
-            inputStatus.EasedRightJoystickPosition = new Vector2(
-                Ease(2 * rightStickRaw.x),
-                Ease(2 * rightStickRaw.y)
-            );
+            var mix = DualStickMix.Mix(
+                leftStickRaw, rightStickRaw,
+                inputStatus.InvertYEnabled, inputStatus.InvertThrottleEnabled);
 
+            inputStatus.EasedLeftJoystickPosition = mix.EasedLeft;
+            inputStatus.EasedRightJoystickPosition = mix.EasedRight;
             inputStatus.RightNormalizedJoystickPosition = rightStickRaw;
             inputStatus.LeftNormalizedJoystickPosition = leftStickRaw;
-
-            inputStatus.XSum = Ease(rightStickRaw.x + leftStickRaw.x);
-            inputStatus.YSum = -Ease(rightStickRaw.y + leftStickRaw.y);
-            inputStatus.XDiff = (rightStickRaw.x - leftStickRaw.x + 2) / 4;
-            inputStatus.YDiff = Ease(rightStickRaw.y - leftStickRaw.y);
-
-            if (inputStatus.InvertYEnabled)
-            {
-                inputStatus.YSum *= -1f;
-                inputStatus.YDiff *= -1f;
-            }
-
-            if (inputStatus.InvertThrottleEnabled)
-            {
-                inputStatus.XDiff = 1f - inputStatus.XDiff;
-            }
+            inputStatus.XSum = mix.XSum;
+            inputStatus.YSum = mix.YSum;
+            inputStatus.XDiff = mix.XDiff;
+            inputStatus.YDiff = mix.YDiff;
         }
 
         private void PerformSpeedAndDirectionalEffects()
```

</details>

### `5d3351050` — fix(dolphin): the crackle is its only skim visual, beam removed

_Claude, 2026-08-15 03:11:51 +0000_

```text
The Dolphin was drawing two skim visuals at once. SkimmerFXPrismEffect — the
legacy per-prism beam, marked [Obsolete] in code as "replaced by
SkimmerForcefieldCracklePrismEffectSO" — went into its skimmer container as the
interim answer to "a skim produces no feedback at all" (1fc9517c), before the
crackle was wired. When the crackle landed three hours later (12d0e3db) the beam
was deliberately left alongside it because the Squirrel runs both. On a vessel
whose economy needs ~150 skims to fill its meter, that is a beam stretching from
the hull to every prism in the sphere on top of the crackle, continuously — noise,
not feedback.

Removed the beam from DolphinSkimmerImpactorDataContainer. The container now holds
the resource gain, the haptic pulse, and the forcefield crackle: one visual, the
same one the Squirrel shows, and nothing else. The Squirrel is untouched — this is
a Dolphin decision, not a platform one, and the obsolete SO stays for its other
consumers.

No wiring is lost: the crackle's three pieces (effect, ForcefieldCrackleController,
overlay MeshRenderer) are all still present, so FrogletTools > Vessels > Audit
Vessel Skimmers still passes — the container is non-empty and the crackle is fully
wired.
```

```text
 .../SkimmerContainers/DolphinSkimmerImpactorDataContainer.asset                  |  1 -
 Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md      | 22 +++++++++++++++++-----
 2 files changed, 17 insertions(+), 6 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md b/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md
index f7a6e6be8..528c15940 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md
@@ -400,11 +400,11 @@ Three things make this legal on a HUD whose upgrade tint is deliberately off (§
 
 ### Why a skim punches the jaws
 
-One skim moves the gape by a 150th of its range — about 0.12°, invisible. The three signals
-wired to a skim are otherwise: a haptic pulse that is a **no-op on desktop**, and a beam VFX
-that only draws if the skimmed prism authors a `ParticleEffect`. So the discrete event gets
-its own beat: `DolphinVesselHUDController` treats an energy **rise** as the skim (nothing
-else raises energy — the blast spends it all, a ram halves it) and punches the jaw pair.
+One skim moves the gape by a 150th of its range — about 0.12°, invisible. The other signals
+wired to a skim are the forcefield crackle across the skimmer sphere and a haptic pulse that
+is a **no-op on desktop**. So the discrete event gets its own beat:
+`DolphinVesselHUDController` treats an energy **rise** as the skim (nothing else raises
+energy — the blast spends it all, a ram halves it) and punches the jaw pair.
 
 ---
 
@@ -438,6 +438,17 @@ The Squirrel gets (2) and (3) free because its skimmer *is* `Skimmer.prefab`, wh
 both. The Dolphin's `EnergySkimmer` is a standalone object and needed all three added. The
 audit checks (2) and (3) whenever a container asks for (1).
 
+### The crackle is the Dolphin's ONLY skim visual
+
+The legacy `SkimmerFXPrismEffectSO` — the per-prism beam, marked `[Obsolete]` in code as
+"replaced by `SkimmerForcefieldCracklePrismEffectSO`" — was added to the Dolphin's container
+as the interim answer to "a skim produces no feedback at all", *before* the crackle was
+wired. Once the crackle landed, both ran: a beam stretching from the hull to every prism in
+the sphere **on top of** the crackle, which reads as noise on a vessel that skims ~150 prisms
+to fill its meter. The beam is now removed from `DolphinSkimmerImpactorDataContainer`; the
+container holds the resource gain, the haptic, and the crackle, and nothing else. The
+Squirrel still runs both — this is a Dolphin decision, not a platform one.
+
 ### The skimmer no longer resizes itself on init
 
 `Skimmer.ApplyScaleIfChanged` writes `localScale` from its `ElementalFloat` **even when
@@ -457,6 +468,7 @@ Play Menu_Main, enter freestyle on the Dolphin.
 | **FrogletTools > Vessels > Audit Vessel Skimmers** | Dolphin `NearFieldSkimmer: 'EnergySkimmer' OK` |
 | **FrogletTools > Vessels > Audit Vessel Ability Rows** | Dolphin: map complete, 4/4 icons, order ✅ |
 | fly through cell mass | crackle arcs across the skimmer sphere per prism; jaw icon punches; gape widens |
+| fly through cell mass | **no beam** stretches from the hull to the skimmed prisms — the crackle is the only skim visual |
 | at zero energy | jaws sit slightly open (4.76°/side), NOT shut — hull and Time icon agree |
 | keep skimming | model's jaws open toward 23.4° per side (**~150 skims** to full); Time icon matches at every step |
 | cross ~85% energy | Time icon's jaws start blending white → lime; solid lime at full |
```

</details>

### `65df61c34` — feat(ecology): rebuild the elemental crystal capture as a 0.44s snatch-suction-absorb

_Claude, 2026-08-15 03:21:56 +0000_

```text
The pickup that pays out the whole food web was the weakest frame in the game:
the crystal dragged to the vessel over 3s (1s on two fauna prefabs, 3s on eleven
flora prefabs - the duration was authored per prefab), lerping from a frozen start
point toward a moving ship so it read as chasing rather than being pulled; it flew
at full scale with the idle tumble still running; then it simply stopped existing
via a bare Destroy - except the Space crystal, which bolted a 0.6s blendshape
shrink onto the END of the flight and parked a full-size crystal on the hull. No
burst, no pickup SFX: the omni crystal has played a spent-husk burst and
CrystalCollect forever, and the elemental path never called it.

Replaced with three beats, all feel in ONE asset (Resources/CrystalCaptureConfig):

  snatch  0.08s - scale pops to 1.5x and the crystal kicks AWAY from the vessel;
                  the recoil is the anticipation that sells the pull
  suction 0.26s - homes on the vessel's live position, accelerating (u^2.6), on an
                  arc, spinning up 2.25 revolutions, shrinking, flaring to 3x
  absorb  0.10s - rides the hull, collapses to zero scale and dissolves _opacity
                  out, and fires the element's spent-crystal husk into the wake

0.44s total against 3.0-3.6s. Everything is sized in crystal radii, so a grown
flora heart and a tiny fauna drop capture identically.

Three rules, recorded in Docs/ECOSYSTEM.md §30:
- a flourish must never outlast its own payoff. The element level lands at contact,
  so OnCrystalCollected (the scoring event four modes count) now fires there too
  instead of inside the flight loop - a mode's objective can never wait on a visual.
- homing on a moving target is a function of DURATION; shortening the flight fixed
  more of the look than any easing change could.
- continuity of existence applies to a crystal, not just to prisms: it blooms in on
  _opacity via FadeIn and now leaves the same way, spent as screen-door coverage.

Nothing new invented - the burst is the existing pooled Crystal.Explode path all
four elemental prefabs were already authored for, the flare is one
MaterialPropertyBlock scaling the crystal's own colours (RGB only, alpha preserved -
a gain brightens without shifting hue, per Docs/PALETTE.md), and no FMOD event was
added. Crystal.Explode gained one optional huskScale so the payoff is sized by the
crystal the pilot picked up rather than by the flourish that shrank it; the
networked path takes the default and is unchanged.

moveToVesselDuration/easeMoveToVessel are gone from the impactor and their now-dead
serialized keys stripped from the 15 prefabs that authored them.
```

```text
 Assets/Resources/CrystalCaptureConfig.asset                           |  25 ++++
 Assets/Resources/CrystalCaptureConfig.asset.meta                      |   8 ++
 Assets/_Models/Fauna/MassBrittlestarFauna.prefab                      |   2 -
 Assets/_Models/Fauna/MassSharkFauna.prefab                            |   2 -
 Assets/_Prefabs/FloraAndFauna/ArborFlora.prefab                       |   2 -
 Assets/_Prefabs/FloraAndFauna/BranchingFlora.prefab                   |   2 -
 Assets/_Prefabs/FloraAndFauna/CactiFlora.prefab                       |   2 -
 Assets/_Prefabs/FloraAndFauna/CoralFlora.prefab                       |   2 -
 Assets/_Prefabs/FloraAndFauna/FrondFlora.prefab                       |   2 -
 Assets/_Prefabs/FloraAndFauna/GyroidFlora.prefab                      |   2 -
 Assets/_Prefabs/FloraAndFauna/LanternFlora.prefab                     |   2 -
 Assets/_Prefabs/FloraAndFauna/ReedFlora.prefab                        |   2 -
 Assets/_Prefabs/FloraAndFauna/RosetteFlora.prefab                     |   2 -
 Assets/_Prefabs/FloraAndFauna/SchwarzPFlora.prefab                    |   2 -
 Assets/_Prefabs/FloraAndFauna/SpireFlora.prefab                       |   2 -
 Assets/_Prefabs/FloraAndFauna/TadPoleFauna.prefab                     |   2 -
 Assets/_Prefabs/FloraAndFauna/TendrilFlora.prefab                     |   2 -
 Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs           | 109 +++++++++++++++---
 .../Controller/ImpactEffects/Impactors/ElementalCrystalImpactor.cs    | 196 ++++++++++++++++++++++----------
 Assets/_Scripts/ScriptableObjects/CrystalCaptureConfigSO.cs           | 193 +++++++++++++++++++++++++++++++
 Assets/_Scripts/ScriptableObjects/CrystalCaptureConfigSO.cs.meta      |  11 ++
 Assets/_Scripts/Tests/Editor/CrystalCaptureConfigTests.cs             | 147 ++++++++++++++++++++++++
 Assets/_Scripts/Tests/Editor/CrystalCaptureConfigTests.cs.meta        |  11 ++
 CLAUDE.md                                                             |   9 ++
 Docs/ECOSYSTEM.md                                                     |  99 ++++++++++++++++
 25 files changed, 733 insertions(+), 105 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 906 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs b/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
index a05beb9d1..99be3986c 100644
--- a/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
+++ b/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
@@ -125,35 +125,51 @@ namespace CosmicShore.Gameplay
 
         static MaterialPropertyBlock s_tintBlock;
 
-        /// <summary>Tints all crystal models from the theme ColorSet by collectability state
-        /// (see comment above). No-op when the theme container or color set is unwired.</summary>
-        protected void ApplyColorSetTint()
+        // The property every crystal shader exposes for its dissolve - the same one FadeIn drives
+        // to bloom a crystal IN (CrystalGraph, ChargeCrystal). A capture drives it back to 0.
+        static readonly int OpacityID = Shader.PropertyToID("_opacity");
+
+        /// <summary>
+        /// The bright/dull colour pair this crystal should currently wear, per the collectability
+        /// rules above. Shared by the steady-state tint and the capture flare so the two can never
+        /// disagree about which colours a crystal is flaring FROM.
+        /// </summary>
+        bool TryResolveTintColors(out Color bright, out Color dull)
         {
-            if (!_themeManagerData || _themeManagerData.ColorSet == null) return;
+            bright = dull = default;
+            if (!_themeManagerData || _themeManagerData.ColorSet == null) return false;
             var colors = _themeManagerData.ColorSet;
 
-            Color bright, dull;
             bool domainOwned = ownDomain is Domains.Jade or Domains.Ruby or Domains.Gold;
             if (domainOwned && colors.TryGetColorSetByDomain(ownDomain, out var domainSet) && domainSet != null)
             {
                 bright = domainSet.BrightCrystalColor;
                 dull = domainSet.DullCrystalColor;
+                return true;
             }
-            else if (IsEmbedded)
+
+            if (IsEmbedded)
             {
                 // A living lifeform's heart: the blue-white neutral range - no domain can take it.
-                if (!colors.TryGetColorSetByDomain(Domains.Blue, out var neutralSet) || neutralSet == null) return;
+                if (!colors.TryGetColorSetByDomain(Domains.Blue, out var neutralSet) || neutralSet == null) return false;
                 bright = neutralSet.BrightCrystalColor;
                 dull = neutralSet.DullCrystalColor;
-            }
-            else
-            {
-                // Free collectible: the lime CTA - any domain can collect it now.
-                if (colors.EnvironmentColors == null) return;
-                bright = colors.EnvironmentColors.BrightCTA;
-                dull = colors.EnvironmentColors.DarkCTA;
+                return true;
             }
 
+            // Free collectible: the lime CTA - any domain can collect it now.
+            if (colors.EnvironmentColors == null) return false;
+            bright = colors.EnvironmentColors.BrightCTA;
+            dull = colors.EnvironmentColors.DarkCTA;
+            return true;
+        }
+
+        /// <summary>Tints all crystal models from the theme ColorSet by collectability state
+        /// (see comment above). No-op when the theme container or color set is unwired.</summary>
+        protected void ApplyColorSetTint()
+        {
+            if (!TryResolveTintColors(out var bright, out var dull)) return;
+
             s_tintBlock ??= new MaterialPropertyBlock();
             foreach (var modelData in crystalModels)
             {
@@ -170,6 +186,53 @@ namespace CosmicShore.Gameplay
             }
         }
 
+        /// <summary>
+        /// Paints one frame of a CAPTURE: the crystal's own colours scaled by
+        /// <paramref name="flareMultiplier"/> (a brightness flare in linear HDR - the element's hue
+        /// survives, per Docs/PALETTE.md, where washing toward white would read as a different
+        /// crystal) and its opacity driven to <paramref name="opacity01"/> for the dissolve that
+        /// ends the absorb.
```

</details>

### `6add5d6d8` — fix(ecology): a living lifeform's heart is blue, not lime

_Claude, 2026-08-15 03:26:17 +0000_

```text
A crystal's element is its shape and its colour is who may collect it:
Crystal.ApplyColorSetTint paints a domain crystal in its domain's pair, an
embedded lifeform heart in BlueColors (alive, uncollectable) and a free
pickup in EnvironmentColors' lime CTA. That rule was already written; it
just never reached the screen.

FadeIn drives _opacity through the SAME per-renderer MaterialPropertyBlock,
wrote its own instance every frame and ended the bloom with
MaterialPropertyBlock.Clear(). A block cannot drop a single key, so the
clear took the tint with it a few seconds after every crystal spawned and
the crystal fell back to its authored MATERIAL. Mass and Space author the
Blue material and looked right by accident; ChargeCrystalMaterial is
literally BrightCTA and Time's Fringe materials carry a lime dull face, so
the 21 Charge and 21 Time species advertised their living hearts as free
pickups.

- FadeIn merges into the block on every write instead of overwriting it,
  and raises FadeCompleted after its closing Clear so co-owners can restore
  what the clear took. The read-back is paid only when someone subscribed,
  so uninvolved renderers keep the cheap write path.
- Crystal claims that co-ownership in Awake (only when a theme is wired and
  a tint can resolve) and re-asserts the tint when a model's bloom ends.
- ActivateCrystal applies the tint itself, so the blue -> lime pickup
  affordance no longer depends on Start firing (which happens only because
  a heart's Crystal component is authored disabled) or on the material
  lerp's tail (skipped outright when a model has no target material).

Domain crystals are unchanged: ThemeManager paints the team crystal
materials from the same BrightCrystalColor/DullCrystalColor pair the tint
writes. No colliders, no spatial queries, no change to edibility or
steering.

Docs: ECOSYSTEM.md section 30, PALETTE.md section 2.2, and CLAUDE.md's
crystal row, which carried this as a known unfixed defect.
```

```text
 Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs | 40 +++++++++++++++++++++++++++++
 Assets/_Scripts/Utility/Effects/FadeIn.cs                   | 51 ++++++++++++++++++++++++++++++++++---
 CLAUDE.md                                                   |  2 +-
 Docs/ECOSYSTEM.md                                           | 58 +++++++++++++++++++++++++++++++++++++++++++
 Docs/PALETTE.md                                             | 38 ++++++++++++++++++++++++++++
 5 files changed, 184 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 279 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs b/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
index a05beb9d1..3858b3181 100644
--- a/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
+++ b/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
@@ -99,6 +99,38 @@ namespace CosmicShore.Gameplay
         [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
         static void ResetRegistry() => s_active.Clear();
 
+        // The models' bloom (FadeIn) writes the SAME per-renderer property block the
+        // collectability tint lives on, and clears it when it finishes - a block cannot drop a
+        // single key. So claim co-ownership and re-assert the tint on the frame the bloom ends.
+        // Without this every crystal settled onto its authored MATERIAL colour a few seconds
+        // after spawning, which is why a living lifeform's heart read as the lime free-pickup
+        // (Charge and Time author lime materials) instead of the neutral blue it resolves to.
+        readonly List<FadeIn> _tintCoOwnedFades = new();
+
+        protected virtual void Awake()
+        {
+            // Only claim the block when a tint can actually be resolved - an unthemed crystal
+            // writes nothing, and co-ownership costs FadeIn a read-back per frame.
+            if (!_themeManagerData || crystalModels == null) return;
+
+            foreach (var modelData in crystalModels)
+            {
+                if (modelData?.model == null) continue;
+                if (!modelData.model.TryGetComponent<FadeIn>(out var fade)) continue;
+                fade.FadeCompleted += HandleModelFadeCompleted;
+                _tintCoOwnedFades.Add(fade);
+            }
+        }
+
+        protected virtual void OnDestroy()
+        {
+            foreach (var fade in _tintCoOwnedFades)
+                if (fade) fade.FadeCompleted -= HandleModelFadeCompleted;
+            _tintCoOwnedFades.Clear();
+        }
+
+        void HandleModelFadeCompleted() => ApplyColorSetTint();
+
         protected virtual void OnEnable()
         {
             if (!s_active.Contains(this)) s_active.Add(this);
@@ -351,6 +383,14 @@ namespace CosmicShore.Gameplay
                 model.GetComponent<Renderer>().material = modelData.inactiveMaterial;
                 StartCoroutine(LerpCrystalMaterialCoroutine(model, ResolveActivationMaterial(modelData, i)));
             }
+
+            // The state just flipped from "living heart" to "collectible", so repaint NOW - after
+            // the lerps above, which drop the block on their way in. A lifeform's heart goes blue
+            // → lime here, which is the signal that it can be picked up. Explicit rather than left
+            // to Start (which only fires because a heart's Crystal component is authored disabled)
+            // or to the material lerp's tail (which is skipped outright when a model has no target
+            // material) - either way the crystal would otherwise stay blue while collectable.
+            ApplyColorSetTint();
         }
 
         /// <summary>
diff --git a/Assets/_Scripts/Utility/Effects/FadeIn.cs b/Assets/_Scripts/Utility/Effects/FadeIn.cs
index 85cb4763c..158646f92 100644
--- a/Assets/_Scripts/Utility/Effects/FadeIn.cs
+++ b/Assets/_Scripts/Utility/Effects/FadeIn.cs
@@ -1,3 +1,4 @@
+using System;
 using System.Collections;
 using UnityEngine;
 
@@ -8,6 +9,17 @@ namespace CosmicShore.Utility
     /// MaterialPropertyBlock — no material clone, no per-frame GetComponent.
     /// The override is cleared once the fade completes so material swaps
     /// (crystal activation, domain changes) always show their authored opacity.
+    ///
+    /// This component owns exactly ONE property (_opacity), but a property block belongs to the
+    /// RENDERER, not to the component that writes it — so it must never assume the block is its
+    /// own. It merges into whatever is already there on every write, and raises
+    /// <see cref="FadeCompleted"/> after the clear so a co-owner can put its overrides back
+    /// (MaterialPropertyBlock cannot drop a single key, so Clear takes the whole block with it).
+    ///
+    /// Crystal.ApplyColorSetTint is the co-owner that made this necessary. Before the merge, the
+    /// bloom wiped every crystal's collectability tint — a living lifeform's heart is blue, a free
```

</details>

### `c75dd0ae5` — feat(crystals): put all five crystals in the CTA lime and make the omni the hero

_Claude, 2026-08-15 03:26:26 +0000_

```text
The omni now reads as the brightest object among the crystals, and all five wear
one lime family. Three parts, in order of how much they matter:

1. FadeIn was ERASING the CTA lime on every crystal in the game. It owned a
   private MaterialPropertyBlock which it pushed wholesale (wiping Crystal's
   tint at the start of the fade) and Clear()ed on completion (wiping it
   permanently). Every crystal model carries a FadeIn - the omni included, via
   the nested TrucatedOctahedron.prefab - so no crystal has ever actually shown
   the lime: the omni read blue-white/green, Space blue-white, Time HDR blue,
   and only Charge was lime, because its shader's authored defaults happen to BE
   the CTA pair. FadeIn now read-modify-writes the block and retires the fade by
   restoring the material's own _opacity instead of clearing. The two writers are
   now order-independent. This also repairs the Dolphin's ghost-crystal preview
   (ApplyDomainPreview), which the same Clear() was undoing.

2. DarkCTA raised to the bloom ceiling: (0.18,0.32,0.05) x1.5625 ->
   (0.28125,0.5,0.078125). Every crystal shader composes lerp(dull, bright,
   (1-N.V)^4), and at that fresnel power the rim is 2.5% of the silhouette - so
   the DULL colour is ~93% of the crystal and therefore nearly all of its bloom.
   The gameplay Bloom clamp is 0.5, so bloom saturates at max channel 0.5: the
   new green channel lands exactly there. Pure scalar, so the hue is identical,
   and nothing reaches 1.0 (tonemapping is None, where >1.0 clips and shifts the
   lime toward yellow-white). BrightCTA is untouched - at 0.92 it was already
   saturating the clamp, so raising it buys nothing.

3. EnvironmentColorSet.ElementalCrystalDimming (0.45) scales the pair for the
   four elementals, leaving the omni at full strength: ~6.2:1 bloom. A scalar
   rather than a second colour pair, deliberately - it cannot move the hue, so
   "all five in one lime family" holds by construction, and it dims correctly
   whichever role each colour plays (the Time graph swaps body and rim). The
   omni and the Mass crystal share the same four materials, so this scalar is
   the only thing that tells them apart.

Docs/PALETTE.md gains section 2.2 with the measured bloom-response curve, the
dimming sensitivity table, and the shader-composition facts behind all of it.

Not done, and flagged in section 7: the gameplay Bloom clamp of 0.5 caps the
bloom source below most of the palette - 56 of 86 authored colours exceed it and
bloom identically, making section 3's "channels above 1.0 bloom" false as
shipped. Raising it is a whole-game relighting, not a crystal change.
```

```text
 Assets/_SO_Assets/Color Palettes/OriginalColorSetSO.asset   |  3 +-
 Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs | 12 ++++++
 Assets/_Scripts/ScriptableObjects/SO_ColorSet.cs            | 23 +++++++++++
 Assets/_Scripts/Utility/Effects/FadeIn.cs                   | 35 ++++++++++++-----
 Docs/PALETTE.md                                             | 90 +++++++++++++++++++++++++++++++++++++++++++
 5 files changed, 153 insertions(+), 10 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 237 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs b/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
index a05beb9d1..4cb69dd0d 100644
--- a/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
+++ b/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
@@ -8,6 +8,7 @@ using Reflex.Attributes;
 using Unity.Collections;
 using UnityEngine;
 using CosmicShore.Data;
+using CosmicShore.ScriptableObjects; // EnvironmentColorSetExtensions.ScaleRGB
 namespace CosmicShore.Gameplay
 {
     [System.Serializable]
@@ -152,6 +153,17 @@ namespace CosmicShore.Gameplay
                 if (colors.EnvironmentColors == null) return;
                 bright = colors.EnvironmentColors.BrightCTA;
                 dull = colors.EnvironmentColors.DarkCTA;
+
+                // The OMNI is the hero pickup and wears the CTA at full strength; the four
+                // elementals ride the same lime, dimmed, so the omni is the brightest crystal
+                // on screen. Brightness is the ONLY difference - a scalar cannot move the hue,
+                // so all five stay in one lime family by construction.
+                if (crystalProperties.IsElemental)
+                {
+                    float dim = colors.EnvironmentColors.ElementalCrystalDimming;
+                    bright = bright.ScaleRGB(dim);
+                    dull = dull.ScaleRGB(dim);
+                }
             }
 
             s_tintBlock ??= new MaterialPropertyBlock();
diff --git a/Assets/_Scripts/ScriptableObjects/SO_ColorSet.cs b/Assets/_Scripts/ScriptableObjects/SO_ColorSet.cs
index 2771d2ddf..5dca7d6c8 100644
--- a/Assets/_Scripts/ScriptableObjects/SO_ColorSet.cs
+++ b/Assets/_Scripts/ScriptableObjects/SO_ColorSet.cs
@@ -144,8 +144,31 @@ namespace CosmicShore.ScriptableObjects
         [ColorUsage(true, true)] [SerializeField] public Color SkyColor;
         [ColorUsage(true, true)] [SerializeField] public Color LightColor;
         [ColorUsage(true, true)] [SerializeField] public Color DarkColor;
+        [Tooltip("The free-pickup lime. DarkCTA paints the crystal BODY and BrightCTA only its " +
+                 "fresnel rim (Blend(Base=Dull, Blend=Bright, Opacity=(1-N.V)^4) in every crystal " +
+                 "shader), and that rim is ~2.5% of the silhouette - so DarkCTA is what ~93% of the " +
+                 "crystal, and therefore almost all of its BLOOM, is made of. Both are sized against " +
+                 "the gameplay Bloom CLAMP (0.5): bloom saturates at max channel 0.5, so DarkCTA's " +
+                 "max channel sits exactly there and pushing either higher buys nothing.")]
         [ColorUsage(true, true)] [SerializeField] public Color BrightCTA;
         [ColorUsage(true, true)] [SerializeField] public Color DarkCTA;
+
+        [Tooltip("Scales the CTA pair for the four ELEMENTAL crystals so the omni reads as the hero " +
+                 "pickup. A pure scalar, deliberately: it cannot move the hue, so all five crystals " +
+                 "stay in one lime family by construction - which a second authored colour pair " +
+                 "could silently break. It also dims correctly whichever role each colour plays " +
+                 "(TimeCrystalGraph swaps body and rim relative to the other graphs).")]
+        [Range(0f, 1f)] [SerializeField] public float ElementalCrystalDimming = 0.45f;
+
         [ColorUsage(true, true)] [SerializeField] public Color Danger;
     }
+
+    public static class EnvironmentColorSetExtensions
+    {
+        /// <summary>
+        /// Scales RGB and leaves ALPHA alone. Unity's Color operator* also scales alpha, which on a
+        /// crystal tint would quietly fade the mesh out instead of dimming it.
+        /// </summary>
+        public static Color ScaleRGB(this Color c, float k) => new(c.r * k, c.g * k, c.b * k, c.a);
+    }
 }
diff --git a/Assets/_Scripts/Utility/Effects/FadeIn.cs b/Assets/_Scripts/Utility/Effects/FadeIn.cs
index 85cb4763c..9cfe6f81f 100644
--- a/Assets/_Scripts/Utility/Effects/FadeIn.cs
+++ b/Assets/_Scripts/Utility/Effects/FadeIn.cs
@@ -6,8 +6,18 @@ namespace CosmicShore.Utility
     /// <summary>
     /// Fades a renderer in by driving its shader's _opacity through a
     /// MaterialPropertyBlock — no material clone, no per-frame GetComponent.
-    /// The override is cleared once the fade completes so material swaps
-    /// (crystal activation, domain changes) always show their authored opacity.
+    ///
+    /// Every write COMPOSES with the block already on the renderer instead of replacing it.
+    /// This is load-bearing, not tidiness: <see cref="CosmicShore.Gameplay.Crystal"/> paints the
+    /// free-pickup lime through a property block on these same renderers, and this component used
```

</details>

### `b3e7a9a9f` — fix(input): keyboard dual-WASD mix and gamepad ability bindings

_josephzhou124, 2026-08-14 23:32:03 -0400_

```text
 Assets/_Scripts/Controller/IO/DualStickMix.cs              | 65 +++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Controller/IO/DualStickMix.cs.meta         |  2 ++
 Assets/_Scripts/Controller/IO/InputController.cs           | 13 ++++++--
 Assets/_Scripts/Controller/Vessel/R_VesselActionHandler.cs |  8 +++--
 Assets/_Scripts/Tests/Editor/DualStickMixTests.cs          | 76 ++++++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Tests/Editor/DualStickMixTests.cs.meta     |  2 ++
 6 files changed, 160 insertions(+), 6 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 223 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/IO/DualStickMix.cs b/Assets/_Scripts/Controller/IO/DualStickMix.cs
new file mode 100644
index 000000000..343497958
--- /dev/null
+++ b/Assets/_Scripts/Controller/IO/DualStickMix.cs
@@ -0,0 +1,65 @@
+using UnityEngine;
+
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// Dual-stick mix shared by keyboard (and matching <see cref="GamepadInputStrategy.Reparameterize"/>).
+    /// XSum = yaw, YSum = pitch, XDiff = speed, YDiff = roll.
+    /// InvertY / InvertThrottle apply after the mix.
+    /// </summary>
+    public readonly struct DualStickMixResult
+    {
+        public readonly float XSum;
+        public readonly float YSum;
+        public readonly float XDiff;
+        public readonly float YDiff;
+        public readonly Vector2 EasedLeft;
+        public readonly Vector2 EasedRight;
+
+        public DualStickMixResult(float xSum, float ySum, float xDiff, float yDiff,
+            Vector2 easedLeft, Vector2 easedRight)
+        {
+            XSum = xSum;
+            YSum = ySum;
+            XDiff = xDiff;
+            YDiff = yDiff;
+            EasedLeft = easedLeft;
+            EasedRight = easedRight;
+        }
+    }
+
+    public static class DualStickMix
+    {
+        const float PiOverFour = 0.785f;
+
+        public static float Ease(float input)
+        {
+            return input < 0
+                ? (Mathf.Cos(input * PiOverFour) - 1)
+                : -(Mathf.Cos(input * PiOverFour) - 1);
+        }
+
+        public static DualStickMixResult Mix(Vector2 left, Vector2 right,
+            bool invertY = false, bool invertThrottle = false)
+        {
+            float xSum = Ease(right.x + left.x);
+            float ySum = -Ease(right.y + left.y);
+            float xDiff = (right.x - left.x + 2f) / 4f;
+            float yDiff = Ease(right.y - left.y);
+
+            if (invertY)
+            {
+                ySum *= -1f;
+                yDiff *= -1f;
+            }
+
+            if (invertThrottle)
+                xDiff = 1f - xDiff;
+
+            return new DualStickMixResult(
+                xSum, ySum, xDiff, yDiff,
+                new Vector2(Ease(2f * left.x), Ease(2f * left.y)),
+                new Vector2(Ease(2f * right.x), Ease(2f * right.y)));
+        }
+    }
+}
diff --git a/Assets/_Scripts/Controller/IO/InputController.cs b/Assets/_Scripts/Controller/IO/InputController.cs
index c3310b423..a9e6b449e 100644
--- a/Assets/_Scripts/Controller/IO/InputController.cs
+++ b/Assets/_Scripts/Controller/IO/InputController.cs
@@ -20,6 +20,7 @@ namespace CosmicShore.Gameplay
         [SerializeField] public bool Portrait;
         [Inject] GameSetting gameSetting;
         IVessel vessel;
+        Player ownerPlayer;
```

</details>

### `4dc8bd012` — feat(rhino): five hairline blade tracers, lower sword mount, swipe recovery

_Claude, 2026-08-15 03:40:42 +0000_

```text
- TRACERS: the single tracer becomes an array of five hairline TrailRenderers
  (RhinoSwordBladeTracer0..4, width 0.5 / time 0.15) spread evenly down the
  blade, element 0 on the tip and the last on the hilt, so a swing draws a comb
  of fine streaks. Their whole look stays authored on the components - the count
  is data too, since the spread is derived from the array length. SeatTracers
  owns placement only, insetting the two END streaks by half their own head
  width so widening one grows it into the blade rather than past the point. All
  five tint from the live blade colour, so they change state with the sword.

- HOME POSITION: the sword mount drops from local y 9.38 to 2. The hull box
  spans y -8.9..+6.4 (centred -1.2), so 9.38 perched the grip ~3 units above the
  hull top; 2 holds it alongside. Recorded in the swipe doc that on a 60-240
  unit blade the rest PITCH is the stronger lever for the towers-overhead read.

- SWIPE RECOVERY: each direction owes swipeCooldownSeconds (0.35) after it
  releases before it can sweep again, so the sword swings with a rhythm instead
  of flapping; ZERO while energized, and the timers clear as it burns so leaving
  energized never inherits a recovery. It suppresses the DIFFERENCE axis only,
  applied after the stance is fed from the raw trigger mirrors - so the chop and
  the energize ritual are never blocked, and the blade keeps cutting everything
  it touches throughout. This is NOT the rejected v1 slash cooldown, which gated
  DAMAGE; both docs carry the distinction in a table so it cannot be misread as
  the rejection being reversed.
```

```text
 Assets/_Prefabs/Spacevessels/Rhino.prefab                             | 599 +++++++++++++++++++++++++++++++-
 Assets/_SO_Assets/VesselActions/Rhino/RhinoShieldSwipeConfig.asset    |   2 +
 .../R_VesselActions/Data Containers/RhinoShieldSwipeConfigSO.cs       |  14 +
 .../Vessel/R_VesselActions/Executors/RhinoSwordFXController.cs        | 138 +++++---
 .../Vessel/R_VesselActions/Executors/ShieldSwipeActionExecutor.cs     |  58 ++++
 .../_Scripts/Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md  |  80 +++--
 .../_Scripts/Controller/Vessel/R_VesselActions/RHINO_SHIELD_SWIPE.md  |  16 +-
 Docs/UNITY_VERIFICATION_CHECKLIST.md                                  |  25 +-
 8 files changed, 843 insertions(+), 89 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 509 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordFXController.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordFXController.cs
index d2e3fc9f0..b251adeec 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordFXController.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordFXController.cs
@@ -26,13 +26,14 @@ namespace CosmicShore.Gameplay
     ///     crackle spark at the exact blade point that made contact
     ///     (<see cref="SkimmerSwingKinematics.ClosestBladePoint"/>); a dim DENIED spark
     ///     when a non-energized blade bounces off a super-shielded prism.
-    ///  4. TIP TRACER — ONE authored TrailRenderer (fuselage-parented in the prefab so the
-    ///     streak's shape never inherits the blade's scale). Its whole SHAPE is authored on the
-    ///     component — width, time, taper curve, gradient, material — and this controller never
-    ///     writes any of it; all it does is PLACE the emitter half a head-width back down the
-    ///     blade so the band's top edge lands on the sword's tip at whatever width is dialled
-    ///     in. Tinted with the live blade colour via MaterialPropertyBlock, so the streak
-    ///     changes with the sword through every state.
+    ///  4. BLADE TRACERS — a comb of hairline authored TrailRenderers spread evenly down the
+    ///     blade, tip (element 0) to hilt (fuselage-parented in the prefab so their shape never
+    ///     inherits the blade's scale). Their whole SHAPE is authored on the components — width,
+    ///     time, taper curve, gradient, material — and this controller never writes any of it;
+    ///     all it does is PLACE each emitter, insetting the two end streaks by half their own
+    ///     head width so widening one grows it into the blade rather than past the point. All
+    ///     tinted with the live blade colour via MaterialPropertyBlock, so the streaks change
+    ///     with the sword through every state.
     ///
     /// Camera shake (super-shield pop, crystal burst) fires for the LOCAL human pilot
     /// only. See <c>RHINO_ENERGY_SWORD.md</c>.
@@ -52,11 +53,12 @@ namespace CosmicShore.Gameplay
         [SerializeField] private MeshRenderer bodyRenderer;
         [Tooltip("The crackle overlay driver on the blade (capsule surface mode). Falls back to this GameObject's controller.")]
         [SerializeField] private ForcefieldCrackleController crackle;
-        [Tooltip("The sword's tip streak. A fuselage child (so the blade's scale never distorts it). " +
-                 "TUNE IT ON THE COMPONENT — width, time, taper curve, gradient and material are all " +
-                 "yours and nothing here overwrites them; this only re-seats the emitter each frame so " +
-                 "the top edge of the band stays on the blade's tip at whatever width you set.")]
-        [SerializeField] private TrailRenderer bladeTracer;
+        [Tooltip("The sword's streaks — hairline TrailRenderers spread evenly down the blade, tip " +
+                 "(element 0) to hilt (last). Fuselage children, so the blade's scale never distorts " +
+                 "them. TUNE THEM ON THE COMPONENTS — width, time, taper curve, gradient and material " +
+                 "are all yours and nothing here overwrites them; this only re-seats each emitter " +
+                 "every frame. Add or remove entries freely: the spread is derived from the count.")]
+        [SerializeField] private TrailRenderer[] bladeTracers;
 
         Skimmer _skimmer;
         SkimmerSwingKinematics _swing;
@@ -68,8 +70,8 @@ namespace CosmicShore.Gameplay
         bool _bodyColorApplied;
         Color _appliedTracerColor;
         bool _tracerColorApplied;
-        float _appliedTracerWidth = float.NaN;   // last widthMultiplier seen, for the curve re-read
-        float _tracerHeadWidthFactor = 1f;       // authored width curve evaluated at the emitting end
+        float[] _appliedTracerWidths;      // last widthMultiplier seen per tracer, for the curve re-read
+        float[] _tracerHeadWidthFactors;   // authored width curve evaluated at each emitting end
 
         float _flash;             // 0 = none, 1 = full white-out; decays each Tick
         float _energizedBlend;    // 0 = heat ramp, 1 = white-hot; eased by ColorTransitionSeconds
@@ -97,8 +99,8 @@ namespace CosmicShore.Gameplay
             _bodyColorApplied = false;
             _tracerColorApplied = false;
 
-            SeatTracer();
-            if (bladeTracer) bladeTracer.Clear();
+            SeatTracers();
+            ForEachTracer(tr => tr.Clear());
 
             WarnOnceIfUnwired();
         }
@@ -107,7 +109,7 @@ namespace CosmicShore.Gameplay
         {
             // Drop the per-renderer overrides so the shared materials show through again.
             if (bodyRenderer) bodyRenderer.SetPropertyBlock(null);
-            if (bladeTracer) bladeTracer.SetPropertyBlock(null);
+            ForEachTracer(tr => tr.SetPropertyBlock(null));
             _bodyColorApplied = false;
             _tracerColorApplied = false;
             _flash = 0f;
@@ -151,7 +153,7 @@ namespace CosmicShore.Gameplay
                 color = Color.Lerp(color, config.FlashColor, _flash);
 
             ApplyBodyColor(color);
-            SeatTracer();
```

</details>

### `9e1484e82` — docs(input): document dual-WASD KeyboardInputStrategy as desktop default

_josephzhou124, 2026-08-14 23:41:45 -0400_

```text
Update the Input Strategy Pattern section to reflect the new
KeyboardInputStrategy (dual-WASD, desktop default) + DualStickMix shared
mix, the DualMouseInputStrategy, the now-unused legacy
KeyboardMouseInputStrategy, and the IsLocalPilot flight-input gate.

Co-authored-by: Cursor <cursoragent@cursor.com>
```

```text
 CLAUDE.md | 4 ++--
 1 file changed, 2 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/CLAUDE.md b/CLAUDE.md
index ee16eacc8..443a2a09f 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -932,8 +932,8 @@ Platform-agnostic input via `Assets/_Scripts/Controller/IO/`:
 
 - `IInputStrategy` — interface for all input handlers
 - `BaseInputStrategy` — shared logic
-- `KeyboardMouseInputStrategy`, `GamepadInputStrategy`, `TouchInputStrategy` — platform-specific implementations
-- `InputController` — manages active strategy and input state
+- `GamepadInputStrategy`, `TouchInputStrategy`, `KeyboardInputStrategy` (dual-WASD, the **desktop default**), `DualMouseInputStrategy` (opt-in two-mice flight) — platform-specific implementations. `KeyboardInputStrategy` maps two digital "sticks" (WASD left, P/;/L/' right; Left/Right Shift = the two triggers) and mixes them through `DualStickMix` (the yaw/pitch/speed/roll formulas shared with — and unit-tested against — `GamepadInputStrategy.Reparameterize`). The legacy `KeyboardMouseInputStrategy` remains in the project but is no longer selected.
+- `InputController` — manages active strategy and input state. Flight input is gated on `Player.IsLocalPilot` (AI and remote `Player` replicas carry an `InputController` but must not consume local WASD/sticks).
 - `IInputStatus` / `InputStatus` — input state container
 - Input strategies are swappable per platform/context at runtime
 
```

</details>

### `26c4b88ce` — feat(prisms): super-shielded prisms jiggle when hit but not destroyed

_Claude, 2026-08-15 03:49:25 +0000_

```text
Super-shielded mass is fully invulnerable, so every hit on it was visually
silent: the impactor's sparks and SFX fired, the mass did not move, and a
deflection read as the shot having missed. This is the deflection made legible.

Each FACE now wobbles about the prism's object origin on an axis that PRECESSES
about that face's own normal and NUTATES (the cone half-angle breathes), at
deliberately non-commensurate rates, decaying to exactly zero at Duration. The
stella's outer spikes wag far while the core barely moves, which is what reads as
jiggly rather than as a rigid nudge.

It runs entirely on the GPU off the prism clock, per the clock-material law: one
stamp of initial conditions at the hit, no CPU writes while it plays, one
scheduled clear at the analytically-known end. It is a per-instance STAMP, not
the global-uniform shape used by the occlusion corridor and the echo sight —
those are view-dependent, a deflection is not.

Notable properties:
- Per-face and per-prism randomness is derived on the GPU from the face normal
  and the object-to-world translation. No seed is stamped and no mesh channel is
  authored, which matters because the super-shield stella carries neither
  tangents nor UVs — the tangent basis is built from the normal alone.
- The rotation runs in the locally-isotropic frame, so a long thin trail slab
  wobbles rigidly instead of shearing along its long axis; the normal rides the
  same frame inverted (inverse transpose), verified against the true geometric
  normal of the rotated face.
- On ExplodingBlockGraph it composes with the shatter rotation rather than
  replacing it, and on both graphs the grow bloom and ballistic flight still
  apply on top.

Gameplay is untouched — no collider, volume, spatial, shell, domain or state-flag
change — which is why it is safe to fire from inside an invulnerability gate.

Also collapses four independent copies of the IsSuperShielded early-return
(Prism.Damage, Prism.Consume, PrismSpatialIndex.ResolveExplosionHit,
ExplosionImpactor.ExecuteCommonPrismCommands) into one
Prism.AbsorbSuperShieldHit. A per-call-site copy is a rule you can forget to
apply at the next damage source.

Graph surgery is out-of-editor and idempotent
(Tools/Shaders/wire_prism_jiggle_clock.py, validate-before-write); its output is
in this commit. Gated by PrismClockWiringValidator, PrismSuperShieldJiggleTests
(CPU/GPU count-match, pool hygiene, amplitude clamp) and a clang harness over the
shipped HLSL that also measured the culling envelope (peak displacement
0.991 x radius x amplitude). Playtest steps: Docs/PRISM_CLOCK_WIRING_CHECKLIST.md
Phase 9. Design: Docs/PRISM_ANIMATION.md 4.8 / tracker C14.
```

```text
 Assets/Resources/PrismSuperShieldJiggleConfig.asset                   |  22 +
 Assets/Resources/PrismSuperShieldJiggleConfig.asset.meta              |   8 +
 Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph              | 709 +++++++++++++++++++++++++++++++-
 Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph     | 638 +++++++++++++++++++++++++++-
 Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl            | 157 +++++++
 Assets/_Scripts/Controller/ECS/Rendering/PrismRenderProperties.cs     |  34 ++
 Assets/_Scripts/Controller/ECS/Rendering/PrismRenderService.cs        |  44 ++
 .../_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs  |   9 +-
 Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs              |   8 +
 Assets/_Scripts/Controller/Vessel/Prism.cs                            |  45 +-
 Assets/_Scripts/Controller/Vessel/PrismSuperShieldJiggle.cs           | 192 +++++++++
 Assets/_Scripts/Controller/Vessel/PrismSuperShieldJiggle.cs.meta      |  11 +
 Assets/_Scripts/Editor/PrismClockGraphWirer.cs                        |  10 +
 Assets/_Scripts/Editor/PrismClockWiringValidator.cs                   |  14 +-
 Assets/_Scripts/ScriptableObjects/PrismSuperShieldJiggleConfigSO.cs   | 109 +++++
 .../_Scripts/ScriptableObjects/PrismSuperShieldJiggleConfigSO.cs.meta |  11 +
 Assets/_Scripts/Tests/Editor/PrismSuperShieldJiggleTests.cs           | 221 ++++++++++
 Assets/_Scripts/Tests/Editor/PrismSuperShieldJiggleTests.cs.meta      |  11 +
 CLAUDE.md                                                             |   2 +-
 Docs/PRISM_ANIMATION.md                                               |  84 ++++
 Docs/PRISM_CLOCK_WIRING_CHECKLIST.md                                  |  60 +++
 Tools/Shaders/wire_prism_jiggle_clock.py                              | 580 ++++++++++++++++++++++++++
 22 files changed, 2959 insertions(+), 20 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1777 lines)</summary>

```diff
diff --git a/Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl b/Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl
index ea64bcdd3..77f6f846b 100644
--- a/Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl
+++ b/Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl
@@ -200,6 +200,163 @@ void PrismFlightClock_float(float Clock, float StartTime, float Duration, float3
 #endif
 }
 
+// -----------------------------------------------------------------------------
+// Super-shield deflection jiggle (BlockGraph + ExplodingBlockGraph vertex stage).
+// Docs/PRISM_ANIMATION.md §5 C14 — a super-shielded prism that is HIT but not
+// destroyed (Prism.AbsorbSuperShieldHit) wobbles and settles.
+//
+// Super-shielded mass is fully invulnerable, so every hit on it was silent: the
+// impactor's sparks fired, the prism did not move, and the deflection read as the
+// shot missing. This is the deflection made visible, and it is animation in the
+// §1 sense — a pure function of the clock and the stamped hit conditions, so it
+// is a per-instance STAMP, not a global uniform (contrast PrismOcclusionFade /
+// PrismDestructionSight, which are view-dependent and therefore per-frame globals).
+//
+// The motion is a struck body's FREE PRECESSION, applied per FACE:
+//   * every face rotates about the prism's object ORIGIN, so the stella's outer
+//     spike tips wag far while the core barely moves — the "jiggly" read;
+//   * the rotation AXIS lies on a cone about that face's own normal. It PRECESSES
+//     around the normal at Params.y and NUTATES — the cone half-angle breathes
+//     0..PI/2 at Params.z — so the face alternates between an in-plane twist
+//     (axis ON the normal) and a maximum tip (axis in the face plane) while the
+//     tip direction sweeps. Rates are deliberately non-commensurate so the
+//     pattern never repeats inside one deflection;
+//   * the ANGLE is amplitude * envelope(t), and the envelope reaches EXACTLY zero
+//     at t = Duration — so the scheduled ClearJiggleStamp is invisible and a stamp
+//     that is never cleared is a permanent no-op rather than a stuck prism.
+//
+// Randomness needs NO mesh channel and NO extra stamped property. Prism meshes are
+// hard-edged (the box is 24 verts / 6 distinct normals; the super-shield stella is
+// 72 verts / 24 — StellatedOctahedronMeshGenerator splits per face for exactly this
+// reason), so the object-space NORMAL *is* the face id; the object-to-world
+// translation is a free per-prism seed, so neighbouring prisms in one blast do not
+// wobble in lockstep; and StartTime re-rolls every hit. This matters because the
+// stella carries no tangents and no UVs — the tangent basis is therefore built
+// branchlessly FROM THE NORMAL (Duff et al. 2017), never read from the vertex
+// stream, where it would be zero.
+//
+// Scale correction: prisms are non-uniformly scaled (a trail slab is long and
+// thin), and an object-space rotation seen through that scale is a shear that wags
+// the long axis far more than the others. The rotation is therefore done in the
+// locally-ISOTROPIC frame (position * objectScale), matching what the shatter's
+// RotateFacesAlongAxis subgraph does on ExplodingBlockGraph. The normal is carried
+// through the same frame inverted, because a normal transforms by the inverse
+// transpose — get this backwards and the fresnel rim slides the wrong way.
+//
+// Duration <= 0 -> unstamped: identity on both outputs, so every prism that has
+// never absorbed a super-shield hit renders byte-identically.
+// -----------------------------------------------------------------------------
+
+// Envelope decay across the deflection. The (1 - u) factor is what guarantees the
+// exact zero at u = 1; this only shapes how front-loaded the wobble is.
+#define PRISM_JIGGLE_DECAY 2.5
+// Maximum nutation half-angle. PI/2 lets the axis swing from the face normal all
+// the way into the face plane, which is the difference between "it twists" and
+// "it wobbles".
+#define PRISM_JIGGLE_CONE 1.5707963
+#define PRISM_JIGGLE_TAU 6.2831853
+
+// Hash 3 -> 1, [0,1). Dave Hoskins' hash13; used only for phase offsets, so its
+// distribution matters and its cryptographic quality does not.
+float PrismJiggleHash13(float3 p)
+{
+    p = frac(p * float3(0.1031, 0.1030, 0.0973));
+    p += dot(p, p.yzx + 33.33);
+    return frac((p.x + p.y) * p.z);
+}
+
+// Branchless orthonormal basis from a unit vector (Duff et al. 2017, "Building an
+// Orthonormal Basis, Revisited"). Stable across the whole sphere, including n.z
+// near -1 where the naive cross-product construction degenerates.
+void PrismJiggleBasis(float3 n, out float3 t, out float3 b)
+{
+    float s = n.z >= 0.0 ? 1.0 : -1.0;
+    float a = -1.0 / (s + n.z);
```

</details>

### `74d1b37a4` — chore: save local Unity material and Rhino prefab edits

_josephzhou124, 2026-08-15 00:01:48 -0400_

```text
 Assets/_Graphics/Materials/BlockMaterials/BlueBlockMateral.mat       | 15 +++++++++++++--
 Assets/_Graphics/Materials/BlockMaterials/ExplodingBlockMaterial.mat | 20 +++++++++++++++++---
 Assets/_Prefabs/Spacevessels/Rhino.prefab                            | 27 +++++++--------------------
 ProjectSettings/GvhProjectSettings.xml                               |  4 ++--
 4 files changed, 39 insertions(+), 27 deletions(-)
```

### `8a68ec152` — docs(dolphin): the crackle is meant to be a vessel's only skim visual

_Claude, 2026-08-15 04:08:48 +0000_

```text
Records the rule the beam removal follows, and the two loose ends it leaves.

CLAUDE.md's skim-feedback paragraph described the beam VFX as one of three
always-present signals. It is per-vessel wiring, and — more usefully — it is the
effect the crackle REPLACED, so a container holding both draws a beam to every
prism in the sphere on top of the crackle. Stated as the rule, with the Dolphin as
the vessel that now follows it and the Squirrel named as the open item rather than
the reference.

Backlog 21: decide the Squirrel's doubled skim visual explicitly — retire the beam
fleet-wide, or drop the [Obsolete] and say the two compose.

Backlog 22: the Dolphin prefab keeps three dead prefab-instance overrides on its
inactive nested legacy Skimmer.prefab, writing a skimmerPrismEffectsSO field that
is commented out on SkimmerImpactor. One still references the beam asset, so a GUID
sweep finds SkimmerFXPrismEffect in Dolphin.prefab even though the container no
longer asks for it. Dead on three counts — commented-out field, inactive GameObject,
_nearFieldSkimmer points at EnergySkimmer — but it is a trap for the next grep.
```

```text
 CLAUDE.md                              | 13 +++++++++----
 Docs/ElementalAbilitySystem/BACKLOG.md | 21 +++++++++++++++++++++
 2 files changed, 30 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/CLAUDE.md b/CLAUDE.md
index 408e966bb..78c496d5f 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -960,10 +960,15 @@ legacy nested `Skimmer.prefab` holding the reference. **Audit it, don't infer it
 `FrogletTools > Vessels > Audit Vessel Skimmers` checks assignment, active state up the whole
 ancestor chain, the components the trigger path needs, and whether the container holds any
 prism effects — asset-only, no play mode. *(Serpent currently fails it.)* Note that a skim's
-three feedback signals are each individually invisible — the haptic is a **no-op on desktop**,
-the beam VFX only draws when the skimmed prism authors a `ParticleEffect`, and a gauge that
-moves a tenth of its range per skim reads as nothing — so "I feel no skimming" is not evidence
-about the wiring in either direction. The forcefield crackle needs **three** pieces to be
+feedback signals are each individually invisible — the haptic is a **no-op on desktop**,
+the legacy beam VFX (`SkimmerFXPrismEffectSO`, `[Obsolete]`) is per-vessel wiring that only
+draws when the container asks for it AND the skimmed prism authors a `ParticleEffect`, and a
+gauge that moves a tenth of its range per skim reads as nothing — so "I feel no skimming" is
+not evidence about the wiring in either direction. **The crackle is meant to be a vessel's ONLY
+skim visual**: the beam is the effect it replaced, so a container holding both draws a beam to
+every prism in the sphere *on top of* the crackle. The Dolphin ran both for three hours of
+branch history and now wires the crackle alone; the Squirrel still carries both, which is the
+open item, not the reference. The forcefield crackle needs **three** pieces to be
 present or `SkimmerForcefieldCracklePrismEffectSO.Execute` returns silently: the effect in the
 container, a `ForcefieldCrackleController` on the impactor's own GameObject, and an overlay
 `MeshRenderer` assigned to it (vessels whose skimmer IS `Skimmer.prefab` get the last two free;
diff --git a/Docs/ElementalAbilitySystem/BACKLOG.md b/Docs/ElementalAbilitySystem/BACKLOG.md
index 32412fefa..1444135b9 100644
--- a/Docs/ElementalAbilitySystem/BACKLOG.md
+++ b/Docs/ElementalAbilitySystem/BACKLOG.md
@@ -216,3 +216,24 @@ Mechanics reference: `_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_ENERGY_
 20. **The Dolphin's speed retune has not been flown.** 60 → 78 cruise and 210 → 357 boost are
     arithmetic, not feel. 357 is a large jump and the speed tunnel amplifies how it reads — expect
     a balancing pass. Steps + knob table: `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
+
+## Skim-visual follow-ups (opened by `claude/dolphin-skim-effect-7sd2w1`)
+
+21. **The Squirrel still runs BOTH skim visuals.** `SquirrelSkimmerImpactorDataContainer` holds
+    `SkimmerFXPrismEffect` (the `[Obsolete]` per-prism beam) *and*
+    `SkimmerForcefieldCracklePrismEffect` (its replacement) — the same doubled state the Dolphin
+    was just cleaned out of. It was left alone deliberately: the Dolphin's removal was a
+    playtest call on one vessel, and the Squirrel's beam may be reading as intentional on a
+    vessel whose whole loop is trail-riding. Decide it explicitly — either retire the beam
+    fleet-wide and delete `SkimmerFXPrismEffectSO` with it, or state in the SO's summary that
+    the two are meant to compose and drop the `[Obsolete]`. Do not leave it as an accident.
+22. **The Dolphin prefab carries three DEAD prefab-instance overrides** on its inactive nested
+    legacy `Skimmer.prefab` instance (`m_IsActive: 0`), writing
+    `skimmerPrismEffectsSO.Array.{size,data[0..2]}` — a field that is **commented out** on
+    `SkimmerImpactor`, so Unity retains the modifications forever without ever resolving them
+    (the same never-pruned-override pattern CLAUDE.md documents for `Cell`). One of them still
+    references the beam asset, which is why a GUID sweep finds `SkimmerFXPrismEffect` in
+    `Dolphin.prefab` after the container was cleaned. Harmless on three independent counts
+    (dead field, inactive GameObject, `_nearFieldSkimmer` points at `EnergySkimmer`) — but it is
+    a false positive for the next person who greps. Sweep it with the dead-override tooling
+    rather than by hand-editing prefab YAML.
```

</details>

### `32f65b5ab` — docs(skills): asset-surgery trap for dead prefab-instance overrides

_Claude, 2026-08-15 04:09:37 +0000_

```text
A GUID sweep after unwiring an asset kept reporting Dolphin.prefab as a live
consumer of the effect that had just been removed from its container. The hit was
three prefab-instance overrides writing skimmerPrismEffectsSO — a field commented
out on SkimmerImpactor — on an inactive nested legacy Skimmer.prefab that nothing
references. Unity never prunes an unresolvable m_Modification, so those entries
outlive the field by years and read as wiring.

Records the three checks that settle it (field still exists? instance active?
anything pointing at it?), notes that any one being alive makes the path real, and
says to backlog the sweep rather than hand-edit override entries out of prefab YAML.
```

```text
 .claude/skills/asset-surgery/SKILL.md | 16 ++++++++++++++++
 1 file changed, 16 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/.claude/skills/asset-surgery/SKILL.md b/.claude/skills/asset-surgery/SKILL.md
index 0d2b34dd8..b1acdc2bf 100644
--- a/.claude/skills/asset-surgery/SKILL.md
+++ b/.claude/skills/asset-surgery/SKILL.md
@@ -1121,6 +1121,22 @@ that would otherwise cost a round-trip to a human at the editor:
   first (blob-filtered, so hundreds of branches of a Unity repo land in ~a minute).
   Then dedupe by BLOB: `git rev-parse "$ref:$path"` per ref and group — N branches
   usually collapse to a handful of distinct file versions worth reading.
+- **A leftover GUID hit after you remove a reference is usually a DEAD prefab-instance
+  override, and proving it dead takes three checks, not one.** Unity never prunes an
+  `m_Modification` whose `propertyPath` no longer resolves, so a prefab keeps writing a field
+  that was commented out years ago — and a `grep -rl "guid: <x>"` sweep reports the vessel
+  prefab as a live consumer of an asset you just unwired. Do not conclude "there is a second
+  wiring path" (nor "it's fine, it's just an override") until you have checked all three, since
+  any ONE of them being alive makes it real: (1) **does the field still exist?** — grep the
+  target script for the `propertyPath`'s root; a `// [SerializeField]` line means the override
+  can never deserialize; (2) **is the instance active?** — find the `PrefabInstance` block
+  carrying that guid and read its `m_IsActive` override; (3) **does anything point at it?** —
+  for a component the engine reaches through a reference (skimmers via
+  `VesselStatus.NearFieldSkimmer`), resolve that fileID and check it is this instance. Cost
+  here: `SkimmerFXPrismEffect` kept showing up in `Dolphin.prefab` after its container was
+  cleaned; all three answers were dead (commented-out `skimmerPrismEffectsSO`, `m_IsActive: 0`,
+  `_nearFieldSkimmer` → the *other* skimmer). Record the finding in a backlog rather than
+  hand-editing prefab YAML to remove override entries — the sweep is what tooling is for.
 
 ### Bundled tool: `field_parity.py`
 
```

</details>

### `7d09e769e` — feat(ecology): the heart's blue -> lime crossing travels on the prism clock

_Claude, 2026-08-15 04:39:29 +0000_

```text
The colour change at ActivateCrystal snapped in a single frame. It now runs
the same shape as a prism domain change
(MaterialPropertyAnimator.ClockColorTransition, Docs/PRISM_ANIMATION.md):

- state final at the start (the crystal is collectable the instant it drops;
  colour is only how it reads),
- the start pair stamped ONCE against PrismClock,
- every pair in between computed analytically from that stamp rather than
  accumulated, on the same smoothstep the prism lerp uses,
- PrismTimerManager fires ONE settle at the analytically-known end, so the
  final colour lands even if the driver is interrupted,
- an interruption re-stamps from the analytic current, so a second state
  change mid-fade departs from what is actually on screen.

Duration is Crystal.colorTransitionSeconds, 0.8s to match the prism
transition; 0 snaps.

Two ordering rules this depends on:
- ActivateCrystal captures the displayed pair on its FIRST line. Clearing
  EmbeddedIn changes what the state resolves to and each material lerp drops
  the block on its way in, so reading it later would depart from the wrong
  colour - the same constraint MaterialPropertyAnimator has when it reads
  start colours before binding the end-state material.
- ClearColorSetTint now forgets the resting pair: after a clear the block no
  longer describes the screen, and a later cross-fade would otherwise start
  from a colour that has not been displayed since.

ApplyColorSetTint is now the re-assert path only (birth, a bloom finishing, a
material lerp settling) and no longer snaps a live transition to its end - it
re-writes what is on screen instead, since a caller is often there precisely
because the block was cleared out from under it.

One deliberate difference from the prism path: a prism hands the
interpolation to the GPU because thousands animate at once and its graphs
carry the clock wiring. The five crystal shaders (four of them 75-270 node
graphs) carry none, so the pair is pushed from the CPU - bounded by the
crystals actually transitioning, and cheaper than the cloned-material lerp it
runs alongside.
```

```text
 Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs | 240 ++++++++++++++++++++++++++++++++++++++----
 Docs/ECOSYSTEM.md                                           |  11 +-
 Docs/PALETTE.md                                             |  28 ++++-
 3 files changed, 256 insertions(+), 23 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 376 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs b/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
index 3858b3181..820e43789 100644
--- a/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
+++ b/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
@@ -40,6 +40,12 @@ namespace CosmicShore.Gameplay
         [SerializeField] protected bool allowVesselImpactEffect = true;
         [SerializeField] bool allowRespawnOnImpact;
 
+        [SerializeField]
+        [Tooltip("Seconds the collectability colour takes to cross-fade when this crystal's " +
+                 "state changes - a lifeform's heart going blue → lime as death drops it. " +
+                 "Matches the prism domain-change transition. 0 snaps.")]
+        float colorTransitionSeconds = 0.8f;
+
         [Header("Data Containers")]
         [SerializeField] protected ThemeManagerDataContainerSO _themeManagerData;
 
@@ -127,6 +133,7 @@ namespace CosmicShore.Gameplay
             foreach (var fade in _tintCoOwnedFades)
                 if (fade) fade.FadeCompleted -= HandleModelFadeCompleted;
             _tintCoOwnedFades.Clear();
+            PrismTimerManager.Instance?.CancelScheduledActions(this);
         }
 
         void HandleModelFadeCompleted() => ApplyColorSetTint();
@@ -139,6 +146,17 @@ namespace CosmicShore.Gameplay
         protected virtual void OnDisable()
         {
             s_active.Remove(this);
+
+            // Land the end pair rather than freezing part-way: the driver dies with the
+            // component and the scheduled settle is cancelled with it, so a crystal disabled
+            // mid-fade would otherwise keep a half-lerped colour when it comes back.
+            if (_tintTransitionActive)
+            {
+                var bright = _tintToBright;
+                var dull = _tintToDull;
+                StopTintTransition();
+                WriteTint(bright, dull);
+            }
         }
 
         protected virtual void Start()
@@ -157,34 +175,204 @@ namespace CosmicShore.Gameplay
 
         static MaterialPropertyBlock s_tintBlock;
 
-        /// <summary>Tints all crystal models from the theme ColorSet by collectability state
-        /// (see comment above). No-op when the theme container or color set is unwired.</summary>
-        protected void ApplyColorSetTint()
+        /// <summary>The (bright, dull) pair this crystal's CURRENT state resolves to. False when
+        /// the theme container or its color set is unwired - the caller keeps whatever it had.</summary>
+        bool TryResolveTintColors(out Color bright, out Color dull)
         {
-            if (!_themeManagerData || _themeManagerData.ColorSet == null) return;
+            bright = default;
+            dull = default;
+            if (!_themeManagerData || _themeManagerData.ColorSet == null) return false;
             var colors = _themeManagerData.ColorSet;
 
-            Color bright, dull;
             bool domainOwned = ownDomain is Domains.Jade or Domains.Ruby or Domains.Gold;
             if (domainOwned && colors.TryGetColorSetByDomain(ownDomain, out var domainSet) && domainSet != null)
             {
                 bright = domainSet.BrightCrystalColor;
                 dull = domainSet.DullCrystalColor;
+                return true;
             }
-            else if (IsEmbedded)
+
+            if (IsEmbedded)
             {
                 // A living lifeform's heart: the blue-white neutral range - no domain can take it.
-                if (!colors.TryGetColorSetByDomain(Domains.Blue, out var neutralSet) || neutralSet == null) return;
+                if (!colors.TryGetColorSetByDomain(Domains.Blue, out var neutralSet) || neutralSet == null) return false;
                 bright = neutralSet.BrightCrystalColor;
                 dull = neutralSet.DullCrystalColor;
+                return true;
             }
-            else
```

</details>

### `545ce7d3a` — docs(palette): route crystal + bloom-clamp findings, and capture the session's traps

_Claude, 2026-08-15 04:48:45 +0000_

```text
- CLAUDE.md's PALETTE row now names the crystal tier (dull is 93% of the
  crystal, bright is a 2.5% hairline) and the bloom-clamp finding, and its
  "read before" trigger covers *CTA fields and "trying to make anything glow
  harder" - that is the question that lands someone in this trap.
- PALETTE.md section 6 gains the crystal playtest steps, ordered with the check
  that has never passed before (are they lime at all?) first, and each failure
  mode paired with what to suspect.
- asset-surgery skill: four traps this branch paid for -
  (1) bloom is the third instance of the saturated-dial trap, with the URP
      prefilter math and the "area, not intensity" corollary;
  (2) find which property covers the AREA before tuning a colour - it is
      usually not the one named "Bright";
  (3) SetPropertyBlock REPLACES and Clear() discards everyone's overrides -
      the bug behind every crystal in the game showing the wrong colour;
  (4) a guid grep of a prefab cannot see components in its nested prefabs,
      which produced two confidently wrong conclusions before it was caught.
```

```text
 .claude/skills/asset-surgery/SKILL.md | 63 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++------
 CLAUDE.md                             |  2 +-
 Docs/PALETTE.md                       | 26 ++++++++++++++++++++++++++
 3 files changed, 84 insertions(+), 7 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 124 lines)</summary>

```diff
diff --git a/.claude/skills/asset-surgery/SKILL.md b/.claude/skills/asset-surgery/SKILL.md
index 0d2b34dd8..83fb8ce66 100644
--- a/.claude/skills/asset-surgery/SKILL.md
+++ b/.claude/skills/asset-surgery/SKILL.md
@@ -1034,12 +1034,63 @@ that would otherwise cost a round-trip to a human at the editor:
   try turning X up", trace X to the value the SCREEN reads and check every
   clamp in between. If the input already exceeds the ceiling, the dial is dead
   — turning it up changes literally nothing, and you will burn a play-test
-  round proving that. Cosmic Shore has bitten twice here (AOE blast `Inertia`
-  vs `PrismExplosion.maxSpeed` 33.33 with a ~222 u/s input; the hull ram vs the
-  same clamp's FLOOR). Symptom to recognize: every instance produces the
-  IDENTICAL magnitude regardless of cause. Fix by putting the path on a
-  true-velocity contract that supplies its own ceiling — never by widening the
-  shared clamp, which retunes every other consumer of it.
+  round proving that. Cosmic Shore has bitten **three** times here (AOE blast
+  `Inertia` vs `PrismExplosion.maxSpeed` 33.33 with a ~222 u/s input; the hull
+  ram vs the same clamp's FLOOR; and **BLOOM**, below). Symptom to recognize:
+  every instance produces the IDENTICAL magnitude regardless of cause. Fix by
+  putting the path on a true-velocity contract that supplies its own ceiling —
+  never by widening the shared clamp, which retunes every other consumer of it.
+- **"Make it glow more" is a CLAMP question first, an HDR-colour question second.**
+  URP's Bloom clamps the bloom SOURCE before thresholding, so the per-pixel bloom
+  contribution is flat above `clamp` and every colour above it blooms identically.
+  This project overrides `clamp` to **0.5** (URP's default is 65472) in the GamePlay
+  and Commander profiles, which makes 56 of the 86 colours in `OriginalColorSetSO`
+  — the danger rim at 1.498, AOE at 4.0 — bloom exactly the same, and makes
+  `Docs/PALETTE.md` §3's "channels above 1.0 bloom" false as shipped. **Read the
+  volume profile's `threshold`/`knee`/`clamp` and compute the response curve BEFORE
+  authoring any HDR value**; URP's prefilter is
+  `c=min(clamp,c); B=max3(c); soft=clip(B-thr+knee,0,2knee)²/(4knee); mult=max(B-thr,soft)/B`.
+  Two consequences: raising a colour past the clamp is a no-op, and **inside** the
+  clamp extra bloom is bought with bright **AREA**, not intensity — so find which
+  property covers the most silhouette (next trap) instead of turning brightness up.
+  Also check the tonemapper: at `mode: 0` (None) there is no shoulder, so channels
+  above 1.0 clip hard and shift hue toward white — "brighter" silently means
+  "less saturated".
+- **Before tuning a colour, find out which property covers the AREA — it is usually
+  not the one named "Bright".** Dump the graph (§2a) and read the composition: these
+  crystal shaders are `lerp(dull, bright, fresnel)` with `fresnel = (1−N·V)⁴`, and at
+  that exponent the rim is **2.5%** of a silhouette (area-weighted mean fresnel 0.067).
+  So `_DullCrystalColor` is ~93% of the object and essentially all of its bloom, while
+  `_BrightCrystalColor` is a hairline. Integrating the bloom response over the
+  silhouette (`∫ bloom(lerp(dull,bright,f(r))) · 2πr dr`) turns "which knob?" into a
+  number in ten lines of numpy. Note sibling graphs can SWAP the roles
+  (`TimeCrystalGraph` does), which is a strong argument for expressing a per-variant
+  difference as a **scalar on the pair** rather than a second authored pair: a scalar
+  cannot move the hue and dims correctly whichever role each colour plays.
+- **`Renderer.SetPropertyBlock` REPLACES the block, and `MaterialPropertyBlock.Clear()`
+  discards EVERYONE's overrides, not just yours.** A component that owns a private block
+  and pushes it wholesale will silently erase any other system's per-renderer tint on the
+  same renderer — and if it `Clear()`s on completion, it erases it permanently. Live case:
+  `FadeIn` drove `_opacity` this way on every crystal model, so `Crystal.ApplyColorSetTint`'s
+  colour was wiped at the start of the fade and again at the end, and **no crystal in the
+  game had ever displayed its intended colour** — each just settled back to its authored
+  material, which looks deliberate. Always `GetPropertyBlock(block)` before `SetFloat`/
+  `SetColor` (it clears and refills the block from the renderer, so it is a true
+  read-modify-write), and retire an override by writing the material's own authored value
+  back rather than clearing. Composing also makes the writers **order-independent**, which
+  matters because `Start()` order between a parent and its child components is undefined.
+  Detection: grep for `SetPropertyBlock` and check each call site is preceded by a
+  `GetPropertyBlock` on the same renderer.
+- **A guid grep of a prefab CANNOT see components that live in its nested prefabs.**
+  `grep -c <FadeIn guid> Crystal.prefab` returned **0**, and the obvious conclusion — "the
+  omni has no FadeIn, so its tint survives, and `DeactivateModels`'s unguarded
+  `GetComponent<FadeIn>().StartFadeIn()` must be NREing" — was wrong on both counts: the
+  four models are instances of `TrucatedOctahedron.prefab`, which carries the component. A
+  `!u!1001 PrefabInstance` contributes its source prefab's whole component set at runtime
+  while contributing only its OWN guid plus override rows to the file. So: resolve
+  `m_SourcePrefab` guids and grep those files too (recursively) before concluding an object
+  lacks a component — or load it the way §5's read-only-tool bullet says and ask the merged
+  hierarchy.
 - **A reference field can point at a DISABLED TWIN of the object doing the
   work.** When a prefab was migrated from a nested component-prefab to a
   bespoke object, the old instance often survives, inactive, still holding
diff --git a/CLAUDE.md b/CLAUDE.md
index 408e966bb..79ec386bc 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -498,7 +498,7 @@ MiniGameControllerBase (abstract, NetworkBehaviour)
 | `SCENES.md` | `Docs/` | Complete scene inventory, game modes, launch pipeline |
```

</details>

### `bda357c49` — fix(ecology): the crystal pickup sound never reached a lifeform's drop

_Claude, 2026-08-15 05:08:59 +0000_

```text
Found while verifying the capture rework's own claim that the burst "ends with
the CrystalCollect sound". It did not, and had not for as long as the ecology has
dropped crystals.

Crystal.PlayExplosionAudio guarded on its [Inject] AudioSystem field. That field is
null on every crystal that was not part of a loaded scene - a lifeform's heart is
Instantiate'd by the cell's spawners, and nothing under Controller/Environment
calls GameObjectInjector.InjectRecursive. So the guard silently swallowed the
pickup sound for the entire food web's output, and for the conveyor toy's local
mints, while reading as correctly wired: a null-guard on an injected field is
exactly what a correct null-guard looks like.

Falls back to AudioSystem.Instance - the same accessor
SkimmerAdjustElementLevelByCrystalEffectSO already uses one frame earlier on the
very same pickup. Scene-loaded crystals keep using the injected reference.

Also warns once when Resources/CrystalCaptureConfig is missing: the built-in
defaults ARE the shipped values, so a missing asset changes nothing about how a
capture looks - it just means nobody can tune it, which is otherwise invisible
until someone edits the asset and sees no effect.

General lesson recorded in Docs/ECOSYSTEM.md §30.2: [Inject] on a prefab that
some system spawns at runtime is a REQUEST, not a guarantee. Find the injector
before relying on the field.
```

```text
 Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs | 13 +++++++++++--
 Assets/_Scripts/ScriptableObjects/CrystalCaptureConfigSO.cs | 13 ++++++++++++-
 Docs/ECOSYSTEM.md                                           | 15 ++++++++++++++-
 3 files changed, 37 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 88 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs b/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
index 99be3986c..60f72d425 100644
--- a/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
+++ b/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
@@ -383,10 +383,19 @@ namespace CosmicShore.Gameplay
             PlayExplosionAudio();
         }
 
+        /// <summary>
+        /// The pickup sound. Falls back to <see cref="AudioSystem.Instance"/> because the injected
+        /// field is null on every crystal that was NOT part of a loaded scene: a lifeform's heart
+        /// is `Instantiate`d by the cell's spawners, and nothing under Controller/Environment calls
+        /// `GameObjectInjector.InjectRecursive`. So this was a silent no-op for the entire ecology's
+        /// crystal drops - which is most of the crystals in the game - while reading as wired.
+        /// Same accessor the elemental powerup effect already uses one frame earlier.
+        /// </summary>
         void PlayExplosionAudio()
         {
-            if (audioSystem != null)
-                audioSystem.PlayGameplaySFX(GameplaySFXCategory.CrystalCollect, transform.position);
+            var audio = audioSystem != null ? audioSystem : AudioSystem.Instance;
+            if (audio != null)
+                audio.PlayGameplaySFX(GameplaySFXCategory.CrystalCollect, transform.position);
         }
 
         /// <summary>
diff --git a/Assets/_Scripts/ScriptableObjects/CrystalCaptureConfigSO.cs b/Assets/_Scripts/ScriptableObjects/CrystalCaptureConfigSO.cs
index 5f535a24e..340266040 100644
--- a/Assets/_Scripts/ScriptableObjects/CrystalCaptureConfigSO.cs
+++ b/Assets/_Scripts/ScriptableObjects/CrystalCaptureConfigSO.cs
@@ -1,3 +1,4 @@
+using CosmicShore.Utility;
 using UnityEngine;
 
 namespace CosmicShore.ScriptableObjects
@@ -185,8 +186,18 @@ namespace CosmicShore.ScriptableObjects
         public static CrystalCaptureConfigSO Load()
         {
             if (_cached) return _cached;
+
             _cached = Resources.Load<CrystalCaptureConfigSO>(ResourcePath);
-            if (!_cached) _cached = CreateInstance<CrystalCaptureConfigSO>();
+            if (!_cached)
+            {
+                // Loud, but once - the defaults ARE the shipped values, so a missing asset changes
+                // nothing about how a capture looks. What it does mean is that nobody can TUNE it,
+                // which is invisible until someone edits the asset and sees no effect.
+                CSDebug.LogWarning($"[CrystalCapture] Resources/{ResourcePath} is missing - captures " +
+                    "will run on built-in defaults and the asset's values will not apply. Restore it, " +
+                    $"or create one via Assets > Create > ScriptableObjects > {nameof(CrystalCaptureConfigSO)}.");
+                _cached = CreateInstance<CrystalCaptureConfigSO>();
+            }
             return _cached;
         }
     }
diff --git a/Docs/ECOSYSTEM.md b/Docs/ECOSYSTEM.md
index 33c39f959..2405aa9de 100644
--- a/Docs/ECOSYSTEM.md
+++ b/Docs/ECOSYSTEM.md
@@ -3732,6 +3732,18 @@ a colour is a rate in linear HDR, so a gain brightens without shifting hue, and
 would read as a *different* crystal (`Docs/PALETTE.md`). No new FMOD event was added — the pickup now
 reaches the shared `CrystalCollect` category it always should have.
 
+**And that reach was itself broken, on far more than this branch's path.** `Crystal.PlayExplosionAudio`
+guarded on its `[Inject] AudioSystem` field, which is null on **every crystal that was not part of a
+loaded scene**: a lifeform's heart is `Instantiate`d by the cell's spawners, and *nothing* under
+`Controller/Environment` calls `GameObjectInjector.InjectRecursive`. So the pickup sound was a silent
+no-op for the entire ecology's crystal drops — and for the conveyor toy's local mints — while reading
+as correctly wired, because the guard is exactly what a correct guard looks like. It now falls back
+to `AudioSystem.Instance`, the same accessor `SkimmerAdjustElementLevelByCrystalEffectSO` already uses
+one frame earlier on the very same pickup. **The general lesson: `[Inject]` on a prefab that some
+system spawns at runtime is a REQUEST, not a guarantee** — before relying on an injected field in
+anything spawned outside a scene load or a `GameObjectInjector` call site, find the injector. There
+may not be one.
+
 `Crystal.Explode` grew one optional argument, `huskScale`: the burst fires at the end of a flight
 that has already shrunk the crystal into the hull, and the payoff must be sized by the crystal the
 pilot *picked up*, not by the flourish that preceded it. The networked path takes the default and is
@@ -3754,7 +3766,8 @@ crystal is a handful of objects, not the 2,000-instance surface that law exists
 
```

</details>

### `2cfcad5d2` — docs: capture the two reusable findings from the crystal-capture session

_Claude, 2026-08-15 05:10:30 +0000_

```text
CLAUDE.md anti-pattern: relying on an [Inject] field in a prefab that a gameplay
system spawns at runtime. Reflex populates [Inject] at scene load and at explicit
GameObjectInjector call sites (vessels, players, pools) - nothing else. The whole
of Controller/Environment is in the "nothing else" set, so every cell-spawned
flora, fauna and crystal has null injected dependencies. It is invisible because
the correct defensive shape and the broken one are identical.

asset-surgery §4.9: answering "does every X actually carry Y" THROUGH prefab
nesting - the three greps that turn the ship protocol's find-the-PRODUCER gate
into an exhaustive owner -> source -> field-state table when the producer is a
serialized reference an instance can override.

asset-surgery §4.9b: stripping a dead serialized key from many prefabs - scope by
the enclosing m_Script or a same-named key on another component goes with it,
assert the reject list is empty, and round-trip the trailing newline.
```

```text
 .claude/skills/asset-surgery/SKILL.md | 41 +++++++++++++++++++++++++++++++++++++++++
 CLAUDE.md                             |  1 +
 2 files changed, 42 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/.claude/skills/asset-surgery/SKILL.md b/.claude/skills/asset-surgery/SKILL.md
index 0d2b34dd8..730051b54 100644
--- a/.claude/skills/asset-surgery/SKILL.md
+++ b/.claude/skills/asset-surgery/SKILL.md
@@ -721,6 +721,47 @@ that would otherwise cost a round-trip to a human at the editor:
   mesh's "nose" by comparing cross-section extents near each end of its long axis (the
   radially-symmetric end is the nose, the asymmetric one is the fins).
 
+## 4.9 Technique: answering "does every X actually carry Y?" THROUGH prefab nesting
+
+Origin: the crystal-capture rework (2026-08). The branch's whole payoff was routed through
+`Crystal.Explode`, which does nothing useful unless the crystal carries a `SpentCrystalPrefab`
+and a non-null `explodingMaterial`. The doc asserted it did. Checking that claim by grepping
+one prefab proves nothing, because **the crystal the lifeform actually drops is a NESTED
+PREFAB INSTANCE** — the value lives in the *source* prefab and can be overridden, or not, at
+each nesting site. This is the general shape of the ship protocol's "find the PRODUCER" gate
+whenever the producer is a serialized reference, and it is three greps, not a judgment call:
+
+1. **Find every direct owner** — grep for the component's script GUID (from its `.cs.meta`),
+   then walk `--- !u!114` MonoBehaviour blocks and read the field out of the block whose
+   `m_Script` matches. Do *not* regex the field name across the whole file: several components
+   can carry a same-named key, and you will attribute the wrong one.
+2. **Resolve the nesting** — a prefab whose component block is *absent* holds the thing as a
+   `--- !u!1001 PrefabInstance`; its `m_SourcePrefab: {fileID: …, guid: G}` names the source.
+   Map `G` back to a path via `grep -rl "guid: G" Assets --include=*.meta`, and you have
+   reduced "16 lifeforms" to "4 crystal prefabs I can check exhaustively".
+3. **Check for a nesting site that STRIPS it** — `grep -rn "propertyPath: <Field>" Assets`.
+   An override to `{fileID: 0}` at one site is precisely the case that makes a
+   verified-at-the-source claim false in the field, and it is invisible from the source prefab.
+
+Report the resulting table (owner → source → field state) in the ship report. An exhaustive
+"all 16 resolve to 4 prefabs, all 4 SET, no site overrides it" is evidence; "I checked one" is not.
+
+## 4.9b Technique: stripping a dead serialized key from many prefabs
+
+Deleting a `[SerializeField]` in C# leaves its key in every prefab that authored it. Unity
+never prunes an unresolvable modification, so the inspector keeps showing a value nothing reads
+— worse than no field at all. Removing them mechanically is safe under three conditions:
+
+- **Scope by the enclosing `m_Script`.** Track the last `  m_Script:` line as you stream the
+  file and only drop the key while that GUID is the component you retired. A bare
+  `sed '/moveToVesselDuration/d'` will happily strip a same-named key from another component.
+- **Assert the scoping found everything.** Collect the rejects (key matched, wrong component)
+  and print them; an empty reject list is the proof the pass was total.
+- **Round-trip the bytes.** `'\n'.join(text.split('\n'))` preserves a trailing newline; verify
+  against `git show <base>:<path>` that `endswith(b'\n')` is unchanged for every file, and
+  confirm `git diff` contains *only* the removed key lines and no `\ No newline` marker. A
+  whitespace-only byte change on 15 prefabs is indistinguishable from a real edit in review.
+
 ## 5. Traps learned the hard way (check these BEFORE debugging for an hour)
 
 - **Renaming a Unity SERIALIZED FIELD must sweep `Tools/**.py` too, not just C# + scenes +
diff --git a/CLAUDE.md b/CLAUDE.md
index 6a1df16b0..e5502b735 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -2404,6 +2404,7 @@ All game code lives under `CosmicShore.*` with 8 primary namespaces:
 - `await UniTask.SwitchToMainThread()` or `await UniTask.Yield(PlayerLoopTiming.Update)` as a thread-marshaling fix — they don't reliably switch threads on this UniTask version. Use `.AsMainThread()` (see `Docs/THREADING.md`)
 - Raising a SOAP `ScriptableEvent` from a UGS / Netcode `Task` continuation without ensuring the continuation has resumed on the main thread first — SOAP `Raise()` invokes listeners inline, so off-thread raises crash any listener that touches Unity state
 - Touching a `UnityEngine.Object` (incl. `== null` checks routing through `op_Equality`) in a `Task` continuation without `.AsMainThread()` upstream — throws `EnsureRunningOnMainThread`
+- **Relying on an `[Inject]` field in a prefab that a gameplay system spawns at runtime, without finding the injector.** Reflex populates `[Inject]` for objects present at scene load (via the scene's `ContainerScope`) and for anything a call site explicitly runs `GameObjectInjector.InjectRecursive` on — vessels, players, projectile/AOE pools. **Everything else gets a null field**, and the whole of `Controller/Environment` is in that set: nothing there injects, so every cell-spawned flora, fauna and crystal has null injected dependencies. The failure is invisible because the correct defensive shape and the broken one are identical — `if (audioSystem != null) audioSystem.Play…()` is exactly what a good null-guard looks like, and it silently swallowed the crystal pickup sound for the entire food web's output for as long as the ecology has dropped crystals (`Docs/ECOSYSTEM.md §30.2`). Before depending on an injected field, grep for who injects that object; if nobody does, resolve at the call site instead (`AudioSystem.Instance`, the live-`Instance` property pattern below), or inject it at the spawner
 - Caching a UGS singleton `*.Instance` (e.g. `MultiplayerService.Instance`) in a service **constructor** — lazy DI singletons are constructed during Bootstrap DI resolution, *before* `UnityServices.InitializeAsync()` completes, so `*.Instance` is null at construction and gets pinned null forever. Instead expose a private property that resolves at use time: `private IMultiplayerService _multiplayerService => MultiplayerService.Instance;` — always reads the live `Instance` at the call site (see `PartySessionService` / `PresenceLobbyService`)
 - Subscribing to per-`RoundStats` C# stat events (`OnScoreChanged`, `OnAnyStatChanged`, `OnCrystalsCollectedChanged`, …) with cleanup gated on `OnMiniGameTurnEnd`, or unsubscribing by iterating `gameData.RoundStatsList` — `RoundStats` lives on the **persistent** Player NetworkObject (survives every scene transition), a mid-turn scene exit never fires the turn-end cleanup, and `SceneLoader.LoadSceneAsync` clears the roster lists via `ResetRuntimeData()` BEFORE the old scene's objects are destroyed, so list-based unsubscribe loops detach nothing. The leaked delegates fire inside the next game's stat-setter raise chains and can silently kill the game-end flow (`Docs/ScoringSystem/BUGS.md` B15). Instead: track the stats you actually subscribed to and detach from that record in `OnDestroy` (see `NetworkCrystalCollisionTurnMonitor` / `MultiplayerHUD`); `Player.PrepareForNewScene` / `InitializeForMultiplayerMode` purge any stragglers via `RoundStats.ClearEventSubscriptions()` at every scene entry
 
```

</details>

### `8572c4311` — fix(rhino): keep tracer spacing even across widths, drop the sword mount to hull centre

_Claude, 2026-08-15 05:13:29 +0000_

```text
SeatTracers inset each streak's span by its OWN head width, so the moment two
tracers were tuned to different widths every streak got a different span and the
spacing drifted apart. One span now serves the whole set - inset at each end by
half the head width of the streak sitting there - so the spacing is even by
construction whatever the individual widths are.

The five tracer rest transforms are also authored spread along the blade's rest
pose instead of stacked at the fuselage origin, so the prefab reads correctly in
the editor before the runtime seating ever runs.

Sword mount y 2 -> -1, about the hull box's own vertical centre (-1.2).
```

```text
 Assets/_Prefabs/Spacevessels/Rhino.prefab                                    | 12 ++++++------
 .../Controller/Vessel/R_VesselActions/Executors/RhinoSwordFXController.cs    | 26 ++++++++++++++++++--------
 Assets/_Scripts/Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md      | 12 ++++++++----
 Assets/_Scripts/Controller/Vessel/R_VesselActions/RHINO_SHIELD_SWIPE.md      |  6 +++---
 Docs/UNITY_VERIFICATION_CHECKLIST.md                                         | 15 ++++++++-------
 5 files changed, 43 insertions(+), 28 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 129 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordFXController.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordFXController.cs
index b251adeec..3b82fde01 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordFXController.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoSwordFXController.cs
@@ -276,11 +276,13 @@ namespace CosmicShore.Gameplay
         /// so the sword draws a comb of hairlines through a swing rather than one slab.
         ///
         /// Their SHAPE is yours: width, time, taper curve, gradient and material are authored on
-        /// each TrailRenderer and this never writes them. All it owns is placement. Because a
-        /// TrailRenderer lays its width symmetrically about the emitter's path, the two END
-        /// streaks are inset by half their own head width, so widening one grows it INTO the
-        /// blade instead of out past the point or behind the grip; everything between is
-        /// interpolated across that inset span.
+        /// each TrailRenderer and this never writes them. All it owns is placement, and the
+        /// spacing is EVEN by construction: ONE span is computed for the whole set, inset at each
+        /// end by half the head width of the streak that sits there (a TrailRenderer lays its
+        /// width symmetrically about the emitter's path, so an end streak parked exactly on the
+        /// point would hang half its band past it). Insetting each streak by its OWN width
+        /// instead would hand every streak a different span, and the spacing would drift apart
+        /// the moment two of them were tuned to different widths.
         ///
         /// (The inset is exact while the sword swings across its own axis — a swipe or chop,
         /// which is when the streaks are visible at all. The width direction is perpendicular to
@@ -298,19 +300,27 @@ namespace CosmicShore.Gameplay
             int count = bladeTracers.Length;
             EnsureTracerCaches(count);
 
+            // One span for every streak — see above.
+            Vector3 spanTip = tip - towardTip * (0.5f * HeadWidthAt(0));
+            Vector3 spanHilt = hilt + towardTip * (0.5f * HeadWidthAt(count - 1));
+
             for (int i = 0; i < count; i++)
             {
                 var tracer = bladeTracers[i];
                 if (!tracer) continue;
 
-                float half = 0.5f * HeadWidth(tracer, i);
                 // 1 at element 0 (tip) running to 0 at the last (hilt).
                 float t = count == 1 ? 1f : 1f - (float)i / (count - 1);
-                tracer.transform.position = Vector3.Lerp(hilt + towardTip * half,
-                                                         tip - towardTip * half, t);
+                tracer.transform.position = Vector3.Lerp(spanHilt, spanTip, t);
             }
         }
 
+        float HeadWidthAt(int index)
+        {
+            var tracer = bladeTracers[index];
+            return tracer ? HeadWidth(tracer, index) : 0f;
+        }
+
         /// <summary>
         /// Width at the EMITTING end of a streak: the multiplier scales the authored curve, and
         /// the curve's value at t=0 is the end being laid down right now. The curve is re-read
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md b/Assets/_Scripts/Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md
index b24fbf307..a78e4b33e 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/RHINO_ENERGY_SWORD.md
@@ -255,10 +255,14 @@ v2's code-built `RhinoSwordVisualizer` (deleted). Four layers:
    **Their look is yours on the components** — `widthMultiplier`, `time`, the width curve, the
    colour gradient, material. Nothing in code writes any of them, and the COUNT is data too:
    `RhinoSwordFXController.bladeTracers` is an array and the spread is derived from its length,
-   so add or remove entries freely. `SeatTracers` owns placement only, insetting the two END
-   streaks by half their own head width (head width = `widthMultiplier` × the width curve at
-   t=0) so widening one grows it INTO the blade rather than out past the point or behind the
-   grip. Authored hairline: `widthMultiplier` 0.5, `time` 0.15.
+   so add or remove entries freely. `SeatTracers` owns placement only, and the spacing is EVEN
+   by construction: ONE span serves the whole set, inset at each end by half the head width of
+   the streak sitting there (head width = `widthMultiplier` × the width curve at t=0) so
+   widening an end streak grows it INTO the blade rather than out past the point. Insetting each
+   streak by its own width instead would give every streak a different span and the spacing
+   would drift apart the moment two were tuned to different widths. Authored hairline:
+   `widthMultiplier` 0.5, `time` 0.15; their prefab rest transforms are authored spread along the
+   blade too, so the set reads correctly in the editor rather than stacked at the fuselage origin.
 
    All five are tinted from the same live blade colour as the body, so **the streaks change with
    the sword through every state** (white-hot → danger red on energize).
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/RHINO_SHIELD_SWIPE.md b/Assets/_Scripts/Controller/Vessel/R_VesselActions/RHINO_SHIELD_SWIPE.md
index cbb511b35..7cadbedea 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/RHINO_SHIELD_SWIPE.md
```

</details>

### `37f9596a2` — perf(prisms): GPU-clock the shield morphs and delete the last CPU prism ticker

_Claude, 2026-08-15 05:48:41 +0000_

```text
Docs/PRISM_ANIMATION.md §5 B4 — the final Phase B migration. The octahedron
shield's engage bloom and disengage shatter (and the stellated super-shield's
pair) were the last per-frame CPU prism animation: PrismOctahedronShieldManager
ticked every morphing shield and each one REBUILT A MESH per frame, on the
un-batched GameObject renderer. Both are now f(clock, stamp); the manager and
IPrismShieldMorphTicker are deleted, and an edit-mode test fails if they return.

The one input a vertex shader cannot derive is which face a vertex belongs to,
so Octahedron/StellatedOctahedronMeshGenerator now bake each vertex's own face
centroid into TEXCOORD1. That makes the cache-SHARED settled shield mesh also
the morph mesh, which is what lets the shield stay on the instanced path for the
whole animation: same-size shields remain ONE batch instead of each minting a
unique mesh and a draw call for 0.35-0.7s.

- PrismShieldMorph_float (PrismClockAnimation.hlsl) + four Hybrid Per Instance
  properties, wired into BOTH live-prism graphs by
  Tools/Shaders/wire_prism_shield_morph.py (idempotent, validate-before-write).
  Easing is smoothstep, which IS the AnimationCurve.EaseInOut both shields
  shipped with — so the feel is reproduced, not approximated, and the curve
  fields are retired with the CPU driver.
- Everything final at t=0: Engage applies the whole shielded pose (collider,
  mass, material, shared mesh, render handoff) and then stamps. No scheduled
  completion callback exists — the shader clamps at t=1, which IS the settled
  shield. The stamp is only ever cleared, at disengage and on pool reuse.
- The exotic-visual handoff is kept and still load-bearing (SetRenderMeshOverride
  is what makes the entity draw the octahedron; a bare MeshFilter swap renders
  nothing), but SetExoticVisualActive is now only ever driven FALSE — nothing in
  the project needs per-prism-unique geometry any more.
- The shatter overlay's per-prism child GameObject becomes batched pure-entity
  debris on the §4.6 carrier (PrismShieldShatter + SpawnShieldShatterBatch,
  grouped by mesh x material x layer since shields vary in size and domain), and
  is deliberately no longer cancelled on re-engage: deleting visible shards
  mid-flight is the continuity-of-existence breach the old StopShatter() made.

Gates: PrismShieldMorphTests (baked centroids vs the retired CPU formula, the
HPI wiring + UV channel on both graphs, the deleted ticker, and no re-added
Update/coroutine/tween/mesh-rebuilder), the wiring script's --check, and
Validate Clock Wiring. In-editor playtest: PRISM_CLOCK_WIRING_CHECKLIST Phase 9.
```

```text
 Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph              | 879 +++++++++++++++++++++++++++++++-
 Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph     | 879 +++++++++++++++++++++++++++++++-
 Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl            |  51 ++
 Assets/_Scripts/Controller/ECS/Rendering/PrismRenderProperties.cs     |  35 ++
 Assets/_Scripts/Controller/ECS/Rendering/PrismRenderService.cs        | 173 ++++++-
 .../_Scripts/Controller/Environment/MiniGameObjects/SegmentSpawner.cs |   8 +-
 Assets/_Scripts/Controller/Managers/PrismOctahedronShieldManager.cs   |  76 ---
 Assets/_Scripts/Controller/Managers/PrismStateManager.cs              |  22 +-
 Assets/_Scripts/Controller/Vessel/PrismOctahedronShield.cs            | 333 ++++--------
 Assets/_Scripts/Controller/Vessel/PrismOctahedronShieldTester.cs      |   7 +
 Assets/_Scripts/Controller/Vessel/PrismShieldMorph.cs                 | 150 ++++++
 .../PrismShieldMorph.cs.meta}                                         |   2 +-
 Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs   | 302 +++--------
 .../Controller/Vessel/PrismStellatedOctahedronShieldTester.cs         |   4 +-
 Assets/_Scripts/Editor/PrismClockWiringValidator.cs                   |  12 +-
 Assets/_Scripts/Tests/Editor/PrismShieldMorphTests.cs                 | 244 +++++++++
 Assets/_Scripts/Tests/Editor/PrismShieldMorphTests.cs.meta            |  11 +
 Assets/_Scripts/Utility/Effects/PrismShieldShatter.cs                 | 301 +++++++++++
 Assets/_Scripts/Utility/Effects/PrismShieldShatter.cs.meta            |  11 +
 Assets/_Scripts/Utility/OctahedronMeshGenerator.cs                    | 133 ++---
 Assets/_Scripts/Utility/StellatedOctahedronMeshGenerator.cs           | 118 ++---
 CLAUDE.md                                                             |   4 +-
 Docs/PRISM_ANIMATION.md                                               |  87 +++-
 Docs/PRISM_CLOCK_WIRING_CHECKLIST.md                                  |  50 +-
 Docs/UNITY_VERIFICATION_CHECKLIST.md                                  |  45 ++
 Tools/Shaders/wire_prism_shield_morph.py                              | 544 ++++++++++++++++++++
 26 files changed, 3710 insertions(+), 771 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 3335 lines)</summary>

```diff
diff --git a/Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl b/Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl
index ea64bcdd3..c763bec8e 100644
--- a/Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl
+++ b/Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl
@@ -229,4 +229,55 @@ void PrismFlightSqrDistance_float(float Clock, float StartTime, float Duration,
     SqrDistance = dot(d, d);
 }
 
+// -----------------------------------------------------------------------------
+// Shield morph — the per-face bloom (engage) and the shatter overlay (disengage),
+// for BOTH shield tiers (BlockGraph + ExplodingBlockGraph vertex stage).
+// Docs/PRISM_ANIMATION.md §5 B4. This replaces the last sanctioned CPU ticker
+// (PrismOctahedronShieldManager), which rebuilt a per-prism morph MESH every frame
+// for the whole 0.35-0.7 s animation.
+//
+// The one thing a vertex shader cannot derive is which face a vertex belongs to,
+// so the mesh generators bake each vertex's own FACE CENTROID into TEXCOORD1
+// (OctahedronMeshGenerator/StellatedOctahedronMeshGenerator.FaceCentroidUVChannel).
+// With that, both animations are the same two-term expression the CPU used:
+//
+//   engage  (Direction >= 0):  p = centroid + t*(v - centroid)
+//   shatter (Direction <  0):  p = centroid + (1-t)*(v - centroid) + t*Offset*n
+//
+// t is smoothstep(0,1,progress) — EXACTLY the authored AnimationCurve.EaseInOut(0,0,1,1)
+// both shields shipped with (a Hermite with zero end tangents IS 3p^2-2p^3), so the
+// migration is faithful, not merely similar. The curve fields are retired with the
+// CPU driver: an arbitrary AnimationCurve has no GPU evaluation, and the whole fleet
+// authored the default.
+//
+// Because the morph runs on the SETTLED shared mesh, a shielded prism never leaves
+// the instanced path: same-size shields batch into one draw through the entire
+// animation. Normal is the OBJECT-space flat face normal (the generators author one
+// normal per face), and it is deliberately NOT re-derived after displacement — a
+// per-face rigid translation cannot change a face's normal.
+//
+// Duration <= 0 -> unstamped: the position passes through untouched, which is every
+// prism in the game that is not mid-shield-morph (and every mesh with no TEXCOORD1).
+// -----------------------------------------------------------------------------
+void PrismShieldMorph_float(float Clock, float StartTime, float Duration, float Direction,
+    float ShatterOffset, float3 Position, float3 Normal, float3 FaceCentroid,
+    out float3 MorphedPosition)
+{
+    if (Duration <= 0.0)
+    {
+        MorphedPosition = Position;
+        return;
+    }
+    float p = saturate((Clock - StartTime) / Duration);
+    float t = smoothstep(0.0, 1.0, p);
+
+    // Branchless select: both tiers and both directions share one expression, so a
+    // shattering prism and a blooming prism in the same batch never diverge.
+    float shatter   = Direction < 0.0 ? 1.0 : 0.0;
+    float faceScale = lerp(t, 1.0 - t, shatter);
+    float offset    = shatter * t * ShatterOffset;
+
+    MorphedPosition = FaceCentroid + faceScale * (Position - FaceCentroid) + offset * Normal;
+}
+
 #endif // PRISM_CLOCK_ANIMATION_INCLUDED
diff --git a/Assets/_Scripts/Controller/ECS/Rendering/PrismRenderProperties.cs b/Assets/_Scripts/Controller/ECS/Rendering/PrismRenderProperties.cs
index 65a3e7826..c334d3765 100644
--- a/Assets/_Scripts/Controller/ECS/Rendering/PrismRenderProperties.cs
+++ b/Assets/_Scripts/Controller/ECS/Rendering/PrismRenderProperties.cs
@@ -217,4 +217,39 @@ namespace CosmicShore.ECS
     {
         public float3 Value;
     }
+
+    // -- Prism set: SHIELD MORPH (Docs/PRISM_ANIMATION.md §5 B4) --
+    // The octahedron shield's per-face engage bloom and its shatter overlay (and the
+    // stellated super-shield's twin pair). Both run in the vertex stage on the
+    // cache-SHARED settled shield mesh, off the per-face centroid the mesh generators
+    // bake into TEXCOORD1 — so a shielded prism never leaves the instanced path and
+    // same-size shields batch through the whole animation. Duration 0 = unstamped =
+    // "render the mesh as authored", which is every prism not mid-transition.
+
+    [MaterialProperty("_ShieldMorphStartTime")]
+    public struct PrismShieldMorphStartTimeOverride : IComponentData
+    {
```

</details>

### `e590712ba` — fix(prisms): size the jiggle's culling envelope by scale ratio; make the exotic-visual gate real

_Claude, 2026-08-15 05:53:27 +0000_

```text
Two defects found by an adversarial review of the branch, both confirmed by reading
the code and one by measurement against the shipped HLSL.

1. The culling envelope under-covered every anisotropic prism. The shader rotates in
   the world-proportioned frame and maps back through 1/scale, so the object-space
   displacement is amplified by the prism's scale RATIO — but the padding was sized
   from the uniform-scale measurement alone. Measured peak displacement as a multiple
   of radius x tilt: 0.98x at uniform scale, 2.73x at (3,3,10), 4.64x at (12,2,2) and
   15.97x at (1,1,20). An under-covered prism frustum-culls away mid-wobble at the
   screen edge, which reads as the mass blinking out. The formula now carries the
   ratio, and the four measured rows are pinned by a test so it cannot regress to the
   uniform one. The original clang harness only measured the uniform case, which is
   exactly why this got through; it now sweeps anisotropic scales too.

2. The exotic-visual gate was inert in the one case it existed for.
   `!UsesEntityColorSink && !TryEnsureRenderEntityForStamp()` reads as "skip while a
   shield morph owns the renderer", but TryEnsureRenderEntityForStamp returns true on
   its first line whenever the handle is usable — so with an exotic visual active and
   a usable handle the condition is false and it stamped anyway, spending a one-shot
   stamp on a hidden entity and then swallowing the next visible hit via the cooldown.
   Now gated on the exotic flag alone, via a new Prism.ExoticVisualActive; the
   self-heal call remains on the stamp-failure path, so entity creation is unaffected.

Also replaces the settle's CancelScheduledActions with an O(1) stamp-time guard.
CancelScheduledActions is a linear scan of the shared action list, so cancelling per
stamp made one blast over N super-shielded prisms O(N^2) - and a blast over a
super-shielded structure is precisely the case that stamps N prisms in one frame. The
same guard invalidates a settle left over from a previous life: a pooled prism is
deactivated rather than destroyed, so the scheduler's null-owner sweep never drops it
(Prism.ResetState now clears the stamp time).

Three further findings were raised and refuted on the code: the AOE path is not
unbudgeted-unbounded (shouldContinue=false destroys the blast on first super-shield
contact, alreadyHit claims each slot once, and the per-prism cooldown bounds
re-stamps), and the stale-settle harm was unreachable before the guard for
independent reasons (super-shielded mass is invulnerable so it cannot reach the pool
by destruction, and the prism pools are disjoint).
```

```text
 Assets/_Scripts/Controller/Vessel/Prism.cs                  | 18 ++++++++++
 Assets/_Scripts/Controller/Vessel/PrismSuperShieldJiggle.cs | 79 +++++++++++++++++++++++++++++++++++++------
 Assets/_Scripts/Tests/Editor/PrismSuperShieldJiggleTests.cs | 43 +++++++++++++++++++++++
 Docs/PRISM_ANIMATION.md                                     | 23 +++++++++++--
 Docs/SPATIAL_INDEX.md                                       |  8 ++++-
 5 files changed, 156 insertions(+), 15 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 271 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/Prism.cs b/Assets/_Scripts/Controller/Vessel/Prism.cs
index 8d6caade4..924fabaf1 100644
--- a/Assets/_Scripts/Controller/Vessel/Prism.cs
+++ b/Assets/_Scripts/Controller/Vessel/Prism.cs
@@ -181,6 +181,13 @@ namespace CosmicShore.Gameplay
         internal bool UsesEntityColorSink =>
             !_exoticVisualActive && PrismRenderService.IsHandleUsable(in RenderHandle);
 
+        /// <summary>True while per-prism-unique geometry (a shield engage morph, a shatter
+        /// overlay) is drawn by this GameObject's MeshRenderer instead of the companion
+        /// entity. A one-shot clock stamp made in this window is spent invisibly, so stamp
+        /// sites that can defer should check this ALONE — <see cref="UsesEntityColorSink"/>
+        /// bundles it with handle usability and is the wrong test for that question.</summary>
+        internal bool ExoticVisualActive => _exoticVisualActive;
+
         public Vector3 TargetScale
         {
             get => scaleAnimator?.TargetScale ?? transform.localScale;
@@ -720,6 +727,11 @@ namespace CosmicShore.Gameplay
             // IsShielded/IsDangerous are NOT cleared: spawners set those pre-Initialize
             // as the requested state for this life.
             if (prismProperties != null) prismProperties.IsSuperShielded = false;
+            // Pool reuse: this is also what INVALIDATES any super-shield deflection settle
+            // still scheduled from the previous life. That callback compares its captured
+            // stamp time against LastSuperShieldJiggleTime and no-ops on a mismatch, so
+            // resetting here is what stops it resetting THIS life's culling envelope.
+            _lastSuperShieldJiggleTime = float.NegativeInfinity;
             _lodCulled = false; // pool reuse: Initialize owns the collider again
             CachedVolume = 0f;  // stale from the previous life; reseeded at CreateBlock
             IsSmallest = false;
@@ -1271,6 +1283,12 @@ namespace CosmicShore.Gameplay
         // the past, which correctly reads as "no recent deflection".
         float _lastSuperShieldJiggleTime = float.NegativeInfinity;
 
+        /// <summary>Clock time of this prism's most recent deflection stamp, or
+        /// <see cref="float.NegativeInfinity"/> if it has none in this life. A scheduled
+        /// settle carries the value it stamped and compares it here, so a re-stamp or a pool
+        /// reuse invalidates the older callback without an O(n) scan of the timer list.</summary>
+        internal float LastSuperShieldJiggleTime => _lastSuperShieldJiggleTime;
+
         /// <summary>
         /// THE super-shield invulnerability gate. Returns true when this prism absorbs the hit
         /// — the caller must then do nothing else to it.
diff --git a/Assets/_Scripts/Controller/Vessel/PrismSuperShieldJiggle.cs b/Assets/_Scripts/Controller/Vessel/PrismSuperShieldJiggle.cs
index 188264202..b028c1375 100644
--- a/Assets/_Scripts/Controller/Vessel/PrismSuperShieldJiggle.cs
+++ b/Assets/_Scripts/Controller/Vessel/PrismSuperShieldJiggle.cs
@@ -102,8 +102,13 @@ namespace CosmicShore.Gameplay
             // While an exotic visual owns the renderer (the shield engage bloom or the shatter
             // overlay) the companion entity is hidden, so a ONE-SHOT stamp would be spent
             // invisibly. Those morphs are already motion; let them carry the frame.
-            if (!prism.UsesEntityColorSink && !prism.TryEnsureRenderEntityForStamp())
-                return false;
+            //
+            // Test the exotic flag ALONE. `UsesEntityColorSink` bundles it with handle
+            // usability, and `TryEnsureRenderEntityForStamp` returns true on its first line
+            // whenever the handle is usable — so the obvious-looking
+            // `!UsesEntityColorSink && !TryEnsure...` is inert in exactly the case it is meant
+            // to catch (exotic active, handle fine) and stamps anyway.
+            if (prism.ExoticVisualActive) return false;
 
             float duration = config.Duration;
             var packed = config.PackParams(impactSpeed);
@@ -134,9 +139,11 @@ namespace CosmicShore.Gameplay
             if (sharedMat && !sharedMat.HasProperty(JiggleStartTimeId))
                 PrismClockDiagnostics.WarnUnwiredMaterial(sharedMat, "_JiggleStartTime", prism);
 
+            // Written BEFORE the settle is scheduled: the settle's guard reads this back off the
+            // prism to prove it is still the current stamp.
             lastStampTime = now;
             ExpandCullingEnvelope(prism, config);
-            ScheduleSettle(prism, duration);
+            ScheduleSettle(prism, duration, now);
             return true;
         }
 
@@ -159,29 +166,79 @@ namespace CosmicShore.Gameplay
             PrismRenderService.ResetBoundsToMesh(in prism.RenderHandle, mesh);
 
             // Rotation is about the prism's object ORIGIN, so the farthest-travelling vertex is
```

</details>

### `76d549fcb` — docs(skills): capture the session's traps in asset-surgery and ship

_Claude, 2026-08-15 05:53:27 +0000_

```text
asset-surgery:
- splice traps: when a splice needs 'the node's OTHER input', derive it from the
  node's slot set, never from the edge that led you there. Walking
  PrismGrowScale.Scale's edge hands you the slot Scale feeds, and retargeting that
  cuts the feature's own feeder out of the chain. Caught by validate-before-write.
- guid uniqueness must be scoped to .meta files: a script guid legitimately appears
  in its own .meta AND in every asset referencing it, so 'appears in exactly one
  file' is the wrong invariant and fails on correct wiring.
- extends the clang-harness technique: use it to MEASURE a bound the CPU then has to
  encode (a culling envelope), not only to check correctness - and sweep the
  parameter space you actually ship, since measuring only the easy case is how an
  under-sized envelope gets through.

ship:
- the doc-section-collision trap also applies against a document's OWN past: a
  migration-tracker row id, a bug id, a test id. The last row is not the highest
  (PRISM_ANIMATION.md runs C1..C13b with C6 mid-table), so grep for the id before
  writing it into code, docs, tool docstrings and commit messages.
```

```text
 .claude/skills/asset-surgery/SKILL.md | 26 ++++++++++++++++++++++++++
 .claude/skills/ship/SKILL.md          |  6 ++++++
 2 files changed, 32 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/.claude/skills/asset-surgery/SKILL.md b/.claude/skills/asset-surgery/SKILL.md
index 0d2b34dd8..aa5b877ce 100644
--- a/.claude/skills/asset-surgery/SKILL.md
+++ b/.claude/skills/asset-surgery/SKILL.md
@@ -95,6 +95,14 @@ every other block is one object keyed by 32-hex `m_ObjectId`.
   `.meta` yourself. Slot integer ids must match the HLSL parameter order.
   PropertyNodes: clone an existing one, point `m_Property.m_Id` at the target.
   Register every node in `GraphData.m_Nodes`.
+- **When a splice needs "the node's OTHER input", DERIVE it from the node's slot set —
+  never from the edge that led you to the node.** Finding the grow `Multiply` by walking
+  `PrismGrowScale.Scale`'s outgoing edge hands you the slot Scale feeds (`B`); the slot you
+  actually want to intercept is the one it does NOT feed (`A`). Returning the wrong one
+  retargets the *feature's own* feeder into your new node and quietly cuts it out of the
+  chain. Take the node's input-slot ids, subtract the one you arrived on, and `assert` exactly
+  one remains. This is cheap to get wrong and free to catch — validate-before-write flagged it
+  with `PrismGrowScale.Scale no longer feeds a Multiply` and nothing was written.
 - **Splice an edge**: edges are
   `{m_OutputSlot:{m_Node:{m_Id}, m_SlotId}, m_InputSlot:{...}}`. To intercept
   a feed, RETARGET the existing edge's end (don't add a duplicate feeder into
@@ -630,6 +638,17 @@ This also verifies a source-rewriting tool end to end: bake values with the tool
 regexes, compile the result, and confirm the round-trip back to the original values is
 byte-identical.
 
+**Use the harness to MEASURE a bound the CPU then has to encode, instead of guessing it.**
+Compiling for correctness is the obvious use; the higher-value one is deriving a number that
+must live on the other side of the CPU/GPU boundary. A vertex-displacing effect needs a
+`RenderBounds` envelope, and the padding is whatever the shader can actually displace — so
+sweep the shipped entry point over the real geometry and every t in the animation window, and
+report peak displacement as a RATIO of the quantity the CPU already has (`radius × amplitude`
+measured at 0.991, so 1.25 ships with headroom). The constant then arrives with its
+derivation attached, and re-running the harness after any shader edit re-checks it. Assert the
+ratio in the harness, so a later change to the motion that widens the envelope fails there
+rather than as prisms popping at the screen edge.
+
 ## 4.6 Technique: hand-authoring a new asset trio
 
 Adding a new SO-configured, prefab-backed thing (here: a cell) means four
@@ -859,6 +878,13 @@ that would otherwise cost a round-trip to a human at the editor:
   prefab with malformed data it spills native parse errors and callstacks before your
   code runs. An auditor that dies on the bad data it exists to find is worse than no
   auditor. Reserve `LoadPrefabContents` for tools that WRITE.
+- **A guid-uniqueness assertion must be scoped to `.meta` files.** "This new guid appears in
+  exactly one file" is the wrong invariant and produces a false failure the moment the guid is
+  *used*: a script guid legitimately appears in its own `.cs.meta` AND in every asset whose
+  `m_Script` points at it. The real invariant is **exactly one `.meta` OWNS a guid**; every
+  other hit is a reference and is evidence the wiring worked. Assert
+  `len(grep -rl <guid> Assets --include=*.meta) == 1`, and print the referencing files rather
+  than failing on them.
 - **A "dangling GUID on this prefab" is usually project-wide.** Before treating
   a missing asset reference as a local bug, grep the WHOLE Assets tree for that
   guid: a reference broken on four flora prefabs turned out to be broken on
diff --git a/.claude/skills/ship/SKILL.md b/.claude/skills/ship/SKILL.md
index 0beb62734..ca60c6645 100644
--- a/.claude/skills/ship/SKILL.md
+++ b/.claude/skills/ship/SKILL.md
@@ -43,6 +43,12 @@ run the `/reorient` skill first and act on its verdict before shipping.
   same new **doc section number** (both took `§4.6`) merges clean per-hunk and produces a
   document with two of them. When you renumber, renumber every inbound reference — and
   only YOURS: grep the whole repo, then split the hits by which section they mean.
+  **The same collision happens WITHOUT a second branch, against the document's own past** —
+  a migration-tracker row id (`C6`), a bug id (`B10`), a test id. You pick the "next" id by
+  reading the last row, and the last row is not the highest: `PRISM_ANIMATION.md`'s tracker
+  runs C1…C13b with C6 sitting mid-table. Grep for the id you intend to claim BEFORE writing
+  it into code comments, doc prose, tool docstrings and commit messages — by the time you
+  notice, it is spread across a dozen files and the fix is a sweep, not an edit.
 - Restate, in a few sentences, WHAT the branch delivers and WHY. If you can't, you are
   not ready to ship — go re-read the diff.
 
```

</details>

### `04cc16ce9` — docs(prisms): correct the skim-deflection claim — four of five skimmers never reach the gate

_Claude, 2026-08-15 05:57:47 +0000_

```text
Playtest showed the deflection on vessel impact (as desired) and NOT on skimmer
impact (also as desired), which contradicted what both docs asserted.

The docs claimed "a skim IS a hit" fleet-wide, generalised from the fact that
SkimmerDamagePrismEffectSO funnels into Prism.Damage with no shield check. That is
true about the SO and false about the fleet: the effect is authored once and
referenced by exactly ONE of the five skimmer containers
(RhinoForceFieldSkimmerImpactorDataContainer). Dolphin, Manta-overcharge, Sparrow and
Squirrel skimmers never call Prism.Damage, so they cannot deflect anything.

This is the same shape CLAUDE.md already records for the prism-collision slow, where
three docs asserted a fleet-wide consequence for vessels that had no speed effect
wired — a correct statement about an effect SO is not a statement about who runs it.
Both docs now say so explicitly and point at the container as the thing to check.

Phase 9 of the wiring checklist is retitled from PLAYTEST OUTSTANDING to FIRST
PLAYTEST PASSED, recording what was confirmed (clean graph import, deflection on
vessel impact, no skim deflection) and what has not been exercised yet.
```

```text
 Docs/PRISM_ANIMATION.md              | 24 ++++++++++++++++++------
 Docs/PRISM_CLOCK_WIRING_CHECKLIST.md | 17 +++++++++++------
 2 files changed, 29 insertions(+), 12 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/PRISM_ANIMATION.md b/Docs/PRISM_ANIMATION.md
index 1c6567b78..c17639d4f 100644
--- a/Docs/PRISM_ANIMATION.md
+++ b/Docs/PRISM_ANIMATION.md
@@ -1647,12 +1647,24 @@ on every vessel whose skimmer container carries `SkimmerDamagePrismEffectSO` the
 already reaches `Prism.Damage`, so routing it too would only double-fire into the rate limit.
 (2) The AstroLeague ball/field sweeps and `Fauna.IsShieldedMass` read the flag to *skip*
 super-shielded mass before any hit is dispatched; those are filters, not gates, and there is no
-deflection to show. **A SKIM does deflect**, because a skimmer contact runs the full
-`SkimmerPrismEffectSO` chain at the stella surface and `SkimmerDamagePrismEffectSO` funnels into
-`Prism.Damage`. That is intended — the shield reacting to a skim is the same event class — but it
-means a vessel riding a super-shielded track lining wobbles each prism it passes (once per prism
-per pass, bounded by `minSecondsBetweenStamps`). If that reads as too busy on the Skim Race /
-Astro League lining, the dial is `minTiltDegrees`, not a new exception.
+deflection to show.
+
+**A SKIM does NOT deflect on four of the fleet's five skimmers — verified by playtest, and the
+reason is wiring, not design.** Reaching the gate requires `SkimmerDamagePrismEffectSO` in the
+skimmer's own effect container, and that effect is authored ONCE
+(`_SO_Assets/Effects/Skimmer Prism Effects/SkimmerDamagePrismEffect.asset`) and referenced by
+exactly one container: `RhinoForceFieldSkimmerImpactorDataContainer`. Dolphin, Manta-overcharge,
+Sparrow and Squirrel skimmers never call `Prism.Damage`, so they never deflect anything. The
+Rhino's forcefield skimmer is the sole vessel that can, which is consistent — that is its
+shield-swipe surface, where damaging mass is the point. (`RhinoSkimmerDamagePrismEffectSO`, a
+different SO, branches super-shield contact into `BounceBack` before any damage call.)
+
+This is worth stating explicitly because the opposite reads as obviously true: the effect SO does
+funnel into `Prism.Damage` with no shield check, so "a skim is a hit" is correct *about the SO*
+and wrong *about the fleet*. Whether a given vessel deflects on skim is a question about that
+vessel's container, not about this feature — the same class of claim CLAUDE.md already records
+for the prism-collision slow, which three docs asserted fleet-wide for vessels that had no speed
+effect wired at all. Check the container before repeating either claim.
 
 **Costs.** Zero per-frame CPU: one stamp per hit (rate-limited per prism, default 0.12 s — a swept
 piercing projectile re-dispatches the same prism every frame it overlaps, a drone swarm re-queues
diff --git a/Docs/PRISM_CLOCK_WIRING_CHECKLIST.md b/Docs/PRISM_CLOCK_WIRING_CHECKLIST.md
index ea741984d..2a306c4f6 100644
--- a/Docs/PRISM_CLOCK_WIRING_CHECKLIST.md
+++ b/Docs/PRISM_CLOCK_WIRING_CHECKLIST.md
@@ -397,7 +397,12 @@ polygon or a wall as large as the gradient band cannot resolve the gradient.
 that the vessel spawned through `VesselController.Initialize` with `IPlayer.IsLocalPilot`
 true; then run the validator above.
 
-## Phase 9 — Super-shield deflection jiggle (C14) — WIRED PROGRAMMATICALLY, **PLAYTEST OUTSTANDING**
+## Phase 9 — Super-shield deflection jiggle (C14) — WIRED PROGRAMMATICALLY, **FIRST PLAYTEST PASSED**
+
+> **2026-08-15:** confirmed in-editor — both graphs import clean (nothing magenta), and the
+> deflection reads on vessel impact. Skimmer impact correctly produces none (step 6). The
+> steps below stay as the regression checklist; steps 2, 3 and 5 have not been
+> specifically exercised yet.
 
 A super-shielded prism that is HIT but not destroyed now wobbles and settles instead of
 absorbing the hit in total silence. Design + rationale: `Docs/PRISM_ANIMATION.md §4.8`.
@@ -434,11 +439,11 @@ segments. Shoot or ram a super-shielded prism and watch:
    blast. Ordinary (non-super-shielded) prisms must be visually identical to before.
 5. **Neighbours are not in lockstep.** A blast that touches several super-shielded prisms
    should make them wobble out of phase with each other.
-6. **Skimming a super-shielded lining.** A skim IS a hit — it funnels through
-   `SkimmerDamagePrismEffectSO` → `Prism.Damage` → the gate — so riding the Skim Race / Astro
-   League lining wobbles each prism as you pass it. Intended; judge whether it reads as the
-   shield reacting or as visual noise. If it is too busy, lower `minTiltDegrees` — do not add a
-   skim exception.
+6. **Skimming does NOT deflect, on every vessel but one.** Confirmed by playtest 2026-08-15
+   (jiggle on vessel impact, none on skimmer impact — both as desired). Reaching the gate needs
+   `SkimmerDamagePrismEffectSO` in that skimmer's container, and only
+   `RhinoForceFieldSkimmerImpactorDataContainer` has it. If you ever want a skim to deflect on
+   another vessel, that is a container change on that vessel — not a change here.
 
 **Tuning** — `Resources/PrismSuperShieldJiggleConfig`:
 
```

</details>

### `d811e0344` — docs(palette): record the heart crossing's playtest steps and the session's techniques

_Claude, 2026-08-15 05:58:56 +0000_

```text
- PALETTE.md section 6 gains step 2d for the heart crossing, led with the point
  that it is the ONE crystal that must not be lime (section 2c's "every crystal
  is lime" is about free pickups), and naming Charge and Time as the two species
  that fail first because their materials are the lime ones.
- asset-surgery skill: section 4.8, proving a runtime VISUAL claim offline by
  walking prefab instance -> renderer materials -> authored .mat values, plus the
  trap that made this bug survive - a symptom asymmetric across variants, where
  two of four crystals looked correct because their authored fallback happened to
  equal the intended output.
- ship skill section 1: a parallel branch may fix the same root cause while you
  work. Take one implementation wholesale, delete the machinery only yours needed,
  and re-scope your docs so the repo does not carry two narratives of one bug.
```

```text
 .claude/skills/asset-surgery/SKILL.md | 31 +++++++++++++++++++++++++++++++
 .claude/skills/ship/SKILL.md          | 11 +++++++++++
 Docs/PALETTE.md                       | 13 +++++++++++++
 3 files changed, 55 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 88 lines)</summary>

```diff
diff --git a/.claude/skills/asset-surgery/SKILL.md b/.claude/skills/asset-surgery/SKILL.md
index 83fb8ce66..05a680745 100644
--- a/.claude/skills/asset-surgery/SKILL.md
+++ b/.claude/skills/asset-surgery/SKILL.md
@@ -721,6 +721,37 @@ that would otherwise cost a round-trip to a human at the editor:
   mesh's "nose" by comparing cross-section extents near each end of its long axis (the
   radially-symmetric end is the nose, the asymmetric one is the fins).
 
+## 4.8 Technique: prove a runtime VISUAL claim offline, by walking to the authored value
+
+"Why is this the wrong colour?" is answerable without Unity, because every step of what a
+renderer shows is a serialized reference. Walk the chain and print it:
+
+1. **Who owns the visual** — for a nested prefab (a lifeform's crystal, a vessel's part), the
+   host prefab holds a `PrefabInstance` whose `m_SourcePrefab` guid names the real source
+   asset. The host's own YAML shows only `stripped` stubs, so a naive grep for the component's
+   script guid finds a block with no fields and reads as "unwired" when it is simply inherited.
+2. **Which material the RENDERER actually uses** — read `m_Materials` off the `MeshRenderer`
+   (and any `m_Modifications` entry whose `propertyPath` is `m_Materials.Array.data[N]`, which
+   overrides it). This is frequently NOT the material named in the component's own fields: a
+   `Crystal` lists `defaultMaterial`/`inactiveMaterial` for its *transitions* while the renderer
+   is authored with one of them, and only the renderer's is on screen at rest.
+3. **The authored values** — `.mat` files carry the properties under `m_SavedProperties`; resolve
+   the guid via the `.meta` sweep. Compare those numbers against the live SO the code says it
+   reads (`ThemeManagerDataContainer.asset` → `ColorSet`).
+
+That chain proved, with no editor, that `ChargeCrystalMaterial._BrightCrystalColor` is *exactly*
+`EnvironmentColors.BrightCTA` — i.e. the fallback colour and the intended colour were the same
+value, which is why one of the four elements looked correct while the mechanism producing it was
+entirely dead.
+
+- **A symptom that is asymmetric across variants is the tell, and it hides the bug.** The same
+  broken mechanism produced a correct-looking result on two of four crystal prefabs (Mass and
+  Space author the *Blue* material on their renderer; Charge and Time author the lime one), so
+  half the ecosystem looked right by accident. Before concluding "it works for X so the system
+  works", check whether X's authored fallback happens to equal the intended output. Count the
+  affected content too — 21 species assets per element turned "two crystals look odd" into "half
+  the ecosystem", which is what set the priority.
+
 ## 5. Traps learned the hard way (check these BEFORE debugging for an hour)
 
 - **Renaming a Unity SERIALIZED FIELD must sweep `Tools/**.py` too, not just C# + scenes +
diff --git a/.claude/skills/ship/SKILL.md b/.claude/skills/ship/SKILL.md
index 0beb62734..6ace6188e 100644
--- a/.claude/skills/ship/SKILL.md
+++ b/.claude/skills/ship/SKILL.md
@@ -43,6 +43,17 @@ run the `/reorient` skill first and act on its verdict before shipping.
   same new **doc section number** (both took `§4.6`) merges clean per-hunk and produces a
   document with two of them. When you renumber, renumber every inbound reference — and
   only YOURS: grep the whole repo, then split the hits by which section they mean.
+- **A parallel branch may have fixed the SAME root cause while you worked.** Read the base
+  branch's new commits by subject before you resolve anything — this is not a merge
+  conflict, it is a design collision, and git will happily interleave two fixes for one
+  bug into a tree that carries both. When it happens: pick ONE implementation on merit and
+  take it **wholesale** (`git checkout --theirs <file>`), then delete the machinery yours
+  needed and theirs does not — a half-merged pair of fixes is worse than either. Then
+  **re-scope your docs to what is still genuinely yours**: your section was written when
+  you owned the whole story, and left as-is the repo ends up with two competing narratives
+  of one bug. Reference theirs rather than restating it, and keep only the part they do
+  not cover. Expect this whenever the base branch touched the same files — check with
+  `git log --oneline <merge-base>..origin/<base> -- <your changed files>`.
 - Restate, in a few sentences, WHAT the branch delivers and WHY. If you can't, you are
   not ready to ship — go re-read the diff.
 
diff --git a/Docs/PALETTE.md b/Docs/PALETTE.md
index 94b02d8ea..88b1d598f 100644
--- a/Docs/PALETTE.md
+++ b/Docs/PALETTE.md
@@ -504,6 +504,19 @@ Machine validation covers structure and colorimetry; only a playtest covers *loo
       drift means something is scaling the pair non-uniformly, or a channel is clipping
       past 1.0 (tonemapping is None — there is no shoulder to absorb it).
 
+2d. Check the **heart crossing** (§2.3) — note it is the one crystal that must NOT be lime:
+   a living lifeform's heart wears the blue-white neutral, and only a *free* crystal is lime.
+   - Fly a cell with wildlife (Menu\_Main freestyle, Wildlife Blitz). Confirm every living
+     lifeform's heart is **blue** — check a **Charge** and a **Time** species specifically
+     (`Arbor Flora Charge`, `Tadpole Fauna Time`): their materials are the lime ones, so
+     they are the two that fail first if the tint is lost again.
+   - Kill one and watch the heart **ease** blue → lime over ~0.8 s rather than flicking.
+     Tune on the crystal prefab's **Color Transition Seconds** (0 snaps).
```

</details>

### `87b7f7b09` — style(crystal): rewrap a comment line

_Claude, 2026-08-15 05:59:23 +0000_

```text
 Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs | 4 ++--
 1 file changed, 2 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs b/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
index be2e7d0fa..65e77b17d 100644
--- a/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
+++ b/Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs
@@ -256,8 +256,8 @@ namespace CosmicShore.Gameplay
         // It differs from the prism path in ONE respect, deliberately. A prism hands the
         // interpolation to the GPU because thousands animate at once and its graphs carry the
         // clock wiring; the crystal shaders carry none (Docs/PALETTE.md section 2.2 audits all
-        // of them), so a crystal's pair is pushed from the CPU. That is bounded by the crystals actually
-        // TRANSITIONING - a heart changes colour once, when it dies - and is strictly cheaper
+        // of them), so a crystal's pair is pushed from the CPU. That is bounded by the crystals
+        // actually TRANSITIONING - a heart changes colour once, when it dies - and is cheaper
         // than the cloned-material lerp it runs alongside. The scheduled settle is what makes
         // the end state independent of the driver: interrupt the coroutine and the crystal still
         // lands on its final colour.
```

</details>

### `492843a17` — docs(palette): record the GPU-clock and theft-decay follow-ups

_Claude, 2026-08-15 06:00:06 +0000_

```text
 Docs/PALETTE.md | 17 +++++++++++++++++
 1 file changed, 17 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/PALETTE.md b/Docs/PALETTE.md
index 88b1d598f..233975292 100644
--- a/Docs/PALETTE.md
+++ b/Docs/PALETTE.md
@@ -577,3 +577,20 @@ Machine validation covers structure and colorimetry; only a playtest covers *loo
 - The `Outside`/`Inside` field names are misleading (§2). Renaming them is a broad,
   GUID-safe but wide-reaching refactor across `SO_ColorSet` + `ThemeManager` + every
   colour set asset — worth doing, not worth bundling with a palette tune.
+- **The crystal crossing (§2.3) interpolates on the CPU because the crystal shaders carry no
+  clock wiring.** The prism path hands the same job to the GPU: `_ColorStartTime` +
+  `_ColorDuration` + the start pair as per-instance properties, and the graph does
+  `lerp(from, to, smoothstep(...))` itself (`MaterialPropertyAnimator.ClockColorTransition`,
+  `Docs/PRISM_ANIMATION.md §4.1`). Wiring it into the crystal graphs would delete the CPU
+  driver outright and make the crossing free. It is real work — `ShepardGraph` (259 nodes),
+  `CrystalGraph` (270), `InverseDynamicFresnelGraph` (135), `DynamicFresnelGraph` (75), plus
+  the hand-written `ChargeCrystal.shader`, which is the easy one — and it is
+  `/asset-surgery` §2 territory (ShaderGraph JSON synthesis), not hand-editing. Worth its own
+  change with its own playtest; the CPU driver is bounded by crystals actually transitioning,
+  so there is no urgency.
+- **`ChangeDomain`'s theft-decay path still snaps.** `ChangeDomain(Domains.Blue)` lerps the
+  MATERIAL back to `defaultMaterial` over 2 s and then the tail `ApplyColorSetTint()` snaps to
+  the CTA lime, because the two do not settle on the same colour. Pre-existing, and not fixed
+  by §2.3 (which only covers `ActivateCrystal`). The fix is the same mechanism — capture the
+  displayed pair before the material lerp starts and cross-fade the tint alongside it — but it
+  needs the theft path play-tested, which nothing currently exercises often.
```

</details>

### `c7167c168` — docs(ecology): record the flare x dimming coupling the merge created

_Claude, 2026-08-15 06:22:15 +0000_

```text
PR #724's CTA-lime pass dims an elemental crystal's resting colour to 0.45 of
the omni's, and the capture flare scales whatever the crystal currently wears.
The two compose correctly - a captured elemental flares 3x relative to itself
and peaks just above the omni's resting brightness - but flareGain and
ElementalCrystalDimming are now coupled, so moving one means re-judging the
other. Both go through the shared Color.ScaleRGB, so neither can shift hue.

Also cross-references asset-surgery §4.9b against the §5 dead-m_Modification
trap that landed in the same window: §5 says record-don't-hand-edit for
PrefabInstance OVERRIDE entries, §4.9b hand-edits plain serialized keys in a
directly-serialized component block whose C# field went away in the same commit.
Different cases; the note says which is which and to assume §5 when unsure.
```

```text
 Docs/ECOSYSTEM.md | 10 ++++++++++
 1 file changed, 10 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/ECOSYSTEM.md b/Docs/ECOSYSTEM.md
index 2405aa9de..babf81e97 100644
--- a/Docs/ECOSYSTEM.md
+++ b/Docs/ECOSYSTEM.md
@@ -3732,6 +3732,16 @@ a colour is a rate in linear HDR, so a gain brightens without shifting hue, and
 would read as a *different* crystal (`Docs/PALETTE.md`). No new FMOD event was added — the pickup now
 reaches the shared `CrystalCollect` category it always should have.
 
+**The flare composes with the omni/elemental brightness split** (the CTA-lime pass that landed
+alongside this branch): `ApplyCaptureVisual` scales whatever the crystal currently *wears*, and an
+elemental's resting colour is now the CTA dimmed by `EnvironmentColors.ElementalCrystalDimming`
+(0.45). So a captured elemental flares 3× **relative to itself** — which is the ratio the eye reads
+over a 0.44 s beat — peaking at ~1.35× the CTA, i.e. just above the omni's resting brightness rather
+than the 3× absolute the gain was first chosen against. That is the intended relationship (a crystal
+being taken briefly outshines the hero pickup), but it means `flareGain` and
+`ElementalCrystalDimming` are coupled: **move one and re-judge the other.** Both scale RGB only
+through the shared `Color.ScaleRGB`, so neither can shift hue.
+
 **And that reach was itself broken, on far more than this branch's path.** `Crystal.PlayExplosionAudio`
 guarded on its `[Inject] AudioSystem` field, which is null on **every crystal that was not part of a
 loaded scene**: a lifeform's heart is `Instantiate`d by the cell's spawners, and *nothing* under
```

</details>

### `31344f776` — docs(prisms): correct the shield-easing claim — smoothstep is exact for the fleet, not for two prefabs

_Claude, 2026-08-15 06:23:33 +0000_

```text
The branch asserted "AnimationCurve.EaseInOut(0,0,1,1) == smoothstep, so the feel is
reproduced exactly" in six places without checking what the constructor actually
produces. Half of it was right and the half that was wrong was the half that ships.

Verified rather than assumed, per the ship protocol's find-the-PRODUCER gate:
Unity's own serialization of that constructor writes inSlope/outSlope 0 on both keys
(cross-checked on SpaceCrystalAnimator.shrinkCurve in MassSharkFauna.prefab and
MassBrittlestarFauna.prefab), and a zero-tangent Hermite IS 3t^2-2t^3. So every shield
whose component is added at runtime by PrismStateManager.Awake — every prism in the game
bar two — is reproduced exactly.

The two exceptions are the only assets that SERIALIZE the curves, and neither is at the
default: BlueBlock.prefab (live in Duel for Cell, Freestyle MP, 2v2, and both Recording
Studios) and OctahedronShieldTest.prefab carry a hand-altered variant with end tangents
2 — 2t^3-3t^2+2t, fast-slow-fast, the opposite shape, up to 0.192 away from smoothstep
mid-transition. They now ease like the rest of the fleet. Keeping them would have cost a
per-instance curve parameter for one prefab's drift, and since the C# fields are deleted
the deviation would not have survived the next save of either prefab anyway.

Corrected in PrismClockAnimation.hlsl, both shield tooltips, PRISM_ANIMATION.md §4.8 +
§5 B4, CLAUDE.md, and both checklists; PRISM_CLOCK_WIRING_CHECKLIST Phase 9 gains step 7
so the expected BlueBlock difference is not mistaken for a regression in playtest.
```

```text
 Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl          | 14 +++++++++-----
 Assets/_Scripts/Controller/Vessel/PrismOctahedronShield.cs          |  2 +-
 Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs |  2 +-
 CLAUDE.md                                                           |  2 +-
 Docs/PRISM_ANIMATION.md                                             | 23 ++++++++++++++++-------
 Docs/PRISM_CLOCK_WIRING_CHECKLIST.md                                |  9 +++++++++
 6 files changed, 37 insertions(+), 15 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 125 lines)</summary>

```diff
diff --git a/Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl b/Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl
index c763bec8e..1f6806643 100644
--- a/Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl
+++ b/Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl
@@ -244,11 +244,15 @@ void PrismFlightSqrDistance_float(float Clock, float StartTime, float Duration,
 //   engage  (Direction >= 0):  p = centroid + t*(v - centroid)
 //   shatter (Direction <  0):  p = centroid + (1-t)*(v - centroid) + t*Offset*n
 //
-// t is smoothstep(0,1,progress) — EXACTLY the authored AnimationCurve.EaseInOut(0,0,1,1)
-// both shields shipped with (a Hermite with zero end tangents IS 3p^2-2p^3), so the
-// migration is faithful, not merely similar. The curve fields are retired with the
-// CPU driver: an arbitrary AnimationCurve has no GPU evaluation, and the whole fleet
-// authored the default.
+// t is smoothstep(0,1,progress), which IS AnimationCurve.EaseInOut(0,0,1,1) — a Hermite
+// with zero end tangents is 3p^2-2p^3 (Unity's own serialization of that constructor
+// carries inSlope/outSlope 0; cross-checked on SpaceCrystalAnimator.shrinkCurve). Every
+// shield whose component is added at RUNTIME therefore animates identically to before.
+// The two prefabs that serialize the curve (BlueBlock, OctahedronShieldTest) carry a
+// hand-altered variant with end tangents 2 — 2p^3-3p^2+2p, fast-slow-fast, up to 0.19
+// away from smoothstep — and now ease like the rest of the fleet. The curve fields are
+// retired with the CPU driver: an arbitrary AnimationCurve has no GPU evaluation, and
+// smoothstep is the easing every other clock transition already uses (PrismColorLerp).
 //
 // Because the morph runs on the SETTLED shared mesh, a shielded prism never leaves
 // the instanced path: same-size shields batch into one draw through the entire
diff --git a/Assets/_Scripts/Controller/Vessel/PrismOctahedronShield.cs b/Assets/_Scripts/Controller/Vessel/PrismOctahedronShield.cs
index e722bfa32..9d4cb6ecf 100644
--- a/Assets/_Scripts/Controller/Vessel/PrismOctahedronShield.cs
+++ b/Assets/_Scripts/Controller/Vessel/PrismOctahedronShield.cs
@@ -71,7 +71,7 @@ namespace CosmicShore.Gameplay
         [SerializeField] private float massRatioShielded = OctahedronMeshGenerator.SHIELD_TO_BOX_VOLUME_RATIO;
 
         [Header("Engage Transition")]
-        [Tooltip("Duration of the face-bloom engage morph. 0 snaps instantly. Easing is smoothstep on the GPU — the exact curve this shield shipped with (AnimationCurve.EaseInOut).")]
+        [Tooltip("Duration of the face-bloom engage morph. 0 snaps instantly. Easing is smoothstep on the GPU, which IS AnimationCurve.EaseInOut(0,0,1,1) — the curve every runtime-added shield used. The retired curve FIELD is gone: the GPU cannot evaluate an arbitrary AnimationCurve.")]
         [SerializeField] private float engageDuration = 0.35f;
 
         [Header("Shatter (Disengage)")]
diff --git a/Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs b/Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs
index 2f6a36130..7be1fb8de 100644
--- a/Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs
+++ b/Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs
@@ -72,7 +72,7 @@ namespace CosmicShore.Gameplay
         [SerializeField] private float massRatioSuperShielded = StellatedOctahedronMeshGenerator.SUPER_SHIELD_TO_BOX_VOLUME_RATIO;
 
         [Header("Engage Transition")]
-        [Tooltip("Duration of the face-bloom engage morph. 0 snaps instantly. Easing is smoothstep on the GPU — the exact curve this shield shipped with (AnimationCurve.EaseInOut).")]
+        [Tooltip("Duration of the face-bloom engage morph. 0 snaps instantly. Easing is smoothstep on the GPU, which IS AnimationCurve.EaseInOut(0,0,1,1) — the curve every runtime-added shield used. The retired curve FIELD is gone: the GPU cannot evaluate an arbitrary AnimationCurve.")]
         [SerializeField] private float engageDuration = 0.45f;
 
         [Header("Shatter (Disengage)")]
diff --git a/CLAUDE.md b/CLAUDE.md
index 625f6939d..ad28e714a 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -2334,7 +2334,7 @@ All game code lives under `CosmicShore.*` with 8 primary namespaces:
 | Prism performance | `PrismStateManager`, `PrismTimerManager`, `BlockDensityGrid` (the CPU animation managers — `PrismScaleManager`/`MaterialStateManager`/`AdaptiveAnimationManager` — were deleted under the clock-material law; see `Docs/PRISM_ANIMATION.md`) + `PrismDebris` (batched pure-entity death VFX for **both** death visuals: a frame's prism deaths spawn as ONE `em.Instantiate(prototype, N)` batch per family — explosions AND fauna-consumption suctions — with full-duration clock animation and sweep-based batch retirement. A live explosion costs zero per-frame CPU; a live suction costs ONE `float3` (its convergence target MOVES — every implosion comes from `Prism.Consume` and every call site passes a live creature Transform — so the §1 exception rides a per-record refresh with a CPU-mirrored culling envelope). The per-death path is split by five `Prism.Destroy.*` markers. **The pooled `PrismExplosion`/`PrismImplosion` GameObjects are NOT a working visual fallback** — under strict clock mode an explosion with no render entity draws nothing and an implosion draws a static block, both loudly, by design; their live job is being the CONFIG source (mesh/material/layer/clamp band/duration) the batch reads off the pool prefab. Retiring them is tracked as `Docs/PRISM_ANIMATION.md` D4/§4.6.1 — a refactor, not a deletion) | `_Scripts/Controller/Managers/`, `_Scripts/Utility/Effects/` |
 | Worm colony kaiju | `WormFauna` (colony brain: follow-the-leader slither, apex-omnivore feeding — grazes prism mass AND devours creatures at the jaws AND hunts pilots, feeding-funded growth, mid-body-kill splitting, wound differentiation, boid separation between colonies) + `WormSegmentFauna` (`WormSegmentRole` Head/Body/Tail — danger prisms + elemental heart on the capitals, one high-volume core prism on the body) + `WormColonyConfigSO` (all tuning). Spawns via `WormColonyFaunaConfig`/`Worm Colony <Element>` species assets; wired into the Lifeform Matrix toy, deliberately in NO SpawnProfile (a boss is opt-in). Design record + invariant rulings + collider budget: `Docs/ECOSYSTEM.md` §23 | `_Scripts/Controller/Environment/FloraAndFauna/`, `_SO_Assets/Lifeforms/` |
 | Cell environments | `CellEnvironmentSpawnableBase` (shared deterministic lay/stream/noise contract) + `SpawnableAtlantis` (Scurry intensity 4, ~69k prisms) + the freestyle seven `SpawnableYggdra`/`Daedala`/`Orrery`/`Zephyr`/`Caldera`/`Geode`/`Ourobor` (~34-41k each, rolled by Menu_Main's Cell via `CellConfigDataSO.EnvironmentPrefab`). Two are built AROUND the nucleus and lay **nothing inside the node-control radius** (an authored environment in there pre-awards node control): **Caldera** — four inward-aimed volcanic massifs in tetrahedral symmetry, no ground plane (`Docs/ECOSYSTEM.md` §18.1) — and **Ourobor** — three interlocked ULTRAWIDE Möbius bands of rolling countryside with a cityscape on BOTH faces, so stalagmites become stalactites and no global "up" survives a lap (`§18.2`). Alongside them, **`SpawnableHesperides`** — the GARDEN cell, the one environment whose world is the **planting**: ~12k authored prisms of architecture (terraces, pergolas, trellises, aqueduct, hanging baskets, super-shielded orchard gate, danger brambles) that `Sow`s ~560 `FloraPlantingSite`s — each tagged with its ground kind (`FloraSiteKind`: Bed/Climb/Basket/Water/Ledge) — which the Cell hands to its ordinary flora spawner (`Cell.TryTakePlantingSite(cfg.PreferredSites, …)` → `Flora.SetPlantPositionOverride(pos, up)`), so a mature Hesperides reaches Yggdra's ~33k prisms by GROWTH — living, grazeable `PhyllotacticFlora` in eight forms (Arbor/Rosette/Frond/Coral/Spire/Tendril/Reed/Lantern) plus gyroid + Schwarz P topiary — not by lay. One growth model, forms are parameters; prisms are shaped by ROLE (stem spans its segment, leaf spans its reach and attaches to the stalk) with depth taper, per-prism jitter, cupped alternating whorls, gravity droop and spiral twist. See `Docs/ECOSYSTEM.md` §23. `EnvironmentLoadVeil` (gate-less scenes defer past boot then hold a connecting-style veil), `CellEnvironmentBaselineMeasurer` (FrogletTools > Ecology > Measure Cell Environment Baselines - PhaseThresholds must ride each measured baseline; see `Docs/ECOSYSTEM.md` §18) | `_Scripts/Controller/Environment/Spawning/`, `_Scripts/Controller/Environment/MiniGameObjects/`, `_Scripts/Editor/` |
-| Prism spatial index | `PrismSpatialIndex` (formerly `PrismAOERegistry`) — THE canonical spatial index of all live prism mass: Burst AOE damage queries + growth occupancy (`TryReserve` claim-before-spawn closes the disabled-collider spawn race) + bucket hash grid. One registration lifecycle (`Register`/`MarkDestroyed`/`MarkRestored`/`Unregister`/`UpdatePosition`), multiple query views. Do not build parallel spatial stores or query prisms via physics — see `Docs/SPATIAL_INDEX.md` | `_Scripts/Controller/Managers/` || Shield octahedra | `PrismOctahedronShield` (the SHIELDED state's octahedron: per-face bloom engage + shatter-overlay disengage, mass scales with volume; the COLLIDER stays the authored primitive box TRIGGER — the octahedron is a look-only change, because a convex-mesh trigger is invisible to trigger-skimmers and a convex-mesh solid is invisible to solid swipes, whereas the primitive box trigger is seen by both, exactly like an unshielded prism; shape-precise shielded collision is SHIPPED as the spatial-index shell tier: `PrismShellContactManager` + `PrismSpatialIndex.CollectShellContacts` + `ShieldShellMath` run an exact Burst narrowphase — sphere/capsule/OBB probes vs the octahedron and vs the stella as the NON-CONVEX union of its two tetrahedra (spike-tip grazes hit, inter-spike gaps inside the bounding box do not) — dispatching through the same AcceptImpactee effect chain while Skimmer/VesselImpactor suppress box-trigger dispatch for shell-owned pairs; see Docs/SPATIAL_INDEX.md § Shell view), `PrismStellatedOctahedronShield` (the SUPER-SHIELDED state's stellated octahedron / Stella Octangula — the Skim Race track look; engaged by `PrismStateManager.ActivateSuperShield` with the OPAQUE team material, reversed by `DeactivateShields`), testers, `OctahedronMeshGenerator` / `StellatedOctahedronMeshGenerator` (`PopulateMesh` + `GetSharedShieldMesh` quantized-geometry caches). **Both integrate with the instanced prism render path via the `SetExoticVisualActive` / `SetRenderMeshOverride` handoff — see the anti-pattern below on why a bare MeshFilter swap renders nothing.** **Both morphs are GPU-CLOCKED since 2026-08-15** (`Docs/PRISM_ANIMATION.md` §4.8, §5 B4 — the migration that deleted the last sanctioned CPU prism ticker, `PrismOctahedronShieldManager`): the generators bake each vertex's FACE CENTROID into TEXCOORD1, which makes the **cache-shared settled mesh also the morph mesh**, so engage and shatter are one `PrismShieldMorph_float` expression off four Hybrid-Per-Instance properties and same-size shields stay in ONE batch through the whole animation. Consequences to respect when editing: everything is FINAL AT t = 0 (`Engage` applies the entire shielded pose, then stamps — there is no completion callback, because the shader clamps at t = 1 which IS the settled shield); the stamp must be CLEARED at disengage and on pool reuse (the prism's own box mesh carries no centroids, so a live stamp would collapse it toward the object origin); the disengage overlay is batched pure-entity debris (`PrismShieldShatter`) and is deliberately **not cancellable** on re-engage, because deleting visible shards mid-flight breaks continuity of existence; and the per-face CPU mesh rebuilders (`PopulateMeshFaceScale`/`PopulateMeshFaceShatter`) and the `AnimationCurve` fields are RETIRED — `AnimationCurve.EaseInOut(0,0,1,1)` is exactly `smoothstep`, which is what the shader runs | `_Scripts/Controller/Vessel/`, `_Scripts/Utility/` |
+| Prism spatial index | `PrismSpatialIndex` (formerly `PrismAOERegistry`) — THE canonical spatial index of all live prism mass: Burst AOE damage queries + growth occupancy (`TryReserve` claim-before-spawn closes the disabled-collider spawn race) + bucket hash grid. One registration lifecycle (`Register`/`MarkDestroyed`/`MarkRestored`/`Unregister`/`UpdatePosition`), multiple query views. Do not build parallel spatial stores or query prisms via physics — see `Docs/SPATIAL_INDEX.md` | `_Scripts/Controller/Managers/` || Shield octahedra | `PrismOctahedronShield` (the SHIELDED state's octahedron: per-face bloom engage + shatter-overlay disengage, mass scales with volume; the COLLIDER stays the authored primitive box TRIGGER — the octahedron is a look-only change, because a convex-mesh trigger is invisible to trigger-skimmers and a convex-mesh solid is invisible to solid swipes, whereas the primitive box trigger is seen by both, exactly like an unshielded prism; shape-precise shielded collision is SHIPPED as the spatial-index shell tier: `PrismShellContactManager` + `PrismSpatialIndex.CollectShellContacts` + `ShieldShellMath` run an exact Burst narrowphase — sphere/capsule/OBB probes vs the octahedron and vs the stella as the NON-CONVEX union of its two tetrahedra (spike-tip grazes hit, inter-spike gaps inside the bounding box do not) — dispatching through the same AcceptImpactee effect chain while Skimmer/VesselImpactor suppress box-trigger dispatch for shell-owned pairs; see Docs/SPATIAL_INDEX.md § Shell view), `PrismStellatedOctahedronShield` (the SUPER-SHIELDED state's stellated octahedron / Stella Octangula — the Skim Race track look; engaged by `PrismStateManager.ActivateSuperShield` with the OPAQUE team material, reversed by `DeactivateShields`), testers, `OctahedronMeshGenerator` / `StellatedOctahedronMeshGenerator` (`PopulateMesh` + `GetSharedShieldMesh` quantized-geometry caches). **Both integrate with the instanced prism render path via the `SetExoticVisualActive` / `SetRenderMeshOverride` handoff — see the anti-pattern below on why a bare MeshFilter swap renders nothing.** **Both morphs are GPU-CLOCKED since 2026-08-15** (`Docs/PRISM_ANIMATION.md` §4.8, §5 B4 — the migration that deleted the last sanctioned CPU prism ticker, `PrismOctahedronShieldManager`): the generators bake each vertex's FACE CENTROID into TEXCOORD1, which makes the **cache-shared settled mesh also the morph mesh**, so engage and shatter are one `PrismShieldMorph_float` expression off four Hybrid-Per-Instance properties and same-size shields stay in ONE batch through the whole animation. Consequences to respect when editing: everything is FINAL AT t = 0 (`Engage` applies the entire shielded pose, then stamps — there is no completion callback, because the shader clamps at t = 1 which IS the settled shield); the stamp must be CLEARED at disengage and on pool reuse (the prism's own box mesh carries no centroids, so a live stamp would collapse it toward the object origin); the disengage overlay is batched pure-entity debris (`PrismShieldShatter`) and is deliberately **not cancellable** on re-engage, because deleting visible shards mid-flight breaks continuity of existence; and the per-face CPU mesh rebuilders (`PopulateMeshFaceScale`/`PopulateMeshFaceShatter`) and the `AnimationCurve` fields are RETIRED — `AnimationCurve.EaseInOut(0,0,1,1)` is exactly `smoothstep` (zero end tangents), which is what the shader runs, so every runtime-added shield is unchanged; `BlueBlock.prefab` and `OctahedronShieldTest.prefab` serialized a hand-altered curve and now ease like the fleet | `_Scripts/Controller/Vessel/`, `_Scripts/Utility/` |
 | Impact effects | `ImpactorBase` + 11 impactor types, 20+ Effect SO types | `_Scripts/Controller/ImpactEffects/` |
 | Swing kinematics | `SkimmerSwingKinematics` (rigid-body velocity of any point on a skimmer that MOVES relative to its vessel — the Rhino's sword: `v = v_vessel + omega_vessel x r + R * v_rel`, every rate differentiated in the VESSEL's frame so translation/teleports can't leak in; `ClosestBladePoint`/`NormalizedAlongBlade` recover WHICH part of the blade a contact landed on, hilt/tip derived from the pivot, never authored) + `SkimmerSwingKinematicsConfigSO`; composed into impacts by `PrismEffectHelper.ContactVelocity` so a destroyed prism gets the velocity of the part that hit it (a tip strike, not the hull). Skimmers without the component collapse to the previous `Course * Speed` exactly. The magnitude survives to the screen via `PrismEffectHelper.DamageProportional`, which hands the debris velocity over **as final** — `Prism.Explode` passes it through untouched (the supplied `DebrisSpeedLimit` marks it) instead of applying the legacy `/ prismProperties.volume`. **That divide is dead code**: `SetupDestruction` disables the scale animator before reading the volume, `GetCurrentVolume()` returns 0 once disabled, so `Max(0,1)` pins the divisor to exactly 1 for every prism — the legacy gain is just `inertia`. Never pre-multiply by volume expecting it to cancel; the leftover is a straight volume multiplier that damps small prisms (a Rhino trail sliver is ~0.75) and pins large ones to the ceiling. Opt-in per effect (`proportionalDebris`) — on for the sword AND the hull (`VesselDamagePrismEffectSO`), since every vessel's `Inertia` is 1 and the legacy hull formula therefore landed under the clamp's FLOOR, making every ram produce an identical 30 u/s; with both proportional a hull hit and a parked-sword hit at the same velocity now impart the same magnitude. Debris ships at **1/3** the physical read via one tuning group that must move together — `restitution` + `debrisSpeedLimit` on the three damage SOs, `debrisRestitution` + `Inertia` on `AOEExplosion` (the **AOE blasts** joined the group; see below), and `minSpeed`/`maxSpeed` on `PrismExplosion.prefab` (the band also carries the clamp-bound legacy paths, so the retune is uniform). On the three damage SOs `inertia` is NOT the lever — proportional paths ignore it and legacy paths are saturated — but on an AOE blast running `proportionalDebris` it IS the single lever: the blast supplies its OWN ceiling, so `Inertia` scales throw AND shatter linearly, and `debrisRestitution x Inertia = 1` holds the pre-existing shatter rate. `restitution` also drives the shatter rate, so shatter violence tracks impact force. A parked sword must add exactly zero, so elongation (ambient shield scaling, +15/-5 u/s at the tip) defaults off, `restDeadbandSpeed` zeroes sub-threshold residue (which rectifies upward, `|v+n|>|v|`), and `AngularVelocity` reads the angle off the quaternion's vector part via `atan2` — `ToAngleAxis`/`acos` returns exactly zero below ~0.01 deg/frame in float32 and drops slow vessel rotation. See `_Scripts/Controller/Vessel/R_VesselActions/RHINO_SHIELD_SWIPE.md` § "Swing velocity model" | `_Scripts/Controller/Vessel/`, `_Scripts/Controller/ImpactEffects/EffectsSO/Helpers/` |
 | Forcefield crackle | `SkimmerForcefieldCracklePrismEffectSO` (computes impact points via `Collider.ClosestPoint`), `ForcefieldCrackleController` (`[ExecuteAlways]`, 16-impact ring buffer + MaterialPropertyBlock arrays, owns all visual params), `ForcefieldCrackle.hlsl` (FBM electrical arcs on geodesic sphere), `ForcefieldCrackleControllerEditor` (edit-mode preview) | `_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/`, `_Scripts/Controller/Vessel/`, `Assets/Materials/Graphs/`, `_Scripts/Editor/` |
diff --git a/Docs/PRISM_ANIMATION.md b/Docs/PRISM_ANIMATION.md
index fa1f606bc..a334a25f3 100644
--- a/Docs/PRISM_ANIMATION.md
+++ b/Docs/PRISM_ANIMATION.md
@@ -1647,12 +1647,21 @@ Five properties of the result are worth carrying to the next migration:
 - **A shatter is never cancelled.** The old `StopShatter()` deleted visible, mass-shaped
   geometry mid-flight on a re-engage — the thing "nothing pops out of existence" forbids.
   Re-engaging now lets the old shards finish while the new shield blooms.
-- **`AnimationCurve` retired, and the migration is faithful rather than merely similar.**
-  Both shields authored `AnimationCurve.EaseInOut(0,0,1,1)`, whose Hermite with zero end
-  tangents IS `3t² − 2t³` — so `smoothstep` reproduces the shipped feel exactly. An
-  arbitrary curve has no GPU evaluation, and the whole fleet authored the default (the
-  fields were serialized on `BlueBlock.prefab` and `OctahedronShieldTest.prefab`, both at
-  the default; the components are otherwise added in code by `PrismStateManager.Awake`).
+- **`AnimationCurve` retired — exact for the fleet, a deliberate change on two prefabs.**
+  `AnimationCurve.EaseInOut(0,0,1,1)` is a Hermite with **zero** end tangents, which IS
```

</details>

### `6b27b05f5` — docs(skills): asset-surgery — the serialized-vs-initializer trap, the out-param harness trap, compiler-first framing

_Claude, 2026-08-15 06:24:37 +0000_

```text
Three things this branch paid for and the next session should not:

- **A serialized value is not its C# field initializer.** Retiring a [SerializeField]
  with a non-trivial default is an equivalence CLAIM with two halves, both checkable
  offline: what the constructor actually produces (find another asset in the repo
  carrying the same initializer and read what Unity serialized — AnimationCurve.EaseInOut
  really is zero-tangent Hermite = smoothstep, proven from SpaceCrystalAnimator in about
  a minute), and which assets serialize the field at all and whether they sit at that
  default. Two shield prefabs did not — hand-altered tangents, 0.192 from smoothstep, on
  a prefab live in three multiplayer scenes. Silent and permanent: once the C# field is
  gone Unity drops the orphaned keys and the deviation vanishes with no diff.
- **Never `#define out` in a clang HLSL harness.** It compiles, makes every out-param
  pass by value, and prints 0.000 for everything — which reads as a shader logic bug and
  sends you debugging correct code. Rewrite `out float3 X` -> `float3 &X` in the extract.
  Uniform-zero harness output is a harness bug until proven otherwise.
- **Compiler-first framing.** The description said "C# verification without a compiler",
  which reads as "no compiler is available" and is the reason this session went straight
  to brace-balance while §4 said "get a real compiler first" and mcs installs in seconds.
  Both the C# and the HLSL on this branch compile; that was worth having before the PR.
```

```text
 .claude/skills/asset-surgery/SKILL.md | 27 ++++++++++++++++++++++++++-
 1 file changed, 26 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/.claude/skills/asset-surgery/SKILL.md b/.claude/skills/asset-surgery/SKILL.md
index e4d5fea40..12b170eb8 100644
--- a/.claude/skills/asset-surgery/SKILL.md
+++ b/.claude/skills/asset-surgery/SKILL.md
@@ -1,6 +1,6 @@
 ---
 name: asset-surgery
-description: Do "editor-only" Unity work programmatically instead of handing the human an in-editor checklist - ShaderGraph node wiring via JSON synthesis, prefab/scene YAML component surgery, SO asset re-authoring, C# verification without a compiler. Use whenever the plan is drifting toward "I'll prepare instructions and you do it in the editor", whenever a task touches .shadergraph/.prefab/.unity/.asset files directly, or when the human says some flavor of "you can do this" / "write a tool for it". The human's editor time is for PLAY TESTING and things that genuinely need the running editor - not for mechanical asset edits you can machine-validate.
+description: Do "editor-only" Unity work programmatically instead of handing the human an in-editor checklist - ShaderGraph node wiring via JSON synthesis, prefab/scene YAML component surgery, SO asset re-authoring, and REAL compilation of the C#/HLSL you are about to commit (mcs + clang are installable here - see 4 and 4.5c; do not settle for inspection). Use whenever the plan is drifting toward "I'll prepare instructions and you do it in the editor", whenever a task touches .shadergraph/.prefab/.unity/.asset files directly, or when the human says some flavor of "you can do this" / "write a tool for it". The human's editor time is for PLAY TESTING and things that genuinely need the running editor - not for mechanical asset edits you can machine-validate.
 ---
 
 # Asset Surgery — do it programmatically, prove it before writing
@@ -595,6 +595,13 @@ SUBS = [(r"\[unroll\]", ""),          # HLSL loop attribute
         (r"\bfloat2\(", "mk2(")]      # vector constructor spelling
 ```
 
+- **Rewrite `out` params to C++ references in the EXTRACT, never with a `#define out`.**
+  An empty `#define out` compiles clean and silently makes every out-param pass by VALUE,
+  so the harness runs, prints, and reports `0.000` for every result — which reads as a
+  logic bug in the shader and sends you debugging correct code. `out float3 X` →
+  `float3 &X` as a substitution on the extracted text (the SUBS list above already has the
+  scalar form; the vector forms need the same). A harness whose output is uniformly the
+  zero value is a harness bug until proven otherwise.
 - **`__attribute__((ext_vector_type(N)))` is the whole trick.** clang's vector types give
   you elementwise arithmetic and *arbitrary swizzles* (`.xyx`, `.yzx`, `.zy`) for free, so
   hash functions written for HLSL compile unmodified. Only the `floatN(a,b)` constructor
@@ -725,6 +732,24 @@ that would otherwise cost a round-trip to a human at the editor:
 
 ## 5. Traps learned the hard way (check these BEFORE debugging for an hour)
 
+- **A SERIALIZED value is not its C# field initializer — and the initializer's output is
+  not what you remember it being.** Retiring a `[SerializeField]` whose default comes from
+  a non-trivial expression (`AnimationCurve.EaseInOut(0,0,1,1)`, `new Gradient{…}`, a
+  computed `Vector3`) means claiming an equivalence, and that claim has TWO halves, both
+  checkable offline and both easy to get wrong:
+  (1) **What does the constructor actually produce?** Do not recall it — find another asset
+  in the repo whose field carries the *same* initializer and read the tangents/keys Unity
+  wrote. (`AnimationCurve.EaseInOut` really is zero-tangent Hermite = `smoothstep`;
+  `SpaceCrystalAnimator.shrinkCurve` on two fauna prefabs proved it in about a minute.)
+  (2) **Which assets serialize the field at all, and are they at that default?** Only
+  objects that were touched in the inspector carry a value; the rest take the initializer
+  at runtime. Here, exactly two of the shield prefabs serialized the curves and *neither*
+  was at the default — someone had dragged the tangents to 2, a fast-slow-fast shape 0.192
+  away from `smoothstep` at its worst, on a prefab live in three multiplayer scenes.
+  Sweep it mechanically: `grep -rl <scriptGuid> Assets --include=*.prefab --include=*.unity`
+  then parse the field block out of each hit. The failure mode is silent and permanent —
+  once the C# field is deleted, Unity drops the orphaned YAML keys on the next save, so the
+  authored deviation disappears with no diff that mentions it.
 - **Renaming a Unity SERIALIZED FIELD must sweep `Tools/**.py` too, not just C# + scenes +
   prefabs.** This repo authors scene/prefab YAML from Python generators
   (`Tools/Build/author_*_assets.py`), and several of them both WRITE and VALIDATE a field by
```

</details>

### `cb638c9d7` — docs(audit): add documentation audit with per-issue resolution prompts

_Claude, 2026-08-15 07:25:26 +0000_

```text
Machine-verified audit of all 145 first-party Markdown files. Every claim
that could be checked was checked against the tree: referenced paths against
the real file list, C# type names against every type declaration, enum values
and SO fields against source and asset YAML, section anchors against real
headers, plus paragraph-level duplicate detection.

29 findings across 7 categories, each with a ready-to-paste prompt that
resolves that issue in isolation. Highlights:

- DomainAssigner is documented in 4 places with a full API and does not
  exist in the codebase
- The vessel HUD table names 8 non-existent classes and marks 2 existing
  controllers as absent
- ECOSYSTEM.md sections 1-2 still teach the retired "prism count is the
  spine" model, contradicting the LOCKED volume invariant and Cell.cs
- GameModes.cs enum comments describe two designs that were explicitly
  reverted (Wildlife Liberation free-for-all, Ribcage shielded cage + fauna)
- Three different answers to the Tournament mode's display name
- 69 of 145 docs are unreachable from any index
- "Scurry" and "Skim Race" are used as established terms and defined nowhere

Docs-only; no source or asset changes.
```

```text
 Docs/DOCUMENTATION_AUDIT.md | 1494 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 1494 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 1500 lines)</summary>

```diff
diff --git a/Docs/DOCUMENTATION_AUDIT.md b/Docs/DOCUMENTATION_AUDIT.md
new file mode 100644
index 000000000..b6bfdd2cd
--- /dev/null
+++ b/Docs/DOCUMENTATION_AUDIT.md
@@ -0,0 +1,1494 @@
+# Documentation Audit — Cosmic Shore
+
+**Date:** 2026-08-15 · **Scope:** all 145 first-party Markdown files (`CLAUDE.md`, `README.md`,
+`PLAN.md`, `GIT_RULES.md`, `Docs/**`, `Assets/_Scripts/**/*.md`, `Tools/**`, `.claude/skills/**`).
+Third-party vendor docs (Plugins, PlayFabSDK, NiceVibrations, Wwise, ExternalDependencyManager,
+YethGameDev) are excluded.
+
+**Method.** Every claim that could be machine-checked was machine-checked against the tree at
+`claude/documentation-audit-p5ov1g`: referenced file paths resolved against the real file list,
+C# type names resolved against every `class`/`struct`/`interface`/`enum` declaration, enum values
+read from source, SO fields read from asset YAML, section anchors resolved against real headers,
+and paragraph-level duplicate detection across all docs. Findings below carry the evidence that
+produced them. Where a doc and the code disagree, the code was read to decide which side is wrong.
+
+---
+
+## How to use this document
+
+Each finding is self-contained and ends with a **Prompt** — a ready-to-paste instruction for a
+fresh session that fixes exactly that issue and nothing else. Prompts are written to be run
+independently and in any order, except where a dependency is stated.
+
+Prompts assume the agent will follow `CLAUDE.md` house rules (verify against code, don't invent,
+don't restructure without instruction). None of them ask for code changes unless explicitly noted
+(D-3, F-2 and G-1 touch source comments or add a tiny doc file; everything else is docs-only).
+
+**Severity:**
+
+| | Meaning |
+|---|---|
+| 🔴 **Critical** | Actively misleads. An agent or engineer following the doc will write wrong code or waste a session. |
+| 🟠 **High** | Wrong or contradictory, but the reader is likely to notice before acting. |
+| 🟡 **Medium** | Navigation, redundancy, or lifecycle problems. Costs time, not correctness. |
+| 🔵 **Low** | Polish, consistency, and future-proofing. |
+
+---
+
+## Index
+
+| # | Severity | Finding | Primary file(s) |
+|---|---|---|---|
+| **A. Docs contradict the code** | | | |
+| A-1 | 🔴 | Vessel HUD controller/view table names 11 classes; 8 do not exist, 2 marked absent do exist | `CLAUDE.md` |
+| A-2 | 🔴 | `DomainAssigner` is documented in 4 places and does not exist in the codebase | `CLAUDE.md`, `MultiplayerArchitecture/…/90-appendix-files.md` |
+| A-3 | 🔴 | `ECOSYSTEM.md` §1–§2 still teach "prism count is the spine"; the LOCKED invariant is volume | `Docs/ECOSYSTEM.md` |
+| A-4 | 🟠 | `FLEET_MAPS.md` records the Sparrow Mass-5/Space-5 swap that was reverted on 2026-08-13 | `Docs/ElementalAbilitySystem/FLEET_MAPS.md` |
+| A-5 | 🟠 | `PlayerCountStepper` documented as a real class; actual classes are `IntStepper` / `PlayerCountButton` | `CLAUDE.md` |
+| A-6 | 🟠 | HexRace key-files table names 4 classes that don't exist | `CLAUDE.md`, `Docs/SCENES.md` |
+| A-7 | 🟠 | Every file count in the Project Structure tree is understated by 20–55% | `CLAUDE.md` |
+| A-8 | 🟠 | "`_Scripts/Game/` contains only non-code assets" — it contains 3 `.cs` files, one of them live | `CLAUDE.md` |
+| A-9 | 🟠 | "8 primary namespaces" — there are 10 | `CLAUDE.md` |
+| A-10 | 🟡 | "17 test files" in `_Scripts/Tests/Editor/` — there are 54 | `CLAUDE.md` |
+| A-11 | 🟡 | Scene inventories miss 4–5 real scenes each; `SCENES.md` predates Dog Fight and Maelstrom | `CLAUDE.md`, `Docs/SCENES.md` |
+| A-12 | 🟡 | ~60 dangling file-path references across 30 docs | many |
+| **B. Docs contradict each other** | | | |
+| B-1 | 🔴 | Three different answers to "what is the Tournament mode's display name?" | `ShuffleSystem/`, `Docs/README.md`, `CLAUDE.md` |
+| B-2 | 🟠 | `Docs/README.md` bug-tracker summaries are stale in both directions | `Docs/README.md` |
+| B-3 | 🟡 | `Docs/README.md` calls the shipped Shuffle deltas "deferred / not yet built" | `Docs/README.md` |
+| **C. Navigation and discoverability** | | | |
+| C-1 | 🔴 | 69 of 145 docs are unreachable from any index | all indexes |
+| C-2 | 🟠 | `CLAUDE.md`'s Documentation Index omits `ECOSYSTEM.md` — the most cross-referenced doc in the repo | `CLAUDE.md` |
+| C-3 | 🟠 | `Docs/README.md` claims to be "the navigation index" but covers 12 of 44 entries | `Docs/README.md` |
+| C-4 | 🟡 | Root `README.md` game-mode list is ~2 years stale and lists modes that no longer exist | `README.md` |
+| **D. Redundancy and duplication** | | | |
+| D-1 | 🟠 | `MultiplayerArchitecture/src/content/` is a second, drifting copy of 6 subsystem docs | `Docs/MultiplayerArchitecture/` |
+| D-2 | 🟠 | `CLAUDE.md` inlines whole sections that exist as dedicated docs (~40% of its 292 KB) | `CLAUDE.md` |
+| D-3 | 🟡 | `GameModes.cs` enum comments duplicate — and contradict — the mode docs | `Assets/_Scripts/Data/Enums/GameModes.cs` |
+| D-4 | 🔵 | 6 byte-identical paragraph blocks duplicated across doc pairs | various |
+| **E. Lifecycle: ephemera parked in a reference tree** | | | |
+| E-1 | 🟠 | 8 session-scoped docs (kickoffs, handoffs, overnight logs) sit unmarked next to canonical refs; all cite dead branches | `Docs/*_KICKOFF.md`, `*_HANDOFF.md`, `*_LOG.md` |
+| E-2 | 🟠 | `PLAN.md` at repo root is a dead scratch plan for work that shipped differently | `PLAN.md` |
+| E-3 | 🟡 | `UNITY_VERIFICATION_CHECKLIST.md` has 14 🔴 and zero 🟡/🟢 — the close-out half has never run | `Docs/UNITY_VERIFICATION_CHECKLIST.md` |
+| **F. Ambiguity and missing definitions** | | | |
+| F-1 | 🔴 | Player-facing mode names are used as if defined but are documented nowhere ("Scurry", "Skim Race") | many |
```

</details>

_Also contains 36 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
