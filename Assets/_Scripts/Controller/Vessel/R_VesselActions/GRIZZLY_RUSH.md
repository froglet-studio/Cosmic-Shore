# Grizzly — Rush / Charge Forward (Button2)

Design source: `ClassGrizzly.md`. Element link: **Time**.

Spend Energy to RAM forward with a burst of momentum. For the burst's length the hull smashes every
rival prism within `ramRadius` (16 u) of it: plain and shielded break, super-shielded mass and a
silenced pilot's ram deflect (Prism.Damage's own gates, devastate on). Multiple banked charges, 2s
cooldown between rushes. The Grizzly's second mobility tool alongside riding its own explosions, and
now a weapon: rush through a wall. (2026-11-16, the weakest-abilities pass: the rush hit nothing.)

## Mechanics

- Burst = `VesselTransformer.ModifyVelocity(forward × Magnitude, Duration)` —
  the engine's velocity-modifier channel gives a cosine ease-out dash
  (starts ~1.5×, fades to 0.5×, expires; accumulation clamps at 100).
- Cost = `EnergyCost`, flat. Single Energy pool (same resource the cannon charges from —
  rushing trades firepower for position).
- **Time is the refill**: `refillSeconds` (ElementalFloat, 6 s at rest -> 2.5 s at Time 10, never under 1),
  read per charge. With the bank empty the Time card's veil counts the next charge back; every ram flashes the card.
- The ram sweeps (`ramTickSeconds`, 0.05 s) run on the vessel's authority only, so a prism breaks once.
- Charges: executor-instance state (`MaxCharges`, refill one per
  `ChargeRefillSeconds`) — NEVER stored on the shared SO asset. HUD pips via
  `OnChargesChanged(current, max)`.
- Rushing while dug in un-plants first (netvar-safe) and reconciles the dig-in
  executor's regen — dig in → bombard → rush out is the intended loop.
- Time 5 — **Vector Control**: the burst is split into `SteeringPulses` sub-pulses
  applied along the LIVE forward vector, so turning mid-rush genuinely redirects
  the charge. (The class doc listed alternatives — end-of-rush explosion, enhanced
  turn radius; steering was chosen as the default. Tunable.)
