# Rhino Energy Sword — energy economy, energize stance, slash cooldown, crystal burst

Layers an **energy economy** onto the Rhino's swordsmanship (the pose/analog-swipe rig is in
`RHINO_SHIELD_SWIPE.md` — read it first). The blade banks energy by destroying prisms and spends
it two ways: a **crystal burst** (a 3D size pop + explosion, both scaled by the energy consumed)
and an **energize** stance (lights the blade up, pops super-shields, removes the slash cooldown).

## Energy = the Shield resource (index 1)

"Energy" is the Rhino's **Shield** `Resource` (`ResourceSystem.Resources[1]`, normalized `0..1`,
`MaxAmount = 1`, passive gain `0`). One asset already reads it; this feature makes it the sword's
fuel. Max energy = `1.0`, so **1/10 of max energy = `0.1`** (the energize cost).

- **Gain:** each time a slash **destroys** a prism, `RhinoSkimmerDamagePrismEffectSO` banks
  `energyPerPrism` (default `0.04` → ~25 kills to full). The blade's **resting length reflects
  stored energy** (Y-only elongation from `BaseScale`→`MaxScale`), which is the same 0..1 meter the
  Rhino HUD draws through `ShieldSkimmerScaleDriver.OnScaleChanged`. Energy has **no passive
  decay** — it is a meter you fill and spend, not an oscillator.

## Spend 1 — Crystal burst (all three dimensions + explosion)

When the sword collects a crystal, `RhinoSwordCrystalBurstEffectSO` (a `SkimmerCrystalEffectSO`
on the sword's effect container) reads the energy at that instant and:

1. Spawns an `AOEExplosion` **at the crystal**, `MaxScale = Lerp(minExplosionScale,
   maxExplosionScale, energy)`.
2. Calls `IRhinoSwordState.TriggerCrystalBurst()`, which bursts the blade in **all three
   dimensions** to `authoredSilhouette × Lerp(1, CrystalBurstFactorAtFullEnergy, energy)`
   (default factor `4 = maxScale/baseScale`), holds `CrystalBurstHoldSeconds`, eases back, and
   **drains ALL energy to 0**.

At **full energy** the burst reaches the authored max scale (today's `maxScale` in length, ×4 in
width/depth) and the explosion its max size; **less energy scales both down** toward 1× / min.
Unlike the old crystal path this scales the **whole** blade (X/Y/Z), not just its length.

## Spend 2 — Energize (hold the lower/chop stance)

Holding the **lower position** — both triggers pulled, sword chopped straight down (sum near max,
difference near 0) — for `EnergizeHoldSeconds` (1 s) spends `EnergizeCostFraction` (0.1) of max
energy and **energizes** the blade. State machine (`ShieldSkimmerScaleDriver`):

```
Idle → Charging (hold ≥1s, energy ≥0.1) → Energized → Cooldown → Idle
```

- **Energized** stays lit while the stance is held; on **leaving** the stance it stays energized
  for `EnergizedTailSeconds` (5 s), then locks out for `EnergizeCooldownSeconds` (5 s) — **10 s
  total** from leaving the stance before it can be energized again.
- While energized: the blade goes **white**, its **edge tracers** recolour, it **pops
  super-shielded prisms**, and the **slash cooldown is 0** (a frenzy of slashes — the intended
  play is: hold to charge, leave, then slash wildly for 5 s).

## Slashing (left/right trigger) + cooldown

A **slash** is a single-trigger swipe (difference magnitude past `SlashTriggerThreshold`). The
sword damages a prism only when it `CanSlashDamage`:

| State | Normal prism | Super-shielded prism |
|---|---|---|
| **Energized** | pops on contact (cooldown 0) | **pops** (DeactivateShields → devastating Damage) |
| Not energized, slashing, off cooldown | pops, then **1 s** cooldown (`SlashCooldownSeconds`) | bounces (recoil) |
| Not energized, on cooldown / not slashing | no damage (pass through) | bounces |

So outside the energized window you must **time** each slash (one pop per second); during it you
mow through everything. Energy is banked on every pop (normal or super-shield).

Because damage fires once on `OnTriggerEnter`, a slash that opens with a prism **already inside**
the blade would miss it — so on a slash rising-edge the driver calls
`SkimmerImpactor.ReapplyPrismEffectsToOverlapping()`, which re-runs the prism effects against
everything currently in the trigger (the effects self-gate on `CanSlashDamage`, so it only bites
during a real slash).

## Blade visuals (`RhinoSwordVisualizer`)

The blade body uses the **shared** `FresnelMaterial`, so the visualizer never touches
`renderer.material` — it drives `_Color` through a **MaterialPropertyBlock**:

- **Twice as visible:** the authored teal is multiplied by `VisibilityMultiplier` (2) always.
- **Blue → white on energize:** `_Color` blends to `EnergizedColor` (HDR white) over
  `ColorTransitionSeconds`.
- **Edge tracers:** two runtime `TrailRenderer` streaks seated at the blade tips (parented to the
  fuselage so their width doesn't scale with the growing blade), recoloured with the body. Their
  material is created at runtime from an available URP/Sprites unlit shader (additive), and skipped
  cleanly if no such shader is in the build.

## Architecture — how shared effect SOs reach per-vessel state

Effect SOs are singletons and can't hold per-vessel state. The per-Rhino state lives on
`ShieldSkimmerScaleDriver` (the sword's brain, one per Rhino), which implements
`IRhinoSwordState` and registers itself on `Skimmer.SwordState`. Effects read it via
`impactor.Skimmer.SwordState`; the swipe executor via `status.NearFieldSkimmer.SwordState`. This
kept the whole feature to **code + flat asset edits — no new prefab components**.

## Files

| Role | File |
|---|---|
| Per-vessel state contract | `Executors/IRhinoSwordState.cs` |
| Sword brain (energy, energize, slash, crystal burst, scale) | `Executors/ShieldSkimmerScaleDriver.cs` |
| Blade look (2× visibility, blue→white, tracers) | `Executors/RhinoSwordVisualizer.cs` |
| Tuning (energy/energize/slash/burst/visual) | `Executors/ShieldSkimmerScaleConfigSO.cs` → `_SO_Assets/VesselActions/Rhino/ShieldSkimmerScaleConfig.asset` |
| Gesture thresholds (slash / stance) | `Data Containers/RhinoShieldSwipeConfigSO.cs` → `RhinoShieldSwipeConfig.asset` |
| Stance/slash signal source | `Executors/ShieldSwipeActionExecutor.cs` (`FeedSwordSignals`) |
| Prism effect (slash gate, energy gain, super-shield pop) | `ImpactEffects/EffectsSO/Skimmer Prism Effects/RhinoSkimmerDamagePrismEffectSO.cs` → `_SO_Assets/Effects/Vessel Prism Effects/RhinoSkimmerDamagePrismEffect.asset` |
| Crystal effect (explosion + burst + drain) | `ImpactEffects/EffectsSO/Skimmer Prism Effects/RhinoSwordCrystalBurstEffectSO.cs` → `_SO_Assets/Effects/Skimmer Crystal Effects/RhinoSwordCrystalBurstEffect.asset` |
| Overlap reapply | `ImpactEffects/Impactors/SkimmerImpactor.cs` (`ReapplyPrismEffectsToOverlapping`) |
| Skimmer hook | `Vessel/Skimmer.cs` (`SwordState`) |
| Effect wiring | `_SO_Assets/Effects/Effect Containers/SkimmerContainers/RhinoForceFieldSkimmerImpactorDataContainer.asset` (prism[0] → Rhino variant; crystal list → burst effect) |

## Tuning knobs (`ShieldSkimmerScaleConfig.asset` unless noted)

| Knob | Default | Meaning |
|---|---|---|
| `energyPerPrism` (on `RhinoSkimmerDamagePrismEffect.asset`) | 0.04 | energy banked per prism destroyed |
| `crystalBurstFactorAtFullEnergy` | 4 | uniform size factor at full-energy crystal hit |
| `crystalBurstHoldSeconds` | 2.5 | burst hold before easing back |
| `energizeCostFraction` | 0.1 | 1/10 of max energy to energize |
| `energizeHoldSeconds` | 1 | stance hold time to energize |
| `energizedTailSeconds` | 5 | stays energized after leaving the stance |
| `energizeCooldownSeconds` | 5 | lockout after the tail (tail+cooldown = 10 s) |
| `slashCooldownSeconds` | 1 | between slashes when not energized |
| `visibilityMultiplier` | 2 | blade brightness (twice as visible) |
| `energizedColor` | white (HDR) | blade colour when energized |
| `minExplosionScale`/`maxExplosionScale` (on `RhinoSwordCrystalBurstEffect.asset`) | 60 / 400 | crystal explosion size range (lerped by energy) |
| `slashTriggerThreshold` / `stanceSumThreshold` / `stanceCenterEpsilon` (on `RhinoShieldSwipeConfig.asset`) | 0.4 / 1.5 / 0.4 | gesture detection |

## In-editor verification

1. Rhino in any playable mode (or Menu_Main freestyle, swap to Rhino) with a gamepad.
2. **Gain + length:** slash trail/opposing prisms — the blade lengthens as energy banks; pops are
   rate-limited to ~1/s (time your slashes). The HUD skimmer meter tracks energy.
3. **Energize:** hold both triggers (blade chops down) ~1 s → blade turns **white**, tracers
   recolour, slashes go rapid-fire (cooldown 0). Leave the stance → still energized ~5 s, then
   reverts; can't re-energize for another ~5 s.
4. **Super-shield:** while energized, slash a super-shielded (Stella-Octangula) prism → it
   **pops** (shatter + explode-out); when not energized the Rhino **bounces** off it.
5. **Crystal:** with partial vs full energy, collect a crystal → blade bursts in **all three
   dimensions** and an explosion fires, both **bigger at higher energy**; energy drops to 0 and the
   blade eases back.

## Follow-ups

- **Replication:** energized state + slash gating are owner/local-authoritative (same precedent as
  the analog swipe pose — remote peers see the base blade). If the white/energized look or the
  gameplay gate should replicate, add owner-write NetworkVariables (an energized bool + energy) to
  the driver, mirroring the analog-replication follow-up in `RHINO_SHIELD_SWIPE.md`.
- **Own-trail:** the effect doesn't domain-gate, so an energized frenzy can clip your own trail if
  you loop back through it (pre-existing skimmer behaviour, now reduced to slash windows). Add a
  same-domain skip if it becomes a problem.
- **Tracer material:** created from whatever unlit shader the build ships; if a specific look is
  wanted, expose a serialized tracer material on the driver and wire it.
