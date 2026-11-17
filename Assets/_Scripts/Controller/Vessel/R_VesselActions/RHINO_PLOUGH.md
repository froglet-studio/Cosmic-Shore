# Rhino Plough — the hull shoves prisms aside instead of destroying them

> **2026-11-07 (Garrett): the Rhino is ALL PLOUGH.** "split the rhino into two vessels: the rhino and a swordfish.
> the sword fish will be all about the sword. the rhino will be all about the plow." The sword, its energize, the
> afterimages and the Surge moved to the **Swordfish** (`SWORDFISH.md`); the Rhino keeps the plough and the ramp and
> gains the plough kit below (§ "The plough kit"): Space **Furrow** / L5 **Unstoppable**, Mass **Blade** / L5
> **Berm**, Charge **Carry** / L5 **Avalanche**, and the triggers **steer** the plough. Wherever the rest of this
> doc says "the sword" or "the blade cuts", read it as the Swordfish's.

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

## The plough kit (2026-11-07)

Garrett's answers, 2026-11-07: the plough kit with all three level-5s, and for the triggers "the steer is fun, but
pulling it should be a little shove in the direction of the trigger, holding it keeps the plough steered."

| Element | Ability | What it does | Level 5 |
|---|---|---|---|
| Space | **Furrow** | Adds 0 u at rest → 20 u at Space 10 to every throw, at any speed, on top of the speed throw (`furrowDistance`) | **Unstoppable**: super-shielded mass is ploughed too (`unstoppableUpgradeElement`) |
| Mass | **Blade** | A plough blade across the nose, 14 u wide at rest → 40 u at Mass 10, floor 10 (`RhinoPloughBlade.width`) | **Berm**: a prism that LANDS lands shielded (`bermUpgradeElement`) |
| Charge | **Carry** | A thrown prism still strikes a prism up to 0 u (rest) → 40 u (Charge 10) past its landing point; a strike destroys both (`carryDistance`) | **Avalanche**: after a strike the next prism down the line within the carry is thrown on too, up to 3 more (`avalancheUpgradeElement`, `avalancheLinks`) |
| Time | **Ramp Spool** | unchanged (`RHINO_RAMP_BOOST.md`) | **Momentum**, unchanged |

Every element read is the REPLICATED level (`ElementalFloat.EvaluateReplicated`) and every L5 is the replicated unlock
bit (`IsUpgradeActive`), so every peer ploughs alike. At rest Furrow and Carry are 0 and the blade is a little wider than
the hull, so a Rhino at rest ploughs almost exactly as it did before the kit (the blade adds 14 u of width at the nose).

### Steering (LT / RT)

`RhinoPloughSteerActionSO` (two assets, `RhinoPloughSteerLeftAction` / `RhinoPloughSteerRightAction`, bound on the
pad and touch overrides to `LeftStickAction` / `RightStickAction`, the inputs the sword swing used) and
`RhinoPloughSteerExecutor` (the swing executor's slot, same fileID):

- **Press**: a small sideways shove toward that trigger, `ModifyVelocity(right × ±90 u/s, 0.25 s)` — about 14 u of
  sidestep (tripled 2026-11-09), still under the fleet's 100 u/s displacement ceiling (vessel skill §4.ab2).
- **Hold**: every prism the hull ploughs is thrown to that side, across the centreline if it sat on the other one
  (`RhinoPlough.ShoveToward`). Both held: the last one pulled wins; letting it go hands back to the one still held.
- **Release**: each prism goes to the side it already sat on again.
- The press and release replicate (`R_VesselActionHandler`), so the steered side is the same on every peer.
- **Phone**: a thumb lift is that side's trigger (`author_touch_profiles.py`), so lifting a thumb shoves and holding
  it up keeps the steer. Nothing energizes any more, so both-thumbs-off does nothing special.
- **AI**: nothing presses the steer for a bot yet (vessel rule 52). Follow-up below.

### The Blade (Mass)

`RhinoPloughBlade` sits on a new `PloughBlade` GameObject under the root: a trigger `BoxCollider` (10 u tall, 2 u deep,
centred 7.5 u ahead of the root and 1.2 u down, level with the fuselage box) with an `ImpactCollider` routed to the
`VesselImpactor`, so `VesselImpactor.HullColliders` already includes it and the plough's clearance is measured off it:
a wider blade catches more mass and throws it clear of the whole blade. The width changes the box's local x only, when
the replicated Mass level moves. Side effect worth knowing: a rival's skimmer also sees the wider hull.

### The crystal skimmer

The sword's blade was the Rhino's only near-field skimmer, and an elemental crystal is only collected by an initialised
skimmer (vessel rule 11). So the blade's skimmer stays on the Rhino as `CrystalSkimmer`: re-posed to the hull's centre,
a static sphere (`elongateYOnly` off, `Scale` fixed at 30, so 15 u world radius) with a haptics-only container
(`RhinoSkimmerImpactorDataContainer`). It cuts nothing and takes no petals.

### Avalanche, exactly

On a strike (a thrown prism meets another and neither is shielded) both break, as always. With Avalanche, before the
pair breaks the plough sweeps the struck prism's box on along the throw line for the carry distance; the first prism
it meets (not super-shielded, unless Unstoppable) is then thrown the full carry along the same line, and its own strike
repeats the rule while links remain. A thrown-on prism that strikes nothing lands at the end of its carry. A shielded
pair bounces as before, and a bounce ends the chain.

## Files

| File | Role |
|---|---|
| `_Scripts/Controller/ImpactEffects/EffectsSO/Vessel Prism Effects/RhinoPloughPrismEffectSO.cs` | The effect, plus the pure `RhinoPlough` geometry (side, shove, support distances) |
| `_SO_Assets/Effects/Vessel Prism Effects/RhinoPloughPrismEffect.asset` | The tuned instance |
| `_SO_Assets/Effects/Effect Containers/VesselContainers/RhinoImpactorDataContainer.asset` | `vesselPrismEffects[1]`: `VesselDamagePrismEffect` → `RhinoPloughPrismEffect` |
| `_Scripts/Tests/Editor/RhinoPloughTests.cs` | Side choice, landing point, idempotence, support distances, disabled colliders; the steered shove, the furrow, the steer rule |
| `_Scripts/Controller/Vessel/R_VesselActions/Data Containers/RhinoPloughSteerActionSO.cs` | A trigger: the shove on press, the steer while held |
| `_Scripts/Controller/Vessel/R_VesselActions/Executors/RhinoPloughSteerExecutor.cs` | The held side (`SteeredSide`), per vessel |
| `_Scripts/Controller/Vessel/RhinoPloughBlade.cs` | The Mass blade's width |
| `_SO_Assets/VesselActions/Rhino/RhinoPloughSteerLeftAction.asset`, `...RightAction.asset` | The two triggers' numbers |
| `_SO_Assets/Effects/Effect Containers/SkimmerContainers/RhinoSkimmerImpactorDataContainer.asset` | The crystal skimmer's haptics-only container |
| `Tools/Build/author_swordfish_assets.py` | Authored the split; `--check` holds it |

## Tuning knobs (`RhinoPloughPrismEffect.asset`)

| Field | Shipped | Meaning |
|---|---|---|
| `clearanceMargin` | 1 u | Air between the hull's side and the prism's near face, on top of just-touching |
| `throwSecondsOfSpeed` | 0.12 s | Extra sideways throw per unit of ram speed (`min(maxThrowDistance, speed × 0.12)`): 6 u at cruise (50), 10.8 u at 90. Tripled from 0.04 on 2026-11-09 (Garrett) |
| `maxThrowDistance` | 90 u | Ceiling on that throw — reached from 750 u/s, so a ramp-boosted Rhino at top speed (840 u/s) throws 90, not 100.8. Tripled from 30 on 2026-11-09 |
| `slideSeconds` | 0.25 s | How long the visual takes to slide out. Animation only |
| `skipWhileAttached` | on | A riding vessel does not plough the trail it rides |
| `furrowDistance` | 0 → 60 u (Space), floor 0 | The Furrow: added to every throw. Tripled from 20 on 2026-11-09 |
| `unstoppableUpgradeElement` | Space | Unstoppable's gate (None = never) |
| `carryDistance` | 0 → 40 u (Charge), floor 0 | The Carry: how far past landing a thrown prism still strikes |
| `avalancheUpgradeElement` / `avalancheLinks` | Charge / 3 | Avalanche's gate and how many more prisms one chain throws |
| `bermUpgradeElement` | Mass | Berm's gate |
| `RhinoPloughBlade.width` (prefab) | 14 → 40 u (Mass), floor 10 | The Blade |
| `RhinoPloughSteer*Action.shoveSpeed` / `shoveSeconds` | 90 u/s / 0.25 s | The trigger's shove (tripled from 30 on 2026-11-09) |

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

**The plough kit (2026-11-07)** — not run in the editor either:

9. **Steer.** Hold RT and fly through a trail: every prism lands on the RIGHT, including those left of the centreline.
   A pull alone gives a small visible sidestep. Release: prisms go back to their own sides.
10. **Furrow.** Raise Space (the Toybox's element panel): the furrow is visibly wider at cruise.
11. **Blade.** Raise Mass: a wider band of a trail is caught at the nose. No new console warnings.
12. **Carry.** Raise Charge near dense mass: thrown prisms reach further to break others.
13. **L5s.** Unstoppable: a super-shielded prism is shoved rather than jiggled. Berm: landed prisms are shielded.
    Avalanche: one strike in a dense field breaks a line of prisms.
14. **Crystals.** Elemental and omni crystals still collect (the re-posed crystal skimmer), with no "pickup with no
    vessel" warning.

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

- **The plough kit is unplayed.** Every number above is a first guess: the furrow's 20, the carry's 40, the blade's
  14 → 40, the shove's 30 u/s. Tune in play.
- **The AI never steers** the plough and has no use for Avalanche or Berm (vessel rule 52): it ploughs and ramps.
- **The HUD's cards still draw the sword's icons**: the four plough abilities need their own (`/ability-icon`), and the
  Rhino has no omni-crystal ability (the Surge was the sword's).
- **Unstoppable ploughs arena linings and race tracks** (they are super-shielded), and the comeback system hands L5 to
  whoever is losing (vessel rule 31). Watch Headlong and Regatta. A ploughed super-shielded prism that strikes a
  plain one is destroyed with it (PrismMotion: the mover always breaks), so Unstoppable can break a lining, not only move it.
- **Broadside**: the plough-only Rhino has no vessel-hit verb, so the Swordfish took its seat (`broadside_balance.py`).

- A prism ploughed again while its first slide is still running restarts the slide from its
  previous REST point rather than from where its visual currently is (a small visible hop). Track
  the stamp if it shows.
- A ploughed prism lands where it lands — onto another prism or structure if one is there. If
  pile-ups read badly, probe `PrismSpatialIndex` for a free landing site.
- Flora and fauna body prisms are ploughed like any other mass; a fauna re-poses its body prisms
  next frame, so they snap back. Decide whether creatures should be immovable to the hull.
- **Per-peer landing divergence** near the clearance edge (above). If it shows in play, the fix
  shape is an owner-decided shove (or quantizing the throw), not a change to the side rule.

## 2026-11-09 tune (Garrett: "the movement of prisms from the plow should triple")

- The throw is not mass-based (it is geometry plus a speed term), so the push was tripled at its knobs:
  `throwSecondsOfSpeed` 0.04 → 0.12, `maxThrowDistance` 30 → 90, the Space furrow 0–20 → 0–60. The hull clearance
  is still measured from the colliders, so the prism always lands just clear plus three times the old throw.
- Trigger shove 30 → 90 u/s (still under the 100 u/s displacement ceiling).
- Camera (`RhinoCameraSettingsSO`): follow 70 → 90 u back, `lookAheadLift` 18 → 0 (no vertical offset).
- Impact feel (`CameraFeelConfig` Rhino weight 1.25 → 0.42): a third, the camera reading of "triple the mass",
  so ram shakes land lighter. The Rigidbody mass was left at 1 since nothing in the plough reads it.

## The 2026-11-12 rework (Garrett: "Mass has a space identity, charge has no pvp, no omni, etc. it breaks too many fundamentals")

Checked against the fleet convention (`Docs/ElementalAbilitySystem/ARCHITECTURE.md` §1: Space = reach/presence, Time =
rate/mobility, Charge = threat/energy output, Mass = physical size/volume), silence (P-vessel-11: Charge and Space are
the attacks), and the omni rule (P-ecology-16: every vessel morphs an omni crystal into its own effect).

| Element | Before | After | Level 5 |
|---|---|---|---|
| Charge | Carry (prism strike reach, no rival hit) | **Gore**: ramming a rival is a scored Strike that knocks petals loose (flat Strike price). Charge lowers the minimum ram speed, the Squirrel joust rule: 150 u/s at rest → 50 u/s at Charge 10 (`RhinoGoreSpeed`, a `JoustSpeedConfigSO`, asked through `IContactAdmission`; 2026-11-12) (`RhinoGoreBySkimmerEffectSO`, reported by `VesselCombatHitByGore`, only while faster than the victim, 1.4 s per victim). Carry stays as Charge's prism-side channel. | **Gore Steal** (petals land on the Rhino). Avalanche retired (`avalancheUpgradeElement` None) |
| Mass | Blade width (a reach, Space's identity) | **Heft**: a heavier plough throws further, 0 → 60 u (`furrowDistance`, now on Mass). LT / RT steer belongs to this card. | **Berm** (unchanged) |
| Space | Furrow (throw distance) | **Blade**: the blade's width, 14 → 40 u (`RhinoPloughBlade.width`, now on Space) | **Unstoppable** (unchanged; the fleet's Space shield-breaker slot, as on the Swordfish and Manta) |
| Time | Ramp Spool | unchanged (rate/mobility). Still shared with the Swordfish, which inherited it. | Momentum |
| Omni | the slow blast, shared with the Swordfish | **Stampede**: the omni crystal is absorbed (`VesselAbsorbCrystalMorphByCrystal`), for 0.1 s per prism ploughed since the last omni crystal (capped at 12 s; nothing ploughed, no stampede: Garrett 2026-11-12, "they can't surge from crystal to crystal and they get rewarded for prisms ploughed") the throttle surges by the crystal's speed buff, the Rhino is invulnerable (a `VesselTimedElementalWard` on the root warding everything: no drain, no theft, no scored hit, no silence), and every gore needs only the Charge 10 speed (50 u/s) and steals (`RhinoStampedeByCrystalEffectSO`, `RhinoStampede`) | - |

Netcode: the gore is decided on the Rhino's owner like every contact take; the stampede is set where omni pickups
resolve (server, replayed on the owner), so the owner knows it when it decides a gore. Nothing new is simulated per peer.

**Not yet seen in the editor.** Hand test: ram a slower rival above 150 u/s (petals come loose; at Charge 10 a 50 u/s cruise is enough; at Charge 5 they land on you); a
rival faster than you is not gored; plough prisms, then take an omni crystal (surge, invulnerable, every ram steals, 0.1 s per prism ploughed up to 12 s); take a second crystal straight away (nothing happens beyond the absorb); raise Space (wider
blade) and Mass (prisms thrown further). The HUD (2026-11-12): Rhino.prefab's overrides on its nested `RhinoHUDVariant` put `Rhino_Gore`, `Rhino_Heft` and
`Rhino_Blade` (`Tools/Build/icon_kit/specs/rhino.py`) on the Charge, Mass and Space cards and switch the Swordfish's
sword parts off; Time keeps the Ramp Spool card. The Charge plate is the gore-speed gauge (`RhinoHUDController.PushGore`:
ram speed over the live minimum, full and lit when a ram counts, a flash when a Stampede starts). The omni Stampede has
no card (the row is four).
