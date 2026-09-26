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

A pilot cannot choose a place they cannot see. The reach is the fold's whole range —
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

## One reach, everywhere (2026-09-25)

The Fold used to have **two** branches. Inside a membrane the hold addressed a point in the cell's
sphere — `XDiff` the radius, `XSum`/`YSum` the azimuth and elevation, the whole frame rolled with
the vessel; outside one it was a reach along the heading. That is retired. **There is one
behaviour: a reach along the heading, `hold x reachSpeed`, capped at `ResolveRange`.**

Three things it bought.

**The upgrade stopped being a lie.** Time 5 "Far Fold" multiplies the RANGE, and inside a membrane
the membrane was already the limit — so the ability's one level-5 upgrade did nothing at all in the
branch a player spends nearly all of their time in. Its own tooltip said so. With one reach it is
always worth something.

**No boundary changes the ability under you.** A pilot crossing a membrane found their trigger
doing a different thing, with nothing on screen to say why. `Cell.MembraneRadius` also returns 0
until the membrane has SPAWNED, so which branch you got was a function of load order for the first
seconds of a match.

**The cost, stated: the sticks no longer aim it.** A fold is committed to the heading the pilot was
already flying — pitch and yaw were dead for the duration anyway (`restrictedTurnMultiplier = 0`),
and the spherical placement was the only thing the sticks were steering. So the decision moves
BEFORE the press: you have to finish whatever you were doing on a line that points where you want
to go. On the fleet's slowest hull that is a good trade — the fold gives distance for free and
charges you for the exit line — and it is what `Waystation` is built on. If aiming is ever wanted
back, it is one prefab field (`restrictedTurnMultiplier`), not a second branch.

## The mode built on it, and the two things it needed from the platform (2026-09-25)

`Waystation(58)` — the Butterfly-only migration race — is cut against *exactly* the one degree of
freedom the section above left: the heading you leave on. Clusters of rings, laid a fold apart,
each ending in an **exit gate** that faces the next cluster. Full record:
`_Scripts/Controller/Arcade/WAYSTATION.md`.

Two things it needed, and both are the platform's rather than the mode's.

### A teleport threads nothing

A fold crosses hundreds of units along its own heading, and in that mode the next cluster's rings
are on that heading *by construction*, so without a rule a pilot would be paid for every ring their
jump passed through. `VesselTransformer` therefore carries **`TeleportCount`** — incremented by
`SetPose` and by `VesselController.Teleport` — and `GateRaceController` declines any step whose
frame contains one.

**It is a COUNTER rather than a distance, and that is the whole point.** The gate race already had
a step guard (`maxPlausibleSpeed × Δt × 2 + 5`) meant to reject a respawn, and it fails the *other*
way round for a teleport: a LONG jump is rejected by accident and a SHORT one is credited. No
distance threshold can separate "the pilot flew here" from "the pilot was placed here", because
both are just a position delta. The vessel that MOVED is the only thing that knows, so it says so.

Anything else that places a vessel — an eject, a mode's reposition, a future ability — gets the
same protection for free by going through `SetPose`, and anything that writes the transform
directly does not. *If you write a vessel's position without telling it, every system downstream
that has to tell a jump from a flight is guessing.*

### `R_VesselActionHandler.TryGetBoundAction<T>`

An autopilot in that mode has to hold the Fold, and **how long** is a function of the ability's own
`reachSpeed`. The existing `TryGetInputForAction<T>` answers *which control* — enough to press a
one-shot, not enough to time a hold. The new sibling hands back the **action** as well, so the
mode's controller reads the reach speed off `ButterflyFoldAction.asset` instead of copying it: a
copied constant is right on the day it is copied and silently stale after the next retune.

The drive itself lives in the **controller**, not here — an AI's decision is mode knowledge (how far
the next ring is) while the numbers are the ability's. That split is why this file gains a query and
no behaviour.

---

## Every fold leaves a PAIR OF GATES standing (2026-09-25)

A fold now opens **two portals** — one where the vessel left, one where it arrived — and they stay
open. Any vessel of the Butterfly's **domain** threads either and is at the other, as often as it
likes. The pair stands until that Butterfly folds again, and the new pair replaces it.

`FoldGate` owns a gate; `FoldActionExecutor` owns the pair.

### Why the Butterfly is the hull that gets this

It is the fleet's slowest ship (55 u/s cruise, 45°/s turn) and it can never out-fly anybody. What
it can do instead is **leave a shortcut standing that its whole team keeps** — so the Fold stops
being a movement option the Butterfly spends on itself and becomes the one thing this hull
contributes to a side. Placing is the Butterfly's alone; *using* is everybody's, provided they are
already in its domain.

### It is a SWITCH, and it is the second domain-coloured one outside the toybox

A ring you thread is the platform's one word for "this activates something", so a gate is the
ordinary `ToyFactory.AddSwitchRing` ring drawn at **its own trigger radius** — the ring IS the
volume, never an advertisement for a bigger one (`Docs/ToySystem/ARCHITECTURE.md` § "The switch").

It wears the placer's DOMAIN, which is reserved: normally a domain-coloured switch is one that
**hands** you that domain. `ScarabSwitch` was the first exception — there the colour names the
domain the switch *belongs to* — and a fold gate is the second, one notch further: the colour says
**who may thread it**. That is a claim about the gate, not about the pilot; a gate never changes
anyone's domain, it only declines pilots who are not already in it, so the two readings of a
domain-coloured ring still never share a screen. `ToySwitchVocabularyTests` carries the row.

### Nothing removes a gate but the pilot who placed it

A pair stands until that Butterfly folds again (an **active, explicit player act** — the same class
of removal as a cell swap, or a Scarab standing one switch too many) or until the vessel that laid
them is destroyed, at which point nothing is left that could ever replace them. There is **no
lifespan, no decay and no idle culler here, and there must never be one**: that is the timed culler
the platform rejects, wearing a portal's costume.

The **arming latch** is not an exception to that, and it is deliberately not a timer. A vessel may
only be taken by a gate it has been *clear of*. Without it the very first thing every fold does is
teleport the pilot back: the destination gate is laid AROUND them, so flying out of their own
arrival ring crosses its plane and sends them home. "You got clear of this gate" is exactly the
fact that matters, it carries no number of its own (the near zone is the mouth, one exit clearance
deep), and *a system whose whole rule is that nothing runs on a clock should not gate its own
detector on one.*

### Why the pair is laid at ARRIVAL, from two replicated positions

Both gates are built **independently on every peer**, like `ScarabSwitch`'s dais, and nothing about
them is replicated. That only works if every machine agrees on where they go, and the two positions
chosen are the only two that do:

| end | position | why every peer already agrees |
|---|---|---|
| origin | the hull's pose at the moment of the commit | it has been stopped (`IsTranslationRestricted`, replicated) for the whole hold |
| destination | the hull's pose once the arrival bloom completes | `SetPose` replicates, and the vessel is standing still in it |

The tempting alternative is to derive the destination from the HOLD — `_heldSeconds` does
accumulate on every peer, and `ResolveTarget` is pure. It is correct on the owner and **tens of
units out everywhere else**, because the press and the release arrive over the wire: at
`reachSpeed 900`, 50 ms of jitter is 45 units against a 55-unit mouth. *A quantity every peer can
compute is not a quantity every peer computes the same.*

A peer waits up to `gateSettleSeconds` for the replicated pose to actually move. If the two ends
are still inside `minGateSeparation` at that deadline, **no pair is laid and the standing pair is
left alone** — one branch that covers both the degenerate tap-and-release (a portal to where you
already are) and the peer whose pose never arrived, because in both cases the honest answer is to
draw nothing rather than guess.

### Who detects, who moves

Each machine tests **only the vessels it owns**, and the owner writes its own pose — which
replicates. So a transit needs no new networking, cannot double-fire across peers, and cannot be
decided for you by somebody else's frame. It is the `ReportFaunaKill_ServerRpc` family's shape with
the report removed, because `SetPose` already travels.

Two properties are preserved on a transit, and together they are what make a portal predictable
rather than a shuffle:

- **Where in the mouth you entered is where you leave.** The lateral offset inside one ring is
  re-applied inside the other, so threading near the rim comes out near the rim.
- **The side you were heading for is the side you come out on**, one `gateExitClearance` along the
  shared axis. Momentum reads through the gate and a transit never spits a pilot backwards.

Rotation and speed are untouched. *A gate moves you; it does not fly you.*

Both ends are then **re-seeded at the exit and disarmed**. The re-seed alone is not enough: the far
gate deposits the pilot one clearance from its own plane, so it has to treat them as somebody
standing in its mouth — which is what disarming says — until they have flown clear of it.

### A transit is a teleport, and every watcher already knows

`IVessel.SetPose` bumps `VesselTransformer.TeleportCount`, so a gate transit is a jump the mover
STATES rather than one a watcher has to infer. `GateRaceController` declines any step containing
one — which means **a gate cannot thread a race ring**, the rule Waystation needed for the Fold,
covering the gates with nothing added.

### Stated judgement: the vessel is not withered on a transit, the gates flare

The Fold itself withers and blooms the hull because a teleport out of open space is a
disappearance with nothing to explain it. A gate transit is not that: the pilot flies INTO a
visible ring and OUT of a visible ring, and **the rings are the continuity**. Withering the hull at
both ends would also put a quarter-second of dead time on a movement option meant to be flown
through at speed. If a playtest reads it as a pop, the fix is to route the transit through the
wither/bloom the Fold already owns — not to add a second one.

The gates themselves obey the law in full: they bloom in over `gateBloomSeconds` and wither away
over the same when a fold replaces them.

### The platform fix it needed: a CLIENT may move its own vessel

`VesselController.SetPose` sent `SetPose_ClientRpc` unconditionally, and **only a server may send a
ClientRpc** — so every client-owned teleport reached that method on a party guest, hit the RPC and
did nothing but log. That was already true of the Fold itself and of the Wanderway's return; the
gates would have inherited it. `SetPose` is now three branches: not spawned → local; server →
broadcast, byte-identical to before; owner → ask the server, which broadcasts. A peer that is
neither writes nothing, because it is not that machine's vessel to move and it will receive the
pose like everybody else.

The server branch is deliberately kept rather than folded into the ServerRpc the way the
slowed-transform pair is — a ServerRpc invoked on the server is still dispatched through the
network layer, and every pre-existing caller is a host-side teleport that should not pay a tick for
a route it does not need.

### Tuning (all on `ButterflyFoldAction.asset`)

| field | shipped | what it is |
|---|---|---|
| `gateRadius` | 55 | mouth radius; the ring is drawn at exactly this |
| `minGateSeparation` | 300 | shortest fold worth leaving gates for |
| `gateExitClearance` | 40 | how far past the far plane you emerge, and the depth of the near zone the arming latch reads |
| `gateBloomSeconds` | 0.45 | bloom in, and wither out when replaced |
| `gateSettleSeconds` | 0.75 | longest a peer waits for the replicated arrival pose |

### Verification status

Authored headless. **Nothing has been run in the editor.**

What IS proven, by compiling and RUNNING the shipped geometry
(`Tools/Build/foldgate_harness/`, which takes `FoldGateGeometry.cs` verbatim): seven properties
with four negative controls, all firing — a pilot flying out of their own arrival gate is not
taken (and, with the latch removed, would be), flying back in once clear IS taken, every exit
lands inside the far gate's near zone so disarming there is *sufficient* rather than
approximately right, lateral offset and travel sense are preserved exactly, a pass just outside
the rim is never taken while the same flight just inside is, and a round trip is an involution.

The arming latch is the whole reason that harness exists: it is invisible to every textual gate in
the repo and to a Roslyn type check, and getting it wrong is not a nuance — it is *every fold
teleports you straight back*, which is exactly what the first cut did.

`FoldGate.cs` additionally type-checks against a Roslyn stub of its API surface, and the standing
textual gates pass.

**NOT verified:** the ring's appearance and domain paint, the flare, the transit feel, every
multiplayer path (the client `SetPose` route in particular, and whether `gateSettleSeconds 0.75`
is long enough for a real peer), whether `minGateSeparation 300` is the right floor against a
`reachRange` of 1800, and whether teammates being carried off by a gate they flew into by accident
is a problem in play.

### Follow-ups

- No HUD marker for a standing gate. The Butterfly has no way to see where its own pair is once it
  has flown away from both ends; `FoldGate.Live` is the roster an objective-arrow-style marker
  would read.
- No sound. Both the bloom and the transit want one, and per the FMOD convention each gets its own
  `EventReference` field rather than a borrowed category.
- An AI never uses a gate. `AIPilot` steers at objectives and knows nothing about `FoldGate.Live`,
  so a bot teammate walks past a shortcut its Butterfly left for it.
