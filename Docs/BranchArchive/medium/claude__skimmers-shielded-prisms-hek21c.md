# Branch archive: `claude/skimmers-shielded-prisms-hek21c`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-07-24 by Claude
- **Unmerged commits:** 9
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/618
- **Forked from:** `6827d8a81` (2026-07-22, Merge pull request #617 from froglet-studio/claude/skimmers-shielded-prisms-he)
- **Tip:** `0b7978139`
- **Files touched (14):**
  - `Assets/_Prefabs/Spacevessels/Squirrel.prefab`
  - `Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/Impactors/PrismImpactor.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/Impactors/VesselImpactor.cs`
  - `Assets/_Scripts/Controller/Vessel/IShieldContainmentGate.cs`
  - `Assets/_Scripts/Controller/Vessel/IShieldContainmentGate.cs.meta`
  - `Assets/_Scripts/Controller/Vessel/Prism.cs`
  - `Assets/_Scripts/Controller/Vessel/PrismOctahedronShield.cs`
  - `Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs`
  - `Assets/_Scripts/Utility/OctahedronMeshGenerator.cs`
  - `Assets/_Scripts/Utility/StellatedOctahedronMeshGenerator.cs`
  - `CLAUDE.md`
  - `Docs/CollisionLOD/DESIGN.md`

### `0875efac6` — docs(collision): three-LOD prism collision plan + shield narrowphase spec

_Claude, 2026-07-22 07:21:16 +0000_

```text
Robust build spec for shape-precise shielded-prism collision: keep the primitive
box trigger but resize it to the shell AABB (no swap), add a signed-margin
narrowphase (octahedron L1 / stella 4-form, already on-branch as ContainsPointLocal)
at the ImpactorBase dispatch chokepoint, with a per-interaction threshold (skim =
proximity band, pop = containment) and pending re-test. Fixes the two failure modes
of the parallel tetrahedral branch (grazing skims rejected; pop-then-destroy from a
collider swap) by construction. Point/sphere tiers are already realized (LOD cull +
PrismSpatialIndex); this front-loads the bespoke-bounds tier. Includes exact
contracts and the in-editor verification steps.
```

```text
 Docs/CollisionLOD/DESIGN.md | 155 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 155 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 161 lines)</summary>

```diff
diff --git a/Docs/CollisionLOD/DESIGN.md b/Docs/CollisionLOD/DESIGN.md
new file mode 100644
index 000000000..06891545b
--- /dev/null
+++ b/Docs/CollisionLOD/DESIGN.md
@@ -0,0 +1,155 @@
+# Prism Collision LOD — Design & Build Plan
+
+**Status:** committed plan, Phase 0–1 building. **Base:** bleeding-edge (post PR #617).
+**Owner branch:** `claude/skimmers-shielded-prisms-hek21c`.
+
+## 0. Why
+
+PR #617 fixed shielded-prism skimming by keeping the authored **primitive box trigger** as
+the collider (no convex `MeshCollider`, no swap). That works — every skimmer family and the
+Rhino swipe see the prism again — but the interaction happens at the **bare prism size**, so
+the 3× visible shield shell is cosmetic: you skim/pop the small prism, not the octahedron you
+see. This plan restores **shape-precise interaction at the visible shell**, cheaply, without
+reintroducing the two bugs that sank the parallel `tetrahedral-collider-cost` branch.
+
+It also formalizes the collision-LOD story the codebase already half-implements.
+
+## 1. The three tiers (and what already exists)
+
+The player-facing idea (from the prompter): bucket prism collision into three LODs.
+
+| Tier | Shape | Serves | Status on bleeding-edge |
+|---|---|---|---|
+| **Point / box** | authored primitive box **trigger** | bulk direct contact (the 95% case) | ✅ exists; `PrismColliderLodManager` already culls the box far from every focus, so active-collider count is bounded by radius, not population |
+| **Sphere / volumetric** | sphere via the Burst index | explosions / AOE / large-scale | ✅ exists; `ExplosionImpactor` is the *only* impactor that routes through `PrismSpatialIndex` |
+| **Bespoke analytic bounds** | octahedron (shielded) / stella (super-shielded) | precision, player-noticed objects | ❌ **this plan** |
+
+**Refinement finding:** tiers 1 and 2 are already realized. The genuinely new, high-value tier
+is the analytic bounds — and it is exactly what completes the shield work. So we front-load it;
+point/sphere are *measure-first* formalization (Phase 2), not new machinery.
+
+## 2. Mechanism (validated against the code)
+
+**Broadphase** — one stable primitive collider, never a mesh, never swapped.
+- Unshielded: authored box trigger at authored size (unchanged).
+- Shielded/super-shielded: the **same** box trigger, **resized** to the shell AABB
+  (`size × shieldScale`, `shieldScale = CIRCUMSCRIBING_SCALE = 3`) so the broadphase
+  over-covers the visible shell. Resize on engage, resize back on disengage. **Never
+  disable/enable or swap the collider** — resizing an already-overlapping box can only fire
+  `OnTriggerExit` (shrink) or a fresh `OnTriggerEnter` for *newly*-covered colliders, never a
+  second Enter for a contact that was already overlapping. That is what structurally prevents
+  the pop-then-destroy bug (which was caused by *enabling a previously-disabled* collider under
+  a continuous overlap).
+- Rides `PrismColliderLodManager` unchanged (it culls/restores `boxCollider` by proximity).
+
+**Narrowphase** — a signed-margin refinement at the dispatch chokepoint, active only when the
+prism has an engaged shield.
+- `IShieldContainmentGate.SignedMargin(worldPoint)`: **> 0 inside, 0 on the surface, < 0
+  outside**, in the shell's normalized metric (magnitude ∝ distance-to-surface).
+- Octahedron: `margin = 1 − (|x|·invA + |y|·invB + |z|·invC)` (the L1 test already on-branch,
+  returning the slack instead of the bool).
+- Stella (union of two tetrahedra): `margin = max(minF + 1, 1 − maxF)` over the 4 linear forms
+  already on-branch. Inside the union iff `margin ≥ 0`.
+- Cost ≈ a box AABB test. No mesh cook, no convex narrowphase.
+
+**Per-interaction threshold** — the one margin, two predicates:
+- **Skim** (grazing): dispatch iff `margin ≥ −skimBand`. Skimming is a proximity interaction —
+  the skimmer rides *near* the shell — so a band, not containment, is required. This is the
+  exact thing the tetrahedral branch's containment-only gate could not express, which is why
+  the Squirrel stopped skimming.
+- **Pop / damage** (penetration): dispatch iff `margin ≥ 0` (reached the surface).
+
+**Pending re-test** — `OnTriggerEnter` fires once. A contact that enters the enlarged AABB in a
+corner *outside* the shell (margin below threshold) is parked and re-tested on `OnTriggerStay`
+until it crosses the threshold (dispatch) or `OnTriggerExit` (drop). Without this, a swipe that
+enters the corner then sweeps into the shell never pops.
+
+**Both tetrahedral bugs die by construction:** grazing skims pass (band); no collider
+enable/disable under overlap (resize only) so no spurious second dispatch.
+
+## 3. Contracts (exact — build to these)
+
+### 3.1 `IShieldContainmentGate` (new file `_Scripts/Controller/Vessel/IShieldContainmentGate.cs`)
+```csharp
+public interface IShieldContainmentGate
+{
+    /// Signed margin of a WORLD point vs the engaged shell surface.
+    /// > 0 inside, 0 on surface, < 0 outside (normalized; magnitude ∝ distance).
+    float SignedMargin(Vector3 worldPoint);
+    /// Convenience: inside or on the surface. Default: SignedMargin(p) >= 0.
+    bool ContainsWorldPoint(Vector3 worldPoint);
+}
+```
+
+### 3.2 Generator math (add alongside the existing `ContainsPointLocal`)
+- `OctahedronMeshGenerator.SignedMarginLocal(Vector3 localPoint, float invA, float invB, float invC)`
+  → `1f - (|x|·invA + |y|·invB + |z|·invC)`. Keep `ContainsPointLocal` as `SignedMarginLocal(...) >= 0f`.
+- `StellatedOctahedronMeshGenerator.SignedMarginLocal(...)`
+  → compute `f1..f4`, `minF`, `maxF`; return `Mathf.Max(minF + 1f, 1f - maxF)`.
+  Keep `ContainsPointLocal` as `SignedMarginLocal(...) >= 0f`.
+
+### 3.3 Shield classes implement the gate
+`PrismOctahedronShield` and `PrismStellatedOctahedronShield` implement `IShieldContainmentGate`:
+- Precompute `invA/B/C = 1 / (shieldScale · halfExtent)` on engage (halfExtents already cached
+  as `_halfExtents`, center `_center`).
+- `SignedMargin(worldPoint)`: `local = transform.InverseTransformPoint(worldPoint) - _center;`
+  then the generator's `SignedMarginLocal(local, invA, invB, invC)`.
+
+### 3.4 Broadphase resize (shield classes, `ApplyShieldedPose` / `ApplyUnshieldedPose`)
+- On shielded pose: `boxCollider.size = _authoredSize * shieldScale;`
+  `boxCollider.center = _authoredCenter;` `boxCollider.enabled = true;` (never disabled).
+- On unshielded pose: restore `boxCollider.size = _authoredSize;` `boxCollider.enabled = true;`.
+- Cache `_authoredSize`/`_authoredCenter` at init before any resize. Reach factor comes from a
+  serialized `[SerializeField] float interactionShellScale = CIRCUMSCRIBING_SCALE;` (config knob,
+  default = the visual) — **not** hardcoded, per Config Separation.
+
+### 3.5 `Prism.ActiveShieldGate`
+- `public IShieldContainmentGate ActiveShieldGate { get; private set; }`
+- Set to the shield when its engage settles; cleared (`null`) on disengage and on pool return
+  (`OnDisable`). Wire from `PrismStateManager` shield engage/disengage (same site that toggles
+  the shield component), or from the shield's settle/withdraw callbacks.
+
+### 3.6 Dispatch narrowphase (`ImpactorBase`)
+- Add a protected virtual seam so each impactor picks its threshold:
+  `protected virtual float ShieldMarginThreshold => 0f;` (pop/containment default).
+  `SkimmerImpactor` overrides → `-skimBand` (serialized on the skimmer data container / config).
+- In `OnTriggerEnter`, after resolving `impacteeCollider.Impactor`: if the impactee's prism (or
+  this prism, self-side) has `ActiveShieldGate != null`, compute
+  `gate.SignedMargin(other.ClosestPoint(prismCenter))` and dispatch only if `≥ threshold`; else
+  park `(other → gate, threshold)` as pending.
+- Add virtual `OnTriggerStay`/`OnTriggerExit` on `ImpactorBase` that re-test / drop the pending
+  set. `SkimmerImpactor.OnTriggerStay/OnTriggerExit` must call `base` first (its own Stay/Exit
+  handle crystal-vacuum + skim-start bookkeeping — preserve them).
+- Self-side (this prism is the shielded one being entered): mirror via a
+  `protected virtual bool PassesOwnShieldNarrowphase(Collider other)` on `ImpactorBase` (default
+  true), overridden on `PrismImpactor` to test its own `Prism.ActiveShieldGate`.
+
+## 4. Phased plan
+
+| Phase | Deliverable | Verify |
+|---|---|---|
+| **0 — math** | `SignedMarginLocal` on both generators; `IShieldContainmentGate`; edit-mode tests (center → +, vertices → ~0, AABB corners → −, band points) | Edit-mode tests (no scene) |
+| **1 — payload** | Shell-AABB resize (no swap); `Prism.ActiveShieldGate`; dispatch narrowphase + pending re-test + per-interaction threshold + self-side | In-editor (below) |
+| **2 — tiers/budget** | Document point/box/sphere/bounds abstraction; add bulk-point broadphase shrink **only if** profiling shows the near-set cost matters; per-tier budget accounting | Profiler before/after |
+| **3 — docs** | Promote this to `ARCHITECTURE.md`; CLAUDE.md shield row; SPATIAL_INDEX cross-ref | — |
+
+## 5. Collider-budget impact (ecology hard gate)
+
+No new physics queries. No mesh colliders. One trigger per prism, unchanged. The shielded box
+grows to 3× (more broadphase over-cover → more candidate `OnTrigger` callbacks near a shielded
+prism), each resolved by a ~box-AABB margin test; shielded prisms remain `PrismColliderLodManager`-
+cullable exactly as today. Net: unchanged collider count, a cheap analytic test added only for the
+few shielded prisms actually near a focus.
+
+## 6. In-editor verification (human gate — cannot run Unity headless)
```

</details>

### `2e96827e7` — feat(collision): shielded prisms interact at their visible shell (bounds narrowphase)

_Claude, 2026-07-22 07:52:25 +0000_

```text
Phase 0-1 of Docs/CollisionLOD/DESIGN.md. Shielded/super-shielded prisms keep the
one primitive box trigger (never a mesh, never swapped) but resize it to the shell
AABB (interactionShellScale, default = the 3x visual); a signed-margin analytic
narrowphase then refines contacts down to the true octahedron / stella surface at
the ImpactorBase dispatch chokepoint, with a per-interaction threshold (skim = a
grazing proximity band, pop/damage = containment) and a pending re-test on
OnTriggerStay for contacts that enter an AABB corner before reaching the shell.

- SignedMarginLocal on both mesh generators (positive-inside; ContainsPointLocal
  redefined through it, one source of truth); IShieldContainmentGate; both shields
  implement it and publish Prism.ActiveShieldGate on settle, clear it on withdraw
  (ReferenceEquals-guarded for the shield<->super handoff) and on pool return.
- Both tetrahedral-branch failure modes are avoided by construction: grazing skims
  pass (band, not containment), and the collider is resize-only (no enable/disable
  cycle) so a single continuous overlap cannot double-dispatch (no pop-then-destroy).

Post-implementation adversarial review (fable-5) caught and this commit fixes:
the impactee-side gate probed the prism's OWN box (returning the centre, margin
always ~+1, gate never bit) -> now probes THIS impactor's own collider; and the
pose methods force-enabled the collider (fighting the spawn window / collider-LOD)
-> removed, poses are resize-only. Stella union math and compile verified clean.

In-editor verification (DESIGN.md section 6) is the human gate and still pending.
```

```text
 Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs    | 163 +++++++++++++++++++++++++++++++-
 Assets/_Scripts/Controller/ImpactEffects/Impactors/PrismImpactor.cs   |  18 +++-
 Assets/_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs |  24 ++++-
 Assets/_Scripts/Controller/Vessel/IShieldContainmentGate.cs           |  24 +++++
 Assets/_Scripts/Controller/Vessel/IShieldContainmentGate.cs.meta      |  11 +++
 Assets/_Scripts/Controller/Vessel/Prism.cs                            |  22 +++++
 Assets/_Scripts/Controller/Vessel/PrismOctahedronShield.cs            | 110 +++++++++++++++++----
 Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs   | 111 ++++++++++++++++++----
 Assets/_Scripts/Utility/OctahedronMeshGenerator.cs                    |  35 ++++++-
 Assets/_Scripts/Utility/StellatedOctahedronMeshGenerator.cs           |  50 +++++++---
 10 files changed, 509 insertions(+), 59 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 880 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
index a94c83aca..84abaa1b5 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
@@ -1,4 +1,5 @@
 using System;
+using System.Collections.Generic;
 using Unity.Netcode;
 using Unity.Profiling;
 using UnityEngine;
@@ -18,11 +19,68 @@ namespace CosmicShore.Gameplay
 
         public Transform Transform => transform;
         public abstract Domains OwnDomain { get; }
-        
+
         protected abstract void AcceptImpactee(IImpactor impactee);
 
         protected bool DoesEffectExist(ImpactEffectSO[] effects) => effects is { Length: > 0 };
 
+        // --- Shielded-prism narrowphase (Collision LOD analytic tier) --------
+        // A shielded prism's broadphase box is resized to over-cover its visible
+        // shell, so entering the box is no longer proof of touching the shell.
+        // Dispatch is gated on the shell's signed margin; each impactor picks its
+        // threshold: 0 = must reach the surface (pop/damage), negative = a grazing
+        // proximity band (skim). See Docs/CollisionLOD/DESIGN.md §2/§3.6.
+
+        /// <summary>
+        /// Minimum shell signed margin (normalized shell units) at which this
+        /// impactor dispatches against a shielded prism. 0 = containment
+        /// (reached the surface); SkimmerImpactor overrides with a negative
+        /// grazing band.
+        /// </summary>
+        protected virtual float ShieldMarginThreshold => 0f;
+
+        /// <summary>
+        /// Self-side narrowphase seam: when THIS impactor is itself a shielded
+        /// prism, an incoming contact must reach its shell before dispatch.
+        /// Default true (non-prism impactors have no shell of their own);
+        /// PrismImpactor overrides to test its own Prism.ActiveShieldGate.
+        /// </summary>
+        protected virtual bool PassesOwnShieldNarrowphase(Collider other) => true;
+
+        struct PendingShieldContact
+        {
+            public ImpactCollider ImpacteeCollider;
+            public PrismImpactor ImpacteePrism; // null = self-side (re-test own gate)
+        }
+
+        // Contacts that entered the enlarged broadphase box OUTSIDE the shell
+        // (margin below threshold): parked and re-tested on OnTriggerStay until
+        // they cross the threshold (dispatch) or OnTriggerExit (drop). Without
+        // this, a swipe that enters an AABB corner then sweeps into the shell
+        // would never dispatch — OnTriggerEnter only fires once. Lazily
+        // allocated: most impactors never meet a shielded prism.
+        Dictionary<Collider, PendingShieldContact> _pendingShieldContacts;
+
+        // This impactor's own trigger collider — the "toucher" whose nearest approach
+        // to a shielded prism the narrowphase measures. On the toucher's OnTrigger
+        // callback `other` is the prism's OWN (enlarged) box, so measuring `other`
+        // returns the prism centre (deep inside the shell) and defeats the gate; probe
+        // from this collider instead. Lazily resolved once.
+        Collider _ownCollider;
+        bool _ownColliderLookedUp;
+        Collider OwnCollider
+        {
+            get
+            {
+                if (!_ownColliderLookedUp)
+                {
+                    _ownColliderLookedUp = true;
+                    TryGetComponent(out _ownCollider);
+                }
+                return _ownCollider;
+            }
+        }
+
         // Per-concrete-type profiler marker so an impact storm shows up in captures as
         // e.g. 'SkimmerImpactor.AcceptImpactee' with real timings instead of vanishing
         // into Physics.SendEvents self-time. Lazily created (one string per component
@@ -44,6 +102,107 @@ namespace CosmicShore.Gameplay
             if (!other.TryGetComponent(out ImpactCollider impacteeCollider))
                 return;
 
+            // Self-side narrowphase: if THIS impactor is a shielded prism, the
+            // incoming contact must reach its analytic shell first.
+            if (!PassesOwnShieldNarrowphase(other))
+            {
+                ParkPendingContact(other, impacteeCollider, null);
+                return;
+            }
+
+            // Impactee-side narrowphase: entering a shielded prism's enlarged
+            // broadphase box only dispatches once the contact reaches its shell
+            // (margin >= this impactor's threshold).
+            if (impacteeCollider.Impactor is PrismImpactor prismImpactee
+                && !PassesShieldGate(prismImpactee))
+            {
+                ParkPendingContact(other, impacteeCollider, prismImpactee);
+                return;
+            }
+
+            DispatchAccept(impacteeCollider);
+        }
+
+        /// <summary>
+        /// Re-tests parked contacts against the shell each physics tick and
+        /// dispatches once the margin crosses this impactor's threshold.
+        /// Overrides MUST call base first.
+        /// </summary>
+        protected virtual void OnTriggerStay(Collider other)
+        {
+            if (_pendingShieldContacts == null || _pendingShieldContacts.Count == 0)
+                return;
+
+            if (!_pendingShieldContacts.TryGetValue(other, out var pending))
+                return;
+
+            if (!isInitialized)
+                return;
+
+            if (pending.ImpacteeCollider == null)
+            {
+                // Impactee destroyed while parked — nothing left to dispatch.
+                _pendingShieldContacts.Remove(other);
+                return;
+            }
+
+            bool passes = pending.ImpacteePrism != null
+                ? PassesShieldGate(pending.ImpacteePrism)
+                : PassesOwnShieldNarrowphase(other);
+
+            if (!passes)
+                return;
+
+            _pendingShieldContacts.Remove(other);
+            DispatchAccept(pending.ImpacteeCollider);
+        }
+
+        /// <summary>
+        /// Drops any parked shell contact for the departing collider.
+        /// Overrides MUST call base first.
+        /// </summary>
+        protected virtual void OnTriggerExit(Collider other)
+        {
+            _pendingShieldContacts?.Remove(other);
+        }
+
+        /// <summary>
+        /// True when the contact may dispatch against the impactee prism: the
```

</details>

### `c250df430` — docs(collision): record round-2 re-verify findings + open items

_Claude, 2026-07-22 08:07:17 +0000_

```text
Adversarial re-verify confirmed the lifecycle/compile fixes and stella math, but
found shell-precision needs a real sphere-vs-shell margin (point sample toward the
prism centre under-measures grazes -> dead zones, worst on the non-convex stella),
a unified threshold (pop dispatches via the skimmer damage effect, so skim+pop share
one threshold), and PhysX-timing handling for two pop-then-destroy paths (same-tick
parked dispatch after gate clear; one-swing re-enter after the 3x->1x shrink). The
last is a gameplay-feel fork. Shell-precision is not headless-verifiable; #617
(authored-size shielded skimming) remains the working baseline.
```

```text
 Docs/CollisionLOD/DESIGN.md | 35 +++++++++++++++++++++++++++++++++++
 1 file changed, 35 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/CollisionLOD/DESIGN.md b/Docs/CollisionLOD/DESIGN.md
index 06891545b..1873f9ef6 100644
--- a/Docs/CollisionLOD/DESIGN.md
+++ b/Docs/CollisionLOD/DESIGN.md
@@ -153,3 +153,38 @@ few shielded prisms actually near a focus.
    back → restores.
 5. **No pop-then-destroy under a lingering swipe**; **no skim dead-zone** in the AABB corners.
 6. Profiler: no regression in `Physics.SendEvents` / `*.AcceptImpactee` around dense shielded mass.
+
+## 7. Re-verify findings (round 2) — status & open items
+
+Two fable-5 adversarial rounds against the working tree. Confirmed done vs open:
+
+- **[done] Lifecycle / compile** — collider is resize-only (the four `enabled = true` pose
+  writes removed; `enabled` owned by spawn window + collider-LOD + destruction); `ActiveShieldGate`
+  cleared on withdraw + `OnDisable`; `SignedMargin`/interface/overrides compile. Verified exhaustively.
+- **[done] Stella union math** — `max(minF+1, 1−maxF)` derived + landmark-checked equivalent to the
+  old boolean.
+- **[open — needs sphere metric] Skim precision** — the impactee-side probe now uses this impactor's
+  own collider, but `Collider.ClosestPoint(prismCentre)` samples the sphere point toward the prism
+  *centre*, not its nearest approach to the *shell*, so tangential grazes under-measure → dead zones
+  (elongated prisms; worst on the non-convex stella, where a spike grazes through an inter-spike gap).
+  **Fix:** `IShieldContainmentGate.SignedMarginSphere(worldCentre, worldRadius)` = `SignedMargin(centre)
+  + worldRadius · |shell gradient in world|` (octahedron: per-octant L1 gradient via lossyScale; stella:
+  per linear form, max over the two tetrahedra); use it when the toucher is a `SphereCollider`. The
+  metric is approximate and **must be tuned/confirmed in-editor**.
+- **[open — threshold] Pop dispatches via the skimmer's damage effect** (`RhinoSkimmerDamagePrismEffectSO`
+  → `Prism.Damage`), so skim (graze) and pop (reach shell) share the skimmer's one threshold — a −band
+  threshold pops the shield ~0.35 normalized units *outside* the shell. With a proper sphere margin,
+  **threshold 0** unifies both at "sphere reaches shell" and removes the band (no per-effect threshold
+  needed) — needs editor feel-check.
+- **[open — PhysX race] Same-tick pop-then-destroy** — a contact parked in the enlarged box, when the
+  pop clears the gate mid-callback, re-tests with `gate==null` → dispatches as a plain-box hit → destroy.
+  **Fix:** in `OnTriggerStay`, drop a parked contact whose prism gate went null (don't dispatch — a real
+  hit re-fires `OnTriggerEnter`).
+- **[open — GAMEPLAY FORK] One-swing pop+destroy** — the pop shrinks the box 3×→1×, so the sword exits
+  and the swing's follow-through re-enters the 1× box → destroy in the *same* swing. Bleeding-edge
+  popped-only per swing (the 1× box stayed overlapped). Whether one swing may pop AND destroy is a
+  feel decision (accept it, or add a short post-pop grace against the popper).
+
+**Reality:** the merged #617 (authored-size shielded skimming) is the working baseline. Shell-precision
+adds a sphere-vs-shell metric, a threshold/feel choice, and PhysX-timing handling that can only be
+**confirmed in-editor** — it is not a headless-verifiable change.
```

</details>

### `f8c068c2a` — feat(collision): shape-precise shielded collision at the visible shell

_Claude, 2026-07-22 08:30:46 +0000_

```text
Shell-precise narrowphase for shielded/super-shielded prisms, on top of the
#617 box-trigger baseline. The primitive box trigger resizes to the shell AABB
(no swap), and impact dispatch refines it to the true octahedron/stella via an
analytic signed-margin gate.

- SignedMargin/SignedMarginSphere on both shells (octahedron L1, stella 4-form),
  reusing the on-branch ContainsPointLocal math. Sphere touchers gate on the
  sphere-vs-shell margin (centre + radius x world gradient) so grazes register
  without dead zones; point touchers use ClosestPoint.
- Unified threshold 0 ("sphere reaches the shell") for skim and pop; removed the
  skim band that popped shields in thin air.
- Parked-contact re-test drops on a null shell gate (impactee-side and self-side)
  so a pop cannot destroy the freshly-unshielded prism in its own tick.
- Collider stays resize-only; enabled owned by spawn window / collider-LOD /
  destruction. One-swing pop+destroy left emergent (no bespoke grace).

Reach is a per-shell config knob (interactionShellScale, default = the 3x
visual). Not yet play-tested: the in-editor checklist in Docs/CollisionLOD/DESIGN.md
sec 6 is the gate for feel/timing.
```

```text
 Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs    | 94 ++++++++++++++++++++++++++-------
 Assets/_Scripts/Controller/ImpactEffects/Impactors/PrismImpactor.cs   |  4 ++
 Assets/_Scripts/Controller/ImpactEffects/Impactors/SkimmerImpactor.cs | 20 ++++---
 Assets/_Scripts/Controller/Vessel/IShieldContainmentGate.cs           | 10 ++++
 Assets/_Scripts/Controller/Vessel/PrismOctahedronShield.cs            | 19 +++++++
 Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs   | 19 +++++++
 Docs/CollisionLOD/DESIGN.md                                           | 23 ++++++++
 7 files changed, 160 insertions(+), 29 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 303 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
index 84abaa1b5..e23d54369 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
@@ -27,15 +27,17 @@ namespace CosmicShore.Gameplay
         // --- Shielded-prism narrowphase (Collision LOD analytic tier) --------
         // A shielded prism's broadphase box is resized to over-cover its visible
         // shell, so entering the box is no longer proof of touching the shell.
-        // Dispatch is gated on the shell's signed margin; each impactor picks its
-        // threshold: 0 = must reach the surface (pop/damage), negative = a grazing
-        // proximity band (skim). See Docs/CollisionLOD/DESIGN.md §2/§3.6.
+        // Dispatch is gated on the shell's signed margin. Sphere touchers (the
+        // skimmer sphere included) are measured with the sphere-vs-shell margin,
+        // so "sphere reaches the shell" (margin >= 0) is the one condition for
+        // both skim and pop — no per-impactor grazing band needed.
+        // See Docs/CollisionLOD/DESIGN.md §2/§3.6/§7.
 
         /// <summary>
         /// Minimum shell signed margin (normalized shell units) at which this
-        /// impactor dispatches against a shielded prism. 0 = containment
-        /// (reached the surface); SkimmerImpactor overrides with a negative
-        /// grazing band.
+        /// impactor dispatches against a shielded prism. Default 0 = containment
+        /// (the toucher — sphere-aware for SphereCollider touchers — reaches the
+        /// shell surface).
         /// </summary>
         protected virtual float ShieldMarginThreshold => 0f;
 
@@ -47,6 +49,15 @@ namespace CosmicShore.Gameplay
         /// </summary>
         protected virtual bool PassesOwnShieldNarrowphase(Collider other) => true;
 
+        /// <summary>
+        /// Whether THIS impactor currently has an engaged shell of its own (only a
+        /// shielded prism does; base = false). Used to DROP a self-side parked
+        /// contact when the shell is popped/withdrawn since parking — mirrors the
+        /// impactee-side gate-null drop so a pop can't destroy the freshly
+        /// unshielded prism in its own tick via a parked corner contact.
+        /// </summary>
+        protected virtual bool HasOwnShieldGate => false;
+
         struct PendingShieldContact
         {
             public ImpactCollider ImpacteeCollider;
@@ -146,12 +157,42 @@ namespace CosmicShore.Gameplay
                 return;
             }
 
-            bool passes = pending.ImpacteePrism != null
-                ? PassesShieldGate(pending.ImpacteePrism)
-                : PassesOwnShieldNarrowphase(other);
+            if (pending.ImpacteePrism != null)
+            {
+                // Impactee-side parked contact: only dispatch while the shell
+                // that parked it is still engaged. If the gate went null the
+                // shell was popped/withdrawn since parking (possibly by this
+                // very swing, mid-callback) — DROP the contact instead of
+                // dispatching it as a plain-box hit, which would destroy the
+                // freshly-unshielded prism in the same tick as the pop. A
+                // genuine hit on the restored authored box produces its own
+                // fresh OnTriggerEnter. See Docs/CollisionLOD/DESIGN.md §7.
+                var prism = pending.ImpacteePrism.Prism;
+                if (prism == null || prism.ActiveShieldGate == null)
+                {
+                    _pendingShieldContacts.Remove(other);
+                    return;
+                }
 
-            if (!passes)
-                return;
+                if (!PassesShieldGate(pending.ImpacteePrism))
+                    return;
+            }
+            else
+            {
+                // Self-side parked contact: THIS prism's shell parked it. If the
+                // shell is gone now (popped/withdrawn since parking, possibly this
+                // very swing), DROP — mirrors the impactee-side gate-null drop so
+                // the pop can't destroy the freshly-unshielded prism in its tick
+                // via a corner contact that never reached the shell.
+                if (!HasOwnShieldGate)
+                {
+                    _pendingShieldContacts.Remove(other);
+                    return;
+                }
+
+                if (!PassesOwnShieldNarrowphase(other))
+                    return;
+            }
 
             _pendingShieldContacts.Remove(other);
             DispatchAccept(pending.ImpacteeCollider);
@@ -168,8 +209,11 @@ namespace CosmicShore.Gameplay
 
         /// <summary>
         /// True when the contact may dispatch against the impactee prism: the
-        /// prism has no engaged shell, or the contact's closest point to the
-        /// prism center is within this impactor's margin threshold of the shell.
+        /// prism has no engaged shell, or this impactor's toucher reaches the
+        /// shell within this impactor's margin threshold. Sphere touchers use
+        /// the sphere-vs-shell margin (centre + radius × world gradient) —
+        /// a ClosestPoint sample toward the prism centre under-measures
+        /// tangential grazes and creates skim dead zones.
         /// </summary>
         bool PassesShieldGate(PrismImpactor prismImpactee)
         {
@@ -179,12 +223,26 @@ namespace CosmicShore.Gameplay
 
             var gate = prism.ActiveShieldGate;
             if (gate == null)
-                return true; // unshielded (or shell dropped while parked): the box IS the shape
+                return true; // unshielded: the box IS the shape
+
+            // Sphere touchers (skimmer sphere, other sphere triggers): gate on
+            // the analytic sphere-vs-shell margin at the sphere's WORLD centre
+            // and WORLD radius — "sphere reaches the shell" — rather than a
+            // point sampled toward the prism centre.
+            if (OwnCollider is SphereCollider sc)
+            {
+                Vector3 worldCentre = sc.transform.TransformPoint(sc.center);
+                Vector3 ls = sc.transform.lossyScale;
+                float worldRadius = sc.radius * Mathf.Max(Mathf.Abs(ls.x),
+                    Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z)));
+                return gate.SignedMarginSphere(worldCentre, worldRadius) >= ShieldMarginThreshold;
+            }
 
-            // Probe from THIS impactor's OWN collider — the toucher's nearest approach
-            // to the prism centre. Measuring the prism's own box (the `other` that
-            // entered our trigger) would return the centre and evaluate the margin
-            // deep inside the shell (~+1), so the gate would never bite.
+            // Non-sphere touchers: probe from THIS impactor's OWN collider — the
+            // toucher's nearest approach to the prism centre. Measuring the
+            // prism's own box (the `other` that entered our trigger) would return
+            // the centre and evaluate the margin deep inside the shell (~+1), so
+            // the gate would never bite.
             var probe = OwnCollider != null
                 ? OwnCollider.ClosestPoint(prism.transform.position)
                 : transform.position;
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/PrismImpactor.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/PrismImpactor.cs
index bc2319999..2933092e5 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/PrismImpactor.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/PrismImpactor.cs
@@ -39,6 +39,10 @@ namespace CosmicShore.Gameplay
             return gate.SignedMargin(other.ClosestPoint(transform.position)) >= ShieldMarginThreshold;
         }
 
+        // True only while this prism's shell is engaged — lets ImpactorBase drop a
+        // self-side parked contact if the shell pops/withdraws before it dispatches.
+        protected override bool HasOwnShieldGate => Prism != null && Prism.ActiveShieldGate != null;
+
```

</details>

### `95c9a565c` — docs(collision): CLAUDE.md shield row reflects shipped shape-precise collision

_Claude, 2026-07-22 14:21:55 +0000_

```text
The shield's box trigger resizes to the shell AABB and dispatch refines to the
true octahedron/stella via the analytic signed-margin narrowphase (no mesh
collider); update the "planned follow-up" note to the shipped mechanism.
```

```text
 CLAUDE.md | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/CLAUDE.md b/CLAUDE.md
index 0c91da08b..04f427f77 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -1805,7 +1805,7 @@ All game code lives under `CosmicShore.*` with 8 primary namespaces:
 | Prism lifecycle | `Prism`, `PrismFactory`, `Trail`, `TrailFollower` | `_Scripts/Controller/Vessel/`, `_Scripts/Controller/Prisms/` |
 | Prism performance | `PrismScaleManager`, `MaterialStateManager`, `AdaptiveAnimationManager`, `PrismStateManager`, `PrismTimerManager`, `BlockDensityGrid` | `_Scripts/Controller/Managers/` |
 | Prism spatial index | `PrismSpatialIndex` (formerly `PrismAOERegistry`) — THE canonical spatial index of all live prism mass: Burst AOE damage queries + growth occupancy (`TryReserve` claim-before-spawn closes the disabled-collider spawn race) + bucket hash grid. One registration lifecycle (`Register`/`MarkDestroyed`/`MarkRestored`/`Unregister`/`UpdatePosition`), multiple query views. Do not build parallel spatial stores or query prisms via physics — see `Docs/SPATIAL_INDEX.md` | `_Scripts/Controller/Managers/` |
-| Shield octahedra | `PrismOctahedronShield` (the SHIELDED state's octahedron: per-face bloom engage + shatter-overlay disengage, mass scales with volume; the COLLIDER stays the authored primitive box TRIGGER — the octahedron is a look-only change, because a convex-mesh trigger is invisible to trigger-skimmers and a convex-mesh solid is invisible to solid swipes, whereas the primitive box trigger is seen by both, exactly like an unshielded prism; shape-precise shielded collision is the planned three-LOD follow-up), `PrismStellatedOctahedronShield` (the SUPER-SHIELDED state's stellated octahedron / Stella Octangula — the Skim Race track look; engaged by `PrismStateManager.ActivateSuperShield` with the OPAQUE team material, reversed by `DeactivateShields`), testers, `OctahedronMeshGenerator` / `StellatedOctahedronMeshGenerator` (`PopulateMesh*` + `GetSharedShieldMesh` quantized-geometry caches). **Both integrate with the instanced prism render path via the `SetExoticVisualActive` / `SetRenderMeshOverride` handoff — see the anti-pattern below on why a bare MeshFilter swap renders nothing** | `_Scripts/Controller/Vessel/`, `_Scripts/Utility/` |
+| Shield octahedra | `PrismOctahedronShield` (the SHIELDED state's octahedron: per-face bloom engage + shatter-overlay disengage, mass scales with volume; the COLLIDER stays the authored primitive box TRIGGER — never a convex MeshCollider, which is invisible to trigger-skimmers as a trigger and to solid swipes as a solid, whereas the primitive box is seen by both; shape-precise shielded collision is achieved WITHOUT a mesh collider by RESIZING the box to the shell AABB and refining dispatch to the true octahedron/stella via an analytic signed-margin narrowphase (`IShieldContainmentGate.SignedMargin`/`SignedMarginSphere` on the shield → gated at the `ImpactorBase` chokepoint; box stays resize-only, never swapped/disabled — see `Docs/CollisionLOD/DESIGN.md`)), `PrismStellatedOctahedronShield` (the SUPER-SHIELDED state's stellated octahedron / Stella Octangula — the Skim Race track look; engaged by `PrismStateManager.ActivateSuperShield` with the OPAQUE team material, reversed by `DeactivateShields`), testers, `OctahedronMeshGenerator` / `StellatedOctahedronMeshGenerator` (`PopulateMesh*` + `GetSharedShieldMesh` quantized-geometry caches). **Both integrate with the instanced prism render path via the `SetExoticVisualActive` / `SetRenderMeshOverride` handoff — see the anti-pattern below on why a bare MeshFilter swap renders nothing** | `_Scripts/Controller/Vessel/`, `_Scripts/Utility/` |
 | Impact effects | `ImpactorBase` + 11 impactor types, 20+ Effect SO types | `_Scripts/Controller/ImpactEffects/` |
 | Forcefield crackle | `SkimmerForcefieldCracklePrismEffectSO` (computes impact points via `Collider.ClosestPoint`), `ForcefieldCrackleController` (`[ExecuteAlways]`, 16-impact ring buffer + MaterialPropertyBlock arrays, owns all visual params), `ForcefieldCrackle.hlsl` (FBM electrical arcs on geodesic sphere), `ForcefieldCrackleControllerEditor` (edit-mode preview) | `_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/`, `_Scripts/Controller/Vessel/`, `Assets/Materials/Graphs/`, `_Scripts/Editor/` |
 | Camera | `CustomCameraController`, `VesselCameraCustomizer`, `CameraSettingsSO`, `ICameraController`, `ICameraConfigurator` | `_Scripts/Controller/Camera/` |
```

</details>

### `8287772a5` — fix(collision): shape-aware shielded narrowphase — hulls catch at the tips

_Claude, 2026-07-22 15:49:59 +0000_

```text
First playtest: the 3x box reach is live but a vessel hull clipped through the
pointy octahedron/stella tips because the non-sphere gate tested a SINGLE point
(nearest approach to the prism centre), blind to the thin tips and the hull body.
Only the sphere skimmer path was shape-aware.

Make every collider shape-aware via ImpactorBase.ColliderReachesShell — the
analytic equivalent of a convex octahedron/stella mesh collider:
- Sphere: exact sphere-vs-shell margin (unchanged).
- Any other convex/primitive collider: MAX shell margin over two support points -
  the collider's farthest point toward the shell interior
  (ClosestPoint(centre + ShellInwardNormal*big), catches the tips) and its nearest
  point to the prism centre (deep body).
- New IShieldContainmentGate.ShellInwardNormal returns the nearest-facet inward
  normal (octahedron: octant L1 coefficients; stella: active linear form).
- Impactee-side and self-side both route through the one helper.

Needs the in-editor pass: hull should catch at the tips without over-reaching in
open space.
```

```text
 Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs  | 56 ++++++++++++++++++++++++-----------
 Assets/_Scripts/Controller/ImpactEffects/Impactors/PrismImpactor.cs |  6 +++-
 Assets/_Scripts/Controller/Vessel/IShieldContainmentGate.cs         | 10 +++++++
 Assets/_Scripts/Controller/Vessel/PrismOctahedronShield.cs          | 20 +++++++++++++
 Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs | 42 ++++++++++++++++++++++++++
 Docs/CollisionLOD/DESIGN.md                                         | 24 +++++++++++++++
 6 files changed, 139 insertions(+), 19 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 225 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
index e23d54369..9a7ad69c4 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
@@ -225,28 +225,48 @@ namespace CosmicShore.Gameplay
             if (gate == null)
                 return true; // unshielded: the box IS the shape
 
-            // Sphere touchers (skimmer sphere, other sphere triggers): gate on
-            // the analytic sphere-vs-shell margin at the sphere's WORLD centre
-            // and WORLD radius — "sphere reaches the shell" — rather than a
-            // point sampled toward the prism centre.
-            if (OwnCollider is SphereCollider sc)
+            // THIS impactor's collider is the toucher; test it against the
+            // impactee prism's shell.
+            return ColliderReachesShell(OwnCollider, gate, prism.transform.position,
+                ShieldMarginThreshold, transform.position);
+        }
+
+        /// <summary>
+        /// Shape-aware "does collider <paramref name="c"/> reach the shell
+        /// <paramref name="gate"/> within <paramref name="threshold"/>" test — the
+        /// analytic equivalent of a convex octahedron/stella mesh collider.
+        /// A SphereCollider uses the exact sphere-vs-shell margin. Any other
+        /// convex/primitive collider takes the MAX shell margin over two SUPPORT
+        /// points: its farthest point toward the shell interior
+        /// (<see cref="IShieldContainmentGate.ShellInwardNormal"/> — this is what
+        /// catches the thin tips) and its nearest point to the prism centre (the
+        /// deep-body case). A single centre-facing sample misses the tips, which is
+        /// why hulls clipped straight through them. ClosestPoint requires a
+        /// primitive or CONVEX collider (the gameplay case).
+        /// </summary>
+        protected static bool ColliderReachesShell(Collider c, IShieldContainmentGate gate,
+            Vector3 prismCentre, float threshold, Vector3 fallbackPoint)
+        {
+            if (c == null)
+                return gate.SignedMargin(fallbackPoint) >= threshold;
+
+            if (c is SphereCollider sc)
             {
-                Vector3 worldCentre = sc.transform.TransformPoint(sc.center);
+                Vector3 wc = sc.transform.TransformPoint(sc.center);
                 Vector3 ls = sc.transform.lossyScale;
-                float worldRadius = sc.radius * Mathf.Max(Mathf.Abs(ls.x),
-                    Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z)));
-                return gate.SignedMarginSphere(worldCentre, worldRadius) >= ShieldMarginThreshold;
+                float r = sc.radius * Mathf.Max(Mathf.Abs(ls.x), Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z)));
+                return gate.SignedMarginSphere(wc, r) >= threshold;
             }
 
-            // Non-sphere touchers: probe from THIS impactor's OWN collider — the
-            // toucher's nearest approach to the prism centre. Measuring the
-            // prism's own box (the `other` that entered our trigger) would return
-            // the centre and evaluate the margin deep inside the shell (~+1), so
-            // the gate would never bite.
-            var probe = OwnCollider != null
-                ? OwnCollider.ClosestPoint(prism.transform.position)
-                : transform.position;
-            return gate.SignedMargin(probe) >= ShieldMarginThreshold;
+            // Convex/primitive hull: MAX margin over the support toward the shell
+            // interior (catches the thin tips) and the nearest point to the centre
+            // (deep body). ClosestPoint(centre + inward·big) is the support point.
+            Vector3 centre = c.bounds.center;
+            Vector3 inward = gate.ShellInwardNormal(centre);
+            float margin = Mathf.Max(
+                gate.SignedMargin(c.ClosestPoint(centre + inward * 1000f)),
+                gate.SignedMargin(c.ClosestPoint(prismCentre)));
+            return margin >= threshold;
         }
 
         void ParkPendingContact(Collider other, ImpactCollider impacteeCollider, PrismImpactor impacteePrism)
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/PrismImpactor.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/PrismImpactor.cs
index 2933092e5..56fe9b0b6 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/PrismImpactor.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/PrismImpactor.cs
@@ -36,7 +36,11 @@ namespace CosmicShore.Gameplay
             if (gate == null)
                 return true;
 
-            return gate.SignedMargin(other.ClosestPoint(transform.position)) >= ShieldMarginThreshold;
+            // Same shape-aware test as the impactee side, but the toucher is
+            // `other` and the shell is THIS prism's — so a hull grazing our
+            // octahedron/stella tips is caught, not just a centre-facing point.
+            return ColliderReachesShell(other, gate, transform.position,
+                ShieldMarginThreshold, other.transform.position);
         }
 
         // True only while this prism's shell is engaged — lets ImpactorBase drop a
diff --git a/Assets/_Scripts/Controller/Vessel/IShieldContainmentGate.cs b/Assets/_Scripts/Controller/Vessel/IShieldContainmentGate.cs
index 25b1c3866..49dee1565 100644
--- a/Assets/_Scripts/Controller/Vessel/IShieldContainmentGate.cs
+++ b/Assets/_Scripts/Controller/Vessel/IShieldContainmentGate.cs
@@ -28,6 +28,16 @@ namespace CosmicShore.Gameplay
         /// </summary>
         float SignedMarginSphere(Vector3 worldCentre, float worldRadius);
 
+        /// <summary>
+        /// World-space unit direction of steepest margin INCREASE at a world point
+        /// (i.e. toward the shell interior — the inward normal of the nearest
+        /// facet). The dispatch narrowphase queries a non-sphere collider's SUPPORT
+        /// point in this direction (its farthest point toward the shell), so a hull
+        /// is tested where it actually reaches the octahedron/stella surface — the
+        /// thin tips included — instead of at a single centre-facing point.
+        /// </summary>
+        Vector3 ShellInwardNormal(Vector3 worldPoint);
+
         /// <summary>Convenience: inside or on the surface (SignedMargin ≥ 0).</summary>
         bool ContainsWorldPoint(Vector3 worldPoint);
     }
diff --git a/Assets/_Scripts/Controller/Vessel/PrismOctahedronShield.cs b/Assets/_Scripts/Controller/Vessel/PrismOctahedronShield.cs
index dd9b0e229..1220b2448 100644
--- a/Assets/_Scripts/Controller/Vessel/PrismOctahedronShield.cs
+++ b/Assets/_Scripts/Controller/Vessel/PrismOctahedronShield.cs
@@ -374,6 +374,26 @@ namespace CosmicShore.Gameplay
             return SignedMargin(worldCentre) + worldRadius * gradWorld;
         }
 
+        /// <summary>
+        /// Inward (toward-interior) unit normal of the octahedron facet nearest a
+        /// world point. In the active octant the L1 face is the plane
+        /// sign(x)·invA·x + sign(y)·invB·y + sign(z)·invC·z = 1, so its inward
+        /// normal (decreasing L1 sum = increasing margin) is the negated coefficient
+        /// vector, mapped to world.
+        /// </summary>
+        public Vector3 ShellInwardNormal(Vector3 worldPoint)
+        {
+            Vector3 local = transform.InverseTransformPoint(worldPoint) - _center;
+            Vector3 inwardLocal = new Vector3(
+                -Mathf.Sign(local.x) * _shellInvA,
+                -Mathf.Sign(local.y) * _shellInvB,
+                -Mathf.Sign(local.z) * _shellInvC);
+            Vector3 world = transform.TransformDirection(inwardLocal);
+            return world.sqrMagnitude > 1e-10f
+                ? world.normalized
+                : (transform.TransformPoint(_center) - worldPoint).normalized;
+        }
+
         /// <summary>Inside or on the interaction shell surface.</summary>
         public bool ContainsWorldPoint(Vector3 worldPoint) => SignedMargin(worldPoint) >= 0f;
 
diff --git a/Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs b/Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs
index c35b9aad6..46f7899ef 100644
--- a/Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs
+++ b/Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs
@@ -383,6 +383,48 @@ namespace CosmicShore.Gameplay
             return SignedMargin(worldCentre) + worldRadius * gradWorld;
         }
 
+        /// <summary>
+        /// Inward (toward-interior) unit normal of the stella facet nearest a world
```

</details>

### `292655cd6` — fix(collision): hull clips through tips — child colliders + exact OBB-vs-shell SAT

_Claude, 2026-07-22 21:06:53 +0000_

```text
Two confirmed root causes (5-agent diagnosis) of the Squirrel hull passing
through shielded octahedron/stella tips:

1) The vessel's hull BoxColliders live on CHILD GameObjects (VesselImpactor sits
   on the root with the kinematic Rigidbody). ImpactorBase resolved the toucher
   collider via TryGetComponent on its OWN GameObject -> null -> the narrowphase
   fell back to a single point at the ship pivot, so the shape-aware test never
   ran on the vessel side (only the pivot-deep region interacted). Fix: cache the
   impactor's collider SET (own GO, else GetComponentsInChildren) and dispatch if
   ANY reaches the shell; pivot fallback only when no collider exists at all.

2) The two-point sample false-rejected ~18% of rotated-OBB tip overlaps. Fix:
   exact analytic OBB-vs-shell overlap via a Separating-Axis Test
   (IShieldContainmentGate.OverlapsWorldBox -> OverlapsBoxNormalized): octahedron
   as the L1 unit ball (25 axes); stella as the union of two tetrahedra, SAT per
   tet OR'd. BoxCollider touchers use it exactly; other colliders use a
   conservative world-AABB OBB; the sphere skimmer path is unchanged.

The SAT is numerically proven against exact LP + dense-grid ground truth:
0/72751 false-reject on the octahedron, 0/94373 on the stella, 0% false-accept
both, across 200k cases biased toward the tips/spikes. Still gated on the
in-editor pass (cannot run Unity headless). Docs/CollisionLOD/DESIGN.md sec 7.3.
```

```text
 Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs  | 114 ++++++++++++++++++++++++----------
 Assets/_Scripts/Controller/Vessel/IShieldContainmentGate.cs         |  21 +++++++
 Assets/_Scripts/Controller/Vessel/PrismOctahedronShield.cs          |  20 ++++++
 Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs |  21 +++++++
 Assets/_Scripts/Utility/OctahedronMeshGenerator.cs                  |  80 ++++++++++++++++++++++++
 Assets/_Scripts/Utility/StellatedOctahedronMeshGenerator.cs         | 104 +++++++++++++++++++++++++++++++
 Docs/CollisionLOD/DESIGN.md                                         |  39 ++++++++++++
 7 files changed, 367 insertions(+), 32 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 496 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
index 9a7ad69c4..c51b354a4 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
@@ -72,23 +72,31 @@ namespace CosmicShore.Gameplay
         // allocated: most impactors never meet a shielded prism.
         Dictionary<Collider, PendingShieldContact> _pendingShieldContacts;
 
-        // This impactor's own trigger collider — the "toucher" whose nearest approach
-        // to a shielded prism the narrowphase measures. On the toucher's OnTrigger
-        // callback `other` is the prism's OWN (enlarged) box, so measuring `other`
-        // returns the prism centre (deep inside the shell) and defeats the gate; probe
-        // from this collider instead. Lazily resolved once.
-        Collider _ownCollider;
-        bool _ownColliderLookedUp;
-        Collider OwnCollider
+        // This impactor's own trigger collider SET — the "touchers" whose overlap with a
+        // shielded prism the narrowphase measures. On a toucher's OnTrigger callback
+        // `other` is the prism's OWN (enlarged) box, so measuring `other` returns the
+        // prism centre (deep inside the shell) and defeats the gate; probe from these
+        // colliders instead. The impactor's collider often lives on CHILD GameObjects
+        // (e.g. VesselImpactor sits on the vessel ROOT with the kinematic Rigidbody,
+        // while the hull BoxColliders are on children — the Squirrel has two), so a
+        // same-GO-only lookup returns null and defeats the whole shape test. Prefer
+        // collider(s) on this GO; else fall back to the compound hull in children.
+        // Lazily resolved once.
+        Collider[] _ownColliders;
+        bool _ownCollidersLookedUp;
+        Collider[] OwnColliders
         {
             get
             {
-                if (!_ownColliderLookedUp)
+                if (!_ownCollidersLookedUp)
                 {
-                    _ownColliderLookedUp = true;
-                    TryGetComponent(out _ownCollider);
+                    _ownCollidersLookedUp = true;
+                    var own = GetComponents<Collider>();
+                    _ownColliders = (own != null && own.Length > 0)
+                        ? own
+                        : GetComponentsInChildren<Collider>(true);
                 }
-                return _ownCollider;
+                return _ownColliders;
             }
         }
 
@@ -225,24 +233,52 @@ namespace CosmicShore.Gameplay
             if (gate == null)
                 return true; // unshielded: the box IS the shape
 
-            // THIS impactor's collider is the toucher; test it against the
-            // impactee prism's shell.
-            return ColliderReachesShell(OwnCollider, gate, prism.transform.position,
+            // THIS impactor's collider(s) are the touchers; dispatch if ANY of them
+            // reaches the impactee prism's shell (logical OR — a compound hull is one
+            // toucher made of several boxes).
+            return AnyColliderReachesShell(OwnColliders, gate, prism.transform.position,
                 ShieldMarginThreshold, transform.position);
         }
 
+        /// <summary>
+        /// OR over an impactor's collider SET: dispatch if ANY collider reaches the shell.
+        /// A compound hull can be several colliders (the Squirrel's two boxes); the
+        /// impactor touches the shell if any piece does. Falls back to the single
+        /// pivot-point test ONLY when the set holds no live collider at all.
+        /// </summary>
+        protected static bool AnyColliderReachesShell(Collider[] colliders, IShieldContainmentGate gate,
+            Vector3 prismCentre, float threshold, Vector3 fallbackPoint)
+        {
+            bool sawCollider = false;
+            if (colliders != null)
+            {
+                for (int i = 0; i < colliders.Length; i++)
+                {
+                    var c = colliders[i];
+                    if (c == null)
+                        continue;
+                    sawCollider = true;
+                    if (ColliderReachesShell(c, gate, prismCentre, threshold, fallbackPoint))
+                        return true;
+                }
+            }
+
+            // No collider exists at all — measure from the impactor's pivot.
+            return sawCollider ? false : gate.SignedMargin(fallbackPoint) >= threshold;
+        }
+
         /// <summary>
         /// Shape-aware "does collider <paramref name="c"/> reach the shell
         /// <paramref name="gate"/> within <paramref name="threshold"/>" test — the
         /// analytic equivalent of a convex octahedron/stella mesh collider.
-        /// A SphereCollider uses the exact sphere-vs-shell margin. Any other
-        /// convex/primitive collider takes the MAX shell margin over two SUPPORT
-        /// points: its farthest point toward the shell interior
-        /// (<see cref="IShieldContainmentGate.ShellInwardNormal"/> — this is what
-        /// catches the thin tips) and its nearest point to the prism centre (the
-        /// deep-body case). A single centre-facing sample misses the tips, which is
-        /// why hulls clipped straight through them. ClosestPoint requires a
-        /// primitive or CONVEX collider (the gameplay case).
+        /// A SphereCollider uses the exact sphere-vs-shell margin. A BoxCollider uses
+        /// the exact analytic OBB-vs-shell overlap (Separating-Axis Test) — no
+        /// false-reject at the thin tips, unlike the old two-point support sample that
+        /// let hulls clip straight through. Any other convex/primitive collider is
+        /// approximated by its world-AABB OBB (conservative over-cover). The
+        /// <paramref name="threshold"/> maps to the shell inflate in normalized units
+        /// (inflate = −threshold): threshold 0 ⇒ exact containment/pop, a negative
+        /// grazing threshold ⇒ the shell is grown by that magnitude.
         /// </summary>
         protected static bool ColliderReachesShell(Collider c, IShieldContainmentGate gate,
             Vector3 prismCentre, float threshold, Vector3 fallbackPoint)
@@ -258,15 +294,29 @@ namespace CosmicShore.Gameplay
                 return gate.SignedMarginSphere(wc, r) >= threshold;
             }
 
-            // Convex/primitive hull: MAX margin over the support toward the shell
-            // interior (catches the thin tips) and the nearest point to the centre
-            // (deep body). ClosestPoint(centre + inward·big) is the support point.
-            Vector3 centre = c.bounds.center;
-            Vector3 inward = gate.ShellInwardNormal(centre);
-            float margin = Mathf.Max(
-                gate.SignedMargin(c.ClosestPoint(centre + inward * 1000f)),
-                gate.SignedMargin(c.ClosestPoint(prismCentre)));
-            return margin >= threshold;
+            // Shell inflate (normalized units): 0 = exact containment/pop; a negative
+            // grazing threshold grows the shell by its magnitude.
+            float inflate = -threshold;
+
+            if (c is BoxCollider bc)
+            {
+                Transform t = bc.transform;
+                Vector3 worldCenter = t.TransformPoint(bc.center);
+                Vector3 axX = t.TransformVector(new Vector3(bc.size.x * 0.5f, 0f, 0f));
+                Vector3 axY = t.TransformVector(new Vector3(0f, bc.size.y * 0.5f, 0f));
+                Vector3 axZ = t.TransformVector(new Vector3(0f, 0f, bc.size.z * 0.5f));
+                return gate.OverlapsWorldBox(worldCenter, axX, axY, axZ, inflate);
+            }
+
+            // Any other convex/primitive (or non-convex mesh) collider: approximate as
+            // its world-AABB OBB. Conservative — over-covers the true hull, the safe
+            // direction (never a skim dead zone). ClosestPoint isn't usable for a
+            // general shape, and the AABB SAT still catches the thin tips.
+            Bounds b = c.bounds;
+            Vector3 ext = b.extents;
+            return gate.OverlapsWorldBox(b.center,
+                new Vector3(ext.x, 0f, 0f), new Vector3(0f, ext.y, 0f), new Vector3(0f, 0f, ext.z),
+                inflate);
         }
 
```

</details>

### `3f8ab0404` — Update Squirrel.prefab

_Garrett Milliron, 2026-07-22 21:37:47 -0400_

```text
remove collider
```

```text
 Assets/_Prefabs/Spacevessels/Squirrel.prefab | 37 ++++---------------------------------
 1 file changed, 4 insertions(+), 33 deletions(-)
```

### `0b7978139` — fix(collision): shrink hull to visible silhouette in shielded narrowphase

_Claude, 2026-07-24 15:22:02 +0000_

```text
The exact OBB-vs-shell SAT was correct, but the authored hull box is a loose
bounding box larger than the visible mesh, so it popped a shielded prism the
instant the box EDGE reached the small octahedron shell — "the box outline
pops it, not the octahedron."

Add HullNarrowphaseScale (ImpactorBase, default 1) that uniformly shrinks the
toucher's box half-edges / sphere radius about its own centre in the analytic
shell test ONLY — never the physics broadphase trigger, so the parked-contact
re-test window (and tunneling safety) is unchanged; the shrink only decides
dispatch within an existing overlap. VesselImpactor exposes it as a live
Range(0.2,1) inspector knob (hullNarrowphaseScale, default 0.7) so each
vessel's effective hull hugs its visible silhouette; the skimmer sphere stays
scale-1/exact to avoid a tangential-skim dead zone.

Also remove the now-dead ShellInwardNormal (+ stella CoeffOf helper): the
two-point support sample it fed was replaced by the SAT in a prior commit,
leaving zero callers. Interface, both shields, and DESIGN.md §7.2 updated.
```

```text
 Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs   | 42 ++++++++++++++++++++++++++--------
 Assets/_Scripts/Controller/ImpactEffects/Impactors/VesselImpactor.cs | 11 +++++++++
 Assets/_Scripts/Controller/Vessel/IShieldContainmentGate.cs          | 10 --------
 Assets/_Scripts/Controller/Vessel/PrismOctahedronShield.cs           | 20 ----------------
 Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs  | 42 ----------------------------------
 CLAUDE.md                                                            |  2 +-
 Docs/CollisionLOD/DESIGN.md                                          | 26 ++++++++++++++++++++-
 7 files changed, 69 insertions(+), 84 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 286 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
index c51b354a4..af58cba9b 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/ImpactorBase.cs
@@ -41,6 +41,22 @@ namespace CosmicShore.Gameplay
         /// </summary>
         protected virtual float ShieldMarginThreshold => 0f;
 
+        /// <summary>
+        /// Uniform shrink applied to THIS impactor's hull collider(s) in the
+        /// shielded narrowphase ONLY — never to the physics broadphase trigger.
+        /// A vessel's authored hull box is a loose bounding box, larger than the
+        /// visible mesh, so an exact box-vs-shell overlap fires the instant the
+        /// box EDGE reaches the shell while the visible ship still has a gap
+        /// ("the box outline pops it"). Scaling the box half-edges about their
+        /// centre by this factor lets the effective hull hug the visible
+        /// silhouette so contact reads as ship-touches-shell, not
+        /// box-touches-shell. 1 = the authored collider (default — the skimmer
+        /// sphere MUST stay exact or tangential skims dead-zone). VesselImpactor
+        /// exposes it as a live-tunable inspector knob.
+        /// See Docs/CollisionLOD/DESIGN.md §7.4.
+        /// </summary>
+        protected virtual float HullNarrowphaseScale => 1f;
+
         /// <summary>
         /// Self-side narrowphase seam: when THIS impactor is itself a shielded
         /// prism, an incoming contact must reach its shell before dispatch.
@@ -235,9 +251,10 @@ namespace CosmicShore.Gameplay
 
             // THIS impactor's collider(s) are the touchers; dispatch if ANY of them
             // reaches the impactee prism's shell (logical OR — a compound hull is one
-            // toucher made of several boxes).
+            // toucher made of several boxes). HullNarrowphaseScale shrinks the loose
+            // hull box down to the visible silhouette (vessels only; 1 elsewhere).
             return AnyColliderReachesShell(OwnColliders, gate, prism.transform.position,
-                ShieldMarginThreshold, transform.position);
+                ShieldMarginThreshold, transform.position, HullNarrowphaseScale);
         }
 
         /// <summary>
@@ -247,7 +264,7 @@ namespace CosmicShore.Gameplay
         /// pivot-point test ONLY when the set holds no live collider at all.
         /// </summary>
         protected static bool AnyColliderReachesShell(Collider[] colliders, IShieldContainmentGate gate,
-            Vector3 prismCentre, float threshold, Vector3 fallbackPoint)
+            Vector3 prismCentre, float threshold, Vector3 fallbackPoint, float hullScale = 1f)
         {
             bool sawCollider = false;
             if (colliders != null)
@@ -258,7 +275,7 @@ namespace CosmicShore.Gameplay
                     if (c == null)
                         continue;
                     sawCollider = true;
-                    if (ColliderReachesShell(c, gate, prismCentre, threshold, fallbackPoint))
+                    if (ColliderReachesShell(c, gate, prismCentre, threshold, fallbackPoint, hullScale))
                         return true;
                 }
             }
@@ -281,17 +298,21 @@ namespace CosmicShore.Gameplay
         /// grazing threshold ⇒ the shell is grown by that magnitude.
         /// </summary>
         protected static bool ColliderReachesShell(Collider c, IShieldContainmentGate gate,
-            Vector3 prismCentre, float threshold, Vector3 fallbackPoint)
+            Vector3 prismCentre, float threshold, Vector3 fallbackPoint, float hullScale = 1f)
         {
             if (c == null)
                 return gate.SignedMargin(fallbackPoint) >= threshold;
 
+            // hullScale shrinks the effective toucher about its OWN centre so a loose
+            // bounding collider reads as the visible silhouette, not the box outline.
+            if (hullScale <= 0f) hullScale = 1f;
+
             if (c is SphereCollider sc)
             {
                 Vector3 wc = sc.transform.TransformPoint(sc.center);
                 Vector3 ls = sc.transform.lossyScale;
                 float r = sc.radius * Mathf.Max(Mathf.Abs(ls.x), Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z)));
-                return gate.SignedMarginSphere(wc, r) >= threshold;
+                return gate.SignedMarginSphere(wc, r * hullScale) >= threshold;
             }
 
             // Shell inflate (normalized units): 0 = exact containment/pop; a negative
@@ -302,9 +323,10 @@ namespace CosmicShore.Gameplay
             {
                 Transform t = bc.transform;
                 Vector3 worldCenter = t.TransformPoint(bc.center);
-                Vector3 axX = t.TransformVector(new Vector3(bc.size.x * 0.5f, 0f, 0f));
-                Vector3 axY = t.TransformVector(new Vector3(0f, bc.size.y * 0.5f, 0f));
-                Vector3 axZ = t.TransformVector(new Vector3(0f, 0f, bc.size.z * 0.5f));
+                float h = 0.5f * hullScale;
+                Vector3 axX = t.TransformVector(new Vector3(bc.size.x * h, 0f, 0f));
+                Vector3 axY = t.TransformVector(new Vector3(0f, bc.size.y * h, 0f));
+                Vector3 axZ = t.TransformVector(new Vector3(0f, 0f, bc.size.z * h));
                 return gate.OverlapsWorldBox(worldCenter, axX, axY, axZ, inflate);
             }
 
@@ -313,7 +335,7 @@ namespace CosmicShore.Gameplay
             // direction (never a skim dead zone). ClosestPoint isn't usable for a
             // general shape, and the AABB SAT still catches the thin tips.
             Bounds b = c.bounds;
-            Vector3 ext = b.extents;
+            Vector3 ext = b.extents * hullScale;
             return gate.OverlapsWorldBox(b.center,
                 new Vector3(ext.x, 0f, 0f), new Vector3(0f, ext.y, 0f), new Vector3(0f, 0f, ext.z),
                 inflate);
diff --git a/Assets/_Scripts/Controller/ImpactEffects/Impactors/VesselImpactor.cs b/Assets/_Scripts/Controller/ImpactEffects/Impactors/VesselImpactor.cs
index 446180c32..5b1ff5280 100644
--- a/Assets/_Scripts/Controller/ImpactEffects/Impactors/VesselImpactor.cs
+++ b/Assets/_Scripts/Controller/ImpactEffects/Impactors/VesselImpactor.cs
@@ -25,6 +25,17 @@ namespace CosmicShore.Gameplay
         [SerializeField] VesselImpactorDataContainerSO vesselImpactorDataContainerSO;
         [SerializeField] NetworkVesselImpactor networkVesselImpactor;
 
+        [Header("Shielded-prism narrowphase")]
+        [Tooltip("Uniform shrink of the hull collider(s) used ONLY for shielded-prism " +
+                 "shape collision — never the physics trigger, never the skimmer sphere. " +
+                 "The authored hull box is a loose bounding box larger than the visible " +
+                 "mesh, so the exact box-vs-octahedron test fires when the box EDGE reaches " +
+                 "the shell while the visible ship still has a gap. Lower this until the " +
+                 "interaction fires when the VISIBLE ship touches the octahedron/stella. " +
+                 "1 = authored box.")]
+        [SerializeField, Range(0.2f, 1f)] float hullNarrowphaseScale = 0.7f;
+        protected override float HullNarrowphaseScale => hullNarrowphaseScale;
+
         readonly Dictionary<int, float> _lastCrystalImpactTime = new();
 
         SkimmerImpactor[] _skimmerImpactors;
diff --git a/Assets/_Scripts/Controller/Vessel/IShieldContainmentGate.cs b/Assets/_Scripts/Controller/Vessel/IShieldContainmentGate.cs
index fcf9dc4d3..f85ac5307 100644
--- a/Assets/_Scripts/Controller/Vessel/IShieldContainmentGate.cs
+++ b/Assets/_Scripts/Controller/Vessel/IShieldContainmentGate.cs
@@ -28,16 +28,6 @@ namespace CosmicShore.Gameplay
         /// </summary>
         float SignedMarginSphere(Vector3 worldCentre, float worldRadius);
 
-        /// <summary>
-        /// World-space unit direction of steepest margin INCREASE at a world point
-        /// (i.e. toward the shell interior — the inward normal of the nearest
-        /// facet). The dispatch narrowphase queries a non-sphere collider's SUPPORT
-        /// point in this direction (its farthest point toward the shell), so a hull
-        /// is tested where it actually reaches the octahedron/stella surface — the
-        /// thin tips included — instead of at a single centre-facing point.
-        /// </summary>
-        Vector3 ShellInwardNormal(Vector3 worldPoint);
-
         /// <summary>Convenience: inside or on the surface (SignedMargin ≥ 0).</summary>
         bool ContainsWorldPoint(Vector3 worldPoint);
 
diff --git a/Assets/_Scripts/Controller/Vessel/PrismOctahedronShield.cs b/Assets/_Scripts/Controller/Vessel/PrismOctahedronShield.cs
index 8487dbf17..fe8678a19 100644
```

</details>
