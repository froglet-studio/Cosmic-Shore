# Prism Collision LOD — Design & Build Plan

**Status:** committed plan, Phase 0–1 building. **Base:** bleeding-edge (post PR #617).
**Owner branch:** `claude/skimmers-shielded-prisms-hek21c`.

## 0. Why

PR #617 fixed shielded-prism skimming by keeping the authored **primitive box trigger** as
the collider (no convex `MeshCollider`, no swap). That works — every skimmer family and the
Rhino swipe see the prism again — but the interaction happens at the **bare prism size**, so
the 3× visible shield shell is cosmetic: you skim/pop the small prism, not the octahedron you
see. This plan restores **shape-precise interaction at the visible shell**, cheaply, without
reintroducing the two bugs that sank the parallel `tetrahedral-collider-cost` branch.

It also formalizes the collision-LOD story the codebase already half-implements.

## 1. The three tiers (and what already exists)

The player-facing idea (from the prompter): bucket prism collision into three LODs.

| Tier | Shape | Serves | Status on bleeding-edge |
|---|---|---|---|
| **Point / box** | authored primitive box **trigger** | bulk direct contact (the 95% case) | ✅ exists; `PrismColliderLodManager` already culls the box far from every focus, so active-collider count is bounded by radius, not population |
| **Sphere / volumetric** | sphere via the Burst index | explosions / AOE / large-scale | ✅ exists; `ExplosionImpactor` is the *only* impactor that routes through `PrismSpatialIndex` |
| **Bespoke analytic bounds** | octahedron (shielded) / stella (super-shielded) | precision, player-noticed objects | ❌ **this plan** |

**Refinement finding:** tiers 1 and 2 are already realized. The genuinely new, high-value tier
is the analytic bounds — and it is exactly what completes the shield work. So we front-load it;
point/sphere are *measure-first* formalization (Phase 2), not new machinery.

## 2. Mechanism (validated against the code)

**Broadphase** — one stable primitive collider, never a mesh, never swapped.
- Unshielded: authored box trigger at authored size (unchanged).
- Shielded/super-shielded: the **same** box trigger, **resized** to the shell AABB
  (`size × shieldScale`, `shieldScale = CIRCUMSCRIBING_SCALE = 3`) so the broadphase
  over-covers the visible shell. Resize on engage, resize back on disengage. **Never
  disable/enable or swap the collider** — resizing an already-overlapping box can only fire
  `OnTriggerExit` (shrink) or a fresh `OnTriggerEnter` for *newly*-covered colliders, never a
  second Enter for a contact that was already overlapping. That is what structurally prevents
  the pop-then-destroy bug (which was caused by *enabling a previously-disabled* collider under
  a continuous overlap).
- Rides `PrismColliderLodManager` unchanged (it culls/restores `boxCollider` by proximity).

**Narrowphase** — a signed-margin refinement at the dispatch chokepoint, active only when the
prism has an engaged shield.
- `IShieldContainmentGate.SignedMargin(worldPoint)`: **> 0 inside, 0 on the surface, < 0
  outside**, in the shell's normalized metric (magnitude ∝ distance-to-surface).
- Octahedron: `margin = 1 − (|x|·invA + |y|·invB + |z|·invC)` (the L1 test already on-branch,
  returning the slack instead of the bool).
- Stella (union of two tetrahedra): `margin = max(minF + 1, 1 − maxF)` over the 4 linear forms
  already on-branch. Inside the union iff `margin ≥ 0`.
- Cost ≈ a box AABB test. No mesh cook, no convex narrowphase.

**Per-interaction threshold** — the one margin, two predicates:
- **Skim** (grazing): dispatch iff `margin ≥ −skimBand`. Skimming is a proximity interaction —
  the skimmer rides *near* the shell — so a band, not containment, is required. This is the
  exact thing the tetrahedral branch's containment-only gate could not express, which is why
  the Squirrel stopped skimming.
- **Pop / damage** (penetration): dispatch iff `margin ≥ 0` (reached the surface).

**Pending re-test** — `OnTriggerEnter` fires once. A contact that enters the enlarged AABB in a
corner *outside* the shell (margin below threshold) is parked and re-tested on `OnTriggerStay`
until it crosses the threshold (dispatch) or `OnTriggerExit` (drop). Without this, a swipe that
enters the corner then sweeps into the shell never pops.

**Both tetrahedral bugs die by construction:** grazing skims pass (band); no collider
enable/disable under overlap (resize only) so no spurious second dispatch.

## 3. Contracts (exact — build to these)

### 3.1 `IShieldContainmentGate` (new file `_Scripts/Controller/Vessel/IShieldContainmentGate.cs`)
```csharp
public interface IShieldContainmentGate
{
    /// Signed margin of a WORLD point vs the engaged shell surface.
    /// > 0 inside, 0 on surface, < 0 outside (normalized; magnitude ∝ distance).
    float SignedMargin(Vector3 worldPoint);
    /// Convenience: inside or on the surface. Default: SignedMargin(p) >= 0.
    bool ContainsWorldPoint(Vector3 worldPoint);
}
```

### 3.2 Generator math (add alongside the existing `ContainsPointLocal`)
- `OctahedronMeshGenerator.SignedMarginLocal(Vector3 localPoint, float invA, float invB, float invC)`
  → `1f - (|x|·invA + |y|·invB + |z|·invC)`. Keep `ContainsPointLocal` as `SignedMarginLocal(...) >= 0f`.
- `StellatedOctahedronMeshGenerator.SignedMarginLocal(...)`
  → compute `f1..f4`, `minF`, `maxF`; return `Mathf.Max(minF + 1f, 1f - maxF)`.
  Keep `ContainsPointLocal` as `SignedMarginLocal(...) >= 0f`.

### 3.3 Shield classes implement the gate
`PrismOctahedronShield` and `PrismStellatedOctahedronShield` implement `IShieldContainmentGate`:
- Precompute `invA/B/C = 1 / (shieldScale · halfExtent)` on engage (halfExtents already cached
  as `_halfExtents`, center `_center`).
- `SignedMargin(worldPoint)`: `local = transform.InverseTransformPoint(worldPoint) - _center;`
  then the generator's `SignedMarginLocal(local, invA, invB, invC)`.

### 3.4 Broadphase resize (shield classes, `ApplyShieldedPose` / `ApplyUnshieldedPose`)
- On shielded pose: `boxCollider.size = _authoredSize * shieldScale;`
  `boxCollider.center = _authoredCenter;` `boxCollider.enabled = true;` (never disabled).
- On unshielded pose: restore `boxCollider.size = _authoredSize;` `boxCollider.enabled = true;`.
- Cache `_authoredSize`/`_authoredCenter` at init before any resize. Reach factor comes from a
  serialized `[SerializeField] float interactionShellScale = CIRCUMSCRIBING_SCALE;` (config knob,
  default = the visual) — **not** hardcoded, per Config Separation.

### 3.5 `Prism.ActiveShieldGate`
- `public IShieldContainmentGate ActiveShieldGate { get; private set; }`
- Set to the shield when its engage settles; cleared (`null`) on disengage and on pool return
  (`OnDisable`). Wire from `PrismStateManager` shield engage/disengage (same site that toggles
  the shield component), or from the shield's settle/withdraw callbacks.

### 3.6 Dispatch narrowphase (`ImpactorBase`)
- Add a protected virtual seam so each impactor picks its threshold:
  `protected virtual float ShieldMarginThreshold => 0f;` (pop/containment default).
  `SkimmerImpactor` overrides → `-skimBand` (serialized on the skimmer data container / config).
- In `OnTriggerEnter`, after resolving `impacteeCollider.Impactor`: if the impactee's prism (or
  this prism, self-side) has `ActiveShieldGate != null`, compute
  `gate.SignedMargin(other.ClosestPoint(prismCenter))` and dispatch only if `≥ threshold`; else
  park `(other → gate, threshold)` as pending.
- Add virtual `OnTriggerStay`/`OnTriggerExit` on `ImpactorBase` that re-test / drop the pending
  set. `SkimmerImpactor.OnTriggerStay/OnTriggerExit` must call `base` first (its own Stay/Exit
  handle crystal-vacuum + skim-start bookkeeping — preserve them).
- Self-side (this prism is the shielded one being entered): mirror via a
  `protected virtual bool PassesOwnShieldNarrowphase(Collider other)` on `ImpactorBase` (default
  true), overridden on `PrismImpactor` to test its own `Prism.ActiveShieldGate`.

## 4. Phased plan

| Phase | Deliverable | Verify |
|---|---|---|
| **0 — math** | `SignedMarginLocal` on both generators; `IShieldContainmentGate`; edit-mode tests (center → +, vertices → ~0, AABB corners → −, band points) | Edit-mode tests (no scene) |
| **1 — payload** | Shell-AABB resize (no swap); `Prism.ActiveShieldGate`; dispatch narrowphase + pending re-test + per-interaction threshold + self-side | In-editor (below) |
| **2 — tiers/budget** | Document point/box/sphere/bounds abstraction; add bulk-point broadphase shrink **only if** profiling shows the near-set cost matters; per-tier budget accounting | Profiler before/after |
| **3 — docs** | Promote this to `ARCHITECTURE.md`; CLAUDE.md shield row; SPATIAL_INDEX cross-ref | — |

## 5. Collider-budget impact (ecology hard gate)

No new physics queries. No mesh colliders. One trigger per prism, unchanged. The shielded box
grows to 3× (more broadphase over-cover → more candidate `OnTrigger` callbacks near a shielded
prism), each resolved by a ~box-AABB margin test; shielded prisms remain `PrismColliderLodManager`-
cullable exactly as today. Net: unchanged collider count, a cheap analytic test added only for the
few shielded prisms actually near a focus.

## 6. In-editor verification (human gate — cannot run Unity headless)

1. **Rhino vs shielded prism** (Skim Race track / any shielded prism): swipe pops the shield and
   does **not** destroy the inner prism; popping at the *shell* distance (≈3× reach), not point-blank.
2. **Squirrel vs shielded prism**: skims register while grazing the **shell** (not only on
   center-punch); danger-skim energy still applies.
3. **Super-shielded (stella)**: same two checks against a stellated prism (Astro League edge
   lining / Skim Race).
4. **LOD**: fly away from a shielded prism → its collider culls (no skim/pop at distance); fly
   back → restores.
5. **No pop-then-destroy under a lingering swipe**; **no skim dead-zone** in the AABB corners.
6. Profiler: no regression in `Physics.SendEvents` / `*.AcceptImpactee` around dense shielded mass.

## 7. Re-verify findings (round 2) — status & open items

Two fable-5 adversarial rounds against the working tree. Confirmed done vs open:

- **[done] Lifecycle / compile** — collider is resize-only (the four `enabled = true` pose
  writes removed; `enabled` owned by spawn window + collider-LOD + destruction); `ActiveShieldGate`
  cleared on withdraw + `OnDisable`; `SignedMargin`/interface/overrides compile. Verified exhaustively.
- **[done] Stella union math** — `max(minF+1, 1−maxF)` derived + landmark-checked equivalent to the
  old boolean.
- **[open — needs sphere metric] Skim precision** — the impactee-side probe now uses this impactor's
  own collider, but `Collider.ClosestPoint(prismCentre)` samples the sphere point toward the prism
  *centre*, not its nearest approach to the *shell*, so tangential grazes under-measure → dead zones
  (elongated prisms; worst on the non-convex stella, where a spike grazes through an inter-spike gap).
  **Fix:** `IShieldContainmentGate.SignedMarginSphere(worldCentre, worldRadius)` = `SignedMargin(centre)
  + worldRadius · |shell gradient in world|` (octahedron: per-octant L1 gradient via lossyScale; stella:
  per linear form, max over the two tetrahedra); use it when the toucher is a `SphereCollider`. The
  metric is approximate and **must be tuned/confirmed in-editor**.
- **[open — threshold] Pop dispatches via the skimmer's damage effect** (`RhinoSkimmerDamagePrismEffectSO`
  → `Prism.Damage`), so skim (graze) and pop (reach shell) share the skimmer's one threshold — a −band
  threshold pops the shield ~0.35 normalized units *outside* the shell. With a proper sphere margin,
  **threshold 0** unifies both at "sphere reaches shell" and removes the band (no per-effect threshold
  needed) — needs editor feel-check.
- **[open — PhysX race] Same-tick pop-then-destroy** — a contact parked in the enlarged box, when the
  pop clears the gate mid-callback, re-tests with `gate==null` → dispatches as a plain-box hit → destroy.
  **Fix:** in `OnTriggerStay`, drop a parked contact whose prism gate went null (don't dispatch — a real
  hit re-fires `OnTriggerEnter`).
- **[open — GAMEPLAY FORK] One-swing pop+destroy** — the pop shrinks the box 3×→1×, so the sword exits
  and the swing's follow-through re-enters the 1× box → destroy in the *same* swing. Bleeding-edge
  popped-only per swing (the 1× box stayed overlapped). Whether one swing may pop AND destroy is a
  feel decision (accept it, or add a short post-pop grace against the popper).

**Reality:** the merged #617 (authored-size shielded skimming) is the working baseline. Shell-precision
adds a sphere-vs-shell metric, a threshold/feel choice, and PhysX-timing handling that can only be
**confirmed in-editor** — it is not a headless-verifiable change.

### 7.1 Resolution (implemented)

All of §7 is implemented on this branch:

- **Sphere-vs-shell margin** — `IShieldContainmentGate.SignedMarginSphere(centre, R) = SignedMargin(centre)
  + R·√((invA/sx)² + (invB/sy)² + (invC/sz)²)` on both shells; `ImpactorBase.PassesShieldGate` gates
  `SphereCollider` touchers on it (world centre + world radius). Conservative across octant/facet seams
  (fires slightly early, never a dead zone).
- **Unified threshold 0** — the skimmer's `ShieldMarginThreshold` override and `_skimBand` are gone; skim
  and pop both fire when the sphere reaches the shell (pop no longer triggers in thin air).
- **Same-tick pop-then-destroy** — `OnTriggerStay` drops a parked contact whose shell went null since
  parking, on BOTH the impactee-side (`pending.ImpacteePrism.ActiveShieldGate == null`) and the self-side
  (`HasOwnShieldGate == false`) — a pop cannot destroy the freshly-unshielded prism in its own tick via a
  corner contact that never reached the shell.
- **One-swing pop+destroy** — left **emergent** (no bespoke post-pop grace): a swing that reaches through a
  popped shell hits the prism; a graze pops only. Revisit only if the editor feel-check wants a beat.

**Verification status:** implemented and reviewed by inspection (the fable-5 adversarial verifier hit its
usage limit mid-run, so the final pass was manual). Not compiled or play-tested here. The §6 in-editor
checklist is the gate — pay special attention to skim reach vs the visible shell, pop landing on the
shell (not early/late), the stella (non-convex) inter-spike gaps, and whether one swing popping AND
killing feels right.

### 7.2 Shape-aware narrowphase (in-editor: hulls clipped through the tips)

First playtest confirmed the 3× box **reach** is live (the `BoxCollider` gizmo encloses the spikes) but a
**vessel hull clipped straight through the pointy tips** — only the inner region interacted. Cause: the
non-sphere path gated on a SINGLE point — the collider's nearest approach to the prism *centre*
(`ClosestPoint(prismCentre)`) — which cannot see the thin octahedron/stella tips or the hull's extended
body. Only the sphere skimmer path was shape-aware.

Fix — make every collider shape-aware, the analytic equivalent of a convex octahedron/stella mesh
collider (`ImpactorBase.ColliderReachesShell`):
- **Sphere** → exact sphere-vs-shell margin (unchanged).
- **Any other convex/primitive collider** → MAX shell margin over two SUPPORT points: the collider's
  farthest point toward the shell interior — `ClosestPoint(centre + ShellInwardNormal·big)` — which
  catches the thin tips, and its nearest point to the prism centre (deep body). New
  `IShieldContainmentGate.ShellInwardNormal(worldPoint)` returns the inward normal of the nearest facet
  (octahedron: the octant's negated L1 coefficients; stella: the active linear form's coefficients).
- Both the impactee-side (`ImpactorBase`) and the self-side (`PrismImpactor`) route through the one helper.
- Non-convex `MeshCollider` touchers degrade to the centre sample (over-reach, the safe direction) since
  `Collider.ClosestPoint` requires primitive/convex geometry — gameplay hulls are convex/primitive.

Cost: the non-sphere narrowphase is now 2 `ClosestPoint` + 2 margin evals (was 1+1), only for shielded
contacts with a non-sphere toucher. Still needs the in-editor pass: confirm the hull now catches at the
octahedron/stella tips and does not over-reach in open space.
