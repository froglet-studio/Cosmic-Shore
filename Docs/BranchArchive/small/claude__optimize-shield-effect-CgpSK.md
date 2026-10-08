# Branch archive: `claude/optimize-shield-effect-CgpSK`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-04-15 by Claude
- **Unmerged commits:** 1
- **Forked from:** `0b34ad3c3` (2026-04-15, Reset crystal count in Hex Race to Default)
- **Tip:** `e6e90a34e`
- **Files touched (5):**
  - `Assets/_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs`
  - `Assets/_Scripts/Controller/Managers/PrismAOERegistry.cs`
  - `Assets/_Scripts/Controller/Managers/PrismShieldBroadcaster.cs`
  - `Assets/_Scripts/Controller/Managers/PrismStateManager.cs`
  - `Assets/_Scripts/Controller/Vessel/Prism.cs`

### `e6e90a34e` — feat(prisms): coalesce shield events into single shockwave per wave origin

_Claude, 2026-04-15 18:09:59 +0000_

```text
Phase 1 of the shield effect redesign. When many prisms shield or unshield
at once (AOE explosions, crystal collisions, assembler shields), the current
system queues N material animations + plays N stacked SFX, and the visual
reads as a subtle simultaneous tint change rather than a single event.

New PrismShieldBroadcaster singleton collects shield state changes in a
frame-scoped queue, coalesces them by domain + spatial proximity, and fires
one procedural LineRenderer shockwave ring + one SFX per coalesced event.
Wave origin is threaded through Prism.OnTriggerEnter/Exit, PrismAOERegistry
burst-job post-processing, and ExplosionImpactor so AOE shields all share a
single wave origin and merge into one expanding ring.

Shield material transition duration drops from 0.8s to 0.15s so the
MaterialStateManager animation job queue drains faster and the transition
reads as an event instead of a slow fade. Per-prism shield SFX are
suppressed whenever the broadcaster owns shield audio (default on).

Pure code — no prefabs, shaders, or scene wiring required. Broadcaster
auto-creates itself via EnsureInstance pattern matching PrismTimerManager.
Shielded steady-state readability is deferred to a follow-up shader pass.
```

```text
 .../_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs  |   9 +-
 Assets/_Scripts/Controller/Managers/PrismAOERegistry.cs               |   8 +-
 Assets/_Scripts/Controller/Managers/PrismShieldBroadcaster.cs         | 333 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/Controller/Managers/PrismStateManager.cs              |  59 +++++-
 Assets/_Scripts/Controller/Vessel/Prism.cs                            |   6 +-
 5 files changed, 402 insertions(+), 13 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 534 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs
index e8d9b015b..a6b26c057 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs
@@ -127,10 +127,13 @@ namespace CosmicShore.Gameplay
             } 
             if ((prism.Domain == explosion.Domain && !affectSelf) || !destructive)
             {
+                // Use the explosion's world position as the shield wave origin so
+                // all prisms caught by the same explosion merge into one shockwave.
+                Vector3 originWS = explosion != null ? explosion.transform.position : prism.transform.position;
                 if (shielding && prism.Domain == explosion.Domain)
-                    prism.ActivateShield();
-                else 
-                    prism.ActivateShield(2f);
+                    prism.ActivateShield(originWS);
+                else
+                    prism.ActivateShield(2f, originWS);
                 return;
             }
             
diff --git a/Assets/_Scripts/Controller/Managers/PrismAOERegistry.cs b/Assets/_Scripts/Controller/Managers/PrismAOERegistry.cs
index 734691f3a..62481bd3f 100644
--- a/Assets/_Scripts/Controller/Managers/PrismAOERegistry.cs
+++ b/Assets/_Scripts/Controller/Managers/PrismAOERegistry.cs
@@ -363,13 +363,15 @@ namespace CosmicShore.Gameplay
                     // Fall through — original code does NOT return/continue here
                 }
 
-                // Same team (and not affectSelf) or non-destructive: shield the prism
+                // Same team (and not affectSelf) or non-destructive: shield the prism.
+                // Pass the explosion center as the shield wave origin so all prisms in
+                // this AOE batch coalesce into a single shockwave event, not N of them.
                 if ((prismDomain == expDomain && !affectSelf) || !destructive)
                 {
                     if (shielding && prismDomain == expDomain)
-                        prism.ActivateShield();
+                        prism.ActivateShield(center);
                     else
-                        prism.ActivateShield(2f);
+                        prism.ActivateShield(2f, center);
                     UpdateShieldState(idx, true, false);
                     continue;
                 }
diff --git a/Assets/_Scripts/Controller/Managers/PrismShieldBroadcaster.cs b/Assets/_Scripts/Controller/Managers/PrismShieldBroadcaster.cs
new file mode 100644
index 000000000..7b3e09826
--- /dev/null
+++ b/Assets/_Scripts/Controller/Managers/PrismShieldBroadcaster.cs
@@ -0,0 +1,333 @@
+using System.Collections.Generic;
+using CosmicShore.Core;
+using CosmicShore.Data;
+using CosmicShore.Utility;
+using UnityEngine;
+
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// Phase 1 of the shield effect redesign.
+    ///
+    /// Coalesces per-prism shield state changes into a small number of spatial
+    /// "shield events" and fires one procedural shockwave ring + one SFX per
+    /// event, instead of N per-prism animations + N SFX plays. When 200 prisms
+    /// shield together from the same AOE, the player sees and hears a single
+    /// wave event, not 200 overlapping smooth color tweens.
+    ///
+    /// Concerns addressed:
+    ///   (a) Per-prism shield SFX are de-duplicated to one per coalesced event.
+    ///       Material animation duration is also shortened at the state manager
+    ///       level so the animation job queue drains faster.
+    ///   (b) The expanding ring visual + single loud SFX hit reads as an event,
+    ///       rather than a subtle simultaneous color lerp on many prisms.
+    ///   (c) Shielded *steady state* readability is deferred to a follow-up
+    ///       shader pass — this phase only changes the transition moment.
+    ///
+    /// This class is fully code-driven: no prefabs, no materials, no shaders
+    /// required. It auto-creates itself on first use (EnsureInstance pattern
+    /// matching PrismTimerManager) and builds its shockwave pool at runtime
+    /// from LineRenderers with a Sprites/Default material.
+    /// </summary>
+    public class PrismShieldBroadcaster : Singleton<PrismShieldBroadcaster>
+    {
+        public static PrismShieldBroadcaster EnsureInstance()
+        {
+            if (Instance != null) return Instance;
+
+            var go = new GameObject("[PrismShieldBroadcaster]");
+            go.AddComponent<PrismShieldBroadcaster>();
+            return Instance;
+        }
+
+        [Header("Coalescing")]
+        [Tooltip("Shield state changes for the same domain within this world-space radius in the same frame merge into a single shockwave event.")]
+        [SerializeField] private float coalesceRadius = 6f;
+
+        [Header("Activation Shockwave")]
+        [SerializeField] private float activateStartRadius = 0.5f;
+        [SerializeField] private float activateEndRadius = 9f;
+        [SerializeField] private float activateDuration = 0.35f;
+        [SerializeField] private float activateStartWidth = 0.9f;
+        [SerializeField] private float activateEndWidth = 0.12f;
+
+        [Header("Deactivation Shockwave")]
+        [SerializeField] private float deactivateStartRadius = 5f;
+        [SerializeField] private float deactivateEndRadius = 0.2f;
+        [SerializeField] private float deactivateDuration = 0.22f;
+        [SerializeField] private float deactivateStartWidth = 0.15f;
+        [SerializeField] private float deactivateEndWidth = 0.6f;
+
+        [Header("Visual")]
+        [SerializeField] private int poolSize = 32;
+        [SerializeField] private int segmentCount = 48;
+        [Tooltip("Extra emissive bump on top of the domain color. Higher values = hotter ring.")]
+        [SerializeField] private float colorIntensity = 1.4f;
+
+        [Header("SFX")]
+        [Tooltip("When true, PrismStateManager skips its per-prism shield SFX — the broadcaster plays one SFX per coalesced event instead.")]
+        public bool OwnsShieldSfx = true;
+
+        private struct PendingEvent
+        {
+            public Vector3 OriginWS;
+            public Domains Domain;
+            public bool IsSuper;
+        }
+
+        private struct ActiveRing
+        {
+            public Transform Root;
+            public LineRenderer Line;
+            public float StartTime;
+            public float Duration;
+            public float StartRadius;
+            public float EndRadius;
+            public float StartWidth;
+            public float EndWidth;
+            public Color StartColor;
+            public Color EndColor;
+            public bool InUse;
+        }
+
+        private readonly List<PendingEvent> _pendingActivations = new(32);
+        private readonly List<PendingEvent> _pendingDeactivations = new(32);
+        private bool[] _coalesceConsumed = new bool[64];
+
+        private ActiveRing[] _pool;
+        private Material _sharedLineMaterial;
+        private Transform _poolRoot;
+        private Camera _cachedCamera;
```

</details>
