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
**uniform** (`flightGrowthUniform`), unlike the cross-section-only rule the hit-volume path uses — that rule
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

**The model IS the hit volume — the other way of satisfying the same law.** Since PR #786 the
platform rule is *MASS in-flight growth is a HIT VOLUME, not a size*: a round's model stays the
size it left the muzzle and a see-through `chargeField` shell, sized every frame to the swept hit
radius, is what carries the read. That is right for the bullets, whose model is a 20-long tracer
**streak** — a smear, not a body; growing it drew a cannonball.

The skyburst has a readable **body** worth growing, and no growing hit volume for a shell to draw.
So it satisfies the same law from the other end: **the model grows and the sphere collider is
fitted to it, every frame.** `Projectile.flightGrowthTarget` selects that path, and
`ModelHitRadius` / `ModelHitCentre` do the fit:

- **Radius = the model at its WIDEST across the flight axis.** For a body of revolution that is
  its radius — never the bounding-box *diagonal*, which would overstate a round missile by √2.
- **The sphere's FRONT surface sits exactly on the model's tip.** That is the contract, and the
  reason the tail is free to trail: a model may stick out the **back** of its collider (a tail
  that has already passed you cannot cause a false read) but never out the **front**, where the
  nose would visibly reach a target before the hit registered.

Both are linear in growth, because the model scales about the root origin — so the fit is one
multiply, and the settle latch stops it entirely once the size is final.

| Mass level | −5 (14×) | **0 (20×)** | 5 (26×) | 10 (32×) | 15 (38×) |
|---|---|---|---|---|---|
| Hit radius | 2.67 u | **3.81 u** | 4.95 u | 6.10 u | 7.24 u |
| Model length | 23.2 u | 33.2 u | 43.1 u | 53.1 u | 63.0 u |
| Tail behind the sphere | 17.9 u | 25.6 u | 33.2 u | 40.9 u | 48.6 u |

Nothing hardcodes the missile: the fit is measured per flight from the growth target's own
renderer bounds through the renderer→root matrix, so it is correct for any nesting, rotation or
child scale.

### What this costs, stated plainly

**The missile's reach dropped.** It used to hit with a fixed **8.5 u** sphere; at resting Mass it
now hits with **3.81 u** — 45%. That is a deliberate Dog Fight balance change (a missile hit is 50
points), made because the 8.5 was never authored: it is `0.85 × ProjectileScale 10` arithmetic
that happened to dwarf the 1.7 u model nobody could see. `SparrowRoundGrowthTests` pins 3.81 so a
future change has to argue with the number rather than drift past it.

Three consequences worth knowing:

- **Reach now varies with MASS**, from 2.67 u to 7.24 u. It did not before. Mass buying reach is
  consistent with *MASS owns the substance of what you fire*, but it is new for this weapon.
- **Reach is small for the first fifth of the flight** (0.19 u at the muzzle, rising to full at
  20%). The missile is leaving the bay through that window, so it will not detonate on something
  it brushes past the hull — arguably a feature, but it is a change.
- **The forward overhang is gone**; what remains is up to 48 u of tail behind the sphere, which is
  the permitted direction.

## It has a TAIL, and that word is precise

The missile carries the fleet's **tail** — the long streak whose whole job is legibility at range
(`Docs/VESSEL_TAIL_AND_JETS.md` §4.2). Not a *jet* (a short plume on an engine node, tuned for the
pilot flying that engine) and not a *trail* (conserved prism mass, which this is emphatically not:
the tail carries no collider, no state, and destroying it destroys nothing). It is the first
non-vessel in the game to have one, and it earns it — the round crosses ~360 u in three seconds and
a hit is worth 50 points in Dog Fight, so everyone in the arena has a reason to see it coming and to
know whose it is.

It is the **shared `VesselTail.prefab`**, nested — not a copy — so a retune of the tail's look
reaches the missile with no second edit. What is missile-specific is decided in code, per flight,
off the measurement `CaptureModelHitSphere` already takes for the hit sphere:

| | value | where |
|---|---|---|
| mount | the model's measured **rear face**, scaled by growth (the mirror of the nose fit) | `Projectile.TailMount` |
| width | `0.4 × the round's own body diameter` — 3.05 u at resting Mass | `Projectile.TailWidth` |
| colour | the firing pilot's **domain**, re-read per flight | `Projectile.PaintTail` → `TailGradient` |
| lifetime | the shared prefab's 4 s | `VesselTail.prefab` |

Deriving the width is not optional: the round swells **14×–38×** with MASS and a `TrailRenderer`'s
width is world-space, so a fixed ribbon would be a thread behind a 63 u missile. Because both
numbers come out of the same measurement the collider is fitted to, the tail and the hit volume can
never disagree about how big the round is.

Two things a pooled round needs that a vessel does not, both in `Projectile`:

- **`ReclaimTail`** clears the ribbon at launch. A `TrailRenderer` records world-space points, so
  without it every reissue draws one straight line from wherever the last missile detonated to this
  one's bay.
- **`ReleaseTailToFade`** cuts the tail loose at retirement, 0.025 s after detonation, so several
  hundred units of ribbon fade out where they were laid instead of blinking out with the round.
  That is continuity of existence, not polish. It comes home when the fade ends or when the round
  is fired again, whichever is first — a 20-deep pool cycles faster than a 4 s ribbon.

`SparrowRoundGrowthTests` pins the mount sign, the rear-face fit, the width curve and the 0.4.

## Files

| File | Role |
|---|---|
| `Assets/_Models/Sparrow Missile.fbx` | The missile pulled out of the Sparrow model (guid `98d8cb0114a1ad04e9682869849be719`, from the old branch — the skyburst projectile prefab references its mesh + embedded material). **Subdivided 2 Catmull-Clark levels in place** (624 → 9,984 tris) so it holds up at 20x; same guid, same mesh fileID, same bounding box |
| `Tools/Build/subdivide_sparrow_missile.py` | The subdivision, with `--check` proving the shipped mesh's invariants (all-quad, closed, material split, authored bounds, unit normals) |
| `Tools/Build/fbx_binary.py` | Round-trip codec for binary FBX 7.x — lets a tool edit an artist-authored file and write it back without a modelling package |
| `Assets/_Models/Vessel Models/SparrowModel4.fbx` | Animation donor: the two "Missile Launch" takes |
| `Assets/_Animations/SparrowAnimatorController.controller` | + additive layer **Missile Launching** (index 1, default weight 0) with states `Missile Launch 1` / `Missile Launch 2` at 2.5× speed |
| `_Scripts/Controller/Animation/SparrowAnimationController.cs` | Resurrected (was dead code; the prefab ran `MantaAnimationContoller`): identical puppetry + bay-layer driving off `OnMissileFired` |
| `_Scripts/Controller/Vessel/R_VesselActions/Executors/FireGunActionExecutor.cs` | `OnMissileFired(bool)` at press; bay-bone lazy resolution BY NAME (`b_Missile.R`/`.L`, warn + muzzle fallback); delayed bay-anchored spawn (UniTask, cancelled on disable/turn end/destroy) |
| `_Scripts/Controller/Vessel/R_VesselActions/Data Containers/FireGunActionSO.cs` | + `launchDelaySeconds` (0 = legacy instant muzzle spawn — FullAuto-class actions unaffected); + the MASS growth pair and `ResolveGrowthFactor` |
| `_Scripts/Controller/Vessel/ElementalScaling.cs` | `RoundGrowthFactorForLevel` / `RoundGrowthFactor` — the ONE in-flight growth curve, moved here off `FullAutoActionSO` so the bullets and the missile cannot drift apart |
| `_Scripts/Controller/Projectiles/Projectile.cs` | + the TAIL (`tail`, `tailWidthPerBodyDiameter`, `TailMount`/`TailWidth`, and the pooled-round reclaim/release pair); + `flightGrowthTarget` (empty = the root, i.e. every existing round unchanged), `flightGrowthUniform` and `flightGrowthCompleteAt01`; the launch pass rebases a child target off its authored scale so a pooled reissue cannot compound last flight's growth, and re-arms the settled latch |
| `_Scripts/Controller/Projectiles/RoundGrowthRamp.cs` | The growth SHAPE as a pure function — swell across the whole flight, or swell early and hold — plus the latch that lets a settled round stop writing its transform |
| `Assets/_SO_Assets/VesselActions/Sparrow/SkyBurstGunAction.asset` | `launchDelaySeconds: 0.2`; `growthFactorAtRestingMass: 20` / `growthFactorAtFullMass: 32` |
| `Assets/_Prefabs/Spacevessels/Components/VesselTail.prefab` | The shared tail, nested here. Also stripped of six dead disabled particle systems in the same pass — they were free on a vessel and would not have been on a 20-deep projectile pool (`Docs/VESSEL_TAIL_AND_JETS.md` §6) |
| `_Scripts/Controller/Vessel/TailGradient.cs` | The one composition of a tail's colour gradient, shared by `VesselTailAndJets` and `Projectile` so the two cannot drift |
| `Assets/_Prefabs/Projectile/SkyBurstProjectile.prefab` | `Projectile.flightGrowthTarget` → `MissileVisual`, `flightGrowthUniform: 1`, `flightGrowthCompleteAt01: 0.2`; `SphereCollider` re-authored to the **launch** fit (`r 0.019053`, centred on the model's tip) and re-fitted per frame while the model swells. Visual moved to a `MissileVisual` child: missile mesh + embedded material (+ 2× `BlueBaseOpaqueVesselMaterial` submeshes), rotated X+90° so the nose (+Y in mesh space, the radially-symmetric end) points along flight (+Z), child scale 2 (≈1.7 u world at ProjectileScale 10 — matches the bay missile's world size, armature scale 0.2034 × 8.3-unit mesh). Root scale/collider untouched → the gameplay hit sphere is byte-identical |
| `Assets/_Prefabs/Spacevessels/Sparrow.prefab` | Animation component swapped `MantaAnimationContoller` → `SparrowAnimationController` (same fileID, same serialized fields) + `missileExecutor` wired to the SkyBurst executor |

## Tuning knobs

| Knob | Where | Shipped | Meaning |
|---|---|---|---|
| `launchDelaySeconds` | `SkyBurstGunAction.asset` | 0.2 | Press → projectile spawn. The animated missile departs at 0.4 s ÷ 2.5 = 0.16 s; 0.2 lands the handoff just as it clears the hull |
| State speed | animator states `Missile Launch 1/2` | 2.5 | Whole bay cycle ≈ 0.35 s |
| Visual scale | `SkyBurstProjectile.prefab` → `MissileVisual.localScale` | 2 | World missile length ≈ 1.66 u at ProjectileScale 10 |
| Mesh resolution | `subdivide_sparrow_missile.py --levels` | 2 | Catmull-Clark steps. Each step is 4x the triangles; drop to 1 (2,496 tris, 16-sided) if 9,984 is too heavy |
| Bay side predicate | `FireGunActionExecutor.Fire` | ammo ≥ 2×cost → right | Keep single-sourced; do not re-derive in the animation |
| `growthFactorAtRestingMass` / `growthFactorAtFullMass` | `SkyBurstGunAction.asset` | 20 / 32 | HOW MUCH the missile swells — and, since the collider follows the model, its REACH too (3.81 u at rest). There is no size ceiling any more; there is a balance consequence. See the section above before raising it |
| `flightGrowthCompleteAt01` | `SkyBurstProjectile.prefab` → `Projectile` | 0.2 | WHEN. Fraction of the flight the swell takes; it holds after. 1 = swell all the way in, the tracer's shape |
| `tailWidthPerBodyDiameter` | `SkyBurstProjectile.prefab` → `Projectile` | 0.4 | The tail's ribbon width as a fraction of the round's own body diameter (3.05 u at resting Mass). 0 hides the tail. Derived from the Sparrow's own `widthScale` 2.5 on a ~6.4 u hull, not play-tested |
| Growth target / uniform | `SkyBurstProjectile.prefab` → `Projectile` | `MissileVisual` / on | Selects the model-IS-the-hit-volume path: the model grows and the collider is fitted to it. The only prefab in the game that sets it. Clearing it puts the missile on the shell path, where it would not grow at all — it has no `chargeField` |

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
- The root `ParticleSystem` exhaust was tuned against the 15 u wedge and never re-tuned. It sits
  on the ROOT, so it does not grow with the model — against a 33 u missile it is the most likely
  thing to read as wrong. Needs a size pass. **The tail sharpens this**: it is now the only thing
  streaming off this round that is neither measured off the model nor domain-coloured, so if the
  stern reads as two unrelated effects, that is the one to fix (or delete — the tail may simply
  have made it redundant).
- Remote peers: the bay animation rides the same executor event as the local projectile spawn,
  so it plays wherever the projectile spawns — if skyburst fire is ever server-relayed rather
  than locally simulated per client, the bay animation follows automatically.
