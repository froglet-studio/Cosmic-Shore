# Grizzly — Charged Cannon (Fire, right trigger)

Design source: `ClassGrizzly.md` (07/16/2026 design pass). Element link: **Space**.

## The gesture (fly-by-wire)

One input, four meanings, driven by a per-vessel state machine in
`GrizzlyChargedShotActionExecutor`:

| State | Pull (press) | Release |
|---|---|---|
| Idle | start charging | — |
| Charging | — | fire (charge spent = blast size) |
| InFlight | **freeze** the shell in place | — |
| Frozen | — | **detonate** it where it hangs |

The shell is a BOMB: it is fired with `stopOnFirstPrismImpact: true`, and a natural
impact detonates it where it lands (`HandleFlightEnded` → `SpawnBlast`, the same
charge-scaled AOE the manual path uses — spawned directly rather than through the
detonator, whose delayed `ReturnToFactory` would double-return the pooled shell).
Expiry without impact just ends the flight. The state machine returns to Idle either
way. Stale-shell races are guarded with `Projectile.FlightGeneration` snapshots.

Without the stop flag the shell PIERCES every prism and never explodes on impact —
the only blast is the manual freeze→release, which the 400 u/s shell has carried out
of self-launch range long before a human can press it. That was the "explosion gives
no boost" bug: the impulse effect was wired correctly and physically unreachable.

**Never** use `Gun.StopProjectile()` for the freeze — it silently despawns (this was
the restoration branch's tap-race bug). The freeze is `Projectile.Freeze()`
(engine addition): cancels the move loop, keeps the shell alive, rendered, and
detonatable, zeroes `Velocity`.

## Economy (single pool)

The Grizzly has ONE resource: Energy (index 0). Holding the trigger builds energy
(`ChargePerSecond`); release spends the entire accumulated charge — the charge IS
the cost. Dig In (Button1) accelerates regeneration; Rush (Button2) spends from
the same pool.

## Knockback & self-propulsion

Grizzly blasts carry the strongest knock-back in the game
(`VesselImpulseByExplosionEffectSO`, wired into the Grizzly AOE prefab's
explosion impactor container):

- Detonations pass `AffectSelfOverride = true`, so the shooter's own vessel is a
  valid impact pair — riding your own shockwave is the class's movement identity.
- Impulse = `AOEExplosion.Impulse.Along(dir from blast center)`, applied via
  `VesselTransformer.ModifyVelocity` (cosine ease-out, ~1s).
- A dug-in Grizzly is blasted OUT of turret stance (`SetTranslationRestricted(false)`
  — routed through the controller so the netvar stays in sync).
- Multi-collider hulls are latched per (explosion, vessel) so they aren't impulsed
  once per collider. The latch is private — VesselCombatHitLatch belongs to the
  scoreboard and must not be consumed here.

## Space element

- Quantitative: explosion Min/MaxScale × `ElementalScaling.Multiplier(status,
  Space, 2.5, 0.5)`.
- Level 5 — "Safe Detonation": same-domain vessels are spared by the impulse
  effect and direct damage spares own-domain prisms — but the SHOOTER is always
  hit, so friendly-fire-off never kills self-launch.

## Leak guards

A frozen shell is force-resolved (silent `ReturnToFactory`) on: turn end,
executor disable, and re-`Initialize`. Detonation only happens from the player's
explicit Frozen→release.
