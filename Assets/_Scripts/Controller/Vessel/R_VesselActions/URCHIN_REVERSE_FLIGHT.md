# Urchin — reverse flight (one throttle mapping, on the rail and off it)

The Urchin could run a ribbon in both directions and could only ever fly one way in open space.
That was not two features, it was **one feature with a hole in it**: the same stick position meant
"back up" while grinding and "half cruise" while flying, so the answer to *which way does this send
me* depended on whether there happened to be a prism under the hull.

This closes it. `GunVesselTransformer.ThrottleAxis` is now `ReadThrottle()` — the **same signed
axis the grind has always run on** — and free flight reads it the same way the rail does. The
Urchin is the fleet's only reversing hull; everything below that lives in shared code is gated so
that it is a provable no-op for the other ten.

## What changed on the stick

`XDiff` is the dual-stick speed axis, in `[0, 1]`, resting at **0.5**. The fleet reads it raw. The
Urchin now re-centres it (`(XDiff − 0.5) / 0.5`, which is `ReadThrottle`), so with its authored
`DefaultThrottleScaler 65` and `DefaultMinimumSpeed 0`:

| stick | before | after |
|---|---|---|
| full forward (`XDiff` 1) | **65 u/s** forward | **65 u/s** forward — unchanged |
| centre (`XDiff` 0.5) | 32.5 u/s forward | **stop** |
| full back (`XDiff` 0) | stop | **65 u/s reverse** |

**Re-centring the forward half is the point, not a side effect.** There is no mapping that puts
reverse below rest *and* leaves rest at half cruise without a discontinuity at the rest point. What
the bottom half of the stick used to be was a long mushy run of deceleration whose only distinct
reading — the stop — lived at the very end of it; it is now reverse, and the stop sits where a stop
belongs.

The Urchin can afford the centre-stop that falls out of this where most hulls could not: its
`DefaultMinimumSpeed` is already 0, so a full pull-back has always been a genuine stop
(`MinimumThrottleBrake` lands it), and this only moves **where on the stick that stop lives**.

## What follows from it without being written

Three things the design asks for are consequences of publishing a **positive Speed and a reversed
Course** rather than a negative Speed, and nothing in any of them was taught about reverse:

- **The trail comes out the front.** `VesselPrismController` lays along `Course` and gates on
  `Speed > 3`. A reversing Urchin vacates the space ahead of its nose, so its wake extends out past
  it — which is the same sentence as "the jets are pointing forwards now".
- **Backing into a ribbon latches the grind the way you are travelling.** `TrailFollower` takes its
  direction from `Course` at the moment of contact. Flying into your own forward-laid trail in
  reverse and riding it backwards is the Urchin's substitute for a turning circle — it has no
  drift, no Yastri and no stop ability — and it is what the mode geometry (Hijack's burrs, Skein's
  aimed breaks) already assumes a pilot can do.
- **A danger prism still bites.** `throttleMultiplier` is applied before the split, so a slow scales
  a reverse exactly as it scales a cruise.

`Course × Speed` — inherited projectile velocity, debris impulse, an AI's lead — is the true world
velocity under either split. `Speed` **alone** is a magnitude at every site that reads it on its
own (the speed tunnel's absolute FOV mapping, `wavelength / Speed`, the `> 3` gate, telemetry), and
a negative would have broken every one of them. That asymmetry is why the split is magnitude +
direction and not a signed scalar.

## The jets

`VesselJet` turns its plume end-for-end when its vessel travels backwards, eased over
`reverseFlipSeconds` (nothing on this platform pops). It is driven off `VesselStatus.Course`, which
is **replicated** (`VesselController.n_Course`, owner-write) — so a rival's jets turn around on
every machine with no new networking and no new state. That matters here specifically: a remote
replica's transformer is switched off (`VesselController` → `ToggleActive(false)` for a network
client) and computes nothing at all, so a flip derived from local flight state would have been
visible to its own pilot alone.

It is gated on `VesselTransformer.CanReverse` and not on the course dot alone, and the reason is a
hull this has nothing to do with: **a drifting vessel's course legitimately swings past broadside**
while the pilot spins the nose, so a bare dot test would have turned a Squirrel's plumes around
mid-drift. `CanReverse` is a **code** property rather than a serialized field, so it is true on a
replica whose transformer never runs and cannot be authored onto a hull whose axis cannot actually
go negative.

The 180° is about the jet's own **local Y**, which maps its local +Z to −Z whatever the mount
bone's world orientation is — so it reverses a canted engine exactly as it does a square one, and
composes with the bone puppetry above it (`UrchinAnimation.PerformShipPuppetry` writes the
PARENT's rotation, never the jet's).

## The attach seed, which was right about one case only

`_facingSign` means *does the NOSE agree with the ribbon's index-order heading*. `SeedTrailRide`
took it from the follower's latched travel direction, which is the same fact **only while the
vessel is flying nose-first**. It is now composed: `nose vs index = (travel vs index) × (nose vs
travel)`.

Uncomposed, this is invisible on a forward attach and wrong on every reverse one: a pilot backing
into a ribbon is holding the stick back, the grind reads that as "go opposite my nose", and the
uncomposed seed claims the nose already points the way they are travelling — so the rail would fire
them off the way they came in the same breath it caught them. It is the exact mirror of the defect
the seed was written to fix in the first place.

## Files

| File | What it does here |
|---|---|
| `VesselTransformer.cs` | New `ThrottleAxis` seam (the fleet default is still raw `XDiff`), `CanReverse`, and the magnitude/direction split in `MoveShipScalar` |
| `GunVesselTransformer.cs` | `ThrottleAxis` → the signed `ReadThrottle`, `CanReverse => true`, `reverseThrottleScale`, the composed attach seed, and the speed-carry's reverse handling |
| `MinimumThrottleBrake.cs` | Stands down on ANY non-zero target (so a reverse command is never clamped to a stop) and gains an opt-in mirrored branch so a reversing vessel released to centre lands on a real zero |
| `VesselJet.cs` | The eased end-for-end flip |
| `Urchin.prefab` | `AIPilot.defaultThrottleHigh/Low` 0.6 → 0.8 |
| `Tools/Build/urchin_reverse_harness/` | Compiles and RUNS the shipped brake: the no-op proof, its negative control, and the reverse stop |

## The vector flight model needs nothing

`MoveShipVector` thrusts along the nose and derives `Speed` and `Course` from the velocity every
frame, so a negative `ComputeThrottleTarget` drives the nose component through zero and the course
reverses **by construction** — magnitude positive, direction flipped, exactly what the scalar path
now does by hand. The split is the scalar model's way of arriving at what the vector model already
had. No `vectorFlightModel` hull can command reverse today, so this is an argument, not a shipped
path.

## Tuning knobs

| Knob | Where | Ships at | Notes |
|---|---|---|---|
| `reverseThrottleScale` | `GunVesselTransformer` | **1.0** | What reverse is worth against the same pull forward. 1 is what the RAIL already does (`trailFollower.Throttle = Abs(throttle)`), and therefore what "one throttle mapping" means. **Not in `Urchin.prefab`** — the field initializer is the shipped value until someone opens and saves that prefab |
| `reverseFlipSeconds` | `VesselJet` | 0.25 s | How long a plume takes to swing round. Same caveat: not authored in any prefab yet |
| `reverseCourseBand` | `VesselJet` | 0.25 | A BAND, not a threshold — flip below −band, return above +band, hold in between, so a course on the line cannot flap the plumes |
| `DefaultThrottleScaler` | `Urchin.prefab` | 65 | Now the top speed in BOTH directions |
| `minimumThrottleBrakeSeconds` | `VesselTransformer` | 2 s | Gives 32.5 u/s²; measured stop from 65 u/s is **1.40 s**, identical forward and reverse |
| `defaultThrottleHigh/Low` | `Urchin.prefab` `AIPilot` | **0.8** | Was 0.6. Re-centred to preserve the AI's free-flight cruise EXACTLY: `0.6 × 65 = 39` before, `((0.8−0.5)/0.5) × 65 = 39` after |

## What was proved without Unity

`Tools/Build/urchin_reverse_harness/run.sh` compiles and **runs** the shipped
`MinimumThrottleBrake.cs`:

- **T1** — `symmetric: false` is **bit-identical** to the pre-reverse brake over 600 swept cases,
  so every hull that cannot command reverse is untouched. (The target sweep is non-negative on
  purpose: no shipped transformer without `CanReverse` can produce a negative one — `XDiff` is in
  `[0, 1]`, the scalers are positive and `CurrentBoostAmount` is at least 1.)
- **T1b** — the negative control, and the one to read before changing a constant. The flag can only
  matter BELOW the crossover `rate / LERP_AMOUNT` (21.67 u/s on an Urchin), because above it the
  exponential is the stronger of the two and wins outright. Measured: **4333/4333 differ below,
  7666/7666 identical above.** This test first passed **1 of 3** with a control picked above the
  crossover — green, and proving nothing.
- **T2** — a reversing Urchin released to centre reaches an **exact** zero in **1.40 s**, the same
  time a forward one takes.
- **T3** — the mirrored brake can never push a reversing vessel past the stop into forward motion.
- **T4** — a reverse command (negative target) passes through the brake untouched.

All four changed C# files additionally **type-check clean under Roslyn** against stubs transcribed
from the real declarations in the tree.

**Nothing has been run in the editor.**

## In-editor verification

1. **Menu_Main freestyle, Urchin.** Hands off the stick: the ship should come to a complete stop in
   about 1.4 s, not settle into a cruise. Pull back: it flies backwards, up to 65 u/s.
2. **Jets.** While reversing, all four plumes swing end-for-end over ~0.25 s and stream out past the
   nose. Push forward: they swing back. They must **swing**, never snap.
3. **Trail in front.** Fly forward to lay some ribbon, then reverse. New prisms appear ahead of the
   nose; the ribbon you laid going forward is behind you and you back into it.
4. **Ride it backwards.** Back into your own forward-laid trail. The grind must carry you on **the
   way you were already travelling** while you keep holding the stick back — if it fires you off
   the way you came, the composed attach seed is the thing to look at.
5. **Push forward mid-grind.** The ride should swing through zero and reverse along the ribbon, as
   it always did — the grind itself is untouched.
6. **Every other hull.** Squirrel, Dolphin, Manta, Rhino, Sparrow, Serpent, Scarab: hands-off cruise
   and full-pull-back stop must feel exactly as before, and no hull's jets may flip during a drift.
   Check the Squirrel specifically, mid-drift, nose spun past broadside.
7. **MPPM, two clients.** A remote Urchin's jets flip on the observer's machine, not just the
   pilot's.
8. **AI.** An AI Urchin (Hijack or Skein) should cruise at the same ~39 u/s it did before. Its rail
   grind is **deliberately faster** now (signed throttle 0.6 where it was 0.2) — this is the change
   most likely to want a number, and it moves in the direction the `ram: 0 → 1` fix already wanted.

## Follow-ups

- **`reverseThrottleScale`, `reverseFlipSeconds` and `reverseCourseBand` are field initializers, not
  prefab keys.** The moment anyone opens `Urchin.prefab` or `VesselJet.prefab` in the editor and
  saves, Unity writes them at their then-current values and the asset becomes authoritative.
- The AI's rail-grind speedup (item 8) is untested behaviour, not a measurement.
- A reversing Urchin's **camera** is unchanged — it still sits behind the hull, so reversing flies
  the ship toward the viewer. The rear-view gesture (`C` / `LB+RB`) already covers wanting to look
  the other way, and pointing the camera at the direction of travel would compose with it badly.
  Worth a playtest read before anything is done about it.
