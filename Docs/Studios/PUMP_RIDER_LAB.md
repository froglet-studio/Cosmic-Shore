# Pump Rider Lab

A `/labmaker` browser lab for a **new vessel concept** (no name yet): it latches onto prismscapes the
way the Urchin does, but it makes its speed the way a longboarder does on a pump track, by **leaning into
the bends and bending and extending its knees**. The question it puts to the designer:
**which way of doing that is worth building?**

- **Live page:** https://claude.ai/artifact/Vg9Y3u5hHRZiCX1TtnMxL8 (private to the owner until shared;
  decisions recorded there land in the shared log Claude reads at the start of each round).
- **Source (the page is published from it):** `Docs/Studios/PumpRiderLab.html`. Open it in Chrome to
  ride it with a gamepad (the claude.ai viewer can block the Gamepad API).
- **The Urchin it borrows from:** `Assets/_Scripts/Controller/Vessel/R_VesselActions/URCHIN_TRAIL_RIDER.md`
  (the 1D grind, latching, the speed carry) and `URCHIN_TRACK_PROJECTOR.md` (the rail prism size).
- **Gate:** `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/PumpRiderLab.html` (PASS, round 2).

The stage is a **3D world** (three.js r128): the rail drawn as real 3 × 3 × 6 u prisms every 8 u
(`UrchinTrackAction.asset`), and fixed things to read speed, height and lean against. A floor grid under the
oval (the rollers rise from it like dirt humps), seeded pillars, prism clusters and crystals along the
course, a gate every 600 u (white start gate on the oval), space dust that stays put while you pass through
it, a nebula sky, and the line of the rest of the course. Prisms are tinted by how the bend loads **you, at
your lean and speed**: amber where the rail dips toward you, blue where it humps away, white where a hump
would throw you off. The rider is a board, two legs whose knees visibly bend and extend, and an urchin body.

**Cameras** (Scene Config): *Chase (world up)* (default; the floor and sky stay still, so you see yourself
lean round the rail and climb each roller), *Trackside* (a TV camera beside the track; best for seeing
the humps), *Chase (rolls with you)*, and *Overhead* (the whole oval). The field of view widens with speed.

**Instruments:** the speed bar with the Urchin's 300 u/s grind marked; a **lap timer** (current, best,
last) on the oval, 1000 u splits elsewhere; a 10 s speed trace; the **bend dial**; and the **pump lane**,
a rhythm-game strip of the next 2.4 s: an amber STAND bar on each dip's deepest point and a blue CROUCH bar
on each hump's top, each as wide as one knee stroke, with your knees beside the hit line. **Every stroke is
rated** on screen: PERFECT / GOOD / EARLY / LATE / WRONG WAY with the speed it earned. **Sound** (header
button): wind and rolling that rise with speed and load, and a tone per rated stroke. A start card
explains the controls; the skilled AI rides until you touch a control. Slow motion (0.5×, 0.25×) is in
Scene Config.

---

## The model

**One law for every way that pumps.** Moving your hips by `dh` along your own "up" while the rail bends
under you by `kf = κ·cos(α − φ)` (κ the bend, α its direction round the rail, φ your lean; positive =
the rail dips toward you) changes speed by

    d(v²/2) = v² · kf · dh

which is angular momentum about the bend's centre: pull your weight toward it and you speed up. The
prismscape's own pull does work on your height, not your speed, so **on a straight rail pumping earns
nothing**. The Flat course is the control that proves it.

A stroke pays `bend × travel`, so the best stroke **stands up at the bottom of each dip** (the load's
peak) and **crouches at the top of each hump** (the load's valley). Standing up as a dip *starts* and
crouching as a hump starts is a square wave switched at the zero crossings, and it earns exactly zero
(see Found). Knee speed (`ppLegRate`) is what caps pumping: at speed the dips come faster than the knees
can stroke.

**The rail** is a curve given by its bend in a rotation-minimising frame, so the rider can sit anywhere
round it (the Urchin's grind already lets you roll round a rail). That has a consequence a longboard
does not: **with free roll, every bend can be a dip** (lean onto its inside) **or a hump** (its outside),
and both pay the same, because you crouch over a hump and stand in a dip.

**Leaning.** Three lean models (Input tab): *Free roll* (the Urchin today: the bend never moves you),
*Falls outward* (a bend rolls you to its outside, as it would a bead on a wire, so leaning in is a
balance), *Auto-bank* (the hull rolls onto the nearer side of each bend for you). A *lean limit*
(`ppLeanMax`) turns free roll into a longboard deck (±45° or so either side of where you landed).

**Crests** (Game Config): *Latched* (the Urchin: you never leave the rail) or *Throw you off past the
pull*: faster than `√(pull × radius)` over a hump and you fly, then land where the pull brings you back
(the normal part of the landing speed is lost).

**Speed loss:** rolling drag + speed² drag, and a scrub when the sideways load passes the grip (you did
not lean into the bend). An enemy ribbon multiplies the drag (`ppHostileDrag`); the Urchin instead
crawls there at 20 u/s.

## The ways (Play Style Config)

| Way | Pilot does | Speed comes from |
|---|---|---|
| **Dual stick** (round 2, default) | the Urchin's two sticks: XDiff = knees (apart = bent, together = straight), YDiff = roll round the rail, XSum / YSum = look only, A = push | the law, analog knee stroke; the push from a stop |
| **Knees** | trigger = knees (analog), stick = lean | the law, analog knee stroke |
| **Tap** | one button: hold = crouch, let go = stand; stick = lean | the law, snapped knees (`ppTapLegs` ×) |
| **Beat** | hold through humps, let go at the bottom of each dip; stick = lean | a kick of `ppBeatKick` × how deep in the dip you let go (no leg physics) |
| **Lean** | stick = lean only | the law, the hull's own knees at `ppAutoEff` |
| **Grind + knees** | throttle, trigger = knees, stick = lean | the Urchin's grind motor (300 u/s own ribbon) **plus** the law above it |
| Coast | stick = lean | nothing: the ability off (baseline) |
| Urchin grind | throttle | the shipped Urchin: speed chases 300 / 20 u/s (reference) |

## Shipped vs lab

`SHIPPED` holds the Urchin numbers this vessel would inherit, each naming its source (round 2 adds `PitchScaler` / `YawScaler` / `RollScaler` 90 °/s from `Urchin.prefab`, replacing the lab's 270 °/s lean rate):
`FriendlyTerrainSpeed` 300 / `HostileTerrainSpeed` 20 (`Urchin.prefab` `TrailFollower`),
`speedTrackingRate` 5 (`TrailFollower` C# default), `DefaultThrottleScaler` 65 (`Urchin.prefab`,
free-flight cruise), `rideSurfaceClearance` 1.5 (`GunVesselTransformer` C# default), `prismSpacing` 8
(`UrchinTrackAction.asset`). **Everything prefixed `pp` or `ai` is a lab proposal with no game field**:
the pull (1200 u/s²), knee travel (4 u) and rate (8 u/s), drag (1 u/s² + 10⁻⁴ v²/u), grip, the course
shapes (roller radius 70 u, wavelength 360 u), the per-way dials and the AI.

---

## Round 2 (2026-10-16): the dual-stick scheme and the push

The designer's direction, verbatim: *"two thumb controls based on sums and diffs. pitch and yaw remain the
same but only control look while attached. roll is also the same around the z axis of the blockscape. so
that is just like urchin. the difference is that urchin has a throttle on the xdiff but this will have leg
extension. so pulling your thumbsticks apart will be fully bent knees and full together (0 diff) will be
fully straight legs. players will take their thumb off a joystick to hit the a button and that will pulse
them down the line to build speed from a stop. this will feel like taking you foot off a skateboard to
push, but once you get pumping you wont need it."*

**Built as the new default way, Dual stick:**

- **The game's mix, line for line** (`DualStickMix.Mix`, `GamepadInputStrategy`): `XSum = Ease(rx + lx)`,
  `YSum = −Ease(ry + ly)`, `XDiff = (rx − lx + 2) / 4`, `YDiff = Ease(ry − ly)`, `Ease(x) = ±(1 − cos(x · 0.785))`.
  Browser pads report +y down, so y is negated to Unity's +y up (L-STU-8).
- **XDiff = knees**: knee bend = `clamp((XDiff − 0.5) × 2, 0, 1)`, so sticks fully apart = fully bent and
  together (XDiff 0.5) = straight; pushing them past together stays straight.
- **YDiff = roll round the rail** at `RollScaler` 90 °/s (`Urchin.prefab`); every way now rolls at the Urchin's
  rate instead of round 1's 270 °/s.
- **XSum / YSum only turn your look** at `YawScaler` / `PitchScaler` 90 °/s. The camera follows the look and the
  body turns to it; an aim line shows it. Input ▸ Look: *Turns with the rail* (an offset from the rail ahead;
  default) or *Fixed in the world* (the Urchin hull: it stays pointed where you left it, so you turn it through
  each berm). `ppLookReturn` (0 = stays) springs it back.
- **A = push**: one press adds `ppPushKick` 30 u/s × (1 − v / `ppPushTop` 160), delivered over 0.18 s, at most
  once per `ppPushCooldown` 0.45 s. It helps from a stop and does nothing at 160 u/s, so pumping has to take
  over. The back foot drops to the rail while you push.
- **The thumb comes off a stick.** Pressing A takes the right thumb off for `ppThumbOff` 0.25 s, and the right
  stick springs to centre: the knees half-straighten and the roll changes. A real pad does this; the AI writes it
  into its own sticks so the scorecard pays it too. Keyboard: Space / mouse = knees bent, A / D roll, arrows
  look, F push. Phone: each half of the view is a thumbstick (drag from where the thumb lands), plus a PUSH (A)
  button.
- **Runs start from a stop** (`ppStartSpeed` 0); the scorecard adds **To 150 (s)** and **Pushes /min**.
- Instruments: a STICKS panel draws both sticks as the game reads them, with knee bend, roll, look and the
  push cooldown. Ratings ignore strokes below 40 u/s, where they are noise.

**Measured** (page `runBatch`, seeds 1–2, from a stop; noise band over 8 seeds: Dual 1.9 u/s, Knees 0.9). The oval:

| Way | 20 s skilled / rookie avg | To 150 skilled / rookie (s) | Pushes /min | Perfect % | 60 s skilled / rookie avg (end) |
|---|---|---|---|---|---|
| Dual stick | 125.4 / 102.4 | 18.9 / — | 21 | 70.6 | 192.3 / 122.3 (332) |
| Knees (trigger) | 129.5 / 103.2 | 17.1 / — | 21 | 92.9 | 202.3 / 124.0 (353) |
| Tap | 131.0 / 103.6 | 15.8 / — | 21 | 92.9 | 228.4 / 128.3 (445) |
| Beat | 127.8 / 101.2 | — / — | 24 | 83.3 | 142.8 / 113.7 (165) |
| Lean | 119.7 / 96.8 | — / — | 24 | 0 | 131.3 / 101.0 (141) |
| Grind + knees | 341.2 / 320.9 | 0.13 / 0.13 | 3 | 90.9 | 365.1 / 334.6 (360) |
| Coast (no push) | 0 / 0 | — | 0 | — | 0 / 0 |
| Urchin grind | 297.1 / 297.1 | 0.15 / 0.15 | 0 | — | 299.0 / 299.0 |

(— in "To 150" = not reached in the run; in the 60 s runs the rookie reaches 150 at 52.4 s on Dual stick, 51.1 on Knees.)

### What it says

- **The push does what it was asked to**: from a stop, the skilled AI's pushes reach 60 u/s at 1.0 s, 100 at 2.0 s
  and 120 at 2.95 s; then the push stops paying and pumping carries on (150 at 18.85 s). Once pumping, the skilled AI stops pressing A.
- **The thumb coming off costs real speed.** With `ppThumbOff` 0, Dual stick scores exactly what trigger
  Knees does (129.5 u/s, 92.9% PERFECT, to 150 in 17.1 s). With the thumb off for 0.25 s per push it loses
  4.1 u/s, 1.8 s to 150, and 22 points of PERFECT, all of it in the pushing phase: started at 150 u/s (no
  pushing), Dual stick and Knees are identical (180.5 / 172.0).
- **The gap between "the push stops" and "pumping is strong" is slow.** Pumping pays with v², so from
  ~120 u/s to 150 takes the skilled AI about 15 s and the rookie most of a minute. Either the push should
  reach higher (`ppPushTop`), or the course should pay more at low speed (tighter rollers), or the rookie
  needs to keep pushing longer. That is a design call.
- **The Urchin's 90 °/s roll is enough for a skilled rider**: on the slalom from 150 u/s, Knees averages
  196.5 at 90 °/s against 198.5 at round 1's 270 °/s (no scrub either way), because the skilled AI leans onto
  the nearer side of each bend.

### Checked (round 2)

- `verify_lab.cjs` PASS (desktop and iPhone 13; determinism within a page and across loads).
- The mix is the game's: sticks fully apart give `XDiff` 1, centred 0.5; `sticksFor` (how the AI and the keys
  write sticks) round-trips through `mix` to 1.1e-16 over every unsaturated combination of 5 rolls × 4 bends × 3 yaws × 3 pitches.
- A real two-finger touch on the phone (Chrome DevTools touch events): a thumb on each half, dragged 40 px
  apart, bent the knees (h 4 → 1.1 u in 0.33 s) and handed the ride to the human.
- Screenshots read: the oval from a stop (PUSH pops, the STICKS panel, the back foot on the rail), the slalom
  trackside, the phone with both thumbsticks and the PUSH button.
- Flat pump control 0.00 for every way.

### Decision needed (designer, round 2)

1. **Push strength and reach**: `ppPushKick` 30 and `ppPushTop` 160 leave a slow stretch from ~120 to ~150 u/s.
2. **Look while attached**: turns with the rail, or fixed in the world like the Urchin hull.
3. **Thumb off**: keep the cost (it is what a pad does), or soften it (e.g. the knees hold their last bend while
   A is down).

## Round 1b (2026-10-15): fidelity — a world to ride through (numbers from 150 u/s; superseded by round 2's start from a stop)

The designer's verdict on round 1: *"without frames of reference this is very hard to appreciate at all.
Increase the lab fidelity heavily so I can try to enjoy this."* Round 1 drew a 2D-canvas rail in empty
space with a camera that rolled with the rider, so nothing in view was fixed: no up, no ground, nothing to
pass. Round 1b rebuilds the view in 3D around fixed references (above) and adds the game-feel layer (pump
lane, stroke ratings, laps, sound). **The physics is unchanged**; two things in the model moved:

- **The rail is integrated once per course shape and cached every 2 u**, so the world is fixed and the
  physics, AI, prisms, scenery and camera all read the same points. Runs now start one roller later
  (`s0 = (1 + k/4) × roller length`, so s never goes negative), which shifts the oval's and the wake's
  numbers by 1–4 u/s; the courses that repeat every roller are identical to round 1.
- **Stroke ratings** are an observer: each knee stroke is compared with the best placement of a stroke of
  the same length within half a roller (so it judges timing, not knee speed). PERFECT ≥ 85%, GOOD ≥ 55%,
  otherwise EARLY / LATE by which side of the best placement it fell, and WRONG WAY when it cost speed.
  New scorecard column **Perfect %**.

**Re-measured** (page `runBatch` / `runCourseGrid`, seeds 1–2, 20 s, latch on at 150 u/s; noise band over
8 seeds 0.5–1.1 u/s, 3.1 for Beat). The oval, latched, free roll:

| Way | Skilled | Rookie | End | From cruise | Pump (u/s²) | Flat pump | Perfect % skilled / rookie |
|---|---|---|---|---|---|---|---|
| Knees | 180.9 | 171.6 | 216 | 62.4 | 7.63 | 0.00 | 94.4 / 88.9 |
| Tap | 187.3 | 175.8 | 235 | 62.6 | 8.82 | 0.00 | 85.7 / 88.9 |
| Beat | 154.5 | 153.3 | 162 | 66.2 | 3.98 | 0.00 | 71.4 / 85.7 |
| Lean | 152.3 | 151.2 | 157 | 59.5 | 3.68 | 0.00 | 0 / 0 |
| Grind + knees | 362.1 | 322.7 | 409 | 360.4 | 20.87 | 0.00 | 87.1 / 66.0 |
| Coast | 122.6 | 122.6 | 100 | 51.8 | 0 | 0 | — |
| Urchin grind | 298.6 | 298.6 | 300 | 297.7 | 0 | 0 | — |

Every course (skilled / rookie): Knees 181/172 · 198/183 · 199/183 · 196/182 · 135/132 · 123/123 (oval,
rollers, slalom, twister, wake, flat); Grind + knees 362/323 · 399/336 · 399/335 · 360/321 · 301/300 ·
299/299. Rollers, slalom, twister and flat re-measure identically to round 1 (their shapes repeat every roller); the oval and the wake (which never repeats) move by 1–4 u/s, e.g. Knees on an enemy's oval ribbon 92.5 → 89.5.

### What the ratings add

- **The rating has a control.** Planting round 1's zero-crossing AI back (`extend = kP > 0`) rates 0.0%
  of its strokes PERFECT on rollers (and earns 0.04 u/s²), against 95.5% (10.95 u/s²) for the real AI.
- **Speed makes timing harder.** On rollers after 60 s, the skilled AI is going 500+ u/s and only 50.8%
  of its Knees strokes are PERFECT (the rookie, at ~275 u/s, 77.7%): the dips come faster than its lead
  allows for. The rating shows the player where the skill ceiling is.
- **The Lean way's automatic knees are always late** (0% PERFECT): they react to the load passing its
  peak instead of leading it. That is why Lean pumps at half the rate of Knees, and a human leaning cannot
  fix it.

### Checked (round 1b)

- `verify_lab.cjs` PASS (desktop and iPhone 13; WebGL stage, so the blank check is replaced by reading
  the screenshots); determinism within a page and across loads unchanged.
- Screenshots read: chase on the oval (climbing a roller over the floor grid, pillars and clusters
  passing), trackside (the humps in profile, a gate, the dips amber ahead), overhead (the oval laid out),
  slalom chase (the rider leaned 90° into a bend), phone portrait (instruments clear of the rider and the
  thumb zones), and the start card on both.
- Planted wrong law (`N·dh`): Flat pump −0.85 (Knees, Tap), −0.59 (Grind + knees), 0.00 for the real law.

### Found (round 1b)

- **three.js r128 sizes `instanceColor` from the CURRENT instance count.** Zeroing `count` before the
  first `setColorAt` made a zero-length colour buffer: every per-prism tint was silently dropped (no error),
  and the rail drew in one flat colour. Colour every slot first, then zero the count.
- **Lighting and ACES tone mapping washed the load tint out**; the rail and gates are now unlit (they
  glow, like the game's prisms) and the scenery is dark, so the amber/blue reads at a glance.
- **A rating against an instant stroke punished slow knees, not bad timing** (the skilled AI rated 16% on
  the oval at Grind speed); rating against the best placement of the same-length stroke fixed it.

## Round 1 (2026-10-15) — superseded view; numbers kept as measured

Measured with the page's own `runBatch` / `runCourseGrid` (headless, AI pilots, 60 Hz, seeds 1–2, latch
on at 150 u/s, 20 s unless stated). Columns: average speed for the skilled and rookie AI, the skilled end
speed, the skilled average when latching on at the 65 u/s cruise, and the **Flat pump** control (u/s² that
pumping added on a straight rail; must be ~0). **Noise band:** 8 seeds on the oval give a standard
deviation of 0.4–0.9 u/s per way (3.0 for Beat).

**The pump-track oval** (four rollers, a 180° berm, four rollers, a berm), latched, free roll:

| Way | Skilled | Rookie | End | From cruise | Pump (u/s²) | Flat pump |
|---|---|---|---|---|---|---|
| Knees | 184.5 | 173.2 | 221 | 63.4 | 8.00 | 0.00 |
| Tap | 191.4 | 178.0 | 242 | 63.6 | 9.34 | 0.00 |
| Beat | 156.8 | 155.1 | 163 | 69.2 | 4.09 | 0.00 |
| Lean | 153.9 | 151.8 | 160 | 59.9 | 3.89 | 0.00 |
| Grind + knees | 365.8 | 322.8 | 410 | 363.3 | 21.32 | 0.00 |
| Coast (off) | 122.6 | 122.6 | 100 | 51.8 | 0 | 0 |
| Urchin grind | 298.6 | 298.6 | 300 | 297.7 | 0 | 0 |

**Every course** (skilled / rookie average u/s):

| Way | Oval | Rollers | Slalom | Twister | Wake | Flat |
|---|---|---|---|---|---|---|
| Knees | 185 / 173 | 198 / 183 | 199 / 183 | 196 / 182 | 138 / 137 | 123 / 123 |
| Tap | 191 / 178 | 208 / 189 | 209 / 190 | 205 / 187 | 138 / 137 | 123 / 123 |
| Beat | 157 / 155 | 165 / 161 | 171 / 167 | 164 / 160 | 134 / 133 | 123 / 123 |
| Lean | 154 / 152 | 159 / 156 | 159 / 156 | 158 / 156 | 134 / 133 | 123 / 123 |
| Grind + knees | 366 / 323 | 399 / 336 | 399 / 335 | 360 / 321 | 301 / 300 | 299 / 299 |
| Coast | 123 / 123 | 123 / 123 | 123 / 123 | 123 / 123 | 123 / 123 | 123 / 123 |
| Urchin grind | 299 / 299 | 299 / 299 | 299 / 299 | 299 / 299 | 299 / 299 | 299 / 299 |

**Options, skilled average (rookie), u/s:**

| Setting | Knees | Tap | Lean | Grind + knees |
|---|---|---|---|---|
| Rollers 60 s, crests latched | 356.7 (274.7) | 493.9 (298.6) | 180.6 (171.4) | 468.2 (352.0) |
| Rollers 60 s, crests throw you | 259.7 (241.3) | 268.6 (247.8) | 180.6 (171.4) | 302.0 (300.4) |
| Slalom, free roll | 198.5 (183.4) | 208.8 (189.7) | 159.3 (156.1) | 399.0 (334.7) |
| Slalom, deck ±45° | 166.5 (163.4) | 170.1 (166.4) | 146.6 (146.7) | 297.7 (297.6) |
| Oval, an enemy's ribbon | 92.5 (90.0) | 93.5 (90.9) | 85.1 (84.3) | 92.5 (90.0) |

On an enemy ribbon the shipped Urchin averages 21.2 u/s and Coast 71.5.

### What it says

- **Pumping works as a skill.** On every bendy course the skilled pilot beats the rookie and both beat
  coasting; the Flat control is 0.00 for every way. Knees and Tap gain 8–13 u/s² on the oval and
  slalom; Lean and Beat about half that.
- **Pure pumping cannot get going from cruise.** Latching on at the Urchin's 65 u/s, every pure-pump
  way ends slower than it started (59.9–69.2 average): the gain grows with v², so below ~100 u/s drag
  wins. A pump vessel needs a motor, a fast entry, or a much steeper course.
- **Grind + knees is the strongest and the safest port**: 366 on the oval and 399 on rollers/slalom
  against the Urchin's 299, with the Urchin underneath unchanged. It also has the largest skill gap
  (366 vs 323).
- **Lean (the hull pumps for you) is forgiving and flat**: rookie within 2 u/s of skilled, and about
  +30 u/s over coasting. Leaning alone makes the pilot a passenger.
- **Crests that throw you are the natural speed limit**: ~260–270 on R 70 rollers at the 1200 pull
  (`√(1200 × 70)` = 290). With crests latched only drag caps it, and Tap reaches 762 u/s after 60 s.
  With throwing crests, Grind + knees cannot pump past 300 at all (300 > 290) and pays 10.8 u/s² of scrub.
- **A longboard deck limit costs about a sixth** on the slalom (Knees 198.5 → 166.5): with free roll
  every bend can be put under you.
- **Real wakes are too gentle to pump**: the wake stand-in gains only ~15 u/s over coasting. Pumping needs
  authored rollers and berms, or a vessel that lays its own (the Urchin's track projector, shaped).
- **Enemy ribbons become raidable**: a pump rider averages ~90 u/s on an enemy ribbon against the
  Urchin's 21, with drag ×4 there. That is a balance question, not a result.

### Checked

- `verify_lab.cjs` PASS (desktop 1600×900 and iPhone 13: no console or page errors, no page scroll, the
  `__lab` hook, every SPEC key in SHIPPED and in range, the manual clock, `runBatch` deterministic within
  a page and across two loads, the stage not blank); `verify_lab.cjs --self-test` OK.
- **Planted defect:** counting the pull's share as speed (`N·dh` instead of `v²·kf·dh`) makes the Flat
  pump column read −0.85 for Knees and Tap and −0.59 for Grind + knees (flagged red on the page), against
  0.00 for the real law.
- Noise band measured (8 seeds); the full default batch runs in ~70 ms.
- Screenshots read at desktop (rollers, oval, slalom, wake; every camera) and phone.

### Found

- **The first AI pumped at the zero crossings and earned 0.04 u/s²**: it stood up when a dip *began*.
  Centring each stroke on the load's peak lifts Knees on rollers from 0.04 to 10.95 u/s² of pumping and
  from 123.3 (no better than coasting) to 197.8 u/s average (20 s, seeds 1–2, re-measured on round 1's
  numbers by planting the old rule back).
- **The first AI spun around the rail every half roller** chasing the bend's inside, for no gain: a hump
  pays the same as a dip if you crouch over it. It now takes the nearer side, and gets under the bend
  only when a hump would throw it off.
- **The first Beat kick was a percentage of speed** and compounded to 11 000 u/s in 60 s; it is a fixed
  u/s kick now.
- **The AI is not a test of the lean models.** It balances "Falls outward" and ignores "Auto-bank"
  (identical numbers within the noise band), so those are questions for a human pilot's hands.

### Decision needed (designer)

1. **Which way:** Knees, Tap, Beat, Lean, or Grind + knees (or a mix: Grind + knees with Lean as the
   easy mode).
2. **Motor or pure pump.** Pure pump cannot leave cruise speed; Grind + knees keeps the Urchin's motor.
3. **Lean:** free roll round the rail (Urchin) or a deck limit (longboard).
4. **Crests:** latched (no speed limit but drag) or thrown off past the pull (a limit set by the course).
5. **Lean model** for a human: free, falls outward, or auto-bank. Ride each one yourself; the AI can't tell them apart.
6. **Enemy ribbons:** how much a pump rider should be able to raid them.

### Not modelled

Only 1D prismscapes (trails). Riding a 2D surface (the Urchin's marble roll), where carving S-turns is
the real longboard case, is the next round. The ribbons are stand-in bend functions, not laid trails. No
free flight, approach or latching, no Slip, no prism payoff (grow / steal / restore), no spikes, no
other vessels. The legs, hull and camera are lab drawings. The CoM radius change is folded into the
law to first order. Frame time is not measured on a real GPU. Ideas not built: an ollie (stand up hard
on a crest to jump to another ribbon); pressing into the prismscape to dig your own dip (the cradle
drape); a projector that lays rollers.

### Next round

Read the decision log; build what was picked on a 2D sheet (a gyroid-like surface pump track); if
Grind + knees wins, model the free-flight carry after leaving the rail (`detachSpeedDecayRate` 36) so
pumped excess speed has somewhere to go. When the concept settles, it becomes a bay in the Vessel Studio
(`/vessel-studio`), not a second studio.
