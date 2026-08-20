# Grizzly — Dig In & Reload (Stop, Button1)

Design source: `ClassGrizzly.md`. Element link: **Charge**.

Halt movement to dig in; Energy regenerates faster while parked. Rewards the tank
for holding ground — the intended rhythm is dig in → bombard → rush/self-launch out.

## Mechanics

- Toggle on Button1. Translation restricted via `VesselController.SetTranslationRestricted`
  (rotation still free — you can aim while planted; shots leave along the gun's facing,
  see GRIZZLY_CHARGED_CANNON.md).
- Energy regen while planted: `initialResourceGainRate × StationaryGainMultiplier ×
  ElementalScaling.Multiplier(status, Charge, 3, 0.5)`.
- Prism spawn stops while planted (Sparrow turret-stance convention).
- Charge 5 — **Battle Sight** (stub): hook is `IsUpgradeActive(Element.Charge)`; the
  vision/highlight design is still open in the class doc.

## Stuck-state guarantees (the restoration branch's bug class, closed)

1. Un-plant ALWAYS routes through the controller so the `n_IsTranslationRestricted`
   netvar syncs — remote clients can never see a permanently planted ship. (The bare
   `IVesselStatus` write remains only as a local-vessel fallback.)
2. Gain-rate restore is idempotent — recomputed from `initialResourceGainRate`,
   never compounded — and runs on turn end, executor disable, AND re-Initialize.
3. External un-plants reconcile: being launched by your own blast
   (`VesselImpulseByExplosionEffectSO`) or rushing out (`GrizzlyRushActionExecutor`)
   calls `ReapplyRegen()`, which detects the stance loss and restores base regen.
4. Same-frame RPC-echo dedupe on toggle (`Time.frameCount` guard).
