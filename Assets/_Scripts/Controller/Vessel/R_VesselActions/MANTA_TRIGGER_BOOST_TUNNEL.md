# Manta Trigger Boost — Speed-Tunnel Quasi Dolly Zoom (fundamental trial)

The Manta's analog two-trigger boost now sells itself with the **same** quasi dolly zoom the
Rhino's ramp boost uses: FOV narrows below the live home value while the URP Panini distance
drops below the shared baseline, both proportional to live speed.

This is deliberately a **second data point, not a second implementation**. The intent is to
decide whether the speed tunnel should become a platform fundamental applied to every vessel —
so the Manta runs the identical component with the identical serialized window, and nothing
Manta-specific was authored.

## What changed

Exactly one thing: `SpeedTunnelEffectController` was added to the **Manta prefab root**
(`Assets/_Prefabs/Spacevessels/Manta.prefab`, alongside `VesselStatus`), with Rhino's shipped
values copied verbatim:

| Field | Rhino | Manta |
|---|---|---|
| `minEffectSpeed` | 70 | 70 |
| `maxEffectSpeed` | 280 | 280 |
| `fovDrop` | 25 | 25 |
| `paniniDrop` | 0.5 | 0.5 |
| `responsiveness` | 12 | 12 |

No code, no SO, and no Manta action was touched. The effect's drive signal is measured
`VesselStatus.Speed`, so it picks up the trigger boost's ramp up and its decay down with nothing
to bind, desync, or gate.

## Where the Manta lands in the shared window

`MantaAnalogTurnBoostExecutor` drives `BoostMultiplier = 1 + (base − 1) × min(LT, RT)` with the
prefab's `boostMultiplier = 4`. At full throttle with both triggers buried:

```
speed ≈ XDiff(1) × ThrottleScaler(50) × boost(4) + MinimumSpeed(10) = 210
effect01 = InverseLerp(70, 280, 210) = 0.67   →  ~16.7° FOV drop, ~0.33 Panini drop
```

So the Manta exercises **two thirds** of the shared window at full boost, against the Rhino's
full strength at its 6× ramp top speed (~310, clamped to 1.0). That is the expected consequence
of one shared window across vessels with different top speeds, and it is precisely the thing the
trial is meant to answer: whether a fundamental should key off an absolute speed window (current
behaviour — faster vessels feel faster) or off each vessel's own speed range (every vessel reaches
full tunnel at its own top speed). **Do not "fix" the Manta by retuning its window** — that would
destroy the comparison.

## Everything else is inherited

Behaviour, gating, and the home-values rule are documented once in
`RHINO_RAMP_BOOST.md` § "Visual model" and are unchanged here: local-human-pilot only
(`IsLocalUser && !IsInitializedAsAI`), home FOV/Panini captured from whatever the game is actually
running with and restored exactly, mid-effect camera changes restored on the camera that was
pushed, Panini written to the volume's instantiated profile.

## In-editor verification

1. Launch any game mode (or menu freestyle) as the Manta with a **gamepad** — the analog path is
   gamepad-only (`ActiveInputDevice != Gamepad` early-returns), so touch/keyboard will show the
   effect only via the event-driven `BoostAction` on both sticks.
2. Full throttle, bury both triggers. Speed climbs to ~210; the view should progressively narrow
   while the Panini compression relaxes — noticeably weaker than the Rhino's, by design.
3. Release the triggers: `BoostMultiplier` decays and the view relaxes with it. **Confirm FOV and
   Panini land exactly on their pre-boost values** (compare side by side with a vessel that has no
   `SpeedTunnelEffectController`, e.g. the Dolphin).
4. Pull one trigger only: pure yaw, `min(LT, RT) = 0`, **no tunnel at all**.
5. Feather the triggers in and out rapidly — no snapping onto foreign FOV/Panini values.
6. MPPM two clients: a remote Manta boosting must not move YOUR camera or post-processing.
7. End a turn mid-boost: effect returns home.

## Follow-ups

- **The fundamental decision.** If this ships fleet-wide, the effect should not be per-prefab
  wiring on 11 prefabs — it belongs on the platform side (attached wherever `VesselStatus`
  initializes, tuned from one Resources-loaded config SO, per the enforcement ladder in the
  `/vessel` skill §7). The per-prefab component is correct for a two-vessel trial and wrong as an
  end state.
- Menu freestyle uses Cinemachine, so only the Panini half applies there (no
  `CustomCameraController` to drive FOV on) — same limitation as the Rhino.
