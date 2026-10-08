# Studios — web pages for vessel design decisions

A studio is a single HTML page that flies or shows a design **before** it is built in Unity, so a
decision is made by looking and playing rather than by reading numbers. It is a **reader** of the
shipped numbers, never their authority: every constant in it is copied from an asset or a class and
named after it, and when the asset changes the page must follow (or say on screen that it differs).

| Studio | Live page (decision log on) | Repo copy |
|---|---|---|
| Stoat Flight Studio | https://claude.ai/artifact/Busc3KW6DmVzbsiA2qxoHc | `StoatFlightStudio.html` |

## Stoat Flight Studio (round 3)

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
