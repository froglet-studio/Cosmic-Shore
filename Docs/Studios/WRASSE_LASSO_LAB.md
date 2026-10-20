# Wrasse Lasso Lab

A browser lab (`/labmaker`) that tries new phone controls for the Wrasse's kite. Page:
`Docs/Studios/WrasseLassoLab.html`. Live artifact: https://claude.ai/artifact/T5EBvmTvFw4KNRMQtwXDQb (private to
Garrett until shared from its Share menu; its own decision log).

**The question** (Garrett, 2026-10-20): "the wrasse and goby are really getting to a fun place. i want to further
optimize the mobile experience for the wrasse. it gets old just swirling your finger around in circles. i think we can
do something like the thresher where your finger swings the wrasse around on an invisible tether. give me a mobile
friendly artifact that can explore some fun and satisfying ways to lasso things that require less movement, but more
finesse."

**Who else works here:** the Wrasse is Garrett's crew vessel (landed in #1192 from `cece/compassionate-babbage-gc13io`;
`Assets/_Scripts/Controller/Vessel/Crew/KITE_NAVIGATOR.md`). Its own lab is the Crew Vessel Lab
(`Docs/Studios/CrewVesselLab.html`), which still waits to catch up with rounds 4-4f (`T-20261018-3247`). This lab
does not change either. It reads the shipped kite and changes only how the thumb holds it. In flight on
`cece/charming-wright-4ayoce` (the Wrasse's phone round: tap-to-press cards, the PIP swap) and `cece/sharp-tesla-j0kd7z`
(the Goby). Neither touches the kite's controls.

## Round 1 (2026-10-20)

### What the page is

- **The shipped kite, unchanged:**
  - `KiteNavigatorCore`: 2.5 u tail step, a crossing closes the loop, the tail trimmed to 2.4 board radii (384 u), the
    newest three segments skipped.
  - The loop rule: one element fires base × 2^(n−1), and a mixed loop fizzles.
  - The motes: 26, in clusters of about 4, spread 18 u.
  - Every effect number, from `KiteNavigatorConfig.asset`.
  - **Follow** is the shipped control, line for line (`KiteNavigatorCore.Move`: 320 u/s, accel 12, approach gain 7,
    coast 4, 21 mm above the thumb on touch).
- **Four proposed controls.** Each changes only how the thumb moves the kite:
  - **Sling** (tether): the thumb is a hand on a 42 u rope. Dragging tows the kite; small circles whirl it round in
    big ones, the way a lasso is spun. Lift to let go.
  - **Plant** (anchor, the Thresher's plant): touch plants an anchor in the world. The kite hooks onto an orbit round
    it, keeping its speed, and the chain is rigid. It swings faster every lap (`plSpin`), and the anchor drifts with
    the world. Lift to fling it off along the swing (×1.15). Sliding the thumb steers the anchor at 0.35 (the
    Thresher's pivot steer is under half).
  - **Hook** (flick): a flick throws the kite. The last quarter second of the swipe sets the throw. A straight flick
    darts. A flick that curls at the end throws travelling loop-de-loops: a spinning velocity on top of a drift, a
    prolate trochoid that crosses itself every turn. Touch to catch.
  - **Well** (gravity): the thumb is a gravity well. The kite's climb away from it is damped, so a flyby is caught
    into orbit.
- **Four games, one per element** (`/labmaker` §1.5):
  - **Round-up (Time race):** clear six lit clusters; a clean loop round 3+ lit motes clears one; lose one and +3 s.
  - **Breaker (Space destruction):** rival prisms stream down; the head pops them, and Space loops fire the
    AOEExplosion's radius.
  - **Bloom (Mass creation):** 6 × 2^(n−1) prisms per Mass loop.
  - **Joust (Charge dogfight):** Charge loops bank petals; ram the dodging rival to steal them.
  - Plus **Free lasso**, with no clock.
- **The scorecard:** the five controls × a skilled and a rookie AI × the four games × seeds, plus an idle-thumb
  control. The AI pilots write the thumb (input only), each with a heuristic written for its control.
- **Thumb travel** is measured on the glass, touching or not. A tap somewhere else costs the move to get there. The
  scorecard converts it to millimetres with a 57 mm board, about an upright phone. Your own runs use your screen.
- **The decision log** is shared (the artifact's `decisions` collection), with a browser fallback.

### Measured (`window.__lab.runBatch({seeds:[1,2,3,4,5,6]})`, lab defaults, Medium rival)

| Control | Pilot | Round-up s ↓ | Breaker | Bloom | Joust | Loops/min | Motes/loop | Value/min | **Thumb mm/loop** ↓ | Thumb mm/min | Fizzle % |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Follow (shipped) | skilled | 82.9 | 330 | 282 | 23.3 | 15.9 | 2.66 | 153 | **147** | 2337 | 16 |
| Follow (shipped) | rookie | 117.2 | 292 | 206 | 4.2 | 9.0 | 2.60 | 91 | 168 | 1520 | 19 |
| Sling | skilled | 34.2 | 639 | 459 | 25.7 | 24.0 | 2.81 | 198 | **91** | 2179 | 30 |
| Sling | rookie | 35.2 | 668 | 347 | 38.0 | 23.0 | 2.73 | 216 | 83 | 1900 | 27 |
| Plant | skilled | 42.8 | 693 | 502 | 38.2 | 19.4 | **3.30** | **246** | **29** | 556 | 27 |
| Plant | rookie | 51.4 | 559 | 409 | 15.2 | 13.9 | 3.11 | 178 | 32 | 440 | 35 |
| Hook | skilled | 74.1 | 412 | 242 | 16.7 | 21.4 | 2.14 | 75 | **20** | 437 | 18 |
| Hook | rookie | 144.0 | 322 | 180 | 15.7 | 14.9 | 2.04 | 59 | 29 | 439 | 19 |
| Well | skilled | 121.6 | 250 | 111 | 9.0 | 11.7 | 2.13 | 49 | 113 | 1319 | 15 |
| Well | rookie | 137.2 | 227 | 59 | 5.7 | 7.2 | 2.23 | 31 | 181 | 1303 | 21 |
| Control: idle thumb | — | 185 (DNF) | 47.5 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |

**The noise band** is seeds 1-3 against seeds 4-6, skilled rows.
- **Stable:** thumb mm/loop ±1-4, loops/min ±0.4-3.4, motes/loop ±0.3.
- **Wide:** the game scores move by up to ±140 in Bloom and ±75 in Breaker, and Sling's Joust by 42. So read the four
  game columns as rough.
- **The idle control** is 0 everywhere but Breaker (47.5), where rival prisms flow into a still head. That is Breaker's
  floor.

**What it says:**
- **Plant does the most with the least.** Per loop it needs a fifth of the shipped control's thumb travel (29 mm
  against 147). Its loops hold the most motes (3.3 against 2.66), and its value per minute is the best (246 against
  153). A planted anchor drifts with the motes, so a skilled pilot plants on the cluster and the orbit drags the loop
  round it.
- **Hook is the least work (20 mm per loop) and the hardest to aim.** At 2.1 motes per loop it has the lowest value,
  because loop-de-loops land beside the cluster as often as on it. Its rookie takes 144 s on Round-up against the
  skilled pilot's 74. That is the widest skill gap on the board, the "finesse" signal.
- **Sling catches the most loops per minute (24) but the thumb never stops**: 2179 mm per minute, the shipped
  control's level. Rookie and skilled score alike, so on this evidence it is not a finesse control. A person spins
  differently from the AI's 90°-lead hand, so this is the column most worth checking by hand.
- **Well is the weakest here**, below Follow on every column. Its pilot is the least developed. Try it by hand before
  ruling it out.

### Checked

- `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/WrasseLassoLab.html`: **PASS** at desktop 1600×900 and an
  iPhone 13. The page loads with no console errors and fits the window without clipping. The hooks are present, every
  shipped value is inside its range, the manual clock advances, `runBatch` is deterministic within a page and across
  loads, and the stage is not blank. Screenshots were read: the phone shows the stage full width, the controls in two
  rows, and Settings in a pull-up sheet.
- `window.__lab.selfTest()`: an idle thumb closes no loop; a scripted thumb that circles a pure 3-mote Space cluster
  fires one loop at n = 3; the same circle round a mixed cluster fizzles once and fires nothing.
- `runBatch()` with 3 seeds took about 3 s headless.

### Found

1. **The tail caps a loop at about 61 u radius.** The shipped tail is 2.4 × 160 = 384 u, and one lap must fit inside
   it, so 2πr ≤ 384. The first tether and anchor ropes (70 u and 110 u) made orbits that could never close a loop:
   0 loops in 12 s. Any orbit control the game takes needs its rope under about 55 u, or a longer tail.
2. **A clean orbit round a fixed point never closes a loop.** Each lap lies on the last, so the crossing test sees
   tangent segments, and an inward spiral never crosses itself either. Loops close because the anchor or the motes
   drift with the world, which turns the orbit into a trochoid that crosses itself every lap. On a still board
   (`labFlow` 0), Plant and Well need the proposed snap-shut rule (`labSnap`): a loop closes when the head passes
   within that distance of its own tail.
3. **Planting far from the kite makes a pendulum, not an orbit.** A rope that only pulls flings the kite straight
   through the anchor and out the other side. The Thresher hooks its ship onto the orbit at once. The lab does the
   same: the radial speed is turned into swing and the speed is kept (`plHook`). The ship reels in to the longest rope
   while swinging (`plReelIn`).
4. **The game's thumb offset is large on this board.** 21 mm is about 117 u at the scorecard's 57 mm board, nearly
   three quarters of the board's radius. It matters only for Follow. The tether controls hang the kite off the thumb
   by the rope instead.

### Decision needed (designer)

- **Which control goes to Unity**, if any. The scorecard's pick for "less movement, more finesse" is **Plant**. Hook is
  the finesse runner-up. Feel decides between them; the numbers do not. Record it in the page's Decisions tab
  ("Which control goes to Unity").
- **Snap-shut loops** (`labSnap`): keep the game's crossing-only rule, or let a loop close when the head passes near
  its own tail?
- For the pick, its feel numbers (rope length, spin-up, fling, steer).

### Not modelled

- The 3D world: motes and prisms live only on the board here, with nothing above or below the plane.
- The pilot's turns: the board never rotates, and the world flows down at a steady 40 u/s.
- The real AOEExplosion and Mass cluster (here a ring and a counter), rival hulls that fight back, the HUD, sound and
  network play.
- The AI pilots are heuristics written for each control, so each control's score is partly a score of its pilot.

### Next

Play it on a phone (Garrett), record the pick, then a round 2 on the picked control: its L5s, and how it reads on the
real Wrasse's kite view. Then `/labmaker` §7: a `KiteNavigatorCore` control mode with a parity gate against this page,
landed on the Wrasse in the Toybox Vessel Changer.
