# Grizzly — Dig In & Reload (Stop, Button1)

Design source: `ClassGrizzly.md`. Element link: **Charge**.

Halt movement to dig in and BRACE: while parked, Energy regenerates AND the charged cannon
winds up faster. Rewards the tank for holding ground — the intended rhythm is dig in → wind up →
bombard → rush/self-launch out. (2026-11-16, the weakest-abilities pass: the brace now reaches the
cannon, so digging in is felt on the next shot, and the Charge card stays lit while planted.)

## Mechanics

- Toggle on Button1. Translation restricted via `VesselController.SetTranslationRestricted`
  (rotation still free — you can aim while planted; shots leave along the gun's facing,
  see GRIZZLY_CHARGED_CANNON.md).
- One Charge dial, `GrizzlyDigInActionSO.dugInFill` (ElementalFloat, x2 at rest -> x5 at Charge 10, x1 at -5),
  multiplies BOTH the energy regen while planted (`initialResourceGainRate × fill`, re-read every frame so a crystal
  or debuff lands at once) and the charged cannon's wind-up (`GrizzlyChargedShotActionExecutor` reads
  `GrizzlyDigInActionExecutor.CannonChargeMultiplier`, 1 when not planted).
- HUD: the Charge card is held lit (pressed) while dug in and flashes as the anchor bites.
- Prism spawn stops while planted (Sparrow turret-stance convention).
- Charge 5 — **Battle Sight**: rivals within range are marked through mass while dug in (local pilot only).

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
