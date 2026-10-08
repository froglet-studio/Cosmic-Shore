# Branch archive: `claude/vessels-review-completion-5weidk`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-07-13 by Claude
- **Unmerged commits:** 9
- **Forked from:** `f2b8f5aa2` (2026-07-08, Merge pull request #581 from froglet-studio/claude/fly-by-numbers-enhancement-)
- **Tip:** `f5a0b6017`
- **Files touched (42):**
  - `Assets/Resources/VesselAbilitySets.meta`
  - `Assets/Resources/VesselAbilitySets/Dolphin.asset`
  - `Assets/Resources/VesselAbilitySets/Dolphin.asset.meta`
  - `Assets/Resources/VesselAbilitySets/Manta.asset`
  - `Assets/Resources/VesselAbilitySets/Manta.asset.meta`
  - `Assets/Resources/VesselAbilitySets/Rhino.asset`
  - `Assets/Resources/VesselAbilitySets/Rhino.asset.meta`
  - `Assets/Resources/VesselAbilitySets/Serpent.asset`
  - `Assets/Resources/VesselAbilitySets/Serpent.asset.meta`
  - `Assets/Resources/VesselAbilitySets/Sparrow.asset`
  - `Assets/Resources/VesselAbilitySets/Sparrow.asset.meta`
  - `Assets/Resources/VesselAbilitySets/Squirrel.asset`
  - `Assets/Resources/VesselAbilitySets/Squirrel.asset.meta`
  - `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/SkimmerOverchargeCollectPrismEffectSO.cs`
  - `Assets/_Scripts/Controller/Vessel/ElementalScaling.cs`
  - `Assets/_Scripts/Controller/Vessel/ElementalScaling.cs.meta`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ChargeBoostActionExecutor.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ConsumeBoostActionExecutor.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/GrowTrailActionExecutor.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/OverheatingActionExecutor.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ShardToggleActionExecutor.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/YawsteryActionExecutor.cs`
  - `Assets/_Scripts/Controller/Vessel/SparrowVesselTelemetry.cs`
  - `Assets/_Scripts/Editor/VesselAbilityIconValidator.cs`
  - `Assets/_Scripts/Editor/VesselAbilityIconValidator.cs.meta`
  - `Assets/_Scripts/ScriptableObjects/VesselAbilitySetSO.cs`
  - `Assets/_Scripts/ScriptableObjects/VesselAbilitySetSO.cs.meta`
  - `Assets/_Scripts/UI/AbilityIconPlaceholder.cs`
  - `Assets/_Scripts/UI/AbilityIconPlaceholder.cs.meta`
  - `Assets/_Scripts/UI/Controller/RhinoVesselHUDController.cs`
  - `Assets/_Scripts/UI/Controller/VesselAbilityBar.cs`
  - `Assets/_Scripts/UI/Controller/VesselAbilityBar.cs.meta`
  - `Assets/_Scripts/UI/Controller/VesselHUDController.cs`
  - `Assets/_Scripts/UI/View/DolphinVesselHUDView.cs`
  - `Assets/_Scripts/UI/View/MantaVesselHUDView.cs`
  - `Assets/_Scripts/UI/View/RhinoVesselHUDView.cs`
  - `Assets/_Scripts/UI/View/SerpentVesselHUDView.cs`
  - `Assets/_Scripts/UI/View/SparrowHUDView.cs`
  - `Assets/_Scripts/UI/View/SquirrelVesselHUDView.cs`
  - … and 2 more

### `7924a4e48` — fix(vessels): repair real runtime bugs across the flyable fleet

_Claude, 2026-07-09 03:00:18 +0000_

```text
Serpent — ConsumeBoostActionExecutor: replace multiplicative Mathf.Pow(4, stacks)
(256x at 4 charges, uncontrollable) with config-driven linear stacking
(_so.BoostMultiplier.Value * stacks → 4x..16x); remove per-recompute CSDebug.Log
spam; only play the BoostActivate SFX once a charge is actually consumed, not on
no-op presses (reloading / empty / stationary / no resource).

Dolphin — ShardToggleActionExecutor: guard the null Cell before GetExplosionTarget.
The toggle is bundled on the drift input, so cell-less modes (Menu_Main freestyle,
some minigames) threw a NullReferenceException on every left-stick drift.

Rhino — RhinoVesselHUDController: make Subscribe/Unsubscribe idempotent, re-init
via Unsubscribe→Subscribe, and add OnEnable re-subscribe. Fixes double-counted
slowed tallies on menu vessel-swap/ReInitializePair and dead indicators after a
disable→enable cycle.

Manta — SkimmerOverchargeCollectPrismEffectSO: prune Unity-destroyed SkimmerImpactor
keys from the process-wide static overcharge state when a new skimmer appears,
bounding the cross-scene leak/carryover.

Sparrow — SparrowVesselTelemetry: drop three Debug.Log diagnostics.
```

```text
 .../EffectsSO/Skimmer Prism Effects/SkimmerOverchargeCollectPrismEffectSO.cs   | 24 ++++++++++++++++++++++++
 .../Controller/Vessel/R_VesselActions/Executors/ConsumeBoostActionExecutor.cs  | 17 ++++++++++-------
 .../Controller/Vessel/R_VesselActions/Executors/ShardToggleActionExecutor.cs   |  4 ++++
 Assets/_Scripts/Controller/Vessel/SparrowVesselTelemetry.cs                    |  7 -------
 Assets/_Scripts/UI/Controller/RhinoVesselHUDController.cs                      | 16 ++++++++++++++--
 5 files changed, 52 insertions(+), 16 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ConsumeBoostActionExecutor.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ConsumeBoostActionExecutor.cs
index b26a086dd..098f7af7e 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ConsumeBoostActionExecutor.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ConsumeBoostActionExecutor.cs
@@ -91,8 +91,6 @@ namespace CosmicShore.Gameplay
         {
             if (!so || status == null) return;
 
-            audioSystem.PlayGameplaySFX(GameplaySFXCategory.BoostActivate);
-
             if (_status is { IsTranslationRestricted: true }) return;
         
             if (_so != so)
@@ -120,6 +118,10 @@ namespace CosmicShore.Gameplay
                 OnBoostStarted?.Invoke(_so.BoostDuration, res.CurrentAmount);
             }
 
+            // Only play the activation SFX once a charge is actually being consumed —
+            // not on no-op presses (reloading / empty magazine / stationary / no resource).
+            audioSystem.PlayGameplaySFX(GameplaySFXCategory.BoostActivate);
+
             int pipIndex = Mathf.Clamp(_available - 1, 0, _so.MaxCharges - 1);
             float duration = Mathf.Max(0.05f, _so.BoostDuration);
 
@@ -155,11 +157,12 @@ namespace CosmicShore.Gameplay
             {
                 _status.IsBoosting = true;
 
-                // linear stacking
-               // _status.BoostMultiplier = (_so ? _so.BoostMultiplier.Value : 4f) * stacks;
-                // multiplicative
-                 _status.BoostMultiplier = Mathf.Pow( 4f, stacks);
-                 CSDebug.Log($"Boost Multiplier Working {_status.BoostMultiplier}");
+                // Linear, config-driven stacking. The previous multiplicative Mathf.Pow(4, stacks)
+                // reached 256x at 4 charges — a balance bug that made Serpent uncontrollable. Each
+                // charge now adds one unit of the SO's authored per-charge boost multiplier
+                // (default 4 → 4x..16x across 1..4 charges).
+                float perCharge = _so ? _so.BoostMultiplier.Value : 4f;
+                _status.BoostMultiplier = perCharge * stacks;
             }
             else
             {
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ShardToggleActionExecutor.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ShardToggleActionExecutor.cs
index edce2e22a..f11ff4e4e 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ShardToggleActionExecutor.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ShardToggleActionExecutor.cs
@@ -42,6 +42,10 @@ namespace CosmicShore.Gameplay
             if (!_redirectActive)
             {
                 var cell = cellData.Cell;
+                // Cell-less modes (Menu_Main lava-lamp / freestyle, some minigames) have no
+                // active Cell. The toggle is bundled on the drift input, so without this guard
+                // every left-stick drift threw a NullReferenceException.
+                if (cell == null) return;
                 Vector3 highDensityPosition = cell.GetExplosionTarget(so.Domain);
                 shardFieldBus.BroadcastPointAtPosition(highDensityPosition);
                 _redirectActive = true;
diff --git a/Assets/_Scripts/Controller/Vessel/SparrowVesselTelemetry.cs b/Assets/_Scripts/Controller/Vessel/SparrowVesselTelemetry.cs
index 1de939d39..385e4ce59 100644
--- a/Assets/_Scripts/Controller/Vessel/SparrowVesselTelemetry.cs
+++ b/Assets/_Scripts/Controller/Vessel/SparrowVesselTelemetry.cs
@@ -27,10 +27,6 @@ namespace CosmicShore.Gameplay
 
         protected override void RegisterStatsExtended()
         {
-            Debug.Log($"[SparrowTelemetry] RegisterStats — " +
-                $"prismBlocks={(prismBlocksShotStat != null ? "OK" : "NULL")}, " +
-                $"skyburst={(skyburstMissilesShotStat != null ? "OK" : "NULL")}, " +
-                $"dangerBlocks={(dangerBlocksSpawnedStat != null ? "OK" : "NULL")}");
             RegisterStat(prismBlocksShotStat);
             RegisterStat(skyburstMissilesShotStat);
             RegisterStat(dangerBlocksSpawnedStat);
@@ -43,7 +39,6 @@ namespace CosmicShore.Gameplay
             FullAutoBlockShootActionExecutor.OnBlockShot += HandleBlockShot;
             FireGunActionExecutor.OnShotFired            += HandleSkyburstFired;
             VesselPrismController.OnDangerBlockCreated   += HandleDangerBlockSpawned;
-            Debug.Log("[SparrowTelemetry] Turn started — subscribed to BlockShot, ShotFired, DangerBlockCreated");
         }
 
         protected override void OnTurnEndedExtended()
@@ -51,8 +46,6 @@ namespace CosmicShore.Gameplay
             FullAutoBlockShootActionExecutor.OnBlockShot -= HandleBlockShot;
             FireGunActionExecutor.OnShotFired            -= HandleSkyburstFired;
             VesselPrismController.OnDangerBlockCreated   -= HandleDangerBlockSpawned;
-            Debug.Log($"[SparrowTelemetry] Turn ended — prismBlocks={PrismBlocksShot}, " +
-                $"skyburst={SkyburstMissilesShot}, dangerBlocks={DangerBlocksSpawned}");
         }
 
         protected override void ResetExtended()
diff --git a/Assets/_Scripts/UI/Controller/RhinoVesselHUDController.cs b/Assets/_Scripts/UI/Controller/RhinoVesselHUDController.cs
index 1182e4e40..bfbcc6c97 100644
--- a/Assets/_Scripts/UI/Controller/RhinoVesselHUDController.cs
+++ b/Assets/_Scripts/UI/Controller/RhinoVesselHUDController.cs
@@ -21,6 +21,7 @@ namespace CosmicShore.UI
 
         private int _slowedCount;
         private IVesselStatus _vesselStatus;
+        private bool _subscribed;
 
         readonly HashSet<IVesselStatus> _uniqueSlowedThisExplosion = new();
 
@@ -37,9 +38,18 @@ namespace CosmicShore.UI
 
             view?.Initialize();
 
+            // Re-init (menu vessel swap / ReInitializePair) must not double-subscribe —
+            // that was double-counting the slowed tally on every re-initialize.
+            Unsubscribe();
             Subscribe();
         }
 
+        void OnEnable()
+        {
+            // Re-attach after a disable→enable cycle (pooled/toggled HUD). No-op before Initialize.
+            if (_vesselStatus != null) Subscribe();
+        }
+
         void OnDisable()
         {
             Unsubscribe();
@@ -47,7 +57,8 @@ namespace CosmicShore.UI
 
         void Subscribe()
         {
-            if (_vesselStatus == null) return;
+            if (_subscribed || _vesselStatus == null) return;
+            _subscribed = true;
 
             if (vesselSlowedByExplosionEvent != null)
                 vesselSlowedByExplosionEvent.OnRaised += HandleVesselSlowedByExplosion;
@@ -69,7 +80,8 @@ namespace CosmicShore.UI
 
         void Unsubscribe()
         {
-            if (_vesselStatus == null) return;
+            if (!_subscribed) return;
+            _subscribed = false;
 
             if (vesselSlowedByExplosionEvent != null)
                 vesselSlowedByExplosionEvent.OnRaised -= HandleVesselSlowedByExplosion;
```

</details>

### `0a7c11651` — feat(vessels): elements drive per-vessel parameters (Pillar 3)

_Claude, 2026-07-09 03:06:40 +0000_

```text
Add ElementalScaling — a shared, multiplayer-safe helper that maps a vessel's live
Charge/Mass/Space/Time resource level to a parameter multiplier, read per-vessel in
the executor. This is the correct home for element→parameter scaling in the R_ action
architecture: action SOs are shared, stateless assets, so an ElementalFloat field on
the asset cannot hold per-vessel state (the last vessel to init would drive everyone).
Every mapping is anchored at 1x at the resting level, so a vessel's authored baseline
is unchanged and elements only add power as crystals raise levels — the elemental
economy, with no baseline-regression risk.

Wire the signature mappings:
- Manta:   Space → Yawstery turn rate; Charge → overcharge detonation blast (fills the
           old //TODO: use mult); Mass → overcharge harvest capacity.
- Dolphin: Charge → charge-boost peak strength; Time → charge fill rate.
- Rhino:   Mass → GrowTrail max slab size.
- Serpent: Time → boost charge duration.
- Sparrow: Mass → full-auto projectile size; Time → projectile lifetime.
```

```text
 .../Skimmer Prism Effects/SkimmerOverchargeCollectPrismEffectSO.cs    | 17 ++++++---
 Assets/_Scripts/Controller/Vessel/ElementalScaling.cs                 | 61 +++++++++++++++++++++++++++++++++
 Assets/_Scripts/Controller/Vessel/ElementalScaling.cs.meta            | 11 ++++++
 .../Vessel/R_VesselActions/Executors/ChargeBoostActionExecutor.cs     | 14 ++++++--
 .../Vessel/R_VesselActions/Executors/ConsumeBoostActionExecutor.cs    |  5 ++-
 .../Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs        |  7 ++--
 .../Vessel/R_VesselActions/Executors/GrowTrailActionExecutor.cs       | 11 ++++--
 .../Vessel/R_VesselActions/Executors/YawsteryActionExecutor.cs        |  6 +++-
 8 files changed, 119 insertions(+), 13 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 196 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/ElementalScaling.cs b/Assets/_Scripts/Controller/Vessel/ElementalScaling.cs
new file mode 100644
index 000000000..72c9feec0
--- /dev/null
+++ b/Assets/_Scripts/Controller/Vessel/ElementalScaling.cs
@@ -0,0 +1,61 @@
+using CosmicShore.Data;
+using UnityEngine;
+
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// Maps a vessel's live elemental resource level (Charge / Mass / Space / Time) to a gameplay
+    /// multiplier, so the four elements scale four per-vessel parameters — the "four elements map
+    /// to four parameters" pillar.
+    ///
+    /// WHY THIS LIVES HERE (not on an ElementalFloat inside the action SO): in the R_ action
+    /// architecture, <see cref="ShipActionSO"/> assets are SHARED and STATELESS ("vessel context is
+    /// passed in each call"). A single asset is referenced by every vessel of that class, so an
+    /// <see cref="ElementalFloat"/> field on the asset cannot hold per-vessel state — in multiplayer
+    /// the last vessel to initialize would drive everyone's value. The per-vessel <b>executor</b>
+    /// (a MonoBehaviour on the vessel) is the correct, multiplayer-safe place to read the vessel's
+    /// own <see cref="ResourceSystem"/> and scale the SO's authored base value at use-time.
+    ///
+    /// The mapping is ANCHORED at the resting level (normalized 0 → integer level 0): the multiplier
+    /// is exactly 1 there, so the authored base value is unchanged at game start. Elements only add
+    /// or subtract power as the player's crystal-driven levels rise above / fall below the resting
+    /// band — the intended elemental economy, and a guarantee that this cannot regress a vessel's
+    /// baseline feel.
+    /// </summary>
+    public static class ElementalScaling
+    {
+        /// <summary>
+        /// Normalized element level, where 0 == resting/base and 1 == integer level 10 (a full
+        /// crystal charge). Extrapolates to the [-0.5, 1.5] band the ResourceSystem clamps to
+        /// (deficit down to -5, overcharge up to +15). Returns 0 when no resource system exists,
+        /// so callers fall back to their authored base value.
+        /// </summary>
+        public static float Level01(IVesselStatus status, Element element)
+        {
+            var rs = status?.ResourceSystem;
+            return rs == null ? 0f : rs.GetNormalizedLevel(element);
+        }
+
+        /// <summary>
+        /// A multiplier around 1.0 driven by an element's level. At the resting level (t == 0) the
+        /// result is exactly 1 (base preserved); at level 10 it is <paramref name="atFull"/>; the
+        /// deficit / overcharge extremes extrapolate linearly. Floored at <paramref name="minMul"/>
+        /// so it never inverts or collapses to zero.
+        /// </summary>
+        public static float Multiplier(IVesselStatus status, Element element,
+            float atFull = 2f, float minMul = 0.25f)
+        {
+            float t = Level01(status, element);             // 0 at resting, 1 at level 10
+            float mul = Mathf.LerpUnclamped(1f, atFull, t);  // anchored at 1 when t == 0
+            return Mathf.Max(minMul, mul);
+        }
+
+        /// <summary>
+        /// Scales an authored base value by an element's level. Equivalent to
+        /// <c>baseValue * Multiplier(...)</c>; base is preserved exactly at the resting level.
+        /// </summary>
+        public static float Scale(IVesselStatus status, Element element, float baseValue,
+            float atFull = 2f, float minMul = 0.25f)
+            => baseValue * Multiplier(status, element, atFull, minMul);
+    }
+}
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ChargeBoostActionExecutor.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ChargeBoostActionExecutor.cs
index 8b8931338..2758452c3 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ChargeBoostActionExecutor.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ChargeBoostActionExecutor.cs
@@ -2,6 +2,7 @@ using System;
 using System.Threading;
 using CosmicShore.Core;
 using Cysharp.Threading.Tasks;
+using CosmicShore.Data;
 using CosmicShore.Gameplay;
 using Obvious.Soap;
 using Reflex.Attributes;
@@ -199,11 +200,20 @@ namespace CosmicShore.Gameplay
         float BoostMultiplierFrom(ChargeBoostActionSO so, float rawUnits)
         {
             float t = (so.MaxNormalizedCharge > 0f) ? Mathf.Clamp01(rawUnits / so.MaxNormalizedCharge) : 0f;
-            return 1f + (so.MaxBoostMultiplier - 1f) * t;
+            // Element → parameter (Charge → peak blast strength). Anchored at 1x at resting level;
+            // a fully-charged Charge element makes Dolphin's signature discharge hit harder.
+            float chargeMul = ElementalScaling.Multiplier(_status, Element.Charge, atFull: 1.5f);
+            float peakBoost = 1f + (so.MaxBoostMultiplier - 1f) * chargeMul;
+            return 1f + (peakBoost - 1f) * t;
         }
 
+        // Element → parameter (Time → charge rate). Anchored at 1x at resting level; a high Time
+        // element fills the charge meter faster, so the dart threads gaps into a blast sooner.
         float ChargePerSecond(ChargeBoostActionSO so)
-            => (so.ChargeTimeToFull > 0f) ? (so.MaxNormalizedCharge / so.ChargeTimeToFull) : so.MaxNormalizedCharge;
+        {
+            float baseRate = (so.ChargeTimeToFull > 0f) ? (so.MaxNormalizedCharge / so.ChargeTimeToFull) : so.MaxNormalizedCharge;
+            return baseRate * ElementalScaling.Multiplier(_status, Element.Time, atFull: 1.5f);
+        }
 
         float DischargePerSecond(ChargeBoostActionSO so)
             => (so.DischargeTimeToEmpty > 0f) ? (so.MaxNormalizedCharge / so.DischargeTimeToEmpty) : so.MaxNormalizedCharge;
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ConsumeBoostActionExecutor.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ConsumeBoostActionExecutor.cs
index 098f7af7e..545cc57da 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ConsumeBoostActionExecutor.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ConsumeBoostActionExecutor.cs
@@ -123,7 +123,10 @@ namespace CosmicShore.Gameplay
             audioSystem.PlayGameplaySFX(GameplaySFXCategory.BoostActivate);
 
             int pipIndex = Mathf.Clamp(_available - 1, 0, _so.MaxCharges - 1);
-            float duration = Mathf.Max(0.05f, _so.BoostDuration);
+            // Element → parameter (Time → boost duration). Anchored at 1x at resting level; a high
+            // Time element makes each Serpent boost charge last longer.
+            float duration = Mathf.Max(0.05f,
+                ElementalScaling.Scale(_status, Element.Time, _so.BoostDuration, atFull: 1.6f));
 
             OnChargeConsumed?.Invoke(pipIndex, duration);
             _available = Mathf.Max(0, _available - 1);
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs
index ac3997e47..16fd036f0 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs
@@ -2,6 +2,7 @@ using System;
 using System.Threading;
 using UnityEngine;
 using Cysharp.Threading.Tasks;
+using CosmicShore.Data;
 using CosmicShore.Gameplay;
 using Obvious.Soap;
 using CosmicShore.Utility;
@@ -135,8 +136,10 @@ public sealed class FullAutoActionExecutor : ShipActionExecutorBase
         var   ammoIndex       = so.AmmoIndex;
         var ammoCost        = so.AmmoCost;
         var  inherit         = so.Inherit;
-        var projectileScale = so.ProjectileScale;
-        var projectileTime  = so.ProjectileTime;
+        // Element → parameter (Mass → projectile size, Time → projectile lifetime). Anchored at
+        // base at resting level; captured once per hold, re-captured on the next trigger press.
+        var projectileScale = ElementalScaling.Scale(_status, Element.Mass, so.ProjectileScale, atFull: 1.5f);
+        var projectileTime  = ElementalScaling.Scale(_status, Element.Time, so.ProjectileTime, atFull: 1.5f);
         var   firingPattern   = so.FiringPattern;
         var energy          = so.Energy;
         var speedValue      = so.SpeedValue.Value;
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/GrowTrailActionExecutor.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/GrowTrailActionExecutor.cs
index d1aebf123..30f600713 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/GrowTrailActionExecutor.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/GrowTrailActionExecutor.cs
@@ -1,6 +1,7 @@
 using System;
 using System.Threading;
```

</details>

### `96327603e` — docs(vessels): fleet status/identity spec + Sparrow Charge mapping

_Claude, 2026-07-09 03:10:50 +0000_

```text
Add Docs/VESSELS/FLEET_STATUS.md — the fleet-wide gap map, canonical identity
spec, the intended 4x4 element→parameter table per vessel (marking code-done vs
asset-TODO), what this pass shipped, and the exact Unity-editor checklist to
finish each vessel (including why the five stubs are unspawnable from code alone).

Sparrow: Charge → heat-decay rate via ElementalScaling in OverheatingActionExecutor
(3/4 elements now driven in code).
```

```text
 .../Vessel/R_VesselActions/Executors/OverheatingActionExecutor.cs     |   7 +-
 Docs/VESSELS/FLEET_STATUS.md                                          | 224 ++++++++++++++++++++++++++++++++
 2 files changed, 230 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 255 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/OverheatingActionExecutor.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/OverheatingActionExecutor.cs
index 5409589a9..7730af582 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/OverheatingActionExecutor.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/OverheatingActionExecutor.cs
@@ -2,6 +2,7 @@ using System;
 using System.Threading;
 using Cysharp.Threading.Tasks;
 using UnityEngine;
+using CosmicShore.Data;
 using CosmicShore.Gameplay;
 using Obvious.Soap;
 using CosmicShore.Utility;
@@ -207,7 +208,11 @@ namespace CosmicShore.Gameplay
 
                 while (_heatResource.CurrentAmount > 0)
                 {
-                    _resources.ChangeResourceAmount(so.HeatResourceIndex, -so.HeatDecayRate.Value);
+                    // Element → parameter (Charge → heat-decay rate). Anchored at 1x at resting
+                    // level; a high Charge element cools the overheat penalty off faster.
+                    float decay = so.HeatDecayRate.Value *
+                        ElementalScaling.Multiplier(_status, Element.Charge, atFull: 1.5f);
+                    _resources.ChangeResourceAmount(so.HeatResourceIndex, -decay);
 
                     await UniTask.Delay(
                         TimeSpan.FromSeconds(0.1f),
diff --git a/Docs/VESSELS/FLEET_STATUS.md b/Docs/VESSELS/FLEET_STATUS.md
new file mode 100644
index 000000000..a1b28b28a
--- /dev/null
+++ b/Docs/VESSELS/FLEET_STATUS.md
@@ -0,0 +1,224 @@
+# Vessel Fleet — Status, Identity Spec & Completion Guide
+
+Cosmic Shore ships 11 vessel classes (`VesselClassType`). Each is meant to be a self-contained,
+*fun* experience defined by **five pillars**:
+
+1. **Identity** — a clear genre/fantasy.
+2. **Four abilities ↔ four dynamic icons** — abilities wired on the prefab's
+   `R_VesselActionHandler._inputEventShipActions` (InputEvent → `ShipActionSO`); each ability's HUD
+   icon conveys live state, not just a static button.
+3. **Four elements ↔ four parameters** — `Charge / Mass / Space / Time` each scale a gameplay
+   parameter.
+4. **A distinct trail** — a per-vessel prism prefab / `TrailScaleProfileSO` / prism controller.
+5. **Telemetry** — a `VesselTelemetry` subclass (nice-to-have).
+
+**Squirrel** (racing/drift) is the reference build — fully realizes all five pillars. This document
+is the fleet-wide gap map, the canonical identity spec, and the completion checklist for the other
+ten, derived from a per-vessel audit.
+
+---
+
+## How each pillar is expressed — code vs. Unity asset
+
+| Pillar | Mechanism | Editable in `.cs`? |
+|---|---|---|
+| Abilities (P2 wiring) | `R_VesselActionHandler._inputEventShipActions` in `<Vessel>.prefab` | ❌ prefab YAML |
+| Ability icons (P2 visuals) | `VesselHUDController` subclass drives a `VesselHUDView` subclass; base toggles per-input `highlights`; subclass adds live state | ✅ controller/view code; ❌ the icon `Image` refs + `HighlightBinding`s live in the HUD prefab |
+| Element → parameter (P3) | **executor reads `ResourceSystem` at use-time** via `ElementalScaling` (see below) | ✅ fully code |
+| Trail (P4) | prism prefab + `TrailScaleProfileSO` + prism controller; `PrismFactory.PrismType` | ✅ scaler/controller code; ❌ prefab/pool/`prismType` |
+| Telemetry (P5) | `VesselTelemetry` subclass + `VesselTelemetryBootstrapper` case + stat-event SOs | ✅ subclass; ❌ stat-event `.asset`s + bootstrapper wiring |
+
+### Why P3 lives in the executor, not on an `ElementalFloat`
+
+`ShipActionSO` assets are **shared and stateless** ("vessel context is passed in each call"). One
+asset is referenced by every vessel of that class, so an `ElementalFloat` field on the asset cannot
+hold per-vessel state — in multiplayer the last vessel to initialize would drive everyone's value.
+Worse, the action-SO binder (`ElementalFloatBinder.BindAndClone`) is **commented out** in
+`ShipActionSO.Initialize` **and** broken (it sets a non-existent `"Ship"` property and its clone
+drops `element`/`Min`/`Max`). So element scaling on action SOs is dead today.
+
+The fix (shipped this pass): **`ElementalScaling`** (`_Scripts/Controller/Vessel/ElementalScaling.cs`)
+— a per-vessel-safe helper the executor calls at use-time:
+
+```csharp
+float mul = ElementalScaling.Multiplier(status, Element.Space, atFull: 1.6f);
+// or
+float scaled = ElementalScaling.Scale(status, Element.Mass, so.BaseValue, atFull: 1.5f);
+```
+
+- Reads the vessel's **own** `ResourceSystem.GetNormalizedLevel(element)`.
+- **Anchored at `1.0` at the resting level (0):** the authored base value is unchanged at game
+  start; elements only add/subtract power as crystals raise/lower levels — the intended elemental
+  economy, with **no baseline-regression risk**.
+- `Skimmer`-borne `ElementalFloat`s (which DO bind, via `ElementalShipComponent`) are unaffected.
+
+**Element convention (fleet-wide):** `Space → reach/handling`, `Time → duration/cooldown/rate`,
+`Charge → energy output`, `Mass → physical size`.
+
+---
+
+## Fleet Status Matrix
+
+| Vessel (ID) | Status | Identity (1 line) | HUD (ctrl/view/variant) | Elements mapped in code (C/M/S/T) | Trail | Spawnable? |
+|---|---|---|---|---|---|---|
+| **Squirrel** (6) | ✅ reference | Vaporwave drift racer | ✓/✓/✓ rich | (reference) | ✓ | ✓ |
+| **Sparrow** (11) | ✅ complete | Dual-stance arcade gunship | ✓/✓/✓ rich | **M, T, C** (this pass) + Space via skimmer | ✓ boost-reactive | ✓ |
+| **Manta** (1) | 🟡 half-baked | Reaper ray: harvests & chain-detonates enemy trails | ✓/✓/✓ | **C, M, S** (this pass) | ✓ wing prism | ✓ |
+| **Dolphin** (2) | 🟡 half-baked | Charge-drift-blast racer | ✓/✓/✓ | **C, T** (this pass) | ✓ | ✓ |
+| **Rhino** (3) | 🟡 half-baked | Heavyweight ram/forcefield bruiser | ✓/✓/✓ rich | **M** (this pass) | ✓ armored slab | ✓ |
+| **Serpent** (7) | 🟡 half-baked | Stealthy one-thumb wall-weaver | ✓/✓/✓ | **T** (this pass) | ✓ tall slab + seed walls | ✓ |
+| **Urchin** (4) | 🔴 stub | Attach-and-shoot sea-urchin turret | ✗/✗/✗ | none | borrows Dolphin prisms | ✗ |
+| **Grizzly** (5) | 🔴 stub | Stop-and-fire gun emplacement | ✗/✗/✗ | none (empty `Resources`) | broken channel | ✗ |
+| **Termite** (8) | 🔴 stub | Eusocial drone-commander | ✗/✗/✗ | none (empty `Resources`) | disabled | ✗ |
+| **Falcon** (9) | 🔴 stub | *identity undefined* (Manta clone) | ✗/✗/✗ | none | none | ✗ |
+| **Shrike** (10) | 🔴 stub | *identity undefined* (Manta clone) | ✗/✗/✗ | none | none | ✗ |
+
+**Fleet-wide root cause:** the five stubs' prefabs predate the `ShipAction` MonoBehaviour →
+`ShipActionSO` / `R_VesselActionHandler` migration and were left with empty action handlers, orphaned
+legacy components, `vesselType = 0`, and absence from `Vessel Prefab Container.asset` — so they are
+**unspawnable**. They cannot be made flyable from `.cs` alone; each needs a Unity-editor pass (below).
+
+---
+
+## What this session shipped (code, no Unity needed)
+
+**Real runtime bug fixes** (`fix(vessels): repair real runtime bugs`):
+- **Serpent** — boost was `Mathf.Pow(4, stacks)` = **256× at 4 charges** (uncontrollable). Now
+  config-driven linear `BoostMultiplier.Value * stacks` → 4×..16×. Removed per-frame log spam; the
+  boost SFX now fires only on a real consume, not no-op presses.
+- **Dolphin** — `ShardToggleActionExecutor` dereferenced a null `Cell` → **NRE on every drift** in
+  cell-less modes (Menu freestyle). Guarded.
+- **Rhino** — HUD `Subscribe`/`Unsubscribe` weren't idempotent → **double-counted slow tallies** on
+  menu vessel-swap and dead indicators after disable→enable. Added a guard + `OnEnable` re-subscribe.
+- **Manta** — overcharge kept process-wide `static` state keyed by `SkimmerImpactor`; destroyed
+  impactors leaked and carried state across scenes. Now prunes Unity-destroyed keys.
+- **Sparrow** — removed three `Debug.Log` diagnostics from telemetry.
+
+**Element → parameter mappings** (`feat(vessels): elements drive per-vessel parameters`) — via
+`ElementalScaling`, anchored at 1× at resting level:
+
+| Vessel | Element → parameter | Where |
+|---|---|---|
+| Manta | Space → Yawstery turn rate · Charge → overcharge blast · Mass → harvest capacity | `YawsteryActionExecutor`, `SkimmerOverchargeCollectPrismEffectSO` |
+| Dolphin | Charge → charge-boost peak · Time → charge fill rate | `ChargeBoostActionExecutor` |
+| Rhino | Mass → GrowTrail max slab size | `GrowTrailActionExecutor` |
+| Serpent | Time → boost charge duration | `ConsumeBoostActionExecutor` |
+| Sparrow | Mass → projectile size · Time → projectile lifetime · Charge → heat-decay rate | `FullAutoActionExecutor`, `OverheatingActionExecutor` |
+
+---
+
+## Canonical identities & the full intended 4×4 mapping
+
+Element columns marked ✅ are wired in code this pass; ⬜ is the intended mapping still to author
+(mostly asset-side or a further executor hook).
+
+### Manta (1) — "The Reaper Ray"
+An elegant ray that harvests opposing-domain trail mass with its skimmer and detonates it in a
+distance-ordered chain — a predator that turns the enemy's own conserved mass against them.
+- **Abilities ↔ icons:** Yawstery bank-turn (hold L/R, twin icon) · Boost · Skimmer Overcharge dial
+  (count + radial fill + OVERCHARGED banner — built) · *(4th slot open — propose a manual overcharge
```

</details>

### `dd634844c` — refactor(vessels): consistent Mass-scaled overcharge capacity in HUD reset

_Claude, 2026-07-09 04:14:28 +0000_

```text
Route the Manta overcharge cooldown reset through the same Mass-scaled capacity
(EffectiveMaxFor) used while filling, so the HUD "count / max" denominator no longer
jumps between the scaled cap and the base cap when Mass is above resting. Extracts a
small DRY helper shared by the fill and reset paths.
```

```text
 .../EffectsSO/Skimmer Prism Effects/SkimmerOverchargeCollectPrismEffectSO.cs          | 17 +++++++++++++----
 1 file changed, 13 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff

```

</details>

### `94a6e889b` — feat(vessels): enforce a four-icon ability contract

_Claude, 2026-07-09 15:48:46 +0000_

```text
To the player, every vessel now presents exactly four ability icons — a fixed
expectation of "four abilities" where the icons are the mnemonics. Under the hood a
slot is decoupled from this: it can be a shallow passive or several kit parts merged
into one "ability", since some vessels are more complicated than others.

- VesselAbilitySetSO — a per-vessel set of exactly four VesselAbilitySlots
  (Label/Description/Input/Icon/IsPlaceholder). The four-slot size is enforced in
  OnValidate, so a set with more or fewer cannot be authored.
- VesselAbilityBar — renders exactly four icons from the set, lights each while its
  Input is held, and self-builds its icon row if none is authored. Unfilled slots show
  an obvious code-generated placeholder (AbilityIconPlaceholder, hazard-stripe tile, no
  asset needed), so a vessel cannot display fewer than four icons.
- VesselHUDController resolves and initializes a bar under the HUD if present —
  non-regressing: existing HUDs are untouched until they adopt one.
- Tools > Cosmic Shore > Validate Vessel Ability Icons reports fleet compliance; a
  build gate (EnforceOnBuild, default off) can make a non-compliant fleet fail the
  build once every vessel is migrated.

Docs: FLEET_STATUS.md gains a "four-icon contract" section + per-vessel adoption steps.
```

```text
 Assets/_Scripts/Editor/VesselAbilityIconValidator.cs         |  97 ++++++++++++++++++++++++
 Assets/_Scripts/Editor/VesselAbilityIconValidator.cs.meta    |  11 +++
 Assets/_Scripts/ScriptableObjects/VesselAbilitySetSO.cs      |  82 +++++++++++++++++++++
 Assets/_Scripts/ScriptableObjects/VesselAbilitySetSO.cs.meta |  11 +++
 Assets/_Scripts/UI/AbilityIconPlaceholder.cs                 |  81 ++++++++++++++++++++
 Assets/_Scripts/UI/AbilityIconPlaceholder.cs.meta            |  11 +++
 Assets/_Scripts/UI/Controller/VesselAbilityBar.cs            | 164 +++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/UI/Controller/VesselAbilityBar.cs.meta       |  11 +++
 Assets/_Scripts/UI/Controller/VesselHUDController.cs         |  12 +++
 Docs/VESSELS/FLEET_STATUS.md                                 |  35 ++++++++-
 10 files changed, 514 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 531 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/VesselAbilityIconValidator.cs b/Assets/_Scripts/Editor/VesselAbilityIconValidator.cs
new file mode 100644
index 000000000..2b9af0dcc
--- /dev/null
+++ b/Assets/_Scripts/Editor/VesselAbilityIconValidator.cs
@@ -0,0 +1,97 @@
+using System.Collections.Generic;
+using System.Text;
+using CosmicShore.UI;
+using UnityEditor;
+using UnityEditor.Build;
+using UnityEditor.Build.Reporting;
+using UnityEngine;
+
+namespace CosmicShore.Editor
+{
+    /// <summary>
+    /// Enforces the "every vessel presents four ability icons" contract. A vessel HUD is compliant
+    /// when it contains a <see cref="VesselAbilityBar"/> with a four-slot <c>VesselAbilitySetSO</c>
+    /// assigned. Run the menu item to report the fleet; flip <see cref="EnforceOnBuild"/> to make a
+    /// non-compliant fleet fail the build (kept off until every vessel has been migrated so builds
+    /// aren't broken while the ability sets are still being authored).
+    /// </summary>
+    public static class VesselAbilityIconValidator
+    {
+        // Set to true once every vessel HUD has a compliant VesselAbilityBar to make the four-icon
+        // contract a hard build gate ("impossible to ship a vessel without four icons").
+        const bool EnforceOnBuild = false;
+
+        static readonly string[] SearchFolders =
+        {
+            "Assets/_Prefabs/Spacevessels",
+            "Assets/_Prefabs/UI Elements/VesselHUD",
+        };
+
+        [MenuItem("Tools/Cosmic Shore/Validate Vessel Ability Icons")]
+        public static void Validate()
+        {
+            var report = Scan(out int compliant, out int barNoSet, out int noBar, out int total);
+            Debug.Log($"[VesselAbilityIcons] Scanned {total} vessel/HUD prefab(s): " +
+                      $"{compliant} compliant, {barNoSet} bar-without-set, {noBar} missing a bar.\n{report}");
+
+            if (barNoSet > 0 || noBar > 0)
+                Debug.LogWarning("[VesselAbilityIcons] Some vessels do not yet present four ability " +
+                                 "icons. Add a VesselAbilityBar + a 4-slot VesselAbilitySetSO to each " +
+                                 "(unfilled slots show an obvious placeholder).");
+        }
+
+        static string Scan(out int compliant, out int barNoSet, out int noBar, out int total)
+        {
+            compliant = barNoSet = noBar = total = 0;
+            var sb = new StringBuilder();
+
+            var guids = AssetDatabase.FindAssets("t:Prefab", SearchFolders);
+            var seen = new HashSet<string>();
+
+            foreach (var guid in guids)
+            {
+                var path = AssetDatabase.GUIDToAssetPath(guid);
+                if (!seen.Add(path)) continue;
+
+                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
+                if (!go) continue;
+
+                total++;
+                var bar = go.GetComponentInChildren<VesselAbilityBar>(true);
+                if (!bar)
+                {
+                    noBar++;
+                    sb.AppendLine($"  ✗ no ability bar   — {path}");
+                }
+                else if (!bar.HasAbilitySet)
+                {
+                    barNoSet++;
+                    sb.AppendLine($"  ⚠ bar, no set      — {path}");
+                }
+                else
+                {
+                    compliant++;
+                    sb.AppendLine($"  ✓ four icons       — {path}");
+                }
+            }
+
+            return sb.ToString();
+        }
+
+        sealed class BuildGate : IPreprocessBuildWithReport
+        {
+            public int callbackOrder => 0;
+
+            public void OnPreprocessBuild(BuildReport _)
+            {
+                if (!EnforceOnBuild) return;
+
+                Scan(out _, out int barNoSet, out int noBar, out _);
+                if (barNoSet + noBar > 0)
+                    throw new BuildFailedException(
+                        $"[VesselAbilityIcons] {barNoSet + noBar} vessel/HUD prefab(s) do not present " +
+                        "four ability icons. Run Tools > Cosmic Shore > Validate Vessel Ability Icons.");
+            }
+        }
+    }
+}
diff --git a/Assets/_Scripts/ScriptableObjects/VesselAbilitySetSO.cs b/Assets/_Scripts/ScriptableObjects/VesselAbilitySetSO.cs
new file mode 100644
index 000000000..7f83fbcd0
--- /dev/null
+++ b/Assets/_Scripts/ScriptableObjects/VesselAbilitySetSO.cs
@@ -0,0 +1,82 @@
+using System;
+using System.Collections.Generic;
+using CosmicShore.Data;
+using UnityEngine;
+
+namespace CosmicShore.ScriptableObjects
+{
+    /// <summary>
+    /// One player-facing ability slot. A vessel ALWAYS presents exactly four of these to the player
+    /// (see <see cref="VesselAbilitySetSO"/>), regardless of how the underlying kit is wired: a slot
+    /// may map to a single shallow passive, or bundle several kit parts into one "ability" for
+    /// vessels that are mechanically busier. The icon is the mnemonic; the input is what lights it up.
+    /// </summary>
+    [Serializable]
+    public struct VesselAbilitySlot
+    {
+        [Tooltip("Short player-facing name shown with the icon.")]
+        public string Label;
+
+        [Tooltip("One-line description of what the ability does (tooltip / codex).")]
+        [TextArea(1, 3)] public string Description;
+
+        [Tooltip("The input that triggers this ability — its icon lights up while held. " +
+                 "Passives can leave this at the default and simply read as always-on.")]
+        public InputEvents Input;
+
+        [Tooltip("The ability icon. LEAVE EMPTY to show the obvious 'unassigned ability' placeholder " +
+                 "until a real ability + icon is authored.")]
+        public Sprite Icon;
+
+        [Tooltip("Force the placeholder even when an Icon is set — flags a work-in-progress ability.")]
+        public bool IsPlaceholder;
+
+        /// <summary>True only when a real icon is authored and the slot isn't flagged WIP.</summary>
+        public bool HasIcon => Icon != null && !IsPlaceholder;
+    }
+
+    /// <summary>
+    /// The four player-facing abilities of a vessel. This is a HARD contract: a vessel always
+    /// exposes exactly four ability slots so the HUD can always show four icons, setting one
+    /// consistent expectation for the player. Any unfilled slot renders as an obvious placeholder,
```

</details>

### `c02894d03` — perf(vessels): prefer prefab-authored ability icons over runtime construction

_Claude, 2026-07-09 17:12:20 +0000_

```text
The four-icon bar was building its icon structure at runtime on every HUD init — work
landing on the busy vessel-spawn/swap moment. A pool is the wrong fix: a bar is created
at most once per vessel and lives for the HUD's lifetime, so there is no high-frequency
churn to pool.

Invert primary and fallback instead:
- PREFERRED — author the four icon Images in the HUD prefab and assign them to
  slotImages. They load with the HUD like any other element: zero runtime allocation,
  nothing built on the hotpath.
- FALLBACK — only slots left unassigned are self-built once, so a not-yet-authored
  vessel or stub still shows four icons (with the placeholder). The exceptional path,
  so its one-time cost is irrelevant.

The structure is never rebuilt — it survives HUD show/hide untouched (no per-toggle
release/reacquire). Contract ("always four icons") and enforcement are unchanged.
```

```text
 Assets/_Scripts/UI/Controller/VesselAbilityBar.cs | 128 +++++++++++++++++++++++++++++++---------------------
 Docs/VESSELS/FLEET_STATUS.md                      |  24 ++++++----
 2 files changed, 91 insertions(+), 61 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 252 lines)</summary>

```diff
diff --git a/Assets/_Scripts/UI/Controller/VesselAbilityBar.cs b/Assets/_Scripts/UI/Controller/VesselAbilityBar.cs
index 3f68a881e..164eb234e 100644
--- a/Assets/_Scripts/UI/Controller/VesselAbilityBar.cs
+++ b/Assets/_Scripts/UI/Controller/VesselAbilityBar.cs
@@ -1,4 +1,3 @@
-using System.Collections.Generic;
 using CosmicShore.Data;
 using CosmicShore.Gameplay;
 using CosmicShore.ScriptableObjects;
@@ -8,32 +7,41 @@ using UnityEngine.UI;
 namespace CosmicShore.UI
 {
     /// <summary>
-    /// Renders a vessel's four player-facing ability icons — always exactly four. Any slot without
-    /// an authored icon shows an obvious placeholder, so it is impossible for a vessel to present
-    /// fewer than four ability icons. Each icon lights up while its ability's input is held.
+    /// Guarantees a vessel presents exactly four ability icons, and lights each while its ability's
+    /// input is held.
     ///
-    /// The bar self-builds its icon row if none is authored in the prefab, so a brand-new vessel
-    /// gets four placeholder icons for free. Drive it from <see cref="VesselHUDController"/> by
-    /// calling <see cref="Initialize"/>; the base controller resolves and initializes any bar found
-    /// under the HUD, so existing HUDs are untouched until one is added.
+    /// PREFERRED path — author the four icon <see cref="Image"/>s in the HUD prefab and assign them
+    /// to <see cref="slotImages"/>. They then load with the HUD like any other element: zero runtime
+    /// allocation, and no work on the vessel-spawn/swap hotpath.
+    ///
+    /// FALLBACK path — any slot left unassigned is built once at <see cref="Initialize"/> so a
+    /// not-yet-authored vessel (or a stub) still shows four icons, with an obvious placeholder.
+    /// This is the exceptional path, so its one-time construction cost is irrelevant.
+    ///
+    /// No pooling: a bar is created at most once per vessel and lives for the HUD's lifetime — there
+    /// is nothing high-frequency to pool, and the structure is never rebuilt (it survives HUD
+    /// show/hide untouched). Unfilled abilities show a code-generated placeholder sprite.
     /// </summary>
     public sealed class VesselAbilityBar : MonoBehaviour
     {
         [Header("Data — the four player-facing abilities")]
         [SerializeField] private VesselAbilitySetSO abilitySet;
 
-        [Header("Layout (self-built if the container is left empty)")]
-        [SerializeField] private RectTransform iconContainer;
-        [SerializeField] private Vector2 iconSize = new(96f, 96f);
-        [SerializeField] private float spacing = 16f;
-        [SerializeField] private Vector2 selfBuiltAnchoredOffset = new(0f, 24f);
+        [Header("Icons — assign four in the HUD prefab (preferred, zero runtime alloc)")]
+        [SerializeField] private Image[] slotImages = new Image[VesselAbilitySetSO.SlotCount];
+
+        [Header("Fallback layout — only used to self-build unassigned slots")]
+        [SerializeField] private RectTransform fallbackContainer;
+        [SerializeField] private Vector2 fallbackIconSize = new(96f, 96f);
+        [SerializeField] private float fallbackSpacing = 16f;
+        [SerializeField] private Vector2 fallbackAnchoredOffset = new(0f, 24f);
 
         [Header("Active-state feel")]
         [SerializeField, Range(0f, 1f)] private float idleAlpha = 0.55f;
         [SerializeField, Range(0f, 1f)] private float activeAlpha = 1f;
         [SerializeField] private float activeScale = 1.15f;
 
-        readonly List<Image> _icons = new();
+        readonly Image[] _icons = new Image[VesselAbilitySetSO.SlotCount];
         R_VesselActionHandler _actions;
         bool _subscribed;
         bool _built;
@@ -44,13 +52,14 @@ namespace CosmicShore.UI
         public void Initialize(IVesselStatus status)
         {
             _actions = status?.ActionHandler;
-            BuildIcons();
+            ResolveIcons();
             Subscribe();
         }
 
         void OnEnable()
         {
-            // Re-attach after a disable→enable cycle (pooled / toggled HUD). No-op before Initialize.
+            // Re-attach after a disable→enable cycle (HUD show/hide). The icon structure persists —
+            // it is never released or rebuilt — so this only re-subscribes. No-op before Initialize.
             if (_actions != null) Subscribe();
         }
 
@@ -73,49 +82,77 @@ namespace CosmicShore.UI
             _subscribed = false;
         }
 
-        void BuildIcons()
+        void ResolveIcons()
         {
-            EnsureContainer();
+            if (_built)
+            {
+                Repaint(); // re-Initialize (vessel swap) just repaints the existing structure
+                return;
+            }
 
             for (int i = 0; i < SlotCount; i++)
             {
-                Image img = i < _icons.Count ? _icons[i] : null;
-                if (!img)
-                {
-                    img = CreateIcon(i);
-                    _icons.Add(img);
-                }
+                // Preferred: an icon authored in the prefab — no allocation.
+                var img = (slotImages != null && i < slotImages.Length) ? slotImages[i] : null;
+                // Fallback: self-build a missing slot once, so the four-icon contract still holds.
+                if (!img) img = BuildFallbackIcon(i);
+                _icons[i] = img;
+            }
+
+            _built = true;
+            Repaint();
+
+            if (!abilitySet)
+                Debug.LogError($"[VesselAbilityBar] No VesselAbilitySetSO assigned on '{name}'. " +
+                               "Showing four placeholders — every vessel must have a 4-slot ability set.");
+        }
+
+        void Repaint()
+        {
+            for (int i = 0; i < SlotCount; i++)
+            {
+                var img = _icons[i];
+                if (!img) continue;
 
                 var slot = abilitySet ? abilitySet.GetSlot(i) : default;
                 bool hasIcon = slot.HasIcon;
 
                 img.sprite = hasIcon ? slot.Icon : AbilityIconPlaceholder.Sprite;
+                if (!img.enabled) img.enabled = true;
                 img.color = new Color(1f, 1f, 1f, idleAlpha);
                 img.rectTransform.localScale = Vector3.one;
-                img.name = hasIcon ? $"AbilitySlot{i}_{slot.Label}" : $"AbilitySlot{i}_Placeholder";
             }
+        }
 
-            _built = true;
+        Image BuildFallbackIcon(int index)
+        {
+            EnsureFallbackContainer();
 
-            if (!abilitySet)
-                Debug.LogError($"[VesselAbilityBar] No VesselAbilitySetSO assigned on '{name}'. " +
-                               "Showing four placeholders — every vessel must have a 4-slot ability set.");
+            var go = new GameObject($"AbilitySlot{index}_fallback", typeof(RectTransform), typeof(Image));
+            var rt = go.GetComponent<RectTransform>();
+            rt.SetParent(fallbackContainer, false);
+            rt.sizeDelta = fallbackIconSize;
+
+            var img = go.GetComponent<Image>();
```

</details>

### `09bd12473` — fix(vessels): ability-icon validator build gate compile error

_Claude, 2026-07-09 21:54:30 +0000_

```text
OnPreprocessBuild named its parameter `_`, so `out _` in the Scan(...) call bound to
that BuildReport variable instead of being a discard (CS1503). Name the parameter so
the discards resolve correctly.
```

```text
 Assets/_Scripts/Editor/VesselAbilityIconValidator.cs | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/VesselAbilityIconValidator.cs b/Assets/_Scripts/Editor/VesselAbilityIconValidator.cs
index 2b9af0dcc..c70a674c8 100644
--- a/Assets/_Scripts/Editor/VesselAbilityIconValidator.cs
+++ b/Assets/_Scripts/Editor/VesselAbilityIconValidator.cs
@@ -82,7 +82,7 @@ namespace CosmicShore.Editor
         {
             public int callbackOrder => 0;
 
-            public void OnPreprocessBuild(BuildReport _)
+            public void OnPreprocessBuild(BuildReport report)
             {
                 if (!EnforceOnBuild) return;
 
```

</details>

### `5ddeb889d` — feat(vessels): four-icon contract goes live for all six flyable vessels

_Claude, 2026-07-10 22:13:46 +0000_

```text
The contract is no longer dormant scaffolding — every flyable vessel now presents
four ability icons in-game with zero editor wiring:

- Author Resources/VesselAbilitySets/{Manta,Dolphin,Rhino,Squirrel,Serpent,Sparrow}
  .asset — each vessel's four player-facing slots with truthful Input wiring verified
  against the prefab's _inputEventShipActions (e.g. Sparrow: Fire/SkyBurst/Turret
  Stance/Overheat Boost on inputs 1/2/6/7). Icons are left null so the obvious
  hazard-stripe placeholder shows until real art lands; not-yet-wired abilities
  (Serpent/Dolphin 4th slot, Rhino Forcefield/Ram) are flagged IsPlaceholder.
- VesselHUDController.TryAutoAdoptAbilityBar: when a HUD has no authored bar but a
  Resources set exists for the vessel's class, build one at runtime for the local
  human pilot — the same zero-wire Resources-fallback pattern as ElementalBarsView.
  Authored bars in HUD prefabs take precedence automatically (zero-alloc path).
- VesselAbilityBar.SetAbilitySet enables the runtime-assignment path.
- Validator reworked to match the contract's real shape: a vessel prefab is compliant
  with an authored bar OR a resolvable ability set; scans Spacevessels prefabs and
  reports per-vessel status. Build gate unchanged (off until stubs are resurrected).
```

```text
 Assets/Resources/VesselAbilitySets.meta                |  8 ++++
 Assets/Resources/VesselAbilitySets/Dolphin.asset       | 36 ++++++++++++++++++
 Assets/Resources/VesselAbilitySets/Dolphin.asset.meta  |  8 ++++
 Assets/Resources/VesselAbilitySets/Manta.asset         | 36 ++++++++++++++++++
 Assets/Resources/VesselAbilitySets/Manta.asset.meta    |  8 ++++
 Assets/Resources/VesselAbilitySets/Rhino.asset         | 36 ++++++++++++++++++
 Assets/Resources/VesselAbilitySets/Rhino.asset.meta    |  8 ++++
 Assets/Resources/VesselAbilitySets/Serpent.asset       | 36 ++++++++++++++++++
 Assets/Resources/VesselAbilitySets/Serpent.asset.meta  |  8 ++++
 Assets/Resources/VesselAbilitySets/Sparrow.asset       | 36 ++++++++++++++++++
 Assets/Resources/VesselAbilitySets/Sparrow.asset.meta  |  8 ++++
 Assets/Resources/VesselAbilitySets/Squirrel.asset      | 36 ++++++++++++++++++
 Assets/Resources/VesselAbilitySets/Squirrel.asset.meta |  8 ++++
 Assets/_Scripts/Editor/VesselAbilityIconValidator.cs   | 96 +++++++++++++++++++++++++++++-------------------
 Assets/_Scripts/UI/Controller/VesselAbilityBar.cs      |  8 ++++
 Assets/_Scripts/UI/Controller/VesselHUDController.cs   | 36 +++++++++++++++++-
 Docs/VESSELS/FLEET_STATUS.md                           | 14 +++++--
 17 files changed, 383 insertions(+), 43 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 251 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/VesselAbilityIconValidator.cs b/Assets/_Scripts/Editor/VesselAbilityIconValidator.cs
index c70a674c8..783ef8035 100644
--- a/Assets/_Scripts/Editor/VesselAbilityIconValidator.cs
+++ b/Assets/_Scripts/Editor/VesselAbilityIconValidator.cs
@@ -1,5 +1,7 @@
 using System.Collections.Generic;
 using System.Text;
+using CosmicShore.Gameplay;
+using CosmicShore.ScriptableObjects;
 using CosmicShore.UI;
 using UnityEditor;
 using UnityEditor.Build;
@@ -9,43 +11,46 @@ using UnityEngine;
 namespace CosmicShore.Editor
 {
     /// <summary>
-    /// Enforces the "every vessel presents four ability icons" contract. A vessel HUD is compliant
-    /// when it contains a <see cref="VesselAbilityBar"/> with a four-slot <c>VesselAbilitySetSO</c>
-    /// assigned. Run the menu item to report the fleet; flip <see cref="EnforceOnBuild"/> to make a
-    /// non-compliant fleet fail the build (kept off until every vessel has been migrated so builds
-    /// aren't broken while the ability sets are still being authored).
+    /// Enforces the "every vessel presents four ability icons" contract. A vessel prefab is
+    /// compliant when EITHER:
+    ///   (a) its HUD contains a <see cref="VesselAbilityBar"/> with a four-slot
+    ///       <see cref="VesselAbilitySetSO"/> assigned (authored path), OR
+    ///   (b) a <c>Resources/VesselAbilitySets/{VesselClassType}.asset</c> exists for its
+    ///       <see cref="VesselStatus.VesselType"/> — the zero-wire path where
+    ///       <c>VesselHUDController</c> auto-adopts a bar at runtime.
+    /// Run the menu item to report the fleet; flip <see cref="EnforceOnBuild"/> to make a
+    /// non-compliant fleet fail the build (kept off until the stub vessels are resurrected so
+    /// builds aren't broken by prefabs that are already unspawnable for other reasons).
     /// </summary>
     public static class VesselAbilityIconValidator
     {
-        // Set to true once every vessel HUD has a compliant VesselAbilityBar to make the four-icon
-        // contract a hard build gate ("impossible to ship a vessel without four icons").
+        // Set to true once every vessel prefab is compliant to make the four-icon contract a hard
+        // build gate ("impossible to ship a vessel without four icons").
         const bool EnforceOnBuild = false;
 
-        static readonly string[] SearchFolders =
-        {
-            "Assets/_Prefabs/Spacevessels",
-            "Assets/_Prefabs/UI Elements/VesselHUD",
-        };
+        const string VesselPrefabFolder = "Assets/_Prefabs/Spacevessels";
+        const string AbilitySetFolder = "Assets/Resources/VesselAbilitySets";
 
         [MenuItem("Tools/Cosmic Shore/Validate Vessel Ability Icons")]
         public static void Validate()
         {
-            var report = Scan(out int compliant, out int barNoSet, out int noBar, out int total);
-            Debug.Log($"[VesselAbilityIcons] Scanned {total} vessel/HUD prefab(s): " +
-                      $"{compliant} compliant, {barNoSet} bar-without-set, {noBar} missing a bar.\n{report}");
-
-            if (barNoSet > 0 || noBar > 0)
-                Debug.LogWarning("[VesselAbilityIcons] Some vessels do not yet present four ability " +
-                                 "icons. Add a VesselAbilityBar + a 4-slot VesselAbilitySetSO to each " +
-                                 "(unfilled slots show an obvious placeholder).");
+            var report = Scan(out int compliant, out int nonCompliant, out int total);
+            Debug.Log($"[VesselAbilityIcons] Scanned {total} vessel prefab(s): " +
+                      $"{compliant} compliant, {nonCompliant} non-compliant.\n{report}");
+
+            if (nonCompliant > 0)
+                Debug.LogWarning("[VesselAbilityIcons] Some vessels do not present four ability " +
+                                 "icons. Either author a VesselAbilityBar (+ 4-slot set) in the HUD " +
+                                 "prefab, or add a Resources/VesselAbilitySets/{VesselClassType} " +
+                                 "asset for runtime auto-adoption.");
         }
 
-        static string Scan(out int compliant, out int barNoSet, out int noBar, out int total)
+        static string Scan(out int compliant, out int nonCompliant, out int total)
         {
-            compliant = barNoSet = noBar = total = 0;
+            compliant = nonCompliant = total = 0;
             var sb = new StringBuilder();
 
-            var guids = AssetDatabase.FindAssets("t:Prefab", SearchFolders);
+            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { VesselPrefabFolder });
             var seen = new HashSet<string>();
 
             foreach (var guid in guids)
@@ -57,22 +62,37 @@ namespace CosmicShore.Editor
                 if (!go) continue;
 
                 total++;
+
+                // Authored path: a bar with a set somewhere under the vessel/HUD.
                 var bar = go.GetComponentInChildren<VesselAbilityBar>(true);
-                if (!bar)
-                {
-                    noBar++;
-                    sb.AppendLine($"  ✗ no ability bar   — {path}");
-                }
-                else if (!bar.HasAbilitySet)
+                if (bar && bar.HasAbilitySet)
                 {
-                    barNoSet++;
-                    sb.AppendLine($"  ⚠ bar, no set      — {path}");
+                    compliant++;
+                    sb.AppendLine($"  ✓ authored bar        — {path}");
+                    continue;
                 }
-                else
+
+                // Zero-wire path: an ability set exists for the vessel's class, so
+                // VesselHUDController auto-adopts a bar at runtime.
+                var status = go.GetComponentInChildren<VesselStatus>(true);
+                if (status)
                 {
-                    compliant++;
-                    sb.AppendLine($"  ✓ four icons       — {path}");
+                    var setPath = $"{AbilitySetFolder}/{status.VesselType}.asset";
+                    var set = AssetDatabase.LoadAssetAtPath<VesselAbilitySetSO>(setPath);
+                    if (set)
+                    {
+                        compliant++;
+                        sb.AppendLine($"  ✓ auto-adopt ({status.VesselType,-8}) — {path}");
+                        continue;
+                    }
+
+                    nonCompliant++;
+                    sb.AppendLine($"  ✗ no bar, no set for '{status.VesselType}' — {path}");
+                    continue;
                 }
+
+                nonCompliant++;
+                sb.AppendLine($"  ✗ no bar, no VesselStatus — {path}");
             }
 
             return sb.ToString();
@@ -86,11 +106,11 @@ namespace CosmicShore.Editor
             {
                 if (!EnforceOnBuild) return;
 
-                Scan(out _, out int barNoSet, out int noBar, out _);
-                if (barNoSet + noBar > 0)
+                Scan(out _, out int nonCompliant, out _);
+                if (nonCompliant > 0)
                     throw new BuildFailedException(
-                        $"[VesselAbilityIcons] {barNoSet + noBar} vessel/HUD prefab(s) do not present " +
-                        "four ability icons. Run Tools > Cosmic Shore > Validate Vessel Ability Icons.");
+                        $"[VesselAbilityIcons] {nonCompliant} vessel prefab(s) do not present four " +
+                        "ability icons. Run Tools > Cosmic Shore > Validate Vessel Ability Icons.");
             }
         }
     }
diff --git a/Assets/_Scripts/UI/Controller/VesselAbilityBar.cs b/Assets/_Scripts/UI/Controller/VesselAbilityBar.cs
index 164eb234e..134edf14e 100644
--- a/Assets/_Scripts/UI/Controller/VesselAbilityBar.cs
```

</details>

### `f5a0b6017` — refactor(vessels): four-icon contract binds to existing HUD icons — no visible change

_Claude, 2026-07-13 17:33:58 +0000_

```text
Flying the Squirrel showed four new placeholder icons NEXT TO its four existing icons
— a parallel row, not a unified system. The contract is a refactor: for a vessel that
already presents four icons, adopting it must be invisible to the player.

- VesselHUDView.GetAbilitySlotImage(slotIndex): a view binds the Image that ALREADY
  presents each ability slot (slot order matches the vessel's VesselAbilitySetSO).
  Bound slots are rendered and animated entirely by the view/controller's own logic —
  the bar never repaints, lights, or duplicates them.
- All six flyable views bind their existing icons: Squirrel drift/boost/danger/shield
  (all 4 — zero change), Sparrow fire-highlight/missile/stance/heat (all 4), Rhino
  forcefield/crystal/line/debuff (all 4), Manta overcharge dial (1 of 4), Dolphin
  charge meter (1 of 4), Serpent boost pips + wall icon (2 of 4).
- VesselAbilityBar resolves per slot: authored-on-bar image → view-bound icon →
  self-built fallback placeholder. Only genuinely missing icons render in the fallback
  row, so Manta/Dolphin/Serpent show placeholders exactly where icons are missing.
- VesselHUDController.TryAutoAdoptAbilityBar now skips creation entirely when the view
  presents all four — Squirrel/Sparrow/Rhino get zero new objects.
- Squirrel/Rhino ability sets rewritten so slot labels/descriptions mirror the bound
  icons (Drift / Skim Boost / Joust / Crystal Shield; Forcefield / Crystal Stun /
  Danger Slab / Skim Debuff).
```

```text
 Assets/Resources/VesselAbilitySets/Rhino.asset       | 26 +++++++++----------
 Assets/Resources/VesselAbilitySets/Squirrel.asset    | 22 ++++++++--------
 Assets/_Scripts/UI/Controller/VesselAbilityBar.cs    | 70 ++++++++++++++++++++++++++++++--------------------
 Assets/_Scripts/UI/Controller/VesselHUDController.cs | 28 ++++++++++++++------
 Assets/_Scripts/UI/View/DolphinVesselHUDView.cs      |  5 ++++
 Assets/_Scripts/UI/View/MantaVesselHUDView.cs        |  5 ++++
 Assets/_Scripts/UI/View/RhinoVesselHUDView.cs        | 11 ++++++++
 Assets/_Scripts/UI/View/SerpentVesselHUDView.cs      | 10 ++++++++
 Assets/_Scripts/UI/View/SparrowHUDView.cs            | 11 ++++++++
 Assets/_Scripts/UI/View/SquirrelVesselHUDView.cs     | 11 ++++++++
 Assets/_Scripts/UI/View/VesselHUDView.cs             | 19 ++++++++++++++
 Docs/VESSELS/FLEET_STATUS.md                         | 21 ++++++++++-----
 12 files changed, 172 insertions(+), 67 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 376 lines)</summary>

```diff
diff --git a/Assets/_Scripts/UI/Controller/VesselAbilityBar.cs b/Assets/_Scripts/UI/Controller/VesselAbilityBar.cs
index 134edf14e..afee27c42 100644
--- a/Assets/_Scripts/UI/Controller/VesselAbilityBar.cs
+++ b/Assets/_Scripts/UI/Controller/VesselAbilityBar.cs
@@ -7,42 +7,46 @@ using UnityEngine.UI;
 namespace CosmicShore.UI
 {
     /// <summary>
-    /// Guarantees a vessel presents exactly four ability icons, and lights each while its ability's
-    /// input is held.
+    /// Completes the four-icon contract for a vessel: every vessel presents exactly four ability
+    /// icons, where the icons the HUD ALREADY shows count first.
     ///
-    /// PREFERRED path — author the four icon <see cref="Image"/>s in the HUD prefab and assign them
-    /// to <see cref="slotImages"/>. They then load with the HUD like any other element: zero runtime
-    /// allocation, and no work on the vessel-spawn/swap hotpath.
+    /// Per slot, resolution order:
+    ///   1. <see cref="slotImages"/> — an icon authored on this bar in the HUD prefab.
+    ///   2. <c>VesselHUDView.GetAbilitySlotImage(i)</c> — an existing view icon BOUND to the slot.
+    ///      Bound slots are presented and animated entirely by the view/controller's own logic;
+    ///      the bar does not touch them, so adopting the contract is a pure refactor for a view
+    ///      that already shows four icons (the player sees no change — e.g. Squirrel).
+    ///   3. Fallback — the bar builds an icon showing the set's sprite, or the obvious
+    ///      code-generated placeholder, and lights it while its input is held. Only genuinely
+    ///      missing icons render here, so a vessel cannot present fewer than four.
     ///
-    /// FALLBACK path — any slot left unassigned is built once at <see cref="Initialize"/> so a
-    /// not-yet-authored vessel (or a stub) still shows four icons, with an obvious placeholder.
-    /// This is the exceptional path, so its one-time construction cost is irrelevant.
-    ///
-    /// No pooling: a bar is created at most once per vessel and lives for the HUD's lifetime — there
-    /// is nothing high-frequency to pool, and the structure is never rebuilt (it survives HUD
-    /// show/hide untouched). Unfilled abilities show a code-generated placeholder sprite.
+    /// No pooling: a bar exists at most once per vessel and lives for the HUD's lifetime; the
+    /// structure is never rebuilt (it survives HUD show/hide untouched).
     /// </summary>
     public sealed class VesselAbilityBar : MonoBehaviour
     {
         [Header("Data — the four player-facing abilities")]
         [SerializeField] private VesselAbilitySetSO abilitySet;
 
-        [Header("Icons — assign four in the HUD prefab (preferred, zero runtime alloc)")]
+        [Header("Icons — authored on the bar (highest precedence, zero runtime alloc)")]
         [SerializeField] private Image[] slotImages = new Image[VesselAbilitySetSO.SlotCount];
 
-        [Header("Fallback layout — only used to self-build unassigned slots")]
+        [Header("Fallback layout — only used to self-build genuinely missing slots")]
         [SerializeField] private RectTransform fallbackContainer;
         [SerializeField] private Vector2 fallbackIconSize = new(96f, 96f);
         [SerializeField] private float fallbackSpacing = 16f;
         [SerializeField] private Vector2 fallbackAnchoredOffset = new(0f, 24f);
 
-        [Header("Active-state feel")]
+        [Header("Active-state feel (fallback icons only)")]
         [SerializeField, Range(0f, 1f)] private float idleAlpha = 0.55f;
         [SerializeField, Range(0f, 1f)] private float activeAlpha = 1f;
         [SerializeField] private float activeScale = 1.15f;
 
-        readonly Image[] _icons = new Image[VesselAbilitySetSO.SlotCount];
+        // Icons the bar OWNS (authored-on-bar or self-built fallback). View-bound slots stay null
+        // here — their rendering belongs to the view and must never be repainted or lit by the bar.
+        readonly Image[] _ownedIcons = new Image[VesselAbilitySetSO.SlotCount];
         R_VesselActionHandler _actions;
+        VesselHUDView _view;
         bool _subscribed;
         bool _built;
 
@@ -57,9 +61,10 @@ namespace CosmicShore.UI
             if (_built) Repaint();
         }
 
-        public void Initialize(IVesselStatus status)
+        public void Initialize(IVesselStatus status, VesselHUDView view = null)
         {
             _actions = status?.ActionHandler;
+            if (view) _view = view;
             ResolveIcons();
             Subscribe();
         }
@@ -94,17 +99,26 @@ namespace CosmicShore.UI
         {
             if (_built)
             {
-                Repaint(); // re-Initialize (vessel swap) just repaints the existing structure
+                Repaint(); // re-Initialize (vessel swap) just repaints the owned icons
                 return;
             }
 
             for (int i = 0; i < SlotCount; i++)
             {
-                // Preferred: an icon authored in the prefab — no allocation.
-                var img = (slotImages != null && i < slotImages.Length) ? slotImages[i] : null;
-                // Fallback: self-build a missing slot once, so the four-icon contract still holds.
-                if (!img) img = BuildFallbackIcon(i);
-                _icons[i] = img;
+                // 1. Authored on the bar in the HUD prefab.
+                var authored = (slotImages != null && i < slotImages.Length) ? slotImages[i] : null;
+                if (authored)
+                {
+                    _ownedIcons[i] = authored;
+                    continue;
+                }
+
+                // 2. Bound to an existing view icon — the view presents it; the bar stays out.
+                if (_view && _view.GetAbilitySlotImage(i))
+                    continue;
+
+                // 3. Genuinely missing — self-build with the set icon / placeholder.
+                _ownedIcons[i] = BuildFallbackIcon(i);
             }
 
             _built = true;
@@ -112,15 +126,15 @@ namespace CosmicShore.UI
 
             if (!abilitySet)
                 Debug.LogError($"[VesselAbilityBar] No VesselAbilitySetSO assigned on '{name}'. " +
-                               "Showing four placeholders — every vessel must have a 4-slot ability set.");
+                               "Showing placeholders — every vessel must have a 4-slot ability set.");
         }
 
         void Repaint()
         {
             for (int i = 0; i < SlotCount; i++)
             {
-                var img = _icons[i];
-                if (!img) continue;
+                var img = _ownedIcons[i];
+                if (!img) continue; // view-bound slot — the view owns its rendering
 
                 var slot = abilitySet ? abilitySet.GetSlot(i) : default;
                 bool hasIcon = slot.HasIcon;
@@ -183,8 +197,8 @@ namespace CosmicShore.UI
             {
                 if (abilitySet.GetSlot(i).Input != input) continue;
 
-                var img = _icons[i];
-                if (!img) continue;
+                var img = _ownedIcons[i];
+                if (!img) continue; // view-bound slots animate via their own controller juice
 
                 var c = img.color;
                 c.a = active ? activeAlpha : idleAlpha;
diff --git a/Assets/_Scripts/UI/Controller/VesselHUDController.cs b/Assets/_Scripts/UI/Controller/VesselHUDController.cs
index 1c47760a7..8feb8f645 100644
--- a/Assets/_Scripts/UI/Controller/VesselHUDController.cs
+++ b/Assets/_Scripts/UI/Controller/VesselHUDController.cs
@@ -33,21 +33,24 @@ namespace CosmicShore.UI
 
             baseView?.Initialize();
 
```

</details>
