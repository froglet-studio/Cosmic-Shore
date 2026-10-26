# Rhino Energy Sword — ungated blade, the energize ritual, energy meter, the omni-crystal surge

> **2026-10-26 (Garrett), third round: the two L5s are the sword's two keys.** "Have the sword only
> destroy supershielded prisms as the space L5 upgrade. Have the sword only steal as the charge L5
> upgrade." So: **Space L5 = Shield Breaker** — the sword can pop super-shielded prisms at all
> (`RhinoSkimmerDamagePrismEffectSO.superShieldUpgradeElement`), still with an energized blade;
> without it the blade binds, energized or not. **Charge L5 = Sword Steal** — a sword hit on a
> rival vessel pockets the petals (`RhinoSwordStealBySkimmerEffect.stealUpgradeElement`); below it
> the same petals are EJECTED as crystals at the strike, so the hit still scores and conserves.
> The old L5s are deleted: **Breaker** (shielded prism in one slice) and **Live Wire** (a return to
> the stance re-lights the tail). The Charge ability is renamed **Energize** (the energize, plus
> Charge's scaling of how many petals a hit takes).

> **2026-10-26 (Garrett), second round: NO ENERGY; Charge steals; Space alone sets the length.**
> "Omnicrystal doesn't need the element buff, lets discard the whole notion of energy as a resource
> on the rhino. Charge should control the amount of crystals stolen from vessels with the sword.
> Space alone can grow the sword, but move the camera accordingly." So: the energy meter, the energy
> banked per cut and the energize cost are gone (the Rhino's `Shield` resource is deleted from its
> `ResourceSystem`; `RhinoSwordNetworkRelay` lost `n_energy`, protocol version 15); the energize
> costs nothing and its cooldown is its only limit; **Charge** scales the sword's petal STEAL
> (`RhinoSwordStealBySkimmerEffect.stealScale`, ×1 → ×2.5, the Squirrel joust's numbers on the same
> verb); **Space alone** sets the blade's length (24 → 40), eased, with the local pilot's camera
> pulled back in proportion (§ "Length: Space alone"); and the omni-crystal **Surge** lends no
> elements and fills nothing — it is the 8 s energized sword ring with the camera pulled back
> (§ "The omni-crystal Surge").
>
> **2026-10-26, first round:** "halt all resizing on the rhino sword" (the energy length and the
> surge burst went; Space came back as the one source in the second round), and the Surge became
> "zoom out, re center the camera on the rhino and surround it with swords. they should still be
> controlled by the triggers. this can an 8 second state".

> **2026-10-16:** energy, energize phase and surge are now OWNER-authoritative and replicated by
> `RhinoSwordNetworkRelay`; every peer poses the blade from the replicated trigger mirrors. Energy
> per cut is Charge-scaled (×1 → ×2, floor ×0.5); a let-go tail now always runs out unless Charge
> L5 **Live Wire** is active. The per-petal crystal burst is DELETED — the burst look now belongs
> to the omni-crystal **Surge**. Ignition re-dispatch replays super-shielded contacts only.

Layers the **energize ritual** (the supershield key), the **Space-driven length**, and a full authored
**FX pass** onto the Rhino's swordsmanship (the pose/analog-swipe rig is in
`RHINO_SHIELD_SWIPE.md` — read it first). Ordinary cutting is **ungated**: no stance, no
cooldown — the sword always damages prisms on contact. The ONE gated act is popping a
SUPER-shielded prism, which requires the **Space level-5 upgrade** and the blade to be **ENERGIZED**.

**Design history (do not relitigate):** a first iteration gated ALL damage behind an energize
stance + a slash cooldown; that shipped a sword that mostly didn't cut and was **rejected — never
reintroduce a slash cooldown or stance gate on ordinary cutting.** A second iteration removed the
ritual entirely (always-pops, ungated supershield). The user's ruling for v3: the base sword
stays ungated for normal and shielded prisms, and **energize returns as the supershield key
only** — the rejection covers NORMAL damage gating, not the hardened-target ritual.

## What the sword does on contact

| Target | Blade state | Result |
|---|---|---|
| Normal prism | any | **SLICED** — cut along the plane the blade swept through it; the halves part, open and dissolve from the cut face (§ "The slice"). Falls back to the ordinary explosion, debris thrown at the **contact velocity** (below), whenever the slice cannot be drawn |
| Shielded prism | any | Shield pops, prism survives (standard `Prism.Damage` semantics) |
| **Super-shielded prism** | **ENERGIZED**, **Space L5** active | **POPPED**: `DeactivateShields()` (stellation shatter + SFX) then `Slice(devastate: true)` (the armour shatters, the core is CUT; unrestorable) — the sanctioned mass-conserving teardown, same sequence as `AstroLeagueArena.ClearEdgeLining`. `Prism.Damage` alone hard-ignores super-shielded prisms, which is why the shields must drop first. |
| **Super-shielded prism** | not energized, or no Space L5 | **BINDS** — no recoil, no spin. The blade sticks in the armour: a dim **denied spark**, the prism **jiggles**, a **punish thud**, and while the blade stays inside the vessel's **rotation rate is dragged down** and the pilot feels a **grind** (§ "Binding"). The prism survives. |
| One of this sword's own **afterimages** | any | **SLICED like any prism** once the fleet's 1 s self-trail grace is past, banking 0.04 × Charge each (Garrett, 2026-10-17: *"not immune to our destruction"*; it was never cut before). `IRhinoSwordState.IsOwnAfterimage` survives only for the AI's aim. Watch item: one energize plus self-cuts can refill the meter — the lever is energy-per-own-afterimage, not immunity (`RHINO_SWORD_AFTERIMAGE.md` §3) |
| Another **vessel** | any | The sword takes **petals**: `RhinoSwordStealBySkimmerEffect` (a `VesselOvertakeBySkimmerEffectSO`) sits in the sword's container and runs only for a hit `VesselCombatHitBySword` admits (PvP is petals only). With the **Charge L5** upgrade it STEALS them (they land on the Rhino); below it they are EJECTED as crystals at the strike (`stealUpgradeElement`, 2026-10-26) |

`popRequiresEnergizedBlade` (default **on** — the ritual IS the design) replaces v2's
`destroySuperShielded`: flip it off on the asset to restore the v2 ungated pop as a designer
A/B. With no sword state present (a non-Rhino skimmer reusing the asset) the blade can never be
energized, so super-shielded prisms always bind (the entry beat plays; the drag needs the
Rhino's driver, so a non-Rhino skimmer gets the jiggle and the thud and nothing else).

## Binding (a NON-energized blade inside super-shielded mass)

Shipped 2026-09-25, replacing the `BounceBack` recoil. The recoil threw the ship back along its
course and spun it (`ModifyVelocity` + `GentleSpinShip`), so a slash that grazed armour cost the
pilot their LINE — the one thing a Rhino is built to hold. It now answers the way a Squirrel's hull
answers armour — the mass deflects (the jiggle), the pilot feels it — and adds the sword-specific
half: the blade is IN something, so it drags.

| Beat | Who | What |
|---|---|---|
| Entry | `RhinoSkimmerDamagePrismEffectSO` (fires once per contact entry) | denied spark · `Prism.AbsorbSuperShieldHit(contactSpeed)` (the deflection jiggle, sized by the speed the blade struck at) · `HapticController.PlayPunish()` |
| While inside | `ShieldSkimmerScaleDriver.UpdateBind` (per frame) | rotation rate eases to `bindTurnRateMultiplier` (0.3) · every bound prism re-jiggles each `bindJiggleIntervalSeconds` (0.2 s) · `HapticController.PlayBind` grind each `bindHapticIntervalSeconds` (0.1 s) |
| Exit | the same driver | rotation rate eases back to 1 at `bindReleaseRate` — the armour letting go, not a snap |

Four things that make it correct rather than approximately correct:

- **"Inside" is the shell tier's live pair set, not a second copy.** The shell tier
  (`PrismShellContactManager`) dispatches a pair ONCE on entry, so the driver reads
  `CollectSuperShieldedContactsForOwner(skimmerImpactor, …)` every frame — the exact set its exit
  sweep maintains, against the exact stellated shape (a blade threaded between spikes is not
  bound). A private enter/exit ledger would need its own exit test and could disagree with this
  one. Under the A/B `ForceLegacyBoxInteraction` there is no pair set and the blade never binds.
- **The drag is `VesselTransformer.ExternalTurnRateMultiplier`**, folded into `TurnScalar`
  (pitch/yaw) AND a new `RollScalar` in all three `Roll()` bodies — a sword sweeps with any
  rotation, so "held" means every axis. It is a one-writer property (the driver writes it while
  the drag is non-trivial and hands it back at exactly 1), `ResetTransformer` clears it, and the
  driver's `OnDisable` releases it, so neither an interrupted bind nor a vessel swap can strand a
  slowed turn. It also lowers `MaxTurnRateDegreesPerSecond`, so an AI Rhino's reachability test
  sees the drag honestly.
- **An energized blade never binds** — it pops on entry, and the ignition edge re-dispatches the
  standing contacts, so anything still in the pair set on an energized frame is about to pop.
- **Nothing about the prism changes but photons.** `AbsorbSuperShieldHit` is the one super-shield
  gate: invulnerability, collider, volume, spatial registration and state flags are untouched.

The old recoil is kept behind `recoilWhenDenied` on the damage effect (default **off**) as an A/B.

**Stated cost:** a pilot who parks the blade in armour now turns at 30% until they back out, where
the recoil used to eject them for free. That is the point — but it also means an AI Rhino that
drives its blade into a super-shielded lining turns slower there, which its reachability test
already accounts for.

Consequence to be aware of: super-shielded prisms are used as track/arena lining (Skim Race,
Astro League edge). An energized Rhino can carve those — deliberately a *paid, windowed* act now
rather than free on contact. If a mode must protect its lining outright, that's a follow-up
(per-mode effect container or a prism-level carve-out decided then, not a silent re-gate of
ordinary cutting).

## The energize ritual (the supershield key)

Hold the **lower/chop stance** — both triggers pulled (sum ≥ `stanceSumThreshold`, 1.5) and even
(|difference| ≤ `stanceCenterEpsilon`, 0.4) — for `energizeHoldSeconds` (1 s; 0.5 s for a human on a phone, `touchEnergizeRateMultiplier`):

```
Idle ── stance held, off cooldown ──► Charging (anticipation arcs, blade leans red)
Charging ── stance broken ──► Idle
Charging ── hold met ──► ENERGIZED (IGNITION burst; costs nothing since 2026-10-26)
Energized ── stance held since ignition ──► stays lit
Energized ── stance left ──► tail: stays lit energizedTailSeconds (5 s), then runs out
             (re-taking the stance does NOT re-light it)
tail elapsed ──► Cooldown (energizeCooldownSeconds, 5 s) ──► Idle
```

- **The tail runs out once let go (2026-10-16).** Until then every return to the stance
  refreshed an energized blade with no gate, so a chop every few seconds kept any blade lit
  forever for one payment. Now `ShieldSkimmerScaleDriver.UpdateEnergize` holds the blade lit
  from the stance only until the first release (`_releasedSinceIgnition`); after that the tail
  counts down whatever the fingers do. (Charge L5 **Live Wire** restored the refresh until
  2026-10-26, when Charge's L5 became the sword's steal and Live Wire was deleted.)
- **Who decides (2026-10-16).** The machine that OWNS the vessel (the pilot's, or the server
  for an AI) runs this state machine; `RhinoSwordNetworkRelay` (a `NetworkBehaviour` on the
  Rhino root, owner-write `n_state` — the phase nibble plus a surge bit; `n_energy` went with the
  energy, 2026-10-26) publishes
  it, and every other peer mirrors it through `SetEnergizePhase`, so the ignition re-dispatch,
  the FX and the afterimage window fire there on the same edges. Off the network (a menu
  vessel) the driver decides locally exactly as before.

- **Gesture source — the replicated trigger MIRRORS, on every machine:**
  `ShieldSwipeActionExecutor.FeedSwordStance` evaluates the stance from
  `InputStatus.LeftTriggerAnalog`/`RightTriggerAnalog` (the `n_lTrig`/`n_rTrig`
  NetworkVariables — Owner-write, Everyone-read) rather than the local pose signals. This is
  load-bearing for the conserved prismscape: the stance gates the supershield pop, every client
  executes that pop in its own local prism sim, and the owner's analog thresholds vs a remote's
  binary event replay would give DIFFERENT verdicts (owner at half-pull: sum ~0.95, below 1.5;
  remote both-held synthesis: sum 2) — one machine pops a prism the other keeps. The mirrors
  make every peer run the identical thresholds on the identical values (deadzone-renormalized
  the same way). The gesture is still the chop pose the player already knows; thresholds live
  on `RhinoShieldSwipeConfig.asset`.
- **Binary inputs energize too:** DualMouse and keyboard write 0/1 mirrors, so both held =
  sum 2. **Touch energizes by lifting both thumbs (2026-10-21, § On a phone below)**: with no
  thumb on the glass `TouchInputStrategy` writes both mirrors 1 (`idlePullsBothTriggers`), the
  same values two held triggers write. *(Until 2026-10-21 touch wrote no mirrors and could not
  energize.)* The event path's both-held synthesis (`diff 0, sum 2`) remains for the
  POSE only, and since 2026-10-16 only for touch — every other device, on every peer, poses from
  the mirrors (`RHINO_SHIELD_SWIPE.md` § "Input paths"). *Until 2026-10-16:* AI never pulled
  triggers, so AI Rhinos never energized. Now `RhinoSwordAutopilot` (Rhino root) writes the
  mirrors on the owning machine and holds the chop stance near super-shielded mass or on a
  ≥ 75% meter (`energizeWhenFull` 0.75).
- **A turn end / despawn / autopilot takeover mid-hold** drops the stance (`ResetImmediate` →
  `SetInStance(false)`, plus `FeedSwordStance`'s own owner-side autopilot guard — a paused
  `InputController` FREEZES the mirrors rather than zeroing them), so the local sword can't be
  left charging forever. The frozen mirrors' remote-side residual (a peer's replica reading
  stale held values after the owner entered the lava lamp mid-hold) no longer decides anything
  since 2026-10-16: a replica takes the energize phase from the owner's relay, not from its own
  read of the mirrors, so the owner's dropped stance is what every peer sees.
- **The resting-prism edge (v1's lesson, solved for BOTH contact tiers):** a super-shielded
  prism already resting against the blade when ignition lands gets no fresh `OnTriggerEnter` —
  and since the shell tier owns shielded contact, its pair was dispatched once on ENTRY and
  would never re-fire. The ignition edge therefore re-dispatches standing contacts through both
  tiers: `SkimmerImpactor.ReapplyPrismEffectsToOverlapping()` (box-trigger overlaps, which also
  covers `ForceLegacyBoxInteraction` mode) and the new
  `PrismShellContactManager.RedispatchPairsForOwner(impactor)` (live shell-owned pairs re-run
  through the same `AcceptImpacteeFromShellContact` chain their entry used). The pop the player
  just paid for lands the same frame.
  **Since 2026-10-16 both replays are `superShieldedOnly: true`** — the ignition buys the POP;
  replaying every standing contact also cut the core of any merely-shielded prism resting in the
  blade (a free Breaker). And the box-overlap set is **identity-tested**: each entry remembers
  the `prismProperties.TimeCreated` it was recorded against, so a pooled prism recycled into a
  new life inside the blade is no longer re-cut as if it were the old one.


### Swipe recovery (and why it is NOT the rejected slash cooldown)

Each swipe direction owes a short recovery — `swipeCooldownSeconds` (0.35) on
`RhinoShieldSwipeConfig.asset` — after it releases, so the sword swings with a rhythm instead of
flapping as fast as the triggers can be worked. A direction counts as having swung once its pull
passes `swipeEngageThreshold` (0.4); the timer starts on RELEASE, so a swing always plays out in
full and then pays for itself. **While the blade is ENERGIZED the recovery is ZERO** — the frenzy
is part of what energizing buys, and the timers are cleared as it burns so dropping out of
energized never inherits a recovery the player never felt themselves earn.

This is **not** the slash cooldown that was rejected, and the difference is the whole point:

| | rejected v1 slash cooldown | this |
|---|---|---|
| What it gated | prism DAMAGE — the sword refused to cut between slashes | the lateral POSE only |
| Cutting | rate-limited: the sword "mostly didn't cut" | **unchanged — the blade cuts everything it touches, always** |
| The chop / energize stance | also gated | never gated: recovery is applied to the DIFFERENCE axis after the stance is fed from the raw trigger mirrors, so a recovering sword can still chop and still energize |

Ordinary cutting stays ungated, which is the locked rule. What is rate-limited is how often the
pilot can *sweep the blade sideways*, which is swordsmanship, not a damage gate. Implementation:
`ShieldSwipeActionExecutor.ApplySwipeRecovery` (edges tracked on the RAW target so a suppressed
input cannot re-arm itself mid-hold).

## The blade is HILT-ANCHORED (a sword, not a staff)

The blade mesh is a capsule centred on its transform, so growing it used to extend the sword
equally in BOTH directions from its mount: at length 30 it ran 30 units past the grip each way,
at 120 it ran 120 — a quarterstaff the vessel wears through its middle, and worse the further
the energy meter filled. `ShieldSwipeActionExecutor.ApplyShieldPose` now offsets the blade's
centre by its own half-extent (`bladeHalfExtentLocal` × `localScale.y`) along the pose's local
+Y, which pins the **hilt** to the authored mount and sends every unit of growth out the tip. It
reads as a sword at every size, and the rest pose (raised, 20° from vertical) becomes a
swordsman's guard with the chop bringing it down.

Two consequences the geometry forces, both handled:

- **`lengthScale` is 2, not 1** (`ForceFieldSkimmer Variant.prefab`). `SkimmerSwingKinematics.
  HalfLength` is `0.5 × lossyScale × lengthScale`, which assumes a primitive spanning local
  ±0.5 — but Unity's **capsule spans ±1**, so at 1 the model described the middle HALF of the
  visible blade: a contact out at the real tip clamped to mid-blade and reported a fraction of
  the lever arm it actually rode, and FX anchored to `ClosestBladePoint` landed in the wrong
  place. **This changes tip debris speed** (the correct lever arm is ~2× the modelled one, and
  hilt-anchoring puts the tip further from the pivot again) — more tip strikes will saturate
  `debrisSpeedLimit` (200). If they read too hot, `swingVelocityScale` on
  `RhinoSkimmerDamagePrismEffect.asset` is the dial; do not "fix" it by putting `lengthScale`
  back, which would re-break the geometry.
- **Growth must not read as a swing.** A hilt-anchored blade's CENTRE — the point the velocity
  sampler differentiates — slides along the axis as the blade lengthens, at up to the surge
  burst's 600 u/s (`crystalBurstGrowSpeed`). That is real motion of the transform but it is not a strike, and counting it
  would resurrect exactly what `includeElongation: false` exists to prevent.
  `SkimmerSwingKinematics.RemoveGrowthTranslation` (pure, unit-tested for both signs) strips the
  along-axis growth component before smoothing, gated by `compensateGrowthTranslation` (default
  on). Net effect: the same swipe reports the same velocity it did before the blade moved.

### Contact velocity (composes with the swing model)

Both destruction paths — the normal explode and the super-shield pop — throw debris at the
velocity of the **part of the blade that actually touched the prism**, via
`PrismEffectHelper.ContactVelocity` → `SkimmerSwingKinematics` (`RHINO_SHIELD_SWIPE.md`
§ "Swing velocity model"). A tip strike mid-swipe scatters mass far harder than a hilt graze;
a skimmer with no swing model collapses to `Course * Speed`. `proportionalDebris` (on) hands
that velocity over as final through `DamageProportional`, at `restitution` (1/3) and capped by
`debrisSpeedLimit` (200). The super-shield pop reproduces those same two lines directly
(`Damage(v * restitution, …, devastate: true, debrisSpeedLimit:)`) because the helper cannot
devastate — keep the two branches in sync if the helper ever gains a devastate overload.

**Elongation interaction:** Space changes the blade's length (eased at `spaceLengthEaseSpeed`,
30 u/s, only when the Space level moves), and `SkimmerSwingKinematics` can optionally count that
elongation as tip velocity (`includeElongation`, **off** in `RhinoSwordSwingKinematicsConfig.asset`).
Leave it off: a pilot gaining a Space level must not strike harder for the half-second the blade
grows.

**Length is the lever arm.** The blade's live length bounds `ClosestBladePoint`, so it *is* the lever
arm `r` in `ω × r`: a longer (Space-levelled) blade's tip strikes genuinely faster. *(Until 2026-10-26
the length also came from banked energy - kills lengthened the blade, which made tip strikes faster,
which banked more energy - a positive loop retired with the energy.)*

## The slice (the death visual, 2026-09-26)

A prism the blade destroys is **cut**, not burst — the energy sword is a blade, and a blade kill
that read as a blast was the one place its identity broke. The full record is
`Docs/PRISM_ANIMATION.md §4.10`; the pilot-facing half:

- **The cut is the stroke the pilot made.** The plane contains the blade's own axis and the
  velocity of the blade point that made contact (`SkimmerSwingKinematics`), so a flat sweep cuts
  flat, a vertical chop cuts vertically, and a blade held out while the ship flies past shears the
  prism along the ship's path. It passes through the blade point nearest the prism, clamped so the
  prism's centre sits within `maxCutOffsetFraction` of its half-thickness of the cut — a grazing
  tip still reads as a slice, never as a corner chipped off. A stab (the blade moving along its own
  length) sweeps no plane and cuts across the hull's up instead.
- **What you see:** a white-hot seam flashes across the prism at the instant of the cut; the two
  halves hold for a beat, part along the cut and open like a book about the trailing edge (never
  through each other — the hinge rule is proven); each half shows its cut face cooling from white
  to the domain's bright colour; then each half dissolves AWAY from the cut face behind a ragged
  ember front. The halves drift a little along the swing and coast to a stop. ~1.15 s total.
- **Photons only.** `Prism.Slice` is `Prism.Damage` with the plane parked for the death event, so
  the kill, the energy banked, the stats, the SFX and every gate (shield, super-shield, bind) are
  exactly what they were. A super-shield pop shatters the stellation as before and slices the core.
- **It cannot cost a kill its visual.** Budget full (`maxLiveSlices`, 48), config off, a
  degenerate cut or the render service down → the ordinary explosion plays.
- **A/B:** `sliceDestroyedPrisms` on `RhinoSkimmerDamagePrismEffect.asset` (default on). Look:
  `PrismSliceMaterial` (colours, seam, ember, dissolve noise). Timing and motion:
  `Resources/PrismSliceConfig`.

## No energy (2026-10-26) — and Length: Space alone

**There is no energy.** The Rhino's `Shield` resource (index 1 of its `ResourceSystem`, the meter
that used to be "energy") is deleted from `Rhino.prefab`; nothing else on the Rhino addressed it
(its only other indexed effect, the omni blast, reads index 0). Gone with it: the energy banked per
cut (`energyPerPrism` 0.04 / `energyPerSuperShieldedPrism` 0.12 and the Charge
`chargeEnergyGainMultiplier` on `RhinoSkimmerDamagePrismEffect`), the energize cost
(`energizeCostFraction` 0.1), the surge's `energyFill`, the heat ramp (`fullEnergyColor` /
`fullEnergyBrightness`: the blade now rests at `restingBladeColor`), `IRhinoSwordState.Energy01` /
`AddEnergy`, and the relay's `n_energy`. The energize is gated by its stance and its cooldown alone.

**Charge is the sword's STEAL.** A sword hit on a rival vessel steals petals through
`RhinoSwordStealBySkimmerEffect` (a `VesselOvertakeBySkimmerEffectSO`, `requireOvertake` off), and
its `stealScale` now scales with **Charge**: ×1 at rest (the price Broadside puts on a Strike), ×2.5
at level 10, floored ×1 - the same ElementalFloat the Squirrel's joust authors on the same verb, read
at the thief's replicated level. The base magnitude stays derived by
`Tools/Build/author_combat_debuff_magnitudes.py` (`--check` clean). The card the Charge row draws is
still the energize card (bound to the both-triggers stance): lit while energized, the cooldown veil,
and its gauge now shows the energize itself (`ShieldSkimmerScaleDriver.EnergizeGauge01`: filling as
the stance is held, full while lit, draining over the let-go tail) instead of stored energy.

**Length: Space alone** (Garrett: "Space alone can grow the sword, but move the camera
accordingly"). `ShieldSkimmerScaleDriver.UpdateLength` writes the blade's world length from the
Skimmer's live Space scale (`Skimmer.LiveElementalScale`, the `Scale` ElementalFloat override on
`Rhino.prefab`: 24 at rest → 40 at Space 10), eased at `spaceLengthEaseSpeed` (30 u/s), along local Y
only so the authored X/Z silhouette holds; the first write snaps so a fresh blade never grows in.
It is the one writer (`HasExternalScaleDriver` keeps the Skimmer's own un-eased write down). The
hilt anchor re-applies with the length, so the sword always grows out of its tip.

**The camera follows the length** (the local human pilot only, `RhinoSwordCamera`): the camera's
distance and look point scale by `1 + cameraFollowsBladeLength × (length / rest − 1)` through
`CustomCameraController.VesselDistanceScale` (a point-of-use multiplier, like the surround framing,
so adaptive zoom keeps working underneath; reset with the follow target). At `cameraFollowsBladeLength`
1 the blade reads the same size on screen at every Space level: the Rhino's 70-unit camera sits at
~117 at Space 10. It never comes nearer than authored (a Space deficit does not pull it in).

## The omni-crystal Surge — the sword ring (reworked 2026-10-26)

An **omni** crystal collected by the hull runs `RhinoVesselSurgeByCrystalEffectSO`
(`RhinoVesselSurgeByCrystalEffect.asset`, in `RhinoImpactorDataContainer`'s crystal list beside
the unchanged `RhinoVesselExplosionByCrystalEffect` blast), which calls
`IRhinoSwordState.Surge(surgeSeconds)`. For **`surgeSeconds` (8 s)**:

- **A ring of swords surrounds the hull** (`RhinoSurgeSwordRing`, owned and ticked by the driver).
  `surgeRingSwordCount` (8) swords in all: the pilot's blade plus 7 copies, each the blade's LIVE
  pose turned about the nose axis of the blade's parent (the frame the swipe pivots in) by
  k × 45°. The triggers drive every one of them, because every copy is the blade's pose: the
  swipe spins the ring, the chop folds every sword in toward the nose like a closing flower, and a
  combo flourish plays on all eight. The ring fans out of the blade in `surgeRingUnfurlSeconds`
  (0.35 s, eased) and folds back into it when the surge ends — a turn, never a size change.
- **Every copy cuts.** A copy is a clone of the blade root (mesh, the sphere + capsule triggers,
  the kinematic body, `SkimmerImpactor`, `ImpactCollider`, its own `SkimmerSwingKinematics`),
  cloned under an INACTIVE holder so the one-sword parts are removed before it ever wakes
  (`RhinoSwordFXController`, which would re-seat the pilot's tracers). Its `Skimmer` is initialised
  with the vessel, its own scale write stands down, and its `SwordState` is the real sword — so
  it runs the same prism chain (`RhinoSkimmerDamagePrismEffect`), banks into the same meter, and
  pops super-shields while the blade is energized (which the surge holds it). Its own swing model
  gives its cuts the velocity of the part of THAT sword that struck. Every copy wears the blade's
  live colour (its property block is copied each frame).
- **The camera pulls back and re-centres** (the local human pilot only). `RhinoSwordCamera` eases
  `CustomCameraController.SurroundFraming01` in over `surgeCameraBlendSeconds` (0.6 s): the
  camera moves straight behind the hull at `surgeCameraDistanceScale` (×2: the Rhino's 70 → 140)
  and looks at the hull itself (the 18-unit look lift fades out), so the hull is the centre of the
  frame and the whole ring is on screen. Applied at the point of use, like the rear view, so
  adaptive zoom keeps working underneath; cleared with the follow target. No FOV is touched.
- the blade is held **ENERGIZED** with no stance, overriding a cooldown; the opening beat is a full
  flash, whole-blade crackle and a camera shake (`RhinoSwordFXController.NotifySurge`);
- **no element buff and no fill** (removed 2026-10-26: "Omnicrystal doesn't need the element
  buff"; the `elementBoost` and `energyFill` keys are gone);
- the Mass **afterimages** lay throughout the surge (they otherwise wait for an energize to end;
  `RHINO_SWORD_AFTERIMAGE.md`) — from the pilot's blade only, not the copies.

When the surge ends it hands over to the ordinary rules with the tail already spent, so the
blade goes out the next frame, the ring folds
away, the camera eases back, and the normal 5 s afterimage window follows. The surge is
owner-authoritative (`Surge` is a no-op off the authority); the relay's surge bit raises the ring
on every other peer, and because every peer poses the blade from the replicated trigger mirrors,
every peer's ring stands in the same pose and cuts against its own copies — exactly the contract
the blade already keeps. Omni-crystal collection resolves on the server and replays on a remote
pilot's own machine (`OmniCrystalImpactor.ReplayOnRemoteOwner`); the elemental half lands where
every crystal's elemental effect lands — the server and the owner.

**Known edges** (accepted for the first pass, in § Follow-ups): a kill by a copy sparks and
flashes on the PILOT's blade (the FX has one blade); the copies have no swept-contact tier, so a
very fast swing can step a copy past a thin prism the way the blade's own discrete trigger can;
and for the 0.35 s fan-out every copy overlaps the blade, so a SHIELDED prism the blade touches in
that window can lose its shield to one sword and its core to another (a brief free Breaker).

> **History — the surge burst, DELETED 2026-10-26.** From 2026-10-16 the surge swelled the blade
> to `authoredSilhouette × Lerp(1, crystalBurstFactorAtFullEnergy 4, energy)`, held 2.5 s and eased
> back (`StartBurst`, the `crystalBurst*` keys). Gone with the rest of the resizing; the 6 s surge
> became 8.

> **History — the per-petal crystal burst, DELETED 2026-10-16.** The burst used to belong to the
> SWORD: `RhinoSwordCrystalBurstEffectSO` (the sword container's only crystal effect) fired it
> on every **elemental** crystal the sword collected — and drained ALL energy. The sword capsule
> overlaps the hull, so every ejected petal and dropped heart reached it: collecting the levels
> the Rhino's elements are made of emptied the meter its blade is made of. (Its
> `AOESlowExplosion` had already been removed 2026-10-08 for detonating on each pickup.) The
> class and asset are deleted and the sword container's crystal list is empty. Embedded heart
> crystals were never collectable by a skimmer (`SkimmerImpactor`'s `IsEmbedded`/`IsExploding`
> gate) and still are not.

## Blade FX (`RhinoSwordFXController` — prefab-authored, editor-tunable)

All look lives on a **prefab-authored component on the blade root** (added to the
ForceFieldSkimmer instance in `Rhino.prefab`), with every knob on
`ShieldSkimmerScaleConfig.asset` and the visual assets authored in the project — this replaces
v2's code-built `RhinoSwordVisualizer` (deleted). Four layers:

1. **Resting colour.** The blade rests at `restingBladeColor` (white-hot). *(Until 2026-10-26 it
   brightened toward `fullEnergyBrightness` as energy filled; there is no energy now.)* **The blade
   is never the pilot's domain colour** — it
   friendly-fires (no domain gate, `affectSelf` true), so a team-tinted blade would read as safe
   to allies, which is the one thing it is not. That is also why the resting colour is AUTHORED
   IN THE CONFIG rather than read off `FresnelMaterial`'s `_Color` as v2 did: the blade's meaning
   must not be able to drift with a shared material's tint. The blade body uses the **shared**
   `FresnelMaterial` (sole exposed property `_Color`), so the FX controller never touches
   `renderer.material` — everything goes through a per-renderer **MaterialPropertyBlock** (the
   `AstroLeagueBall` impact-flash precedent; RGB > 1 feeds gameplay bloom — active in game
   scenes, off in Menu_Main where the blade still brightens in
   LDR). **`FresnelGraph.shadergraph` carries the v2 fix**: `_Color` used to feed a Blend node
   whose output connected to NOTHING (every colour write invisible); the graph now renders
   `BaseColor = _Color × Voronoi` (Multiply blend, opacity 1 — structurally re-verified this
   branch by edge-list dump), alpha unchanged (fresnel-driven). The shader has exactly one
   material (`FresnelMaterial.mat`) rendered by exactly one prefab (the Rhino's
   ForceFieldSkimmer) — no other look changed.
2. **Energize — the only thing that moves the blade's HUE.** CHARGING leans it 35% × charge
   toward the energized colour with escalating anticipation arcs every `chargeCrackleInterval`;
   IGNITION blends it fully (`energizedColor` × `visibilityMultiplier` over
   `colorTransitionSeconds`) and detonates `igniteCrackleSites` (5) crackle bursts spread
   hilt→tip (`igniteCrackleIntensity` / `igniteCrackleSeconds`). `energizedColor` is the shared
   **danger colour** — `SO_ColorSet.Danger` from the live `OriginalColorSetSO`, the same
   domain-independent red a danger prism wears on its rim. An energized blade tears apart
   hardened mass and still friendly-fires, so it speaks the platform's existing "this hurts"
   language instead of inventing a private one, and white→red is legible at a glance where
   white→brighter-white was not. The blade crackle material follows it (white core, danger-red
   glow and rim). The crackle is the adapted **forcefield-crackle** system (below).
3. **Impact feedback** — a decaying white-out flash per prism destroyed (`hitFlashAmount` /
   `popFlashAmount`, stronger pulses override weaker mid-decay) plus a **contact spark** at the
   exact blade point that made contact (`SkimmerSwingKinematics.ClosestBladePoint`;
   `sparkIntensity` / `sparkSeconds` / `sparkWorldRadius`); a dim **denied spark**
   (`deniedSparkIntensity`) when a non-energized blade binds in a super-shield. A surge
   opens with a whole-blade crackle.
4. **Blade tracers — a comb of hairlines, authored on the components.** FIVE **authored**
   `TrailRenderer` children of the fuselage (`RhinoSwordBladeTracer0..4` in `Rhino.prefab`, all
   wearing `RhinoSwordTracerMaterial.mat` — no runtime construction, no `Shader.Find` fallback),
   fuselage-parented so the blade's scale can never distort their shape. They are spread evenly
   down the blade, **element 0 on the tip, the last on the hilt**, so a swing draws a comb of
   fine streaks that reads the sword's sweep instead of one slab.

   **Their look is yours on the components** — `widthMultiplier`, `time`, the width curve, the
   colour gradient, material. Nothing in code writes any of them, and the COUNT is data too:
   `RhinoSwordFXController.bladeTracers` is an array and the spread is derived from its length,
   so add or remove entries freely. `SeatTracers` owns placement only, and the spacing is EVEN
   by construction: ONE span serves the whole set, inset at each end by half the head width of
   the streak sitting there (head width = `widthMultiplier` × the width curve at t=0) so
   widening an end streak grows it INTO the blade rather than out past the point. Insetting each
   streak by its own width instead would give every streak a different span and the spacing
   would drift apart the moment two were tuned to different widths. Authored hairline:
   `widthMultiplier` 0.5, `time` 0.15; their prefab rest transforms are authored spread along the
   blade too, so the set reads correctly in the editor rather than stacked at the fuselage origin.

   All five are tinted from the same live blade colour as the body, so **the streaks change with
   the sword through every state** (white-hot → danger red on energize).

   *Do not drive their size from code.* An earlier pass anchored a single tracer mid-blade with
   width = the full blade length, reasoning that a TrailRenderer lays width across its path so
   the ribbon would span hilt-to-tip. It does — and at a 240-unit blade that is a 240-unit-wide
   white sheet swallowing the vessel. The blade is ~10 units thick; the tracers belong on that
   order, which is what "hairline" means here.

**Camera shake (local pilot only, never autopilot/remote/AI):** `popShakeIntensity` (1.2) on a
super-shield pop; `burstShakeMaxIntensity` (2.5) when a surge starts. Routed through `CameraManager.Instance.GetActiveController()` →
`CustomCameraController.Shake` (the `AstroLeagueBall.ShakeCamera` pattern).

**No new haptics** — the two-feel policy stands (`Docs/HAPTICS.md`); the sword already carries
the skim pulse via `SkimmerHapticsByPrismEffect`. Audio is all free-riding on sanctioned paths:
`DeactivateShields` plays `ShieldDeactivate`, `Damage` plays `BlockDestroy`,
`AOEExplosion.Detonate` plays `Explosion`. (A dedicated energize SFX would follow the
`SkimmerSFXByCrystalEffectSO` template — follow-up, not wired.)

### The capsule crackle (adapting the forcefield-crackle system to the blade)

`ForcefieldCrackleController` + `ForcefieldCrackle.hlsl` were purpose-built for the base
skimmer's SPHERE: impacts stored as unit directions, ripples measured as great-circle angles.
On the blade's stretched capsule (built-in capsule mesh under the ~`(1.5, 24, 4.8)` blade
scale) that collapses the whole blade length into the two poles. The adaptation:

- **`ForcefieldCrackleCapsule_float`** (new entry in the same HLSL) measures in **world units**:
  impacts are object-space POSITIONS, the vertex shader supplies the object→world scale per
  axis, and distances are computed on scale-multiplied positions — so a ripple travels the same
  world distance along the blade as around it, arcs stay glued to the blade through swings, and
  they stretch with it as Space grows it. `_ImpactParams.y` becomes the ripple's world-unit
  reach. All visual params (`_ArcDensity`, `_RingThickness` as a fraction of reach, colors,
  fresnel rim) keep their sphere-version meaning.
- **`ForcefieldCrackleCapsule.shader`** (donor-clone of the sphere shader) renders it;
  **`RhinoBladeCrackleMaterial.mat`** (white core, danger-red glow and rim — it was teal/white
  before the energized colour became the danger red) is authored on the blade's
  `ForcefieldCrackleOverlay` child via a variant override — the base `Skimmer.prefab` keeps its
  sphere shader + red material untouched.
- **`ForcefieldCrackleController.surface = Capsule`** (new enum, default Sphere) stores local
  positions instead of normalized directions; authored as a variant override on
  `ForceFieldSkimmer Variant.prefab`. The controller and overlay child already existed on the
  blade (the variant kept them; the Rhino's mesh swap already made the overlay a capsule) —
  what was missing was the parameterization and any caller: the generic
  `SkimmerForcefieldCracklePrismEffectSO` requires a SphereCollider the Rhino removed, and is
  deliberately NOT wired — the sword's sparks are kill/pop/ignition events from the FX
  controller, not every-contact events.

Verified out-of-editor: the capsule HLSL compiles under clang `-Wall` and localizes correctly
(sampled 2048 surface fragments against two impacts: ~5% lit, rim-only elsewhere, alpha 0 with
no impacts). The LOOK still needs the in-editor pass below.

## Architecture — how shared effect SOs reach per-vessel state

Effect SOs are singletons and can't hold per-vessel state. The per-Rhino state lives on
`ShieldSkimmerScaleDriver` (the sword's brain, one per Rhino, on `ScaleSkimmerObject` in
`Rhino.prefab`), which implements the slim `IRhinoSwordState` (`IsEnergized`,
`IsSurging`, `NotifyPrismDestroyed`, `NotifyPopDenied`, `Surge`,
`IsOwnAfterimage`, `SetInStance` — `TriggerCrystalBurst` went with the per-petal burst on
2026-10-16) and registers itself on `Skimmer.SwordState`. Effects read it via
`impactor.Skimmer.SwordState`, null-safe so any non-Rhino skimmer reusing the assets just runs
the damage behavior (and the bind entry beat, since it can never be energized) without the
FX bookkeeping. The driver owns STATE (energize machine, Space length, surge) —
deciding it on the owning machine and mirroring it from `RhinoSwordNetworkRelay` everywhere
else — and delegates all LOOK to the
prefab-authored `RhinoSwordFXController` on the blade root; the gesture arrives from
`ShieldSwipeActionExecutor` (the trigger reparameterization's owner). The FX controller resolves
same-GameObject pieces (`Skimmer`, `SkimmerSwingKinematics`, crackle, body renderer) via
`TryGetComponent` and warns once, naming the missing wire, if an authored reference is absent.

## Files

| Role | File |
|---|---|
| Per-vessel state contract + energize phases | `Executors/IRhinoSwordState.cs` (`RhinoSwordEnergizePhase`) |
| Sword brain (energize state machine, Space length, surge, contact re-dispatch; no energy since 2026-10-26) | `Executors/ShieldSkimmerScaleDriver.cs` |
| The camera's two holds (Space-length distance, surge framing) | `Executors/RhinoSwordCamera.cs` · `CustomCameraController` (`VesselDistanceScale`) |
| Charge = the sword's petal steal | `_SO_Assets/Effects/Vessel Skimmer Effects/RhinoSwordStealBySkimmerEffect.asset` (`stealScale`, Charge ×1 → ×2.5) |
| The surge's sword ring + camera framing | `Executors/RhinoSurgeSwordRing.cs` · `Controller/Camera/CustomCameraController.cs` (`SurroundFraming01`, `SurroundDistanceScale`, `SurroundOffset`) · tests `Tests/Editor/RhinoSurgeSwordRingTests.cs` |
| Blade look (heat ramp, energize blend, ignition/spark crackle, flashes, tracers, shake) | `Executors/RhinoSwordFXController.cs` (prefab-authored on the blade root) |
| Gesture source (stance feed from the reparameterized triggers) | `Executors/ShieldSwipeActionExecutor.cs` (`FeedSwordStance`) |
| Gesture thresholds | `Data Containers/RhinoShieldSwipeConfigSO.cs` → `_SO_Assets/VesselActions/Rhino/RhinoShieldSwipeConfig.asset` |
| Tuning (energize, surge ring + camera, all FX knobs) | `Executors/ShieldSkimmerScaleConfigSO.cs` → `_SO_Assets/VesselActions/Rhino/ShieldSkimmerScaleConfig.asset` |
| Prism effect (damage, energize-gated super-shield pop, the cut plane) | `ImpactEffects/EffectsSO/Skimmer Prism Effects/RhinoSkimmerDamagePrismEffectSO.cs` → `_SO_Assets/Effects/Vessel Prism Effects/RhinoSkimmerDamagePrismEffect.asset` |
| The slice (death visual) | `Utility/Effects/PrismSlice.cs` (batch/budget/retire) · `Utility/PrismSliceGeometry.cs` (the stamps) · `_Graphics/Materials/Graphs/PrismSlice.hlsl` + `PrismSlice.shader` · `_Graphics/Materials/PrismSliceMaterial.mat` · `Resources/PrismSliceConfig.asset` · `Vessel/Prism.cs` (`Slice`) · `Prisms/PrismFactory.cs` (`SpawnExplosion` routes it) |
| Owner-authoritative replication (energize phase, surge bit; protocol 15) | `Controller/Vessel/RhinoSwordNetworkRelay.cs` (on the Rhino root) |
| Omni-crystal Surge (8 s energized, the sword ring + camera) | `ImpactEffects/EffectsSO/Vessel Crystal Effects/RhinoVesselSurgeByCrystalEffectSO.cs` → `_SO_Assets/Effects/Vessel Crystal Effects/RhinoVesselSurgeByCrystalEffect.asset` (in `RhinoImpactorDataContainer`). *The sword-side `RhinoSwordCrystalBurstEffectSO` + asset were deleted 2026-10-16.* |
| AI sword hand (writes the trigger mirrors) | `Controller/AI/Rhino/RhinoSwordAutopilot.cs` (on the Rhino root; moved 2026-10-25) |
| Box-overlap re-apply (energize rising edge) | `ImpactEffects/Impactors/SkimmerImpactor.cs` (`ReapplyPrismEffectsToOverlapping`) |
| Shell-pair re-dispatch (energize rising edge) | `Controller/Managers/PrismShellContactManager.cs` (`RedispatchPairsForOwner`) |
| Capsule crackle surface mode | `Controller/Vessel/ForcefieldCrackleController.cs` (`CrackleSurface`) |
| Capsule crackle math | `_Graphics/Materials/Graphs/ForcefieldCrackle.hlsl` (`ForcefieldCrackleCapsule_float`) |
| Capsule crackle shader / material | `_Graphics/Materials/Graphs/ForcefieldCrackleCapsule.shader` / `_Graphics/Materials/RhinoBladeCrackleMaterial.mat` |
| Authored tracer material | `_Graphics/Materials/RhinoSwordTracerMaterial.mat` (TrailViewer family) |
| Hilt anchoring (sword, not staff) | `Executors/ShieldSwipeActionExecutor.cs` (`AnchorOffsetLocal`, `ApplyShieldPose`) |
| Growth-translation compensation | `Vessel/SkimmerSwingKinematics.cs` (`RemoveGrowthTranslation`) + `SkimmerSwingKinematicsConfigSO.compensateGrowthTranslation` |
| Skimmer hook | `Vessel/Skimmer.cs` (`SwordState`) |
| Embedded-heart guard on skimmer crystal effects | `ImpactEffects/Impactors/SkimmerImpactor.cs` (ElementalCrystalImpactor case) |
| Blade shader fix (`_Color` now rendered) | `Assets/_Graphics/Materials/Graphs/FresnelGraph.shadergraph` |
| Effect wiring | `_SO_Assets/Effects/Effect Containers/SkimmerContainers/RhinoForceFieldSkimmerImpactorDataContainer.asset` (prism → `RhinoSkimmerDamagePrismEffect` + `SkimmerHapticsByPrismEffect`; vessel → `VesselHapticsBySkimmerEffect`, `VesselCombatHitBySword`, `RhinoSwordStealBySkimmerEffect`; crystal list **empty** since 2026-10-16) |
| Prefab wiring | `Rhino.prefab` (FX controller on the blade root + `RhinoSwordBladeTracer0..4` under the fuselage; `RhinoSwordNetworkRelay` + `RhinoSwordAutopilot` on the root; sword mount at local y **−1**; the sword's `CapsuleCollider` is a **trigger**, restored 2026-10-16 after the PR #928 merge left it solid) · `ForceFieldSkimmer Variant.prefab` (overlay material → blade crackle, `surface: Capsule`, `lengthScale: 2`) |

## Tuning knobs

On `RhinoShieldSwipeConfig.asset`: `stanceSumThreshold` 1.5 · `stanceCenterEpsilon` 0.4 ·
`swipeCooldownSeconds` 0.35 · `swipeEngageThreshold` 0.4.

On `RhinoSkimmerDamagePrismEffect.asset`: `inertia` 70 · `popRequiresEnergizedBlade` 1 ·
(the energy keys `energyPerPrism` / `energyPerSuperShieldedPrism` / `chargeEnergyGainMultiplier`
are DELETED, 2026-10-26) · bounce params
`recoilWhenDenied` 0 (the bind is shipped; on = the legacy recoil below) · legacy recoil params
(`bounceSpeedMultiplier` 0.85 / `minBounceSpeed` 10 / `bounceDurationSeconds` 0.35 — a FIXED
recoil window; the old `accelScale` passed `Time.deltaTime` into the modifier's duration, making
the shove ~4× stronger at 30 fps than 120 fps) · plus the
swing-model group `swingVelocityScale` 1 / `maxImpactSpeed` 0 / `proportionalDebris` 1 /
`restitution` 0.333 / `debrisSpeedLimit` 200 (the last two move **together** with the other
damage SOs and `PrismExplosion.prefab`'s speed band — see the swing-kinematics row in
`CLAUDE.md`; `inertia` is not the lever on the proportional path).

On `ShieldSkimmerScaleConfig.asset`: (the length keys `baseScale` 24 / `maxScale` 96 /
`prismGrowSpeed` 30 / `shrinkSpeed` 10 are DELETED, 2026-10-26: the blade's size is its authored
root scale in `Rhino.prefab`) · `spaceLengthEaseSpeed` 30 · `cameraFollowsBladeLength` 1 ·
(`energizeCostFraction` is DELETED with the energy) ·
`energizeHoldSeconds` 1 · `touchEnergizeRateMultiplier` 2 (phone hold = 0.5 s) · `energizedTailSeconds` 5 · `energizeCooldownSeconds` 5 ·
`liveWireUpgradeElement` Charge (the L5 that lets the stance re-light the tail) ·
the surge: `surgeRingSwordCount` 8 · `surgeRingUnfurlSeconds` 0.35 · `surgeCameraDistanceScale` 2 ·
`surgeCameraBlendSeconds` 0.6 (the `crystalBurst*` keys are DELETED with the burst, 2026-10-26) ·
`visibilityMultiplier` 1.2 · `restingBladeColor` white ·
(`fullEnergyColor` / `fullEnergyBrightness` DELETED with the energy) ·
`energizedColor` (1.498, 0.006, 0.007) = `SO_ColorSet.Danger` · `colorTransitionSeconds` 0.25 · `igniteCrackleIntensity` 2.5 / `igniteCrackleSeconds` 0.9 /
`igniteCrackleSites` 5 · `chargeCrackleInterval` 0.18 / `chargeCrackleIntensity` 1.1 ·
`sparkIntensity` 1.6 / `sparkSeconds` 0.45 / `sparkWorldRadius` 14 · `deniedSparkIntensity`
0.7 · tracer size is NOT here — it is authored on the five `RhinoSwordBladeTracer*`
TrailRenderers (hairline: `widthMultiplier` 0.5, `time` 0.15) · `hitFlashAmount` 0.35 · `popFlashAmount` 1 · `flashDecaySeconds` 0.35 · `flashColor`
(2,2,2) · `popShakeIntensity` 1.2 / `popShakeDuration` 0.25 · `burstShakeMaxIntensity` 2.5 /
`burstShakeDuration` 0.4 · binding: `bindTurnRateMultiplier` 0.3 / `bindEngageRate` 8 /
`bindReleaseRate` 4 / `bindJiggleIntervalSeconds` 0.2 / `bindJiggleImpactSpeed` 90 /
`bindHapticIntervalSeconds` 0.1 / `bindHapticStrength` 0.7 — all seven are C# initializers today
(the asset predates them and carries no keys, so the initializer IS the shipped value until the
asset is next saved in the editor). (`prismMaxScale` is DELETED, 2026-10-16: nothing had read
it since the Sparrow full-auto `ApplyMaxSizeDebuff`, its only consumer, was deleted in Sep 2026
with the control-theft tier, `Docs/ELEMENTAL_ECONOMY.md §9`. The v2 tracer keys —
`tracersEnabled`, `tracerMaterial`, `tracerWidth`, `tracerTimeSeconds` — are retired: the five
tracers are authored TrailRenderers in `Rhino.prefab`; tune persistence, width, taper curve and
material on the components. *An earlier revision of this line pointed at a
`tracerWidthLengthFraction` knob and said the width multiplier was code-driven — no such field
exists; nothing in code writes the tracers' size, § "Blade FX" layer 4.*)

On `RhinoBladeCrackleMaterial.mat`: arc density/sharpness, ring thickness (fraction of reach),
ripple speed, core/glow/rim colors — live-tunable in the inspector, edit or play mode
(`ForcefieldCrackleController` is `[ExecuteAlways]`).

On `RhinoVesselSurgeByCrystalEffect.asset`: `surgeSeconds` 8 (6 until 2026-10-26); `energyFill`
and `elementBoost` are DELETED (2026-10-26). The ring and camera knobs are the `surge*` keys on
`ShieldSkimmerScaleConfig.asset` above.

On `RhinoSwordStealBySkimmerEffect.asset`: `stealScale` Charge ×1 → ×2.5, floor ×1 (2026-10-26);
`stealUpgradeElement` Charge (the steal is the Charge L5; below it the petals are ejected);
`debuffMagnitude` / `buffMagnitude` are derived by `author_combat_debuff_magnitudes.py`.

The blade's length range is Space's: the `Skimmer.Scale` override on `Rhino.prefab` (`Min` 24,
`Max` 40, element Space). (`RhinoSwordCrystalBurstEffect.asset`,
which had no knobs, is deleted.)

## In-editor verification

1. Rhino in any playable mode (or Menu_Main freestyle, swap to Rhino) with a gamepad.
2. **Cutting (regression):** fly through trail/opposing prisms — every prism the blade touches
   pops immediately, with a small blade flash + a spark at the blade point that hit. No timing,
   no stance required.
3. **No energy; Space sets the length (2026-10-26):** kills change nothing about the blade (no
   gauge fill, no brightening, no growth). Gain Space levels (crystals, or `SpaceTestHarness` on
   the Rhino's `ResourceSystem`): the blade eases longer, 24 → 40 at level 10, and the camera
   pulls back with it so the blade stays the same size on screen.
3a. **Charge L5 = steal:** hit a rival vessel with the sword at Charge 0 — the victim loses
   petals, but they burst out as crystals at the strike and the Rhino's levels do not move. At
   Charge 5 the same hit lands them on the Rhino (and Charge 10 takes ~2.5x Charge 0's amount).
4. **Energize ritual:** pull BOTH triggers fully and hold the centered chop (no meter to fill
   first) — the Charge gauge fills over ~1 s of rising anticipation arcs, then the blade IGNITES:
   danger red + a crackle burst along the whole blade, the gauge full, and the Charge card lights. Holding the stance from
   ignition keeps it lit. Release the stance — the blade stays lit ~5 s, cools, and can't
   re-charge for ~5 s more. **Re-taking the stance during the tail must NOT re-light it**
   (2026-10-16), at any Charge level (Live Wire is deleted, 2026-10-26).
5. **Super-shield, not energized — the BIND:** slash into a super-shielded (Stella-Octangula)
   prism (the Skim Race track lining or any super-shielded arena mass) with a gamepad. Expect:
   **no** shove and **no** spin — the ship keeps its line; a dim spark, the prism visibly
   jiggles on contact AND keeps shuddering while the blade stays in it; one heavy thud on entry
   then a low repeating grind on the pad; turning/rolling drops to roughly a third while inside
   and eases back when the blade comes out. The prism survives. Tune on
   `ShieldSkimmerScaleConfig` (`bindTurnRateMultiplier` first). A bare desktop editor has no
   motors — judge the haptics with a pad.
5b. **A/B:** tick `recoilWhenDenied` on `RhinoSkimmerDamagePrismEffect.asset` — the old recoil
   returns exactly; untick to restore the bind.
6. **Super-shield, energized:** same prism with the blade lit — below Space 5 it still BINDS
   (step 5). At Space 5 (Shield Breaker) it pops on contact (24-face shatter + explode-out), big
   blade flash + short camera shake.
7. **The resting-prism edge:** park the blade inside a super-shielded prism (bound), then
   energize while still touching — the prism must pop the instant ignition lands, no re-approach
   needed. A merely-SHIELDED prism resting in the blade at ignition must keep its core (no free
   Breaker).
8. **Crystals:** sword-collect an elemental crystal (fly through ejected petals) — the petal is
   collected, **the blade does nothing and the meter does not move** (the per-petal burst is
   deleted). Then fly the hull through an **omni** crystal — the Surge (2026-10-26): meter to
   full, a flash + whole-blade crackle + shake, the blade does NOT grow; seven more red swords fan
   out of the blade into a ring round the nose (8 in all, 45° apart) while the camera eases back to
   about twice its distance and centres on the hull. Swipe: the whole ring spins. Chop (both
   triggers): every sword folds toward the nose. Fly the ring through trail and super-shielded
   mass: every sword cuts and pops. ~8 s later the ring folds back into the blade, the camera eases
   in, the blade goes out and the ordinary 5 s afterimage window follows. The elements read two
   levels higher and fade back; afterimages lay throughout. Tune on `ShieldSkimmerScaleConfig`
   (`surge*`) and `RhinoVesselSurgeByCrystalEffect` (`surgeSeconds`).
9. **Tracers:** five streaks comb the blade from tip to hilt through swipes, tinted with the
   live blade colour (white → danger red when energized).
9a. **Multiplayer (MPPM two clients):** a half-pull sweep on A poses A's blade identically on B
    (B poses from A's replicated analog mirrors); A's energize ignites on B on the same edge and
    B's copy of A's meter tracks A's. A super-shielded prism A pops is popped on B too.
    **The surge ring:** A takes an omni crystal — B sees A's ring fan out on the same edge, swing
    with A's triggers, and fold away with it; B's camera does NOT move (only A's does).
9b. **The slice:** cut trail prisms with a flat sweep, then a vertical chop, then by holding the
    blade out and flying past. Each kill should show a white seam along the stroke's plane, two
    halves that part and open (never through each other), cut faces cooling white → domain colour,
    then a dissolve eating from the cut face outward — gone in ~1.15 s with no pop at the end.
    Sweep a dense rib fast: past 48 live slices kills should fall back to the explosion (still a
    visual, never nothing). Stand the camera so a kill happens between it and the hull: the halves
    must go see-through like every other prism (the occlusion corridor). Untick
    `sliceDestroyedPrisms` — the old explosion returns exactly.
10. **Non-regression:** other vessels' skimmers unaffected (SwordState null; base skimmer
    crackle still the red sphere look); keyboard/DualMouse (binary mirrors) can energize by
    holding both swipe controls; **touch energizes by lifting both thumbs** (2026-10-21, § On a
    phone). (This step used to read
    *"Sparrow full-auto still shrinks the Rhino sword's max (`ApplyMaxSizeDebuff`)"* — that
    effect and that method were removed in Sep 2026 with the control-theft tier, so the
    blade's max is now always its authored length: `Docs/ELEMENTAL_ECONOMY.md §9`.)

## On a phone (2026-10-21)

Garrett, 2026-10-21: *"The rhino can energize by lifting both thumbs off the phone. we can compensate
for lack of control by having it go quicker."*

| Pad | Phone |
| --- | --- |
| LT = swing left | lift the **right** thumb (the left flies on) = swing left |
| RT = swing right | lift the **left** thumb = swing right |
| both triggers held (the chop stance) = energize | lift **both** thumbs = the chop stance = energize, **twice as fast** |

- **The swing.** `Rhino.prefab` `_touchActionOverrides`: `LeftStickAction (2)` → `RhinoShieldSwipeLeftAction`
  (`4ff8e060…`), `RightStickAction (1)` → `RhinoShieldSwipeRightAction` (`b92a3e13…`) - the pad override's own
  pair. On touch `ShieldSwipeActionExecutor.TryReadTriggers` returns false, so these press/release events drive the
  swing (full stance, rate-limited by `swipeOutSeconds` / `returnSeconds`), on every peer (the press replicates with
  its device byte).
- **The energize.** With no thumb down the profile flag `idlePullsBothTriggers` writes
  `InputStatus.LeftTriggerAnalog` and `RightTriggerAnalog` = 1 every frame (0 the frame a thumb lands). The stance
  (`FeedSwordStance` → `MirrorsInStance`) has never had a device gate - it reads the replicated mirrors on every
  device - so it counts, and the owner's `UpdateEnergize` charges and ignites exactly as for two held triggers.
  The event-driven POSE now drops into the centred chop (`diff 0, sum 2`) whenever `MirrorsInStance` is true, so the
  blade is down on every peer exactly while the stance counts (before, the touch pose stayed raised).
- **Quicker.** `ShieldSkimmerScaleConfigSO.touchEnergizeRateMultiplier` (authored **2** in
  `ShieldSkimmerScaleConfig.asset`) divides `energizeHoldSeconds` for a HUMAN on touch
  (`ShieldSkimmerScaleDriver.IsHumanTouchPilot`: replicated device is Touch, not an AI, not on autopilot): 1 s → 0.5 s
  with no steering before the blade lights. Tail, cooldown and cost are unchanged (once lit the pilot puts the thumbs
  back and flies; the tail runs while they steer).
- **Deterministic across peers.** The ignition is decided on the vessel's OWNER and replicated through
  `RhinoSwordNetworkRelay` (phase), as for every device; replicas mirror the phase. The charge ramp a replica draws
  (`Charge01`, FX only) uses the same shortened hold because it reads the replicated device.
- **A swing in progress when the second thumb lifts**: `TouchInputStrategy` releases the lift's stick event first
  (`StopStickEffects`), so `EndSwipe` drops the stance, then writes the mirrors - the blade travels from the swing to
  the centred chop at the binary rate. The swing's recovery timer starts on that release as usual; the chop axis is
  never gated by it. Both mirrors rise on the same frame, which the combo detector reads as a chord, never a combo.
- **Not on a phone**: combos (they need trigger taps; a touch swing is an event, not a mirror) and analog part-pulls.
  An AI Rhino is unaffected (its `RhinoSwordAutopilot` writes the mirrors; pad timing).

## Follow-ups

- ~~**The Space row is EMPTY (2026-10-26).**~~ **Resolved the same day:** Space alone sets the
  blade's length again (24 → 40), and the camera follows it.
- ~~**Charge's level-5 is still Live Wire** while Charge scales the STEAL.~~ **Resolved
  2026-10-26:** the steal itself is Charge's L5 (below it the petals are ejected).
- **The AI does not know about Shield Breaker.** `RhinoSwordAutopilot` still energizes when
  super-shielded mass is near, whatever its Space level; below Space 5 that energize only lays
  afterimages. Harmless, but an AI tuned for it would skip the hardened-mass trigger below Space 5.
- **Surge ring edges** (§ "The omni-crystal Surge"): a copy's kill sparks on the pilot's blade
  (one FX controller — give each copy a light spark if it reads wrong); the copies have no swept
  tier; the 0.35 s fan-out overlaps the blade (a brief free Breaker on shielded prisms — gate the
  copies' colliders on the fan being past half if it shows in play).

- ~~**Replication:** energy, energize state, and the blade's heat/flash look are
  local-authoritative.~~ **DONE 2026-10-16.** Energy, energize phase and surge are
  owner-authoritative through `RhinoSwordNetworkRelay` (owner-write NetworkVariables on the
  Rhino root); non-owners mirror them, so ignition, the FX edges and the afterimage window agree,
  and the lava-lamp frozen-mirror edge no longer decides anything on a replica. The blade POSE
  replicates through the analog trigger mirrors (`RHINO_SHIELD_SWIPE.md`). Still per peer: the
  heat/flash look is driven locally from the mirrored state (photons only), and kills are
  simulated per peer as for every prism contact.
- **Sibling dt-as-duration pattern:** `VesselDeviationByPrismEffectSO` and
  `VesselSpinBySkimmerEffectSO` (since removed) passed `Time.deltaTime * accelScale` into `ModifyVelocity`'s
  DURATION exactly the way the sword's bounce used to — the same frame-rate dependence, on
  paths this branch does not touch. Worth its own pass; not changed here.
- ~~**HUD:** the energize phase is exposed but not yet drawn.~~ **DONE 2026-10-16:** the Charge
  ("Sword Energy") card of the ability row is lit while energized, with an energize-cooldown
  veil; the energy gauge was authored 2026-10-17 (`author_hull_ability_rows.py`, `RHINO_GAUGES`)
  and the card draws the two-trigger chip. Its icon is still a PLACEHOLDER (`Rhino_SwordEnergy`).
  Since 2026-10-26 the card is "Sword Steal" and its gauge shows the energize, not energy.
- **Mode lining:** if a mode's super-shielded lining (Skim Race track, Astro League edge) must
  survive an energized Rhino, decide a per-mode carve-out then — do not re-gate ordinary cutting.
- **Friendly fire + self-farming (design sign-off):** the effect has no domain gate — identical
  to the generic damage effect it replaced (`affectSelf` is true on the Rhino) and consistent
  with the danger-prism friendly-fire philosophy. *(Until 2026-10-26 an energy bank made it
  rewarding - self-cuts banked 0.04 each; with no energy that exploit is gone.)* An energized blade
  popping an **ally's** super-shielded prism still permanently devastates friendly hardened
  structure; if that reads badly in play, gate the pop by domain.
  **The self-trail contact grace does not change this** (`ImpactEffects/SELF_TRAIL_CONTACT.md`):
  it is owner-scoped and lasts ~1 s from the moment a prism is laid, and the Rhino cannot come
  about onto its own freshest ribbon inside that window — so the grace formally covers the sword
  but never fires for it, and the self-farm loop above is intact. The guidance stands: skip the
  ENERGY BANK, never the damage.
- **Energize SFX:** a dedicated ignition sound via the `SkimmerSFXByCrystalEffectSO` template if
  the free-riding audio isn't enough.
