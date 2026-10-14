# Rhino Plough — the hull shoves prisms aside instead of destroying them

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
   path completes the shove. So the furrow is still a plough in open space, and a weapon against a
   wall beside it.

Prism contact is simulated per peer, so the plough runs per peer exactly as the ram did. The side
is read off each peer's own hull pose and agrees everywhere except for a prism struck within float
noise of the centreline.

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
| `throwSecondsOfSpeed` | 0.04 s | Extra sideways throw per unit of ram speed: 1.4 u at cruise (35), 3.6 u at 90 |
| `maxThrowDistance` | 30 u | Ceiling on that throw — reached from 750 u/s, so a ramp-boosted Rhino (~1,200 u/s) throws 30, not 48 |
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
   sides.

## Follow-ups

- A prism ploughed again while its first slide is still running restarts the slide from its
  previous REST point rather than from where its visual currently is (a small visible hop). Track
  the stamp if it shows.
- A ploughed prism lands where it lands — onto another prism or structure if one is there. If
  pile-ups read badly, probe `PrismSpatialIndex` for a free landing site.
- Flora and fauna body prisms are ploughed like any other mass; a fauna re-poses its body prisms
  next frame, so they snap back. Decide whether creatures should be immovable to the hull.
