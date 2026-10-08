# Branch archive: `claude/add-prism-activation-queue-CEoJM`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-06 by Claude
- **Unmerged commits:** 1
- **Forked from:** `9ab277e60` (2026-03-07, Update Gamecanvas to include new connecting panel changes)
- **Tip:** `6718e62a0`
- **Files touched (2):**
  - `Assets/_Scripts/Game/Managers/PrismActivationQueue.cs`
  - `Assets/_Scripts/Game/Ship/Prism.cs`

### `6718e62a0` — Add PrismActivationQueue to eliminate thundering-herd coroutine stalls

_Claude, 2026-03-06 21:42:01 +0000_

```text
Replaces per-prism StartCoroutine(CreateBlockCoroutine) with a centralized
PrismActivationQueue singleton that processes up to 200 prisms per frame
whose delay has elapsed. This spreads activation cost across frames instead
of thousands of WaitForSeconds(0.6) coroutines all resuming on the same
frame causing a multi-second stall.

Key changes:
- New PrismActivationQueue.cs: Singleton queue with swap-remove for O(1)
  cancellation when prisms return to pool
- Prism.Initialize() now calls PrismActivationQueue.Enqueue() instead of
  StartCoroutine(CreateBlockCoroutine())
- Prism.ResetState() cancels pending activations via the queue
- CreateBlockCoroutine replaced by ExecuteDeferredActivation (internal,
  called by the queue)

Cherry-picked and adapted from claude/audit-flora-pooling-f5vmD.
```

```text
 Assets/_Scripts/Game/Managers/PrismActivationQueue.cs | 115 ++++++++++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Game/Ship/Prism.cs                    |  35 +++++++--------
 2 files changed, 130 insertions(+), 20 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 204 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Managers/PrismActivationQueue.cs b/Assets/_Scripts/Game/Managers/PrismActivationQueue.cs
new file mode 100644
index 000000000..b6a2cc1f2
--- /dev/null
+++ b/Assets/_Scripts/Game/Managers/PrismActivationQueue.cs
@@ -0,0 +1,115 @@
+using System.Collections.Generic;
+using CosmicShore.Utilities;
+using UnityEngine;
+
+namespace CosmicShore.Core
+{
+    /// <summary>
+    /// Replaces per-prism CreateBlockCoroutine with a centralized queue that
+    /// activates a bounded number of prisms per frame. This eliminates the
+    /// thundering-herd problem where 50K WaitForSeconds(0.6) coroutines all
+    /// resume on the same frame, causing a multi-second stall.
+    ///
+    /// Each prism queues itself via <see cref="Enqueue"/> with a target activation
+    /// time. Each Update, the queue processes up to <see cref="maxActivationsPerFrame"/>
+    /// prisms whose delay has elapsed, spreading the cost across frames.
+    /// </summary>
+    public class PrismActivationQueue : Singleton<PrismActivationQueue>
+    {
+        [Header("Throughput")]
+        [Tooltip("Max prisms to activate per frame. Higher = faster but more frame cost.")]
+        [SerializeField] private int maxActivationsPerFrame = 200;
+
+        private struct PendingActivation
+        {
+            public Prism Prism;
+            public Vector3 AuthoredTargetScale;
+            public float ActivateAtTime;
+        }
+
+        private readonly List<PendingActivation> _queue = new(256);
+
+        /// <summary>
+        /// Queue a prism for deferred activation. Replaces StartCoroutine(CreateBlockCoroutine).
+        /// </summary>
+        public void Enqueue(Prism prism, Vector3 authoredTargetScale, float delay)
+        {
+            if (prism == null) return;
+
+            _queue.Add(new PendingActivation
+            {
+                Prism = prism,
+                AuthoredTargetScale = authoredTargetScale,
+                ActivateAtTime = Time.time + delay
+            });
+        }
+
+        /// <summary>
+        /// Remove all pending activations for a specific prism (e.g. when returned to pool).
+        /// Uses swap-remove for O(1) per removal.
+        /// </summary>
+        public void Cancel(Prism prism)
+        {
+            for (int i = _queue.Count - 1; i >= 0; i--)
+            {
+                if (_queue[i].Prism == prism)
+                {
+                    int last = _queue.Count - 1;
+                    if (i != last) _queue[i] = _queue[last];
+                    _queue.RemoveAt(last);
+                }
+            }
+        }
+
+        private void Update()
+        {
+            if (_queue.Count == 0) return;
+
+            float now = Time.time;
+            int activated = 0;
+
+            for (int i = _queue.Count - 1; i >= 0 && activated < maxActivationsPerFrame; i--)
+            {
+                var entry = _queue[i];
+
+                if (entry.ActivateAtTime > now)
+                    continue;
+
+                // Remove from queue (swap-remove)
+                int last = _queue.Count - 1;
+                if (i != last) _queue[i] = _queue[last];
+                _queue.RemoveAt(last);
+
+                // Skip destroyed/disabled prisms
+                if (entry.Prism == null || !entry.Prism.gameObject.activeInHierarchy)
+                    continue;
+
+                entry.Prism.ExecuteDeferredActivation(entry.AuthoredTargetScale);
+                activated++;
+            }
+        }
+
+        /// <summary>
+        /// Ensures a PrismActivationQueue instance exists.
+        /// </summary>
+        public static PrismActivationQueue EnsureInstance()
+        {
+            if (Instance != null) return Instance;
+
+            var go = new GameObject("[PrismActivationQueue]");
+            go.AddComponent<PrismActivationQueue>();
+            return Instance;
+        }
+
+        private void OnDisable()
+        {
+            _queue.Clear();
+        }
+
+        protected override void OnDestroy()
+        {
+            _queue.Clear();
+            base.OnDestroy();
+        }
+    }
+}
diff --git a/Assets/_Scripts/Game/Ship/Prism.cs b/Assets/_Scripts/Game/Ship/Prism.cs
index c377b1a4a..b092e1ed4 100644
--- a/Assets/_Scripts/Game/Ship/Prism.cs
+++ b/Assets/_Scripts/Game/Ship/Prism.cs
@@ -1,6 +1,5 @@
 ﻿using System;
 using UnityEngine;
-using System.Collections;
 using CosmicShore.App.Systems.Audio;
 using CosmicShore.Utility.ClassExtensions;
 using CosmicShore.Game;
@@ -136,7 +135,11 @@ namespace CosmicShore.Core
 
             scaleAnimator.Initialize();
             scaleAnimator.SetTargetScale(authoredTargetScale);
-            StartCoroutine(CreateBlockCoroutine(authoredTargetScale));
+
+            // Queue activation through centralized manager instead of per-prism coroutine.
+            // This prevents the thundering-herd problem where thousands of WaitForSeconds
+            // timers expire on the same frame.
+            PrismActivationQueue.EnsureInstance().Enqueue(this, authoredTargetScale, waitTime);
 
             if (prismProperties.IsShielded) ActivateShield();
             if (prismProperties.IsDangerous) MakeDangerous();
@@ -144,6 +147,10 @@ namespace CosmicShore.Core
 
         private void ResetState()
         {
+            // Cancel any pending deferred activation from PrismActivationQueue
```

</details>
