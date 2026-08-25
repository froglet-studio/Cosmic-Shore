# Sparrow Skyburst Missile Bay — bay-open animation + bay-anchored launch

The Sparrow's model has always carried two missiles in a ventral bay (`b_Missile.L` /
`b_Missile.R` on the rig, with `b_Shell*` / `b_SlideChip*` bay doors), and a pair of
bay-open/launch animations was authored for it long ago — but none of it ever shipped on
trunk: the vessel ran the shared `MantaAnimationContoller`, the launch clips lived only on
the abandoned `claude/sparrow-missile-launch-animation-xHuCS` branch, and the skyburst
projectile rendered a stellated wedge polyhedron from `Vessel_Wedge_Scene (4).fbx`, spawned
from a `Gun Point` transform floating 9.9 u ahead of the vessel origin.

This change makes the skyburst fire **from the bay, as the bay animation ejects it, with the
missile model as the projectile**:

1. **Press** → ammo is deducted and `FireGunActionExecutor.OnMissileFired(bool usedRightBay)`
   fires immediately. `SparrowAnimationController` plays the matching bay clip on the additive
   **Missile Launching** animator layer (weight 1 for the clip, back to 0 when it completes).
2. **Bay opens** (~0.16 s at the authored 2.5× state speed), the animated bay missile ejects.
3. **Spawn** — after `SkyBurstGunAction.launchDelaySeconds` (0.2 s) the live projectile spawns
   at the **live bay bone's position** (aim/rotation from the gun, so flight matches course),
   carrying the extracted `Sparrow Missile.fbx` mesh. The animated missile returns into the
   closed bay (reads as the next round loading).

## Which bay is which (measured from the FBX takes, not assumed)

| Clip | Take length | Animates | Ejection window (raw take time) |
|---|---|---|---|
| `Missile Launch 1` | 0.88 s (played at 2.5×) | `b_Missile.R` — RIGHT bay | departs 0.4 s, peak 0.64 s |
| `Missile Launch 2` | 0.88 s (played at 2.5×) | `b_Missile.L` — LEFT bay | departs 0.4 s, peak 0.64 s |

Side selection lives in ONE place — `FireGunActionExecutor.Fire`:
`ammoBefore >= 2 × ammoCost` → right bay (first missile of a pair), else left. The animation
and the spawn both consume that event parameter, so they can never disagree.

## The donor-clip arrangement (why SparrowModel4.fbx is in the repo)

`SparrowModel1.fbx` (the shipped vessel model) has no launch takes. `SparrowModel4.fbx`
(imported from the old branch, guid `ebe998f747168104ca3e85b7295e47be`) carries the two
"Missile Launch" takes on a **bone-for-bone identical rig** (verified: identical bone list,
same `Sparrow_Armature+Mesh` root, and matching numeric scale — model1 imports at FBX-unit
100 × globalScale 1, model4 at FBX-unit 1 × globalScale 100, so curve translations land 1:1).
`SparrowAnimatorController.controller` references model4's clips by internalID
(`2132225776441424335` / `3552259950087936227`); Unity binds clip curves by transform path,
and every bone path resolves on model1's hierarchy. Curves targeting model4-only mesh nodes
(`Cube.113` blend-shape channels, `a_Body`) simply do not bind — and even a binding
blend-shape curve could not fight the element hull morphs, because `VesselAnimation.LateUpdate`
is the single writer of blend-shape weights (the documented defense).

**Model4 is an animation donor only. Do not wire it as a visible model** — its mesh object
names differ from model1's, so the prefab's `SkinnedMeshRenderer` references, the corridor
hull measurement, and `VesselCustomization` are all authored against model1.

## The missile grows as it travels (MASS)

The skyburst swells over its flight using the same machinery the Sparrow's bullets do — one
curve (`ElementalScaling.RoundGrowthFactorForLevel`, linear in the integer Mass level,
extrapolated across the whole `[-5, 15]` band) with its own authored endpoint pair on
`SkyBurstGunAction.asset`. **MASS owns the SUBSTANCE of what you fire**, so every round the
vessel launches grows with it; this is that one parameter reaching the third thing the Sparrow
fires, not a second Mass knob. CHARGE still owns the blast radius — different quantity,
different element.

**It swells EARLY and then holds.** `Projectile.flightGrowthCompleteAt01` is **0.2**: the
missile reaches full size in the first fifth of its flight — **0.6 s, about 70 u from the bay** —
and flies the remaining 80% at that size. That is the opposite shape from the bullets, which are
still growing when they arrive, and it is the point: a tracer's size reports how far it has come,
whereas a missile should read as a fixed object you are watching cross the arena. Growth is also
**uniform** (`flightGrowthUniform`), unlike the bullets' cross-section-only rule — that rule
exists because the tracer mesh is a 20-long dart whose length is a *streak*, and the missile is a
compact round whose length is the round.

Once the swell finishes the round stops re-writing its transform at all (`RoundGrowthRamp.IsComplete`
latches it), so holding for 80% of the flight costs nothing.

| Mass level | −5 | 0 (rest) | 5 | 10 | 15 |
|---|---|---|---|---|---|
| Growth factor | 14× | **20×** | 26× | **32×** | 38× |
| Missile length | 23.2 u | 33.2 u | 43.1 u | 53.1 u | 63.0 u |
| Missile girth | 5.3 u | 7.6 u | 9.9 u | 12.2 u | 14.5 u |
| Nose past the hit sphere, per end | 3.1 u | 8.1 u | 13.1 u | 18.0 u | 23.0 u |

To take Mass out of it and fly one fixed size, author both endpoints equal.

**It grows the MODEL, not the hit sphere.** `Projectile.flightGrowthTarget` points at
`MissileVisual`, so growth never touches the root — which carries the collider — and the
missile's reach is byte-identical to before any of this landed. Growing the root instead would
have multiplied an 8.5 u hit radius by up to 38 and silently rewritten a Dog Fight missile's reach
(a missile hit is 50 points).

### What the 20× size costs, stated plainly

At 5× the model arrived at roughly its own hit radius and the visual/hit relationship was honest.
At 20× it is not, and the mismatch is **asymmetric** — worth knowing which half is which:

- **Girth is still contained.** Even at full overcharge the missile is 14.5 u across against a
  17 u hit diameter, so a round crossing your view never looks wider than the volume that would
  have caught you. That is the read that matters for a near miss, and
  `SparrowRoundGrowthTests.TheMissileIsBroadsideContainedByItsHitSphere` holds it. The budget
  runs out at **~44.6×**; past there the round looks bigger than it hits in every direction,
  which is where the mismatch stops being an overhang and starts being a lie.
- **Length is not, and cannot be.** A 20× missile is 33 u nose to tail inside a 17 u hit sphere,
  so the nose reaches ~8 u past it (~23 u at Mass 15) and visually arrives a beat before the hit
  registers — at 120 u/s, roughly 0.07 s at rest and 0.19 s at overcharge. Accepted as the cost
  of the authored size.

If that beat ever reads as a missed hit rather than a big missile, the fix is the **hit sphere**,
not the growth — see the follow-up below. Shrinking the model back down would just undo the size
that was asked for.

## Files

| File | Role |
|---|---|
| `Assets/_Models/Sparrow Missile.fbx` | The missile pulled out of the Sparrow model (guid `98d8cb0114a1ad04e9682869849be719`, from the old branch — the skyburst projectile prefab references its mesh + embedded material) |
| `Assets/_Models/Vessel Models/SparrowModel4.fbx` | Animation donor: the two "Missile Launch" takes |
| `Assets/_Animations/SparrowAnimatorController.controller` | + additive layer **Missile Launching** (index 1, default weight 0) with states `Missile Launch 1` / `Missile Launch 2` at 2.5× speed |
| `_Scripts/Controller/Animation/SparrowAnimationController.cs` | Resurrected (was dead code; the prefab ran `MantaAnimationContoller`): identical puppetry + bay-layer driving off `OnMissileFired` |
| `_Scripts/Controller/Vessel/R_VesselActions/Executors/FireGunActionExecutor.cs` | `OnMissileFired(bool)` at press; bay-bone lazy resolution BY NAME (`b_Missile.R`/`.L`, warn + muzzle fallback); delayed bay-anchored spawn (UniTask, cancelled on disable/turn end/destroy) |
| `_Scripts/Controller/Vessel/R_VesselActions/Data Containers/FireGunActionSO.cs` | + `launchDelaySeconds` (0 = legacy instant muzzle spawn — FullAuto-class actions unaffected); + the MASS growth pair and `ResolveGrowthFactor` |
| `_Scripts/Controller/Vessel/ElementalScaling.cs` | `RoundGrowthFactorForLevel` / `RoundGrowthFactor` — the ONE in-flight growth curve, moved here off `FullAutoActionSO` so the bullets and the missile cannot drift apart |
| `_Scripts/Controller/Projectiles/Projectile.cs` | + `flightGrowthTarget` (empty = the root, i.e. every existing round unchanged), `flightGrowthUniform` and `flightGrowthCompleteAt01`; the launch pass rebases a child target off its authored scale so a pooled reissue cannot compound last flight's growth, and re-arms the settled latch |
| `_Scripts/Controller/Projectiles/RoundGrowthRamp.cs` | The growth SHAPE as a pure function — swell across the whole flight, or swell early and hold — plus the latch that lets a settled round stop writing its transform |
| `Assets/_SO_Assets/VesselActions/Sparrow/SkyBurstGunAction.asset` | `launchDelaySeconds: 0.2`; `growthFactorAtRestingMass: 20` / `growthFactorAtFullMass: 32` |
| `Assets/_Prefabs/Projectile/SkyBurstProjectile.prefab` | `Projectile.flightGrowthTarget` → `MissileVisual`, `flightGrowthUniform: 1`, `flightGrowthCompleteAt01: 0.2` (the model grows, early, and the hit sphere does not). Visual moved to a `MissileVisual` child: missile mesh + embedded material (+ 2× `BlueBaseOpaqueVesselMaterial` submeshes), rotated X+90° so the nose (+Y in mesh space, the radially-symmetric end) points along flight (+Z), child scale 2 (≈1.7 u world at ProjectileScale 10 — matches the bay missile's world size, armature scale 0.2034 × 8.3-unit mesh). Root scale/collider untouched → the gameplay hit sphere is byte-identical |
| `Assets/_Prefabs/Spacevessels/Sparrow.prefab` | Animation component swapped `MantaAnimationContoller` → `SparrowAnimationController` (same fileID, same serialized fields) + `missileExecutor` wired to the SkyBurst executor |

## Tuning knobs

| Knob | Where | Shipped | Meaning |
|---|---|---|---|
| `launchDelaySeconds` | `SkyBurstGunAction.asset` | 0.2 | Press → projectile spawn. The animated missile departs at 0.4 s ÷ 2.5 = 0.16 s; 0.2 lands the handoff just as it clears the hull |
| State speed | animator states `Missile Launch 1/2` | 2.5 | Whole bay cycle ≈ 0.35 s |
| Visual scale | `SkyBurstProjectile.prefab` → `MissileVisual.localScale` | 2 | World missile length ≈ 1.66 u at ProjectileScale 10 |
| Bay side predicate | `FireGunActionExecutor.Fire` | ammo ≥ 2×cost → right | Keep single-sourced; do not re-derive in the animation |
| `growthFactorAtRestingMass` / `growthFactorAtFullMass` | `SkyBurstGunAction.asset` | 20 / 32 | HOW MUCH the missile MODEL swells. Bounded above by the broadside budget (~44.6×) — see the section above before raising it |
| `flightGrowthCompleteAt01` | `SkyBurstProjectile.prefab` → `Projectile` | 0.2 | WHEN. Fraction of the flight the swell takes; it holds after. 1 = swell all the way in, the tracer's shape |
| Growth target / uniform | `SkyBurstProjectile.prefab` → `Projectile` | `MissileVisual` / on | WHAT grows. Moving the target to the root would grow the hit sphere with it (a Dog Fight balance change) |

## In-editor verification

See the 🔴 entry in `Docs/UNITY_VERIFICATION_CHECKLIST.md` (authored without a Unity
compile/play-test — the donor-clip path binding and the visual seam both need eyes).

## Follow-ups

- **Hit-sphere vs. visual mismatch (pre-existing, now INVERTED):** the skyburst's direct-hit
  sphere is world radius 8.5 (collider 0.85 × ProjectileScale 10). Per the fleet audit's "find
  the line that CHOSE it" rule that smells emergent, not authored (`0.85 × 10` arithmetic). It
  used to be far *larger* than the ~1.7 u model; at the authored 20× the grown model is now
  longer than it, by ~8 u per end at rest. The mismatch did not go away, it changed sign — and
  the sphere is the honest lever for it now, because the size is what was asked for. Resizing it
  is still a DogFight balance change (a missile hit is 50 points), still flagged for Garrett,
  still not silently retuned. A sphere of radius ~16.6 would contain the resting missile end to
  end.
- The root `ParticleSystem` exhaust was tuned against the 15 u wedge; it may need a size pass
  against the smaller missile.
- Remote peers: the bay animation rides the same executor event as the local projectile spawn,
  so it plays wherever the projectile spawns — if skyburst fire is ever server-relayed rather
  than locally simulated per client, the bay animation follows automatically.
