# The spherical-blast omni-crystal morph (Butterfly, Rhino, Manta)

**The crystal does not shatter. It becomes the blast.**

Three hulls answer an omni crystal with the same thing: a plain spherical `AOEExplosion` centred on
the hull. Until now the crystal *also* burst into the shared spent-crystal husk spray, so the pickup
read as two unrelated events: something shattered, and separately a sphere went off. It now reads
as one: the cage swells out from where the crystal was, lands on the blast's surface, and the blast
takes the surface over while the crystal dissolves off it.

This is a **per-vessel omni-crystal retirement** (`VesselImpactorDataContainerSO.OmniCrystalRetirement`),
after the Squirrel's ring (`SQUIRREL_CRYSTAL_MORPH.md`), the Scarab's ball (`SCARAB_CRYSTAL_MORPH.md`)
and the Dolphin's cone (`DOLPHIN_CRYSTAL_MORPH.md`, PR #1680). One target serves all three hulls,
because the thing they become is the same class. It shares everything but the target with the others
(`CrystalMorphRunner`), and the fleet's feel (`Resources/CrystalMorphConfig`) is unchanged.

---

## 1. What each hull's omni-crystal ability is (the analysis)

| hull | effect that fires the blast | prefab | delay / duration | size (MaxScale) | sphere radius at the hand-off (0.374 s) |
|---|---|---|---|---|---|
| Butterfly | `ButterflyVesselExplosionByCrystalEffect` | `AOEButterflyBloom` | 0 / 0.6 s | 900 | ≈ 375 u (83% grown) |
| Rhino | `RhinoVesselExplosionByCrystalEffect` (beside the Surge) | `AOESlowExplosion` | 0.2 / 3 s | 200–500 by resource | ≈ 9–23 u (9% grown) |
| Manta | `MantaKabloomByCrystalEffect`, the extra self-blast | `AOEMantaBloom` (+ `AOEFlowerCreation`, skipped) | 0.2 / 3 s | 140 × Kabloom multiplier | ≈ 6 u (9% grown) |

All three prefabs are the base `AOEExplosion` class drawing Unity's built-in sphere (radius 0.5 at unit
scale). The blast is posed once at the hull (plus the effect's spawn offset: the Rhino's is 5 u
behind) and never moves; it grows `sin(t·π/2)` of full size over its duration after its delay, and its
opacity is `1 − that`. Its colours are the AOE material's `_TextureColor` (body) and `_FresnelColor`
(rim), tinted per domain by `ThemeManager`.

So the reading differs by hull. The Butterfly's bloom is fast and huge, and the crystal bursts out
to a sphere a hundred times its size. The Rhino's and Manta's blasts are slow, so at the hand-off the
sphere is still small (a few crystal widths). There the crystal folds into a small sphere that then
keeps swelling as the blast.

## 2. Why the cast starts from the crystal's middle

A sphere is convex, so this is the Scarab's mapping: every vertex of the cage lands on the sphere's
surface along a ray and takes that facet's normal (`CrystalMorphMeshBuilder.ConvexHullTarget`).

But the blast is centred on the **hull**, not on the crystal. For the Butterfly's 375 u sphere, a
crystal 20 to 30 u from the centre lies in nearly one direction from it, and the plain cast piles the
whole cage onto one patch. Any point strictly inside a convex hull is a valid ray origin, so the ray
origin is the **crystal's own middle** (`SphereBlastMorphGeometry.CastCentre`). If the crystal sits
further than half a radius from the centre (a small Rhino or Manta sphere), the origin is clamped
toward it at half a radius, so it stays strictly inside. No change to the shared builder was needed.
Measured by `SphereBlastCrystalMorphGeometryTests`: cast from the crystal, landed points reach over 90%
of 26 sampled directions. Cast from the centre (the negative control), they reach under 20%.

## 3. What makes the hand-off seamless

1. **It draws the crystal's own renderers** (`CrystalMorphRunner.Adopt`), so frame 0 is the crystal.
2. **It ends where the sphere is drawn when it reappears.** `AOEExplosion.BlastLocalToWorldAt(seconds)`
   reads the blast's own curve (delay, `sin` ease, `MaxScaleVector`) at `MorphSeconds` after Initialize.
   That is the same frame the morph is stamped. Retune the blast's scale, delay or duration and the
   morph follows.
3. **Only the blast's photons wait.** `AOEExplosion.HoldPhotonsForMorph` sets `Renderer.forceRenderingOff`
   and nothing else: the sweep, the trigger and the prism damage run from the frame of the pickup
   exactly as before. **The ability is unchanged.**
4. **The colour pair converges** on the blast's `_TextureColor`/`_FresnelColor`, read off the material
   the blast was initialised with.
5. **It starts in the pose the crystal HAD** (`CrystalForgeOrigin`), never its live transform.

**What is NOT exact**, said plainly:

- The blast is a transparent, Fresnel-rimmed ripple shader. The body converges on BlockGraph's
  formula, which is close to a rim but not the same. The dissolve tail (the last 15%) absorbs the
  difference. Expect a soft cross-over, as on the Dolphin.
- The Butterfly's sphere is ~83% grown at the hand-off, so its opacity is ~17% there. The plates land
  on a sphere that is almost transparent. The cross-over is soft, but the plates may read as the
  brighter of the two for a moment.
- The blast's growth is integrated from `deltaTime`, so the sphere can be up to one frame larger
  than the landed plates.

## 4. How it runs

```
Butterfly / Rhino / Manta hull touches an omni crystal
  ├─ OmniCrystalImpactor.ExecuteEffect                        [server]
  │     vessel carries an OmniCrystalRetirement → ExplodeParams.SuppressHusk = true
  │
  └─ VesselImpactor.ExecuteOmniCrystalImpact                  [owner → server → EVERY peer]
        ├─ OmniCrystalRetirement (runs FIRST)
        │     SphereBlastCrystalMorphByCrystalEffectSO (one asset, all three hulls)
        │       └─ SphereBlastCrystalMorph.Begin(data.Origin, vessel, "<hull>")
        │             resolve crystal by id · adopt body + overlays · subscribe AOEExplosion.BlastInitialized
        └─ VesselCrystalEffects
              the hull's blast effect → ExplosionHelper.CreateExplosion
                └─ AOEExplosion.Initialize → BlastInitialized (isolated)
                      └─ SphereBlastCrystalMorph.OnBlastInitialized
                            filters: plain sphere · same vessel · centre within ringCaptureRadius of the collect
                            sphere at t = MorphSeconds → ConvexHullTarget (body frame), cast from the crystal
                            blast.HoldPhotonsForMorph(true) · ONE stamp
                └─ Detonate → the sweep starts (live, invisible)

  t = morphFraction · duration : HoldPhotonsForMorph(false), and the blast draws itself
  t = duration                 : body dissolved; morph destroyed (mesh destroyed with it)
```

The Manta's Kabloom also detonates every planted bomb. Those blasts go off where the bombs are, far
outside the 60 u capture radius, so the morph skips them. Its flower (`AOEFlowerCreation`) is a
subclass and is skipped by `IsPlainSphere`.

**The morph does not fire the blast, and must not.** If the blast never comes (an effect's anti-spam
cooldown swallows a second pickup, or the Manta's self-blast scale is 0), the morph holds the crystal
for `targetGraceSeconds`, then fades it out with a warning that names the sibling.

## 5. Files

| file | role |
|---|---|
| `R_VesselActions/SphereBlastCrystalMorph.cs` | the sphere target: listen, read the sphere at the hand-off, hold, reveal |
| `…/Vessel Crystal Effects/SphereBlastCrystalMorphByCrystalEffectSO.cs` + `_SO_Assets/…/SphereBlastCrystalMorphByCrystal.asset` | the retirement, wired in the Butterfly, Rhino and Manta containers |
| `Controller/Projectiles/AOEExplosion.cs` | `BlastInitialized` (isolated), `IsPlainSphere`, `BlastMesh`, `BlastLocalToWorldAt`, `TryGetBlastColours`, `HoldPhotonsForMorph` |
| `Utility/SphereBlastMorphGeometry.cs` | the growth curve and the cast origin, pure |
| `Tests/Editor/SphereBlastCrystalMorphGeometryTests.cs` | ten gates, run headlessly by `Tools/Build/crystal_morph_harness/` |

The `AOEExplosion` members are named apart from the Dolphin's `AOEConicExplosion.Initialized` /
`SetMorphStandIn` (PR #1680) on purpose: the same name on base and subclass would hide one with the
other. Once both land, the cone could move onto the base seam; that is a follow-up, not needed.

## 6. Tuning

Nothing new. The window is the fleet's (`Resources/CrystalMorphConfig`); the shape is the blast's.
`ringCaptureRadius` (60 u) doubles as the sphere's capture radius. Inspection: set `duration` to 9 on
the asset.

## 7. Verification

**Not opened in Unity** (no editor in the session that built it). Proven offline: the headless morph
harness (all suites), `unity_refcompile` on the changed files. See the PR for the numbers.

### In-editor verification

1. **Butterfly** in any mode with omni crystals (freestyle). Fly through a crystal. Expect: **no husk
   spray**; the cage bursts out onto the bloom sphere and the bloom carries on; ≈ 0.44 s total.
2. **Rhino**: same; the cage folds onto a small sphere just behind the hull that keeps swelling (the
   slow blast). The Surge still fires.
3. **Manta**: same on the Kabloom self-blast; planted bombs still detonate and bloom; the flower still
   grows.
4. **Prisms inside the blast break from the first frame**, while the plates are still flying.
5. **Two pickups inside the cooldown**: the second has no blast; expect that crystal to hold, then fade,
   with the named warning.
6. **Every other hull is unchanged.** Squirrel ring and Scarab forge still morph; Sparrow/Goby spray.
7. **MPPM, two clients**: the morph plays on both peers, each on its own blast.
8. Trace with **FrogletTools ▸ Toolbox ▸ Logging ▸ CrystalMorph**.

## 8. Known limitations / follow-ups

- **Shader seam** (§3), as on the Dolphin.
- **Rhino and Manta land on a small sphere.** If that reads as the crystal shrinking rather than
  becoming the blast, the fix is to land on the sphere later (a longer window for these two), which
  is a fleet-feel question for `CrystalMorphConfig`, not a per-hull tuning.
- **Blocked blast**: a super-shielded enemy prism stops the blast mid-morph and destroys it. The
  plates land where the sphere would have been and dissolve. Visual only.
