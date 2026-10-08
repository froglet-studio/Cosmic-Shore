# Branch archive: `claude/tetrahedral-collider-cost-f5bfc5`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-07-22 by Claude
- **Unmerged commits:** 3
- **Forked from:** `6fae9ab38` (2026-07-21, Merge pull request #616 from froglet-studio/claude/rhino-skimmer-scale-fix-pvq)
- **Tip:** `d70f12a5a`
- **Files touched (17):**
  - `Assets/_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/Impactors/PrismImpactor.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs`
  - `Assets/_Scripts/Controller/Managers/PrismOctahedronShieldManager.cs`
  - `Assets/_Scripts/Controller/Vessel/Prism.cs`
  - `Assets/_Scripts/Controller/Vessel/PrismOctahedronShield.cs`
  - `Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoBlockShootActionExecutor.cs`
  - `CLAUDE.md`
  - `Docs/ECOSYSTEM.md`
  - `Docs/ElementalAbilitySystem/ARCHITECTURE.md`
  - `Docs/ElementalAbilitySystem/AUDIT.md`
  - `Docs/EnvironmentSpawning/UNIFICATION_ASSESSMENT.md`
  - `Docs/PERFORMANCE_OPTIMIZATION.md`
  - `Docs/PRISM_ECS_MIGRATION.md`
  - `Docs/ToySystem/ARCHITECTURE.md`

### `e215a4f2d` — perf(prisms): shield colliders drop MeshCollider for AABB box proxy + analytic narrowphase gate

_Claude, 2026-07-22 03:53:33 +0000_

```text
Engaged shields (octahedron shielded state AND stellated super-shield) no
longer swap the prism's BoxCollider for a convex MeshCollider. Instead:

- The PhysX trigger while engaged is a shield AABB proxy BoxCollider
  (Prism.SetShieldColliderState; one lazily-created proxy serves both
  shield tiers - same shieldScale, same AABB). For the stellation the AABB
  IS the convex hull PhysX computed anyway (octahedron vertices sit on the
  spike-tip cube's face centers), so broadphase behavior is identical at
  BoxCollider cost.
- The exact octahedron/stellation surface is enforced by an analytic
  narrowphase gate (IShieldContainmentGate -> Prism.ActiveShieldGate,
  checked both directions in ImpactorBase.OnTriggerEnter via the L1 /
  4-linear-form containment tests that were previously dead API).
- Prism.SetColliderCulledByLod now covers the proxy, closing the
  documented LOD exemption: shielded prisms are LOD-cullable like any
  other prism (previously an always-on budget line - AstroLeague 240-prism
  lining, Wanderway palette caps, MASS-5 turret, BACKLOG 2.3).
- Shield collider selection routes through the prism and composes with
  destruction, the LOD cull, and the spawn window - fixes the wart where a
  pooled IsShielded prism re-enabled its box under the engaged shield
  collider (double trigger), and destroyed prisms no longer strand an
  enabled shield collider.
- PrismStellatedOctahedronShield completes the centralized-ticking
  migration: ticked by PrismOctahedronShieldManager (IPrismShieldTicker)
  instead of a per-instance Update().
- Tester harness (no Prism) keeps working via a legacy in-shield proxy.

Collider-budget impact: negative. Active collider count for shielded
prisms is unchanged near foci (exactly one trigger before and after),
newly reclaimable far from foci, and no convex mesh cooks remain.

Docs: CLAUDE.md Shield octahedra row; ECOSYSTEM.md sec 14 collider bullet;
ElementalAbilitySystem ARCHITECTURE/AUDIT budget lines marked resolved;
ToySystem + EnvironmentSpawning cap rationale updated;
PERFORMANCE_OPTIMIZATION.md shipped entry; PRISM_ECS_MIGRATION.md phrase.
```

```text
 Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs  |  23 ++++++
 Assets/_Scripts/Controller/ImpactEffects/Impactors/PrismImpactor.cs |  14 +++-
 Assets/_Scripts/Controller/Managers/PrismOctahedronShieldManager.cs |  42 +++++++---
 Assets/_Scripts/Controller/Vessel/Prism.cs                          | 151 ++++++++++++++++++++++++++++++++--
 Assets/_Scripts/Controller/Vessel/PrismOctahedronShield.cs          | 118 +++++++++++++++++---------
 Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs | 150 ++++++++++++++++++++++-----------
 CLAUDE.md                                                           |   2 +-
 Docs/ECOSYSTEM.md                                                   |  14 ++--
 Docs/ElementalAbilitySystem/ARCHITECTURE.md                         |  12 +--
 Docs/ElementalAbilitySystem/AUDIT.md                                |  12 +--
 Docs/EnvironmentSpawning/UNIFICATION_ASSESSMENT.md                  |   9 +-
 Docs/PERFORMANCE_OPTIMIZATION.md                                    |  22 +++++
 Docs/PRISM_ECS_MIGRATION.md                                         |   2 +-
 Docs/ToySystem/ARCHITECTURE.md                                      |   7 +-
 14 files changed, 442 insertions(+), 136 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 985 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
index a94c83aca..29d1ea0f3 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
@@ -44,6 +44,22 @@ namespace CosmicShore.Gameplay
             if (!other.TryGetComponent(out ImpactCollider impacteeCollider))
                 return;
 
+            // Shield narrowphase: an engaged prism shield's PhysX trigger is its box
+            // AABB proxy, so a contact can land in the AABB's corner/notch regions
+            // outside the true octahedron/stellation surface. Reject those with the
+            // shield's analytic containment test (a few linear forms — cheaper than
+            // the convex MeshCollider narrowphase this replaced). Checked in both
+            // directions: the impactee's shield (their proxy fired our trigger) and
+            // our own (we are a shielded prism receiving a toucher).
+            if (impacteeCollider.Impactor is PrismImpactor prismImpactee)
+            {
+                var gate = prismImpactee.Prism != null ? prismImpactee.Prism.ActiveShieldGate : null;
+                if (gate != null && !gate.ContainsWorldPoint(other.ClosestPoint(transform.position)))
+                    return;
+            }
+            if (!PassesOwnShieldNarrowphase(other))
+                return;
+
             if (!_acceptMarkerInit)
             {
                 _acceptMarkerInit = true;
@@ -52,5 +68,12 @@ namespace CosmicShore.Gameplay
             using (_acceptMarker.Auto())
                 AcceptImpactee(impacteeCollider.Impactor);
         }
+
+        /// <summary>
+        /// Self-side shield narrowphase — overridden by <see cref="PrismImpactor"/>
+        /// to test the toucher against this prism's engaged shield surface. Default:
+        /// pass (only prisms carry shields).
+        /// </summary>
+        protected virtual bool PassesOwnShieldNarrowphase(Collider other) => true;
     }
 }
\ No newline at end of file
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/PrismImpactor.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/PrismImpactor.cs
index 412015fcb..4ddf158ec 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/PrismImpactor.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/PrismImpactor.cs
@@ -23,7 +23,19 @@ namespace CosmicShore.Gameplay
         {
             Prism ??= GetComponent<Prism>();
         }
-        
+
+        /// <summary>
+        /// Self-side shield narrowphase: while a shield is engaged, this prism's
+        /// PhysX trigger is the shield's box AABB proxy — reject touchers whose
+        /// nearest point to us lies in the AABB's corner/notch regions outside the
+        /// true octahedron/stellation surface.
+        /// </summary>
+        protected override bool PassesOwnShieldNarrowphase(Collider other)
+        {
+            var gate = Prism != null ? Prism.ActiveShieldGate : null;
+            return gate == null || gate.ContainsWorldPoint(other.ClosestPoint(transform.position));
+        }
+
         protected override void AcceptImpactee(IImpactor impactee)
         {    
             switch (impactee)
diff --git a/Assets/_Scripts/Controller/Managers/PrismOctahedronShieldManager.cs b/Assets/_Scripts/Controller/Managers/PrismOctahedronShieldManager.cs
index 9ffe8b020..06cd84d49 100644
--- a/Assets/_Scripts/Controller/Managers/PrismOctahedronShieldManager.cs
+++ b/Assets/_Scripts/Controller/Managers/PrismOctahedronShieldManager.cs
@@ -5,13 +5,27 @@ using UnityEngine;
 namespace CosmicShore.Gameplay
 {
     /// <summary>
-    /// Central ticker for <see cref="PrismOctahedronShield"/> engage/shatter
-    /// transitions. Replaces a per-shield MonoBehaviour <c>Update()</c>: at high prism
-    /// counts every prism carries an octahedron shield, so thousands of Update()
-    /// invocations ran every frame just to early-return (profiled: 9234 calls, ~1.3ms
-    /// plus the BehaviourUpdate dispatch overhead). Shields register here ONLY while
-    /// actively morphing — the idle majority cost nothing. Mirrors PrismTimerManager /
-    /// PrismScaleManager (centralized ticking of the few active members).
+    /// Ticked by <see cref="PrismOctahedronShieldManager"/> ONLY while an
+    /// engage/shatter transition is in flight. Implemented by
+    /// <see cref="PrismOctahedronShield"/> and <see cref="PrismStellatedOctahedronShield"/>.
+    /// Return true while still transitioning; the manager drops the shield when
+    /// this returns false. Implementers are MonoBehaviours (the manager's
+    /// destroyed-check relies on it).
+    /// </summary>
+    public interface IPrismShieldTicker
+    {
+        bool Tick(float dt);
+    }
+
+    /// <summary>
+    /// Central ticker for shield engage/shatter transitions (octahedron AND
+    /// stellated super-shield). Replaces a per-shield MonoBehaviour <c>Update()</c>:
+    /// at high prism counts every prism carries an octahedron shield, so thousands
+    /// of Update() invocations ran every frame just to early-return (profiled: 9234
+    /// calls, ~1.3ms plus the BehaviourUpdate dispatch overhead). Shields register
+    /// here ONLY while actively morphing — the idle majority cost nothing. Mirrors
+    /// PrismTimerManager / PrismScaleManager (centralized ticking of the few active
+    /// members).
     /// </summary>
     [DisallowMultipleComponent]
     public class PrismOctahedronShieldManager : Singleton<PrismOctahedronShieldManager>
@@ -27,20 +41,20 @@ namespace CosmicShore.Gameplay
             return go.AddComponent<PrismOctahedronShieldManager>();
         }
 
-        readonly HashSet<PrismOctahedronShield> _active = new();
-        readonly List<PrismOctahedronShield> _scratch = new(64);
+        readonly HashSet<IPrismShieldTicker> _active = new();
+        readonly List<IPrismShieldTicker> _scratch = new(64);
 
         /// <summary>Concurrently morphing shields (telemetry).</summary>
         public int ActiveCount => _active.Count;
 
-        public void Register(PrismOctahedronShield shield)
+        public void Register(IPrismShieldTicker shield)
         {
             if (shield != null) _active.Add(shield);
         }
 
-        public void Unregister(PrismOctahedronShield shield)
+        public void Unregister(IPrismShieldTicker shield)
         {
-            _active.Remove(shield);
+            if (shield != null) _active.Remove(shield);
         }
 
         void Update()
@@ -54,7 +68,9 @@ namespace CosmicShore.Gameplay
             for (int i = 0; i < _scratch.Count; i++)
             {
                 var s = _scratch[i];
-                if (s == null) { _active.Remove(s); continue; }
+                // Interface-typed null check misses destroyed Unity objects — route
+                // through the MonoBehaviour overload (implementers are components).
+                if (s is not MonoBehaviour mb || mb == null) { _active.Remove(s); continue; }
                 if (!s.Tick(dt)) _active.Remove(s); // transition complete — stop ticking
             }
         }
diff --git a/Assets/_Scripts/Controller/Vessel/Prism.cs b/Assets/_Scripts/Controller/Vessel/Prism.cs
index 53f912e49..fc74d62cd 100644
--- a/Assets/_Scripts/Controller/Vessel/Prism.cs
+++ b/Assets/_Scripts/Controller/Vessel/Prism.cs
@@ -12,6 +12,32 @@ using CosmicShore.ECS;
 using System.Linq;
 namespace CosmicShore.Gameplay
 {
```

</details>

### `6c69d06f6` — fix(prisms): harden the shield AABB-proxy gate against the review findings

_Claude, 2026-07-22 05:00:10 +0000_

```text
Fixes the five defects confirmed by adversarial review of e215a4f2:

1. CRITICAL - enter-only narrowphase gate: a contact whose first overlap
   landed in the AABB over-cover (corners/notches) was rejected once and
   PhysX never re-fires Enter for the pair, making engaged shields largely
   intangible off face-center approaches. ImpactorBase now keeps rejected
   pairs in a pending list and re-tests them every OnTriggerStay until the
   toucher crosses the true analytic surface (or the overlap ends) - the
   impact then dispatches exactly where the old surface collider would
   have fired. SkimmerImpactor's private Stay/Exit became overrides that
   call base (they would otherwise hide the base re-test path).

2. MAJOR - gate direction asymmetry: both directions now test the SAME
   conceptual point (the point on the TOUCHER's collider nearest the
   shield center) so the two halves of one physical impact agree - the
   impactee direction uses the toucher's own cached collider vs the proxy
   bounds center instead of a point on the proxy's surface.

3. MAJOR - LOD cull landing inside the ~0.35s engage morph snapshotted
   "no collider was on" and un-cull then stranded a visible engaged prism
   with no trigger at all (unpoppable shield). Morphing now counts as ON
   in the cull snapshot - it always settles into a collider-on state.

4. MAJOR - legacy-physics AOE path: gating explosion enters broke the
   super-shield blocks-the-explosion contract. ExplosionImpactor opts out
   via UsesShieldNarrowphase=false (explosions are volumetric; matches
   the ungated center-based Burst batch path).

5. MINOR - proxy geometry is now (re)applied on every engage (tracks the
   engaging tier's shieldScale + re-cached authored geometry), and the
   FullAuto turret anchor's enable-all collider restore skips the
   shield-owned proxy (was re-enabling a stale un-gated 3x trigger).
```

```text
 .../_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs  |   6 ++
 Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs    | 116 ++++++++++++++++++++++++++++----
 Assets/_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs |  13 +++-
 Assets/_Scripts/Controller/Vessel/Prism.cs                            |  42 +++++++++---
 .../R_VesselActions/Executors/FullAutoBlockShootActionExecutor.cs     |   7 ++
 5 files changed, 159 insertions(+), 25 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 300 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs
index fe42caade..0866745b0 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs
@@ -26,6 +26,12 @@ namespace CosmicShore.Gameplay
 
         public bool IsBatchProcessing => _useBatchProcessing;
 
+        // Explosions are volumetric, not surface contacts: the shield AABB proxy's
+        // over-cover is the correct containment read for an expanding blast, and
+        // gating would break the super-shield blocks-the-explosion contract on this
+        // legacy physics path (the batch path is center-based and ungated too).
+        protected override bool UsesShieldNarrowphase => false;
+
         /// <summary>
         /// When true, BeginBatchProcessing() is a no-op - forces Physics OnTriggerEnter
         /// for all collisions. Used by AOEBenchmarkOverlay for A/B comparison.
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
index 29d1ea0f3..1f7b4ed06 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
@@ -1,4 +1,5 @@
 using System;
+using System.Collections.Generic;
 using Unity.Netcode;
 using Unity.Profiling;
 using UnityEngine;
@@ -30,6 +31,41 @@ namespace CosmicShore.Gameplay
         ProfilerMarker _acceptMarker;
         bool _acceptMarkerInit;
 
+        // Own collider, lazily cached for the shield-narrowphase test point (one
+        // typed lookup per component lifetime — same budget rule as the marker).
+        Collider _ownCollider;
+        bool _ownColliderInit;
+
+        // Colliders whose Enter was rejected by the shield narrowphase. The proxy
+        // AABB over-covers the true shield surface and PhysX fires Enter only once
+        // per overlap episode, so rejected pairs are re-tested every OnTriggerStay
+        // until they cross the analytic surface or the overlap ends — otherwise a
+        // corner/notch approach that later reaches the real shield would be lost
+        // and shielded prisms would be intangible to most trajectories. Lazily
+        // allocated; empty for every impactor not currently grazing a shield AABB.
+        List<Collider> _pendingShieldContacts;
+
+        /// <summary>
+        /// Impactors that are volumetric rather than surface-contact (AOE
+        /// explosions) opt out — for them the AABB over-cover is the correct
+        /// containment read, and gating would break the super-shield
+        /// blocks-the-explosion contract on the legacy physics path.
+        /// </summary>
+        protected virtual bool UsesShieldNarrowphase => true;
+
+        Collider OwnCollider
+        {
+            get
+            {
+                if (!_ownColliderInit)
+                {
+                    _ownColliderInit = true;
+                    _ownCollider = GetComponent<Collider>();
+                }
+                return _ownCollider;
+            }
+        }
+
         protected virtual void OnTriggerEnter(Collider other)
         {
             if (!isInitialized)
@@ -44,22 +80,47 @@ namespace CosmicShore.Gameplay
             if (!other.TryGetComponent(out ImpactCollider impacteeCollider))
                 return;
 
-            // Shield narrowphase: an engaged prism shield's PhysX trigger is its box
-            // AABB proxy, so a contact can land in the AABB's corner/notch regions
-            // outside the true octahedron/stellation surface. Reject those with the
-            // shield's analytic containment test (a few linear forms — cheaper than
-            // the convex MeshCollider narrowphase this replaced). Checked in both
-            // directions: the impactee's shield (their proxy fired our trigger) and
-            // our own (we are a shielded prism receiving a toucher).
-            if (impacteeCollider.Impactor is PrismImpactor prismImpactee)
+            if (UsesShieldNarrowphase && !ShieldNarrowphasePasses(other, impacteeCollider.Impactor))
             {
-                var gate = prismImpactee.Prism != null ? prismImpactee.Prism.ActiveShieldGate : null;
-                if (gate != null && !gate.ContainsWorldPoint(other.ClosestPoint(transform.position)))
-                    return;
+                // Not (yet) touching the true shield surface — keep the pair and
+                // re-test in OnTriggerStay as the overlap deepens.
+                _pendingShieldContacts ??= new List<Collider>(4);
+                if (!_pendingShieldContacts.Contains(other))
+                    _pendingShieldContacts.Add(other);
+                return;
             }
-            if (!PassesOwnShieldNarrowphase(other))
+
+            DispatchImpact(impacteeCollider);
+        }
+
+        protected virtual void OnTriggerStay(Collider other)
+        {
+            // Hot path for every persisting overlap — bail on the common case first.
+            if (_pendingShieldContacts == null || _pendingShieldContacts.Count == 0)
+                return;
+            if (!_pendingShieldContacts.Contains(other))
                 return;
 
+            if (!other.TryGetComponent(out ImpactCollider impacteeCollider))
+            {
+                _pendingShieldContacts.Remove(other);
+                return;
+            }
+
+            if (!ShieldNarrowphasePasses(other, impacteeCollider.Impactor))
+                return; // still in the over-cover region — keep waiting
+
+            _pendingShieldContacts.Remove(other);
+            DispatchImpact(impacteeCollider);
+        }
+
+        protected virtual void OnTriggerExit(Collider other)
+        {
+            _pendingShieldContacts?.Remove(other);
+        }
+
+        void DispatchImpact(ImpactCollider impacteeCollider)
+        {
             if (!_acceptMarkerInit)
             {
                 _acceptMarkerInit = true;
@@ -69,6 +130,37 @@ namespace CosmicShore.Gameplay
                 AcceptImpactee(impacteeCollider.Impactor);
         }
 
+        /// <summary>
+        /// Shield narrowphase: an engaged prism shield's PhysX trigger is its box
+        /// AABB proxy, so a contact can start in the AABB's corner/notch regions
+        /// outside the true octahedron/stellation surface. Both directions use the
+        /// SAME conceptual test point — the point on the TOUCHER's collider nearest
+        /// the shield's center — so the two halves of one physical impact agree:
+        /// the impactee direction tests our own collider against their shield, and
+        /// the self direction (we are the shielded prism) tests the toucher against
+        /// ours via <see cref="PassesOwnShieldNarrowphase"/>.
+        /// </summary>
+        bool ShieldNarrowphasePasses(Collider other, IImpactor impactee)
+        {
+            if (impactee is PrismImpactor prismImpactee)
+            {
+                var prism = prismImpactee.Prism;
+                var gate = prism != null ? prism.ActiveShieldGate : null;
+                if (gate != null)
```

</details>

### `d70f12a5a` — fix(prisms): purge stale pending shield contacts on Enter-pass and OnDisable

_Claude, 2026-07-22 05:07:50 +0000_

```text
Verification hardening: a missed OnTriggerExit could leave a stale pending
entry that double-dispatches the next overlap episode (Enter-pass then the
next Stay finding the leftover entry). Purge the pair on every passing
Enter and clear the list when the impactor is disabled (pooling/teardown).
```

```text
 Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs | 10 ++++++++++
 1 file changed, 10 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
index 1f7b4ed06..ab91cfcaa 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
@@ -90,6 +90,9 @@ namespace CosmicShore.Gameplay
                 return;
             }
 
+            // A stale pending entry for this collider (missed Exit on a prior
+            // overlap episode) must not double-dispatch via the next Stay.
+            _pendingShieldContacts?.Remove(other);
             DispatchImpact(impacteeCollider);
         }
 
@@ -119,6 +122,13 @@ namespace CosmicShore.Gameplay
             _pendingShieldContacts?.Remove(other);
         }
 
+        protected virtual void OnDisable()
+        {
+            // Overlap episodes end with the component — never carry pending
+            // shield contacts across a disable (pooling, scene teardown).
+            _pendingShieldContacts?.Clear();
+        }
+
         void DispatchImpact(ImpactCollider impacteeCollider)
         {
             if (!_acceptMarkerInit)
```

</details>
