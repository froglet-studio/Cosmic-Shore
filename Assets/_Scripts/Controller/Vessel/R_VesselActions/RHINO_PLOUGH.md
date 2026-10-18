# Rhino Plough — the hull shoves prisms aside instead of destroying them

> **2026-10-16:** no plough code changed. The throw figures are restated at the live cruise
> (50 u/s) and top (840 u/s); the peer-agreement claim is corrected (peers agree on the side, not
> always on the landing point); the hull ignoring mass is now a recorded design statement (the
> Time row), and the sword's new Mass afterimages are ploughed like any prism.

Garrett, 2026-10-13: *"the rhino should not destroy prisms it collides with. those should be pushed
to either side like the rhino is a plough."*

The Rhino's **hull** no longer destroys prisms. A prism it rams is shoved sideways — to whichever
side of the hull's centreline it already sat on — far enough to clear the hull, plus a throw that
grows with ram speed. It survives with its domain, shield state and trail membership unchanged.
**The sword is untouched**: cutting is the blade's job (`RHINO_ENERGY_SWORD.md`), and the hull now
only clears a furrow. Every other vessel still rams as before — the change is one entry in the
Rhino's own impactor container.

## How it works

`RhinoImpactorDataContainer.vesselPrismEffects` listed the fleet-shared `VesselDamagePrismEffect`
(the ram). That slot now holds `RhinoPloughPrismEffect`; the haptics and danger-prism debuff
entries beside it are unchanged, so a danger prism still stings when ploughed.

On each hull contact `RhinoPloughPrismEffectSO.Execute`:

1. Skips while the Rhino is riding a trail (`skipWhileAttached`, the same guard the ram has).
2. **Super-shielded mass does not move.** `Prism.AbsorbSuperShieldHit(speed)` plays the deflection
   jiggle — exactly what the ram's `Prism.Damage` did with it — and returns. This is the immovable
   tier (Astro League's edge lining among it).
3. Measures, along the hull's **right** axis only (a plough throws sideways, never up or down):
   - the prism's lateral offset from the hull centre → its **side** (dead centre goes right);
   - the hull's reach on that side — the furthest support point over `VesselImpactor.HullColliders`
     (oriented-box exact for `BoxCollider`s, world AABB for anything else, which errs wide);
   - the prism's half-width along that axis — its box, or its octahedral shell when **shielded**
     (box half-extents × `OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE`), since the shell is what
     the hull touches.
4. Moves the prism so its centre lands at `hullReach + prismHalf + clearanceMargin + throw` on its
   own side, where `throw = min(maxThrowDistance, speed × throwSecondsOfSpeed)`. A prism already
   that far out is not moved, which makes the effect **idempotent**: a multi-collider hull (or the
   shell tier and box tier both) reporting one ram twice does not double the throw.
5. Follows the live-prism mover contract (`ThreatGrove.PoseAndFly`, `BuilderPrismWorld.Settle`):
   the transform — collider, spatial index, shell — is final the same frame
   (`Prism.NotifyPositionChanged`), and the visual slides out from where it was on the prism
   clock's flight stamp (`PrismRenderService.StampFlight`, Docs/PRISM_ANIMATION.md §5 C5) with its
   render bounds widened to cover the start. No per-frame CPU work during the slide.

6. **A shoved prism is a MOVING prism** (2026-10-14, `Docs/PRISM_MOTION.md`). Before the shove is
   committed, `PrismMotion.TrySweep` asks for the first prism its path runs into — box against box,
   exact (one prism per ram is always the few-movers case). If there is one,
   the shoved prism stops at the contact point and **both are destroyed** (devastating, scored to the
   Rhino's pilot; a super-shielded target deflects and only the shoved prism goes). Only a clear
   path completes the shove. A plough moves one prism per ram, so it is always LEVEL 1 of prism
   movement's ladder: both prisms come apart as a COLLISION BURST on the ultra-high-res prism
   (`Docs/PRISM_ANIMATION.md` §4.12) while the burst budget allows, the ordinary explosion otherwise. So the furrow is still a plough in open space, and a weapon against a
   wall beside it.

Prism contact is simulated per peer, so the plough runs per peer exactly as the ram did. The
**side** is read off each peer's own hull pose and agrees everywhere except for a prism struck
within float noise of the centreline. The **landing point does not always agree**: it is computed
from each peer's own hull pose (the clearance) and its own `VesselStatus.Speed` (the throw), and
a replica's pose and speed lag the owner's. A prism that sits near the clearance edge can be left
alone on one peer (already clear, so not moved) and shoved on another, and a shoved prism lands
at a slightly different distance where the peers' speeds differ. *(This paragraph used to claim
the plough agrees everywhere but the centreline — that is true of the side only.)*

**The hull ignores mass — by design (recorded 2026-10-16).** The Rhino carries no
`VesselChangeSpeedByPrismEffect`, so ploughing costs no speed; the Time row's description
("Ramp Spool", `RHINO_RAMP_BOOST.md`) now states this as the bulldozer identity rather than
leaving it to read as a gap.

**Its own afterimages.** The sword's Mass afterimages (and, at Mass L5, Danger Afterimages) are
ordinary prisms to the hull: once the fleet-wide ~1 s self-trail grace (`SelfTrailContactConfigSO`)
has passed, the hull ploughs them aside like any other mass. A Danger Afterimage ploughed that way
also runs the container's danger entry — own-domain danger, so the temporary debuff, not the
permanent burn (`VesselElementalDebuffByDangerPrismEffectSO`) — unless Time L5 Momentum is warding
danger prisms above 588 u/s. The plough is unchanged by the 2026-10-17 call that lets the Rhino's
own BLADE cut its own afterimages past the same 1 s grace (`RHINO_SWORD_AFTERIMAGE.md`): the blade
cuts them, the hull ploughs them.

## Files

| File | Role |
|---|---|
| `_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Prism Effects/RhinoPloughPrismEffectSO.cs` | The effect, plus the pure `RhinoPlough` geometry (side, shove, support distances) |
| `_SO_Assets/Effects/Vessel Prism Effects/RhinoPloughPrismEffect.asset` | The tuned instance |
| `_SO_Assets/Effects/Effect Containers/VesselContainers/RhinoImpactorDataContainer.asset` | `vesselPrismEffects[1]`: `VesselDamagePrismEffect` → `RhinoPloughPrismEffect` |
| `_Scripts/Tests/Editor/RhinoPloughTests.cs` | Side choice, landing point, idempotence, support distances, disabled colliders |

## Tuning knobs (`RhinoPloughPrismEffect.asset`)

| Field | Shipped | Meaning |
|---|---|---|
| `clearanceMargin` | 1 u | Air between the hull's side and the prism's near face, on top of just-touching |
| `throwSecondsOfSpeed` | 0.04 s | Extra sideways throw per unit of ram speed (`min(maxThrowDistance, speed × 0.04)`): 2.0 u at cruise (50), 3.6 u at 90 |
| `maxThrowDistance` | 30 u | Ceiling on that throw — reached from 750 u/s, so a ramp-boosted Rhino at top speed (840 u/s) throws 30, not 33.6 |
| `slideSeconds` | 0.25 s | How long the visual takes to slide out. Animation only |
| `skipWhileAttached` | on | A riding vessel does not plough the trail it rides |

## What this changes elsewhere

- **Cleave** (Rhino-only, scored on `HostilePrismsDestroyed`): a hull ram no longer scores; only the
  sword does. The sword always cuts on contact and sits ahead of the hull, so the AI cage-breakers'
  transits should still cut — confirm in play (verification step 4).
- **`VesselTelemetry`** counts hull kills off `VesselDamagePrismEffectSO.OnVesselDamagedPrism`; a
  Rhino hull now raises none, which is accurate.
- **`RHINO_SHIELD_SWIPE.md`**'s "parked sword == hull" debris parity no longer applies to the Rhino:
  its hull makes no debris.
- **The Rhino's Space-5, Breaker**, was proposed as "ramming destroys shielded prisms". With no
  destroying ram it was re-cut (Garrett, 2026-10-13) onto the sword: a shielded prism takes one
  slice instead of two (`RHINO_ENERGY_SWORD.md`, `FLEET_MAPS.md`).

## In-editor verification

No `unity` binary was available in the authoring session, so none of this has been seen in play.

1. **Plough, not destroy.** Any scene with trails (freestyle / Menu lava lamp). Fly the Rhino
   straight through another pilot's trail with the sword parked out of the way (or through a
   trail the blade misses). Every prism the HULL touches must slide out to the side it was on and
   survive — no debris, no destruction SFX from the prism. It must not still be touching the hull.
2. **Faster throws wider.** Repeat at cruise and on a ramp boost: the boosted furrow is visibly
   wider (≤ 30 u beyond the hull).
3. **Shielded / super-shielded.** A shielded prism is ploughed with its shield intact (and clears
   the shell, not just the box). A super-shielded prism does not move — it jiggles.
4. **Cleave.** Play Cleave against AI: confirm the AI still makes progress on the destruction
   target (its cuts come from the sword); confirm hull contacts no longer tick the score.
5. **Danger prisms** still apply the elemental debuff when ploughed.
6. **No snap-back / flicker.** A ploughed prism stays where it landed; the slide is smooth and does
   not frustum-pop at its start.
7. **MPPM two clients.** One Rhino ploughs a trail; both clients see the prisms land on the same
   sides. Expect small differences in landing DISTANCE (each peer uses its own hull pose and
   speed), and a prism grazed at the clearance edge may move on one client and not the other —
   note how often that reads in play.
8. **Afterimages.** Energize, let it go out, and fly back through the afterimages the sword
   laid: after the ~1 s grace the hull ploughs them aside (danger ones sting unless Momentum is
   up).

## Follow-ups

- A prism ploughed again while its first slide is still running restarts the slide from its
  previous REST point rather than from where its visual currently is (a small visible hop). Track
  the stamp if it shows.
- A ploughed prism lands where it lands — onto another prism or structure if one is there. If
  pile-ups read badly, probe `PrismSpatialIndex` for a free landing site.
- Flora and fauna body prisms are ploughed like any other mass; a fauna re-poses its body prisms
  next frame, so they snap back. Decide whether creatures should be immovable to the hull.
- **Per-peer landing divergence** near the clearance edge (above). If it shows in play, the fix
  shape is an owner-decided shove (or quantizing the throw), not a change to the side rule.
