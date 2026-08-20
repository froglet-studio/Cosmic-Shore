# Rhino Energy Sword — ungated blade, the energize ritual, energy meter, crystal burst

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
| Normal prism | any | Explodes, debris thrown at the **contact velocity** (below) |
| Shielded prism | any | Shield pops, prism survives (standard `Prism.Damage` semantics) |
| **Super-shielded prism** | **ENERGIZED** | **POPPED**: `DeactivateShields()` (stellation shatter + SFX) then `Damage(devastate: true)` (animated explode-out, unrestorable) — the sanctioned mass-conserving teardown, same sequence as `AstroLeagueArena.ClearEdgeLining`. `Prism.Damage` alone hard-ignores super-shielded prisms, which is why the shields must drop first. |
| **Super-shielded prism** | not energized | **BounceBack** recoil + a dim **denied spark** at the contact point (teaches the ritual without rewarding the hit) |

`popRequiresEnergizedBlade` (default **on** — the ritual IS the design) replaces v2's
`destroySuperShielded`: flip it off on the asset to restore the v2 ungated pop as a designer
A/B. With no sword state present (a non-Rhino skimmer reusing the asset) the blade can never be
energized, so super-shielded prisms always bounce — the pre-sword baseline.

Consequence to be aware of: super-shielded prisms are used as track/arena lining (Skim Race,
Astro League edge). An energized Rhino can carve those — deliberately a *paid, windowed* act now
rather than free on contact. If a mode must protect its lining outright, that's a follow-up
(per-mode effect container or a prism-level carve-out decided then, not a silent re-gate of
ordinary cutting).

## The energize ritual (the supershield key)

Hold the **lower/chop stance** — both triggers pulled (sum ≥ `stanceSumThreshold`, 1.5) and even
(|difference| ≤ `stanceCenterEpsilon`, 0.4) — for `energizeHoldSeconds` (1 s):

```
Idle ── stance held, energy ≥ cost, off cooldown ──► Charging (anticipation arcs, blade leans white)
Charging ── stance broken or energy dips ──► Idle (no cost)
Charging ── hold met ──► ENERGIZED (spends energizeCostFraction = 0.1; IGNITION burst)
Energized ── stance held ──► stays lit indefinitely
Energized ── stance left ──► tail: stays lit energizedTailSeconds (5 s)
tail elapsed ──► Cooldown (energizeCooldownSeconds, 5 s) ──► Idle
```

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
- **Binary inputs energize too:** DualMouse writes 0/1 mirrors, so both held = sum 2. (Touch has
  no swipe bindings today; a future touch binding should write the trigger mirrors to join in.)
  The event path's both-held synthesis (`diff 0, sum 2`) remains for the POSE only — remote
  peers see the owner's centered chop instead of a one-sided swipe. AI never pulls triggers, so
  AI Rhinos never energize (same limitation class as the analog swipe pose).
- **A turn end / despawn / autopilot takeover mid-hold** drops the stance (`ResetImmediate` →
  `SetInStance(false)`, plus `FeedSwordStance`'s own owner-side autopilot guard — a paused
  `InputController` FREEZES the mirrors rather than zeroing them), so the local sword can't be
  left charging forever. The frozen mirrors' remote-side residual (a peer's replica reading
  stale held values after the owner entered the lava lamp mid-hold) is the same acknowledged
  class as the remote pose latch and is owned by the replication follow-up below.
- **The resting-prism edge (v1's lesson, solved for BOTH contact tiers):** a super-shielded
  prism already resting against the blade when ignition lands gets no fresh `OnTriggerEnter` —
  and since the shell tier owns shielded contact, its pair was dispatched once on ENTRY and
  would never re-fire. The ignition edge therefore re-dispatches standing contacts through both
  tiers: `SkimmerImpactor.ReapplyPrismEffectsToOverlapping()` (box-trigger overlaps, which also
  covers `ForceLegacyBoxInteraction` mode) and the new
  `PrismShellContactManager.RedispatchPairsForOwner(impactor)` (live shell-owned pairs re-run
  through the same `AcceptImpacteeFromShellContact` chain their entry used). The pop the player
  just paid for lands the same frame.


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
  sampler differentiates — slides along the axis as the blade lengthens, at up to the crystal
  burst's 600 u/s. That is real motion of the transform but it is not a strike, and counting it
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

**Elongation interaction:** the energy meter and the crystal burst change the blade's length
every frame, and `SkimmerSwingKinematics` can optionally count that elongation as tip velocity
(`includeElongation`, **off** in `RhinoSwordSwingKinematicsConfig.asset`). Leave it off: the
burst's grow speed (600 u/s) would read as an enormous phantom tip velocity for the ~0.2 s it
is expanding, and a parked-but-charging sword must impart exactly what the hull does.

**Length is the lever arm — the intended emergent loop.** Even with elongation off, the blade's
live length still bounds `ClosestBladePoint`, so it *is* the lever arm `r` in `ω × r`. Banking
energy lengthens the blade, which makes every subsequent tip strike genuinely faster, which kills
more mass, which banks more energy. That positive loop is the reward curve, not a defect — it is
bounded at both ends (length clamps to `MaxScale`, debris clamps to `debrisSpeedLimit` 200, and a
crystal drains the meter to zero), and it falls out of the two systems composing rather than
being scripted anywhere.

## Energy = the Shield resource (index 1)

"Energy" is the Rhino's **Shield** `Resource` (`ResourceSystem.Resources[1]`, normalized `0..1`,
`MaxAmount = 1`, passive gain `0`). It has **no passive decay** — a meter you fill and spend,
not an oscillator (the old driver's tick-decay loop is gone).

- **Gain:** each prism the sword **destroys** banks `energyPerPrism` (default `0.04` → ~25 kills
  to full) on `RhinoSkimmerDamagePrismEffect.asset`; popping a super-shielded prism banks
  `energyPerSuperShieldedPrism` (default `0.12` — hardened targets are worth more). A hit that
  merely de-shields a prism banks nothing (the prism survived). The pre-existing
  `RhinoVesselChangeResourceByCrystalEffect` (vessel body collects an **omni** crystal) still
  sets the resource straight to 1 — an instant full charge.
- **Spend:** each ENERGIZE costs `energizeCostFraction` (0.1) at the ignition instant; collecting
  an **elemental** crystal with the sword (skimmers are what collect elemental crystals) triggers
  the crystal burst below and drains ALL energy to 0.
- **Readout:** the blade's **resting length** reflects stored energy (Y-only elongation from the
  Space-driven elemental base `Skimmer.LiveElementalScale` toward `MaxScale`), and the blade
  **heats** — colour ramps authored-teal → `fullEnergyColor` (a hot cyan — the WHITE-hot look is
  reserved for the energized blade) and brightens toward `fullEnergyBrightness`. The Rhino HUD
  keeps reading the same meter through `ShieldSkimmerScaleDriver.OnScaleChanged(current, base,
  max)`.

## Crystal burst (all three dimensions + explosion)

When the sword collects an elemental crystal, `RhinoSwordCrystalBurstEffectSO` (the sword
container's only crystal effect) reads the energy at that instant and:

1. Spawns an `AOEExplosion` **at the crystal**, `MaxScale = Lerp(minExplosionScale,
   maxExplosionScale, energy)` (60 → 400; the wired `AOESlowExplosion` also slows victims).
2. Calls `IRhinoSwordState.TriggerCrystalBurst()`, which bursts the blade in **all three
   dimensions** to `authoredSilhouette × Lerp(1, crystalBurstFactorAtFullEnergy, energy)`
   (default factor 4), holds `crystalBurstHoldSeconds`, eases back — and **drains ALL energy**,
   so the blade settles back to its base length. A burst only ever GROWS: the length target is
   floored at the blade's current length (a Space-lengthened blade at low energy must not
   contract) and capped at the debuff-aware `MaxScale` (so the Sparrow shrink debuff still
   bites during a burst) unless the blade is already longer.

At full energy: max-size burst + max explosion + whole-blade crackle + max camera shake. At zero
energy: a small 60-unit pop and no burst. Banked kills literally convert into the boom. During
the burst the HUD meter reports the energy-based resting length (i.e. it honestly drops to empty
at the drain), not the transient ballooned size. Living lifeforms' **embedded** heart crystals
never trigger the burst — `SkimmerImpactor` gates its crystal effects on collectable crystals
(`IsEmbedded`/`IsExploding`), mirroring `ElementalCrystalImpactor`'s own guards.

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
   (`deniedSparkIntensity`) when a non-energized blade bounces off a super-shield. The crystal
   burst fires a whole-blade crackle scaled by the energy consumed.
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
super-shield pop; up to `burstShakeMaxIntensity` (2.5, scaled by energy consumed) on a crystal
burst. Routed through `CameraManager.Instance.GetActiveController()` →
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
  **`RhinoBladeCrackleMaterial.mat`** (teal/white family) is authored on the blade's
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
`AddEnergy`, `NotifyPrismDestroyed`, `NotifyPopDenied`, `TriggerCrystalBurst`, `SetInStance`)
and registers itself on `Skimmer.SwordState`. Effects read it via `impactor.Skimmer.SwordState`,
null-safe so any non-Rhino skimmer reusing the assets just runs the damage behavior (and the
bounce, since it can never be energized) without the energy/FX bookkeeping. The driver owns
STATE (energy, energize machine, scale, burst phases) and delegates all LOOK to the
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
| Prism effect (damage, energize-gated super-shield pop, energy bank) | `ImpactEffects/EffectsSO/Skimmer Prism Effects/RhinoSkimmerDamagePrismEffectSO.cs` → `_SO_Assets/Effects/Vessel Prism Effects/RhinoSkimmerDamagePrismEffect.asset` |
| Crystal effect (explosion + burst kick) | `ImpactEffects/EffectsSO/Skimmer Prism Effects/RhinoSwordCrystalBurstEffectSO.cs` → `_SO_Assets/Effects/Skimmer Crystal Effects/RhinoSwordCrystalBurstEffect.asset` |
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
| Effect wiring | `_SO_Assets/Effects/Effect Containers/SkimmerContainers/RhinoForceFieldSkimmerImpactorDataContainer.asset` (prism[0] → Rhino variant; crystal list → burst effect) |
| Prefab wiring | `Rhino.prefab` (FX controller on the blade root + `RhinoSwordBladeTracer0..4` under the fuselage; sword mount lowered to y 2) · `ForceFieldSkimmer Variant.prefab` (overlay material → blade crackle, `surface: Capsule`, `lengthScale: 2`) |

## Tuning knobs

On `RhinoShieldSwipeConfig.asset`: `stanceSumThreshold` 1.5 · `stanceCenterEpsilon` 0.4 ·
`swipeCooldownSeconds` 0.35 · `swipeEngageThreshold` 0.4.

On `RhinoSkimmerDamagePrismEffect.asset`: `inertia` 70 · `popRequiresEnergizedBlade` 1 ·
`energyPerPrism` 0.04 · `energyPerSuperShieldedPrism` 0.12 · bounce params
(`bounceSpeedMultiplier` 0.85 / `minBounceSpeed` 10 / `bounceDurationSeconds` 0.35 — a FIXED
recoil window; the old `accelScale` passed `Time.deltaTime` into the modifier's duration, making
the shove ~4× stronger at 30 fps than 120 fps) · plus the
swing-model group `swingVelocityScale` 1 / `maxImpactSpeed` 0 / `proportionalDebris` 1 /
`restitution` 0.333 / `debrisSpeedLimit` 200 (the last two move **together** with the other
damage SOs and `PrismExplosion.prefab`'s speed band — see the swing-kinematics row in
`CLAUDE.md`; `inertia` is not the lever on the proportional path).

On `ShieldSkimmerScaleConfig.asset`: `baseScale` 30 (fallback; live base is the Space elemental
scale) · `maxScale` 120 · `prismGrowSpeed` 30 · `shrinkSpeed` 10 · `energizeCostFraction` 0.1 ·
`energizeHoldSeconds` 1 · `energizedTailSeconds` 5 · `energizeCooldownSeconds` 5 ·
`crystalBurstFactorAtFullEnergy` 4 · `crystalBurstHoldSeconds` 2.5 · `crystalBurstGrowSpeed`
600 · `crystalBurstReturnSpeed` 150 · `visibilityMultiplier` 1.2 · `restingBladeColor` white ·
`fullEnergyColor` white (same hue — energy is brightness) · `fullEnergyBrightness` 1.8 ·
`energizedColor` (1.498, 0.006, 0.007) = `SO_ColorSet.Danger` · `colorTransitionSeconds` 0.25 · `igniteCrackleIntensity` 2.5 / `igniteCrackleSeconds` 0.9 /
`igniteCrackleSites` 5 · `chargeCrackleInterval` 0.18 / `chargeCrackleIntensity` 1.1 ·
`sparkIntensity` 1.6 / `sparkSeconds` 0.45 / `sparkWorldRadius` 14 · `deniedSparkIntensity`
0.7 · tracer size is NOT here — it is authored on the five `RhinoSwordBladeTracer*`
TrailRenderers (hairline: `widthMultiplier` 0.5, `time` 0.15) · `hitFlashAmount` 0.35 · `popFlashAmount` 1 · `flashDecaySeconds` 0.35 · `flashColor`
(2,2,2) · `popShakeIntensity` 1.2 / `popShakeDuration` 0.25 · `burstShakeMaxIntensity` 2.5 /
`burstShakeDuration` 0.4. (`prismMaxScale` remains only so the Sparrow full-auto
`ApplyMaxSizeDebuff` keeps its historical meaning. The v2 tracer keys — `tracersEnabled`,
`tracerMaterial`, `tracerWidth`, `tracerTimeSeconds` — are retired: the tracer is an authored
TrailRenderer in `Rhino.prefab` now; tune its persistence, taper curve and material on the
component, and its overall width through `tracerWidthLengthFraction` above — the width
MULTIPLIER is driven from the blade's live length and cannot be authored.)

On `RhinoBladeCrackleMaterial.mat`: arc density/sharpness, ring thickness (fraction of reach),
ripple speed, core/glow/rim colors — live-tunable in the inspector, edit or play mode
(`ForcefieldCrackleController` is `[ExecuteAlways]`).

On `RhinoSwordCrystalBurstEffect.asset`: `minExplosionScale` 60 · `maxExplosionScale` 400 ·
`aoePrefabs` = AOESlowExplosion.

## In-editor verification

1. Rhino in any playable mode (or Menu_Main freestyle, swap to Rhino) with a gamepad.
2. **Cutting (regression):** fly through trail/opposing prisms — every prism the blade touches
   pops immediately, with a small blade flash + a spark at the blade point that hit. No timing,
   no stance required.
3. **Energy + length + heat:** as kills accumulate the blade lengthens (HUD skimmer meter
   tracks) and shifts teal → hot cyan. No decay while idle.
4. **Energize ritual:** bank some energy, pull BOTH triggers fully and hold the centered chop —
   after ~1 s of rising anticipation arcs the blade IGNITES: white-hot + a crackle burst along
   the whole blade, energy dips by 0.1. Release the stance — the blade stays lit ~5 s, cools,
   and can't re-charge for ~5 s more. Holding the stance keeps it lit indefinitely.
5. **Super-shield, not energized:** slash a super-shielded (Stella-Octangula) prism — the Rhino
   recoils (bounce) with a dim spark; the prism survives.
6. **Super-shield, energized:** same prism with the blade lit — it pops on contact (24-face
   shatter + explode-out), big blade flash + short camera shake, energy banks 0.12.
7. **The resting-prism edge:** park the blade against a super-shielded prism (bounce), then
   energize while still touching — the prism must pop the instant ignition lands, no re-approach
   needed.
8. **Crystal:** with partial vs full energy, sword-collect an elemental crystal — blade bursts
   in all three dimensions, whole-blade crackle + explosion at the crystal, both bigger at
   higher energy; energy drops to 0 and the blade eases back to base length.
9. **Tracers:** two streaks ride the blade tips through swipes, tinted with the live blade
   colour (teal → cyan → white-hot when energized).
10. **Non-regression:** other vessels' skimmers unaffected (SwordState null; base skimmer
    crackle still the red sphere look); Sparrow full-auto still shrinks the Rhino sword's max
    (`ApplyMaxSizeDebuff`); the omni-crystal pickup still snaps the meter full; touch/binary
    input can energize by holding both swipe controls.

## Follow-ups

- **Replication:** energy, energize state, and the blade's heat/flash look are
  local-authoritative. The STANCE now converges across peers (evaluated from the replicated
  trigger mirrors), but each machine still runs its own energize machine against its own
  locally-banked energy, so ignition timing can differ when peers' energy tallies differ — and
  a paused owner's frozen mirrors can hold a remote replica in-stance (the lava-lamp mid-hold
  edge). The complete fix is an owner-write NetworkVariable for energy + energize phase on the
  driver, mirroring the analog-replication follow-up in `RHINO_SHIELD_SWIPE.md`.
- **Sibling dt-as-duration pattern:** `VesselDeviationByPrismEffectSO` and
  `VesselSpinBySkimmerEffectSO` pass `Time.deltaTime * accelScale` into `ModifyVelocity`'s
  DURATION exactly the way the sword's bounce used to — the same frame-rate dependence, on
  paths this branch does not touch. Worth its own pass; not changed here.
- **HUD:** the energize phase is exposed (`ShieldSkimmerScaleDriver.EnergizePhase`, `Charge01`)
  but not yet drawn — the blade itself is the readout. If playtests want a HUD echo, feed the
  Rhino HUD from those properties.
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
