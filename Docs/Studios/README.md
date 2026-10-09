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

## Stoat Flight Studio (round 9 — five play styles)

**Play styles.** The rail's **Play styles** card blends the sling and hole settings five ways: Comet
(velocity), Needle (precision), Anchor (forgiving control), Maelstrom (gravity) and Flare (surge). The
sliders elsewhere are now your *base* tuning, and the five weights blend it. A value a style has changed
shows underlined in blue, and its tooltip gives both numbers.

- **Presets** set one style to 1 and the rest to 0.5; **Balanced** returns all five to 0.5.
- **Live** makes a race move the weights:
  - each race starts them at 0.5;
  - element crystals along the course feed one style each: Time → Comet, Space → Anchor, Mass → Maelstrom,
    Charge → Flare;
  - a clean release (out of the grip, nose within 20° of the next ring) earns Needle;
  - unfed weights drift back to 0.5.
- **HUD.** The five weights show as bars on the stage.

The design, the full table of what each style changes, the measured results and the plan for taking it
into the game are in **`STOAT_PLAY_STYLES.md`**.

**Checked offline:**

- the presets measurably differ (table in that doc);
- a Mass crystal moves Maelstrom 0.50 → 0.65, then it drifts back;
- a clean release raises Needle 0.50 → 0.60 and a sloppy one does not;
- all 102 tooltip cards render, with no console errors.

## Stoat Flight Studio (round 8 — birth, annihilation, one pair, Stop)

**A pair's life** (the dipole pair; the **Birth & annihilation** group, 14 settings, each with a tooltip):

- **Birth.** A horizon's radius is proportional to the mass inside it, so the black hole's mass ramps from
  zero over 0.9 s and its horizon, pull, shadow and lens grow with it from a point. A white hole is a black
  hole run backwards, so it arrives the way it will leave, reversed. A light shell converges onto its spot
  from 700 u out over 1.1 s, bending the sky as it passes and washing the view with light where it crosses
  the camera, and its core lights as the shell lands.
- **Annihilation.** When the closing holes' horizons touch, the black hole sinks back to a point over 0.7 s
  and the white hole's light leaves as an outgoing shell. The pair's mass leaves as a **gravitational wave**:
  - a front moving out at 120 u/s, slowed from light speed so it can be watched;
  - a ringdown behind it (period 0.35 s, decay 1 s), weakening as 1/r;
  - strongest sideways to the line the holes closed along and silent along it (sin², as for a head-on
    collision);
  - it is transverse strain, not a pond ripple: the sky is bent, prisms stretch one way and squeeze the
    other, and the whole view stretches and squeezes as the wave passes the camera;
  - the crests and troughs are tinted brighter and darker as a labelled visual aid, which can be set to 0.

**One pair at a time.** A press while a pair exists — held, closing or annihilating — is refused, with an
on-screen notice, in every sling.

**Stop.** The ■ Stop button on the stage (or P) halts the Stoat and pauses the race clock while the world
carries on; ▶ Resume continues. While stopped, LT/RT lays a pair ahead to watch its whole life head-on. In
flight the annihilation usually happens behind you, so this is how to see it.

**Checked offline:**

- In a scripted run, a pair's horizon grows from 0.03 to 3.1 u over 0.7 s, and the white hole lights only
  once its shell lands.
- Presses while a pair is held, closing or dying never make a second pair.
- When the pair meets, its horizon sinks from 3.6 to 0.02 u and both the wave and the outgoing shell appear.
- The Stoat does not move while stopped.
- The sling's grip turns at the same rate as in round 6.
- All 93 tooltip cards render, there are no console errors, and the card fits a 420 px screen.

## Stoat Flight Studio (round 7 — hole settings and illustrated tooltips)

**New setting groups** on the side panel. Each is wired into what the studio flies and draws, so moving a
slider changes the stage as well as the previews.

- **Black hole** — physics: pull per strength (`gmPerStrength`), horizon per strength, the prisms' reach
  (`influenceAccelerationFloor`, `maxInfluenceRadius`), the prisms' pull ceiling and top speed, how
  released prisms settle (`releaseDamping`) and drift home, prism spaghettification (gain and limit), and
  the hull's (`vesselTideScale`). Look: shadow size (× horizon, physical 2.6), lens bend (× Einstein),
  lens reach (`lensRadiusMultiplier`), where the lens starts fading (`lensFadeStart`), and the photon
  ring's glow and width.
- **White hole** — its size and push relative to the black hole, how widely its push is spread, where
  things come out of it (× its horizon), and its look: core brightness (`whiteCoreBrightness`), sky seen
  through the core (`whiteCoreSkyMix`), core size and lens bend.
- **Pair life** — closing speed, closing run-up and the longest hold (moved out of the archived orbit
  group, since the dipole sling uses them).
- The dipole sling gains its two safety ceilings: force (u/s²) and fastest bend (rad/s).

Names in brackets are the game's `BlackHoleConfig` fields. The rest are the studio's own constants made
adjustable. Their shipped values are the numbers the studio used before, so nothing changes until a slider
moves; the sling probe gives the same results as round 6.

**Tooltips.** Every one of the 79 sliders has an ⓘ. Hover the row, focus the slider, or tap the ⓘ (which
pins it; Esc closes it) to open a card with:

- a plain-language description;
- what lowering and raising it does;
- two looping previews of the same moment re-run with only that number changed, at **30%** and **70%** of
  the slider's range;
- the range, the shipped value and the current value.

The previews are small 2D re-enactments built from the studio's own formulas:

| Preview | What it shows |
|---|---|
| sling pass | top-down: press, catch, the grip, release, the pair closing |
| hole view | per-pixel lens: shadow, Einstein ring, photon ring, white core |
| prisms | pulled, spaghettified, through the pair, then released |
| hull stretch | the hull stretching past a hole |
| pair life | the closing gap graph, and the hold cap |
| graphs | the squeeze curve and the key ramp |
| flight | turning circle, roll, speed |
| lope | the bounding body, side view |
| archived | the orbit sling; the crystal pair's life, felt pull, lens and layout |

Where a number only matters in an extreme case (the force ceiling, the reach limit, the ring), the card
says how its preview is staged so the difference is visible. Checked offline: every slider has a card,
none of the 158 previews is blank, there are no console errors, and the card fits a 420 px screen.

## Stoat Flight Studio (round 6 — strength and hold)

**The page.** The dipole sling is the only thing on show. Round 4's orbit sling and charming-cerf's crystal
pair (with the side-by-side compare) moved into a closed **Archive** card; the crystal pair is never the
remembered default. The game still runs the orbit sling; this round is the studio's.

**The sling.** One trigger, two inputs:

- **Squeeze depth = strength.** It is read live and eased over ~80 ms. Strength runs 0.5 → 12 (GM = 20,000 × strength, the game's
  unit) along squeeze^1.5. The horizon grows only 1.5 → 6 u: mostly mass, a little size. The white hole
  mirrors both and pushes with 0.5 × the pull, softened over twice its horizon. **Hold time plays no part in
  the strength.** A keyboard key gives a fixed squeeze (0.5, on the rail).
- **Neither hole moves.** The press lays the black hole 150 u ahead and 45 u to the trigger's side, and the
  white hole the same distance ahead on the other side.
- **The hold radius** is the circle on which the pull exactly bends you round at the engine's speed:
  GM = v²(r − r_s)²/r. **A heavier hole holds you on a WIDER circle**, which is also why it can catch you
  from further out. At 60 u/s this circle is 24 u at a 0.4 squeeze, 48 u at 0.7 and 78 u fully buried.
- **The grip.** Pass within 1.5 × that circle with the hole at least 50° off your nose, or get inside the
  circle, and it catches you. The engine then holds your speed, you settle onto the circle (3 /s), and you
  go round for as long as you hold, up to one full turn (360°, then it lets go on its own). Outside the
  grip the pull only bends you.
- **Release = slingshot.** The pair lets go of you. You leave on the tangent at that angle with a boost of
  speed × (0.25 … 0.9) by the squeeze, over 1.5 s. The pair then falls together and annihilates.
- **The camera** swings out and up on the far side of the circle while you are in the grip, so the hole you
  are going round stays on screen.

**Measured** (scripted pad, full throttle, no steering, RT held):

| squeeze | strength | hold circle | result |
|---|---|---|---|
| 0.15 | 1.2 | 10 u | not caught; bent 27°, release boost +17 u/s |
| 0.4 | 3.4 | 24 u | caught at 28 u; a full turn in ~2.5 s |
| 0.7 | 7.2 | 48 u | caught at 40 u; a full turn in ~4 s; released at 190° → out on the tangent, +38 u/s |
| 1.0 | 12 | 78 u | caught at 76 u; ~8 s a turn; released at 128° → +54 u/s |

The black hole's position was constant in every run.

## Stoat Flight Studio (round 5 — the dipole sling, superseded)

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
