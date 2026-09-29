# Manta Trigger Boost — Speed Tunnel

> **Superseded.** The speed tunnel is no longer a per-vessel visual: it is a fleet-wide
> **PLATFORM LAW** — see **`Docs/SPEED_TUNNEL.md`**, which is now the single reference for
> behaviour, tuning and verification. This file survives only to record what the Manta trial
> found, because two of its findings still matter.

The Manta's analog two-trigger boost (`MantaAnalogTurnBoostExecutor`) wires nothing and knows
nothing about the tunnel. It sets `IsBoosting` + `BoostMultiplier`, the transformer turns that
into speed, and the law reads speed. That is the whole integration, and it is identical for
every vessel.

## What the trial found

**1. The absolute mapping is the design.** The trial ran the Manta on the Rhino's exact window
to answer one question: should the effect key off an absolute speed, or off each vessel's own
speed range? **Answer: absolute** — the same speed on any vessel produces the same visual, and a
faster vessel reaches deeper into the tunnel because it is genuinely faster. That decision is
now locked into `SpeedTunnelConfigSO.Effect01`, which takes a speed and nothing else, with an
edit-mode test asserting it can't grow a vessel parameter.

**2. The Manta is far faster than the rest of the fleet, and the first pass of this doc got that
wrong.** It claimed a cruise of 60 and a boosted top of 210, computed from `VesselTransformer`'s
*class* defaults (`DefaultThrottleScaler` 50, `DefaultMinimumSpeed` 10). The Manta prefab
overrides them:

```
DefaultThrottleScaler 180, DefaultMinimumSpeed 0     →  cruise 180   (fleet norm: 60)
× BoostMultiplier 4                                  →  top    720   (fleet norm: 210)
```

So against the shipped 70–280 window the Manta is at **~0.52 effect at plain unboosted full
throttle** and **saturated the instant it boosts** — not the ~0.67-at-full-boost the trial
predicted. The A/B it was set up to run therefore never happened the way it was described: the
Manta was never showing "two thirds of the Rhino's tunnel", it was showing more tunnel than the
Rhino for most of ordinary flight.

Under the absolute law that is *correct behaviour*, not a defect — but it is the fleet's
sharpest tuning question, so it is called out in `Docs/SPEED_TUNNEL.md` §3 with the fleet table
and the one legitimate lever (the shared floor, moved with the whole fleet in view).

## What changed on the Manta

Nothing, in the end. The trial's per-prefab `SpeedTunnelEffectController` was removed along with
the component itself when the law landed — the Manta tunnels because it is a vessel, exactly
like the other ten.
