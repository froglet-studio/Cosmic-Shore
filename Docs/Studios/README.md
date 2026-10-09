# Studios — web pages for vessel design decisions

A studio is a single HTML page that flies or shows a design **before** it is built in Unity, so a
decision is made by looking and playing rather than by reading numbers. It is a **reader** of the
shipped numbers, never their authority: every constant in it is copied from an asset or a class and
named after it, and when the asset changes the page must follow (or say on screen that it differs).

| Studio | Live page (decision log on) | Repo copy |
|---|---|---|
| Stoat Flight Studio | https://claude.ai/artifact/Busc3KW6DmVzbsiA2qxoHc | `StoatFlightStudio.html` |

**Next: Prisma.** `PRISMA_WORMHOLE_SESSION_PROMPT.md` is the prompt for an engine session that makes the
Stoat and both pair styles flyable, inspectable and swappable in Prisma (`Port/`).

## Stoat Flight Studio (round 5 — the dipole sling, a prototype)

**What changed.** Pair A's sling is now the **dipole sling** (the rail's "A: Dipole sling" switch; "A: Orbit
(round 4)" brings back the orbit sling). It exists only in the studio so far; the game still runs the orbit
sling (`StoatSlingExecutor`).

- **Press** LT/RT: the black hole is laid 240 u ahead and 60 u to the trigger's side, the white hole 240 u
  ahead and 60 u to the other side, both with a 6 u horizon, so the pair's axis starts square across the nose
  and both are in view.
- **Squeeze**: the black hole moves at up to 80 u/s × squeeze^1.5 and its horizon grows at up to 10 u/s
  (to 28 u). How deep you squeeze sets the rate; how long you hold sets how far. While it is still ahead it
  aims for a point beside your line (6 horizons off it, on its own side) so you pass it rather than hit it.
  Once it is abeam it keeps going the way it came. The white hole stays where it was laid, so the axis
  swings diagonal.
- **Physics**: the black hole pulls with the Paczyński–Wiita law, GM sized so a circle at 6 horizons is a
  free orbit at cruise (GM = ((x−1)²/x)·r_s·cruise²). The white hole pushes with 0.5 × its GM, softened over
  twice its horizon. The force across the nose bends it; the force along it is the gravity speed. The
  flight is sub-stepped at 4 ms so a still hole gives back exactly what it gave. Diving through the portal
  restores the speed you had before the dive (the two mouths are one throat); only the white hole's
  bounded push is new.
- **Let go**: the pair falls together at 40 u/s and annihilates; the gravity speed fades over 2 s once you
  are clear of both holes.

**Measured** (scripted pad, full throttle, no steering, RT held ~4.5 s of game time):

| squeeze | turned | speed kept | axis at release | what happened |
|---|---|---|---|---|
| 0.3 | 131° | +91 u/s | 170° | slingshot round the black hole |
| 0.6 | 119° | +27 u/s | 139° | swung round it, climbing out cost most of the gain |
| 1.0 | 88° | +16 u/s | 62° | the black hole arrived fast and large: a dive through the portal |

Control: a black hole that does not move (approach 0, growth 0, no push) gives back what it gave. The
residue left is the depth of the well at the point where the pass ended.

**Why the speed comes from the black hole's motion.** A still well hands back on the way out what it gave on
the way in, so a pass past a still hole gains nothing. The gain is the classic gravity assist: you swing
round a well moving against you. That is why the black hole keeps its momentum after it is abeam, and why a
light, early squeeze (a slow black hole met early) beats a buried one (a big one that swallows you).

**Round 4** (the orbit sling and the vessel portal) is described below; its numbers are still the game's.

## Stoat Flight Studio (round 4)

**Round 4 — your decisions applied.** Pair **A** (the drift pair) is the pick, wearing this branch's
look again (horizon holes traced with the converging lens; the white hole's crossing core glows
white-hot; tides even under time reversal). It now flies the **orbit sling** (`StoatSlingExecutor`,
`StoatSlingMath`): **press** LT/RT and the attractor appears beside you at the orbit radius, the
repulsor mirrored on the other side at the same size and distance; **hold** and you circle the
attractor (squeeze harder → a tighter circle, 150 u at a touch to 40 u buried, horizon = radius ÷ 6,
strength set so the circle at your speed is the free orbit); **let go** and you leave on the tangent
with a boost (0.25–0.9 × speed over 1.5 s) while the two holes fall together (40 u/s after a 0.6 s
ramp) and annihilate when their horizons touch. A pair held for 12 s lets go on its own. **The
portal:** anything that crosses the black hole's horizon (prisms, the Stoat, any vessel) comes out of
the white hole at the point reflection, stretched going in and relaxing coming out — and it leaves in
the closing white hole's frame, so the mouth cannot run it back down. **B** stays for comparison. A
**run** is one race: every ring, every lap, against the clock.

**Found by it (round 4).** A hull carried out of a CLOSING pair between the two holes, flying slower
than they close, was overtaken by its own white hole (the probe showed the distance to the white hole
falling from 23 u to 16 u after the exit). Fixed in the game too: `BlackHoleRegistry.TryGetMouthMotion`.

## Round 3 notes (pair A's numbers here are superseded by round 4)

**What it is.** The plated Stoat (`StoatHullForm`, body-only `StoatLopeMath` lope) flown round a
Slingshot-style course, read through the game's own dual-stick mix (`InputController`: yaw/pitch are
the eased SUM of both sticks, roll/throttle their DIFFERENCE; one stick alone = 29 %, throttle at rest =
half cruise). LT/RT squeeze-and-release slings a wormhole pair with either style:

- **A — the drift pair (this branch):** `BlackHoleRegistry.SpawnPair` geometry from `StoatSlingConfig`
  (strength 2–12 along hold^1.5, horizon 2 u per strength, laid 2 horizons ahead, half gap 4 horizons,
  drift 20 u/s, life 4 s); Paczyński–Wiita pull `GM/(r − r_s)²` with GM = 20,000 × strength, into a
  velocity channel capped at 90 u/s, owner-only; Schwarzschild shadow (2.6 r_s) + Einstein lens
  (θ_E² = 2 r_s / D) and a white-hot repulsor; prisms captured at the horizon re-emerge from the repulsor
  point-reflected through the midpoint.
- **B — the crystal wormhole (`cece/charming-cerf-alf1j1`):** `CrystalWormhole.Curve` (separation
  (1−p)^0.75, spiral 2.5 turns, envelope (1−p)^1.5, anti-phase beat 0.5 × sin 2π·7·p²); Plummer wells
  softened by the throat; the felt law `k·cruise²·R_t·r/(r² + R_t²)^1.5` (k 4, ceiling 1.3 × cruise,
  reach 12 throats); the graded lens `α = A·u·e^(−u²/2)` (A 0.6, w = throat); seamless mouths (soft
  edge 0.65) that show the world carried by Δ and carry the pilot through by a pure translation.

In the game the same choice is one switch, `BlackHoleConfig.crystalPairs` (`Docs/BLACK_HOLE.md` §13): the
Black Hole tool's **Pair style** button (press B), or `blackhole style drift|crystal`.

The throat is mapped to the same size dial as the horizon (2 u per strength), so a full squeeze lays a
24 u throat — close to the crystal cell's shipped ~26 u. B's life is sling-tuned as `BlackHoleConfig`
ships it (0.6 s form, 0.05 s stand, 3.35 s annihilate = A's 4 s); the button "Their cell's 6 s / 9 s" restores the shipped spawnable's.

**Compare** is a split screen: each pair in its own corner of the field, 1,280 u apart, each half
rendered with only its own wells under one orbit. **Runs** records finish times per pair on each
course (seed × intensity × laps). **Decisions** go to the artifact's database collection `decisions`
(`topic`, `choice`, `note`, `style`, `settings` = the pair style, portal switches, course and every
number changed from shipped, `createdAt`, `by`); Claude reads them with `ArtifactData` (`list`,
collection `decisions`). The repo copy has no database: its log lives in that browser, and **Copy log**
exports it as text.

**What it does not model.** The course is a stand-in on the shipped numbers (8 rings on a 600 u
circle, mouths 48/40/32/26 by intensity), not `HeadlongCircuit`; the lenses are screen-space
approximations, not the game's ray march; B's warp field (`WarpFieldRuntime` / vessel scaling) and
A's frame dragging and tides on the hull are not modelled; the vector flight model's grip is replaced
by "velocity = nose × speed + the pull channel".

**Found by it.** The sling slung the LATEST per-frame squeeze; a released trigger sweeps back down
before the release edge, so every gamepad sling was minimum size. The studio's scripted pad releases
the way a thumb does, which is how it showed; `StoatSlingMath.Peak` fixes it (`STOAT.md`).

**Gamepads.** The page uses the browser Gamepad API. If the artifact viewer does not pass the
controller through, open `StoatFlightStudio.html` straight from the repo in Chrome.
