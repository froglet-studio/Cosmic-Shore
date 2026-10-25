# Rhino Energy Sword — ungated blade, the energize ritual, energy meter, the omni-crystal surge

> **2026-10-16:** energy, energize phase and surge are now OWNER-authoritative and replicated by
> `RhinoSwordNetworkRelay`; every peer poses the blade from the replicated trigger mirrors. Energy
> per cut is Charge-scaled (×1 → ×2, floor ×0.5); a let-go tail now always runs out unless Charge
> L5 **Live Wire** is active. The per-petal crystal burst is DELETED — the burst look now belongs
> to the omni-crystal **Surge**. Ignition re-dispatch replays super-shielded contacts only.

Layers an **energy meter**, the **energize ritual** (the supershield key), and a full authored
**FX pass** onto the Rhino's swordsmanship (the pose/analog-swipe rig is in
`RHINO_SHIELD_SWIPE.md` — read it first). Ordinary cutting is **ungated**: no stance, no
cooldown — the sword always damages prisms on contact. The ONE gated act is popping a
SUPER-shielded prism, which requires the blade to be **ENERGIZED**.

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
| Shielded prism, **Breaker** active (SPACE level 5) | any | **One slice**: the shield sheds AND the core is cut in the same hit (`breakerElement`, gated on `IsUpgradeActive(Space)`; Garrett, 2026-10-13). Restorable like any ordinary sword kill — Breaker sheds the shield first rather than devastating. Super-shielded mass is unaffected |
| **Super-shielded prism** | **ENERGIZED** | **POPPED**: `DeactivateShields()` (stellation shatter + SFX) then `Slice(devastate: true)` (the armour shatters, the core is CUT; unrestorable) — the sanctioned mass-conserving teardown, same sequence as `AstroLeagueArena.ClearEdgeLining`. `Prism.Damage` alone hard-ignores super-shielded prisms, which is why the shields must drop first. |
| **Super-shielded prism** | not energized | **BINDS** — no recoil, no spin. The blade sticks in the armour: a dim **denied spark**, the prism **jiggles**, a **punish thud**, and while the blade stays inside the vessel's **rotation rate is dragged down** and the pilot feels a **grind** (§ "Binding"). The prism survives. |
| One of this sword's own **afterimages** | any | **SLICED like any prism** once the fleet's 1 s self-trail grace is past, banking 0.04 × Charge each (Garrett, 2026-10-17: *"not immune to our destruction"*; it was never cut before). `IRhinoSwordState.IsOwnAfterimage` survives only for the AI's aim. Watch item: one energize plus self-cuts can refill the meter — the lever is energy-per-own-afterimage, not immunity (`RHINO_SWORD_AFTERIMAGE.md` §3) |
| Another **vessel** | any | The sword also **steals petals**: `RhinoSwordStealBySkimmerEffect` (a `VesselOvertakeBySkimmerEffectSO`) sits in the sword's container and runs only for a hit `VesselCombatHitBySword` admits (PvP is petals only) — the sword does drain an opponent |

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
Idle ── stance held, energy ≥ cost, off cooldown ──► Charging (anticipation arcs, blade leans white)
Charging ── stance broken or energy dips ──► Idle (no cost)
Charging ── hold met ──► ENERGIZED (spends energizeCostFraction = 0.1; IGNITION burst)
Energized ── stance held since ignition ──► stays lit
Energized ── stance left ──► tail: stays lit energizedTailSeconds (5 s), then runs out
             (re-taking the stance does NOT re-light it — unless Charge L5 Live Wire is active)
tail elapsed ──► Cooldown (energizeCooldownSeconds, 5 s) ──► Idle
```

- **The tail runs out once let go (2026-10-16).** Until then every return to the stance
  refreshed an energized blade with no gate, so a chop every few seconds kept any blade lit
  forever for one 0.1 payment. Now `ShieldSkimmerScaleDriver.UpdateEnergize` holds the blade lit
  from the stance only until the first release (`_releasedSinceIgnition`); after that the tail
  counts down whatever the fingers do. **Live Wire** (CHARGE level 5,
  `ShieldSkimmerScaleConfigSO.liveWireUpgradeElement` = Charge, gated through
  `IsUpgradeActive`) restores the refresh: every return to the stance holds the blade lit again.
- **Who decides (2026-10-16).** The machine that OWNS the vessel (the pilot's, or the server
  for an AI) runs this state machine; `RhinoSwordNetworkRelay` (a `NetworkBehaviour` on the
  Rhino root, owner-write `n_energy` + `n_state` — the phase nibble plus a surge bit) publishes
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

**Elongation interaction:** the energy meter and the surge burst change the blade's length
every frame, and `SkimmerSwingKinematics` can optionally count that elongation as tip velocity
(`includeElongation`, **off** in `RhinoSwordSwingKinematicsConfig.asset`). Leave it off: the
burst's grow speed (600 u/s) would read as an enormous phantom tip velocity for the ~0.2 s it
is expanding, and a parked-but-charging sword must impart exactly what the hull does.

**Length is the lever arm — the intended emergent loop.** Even with elongation off, the blade's
live length still bounds `ClosestBladePoint`, so it *is* the lever arm `r` in `ω × r`. Banking
energy lengthens the blade, which makes every subsequent tip strike genuinely faster, which kills
more mass, which banks more energy. That positive loop is the reward curve, not a defect — it is
bounded (length clamps to `MaxScale`, debris clamps to `debrisSpeedLimit` 200, and each energize
spends 0.1). *Until 2026-10-16 an elemental crystal also drained the meter to zero; that burst is
deleted, so the only drain left is the energize cost* — and Charge now speeds the loop up (energy
per cut ×1 → ×2). It falls out of the two systems composing rather than
being scripted anywhere.

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

## Energy = the Shield resource (index 1)

"Energy" is the Rhino's **Shield** `Resource` (`ResourceSystem.Resources[1]`, normalized `0..1`,
`MaxAmount = 1`, passive gain `0`). It has **no passive decay** — a meter you fill and spend,
not an oscillator (the old driver's tick-decay loop is gone).

- **Gain:** each prism the sword **destroys** banks `energyPerPrism` (`0.04` → ~25 kills to
  full at rest) on `RhinoSkimmerDamagePrismEffect.asset`; popping a super-shielded prism banks
  `energyPerSuperShieldedPrism` (`0.12` — hardened targets are worth more). **Both are
  multiplied by CHARGE** (2026-10-16, `chargeEnergyGainMultiplier`: ×1 at rest → ×2 at level 10,
  floored ×0.5 — the Rhino's Charge row, "Sword Energy"). A hit that merely de-shields a prism
  banks nothing (the prism survived). Only the owning machine banks (`AddEnergy` is a no-op off
  the authority); peers read the owner's meter from the relay. An **omni** crystal on the hull
  runs the **Surge** (§ below), which raises the meter to 1. *(Until 2026-10-16 that was
  `RhinoVesselChangeResourceByCrystalEffect`, a bare meter fill, now replaced.)*
- **Spend:** each ENERGIZE costs `energizeCostFraction` (0.1) at the ignition instant. Nothing
  else spends it. *(Until 2026-10-16 an **elemental** crystal collected by the sword triggered a
  burst that drained ALL energy to 0 — on every petal the Rhino flew through. Deleted; elemental
  crystals are now collected by the sword with no sword-side effect.)*
- **Readout:** the blade's **resting length** reflects stored energy (Y-only elongation from the
  Space-driven elemental base `Skimmer.LiveElementalScale`, 30 at rest → 50 at Space 10, toward
  `MaxScale` 120), and the blade **brightens** toward `fullEnergyBrightness` (1.8) — energy is
  brightness, never hue (`restingBladeColor` and `fullEnergyColor` are both white; the danger
  red is reserved for the energized blade, § "Blade FX"). *(An earlier pass ramped it teal → hot
  cyan; stale.)* The Rhino HUD keeps reading the same meter through
  `ShieldSkimmerScaleDriver.OnScaleChanged(current, base, max)`, and since 2026-10-16 the
  **Charge card** of the ability row is lit while the blade is energized, with an
  energize-cooldown veil; since 2026-10-17 it stays lit through the 5 s tail after the stance is
  let go and its authored gauge fills with energy 0..1.

## The omni-crystal Surge (the burst look moved here, 2026-10-16)

An **omni** crystal collected by the hull runs `RhinoVesselSurgeByCrystalEffectSO`
(`RhinoVesselSurgeByCrystalEffect.asset`, in `RhinoImpactorDataContainer`'s crystal list beside
the unchanged `RhinoVesselExplosionByCrystalEffect` blast), which calls
`IRhinoSwordState.Surge(surgeSeconds, energyFill)`:

- the energy meter is raised to at least `energyFill` (1);
- the blade **bursts** in all three dimensions (below) and is held **ENERGIZED** for
  `surgeSeconds` (6 s) with no stance and no cost, overriding a cooldown;
- every element is lent `elementBoost` (0.2 normalized = two levels), decaying to zero over the
  surge, never touching the base level;
- the Mass **afterimages** lay throughout the surge (they otherwise wait for an energize to end;
  `RHINO_SWORD_AFTERIMAGE.md`).

When the surge ends it hands over to the ordinary rules with the tail already spent, so the
blade goes out the next frame (unless Live Wire is active and the stance is held), and the
normal 5 s afterimage window follows. The surge is owner-authoritative (`Surge` is a no-op off
the authority); the relay's surge bit replays the burst on every other peer. Omni-crystal
collection resolves on the server and replays on a remote pilot's own machine
(`OmniCrystalImpactor.ReplayOnRemoteOwner`); the elemental half lands where every crystal's
elemental effect lands — the server and the owner.

**The burst** (`ShieldSkimmerScaleDriver.StartBurst`) swells the blade to
`authoredSilhouette × Lerp(1, crystalBurstFactorAtFullEnergy, energy)` (factor 4; the surge has
just filled the meter, so in practice the full factor), holds `crystalBurstHoldSeconds` (2.5),
and eases back. It **spends nothing**. A burst only ever GROWS: the length target is floored at
the blade's current length (a Space-lengthened blade must not contract) and capped at
`MaxScale` (the authored 120 — the Sparrow debuff that once lowered it is gone) unless the blade
is already longer. Whole-blade crackle + camera shake scale with the energy at that instant.
During the burst the HUD meter reports the energy-based resting length, not the transient
ballooned size.

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

1. **Heat ramp — energy is BRIGHTNESS, never hue.** The blade rests at `restingBladeColor`
   (white-hot) and brightens toward `fullEnergyBrightness` as energy fills; `fullEnergyColor`
   shares the resting hue on purpose. **The blade is never the pilot's domain colour** — it
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
   (`deniedSparkIntensity`) when a non-energized blade binds in a super-shield. The surge
   burst fires a whole-blade crackle scaled by the energy at that instant.
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
super-shield pop; up to `burstShakeMaxIntensity` (2.5, scaled by energy) on a surge burst. Routed through `CameraManager.Instance.GetActiveController()` →
`CustomCameraController.Shake` (the `AstroLeagueBall.ShakeCamera` pattern).

**No new haptics** — the two-feel policy stands (`Docs/HAPTICS.md`); the sword already carries
the skim pulse via `SkimmerHapticsByPrismEffect`. Audio is all free-riding on sanctioned paths:
`DeactivateShields` plays `ShieldDeactivate`, `Damage` plays `BlockDestroy`,
`AOEExplosion.Detonate` plays `Explosion`. (A dedicated energize SFX would follow the
`SkimmerSFXByCrystalEffectSO` template — follow-up, not wired.)

### The capsule crackle (adapting the forcefield-crackle system to the blade)

`ForcefieldCrackleController` + `ForcefieldCrackle.hlsl` were purpose-built for the base
skimmer's SPHERE: impacts stored as unit directions, ripples measured as great-circle angles.
On the blade's stretched capsule (built-in capsule mesh under the ~`(1.5, 30, 4.8)` blade
scale) that collapses the whole blade length into the two poles. The adaptation:

- **`ForcefieldCrackleCapsule_float`** (new entry in the same HLSL) measures in **world units**:
  impacts are object-space POSITIONS, the vertex shader supplies the object→world scale per
  axis, and distances are computed on scale-multiplied positions — so a ripple travels the same
  world distance along the blade as around it, arcs stay glued to the blade through swings, and
  they stretch with it as energy grows it. `_ImpactParams.y` becomes the ripple's world-unit
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
`Rhino.prefab`), which implements the slim `IRhinoSwordState` (`IsEnergized`, `Energy01`,
`IsSurging`, `AddEnergy`, `NotifyPrismDestroyed`, `NotifyPopDenied`, `Surge`,
`IsOwnAfterimage`, `SetInStance` — `TriggerCrystalBurst` went with the per-petal burst on
2026-10-16) and registers itself on `Skimmer.SwordState`. Effects read it via
`impactor.Skimmer.SwordState`, null-safe so any non-Rhino skimmer reusing the assets just runs
the damage behavior (and the bind entry beat, since it can never be energized) without the
energy/FX bookkeeping. The driver owns STATE (energy, energize machine, scale, burst phases) —
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
| Sword brain (energy, energize state machine, scale, burst phases, contact re-dispatch) | `Executors/ShieldSkimmerScaleDriver.cs` |
| Blade look (heat ramp, energize blend, ignition/spark crackle, flashes, tracers, shake) | `Executors/RhinoSwordFXController.cs` (prefab-authored on the blade root) |
| Gesture source (stance feed from the reparameterized triggers) | `Executors/ShieldSwipeActionExecutor.cs` (`FeedSwordStance`) |
| Gesture thresholds | `Data Containers/RhinoShieldSwipeConfigSO.cs` → `_SO_Assets/VesselActions/Rhino/RhinoShieldSwipeConfig.asset` |
| Tuning (scale mapping, energize, burst, all FX knobs) | `Executors/ShieldSkimmerScaleConfigSO.cs` → `_SO_Assets/VesselActions/Rhino/ShieldSkimmerScaleConfig.asset` |
| Prism effect (damage, energize-gated super-shield pop, energy bank, the cut plane) | `ImpactEffects/EffectsSO/Skimmer Prism Effects/RhinoSkimmerDamagePrismEffectSO.cs` → `_SO_Assets/Effects/Vessel Prism Effects/RhinoSkimmerDamagePrismEffect.asset` |
| The slice (death visual) | `Utility/Effects/PrismSlice.cs` (batch/budget/retire) · `Utility/PrismSliceGeometry.cs` (the stamps) · `_Graphics/Materials/Graphs/PrismSlice.hlsl` + `PrismSlice.shader` · `_Graphics/Materials/PrismSliceMaterial.mat` · `Resources/PrismSliceConfig.asset` · `Vessel/Prism.cs` (`Slice`) · `Prisms/PrismFactory.cs` (`SpawnExplosion` routes it) |
| Owner-authoritative replication (energy, energize phase, surge bit) | `Controller/Vessel/RhinoSwordNetworkRelay.cs` (on the Rhino root) |
| Omni-crystal Surge (fill, burst, 6 s energized, +2 levels to every element) | `ImpactEffects/EffectsSO/Vessel Crystal Effects/RhinoVesselSurgeByCrystalEffectSO.cs` → `_SO_Assets/Effects/Vessel Crystal Effects/RhinoVesselSurgeByCrystalEffect.asset` (in `RhinoImpactorDataContainer`). *The sword-side `RhinoSwordCrystalBurstEffectSO` + asset were deleted 2026-10-16.* |
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
`energyPerPrism` 0.04 · `energyPerSuperShieldedPrism` 0.12 · `chargeEnergyGainMultiplier`
(Charge, ×1 → ×2, floor ×0.5 — new 2026-10-16) · bounce params
`recoilWhenDenied` 0 (the bind is shipped; on = the legacy recoil below) · legacy recoil params
(`bounceSpeedMultiplier` 0.85 / `minBounceSpeed` 10 / `bounceDurationSeconds` 0.35 — a FIXED
recoil window; the old `accelScale` passed `Time.deltaTime` into the modifier's duration, making
the shove ~4× stronger at 30 fps than 120 fps) · plus the
swing-model group `swingVelocityScale` 1 / `maxImpactSpeed` 0 / `proportionalDebris` 1 /
`restitution` 0.333 / `debrisSpeedLimit` 200 (the last two move **together** with the other
damage SOs and `PrismExplosion.prefab`'s speed band — see the swing-kinematics row in
`CLAUDE.md`; `inertia` is not the lever on the proportional path).

On `ShieldSkimmerScaleConfig.asset`: `baseScale` 30 (fallback; live base is the Space elemental
scale) · `maxScale` 120 · `prismGrowSpeed` 30 · `shrinkSpeed` 10 · `energizeCostFraction` 0.1 ·
`energizeHoldSeconds` 1 · `touchEnergizeRateMultiplier` 2 (phone hold = 0.5 s) · `energizedTailSeconds` 5 · `energizeCooldownSeconds` 5 ·
`liveWireUpgradeElement` Charge (the L5 that lets the stance re-light the tail) ·
`crystalBurstFactorAtFullEnergy` 4 (the surge burst — the names predate the move) · `crystalBurstHoldSeconds` 2.5 · `crystalBurstGrowSpeed`
600 · `crystalBurstReturnSpeed` 150 · `visibilityMultiplier` 1.2 · `restingBladeColor` white ·
`fullEnergyColor` white (same hue — energy is brightness) · `fullEnergyBrightness` 1.8 ·
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

On `RhinoVesselSurgeByCrystalEffect.asset`: `surgeSeconds` 6 · `energyFill` 1 · `elementBoost`
0.2 (two levels to each element, decaying over the surge). The burst's size and timing are the
`crystalBurst*` keys on `ShieldSkimmerScaleConfig.asset` above. (`RhinoSwordCrystalBurstEffect.asset`,
which had no knobs, is deleted.)

## In-editor verification

1. Rhino in any playable mode (or Menu_Main freestyle, swap to Rhino) with a gamepad.
2. **Cutting (regression):** fly through trail/opposing prisms — every prism the blade touches
   pops immediately, with a small blade flash + a spark at the blade point that hit. No timing,
   no stance required.
3. **Energy + length + heat:** as kills accumulate the blade lengthens (HUD skimmer meter
   tracks) and brightens (white, no hue change). No decay while idle. At a high Charge level the
   meter fills visibly faster per kill (up to ×2).
4. **Energize ritual:** bank some energy, pull BOTH triggers fully and hold the centered chop —
   after ~1 s of rising anticipation arcs the blade IGNITES: danger red + a crackle burst along
   the whole blade, energy dips by 0.1, and the Charge card lights. Holding the stance from
   ignition keeps it lit. Release the stance — the blade stays lit ~5 s, cools, and can't
   re-charge for ~5 s more. **Re-taking the stance during the tail must NOT re-light it**
   (2026-10-16); with Charge at level 5 (Live Wire) it must.
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
6. **Super-shield, energized:** same prism with the blade lit — it pops on contact (24-face
   shatter + explode-out), big blade flash + short camera shake, energy banks 0.12.
7. **The resting-prism edge:** park the blade inside a super-shielded prism (bound), then
   energize while still touching — the prism must pop the instant ignition lands, no re-approach
   needed. A merely-SHIELDED prism resting in the blade at ignition must keep its core (no free
   Breaker).
8. **Crystals:** sword-collect an elemental crystal (fly through ejected petals) — the petal is
   collected, **the blade does nothing and the meter does not move** (the per-petal burst is
   deleted). Then fly the hull through an **omni** crystal — the Surge: meter to full, the blade
   bursts in all three dimensions with whole-blade crackle and camera shake, stays energized
   ~6 s with no stance, the elements read two levels higher and fade back, afterimages lay
   throughout; when it ends the blade goes out and the ordinary 5 s afterimage window follows.
9. **Tracers:** five streaks comb the blade from tip to hilt through swipes, tinted with the
   live blade colour (white → danger red when energized).
9a. **Multiplayer (MPPM two clients):** a half-pull sweep on A poses A's blade identically on B
    (B poses from A's replicated analog mirrors); A's energize ignites on B on the same edge and
    B's copy of A's meter tracks A's. A super-shielded prism A pops is popped on B too.
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
- **Mode lining:** if a mode's super-shielded lining (Skim Race track, Astro League edge) must
  survive an energized Rhino, decide a per-mode carve-out then — do not re-gate ordinary cutting.
- **Friendly fire + self-farming (design sign-off):** the effect has no domain gate — identical
  to the generic damage effect it replaced (`affectSelf` is true on the Rhino) and consistent
  with the danger-prism friendly-fire philosophy — but the energy bank makes it rewarding:
  cutting your own trail banks 0.04/prism, and an energized blade popping an **ally's**
  super-shielded prism banks 0.12 while permanently devastating friendly hardened structure. If
  that reads as an exploit in play, add a same-domain skip on the energy bank (not on the
  damage) or gate the pop by domain.
  **The self-trail contact grace does not change this** (`ImpactEffects/SELF_TRAIL_CONTACT.md`):
  it is owner-scoped and lasts ~1 s from the moment a prism is laid, and the Rhino cannot come
  about onto its own freshest ribbon inside that window — so the grace formally covers the sword
  but never fires for it, and the self-farm loop above is intact. The guidance stands: skip the
  ENERGY BANK, never the damage.
- **Energize SFX:** a dedicated ignition sound via the `SkimmerSFXByCrystalEffectSO` template if
  the free-riding audio isn't enough.
