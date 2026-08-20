# Grizzly — Rush / Charge Forward (Button2)

Design source: `ClassGrizzly.md`. Element link: **Time**.

Spend Energy to charge forward with a burst of momentum. Multiple banked charges,
2s cooldown between rushes. The Grizzly's second mobility tool alongside riding
its own explosions.

## Mechanics

- Burst = `VesselTransformer.ModifyVelocity(forward × Magnitude, Duration)` —
  the engine's velocity-modifier channel gives a cosine ease-out dash
  (starts ~1.5×, fades to 0.5×, expires; accumulation clamps at 100).
- Cost = `EnergyCost × ElementalScaling.Multiplier(status, Time, 0.4, 0.25)` —
  more Time = cheaper, more frequent rushes. Single Energy pool (same resource
  the cannon charges from — rushing trades firepower for position).
- Charges: executor-instance state (`MaxCharges`, refill one per
  `ChargeRefillSeconds`) — NEVER stored on the shared SO asset. HUD pips via
  `OnChargesChanged(current, max)`.
- Rushing while dug in un-plants first (netvar-safe) and reconciles the dig-in
  executor's regen — dig in → bombard → rush out is the intended loop.
- Time 5 — **Vector Control**: the burst is split into `SteeringPulses` sub-pulses
  applied along the LIVE forward vector, so turning mid-rush genuinely redirects
  the charge. (The class doc listed alternatives — end-of-rush explosion, enhanced
  turn radius; steering was chosen as the default. Tunable.)
