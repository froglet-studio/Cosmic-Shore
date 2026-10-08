# Branch archive: `claude/dolphin-explosion-prism-coverage-qtbstp`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-07-31 by Claude
- **Unmerged commits:** 4
- **Forked from:** `e443d11be` (2026-07-31, Merge pull request #638 from froglet-studio/claude/freestyle-cell-selector-toy)
- **Tip:** `593d734b2`
- **Files touched (8):**
  - `Assets/_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs`
  - `Assets/_Scripts/Controller/Managers/PrismEffectsManager.cs`
  - `Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs`
  - `Assets/_Scripts/Controller/Prisms/PrismFactory.cs`
  - `Assets/_Scripts/Controller/Projectiles/AOEConicExplosion.cs`
  - `Assets/_Scripts/Controller/Projectiles/AOEExplosion.cs`
  - `Assets/_Scripts/Utility/Effects/PrismExplosion.cs`
  - `Docs/SPATIAL_INDEX.md`

### `6090f42e8` — fix(projectiles): the Dolphin cone now damages everything inside it

_Claude, 2026-07-31 05:00:58 +0000_

```text
Prisms sitting plainly inside the conic explosion survived the blast. Two
independent causes, both in the batch AOE path.

PRIMARY - the per-frame damage budget silently dropped hits.
ProcessExplosionFrame caps damage at MAX_NEW_HITS_PER_FRAME (48) and skipped
over-cap prisms WITHOUT claiming them, on the comment "the Burst job will
re-find these prisms next frame". That holds only while the query volume is
NESTED frame to frame. It is true for the spherical explosion (stationary
centre, growing radius) and false for the conic one, whose volume TRANSLATES:
nesting would need MaxScale >= 2*height (4800), and the Dolphin's authored
range is 400..1600, so the volume advanced past every deferred prism and never
came back. The blast was hard-capped at 48 x frames = 7,824 prisms at 60fps
(3,888 at 30fps); everything past that was undamaged by construction - 39% of
a 20k-prism cone survived at 60fps, 81% at 30fps.

Deferral is now lossless: an over-budget hit is claimed into alreadyHit AND
pushed onto the explosion's backlog, drained FIFO each later frame and past the
end of the visual. Damage is bounded by what the blast contained, never by how
long the VFX ran. Backlog entries carry the Prism itself and are dropped if the
free list recycled their slot - the one sanctioned holder of registry indices
across frames.

SECONDARY - the query volume was not the cone. The conic path derived one ball
per frame riding the cone's leading base plane; that family is tangent to the
cone (envelope asin(k) beats atan(k) by 0.37% at min charge), leaving a
scalloped mantle shell and a never-sampled plug at the muzzle, while
over-reaching a hemisphere PAST the visible tip - which let a super-shielded
prism outside the cone abort the whole blast. Replaced with
AOEConicSweepQueryJob: an exact cone test over the axial slab the cone newly
covers each frame. Successive slabs tile the swept cone exactly, so coverage is
frame-rate independent and the damage volume is now precisely the rendered one.

Also in this pass:
- The budget is charged only for real work (Prism.Damage, ActivateShield);
  dead slots and super-shield blocks resolve free, so friendly mass sharing a
  blast no longer starves enemy mass out of the budget.
- One shared per-hit decision for the fresh-query loop and the drain, so a
  deferred hit is re-judged against the prism's state at drain time (a prism
  that gained a super-shield while queued is no longer blindly damaged).
- Both explosions retire from the world before draining (mesh hidden, trigger
  disabled) instead of parking an invisible full-size vessel hitbox, and a
  cancellation after the visual tears the object down instead of stranding it.
- Clamp the eased t to 1 - elapsed overshoots the duration on the last frame
  and sin() past 90 degrees decreases, which shrank the final cone and left a
  tip shell no slab reached.
- Degenerate-query early-outs no longer stall the backlog; dead-entry drains
  are iteration-capped.

BEHAVIOUR CHANGE worth knowing: damage no longer reaches past the visible tip.
Effective reach goes 2600 -> 2400 at min charge and 3200 -> 2400 at max charge,
where the old over-reach hemisphere was 67% of the cone's own volume. Raise the
prefab's height if the old reach is wanted.
```

```text
 .../_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs  |  82 +++++-
 Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs              | 501 +++++++++++++++++++++++++++-----
 Assets/_Scripts/Controller/Projectiles/AOEConicExplosion.cs           |  99 +++++--
 Assets/_Scripts/Controller/Projectiles/AOEExplosion.cs                |  37 ++-
 Docs/SPATIAL_INDEX.md                                                 |  65 ++++-
 5 files changed, 680 insertions(+), 104 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 1060 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs
index d206300c7..7636e99a4 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs
@@ -24,8 +24,16 @@ namespace CosmicShore.Gameplay
         private static int _trailBlockLayer = -1;
         private HashSet<int> _batchHitTracker;
 
+        // Damage deferred by the per-frame budget. Entries here are already claimed
+        // in _batchHitTracker, so the spatial query will never re-emit them - this
+        // queue is their only resolution path. See MAX_NEW_HITS_PER_FRAME.
+        private Queue<PendingExplosionHit> _batchPending;
+
         public bool IsBatchProcessing => _useBatchProcessing;
 
+        /// <summary>True while budget-deferred damage is still waiting to resolve.</summary>
+        public bool HasPendingBatchWork => _batchPending != null && _batchPending.Count > 0;
+
         /// <summary>
         /// When true, BeginBatchProcessing() is a no-op - forces Physics OnTriggerEnter
         /// for all collisions. Used by AOEBenchmarkOverlay for A/B comparison.
@@ -67,14 +75,20 @@ namespace CosmicShore.Gameplay
                 _batchHitTracker = new HashSet<int>(256);
             else
                 _batchHitTracker.Clear();
+
+            if (_batchPending == null)
+                _batchPending = new Queue<PendingExplosionHit>(256);
+            else
+                _batchPending.Clear();
         }
 
         /// <summary>
         /// Processes one frame of batch AOE damage via the PrismSpatialIndex.
         /// Called from AOEExplosion.ExplodeAsync each frame instead of relying on Physics.
-        /// center/radius describe this frame's blast wavefront (a conic explosion
-        /// passes a different, forward-traveling sphere each frame); blastOrigin is
-        /// the fixed emission point all impact vectors radiate from.
+        /// center/radius describe this frame's blast sphere (stationary centre,
+        /// growing radius - so each frame's volume strictly contains the last);
+        /// blastOrigin is the emission point all impact vectors radiate from.
+        /// The conic explosion uses <see cref="ProcessBatchConeFrame"/> instead.
         /// Returns true if the explosion should continue, false if it should be destroyed
         /// (e.g. hit a super-shielded enemy prism).
         /// </summary>
@@ -92,7 +106,61 @@ namespace CosmicShore.Gameplay
                     affectSelf, destructive, devastating, shielding,
                     explosion.AnonymousExplosion,
                     explosion.Vessel,
-                    _batchHitTracker);
+                    _batchHitTracker,
+                    _batchPending);
+            }
+        }
+
+        /// <summary>
+        /// Processes one frame of batch AOE damage for the CONIC explosion: an exact
+        /// test against the rendered cone over the axial slab [sliceMin, sliceMax]
+        /// it newly covers this frame. Successive slabs tile the swept cone exactly,
+        /// so coverage does not depend on frame rate and never reaches past the
+        /// visible tip. tanHalfAngle is baseRadius/height - invariant as the
+        /// self-similar cone grows.
+        /// Returns true if the explosion should continue, false if it should be
+        /// destroyed (e.g. hit a super-shielded enemy prism).
+        /// </summary>
+        public bool ProcessBatchConeFrame(
+            Vector3 apex, Vector3 axis, float sliceMin, float sliceMax,
+            float tanHalfAngle, float speed, float inertia)
+        {
+            using (s_processBatch.Auto())
+            {
+                if (!_useBatchProcessing) return true;
+                var registry = PrismSpatialIndex.Instance;
+                if (registry == null) return true;
+
+                return registry.ProcessExplosionConeFrame(
+                    apex, axis, sliceMin, sliceMax, tanHalfAngle, speed, inertia,
+                    explosion.Domain,
+                    affectSelf, destructive, devastating, shielding,
+                    explosion.AnonymousExplosion,
+                    explosion.Vessel,
+                    _batchHitTracker,
+                    _batchPending);
+            }
+        }
+
+        /// <summary>
+        /// Drains one frame's worth of budget-deferred damage without running a new
+        /// spatial query. Called after the explosion's visual finishes so a blast
+        /// dense enough to exceed the per-frame budget still damages everything it
+        /// enclosed. Returns true while work remains.
+        /// </summary>
+        public bool DrainPendingBatchFrame(float speed, float inertia)
+        {
+            using (s_processBatch.Auto())
+            {
+                if (!HasPendingBatchWork) return false;
+                var registry = PrismSpatialIndex.Instance;
+                if (registry == null) { _batchPending.Clear(); return false; }
+
+                return registry.DrainPendingExplosionDamage(
+                    _batchPending, speed, inertia,
+                    explosion.Domain,
+                    affectSelf, destructive, devastating, shielding,
+                    explosion.AnonymousExplosion, explosion.Vessel);
             }
         }
 
@@ -102,7 +170,11 @@ namespace CosmicShore.Gameplay
         public void EndBatchProcessing()
         {
             _useBatchProcessing = false;
-            // Keep HashSet allocated for reuse - just clear on next BeginBatchProcessing
+            // Keep HashSet/Queue allocated for reuse - cleared on next BeginBatchProcessing.
+            // Any hits still pending here are abandoned deliberately: EndBatchProcessing
+            // runs on cancellation (turn end) and on the destroy paths, where further
+            // damage must not land.
+            _batchPending?.Clear();
         }
 
         protected override void OnTriggerEnter(Collider other)
diff --git a/Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs b/Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs
index 0da0ecc16..cb1878ef1 100644
--- a/Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs
+++ b/Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs
@@ -260,11 +260,13 @@ namespace CosmicShore.Gameplay
     /// With 4 entries per cache line, a sequential scan of 3000 prisms
     /// touches only 750 cache lines (48KB).
     ///
-    /// The query sphere (Center/RadiusSq) is the traveling blast WAVEFRONT -
-    /// for a conic explosion it rides the growing cone's leading base plane,
-    /// a different sphere each frame. BlastOrigin is the fixed emission point
-    /// (cone apex / spherical-explosion center): every hit's impact direction
-    /// radiates from it, so struck prisms all fly outward with the blast wave.
+    /// The query sphere (Center/RadiusSq) belongs to the SPHERICAL explosion: a
+    /// stationary Center with a growing radius, so each frame's volume strictly
+    /// contains the previous frame's. BlastOrigin is the emission point every hit's
+    /// impact direction radiates from, so struck prisms fly outward with the blast.
+    ///
+    /// The conic explosion does NOT use this job - its volume translates rather than
+    /// grows, so it queries an exact cone slab via <see cref="AOEConicSweepQueryJob"/>.
     /// </summary>
     [BurstCompile]
     public struct AOESpatialQueryJob : IJobParallelFor
@@ -297,6 +299,80 @@ namespace CosmicShore.Gameplay
         }
     }
 
+    /// <summary>
+    /// One explosion hit deferred by the per-frame budget, waiting in the
```

</details>

### `060d160c6` — fix(projectiles): guard deferred AOE hits by slot generation, not object identity

_Claude, 2026-07-31 05:05:01 +0000_

```text
Follow-up to the conic-explosion coverage fix. The backlog's identity guard was
inverted in the exact case it existed for, and could not see pool rebirth.

1. `expected != null` used Unity's overloaded operator. `Prism` is a
   MonoBehaviour, so a deferred prism whose GameObject was destroyed while
   queued compares fake-null - the guard short-circuited to FALSE and was
   skipped entirely, applying the hit to whatever now occupied the slot. The
   check disabled itself precisely when a recycle had happened.

2. Object identity cannot distinguish "same prism" from "same prism, next
   life". Prisms are pooled: a deferred prism can die, return to the pool, be
   re-Initialized and - the free list being LIFO - land back in the same slot
   with `destroyed` reset to false. A reference comparison passes and the
   reborn prism eats the old blast's damage.

Replaced with a per-slot occupancy stamp: `_slotGeneration[index]`, incremented
by every `Register`. `PendingExplosionHit` captures it at defer time and the
drain drops any entry whose stamp no longer matches, which covers both the
recycle-to-another-prism and the same-instance-rebirth cases. The check now runs
BEFORE `_prisms[idx]` is read, so a stale entry never touches the new occupant.
`PendingExplosionHit` loses its managed field as a result.
```

```text
 Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs | 62 +++++++++++++++++++++++++++++++++-------------
 Docs/SPATIAL_INDEX.md                                    | 12 ++++++---
 2 files changed, 53 insertions(+), 21 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 181 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs b/Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs
index cb1878ef1..ad18a0c53 100644
--- a/Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs
+++ b/Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs
@@ -301,15 +301,18 @@ namespace CosmicShore.Gameplay
 
     /// <summary>
     /// One explosion hit deferred by the per-frame budget, waiting in the
-    /// explosion's backlog. Managed (never enters a Burst job) precisely so it can
-    /// carry <see cref="Prism"/> as an identity guard: registry slots are recycled
-    /// through the free list, and a deferred hit may wait several frames, so the raw
-    /// index alone can silently alias onto a DIFFERENT prism by drain time.
+    /// explosion's backlog. Carries the slot's occupancy GENERATION as an identity
+    /// guard: registry slots are recycled through the free list and a deferred hit
+    /// may wait many frames, so the raw index alone can silently alias onto a
+    /// different prism — or onto the same pooled instance living a new life — by the
+    /// time it drains. A generation stamp catches both; an object reference catches
+    /// only the first (and a Unity-destroyed reference compares fake-null, which
+    /// would disable the check in exactly the case it exists for).
     /// </summary>
     public struct PendingExplosionHit
     {
-        public Prism Prism;      // identity captured at defer time
         public int Index;
+        public int Generation;   // _slotGeneration[Index] captured at defer time
         public float3 ImpactDir;
     }
 
@@ -628,6 +631,12 @@ namespace CosmicShore.Gameplay
         /// </summary>
         private const int MAX_DRAIN_EXAMINED_PER_FRAME = MAX_NEW_HITS_PER_FRAME * 8;
 
+        /// <summary>
+        /// Sentinel for "no generation check" - used by same-frame hits, which have no
+        /// aliasing window. Never produced by Register (it pre-increments from 0).
+        /// </summary>
+        private const int AnyGeneration = 0;
+
         // Hot: scanned by Burst job every frame during AOE
         private NativeArray<PrismSpatialData> _spatial;
 
@@ -660,6 +669,13 @@ namespace CosmicShore.Gameplay
         // Managed: Prism references for applying damage callbacks
         private Prism[] _prisms;
 
+        // Per-slot occupancy stamp, incremented on every Register. A slot index is
+        // only a valid handle while its generation is unchanged, so anything that
+        // holds an index across frames (the explosion backlog) can detect BOTH a
+        // free-list recycle to a different prism AND a pooled prism re-entering the
+        // same slot for a new life. Object identity alone catches only the first.
+        private int[] _slotGeneration;
+
         // Managed: the cell whose per-domain density grids each prism is filed in
         // (the coarse view of this same lifecycle), or null - open space, fauna
         // bodies, slot free. Bound on Register/MarkRestored, released on
@@ -724,6 +740,7 @@ namespace CosmicShore.Gameplay
             _cellVolumeScratch = new NativeArray<float>(CellVolumeResultCount, Allocator.Persistent);
             _shell = new NativeArray<PrismShellData>(INITIAL_CAPACITY, Allocator.Persistent);
             _prisms = new Prism[INITIAL_CAPACITY];
+            _slotGeneration = new int[INITIAL_CAPACITY];
             _cells = new Cell[INITIAL_CAPACITY];
             _aoeHits = new NativeList<AOEHit>(512, Allocator.Persistent);
             _buckets = new NativeParallelMultiHashMap<int3, int>(INITIAL_CAPACITY, Allocator.Persistent);
@@ -1375,6 +1392,10 @@ namespace CosmicShore.Gameplay
             }
 
             _prisms[index] = prism;
+            unchecked { _slotGeneration[index]++; }
+            // Never let a live slot carry the "no check" sentinel (only reachable
+            // after a full 2^32 wrap on one slot, but the guard is one comparison).
+            if (_slotGeneration[index] == AnyGeneration) _slotGeneration[index] = 1;
 
             // Build flags byte
             byte flags = PrismFlags.IsActive;
@@ -1973,8 +1994,8 @@ namespace CosmicShore.Gameplay
                     alreadyHit.Add(idx);
                     pending.Enqueue(new PendingExplosionHit
                     {
-                        Prism = live,
                         Index = idx,
+                        Generation = _slotGeneration[idx],
                         ImpactDir = _aoeHits[i].ImpactDir
                     });
                     continue;
@@ -1982,7 +2003,7 @@ namespace CosmicShore.Gameplay
 
                 alreadyHit.Add(idx);
 
-                if (ResolveExplosionHit(idx, null, _aoeHits[i].ImpactDir,
+                if (ResolveExplosionHit(idx, AnyGeneration, _aoeHits[i].ImpactDir,
                         speed, inertia, expDomain, affectSelf, destructive, devastating,
                         shielding, anonymous, vesselDomain, vesselPlayerName, ref shouldContinue))
                     budgetSpent++;
@@ -1997,11 +2018,12 @@ namespace CosmicShore.Gameplay
         /// query time (a prism that gained a super-shield or changed domain while
         /// queued must be re-judged, not blindly damaged).
         ///
-        /// <paramref name="expected"/> is the identity guard: registry slots are
-        /// recycled through <c>_freeList</c>, so a hit that sat in the backlog for
-        /// several frames may find a DIFFERENT prism in its slot. Pass the prism
-        /// captured at defer time; a mismatch drops the hit. Pass null for a
-        /// same-frame hit, where no aliasing window exists.
+        /// <paramref name="expectedGeneration"/> is the identity guard: registry
+        /// slots are recycled through <c>_freeList</c>, so a hit that sat in the
+        /// backlog for several frames may find a different prism in its slot — or the
+        /// same pooled instance living a new life. Pass the generation captured at
+        /// defer time; a mismatch drops the hit. Pass <see cref="AnyGeneration"/> for
+        /// a same-frame hit, where no aliasing window exists.
         ///
         /// Returns true if the frame's budget should be charged - i.e. real work was
         /// done. Both outcomes that do work are charged: <see cref="Prism.Damage"/>
@@ -2011,7 +2033,7 @@ namespace CosmicShore.Gameplay
         /// </summary>
         private bool ResolveExplosionHit(
             int idx,
-            Prism expected,
+            int expectedGeneration,
             float3 impactDir,
             float speed,
             float inertia,
@@ -2025,12 +2047,14 @@ namespace CosmicShore.Gameplay
             string vesselPlayerName,
             ref bool shouldContinue)
         {
+            // Slot-recycling guard - see the summary. Checked BEFORE the prism is
+            // touched: a stale entry must not resolve against whatever now owns the slot.
+            if (expectedGeneration != AnyGeneration && _slotGeneration[idx] != expectedGeneration)
+                return false;
+
             var prism = _prisms[idx];
             if (prism == null || prism.destroyed) return false;
 
-            // Slot-recycling guard - see the summary.
-            if (expected != null && !ReferenceEquals(prism, expected)) return false;
-
             // Read cold data - only for hit prisms, never pollutes the Burst job's cache
             var flags = _spatial[idx].Flags;
             int prismDomain = _damage[idx].Domain;
@@ -2107,7 +2131,7 @@ namespace CosmicShore.Gameplay
             {
                 examined++;
                 var deferred = pending.Dequeue();
-                if (ResolveExplosionHit(deferred.Index, deferred.Prism, deferred.ImpactDir,
+                if (ResolveExplosionHit(deferred.Index, deferred.Generation, deferred.ImpactDir,
                         speed, inertia, expDomain, affectSelf, destructive, devastating,
                         shielding, anonymous, vesselDomain, vesselPlayerName, ref shouldContinue))
                     spent++;
@@ -2197,6 +2221,10 @@ namespace CosmicShore.Gameplay
             System.Array.Copy(_prisms, newPrisms, _prisms.Length);
             _prisms = newPrisms;
```

</details>

### `f913f7e42` — fix(prisms): destroyed prisms animate out under load instead of vanishing

_Claude, 2026-07-31 20:04:54 +0000_

```text
Blowing up a few prisms looked right; blowing up many made them disappear.
Prism.SetupDestruction hides the prism immediately (SetRenderVisible(false)) and
the pooled VFX is the only thing that stands in for it, so anything that skips or
truncates that VFX shows as mass popping out of existence - the continuity law
("deaths animate out", CLAUDE.md) broken at exactly the moment the player is
looking. Two independent throttles did that, both bounding COVERAGE where they
meant to bound COST.

1. PrismFactory dropped the visual outright past 64 spawns/frame ("Prism
   destruction still happens, we just skip the visual"). Now it DEFERS: the
   effect is queued and spawned within a later frame's budget, oldest first, so
   the per-frame ceiling the cap exists to enforce is unchanged but a burst
   animates out over the next frame or two instead of vanishing. The queue is
   bounded at a few frames' worth and discards the stalest when full, matching
   PrismEffectsManager's recycle-the-oldest policy; it is cleared on disable so a
   scene reload cannot replay effects where nothing is.

2. PrismEffectsManager's 256-concurrent ceiling recycled the oldest effect, and
   OnEffectComplete returns it to the pool at once - an explosion cut off
   mid-animation. What the ceiling really bounds is the PRODUCT of concurrency
   and duration: at the fixed 5s length it sustained only ~51 deaths/sec, so a
   dense blast (48+/frame, more with concurrent blasts) recycled every explosion
   a few frames in - a flicker, then nothing. Effects now shorten as the buffer
   fills (full length until half full, eased to 0.22s at saturation), raising the
   sustainable rate to ~1160/sec so they COMPLETE rather than pop. Unpressured
   load is untouched.

Because expansion and drift are speed*elapsed, a pressured effect is a smaller,
quicker puff rather than the same bloom fast-forwarded - deliberate, since
scaling speed to compensate would fling debris at up to 22x. Tune via
MIN_PRESSURED_DURATION.

Note this became visible partly because the conic-explosion fix earlier on this
branch made blasts actually destroy everything they contain: prisms that used to
silently survive now die, which pushes far more frames past both throttles.
```

```text
 Assets/_Scripts/Controller/Managers/PrismEffectsManager.cs |  43 ++++++++++++++-
 Assets/_Scripts/Controller/Prisms/PrismFactory.cs          | 121 +++++++++++++++++++++++++++++++++++++------
 Assets/_Scripts/Utility/Effects/PrismExplosion.cs          |  12 ++++-
 3 files changed, 158 insertions(+), 18 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 260 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Managers/PrismEffectsManager.cs b/Assets/_Scripts/Controller/Managers/PrismEffectsManager.cs
index 08a9c873a..15581c2a2 100644
--- a/Assets/_Scripts/Controller/Managers/PrismEffectsManager.cs
+++ b/Assets/_Scripts/Controller/Managers/PrismEffectsManager.cs
@@ -53,6 +53,12 @@ namespace CosmicShore.Gameplay
         // See PRISM_PERFORMANCE_AUDIT.md rec 5.
         private const int MAX_ACTIVE_EFFECTS = 256;
 
+        // Shortest an explosion is allowed to be squeezed to under full pressure. Still
+        // long enough to read as a death (~13 frames at 60fps) while raising the
+        // sustainable rate from ~51/s at the 5s default to ~1170/s here - the headroom a
+        // dense blast needs for every prism to animate out rather than pop.
+        private const float MIN_PRESSURED_DURATION = 0.22f;
+
         // Explosion tracking
         private readonly List<PrismExplosion> activeExplosions = new(INITIAL_CAPACITY);
         private readonly List<PrismExplosion> tempExplosionList = new(INITIAL_CAPACITY);
@@ -100,8 +106,26 @@ namespace CosmicShore.Gameplay
         public void RegisterExplosion(PrismExplosion explosion)
         {
             if (explosion == null || activeExplosions.Contains(explosion)) return;
-            // Bound concurrent active VFX - recycle the oldest (front of the list,
-            // longest-running) to make room so the per-frame apply stays O(cap).
+
+            // What the ceiling really bounds is the PRODUCT of concurrency and duration.
+            // At the unpressured 5s length a 256-effect ceiling sustains only ~51 deaths
+            // per SECOND; a big blast produces that many per frame, so every explosion
+            // used to be recycled a few frames in - a flicker, then nothing, which is
+            // the "blow up many and they just disappear" report. Shortening new effects
+            // as the buffer fills lets them run to completion instead. Unpressured load
+            // is untouched (pressure 0 -> DefaultDuration).
+            //
+            // Note what a shorter duration actually looks like: expansion and debris
+            // drift are speed*elapsed / velocity*elapsed, so a pressured effect is a
+            // SMALLER, quicker puff rather than the same bloom fast-forwarded. That is
+            // deliberate - scaling speed to compensate would fling shrapnel at up to 22x
+            // - and hundreds of small pops read better in a frenzy than hundreds of
+            // overlapping 5s blooms. Tune via MIN_PRESSURED_DURATION.
+            explosion.MaxDuration = PressuredDuration(activeExplosions.Count);
+
+            // Backstop only - with the duration scaling above this should now be rare.
+            // Recycle the oldest (front of the list, longest-running, hence nearest done)
+            // to make room so the per-frame apply stays O(cap).
             while (activeExplosions.Count >= MAX_ACTIVE_EFFECTS)
             {
                 var oldest = activeExplosions[0];
@@ -112,6 +136,21 @@ namespace CosmicShore.Gameplay
             EnsureExplosionCapacity();
         }
 
+        /// <summary>
+        /// Animation length for an effect registering while <paramref name="activeCount"/>
+        /// are already running. Full length until the buffer is half full, then eased down
+        /// to <see cref="MIN_PRESSURED_DURATION"/> as it saturates - so throughput rises
+        /// with load exactly when it has to, and deaths animate out instead of popping.
+        /// </summary>
+        private static float PressuredDuration(int activeCount)
+        {
+            const float pressureFloor = MAX_ACTIVE_EFFECTS * 0.5f;
+            if (activeCount <= pressureFloor) return PrismExplosion.DefaultDuration;
+
+            float pressure = Mathf.InverseLerp(pressureFloor, MAX_ACTIVE_EFFECTS, activeCount);
+            return Mathf.Lerp(PrismExplosion.DefaultDuration, MIN_PRESSURED_DURATION, pressure);
+        }
+
         public void UnregisterExplosion(PrismExplosion explosion)
         {
             activeExplosions.Remove(explosion);
diff --git a/Assets/_Scripts/Controller/Prisms/PrismFactory.cs b/Assets/_Scripts/Controller/Prisms/PrismFactory.cs
index de82cd8c5..f6453608f 100644
--- a/Assets/_Scripts/Controller/Prisms/PrismFactory.cs
+++ b/Assets/_Scripts/Controller/Prisms/PrismFactory.cs
@@ -1,4 +1,5 @@
 using System;
+using System.Collections.Generic;
 using UnityEngine;
 using CosmicShore.Gameplay;
 using CosmicShore.ScriptableObjects;
@@ -37,6 +38,24 @@ namespace CosmicShore.Gameplay
         private int _lastExplosionFrame;
         private int _lastImplosionFrame;
 
+        // A destruction whose visual does not fit in this frame's cap is DEFERRED, not
+        // dropped. Prism.SetupDestruction has already hidden the prism, so the pooled
+        // VFX is the only thing standing in for it - skipping the spawn makes the prism
+        // vanish mid-air, which the continuity law forbids ("deaths animate out"; see
+        // CLAUDE.md). Deferred effects are spawned within a later frame's cap, so the
+        // per-frame ceiling this budget exists to enforce is unchanged.
+        //
+        // The queue is bounded at a few frames' worth: past that the visual would play
+        // so long after the death that it reads as a random pop somewhere the fight has
+        // already left. When full we discard the STALEST, matching PrismEffectsManager's
+        // recycle-the-oldest policy for the concurrent-effect ceiling.
+        private const int MaxDeferredVFX = 192;
+        private readonly Queue<PrismEventData> _deferredExplosions = new(64);
+        private readonly Queue<PrismEventData> _deferredImplosions = new(64);
+
+        /// <summary>Destruction visuals waiting on a later frame's spawn budget.</summary>
+        public int DeferredExplosionCount => _deferredExplosions.Count;
+
         [Header("Pool Managers")]
         [SerializeField] private InteractivePrismPoolManager dolphinPrismPool;
         [SerializeField] private InteractivePrismPoolManager serpentPrismPool;
@@ -84,6 +103,11 @@ namespace CosmicShore.Gameplay
         {
             if (_onPrismSpawnedEventChannel)
                 _onPrismSpawnedEventChannel.OnEventReturn -= OnPrismSpawnedEventRaised;
+
+            // Queued visuals belong to deaths in the scene being torn down - replaying
+            // them after a reload would pop effects at coordinates nothing occupies.
+            _deferredExplosions.Clear();
+            _deferredImplosions.Clear();
         }
         #endregion
 
@@ -231,15 +255,54 @@ namespace CosmicShore.Gameplay
         
         GameObject SpawnExplosion(PrismEventData data)
         {
-            // Cap explosion VFX per frame to prevent pool exhaustion.
-            // Prism destruction still happens (Damage already applied), we just skip the visual.
-            if (Time.frameCount != _lastExplosionFrame)
+            RollExplosionFrameBudget();
+
+            // Over budget: queue the visual for a later frame rather than dropping it.
+            // The prism is already hidden, so a dropped visual IS a disappearing prism.
+            if (_explosionVFXCount >= MaxExplosionVFXPerFrame)
             {
-                _lastExplosionFrame = Time.frameCount;
-                _explosionVFXCount = 0;
+                Defer(_deferredExplosions, data);
+                return null;
             }
-            if (_explosionVFXCount >= MaxExplosionVFXPerFrame)
+
+            return SpawnExplosionNow(data);
+        }
+
+        GameObject SpawnImplosion(PrismEventData data)
+        {
+            RollImplosionFrameBudget();
+
+            // Same reasoning as explosions - defer, never drop.
+            if (_implosionVFXCount >= MaxImplosionVFXPerFrame)
+            {
+                Defer(_deferredImplosions, data);
                 return null;
+            }
+
+            return SpawnImplosionNow(data);
```

</details>

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
