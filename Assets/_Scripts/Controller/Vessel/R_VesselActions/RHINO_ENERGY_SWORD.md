# Rhino Energy Sword — ungated blade, super-shield popping, energy meter, crystal burst

Layers an **energy meter** and a **super-shield-popping edge** onto the Rhino's swordsmanship
(the pose/analog-swipe rig is in `RHINO_SHIELD_SWIPE.md` — read it first). The blade is
**ungated**: no stance, no cooldown, no energize requirement. It always damages prisms on
contact and always destroys super-shielded prisms. A first iteration gated damage behind an
"energize" stance + a slash cooldown; that shipped a sword that mostly didn't cut and was
**rejected — do not reintroduce damage gates on the sword.**

## What the sword does on contact

| Target | Result |
|---|---|
| Normal prism | Explodes (standard `PrismEffectHelper.Damage`, as before) |
| Shielded prism | Shield pops, prism survives (standard `Prism.Damage` semantics) |
| **Super-shielded prism** | **POPPED**: `DeactivateShields()` (stellation shatter + SFX) then `Damage(devastate: true)` (animated explode-out, unrestorable) — the sanctioned mass-conserving teardown, same sequence as `AstroLeagueArena.ClearEdgeLining`. `Prism.Damage` alone hard-ignores super-shielded prisms, which is why the shields must drop first. |

`destroySuperShielded` (default **on** — popping IS the feature) preserves the legacy
bounce-off as a config fallback: flip it off on the asset to restore the old recoil.
Consequence to be aware of: super-shielded prisms are used as track/arena lining (Skim Race,
Astro League edge). A Rhino can now carve those. That is the intended universality — one rule
set — but if a mode must protect its lining, that's a follow-up (per-mode effect container or
a prism-level carve-out decided then, not a silent re-gate of the sword).

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
- **Readout:** the blade's **resting length** reflects stored energy (Y-only elongation from the
  Space-driven elemental base `Skimmer.LiveElementalScale` toward `MaxScale`), and the blade
  **heats** — colour ramps authored-teal → `fullEnergyColor` and brightens toward
  `fullEnergyBrightness`. The Rhino HUD keeps reading the same meter through
  `ShieldSkimmerScaleDriver.OnScaleChanged(current, base, max)`.
- **Spend:** collecting an **elemental** crystal with the sword (skimmers are what collect
  elemental crystals) triggers the crystal burst below and drains ALL energy to 0.

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

At full energy: max-size burst + max explosion + max camera shake. At zero energy: a small
60-unit pop and no burst. Banked kills literally convert into the boom. During the burst the
HUD meter reports the energy-based resting length (i.e. it honestly drops to empty at the
drain), not the transient ballooned size. Living lifeforms' **embedded** heart crystals never
trigger the burst — `SkimmerImpactor` gates its crystal effects on collectable crystals
(`IsEmbedded`/`IsExploding`), mirroring `ElementalCrystalImpactor`'s own guards.

## Blade FX (`RhinoSwordVisualizer`)

The blade body uses the **shared** `FresnelMaterial` (sole exposed property `_Color`, non-HDR
but unclamped), so the visualizer never touches `renderer.material` — everything goes through a
per-renderer **MaterialPropertyBlock** (the `AstroLeagueBall` impact-flash precedent; RGB > 1
feeds gameplay bloom, which is active in game scenes — menu has bloom off, where the blade still
brightens in LDR). **`FresnelGraph.shadergraph` was fixed with this feature**: its `_Color`
property fed a Blend node whose output connected to nothing (BaseColor came straight from the
Voronoi — every `_Color` write was invisible). The graph now multiplies `_Color` into the
animated Voronoi (`BaseColor = _Color × Voronoi`, alpha unchanged: fresnel-driven), so the base
blade reads teal instead of grayscale and the MPB drive below actually renders. The shader has
exactly one material (`FresnelMaterial.mat`) rendered by exactly one prefab (the Rhino's
ForceFieldSkimmer) — no other look changed.

- **Always visible:** authored teal × `visibilityMultiplier` (2).
- **Heat ramp:** colour lerps teal → `fullEnergyColor` and brightness lerps 1 →
  `fullEnergyBrightness` (2.5) by stored energy — power readable at a glance.
- **Impact flash:** a decaying white-out pulse per prism destroyed — `hitFlashAmount` (0.35) for
  a normal pop, `popFlashAmount` (1.0) for a super-shield pop or crystal burst, decaying over
  `flashDecaySeconds`; stronger pulses override weaker mid-decay.
- **Tip tracers:** two runtime `TrailRenderer` streaks seated at the blade tips (parented to the
  fuselage so width doesn't scale with the growing blade), tinted with the live blade colour.
  The material is an **instance of the config-authored `tracerMaterial`**
  (`TrailViewerMaterial.mat` — instanced so tinting never mutates the shared asset); a runtime
  unlit fallback covers a missing reference.
- **Camera shake (local pilot only, never autopilot/remote/AI):** `popShakeIntensity` (1.2) on a
  super-shield pop; up to `burstShakeMaxIntensity` (2.5, scaled by energy consumed) on a crystal
  burst. Routed through `CameraManager.Instance.GetActiveController()` →
  `CustomCameraController.Shake` (the `AstroLeagueBall.ShakeCamera` pattern).
- **No new haptics** — the two-feel policy stands (`Docs/HAPTICS.md`); the sword already carries
  the skim pulse via `SkimmerHapticsByPrismEffect`.

Audio is all free-riding on sanctioned paths: `DeactivateShields` plays `ShieldDeactivate`,
`Damage` plays `BlockDestroy`, `AOEExplosion.Detonate` plays `Explosion`.

## Architecture — how shared effect SOs reach per-vessel state

Effect SOs are singletons and can't hold per-vessel state. The per-Rhino state lives on
`ShieldSkimmerScaleDriver` (the sword's brain, one per Rhino, on `ScaleSkimmerObject` in
`Rhino.prefab`), which implements the slim `IRhinoSwordState` (`Energy01`, `AddEnergy`,
`NotifyPrismDestroyed`, `TriggerCrystalBurst`) and registers itself on `Skimmer.SwordState`.
Effects read it via `impactor.Skimmer.SwordState`, null-safe so any non-Rhino skimmer reusing
the assets just runs the damage/pop behavior without the energy/FX bookkeeping. The whole
feature is **code + flat asset edits — no new prefab components**.

## Files

| Role | File |
|---|---|
| Per-vessel state contract | `Executors/IRhinoSwordState.cs` |
| Sword brain (energy meter, scale, burst phases, flash/shake dispatch) | `Executors/ShieldSkimmerScaleDriver.cs` |
| Blade look (heat ramp, impact flash, tracers) | `Executors/RhinoSwordVisualizer.cs` |
| Tuning (scale mapping, burst, all FX knobs) | `Executors/ShieldSkimmerScaleConfigSO.cs` → `_SO_Assets/VesselActions/Rhino/ShieldSkimmerScaleConfig.asset` |
| Prism effect (damage, super-shield pop, energy bank) | `ImpactEffects/EffectsSO/Skimmer Prism Effects/RhinoSkimmerDamagePrismEffectSO.cs` → `_SO_Assets/Effects/Vessel Prism Effects/RhinoSkimmerDamagePrismEffect.asset` |
| Crystal effect (explosion + burst kick) | `ImpactEffects/EffectsSO/Skimmer Prism Effects/RhinoSwordCrystalBurstEffectSO.cs` → `_SO_Assets/Effects/Skimmer Crystal Effects/RhinoSwordCrystalBurstEffect.asset` |
| Arbitrary-position explosion overload | `ImpactEffects/EffectsSO/Helpers/ExplosionHelper.cs` (`CreateExplosion(prefabs, init, container)`) |
| Skimmer hook | `Vessel/Skimmer.cs` (`SwordState`) |
| Embedded-heart guard on skimmer crystal effects | `ImpactEffects/Impactors/SkimmerImpactor.cs` (ElementalCrystalImpactor case) |
| Blade shader fix (`_Color` now rendered) | `Assets/_Graphics/Materials/Graphs/FresnelGraph.shadergraph` |
| Effect wiring | `_SO_Assets/Effects/Effect Containers/SkimmerContainers/RhinoForceFieldSkimmerImpactorDataContainer.asset` (prism[0] → Rhino variant; crystal list → burst effect) |

Unchanged from bleeding-edge (the failed first pass touched these; the retry does not):
`ShieldSwipeActionExecutor`, `RhinoShieldSwipeConfigSO` + asset, `SkimmerImpactor` (no gesture
feed, no overlap re-apply — with no damage gate, `OnTriggerEnter` always bites).

## Tuning knobs

On `RhinoSkimmerDamagePrismEffect.asset`: `inertia` 70 · `destroySuperShielded` 1 ·
`energyPerPrism` 0.04 · `energyPerSuperShieldedPrism` 0.12 · legacy bounce params.

On `ShieldSkimmerScaleConfig.asset`: `baseScale` 30 (fallback; live base is the Space elemental
scale) · `maxScale` 120 · `prismGrowSpeed` 30 · `shrinkSpeed` 10 ·
`crystalBurstFactorAtFullEnergy` 4 · `crystalBurstHoldSeconds` 2.5 · `crystalBurstGrowSpeed`
600 · `crystalBurstReturnSpeed` 150 · `visibilityMultiplier` 2 · `fullEnergyColor` white ·
`fullEnergyBrightness` 2.5 · `hitFlashAmount` 0.35 · `popFlashAmount` 1 · `flashDecaySeconds`
0.35 · `flashColor` (3,3,3) · `tracersEnabled` · `tracerMaterial` (TrailViewerMaterial) ·
`tracerWidth` 2 · `tracerTimeSeconds` 0.3 · `popShakeIntensity` 1.2 / `popShakeDuration` 0.25 ·
`burstShakeMaxIntensity` 2.5 / `burstShakeDuration` 0.4. (`prismMaxScale` remains only so the
Sparrow full-auto `ApplyMaxSizeDebuff` keeps its historical meaning.)

On `RhinoSwordCrystalBurstEffect.asset`: `minExplosionScale` 60 · `maxExplosionScale` 400 ·
`aoePrefabs` = AOESlowExplosion.

## In-editor verification

1. Rhino in any playable mode (or Menu_Main freestyle, swap to Rhino) with a gamepad.
2. **Cutting (regression):** fly through trail/opposing prisms — every prism the blade touches
   pops immediately, with a small blade flash per kill. No timing, no stance required.
3. **Energy + length + heat:** as kills accumulate the blade lengthens (HUD skimmer meter
   tracks) and shifts teal → bright white. No decay while idle.
4. **Super-shield:** slash a super-shielded (Stella-Octangula) prism — it pops on contact
   (24-face shatter + explode-out), big blade flash + a short camera shake. No bounce.
5. **Crystal:** with partial vs full energy, sword-collect an elemental crystal — blade bursts
   in all three dimensions, an explosion fires at the crystal, both bigger at higher energy;
   energy drops to 0 and the blade eases back to base length.
6. **Non-regression:** other vessels' skimmers unaffected (SwordState null); Sparrow full-auto
   still shrinks the Rhino sword's max (`ApplyMaxSizeDebuff`); the omni-crystal pickup still
   snaps the meter full.

## Follow-ups

- **Replication:** energy and the blade's heat/flash state are local-authoritative (same
  precedent as the analog swipe pose — remote peers see the base blade at whatever scale their
  local effects produced). If the heat/flash look should replicate, add an owner-write
  NetworkVariable for energy on the driver, mirroring the analog-replication follow-up in
  `RHINO_SHIELD_SWIPE.md`.
- **Mode lining:** if a mode's super-shielded lining (Skim Race track, Astro League edge) must
  survive Rhino contact, decide a per-mode carve-out then — do not re-gate the sword globally.
- **Friendly fire + self-farming (design sign-off):** the effect has no domain gate — identical
  to the generic damage effect it replaced (`affectSelf` is true on the Rhino) and consistent
  with the danger-prism friendly-fire philosophy — but the NEW energy bank makes it rewarding:
  cutting your own trail banks 0.04/prism, and popping an **ally's** super-shielded prism banks
  0.12 while permanently devastating friendly hardened structure. If that reads as an exploit in
  play, add a same-domain skip on the energy bank (not on the damage) or gate the pop by domain.
- **Tracer look:** `TrailViewerMaterial` was chosen as the authored, serialized-reference-proven
  ribbon material; swap the config's `tracerMaterial` to taste.
