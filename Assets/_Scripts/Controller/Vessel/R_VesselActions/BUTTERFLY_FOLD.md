# The Fold — the Butterfly's TIME ability

Hold the left trigger. The vessel stops, a ghost of it appears on the hull, and the two sticks place
that ghost anywhere inside the cell. Let go and you are there.

It is the one powerful thing the slowest hull in the fleet can do, and it is built so that the power
is in the *placing* rather than in the pressing.

## 1. The placement, and why it needs no new input maths

The whole mechanic falls out of the dual-stick mix exactly as it already is
(`DualStickMix.Mix`, `IInputStatus`):

| Axis | Formula | At rest | Rolled IN | Rolled OUT | What the Fold reads it as |
|---|---|---|---|---|---|
| `XDiff` | `(right.x − left.x + 2) / 4` | **0.5** | **0** | **1** | **Radius** — core / half-way / membrane |
| `XSum` | `Ease(right.x + left.x)` | 0 | ±1 | | **Azimuth** |
| `YSum` | `−Ease(right.y + left.y)` | 0 | ±1 | | **Elevation** |
| `YDiff` | `Ease(right.y − left.y)` | 0 | ±1 | | **Roll** — *not read by the Fold at all* |

`XDiff` already runs 0 → 0.5 → 1 for thumbs-in → rest → thumbs-out, so it maps onto the cell's
radius with nothing inverted and nothing rescaled. It is also already subject to the player's own
throttle-invert preference, which is the correct behaviour for free.

**The frame is the VESSEL's.** Azimuth and elevation are taken against the hull's own forward/right/
up, so the placement basis is whatever the pilot is currently facing.

## 2. The roll, which is the part that makes it fast

`YDiff` is deliberately **left alone**. `VesselTransformer.Roll()` already applies it about
`transform.forward`, is **not** scaled by `TurnScalar`, and is gated only by
`BankIntoTurnSuppressed` — which this ability does not set. So rolling the vessel rolls the camera
(the camera reads the ROOT's rotation), which carries the entire spherical frame around with it.

That is the whole *"roll the world until the place you want is where your thumbs already are"*
mechanic, and it is bought with **no new code and no camera API**. The pilot is not solving for two
angles in a fixed world frame; they are turning the frame.

## 3. The stop, and the one authored number that makes it work

The stop is `IsTranslationRestricted` — the Serpent's existing stationary-stance primitive, which
is replicated (`VesselController.SetTranslationRestricted` → `n_IsTranslationRestricted`) so every
peer sees the vessel halt.

While restricted, `VesselTransformer.TurnScalar` returns `restrictedTurnMultiplier`. The Butterfly
authors that at **0**, which zeroes pitch and yaw for the hold and frees all four stick axes for
placing the ghost. Roll is not scaled by it — see §2. That single authored zero is the difference
between "the sticks aim the ghost" and "the sticks aim the ghost *and* fly the ship".

## 4. The pen-up, which is not optional

`IsTranslationRestricted` deliberately does **not** write `VesselStatus.Speed` — so nothing
downstream (gun velocity inheritance, telemetry, the speed tunnel) shifts when a vessel stops. The
consequence is that `VesselPrismController`'s spawn loop still sees `Speed > 3` while the vessel sits
perfectly still, and a Butterfly would pile its entire wake into one point.

So the executor pens the spawner UP for the duration (`SetSpawnerPaused`). That is an *independent*
axis from the spawner's own enable, which is why it cannot fight an ability that stopped the loop.

## 5. Open space

Outside a membrane there is no sphere to place anything in, so the hold becomes a **reach**: the
ghost glides out along the heading at `freeSpaceReachSpeed`, capped at `freeSpaceRange`. Same
trigger, same ghost, same release.

**A cell whose membrane has not spawned reads as open space, deliberately.** `Cell.MembraneRadius`
returns 0 until the membrane exists, and a radius of zero would collapse every placement onto the
cell centre — the same trap the arena preview's framing records. There genuinely is no sphere yet,
so the free-space branch is correct rather than a fallback.

## 6. Continuity of existence

A teleport is a disappearance followed by an appearance, and the platform law says nothing may do
either instantly. So the hull **withers** at the origin over `departSeconds`, the pose is written at
zero, and it **blooms** back over `arriveSeconds`.

Only the *visual* is scaled. The vessel root carries the colliders, the skimmers and the trail
spawner; scaling those would shrink the ship's whole interaction with the world for a quarter of a
second. The wings also **close over the back** for the whole hold — the resting-butterfly pose — on
every peer, so a Butterfly that is about to leave is legible to everybody else in the match.

## 7. What runs where

| Thing | Where | Why |
|---|---|---|
| The stop, the closed wings, the wither/bloom | **Every peer** | All three are things other pilots should be able to read |
| The ghost | **Owner only** | A screen is a thing one machine has; it previews a decision nobody else is making |
| The commanded position | **Owner only** | Four peers reading their own sticks would place one vessel four ways |
| The final pose | **Owner writes, `SetPose` replicates** | One writer |

## 8. Time's two dials

- **Continuous — the RECHARGE.** `cooldownSeconds` 30 at rest, `× 0.5` at Time 10, ceiling `× 2` so
  a deep deficit slows the ability rather than removing it. Recharge is the only dial that means
  something in *both* branches: inside a cell the reach IS the cell, so reach cannot be it.
- **Level 5 — "Far Fold".** Doubles the **open-space** range only. Inside a membrane it changes
  nothing, because the membrane is already the limit and an upgrade that promised more there would
  be promising something the geometry cannot give.

Both read through `IsUpgradeActive` — the replicated unlock bit — never a raw local level read,
because what they decide is a POSE that every peer adopts, and two machines disagreeing about how
far it may reach is a vessel in two places.

## 9. Tuning knobs

| Knob | Asset | Shipped |
|---|---|---|
| `cooldownSeconds` | `ButterflyFoldAction` | 30 |
| `cooldownMultiplierAtFullTime` | `ButterflyFoldAction` | 0.5 |
| `maxCooldownMultiplier` | `ButterflyFoldAction` | 2 |
| `azimuthDegrees` / `elevationDegrees` | `ButterflyFoldAction` | 180 / 90 |
| `maxRadiusFraction` | `ButterflyFoldAction` | 0.94 (never land ON the membrane) |
| `freeSpaceReachSpeed` / `freeSpaceRange` | `ButterflyFoldAction` | 900 u/s / 1800 u |
| `upgradeRangeMultiplier` | `ButterflyFoldAction` | 2 |
| `ghostTravelSpeed` | `ButterflyFoldAction` | 1400 u/s |
| `departSeconds` / `arriveSeconds` | `ButterflyFoldAction` | 0.22 / 0.3 |
| `restrictedTurnMultiplier` | `Butterfly.prefab` | **0** — see §3 |

## 10. In-editor verification

1. Hold LT inside a cell: the vessel stops, the wings close, a ghost blooms on the hull.
2. Roll both thumbs IN — the ghost travels to the cell core. OUT — to just inside the membrane.
   Hands off — half radius.
3. Sweep `XSum`/`YSum` — the ghost sweeps azimuth/elevation in the hull's frame.
4. Apply `YDiff` — the vessel and camera roll, and the ghost's frame rolls with them.
5. Release — the hull withers, reappears at the ghost, and flies on at its previous speed.
6. Hold for 5 s and confirm **no prisms** accumulate at the origin (§4).
7. Press again immediately — nothing happens, and the Time card's veil is sweeping (§8).
8. Fly outside the membrane and hold — the ghost reaches along the heading instead, further the
   longer you hold.
9. **MPPM:** on the second client the folding vessel stops with its wings shut and no ghost, and
   arrives at the same place.

## 11. Follow-ups

- **Unverified** — nothing in this document has been run in the editor.
- The ghost borrows the hull's material (opaque). A translucent domain-tinted material is intended.
- `ghostBloomSeconds` and the wither/bloom pair are unplayed guesses.
- An AI never folds: `AIPilot` produces no trigger input and this ability is a placement decision
  with no obvious autopilot policy. Stated rather than stubbed.

## The camera goes with the placement

A pilot cannot choose a place they cannot see. The reach is `MaxRadiusFraction` of the membrane —
hundreds of units — so a camera left behind the stopped vessel shows the destination as a few
pixels of ghost against the cell, if it is on screen at all. `VesselPlacementView` frames the ghost
for the length of the hold, at the vessel's own follow distance and in the vessel's own **rolled**
frame, which is what keeps the roll-the-world mechanic legible: the frame the sticks address is the
frame you are looking through. What the hold becomes is *you fly the DESTINATION with the sticks
and let go when you like where you are*.

Three details are load-bearing.

**The anchor is seeded at the vessel**, so entering the view is a no-op snap rather than a cut; the
point then sweeps out under the sticks and the camera's ordinary smoothing carries it.

**It is HELD through the wither** and released on the frame the pose is written — which is the frame
the vessel arrives at the point the camera is already framing. So the teleport costs the camera no
motion at all: it is already there, looking the right way, and the ship blooms in ahead of it.
Releasing the anchor at the RELEASE edge instead would swing the camera back to the stationary hull
for the length of the departure and then swing it out again, which is the one cut this vantage
exists to avoid.

**Every peer calls it and only the local pilot's machine acts on it.** `VesselPlacementView` is
keyed on the vessel bound at `VesselController.Initialize`/`ChangePlayer` under `IsLocalPilot`, so
this executor carries no camera gate of its own to get wrong — an AI Butterfly folds on the server,
where there is no camera and no ghost, and its `Place` calls are simply ignored.
