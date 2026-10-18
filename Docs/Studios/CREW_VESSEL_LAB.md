# Crew Vessel Lab

A `/labmaker` browser studio for the first **multiplayer vessel**: one hull, two seats. The question it
puts to the designer, from the brief ("Maverick and Goose"):

> Make a vessel that is meant to be played by several players. Start with a pilot and a navigator: a
> player who does not want to process a 3D environment plays a 2D puzzle while the other player flies.
> If there is no other player, an AI flies. The D-pad swaps players between flyer and puzzler, as if
> they had another squad mate.

**Round 3 was signed off on 2026-10-17 ("this is fun, build it in Unity") and has graduated:** the game's port is
`Assets/_Scripts/Controller/Vessel/Crew/` (start at `KITE_NAVIGATOR.md`). Round 3 is one design: a kite on the flying vessel's x-z
plane, flown with one thumb (offset above it on a phone). Its head destroys hostile prisms and rams rivals; its tail is
a lasso; a loop of one navigator-only element fires an exponential effect (Space blast, Mass cluster, Time boost, Charge
steal). Rounds 2 (twelve one-thumb presets) and 1 (four consoles) are kept below.

How to use round 3: **Navigate** (or touch the board) to fly the kite while an AI flies the vessel; **Fly** to fly the
vessel (A/D yaw, W/S pitch, Q/E roll, Space boost while you hold boost energy, Shift brake); **Watch**; or **Couch** for
two people. Pick a variant in the Presets tab. Swap seats with the D-pad left/right, 1 / 2 or the header button.

- **Source (the page is published from it):** `Docs/Studios/CrewVesselLab.html` (rounds 1 and 2: `CrewVesselLab.round1.html`, `CrewVesselLab.round2.html`). Open it in Chrome to use a
  gamepad (the claude.ai viewer can block the Gamepad API).
- **Live page:** https://claude.ai/artifact/JtQptDHQhNdC2CxPmspfct (private to the owner until shared from the page's
  Share menu; decisions recorded there land in the shared log Claude reads at the start of each round).
- **Verify:** `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/CrewVesselLab.html` (PASS, round 3).

## Round 3 (2026-10-17): the kite and its tail (signed off; graduated to Unity)

**Input this round** (the designer, in substance):
- On mobile, a proper thumb offset, so the thumb never covers the play.
- Keep the Lasso for collecting, collected with the kite's lasso TAIL. Keep the Guardian's direct prism destruction, done with the kite's head.
- Loop only one type of element and you get an effect, exponentially bigger for each mote you surround:
  - Space: a prism-destroying explosion;
  - Mass: a mass cluster;
  - Time: boost energy for the pilot;
  - Charge: powers up the kite, so it steals more crystals for the pilot when it collides with a vessel.
- The elements are seen only by the navigator and are not the game's crystals.
- The prisms, the vessel and the effects must match the game.
- Sign off one more time before trying it in Unity.

The decision log was empty again; this message is the round's input. Round 2's page is kept as
`CrewVesselLab.round2.html`, and its numbers are in the Archive tab.

### The design under sign-off

- **One thumb flies the kite.** Hold anywhere on the board: the kite flies toward an aim point
  **`thumbOffset` 80 px above the thumb** on a touch screen (0 on a mouse, with an option to feel the offset on a PC),
  so the thumb never covers the kite, its tail or what is ahead of it. Lift the thumb and the kite coasts to a stop.
- **The head destroys.** Hostile prisms (any domain but ours) whose shadow the head touches are destroyed. The head
  also **rams rival vessels**: with kite power ≥ 1, a ram steals that many whole petals (only what the rival holds).
- **The tail collects.** The kite drags a tail `tailLen` 1.6 board radii long. Cross it and the loop closes; every
  touchable mote inside is taken.
- **One element per loop fires an effect, `base × growth^(n − 1)` for n motes** (`growth` 2, so every extra mote doubles it):

| Mote | Effect | Base (1 mote) | What it is in the game |
|---|---|---|---|
| Space | A blast at the loop's centre that destroys hostile prisms, sparing our own | MaxScale 60 (radius 30 u), cap 640 | `AOEExplosion`: radius `0.5 × MaxScale × sin(t·π/2)` over 3 s after 0.2 s (`AOEExplosion.prefab`), own domain spared (`AffectSelfOverride`) |
| Mass | A cluster of our domain's prisms at the loop's centre | 6 prisms, cap 240 | Sparrow trail prisms (BaseScale 2 × 2 × 5). **New:** the game has no cluster spawner |
| Time | Boost energy for the pilot | 1.5 s, holds up to 30 s | Sparrow boost (`boostMultiplier` 5 on the throttle term: 35 → 135 u/s). **New:** the Sparrow has no boost meter |
| Charge | Kite power: the next ram steals that many petals | 1 petal, cap 30 | `ElementalTransfer.Steal` (whole petals, only what is held) |

- **A mixed loop fizzles**: the motes are taken and nothing fires (option: *Majority* fires the biggest element, one
  mote weaker per stray).
- **Motes are the navigator's own.** They are orbs with element glyphs, not crystals, and only the board draws them.
  By default they **live on the board**: they flow past with the world but always stay in the plane, so they are
  always touchable. Option: *in the world*, fixed in space, so only what the pilot's plane cuts through can be looped.
- **Matching the game.** Every hull flies the Sparrow's numbers and lays the Sparrow's prisms (one per 7 u). Two AI
  rivals (Ruby, Gold) fly the same model, lay hostile trails and carry petals. Domain colours are the game's palette.
  The game's own crystals still exist: the pilots fly for them, and the board does not show them (option: outlines).

Presets are eight variants of this one design: **Kite & Tail** (the candidate), Gentle growth (×1.5), Steep growth (×3),
Long tail (2.6), Short tail (1.0), Forgiving mixes, Locked board, Motes in the world, and Thin slice (motes in the world, ±18 u).

### Scorecard

`runBatch()` defaults: 3 seeds × 60 s at 30 Hz, AI Hard pilot, 2 AI rivals; each preset with a Hard, an Easy and an
idle navigator. **Skill** = how much less an Easy navigator produces (hull petals + boost seconds + prisms ÷ 10), as %
of Hard; **Idle** = what a navigator who never touches produces, as % of Hard. Re-run with **Score every preset** (about 8 s).

| Preset | Hull petals/min | Lift % | Stolen/min | Popped/min | Blasted/min | Mass/min | Boost s/min | Loops/min | Pure % | Mean n | Best n | Skill % | Idle % | Moments/min | Dry spell (s) | Thumb down % | Board turn °/s | ± SE |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Solo pilot | 10.00 | 0 | | | | | | | | | | | | | | | | 1.00 |
| **Kite & Tail** | 28.67 | +186.7 | 11.33 | 43.7 | 12.0 | 92 | 16.5 | 18.33 | 86 | 2.25 | 4.3 | 66.5 | 19.6 | 24.7 | 8.5 | 93 | 41.5 | 1.45 |
| Gentle growth (×1.5) | 26.00 | +160.0 | 11.67 | 52.0 | 7.0 | 62.3 | 8.38 | 20.67 | 90 | 2.37 | 4.0 | 48.8 | 25.2 | 30.0 | 6.2 | 93 | 37.2 | 4.58 |
| Steep growth (×3) | 40.00 | +300.0 | 23.67 | 40.7 | 47.0 | 366 | 27.25 | 18.67 | 83 | 2.25 | 4.7 | 79.8 | 10.4 | 25.3 | 9.2 | 94 | 38.0 | 10.12 |
| Long tail (2.6) | 29.00 | +190.0 | 12.33 | 43.0 | 32.0 | 74 | 16.0 | 20.00 | 87 | 2.14 | 4.3 | 66.5 | 19.6 | 26.3 | 7.3 | 93 | 41.1 | 1.53 |
| Short tail (1.0) | 13.33 | +33.3 | 3.33 | 58.3 | 0 | 16 | 5.5 | 4.33 | 80 | 2.19 | 3.3 | 34.8 | 44.7 | 15.0 | 18.5 | 91 | 35.6 | 2.33 |
| Forgiving mixes | 36.00 | +260.0 | 17.67 | 49.0 | 0 | 102 | 15.0 | 16.67 | 83 | 2.16 | 5.0 | 64.0 | 17.8 | 24.3 | 10.0 | 93 | 41.0 | 8.54 |
| Locked board | 25.33 | +153.3 | 8.33 | 38.0 | 1.7 | 92 | 15.5 | 16.67 | 87 | 2.12 | 4.3 | 38.5 | 22.0 | 23.7 | 9.6 | 93 | 57.0 | 5.46 |
| Motes in the world | 21.67 | +116.7 | 7.00 | 95.7 | 0 | 6 | 8.5 | 12.00 | 89 | 1.71 | 3.3 | 68.2 | 29.1 | 23.7 | 8.1 | 86 | 34.8 | 3.67 |
| Thin slice | 17.00 | +70.0 | 4.33 | 62.3 | 32.7 | 44 | 13.5 | 7.33 | 93 | 2.11 | 3.7 | 71.5 | 26.0 | 16.0 | 11.4 | 78 | 35.4 | 4.51 |

**Noise.** Three seeds of a minute is a small sample: the standard error of hull petals is 1.5–10 per minute, and
the effect columns swing more than that (see Found 2). Read differences under about 30% as a plateau.

### Found

1. **The kite nearly triples the hull's petals** (10 → 28.7 a minute, +187%). About 11 of those come from rams that
   steal rival petals. Time loops hand the pilot 16.5 s of boost a minute, which buys faster crystal runs.
2. **The exponent amplifies cluster size, so the mote spawner is a balance knob as strong as the growth rate.** One
   mid-round change moved the motes in each cluster 4 u further apart (spread 14 → 18). Space blasts fell from 327 to
   12 prisms a minute, and the mean loop from 2.8 to 2.25 motes. With ×2 growth a 4-mote Space loop is MaxScale 480
   (a 240 u blast radius, three-quarters of the cell's 320 u radius); a 2-mote loop is a 60 u radius. What decides which one players see is how the
   motes arrive. **Decision needed:** how tightly motes cluster, and whether a cap on n is wanted.
3. **Steep growth (×3) is the most rewarding and the most extreme**: +300% hull petals, 366 Mass prisms a minute, and
   the highest skill share (80%: an Easy navigator's small mixed loops are worth little). Gentle growth (×1.5) is calmer
   and makes skill matter least (49%).
4. **The Hard AI keeps 86% of its loops pure; the Easy AI does not try.** Mixed loops fizzle, which is where the 67%
   skill gap comes from. Forgiving mixes (+260%) reads above the candidate but inside the noise (SE 8.5).
5. **Tail length binds only when it is short.** At 1.6 and 2.6 radii the AI's loops are the same size (2.1–2.3 motes);
   at 1.0 the tail is too short to circle a cluster (4 loops a minute, a dry spell of 18.5 s, and an idle navigator
   earns 45% of a playing one). A person may draw bigger loops than this AI with a long tail.
6. **Motes in the world halve the loops** (12 a minute, 1.7 motes each): the pilot's pitch and roll carry most motes
   out of reach, so the head pops twice as many prisms instead. Fixed in space, only 1–4 of 30 motes were ever
   touchable before the board option existed (L-STU-85).
7. **The thumb is on the glass 93% of the time** (the kite follows a held thumb), and a navigator who never touches
   still produces 20%, mostly from prisms drifting onto a parked head.
8. **The hull never hits a hostile prism** in these runs (the AI pilot dodges), so the head's pops are not defence.
   They destroy rival mass, which is cell control.

**Fixed in the lab before any number was taken:**
- the tail never grew from empty, so no loop could close;
- world-fixed motes were almost never touchable (Found 6); the board home is now the default.

### Checked

- `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/CrewVesselLab.html`: PASS (no console errors, desktop fits,
  phone does not scroll sideways, `__lab` hooks, `SPEC` ⊂ `SHIPPED` within range, manual clock, `runBatch`
  deterministic within a page and across loads, stage not blank).
- Mid-game screenshots (after 21 s and 30 s of manual-clock play, desktop 1600 × 900 and iPhone 13) were read. On a
  phone the board now sits below the pilot's picture-in-picture so nothing entering at the top is hidden (it was
  covered in the first shot).
- Per-seed values for every preset were printed before the table was trusted.

### What round 3 does not model

- Prisms are flat boxes on the board and points in the 3D view, with no shaders, shields or lighting. Trails and Mass
  clusters recycle their oldest prisms (the game conserves mass).
- A hostile prism slows a hull for a second (a stand-in for the danger-prism effects). No hull ability fires.
- A steal is a scored combat hit in the game (`CombatHitDrain.TryAdmit`, ten points per petal); the lab counts petals.
- Two players in one hull, networking, and which machine runs the board.
- A real thumb tires and misses; the AI navigator's aim only has a speed, a reaction time and aim noise.

### Decisions needed (designer) for sign-off

1. **Sign off Kite & Tail (or a variant) for Unity.** Record it on the Decisions tab as *Sign-off*.
2. **Growth rate**: ×2 (the candidate), ×1.5 or ×3, and whether n needs a cap (Found 2, 3).
3. **How motes arrive**: cluster size and spread (Found 2).
4. **Mixed loops**: fizzle (the candidate) or majority.
5. **Where motes live**: on the board (the candidate) or in the world.
6. **Thumb offset**: 80 px.

### Signed off, and graduated to Unity (2026-10-17)

Garrett's sign-off ("this is fun, build it in Unity") took the candidate as it stood. That settles the five open
decisions above at the candidate's values: growth ×2 with the caps as shipped, clusters of 4 at an 18 u spread,
mixed loops fizzle, motes live on the board, and an 80 px thumb offset (21 mm of glass in the game).

The port follows `/labmaker` §7:

- **The core was ported line for line.** The page's kite core was first refactored into pure functions (`kiteMove`,
  `tailAdvance`, `resolveLoop`, `blastRadius`, exposed as `__lab.core`). The refactor is behaviour-neutral: the
  scorecard is unchanged at 28.67 / 18.33 / 86 / 2.25. The functions were then ported to `KiteNavigatorCore.cs`, and
  the AI to `KiteNavigatorAI.cs`.
- **The parity gate:** `node Tools/Build/kite_navigator_lab/parity.cjs`.
  - It compiles the shipped C# from its real path and diffs it against `__lab.parityTrace` frame by frame.
  - Result: PASS, 3120 frames over 6 scripted thumbs, 11 loops closed, worst error 0.000× tolerance.
  - The asset matches the page on 21 fields.
  - Every planted control was caught: kiteAccel 12→11, tailLen 1.6→1.45, kiteSpeed 320→300, and the asset's growth +0.1.
- **The effects go through the game's own systems.**
  - Space: `ExplosionHelper.CreateExplosion` with `AOEExplosion.prefab`.
  - Mass: the hull's `PrismSpawnChannel`.
  - Time: `IsBoosting`.
  - Charge: rams through `CombatHitDrain.TryAdmit`, then `ElementalTransfer.ApplyAuthoritative` (Steal), then
    `OnCombatHitLanded`.
- **Seats use the arena's swap gesture**, and the AI squad mate takes whichever seat is empty.

**Where the game differs from the lab:**
- Time is burned at once, because the Sparrow's boost is unlimited.
- The real blast can hit rival hulls.
- Everything runs on the local machine for now.

Each is explained in `KITE_NAVIGATOR.md`, along with the follow-ups: networking, the arena swap, duo-vessel
registration, and sounds.

**Round 4 went to Unity first (2026-10-18).** After flying the port, Garrett directed round 4 straight into the game:
- the views drawn by the game's renderer, which swap when you change seats;
- lime motes in each element's petal shape;
- a kite that follows the mouse or stick with no click;
- a one-thumb pilot with no buttons, who boosts by lifting the thumb;
- mote frequency weighted by the hull's petals;
- Charge rams on fauna;
- the L5 upgrades;
- the Omni crystal's wide board.

The game's `Assets/_Scripts/Controller/Vessel/Crew/KITE_NAVIGATOR.md` ("Round 4") is the record until this lab catches
up (task `T-20261018-3247`). The parity gate still holds the kite core, which round 4 did not change. One round 4 number
is already here: the lasso is 50% longer (`tailLen` 2.4, was 1.6). On the scorecard the candidate now reads 20 loops,
87% pure, a mean of 2.14 motes and 32 prisms blasted (was 18.33 / 86% / 2.25 / 12).

**Not yet seen in the Editor.** The play-mode menu, the board's look and the feel on a phone are unverified (entry in
`Docs/UNITY_VERIFICATION_CHECKLIST.md`).

## Round 2 (2026-10-16): the 2D navigator on the vessel's plane (superseded by round 3)

**Input this round** (the decision log was empty; the designer's direction, in substance): keep the radar
character's best traits. It needs no vessel of its own, it suits a phone, and it appeals to people who want
2D play. But round 1 did not look fun and did not make sense in 3D without a way to rotate the slice. So:
the character always lives in the **x-z plane of the flying vessel**, the rest of the map projects onto that
plane, and it is played with **one thumb**. Explore it with many presets.

### What changed

- **The flight is 3D now.** The vessel yaws, pitches and rolls (Sparrow `YawScaler` 80, `PitchScaler` 80,
  `RollScaler` 30 plus speed × 0.1), crystals and the rival's danger trail fill a sphere, and the AI pilot banks
  into turns (`pilotBank`). The pilot's view is a plain chase view (a picture-in-picture on a phone).
- **The navigator's board is the vessel's own x-z plane.** Every object casts a shadow `(x, z)` on it, and its
  height `y` above or below the plane decides whether the shadow can be touched (`reachY`, ±40 u by default).
  The pilot's banks and pitches rotate and sweep the board, so **the pilot aims the navigator's slice**.
- **The navigator is a kite** that moves on the board. Forward is up the screen and the board's centre sits ahead
  of the vessel (`anchor` 0.3), so the world flows in from the top like a vertical scrolling shooter.
- **Twelve presets**, each bundling a one-thumb control, a job, a frame and a depth rule:

| Preset | One-thumb control | Job | Frame | Off-plane shadows |
|---|---|---|---|---|
| Glide | Follow the thumb (the kite rides 0.12 radii above it) | catch + pop | Eased | Ghosts |
| Thumbstick | Floating stick | catch + pop | Eased | Ghosts |
| Hopper | Tap to dash (0.5 s cooldown) | catch + combo | Eased | Ghosts |
| Pinball | Pull and sling (bounces off the rim) | catch + pop | Eased | Ghosts |
| Lasso | Draw a loop (ink refills) | catch + pop | Eased | Ghosts |
| Pathfinder | Draw a path, the kite runs it | catch | Eased | Ghosts |
| Chain Glide | Follow the thumb | catch + combo | Eased | Ghosts |
| Guardian | Follow the thumb | pop only | Eased | Ghosts |
| Slice Surfer | Follow the thumb | catch + pop | Locked | Slab, ±18 u |
| Everything Flat | Follow the thumb | catch + pop | Eased | Flatten, ±400 u |
| Glide, locked | Follow the thumb | catch + pop | Locked | Ghosts |
| Glide, level | Follow the thumb | catch + pop | Level (yaw only) | Ghosts |

Frames: **Locked** is the vessel's plane every frame; **Eased** follows it at `easeRate` 3/s; **Level** keeps the
board horizontal and turns it only with heading. It is a control, not the brief. Jobs: a caught crystal is reeled
into the hull as one petal; a pop removes a danger prism (an active force, so mass rules hold; it acts on mass,
not on another pilot, so the LOCKED "PvP is petals only" rule is not touched); a combo of three same-element catches
in a row is a temporary buff (`ApplyElementalEffect` with a duration, never a held level).

- **The navigator AI is a thumb.** It moves at a finite speed (Easy 1.0, Medium 2.0, Hard 3.4 board radii per
  second), aims with noise, reacts late, and presses and releases exactly as a person does. Every control reads the
  same `{x, z, down}` whether a finger or the AI wrote it. The Easy navigator also chases untouchable shadows
  (`reachAware` 0.4).
- **Touching the board takes the navigator seat.** Seats, the AI squad mate and the D-pad swap carry over from
  round 1. Round 1's page is kept as `CrewVesselLab.round1.html`, and its scorecard is in the Archive tab.

### Scorecard

`runBatch()` defaults: 4 seeds × 90 s at 30 Hz, AI Hard pilot. Each preset runs with a Hard, an Easy and an
idle navigator (one who never touches the screen). Re-run with **Score every preset** (about 11 s).

| Preset | Petals/min | Lift % | Kite share % | Skill % | Idle % | Moments/min | Dry spell (s) | Thumb down % | Thumb travel | Board turn °/s | Touchable % | ± SE |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Solo pilot | 16.17 | 0 | 0 | 0 | 0 | 0 | | | | | | 0.17 |
| Glide | 37.34 | +130.9 | 99.1 | 97.9 | +20.6 | 51.7 | 2.5 | 100 | 50.5 | 47.4 | 15 | 0.90 |
| Thumbstick | 38.50 | +138.1 | 98.7 | 95.9 | +20.6 | 46.5 | 2.7 | 100 | 30.6 | 48.9 | 14 | 1.40 |
| **Hopper** | **52.83** | **+226.8** | 99.7 | **149.5** | +16.5 | 51.7 | 4.0 | **3** | 39.6 | 56.5 | 11 | 1.95 |
| Pinball | 20.16 | +24.7 | 33.0 | 9.3 | +20.6 | 10.3 | 21.7 | 11 | 18.3 | 36.3 | 24 | 0.96 |
| Lasso | 19.67 | +21.6 | 41.5 | 17.5 | 0 | 10.8 | 13.2 | 24 | 25.6 | 38.6 | 22 | 1.14 |
| Pathfinder | 23.00 | +42.3 | 50.7 | 18.6 | +16.5 | 11.5 | 18.5 | 45 | 62.6 | 43.5 | 22 | 0.19 |
| Chain Glide | 47.17 | +191.7 | 99.3 | 122.7 | +16.5 | 42.4 | 5.0 | 100 | 37.1 | 49.9 | 15 | 1.53 |
| Guardian | 16.50 | +2.1 | 0 | −4.1 | +3.1 | 34.0 | 11.6 | 100 | 36.0 | 36.1 | 23 | 0.32 |
| Slice Surfer | 35.67 | +120.6 | 100 | 90.7 | +19.6 | 49.0 | 3.3 | 100 | 54.5 | 67.5 | 13 | 2.78 |
| Everything Flat | 74.50 | +360.8 | 97.0 | 204.1 | +57.7 | 48.7 | 2.1 | 100 | 51.9 | 46.0 | 99 | 2.60 |
| Glide, locked | 40.33 | +149.5 | 99.6 | 115.5 | +19.6 | 51.3 | 3.0 | 100 | 56.5 | 70.4 | 18 | 2.05 |
| Glide, level | 31.17 | +92.8 | 94.8 | 70.1 | +30.9 | 52.5 | 3.1 | 100 | 51.9 | 52.6 | 11 | 1.97 |

Columns: **Lift** against the same pilot alone; **Kite share** of the hull's petals; **Skill** Hard minus Easy
navigator as % of solo; **Idle** what a navigator who never touches earns; **Moments/min** the navigator's beats
(a catch, pop or combo, with events under 0.4 s apart counted once); **Dry spell** the longest wait between two
moments; **Thumb down / travel** the one-thumb load; **Board turn** how fast the board turns under the thumb (mean,
°/s); **Touchable** the share of crystals on the board near enough the plane to touch. The table is the
navigator-AI's play, not a person's: fun is the designer's call on a phone.

### Found

1. **The kite takes the pilot's catches.** Kite share is 97–100% on every steering preset: a crystal the pilot flies
   at is in the vessel's plane by construction, so the kite reaches it first. The pilot's job becomes *aiming the
   plane*, not catching. That may be the design (the pilot's 3D flying matters only through the slice it sweeps),
   or the kite needs a reason to leave the pilot's line alone. **Decision needed.**
2. **Tap to dash is the strongest and the lightest control.** Hopper catches the most of any real preset (+227%,
   navigator skill 150%) with the thumb on the glass 3% of the time, against 100% for Follow and Floating stick. The
   thumb hides nothing between taps, and every tap is a decision. On a phone that is the shape to beat.
3. **The gesture controls are slow in these hands.** Pinball, Lasso and Pathfinder's navigators catch 7–12 crystals a minute on average, with dry
   spells of 13–22 s. Part of that is the AI: a planned gesture (aim a sling, loop a cluster, draw a route) is
   overtaken by a board that scrolls and turns while it is drawn, and the AI does not plan for the turn. A person may
   do better; these three need a hand on a phone before they are judged.
4. **The board turns about 47 °/s under the thumb on average**, mostly from the vessel's yaw. Eased (47) is calmer
   than Locked (70), and calmer than Level (53) too, because Level follows every heading change at once while Eased
   smooths all three axes. Easing helps; a heading-up board that turns with every yaw is still the biggest comfort
   question. **Decision needed:** whether the board's in-plane rotation may lag the heading (it stays the vessel's
   plane; only how it is turned on screen changes).
5. **Only 11–24% of the board is touchable at ±40 u.** Most shadows are ghosts, which gives the pilot's pitch and
   roll their meaning (sweep the plane through them). Everything Flat makes 99% touchable and wins every catch column
   (+361%), but its idle navigator earns +58% without touching the screen: the 3D world stops mattering to anyone.
6. **A navigator who never touches still earns 16–31%** on every kite preset: shadows drift onto a parked kite.
   Lasso is the only control with no passive income (0%), because the line is the character.
7. **Guardian has nothing to guard.** The AI pilot dodges the rival's trail on its own (0.17 hits a minute solo), so
   popping prisms saves nothing (+2%). A defence job needs a world where the pilot cannot see the danger coming.
8. **Combos lift the steady controls.** Chain Glide (Glide with combos, no pops) beats Glide by 26% in petals and
   keeps skill high (123%), because it rewards choosing one element at a time.

**Fixed in the lab before any number above was taken** (each would have frozen a human's kite too):
- the AI thumb measured "arrived" against its goal without its own aim noise, so a noisy aim never arrived and the
  Pinball and Pathfinder AIs froze (L-STU-82);
- a dash target or path point past the board's rim could never be reached, so the kite froze at the rim for the rest
  of the run (Hopper seed 2 caught one crystal in the 57 s after it froze at 33 s; Pathfinder seed 3 one in 45 s after 3 s) (L-STU-83);
- a sweep through a trail counted every prism as a separate moment (Glide read 160–230 moments a minute), so events
  under 0.4 s apart are now one moment;
- the Lasso AI looped clusters of prisms rather than crystals; it now anchors its loop on crystals.

### Checked

- `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/CrewVesselLab.html`: PASS (no console errors, desktop
  1600×900 fits, phone 390 px does not scroll sideways, `__lab` hooks, `SPEC` ⊂ `SHIPPED` within range, manual clock,
  `runBatch` deterministic within a page and across two loads, stage not blank). Both screenshots read: on a phone the
  board fills the stage with the pilot's chase view as a picture-in-picture; on a PC they sit side by side.
- Per-seed values for every preset (8 seeds × 90 s) were printed before the table was trusted; after the fixes the
  steering presets sit within about ±15% of their mean.
- The two frozen-kite seeds were traced frame by frame to the rim clamp before the fix and re-run after it.

### What round 2 does not model

- The 3D world is points with no prism shapes, lighting or camera feel. The board is what is under test.
- Element effects are stand-ins; no hull ability fires; one hull, one rival, no mode, no scoring rule.
- The rival's trail recycles its oldest prism (the game conserves mass).
- Two players in one hull, networking, and which machine runs the board.
- A real thumb hides what is under it, tires and misses. The AI thumb only has a speed, a reaction time and aim noise.

### Decisions needed (designer)

1. **Which presets are fun on your phone?** The table cannot say. Play Hopper, Chain Glide, Glide and Lasso first.
2. **Should the kite take the pilot's catches?** (Found 1.)
3. **May the board's on-screen rotation lag the heading?** (Found 4.)
4. **How far off the plane is touchable?** ±40 u leaves 11–24% touchable; ±400 makes the 3D world irrelevant.
5. **Is popping danger prisms part of the job?** It is legal, it keeps the thumb busy, and it saves nothing while the
   pilot can see the danger.

## Round 1 (2026-10-14): four navigator consoles (superseded)

> **Superseded by the designer (2026-10-16):** "I like that the radar character needs no vessel of its own, can be
> optimized for mobile, and appeals to people who want more 2d gameplay. it does not look fun, and it doesn't make
> sense in 3d without a way to rotate your slice." Round 2 keeps the vessel-less 2D character and rebuilds it. The
> round-1 page is kept at `Docs/Studios/CrewVesselLab.round1.html`; its scorecard is in the live page's Archive tab.

### How to use it

- **Your seat** is in the header: **Fly**, **Navigate**, **Watch** (two AIs) or **Couch** (two people on one
  screen: keyboard or pad flies, mouse or touch navigates).
- **Swap seats** with the D-pad left/right, the 1 / 2 keys or the header button. It is the same gesture as the
  arena hull swap (`PilotSwapGesture`, `PilotSwap.CooldownSeconds` 0.75 s). The AI squad mate moves to the seat
  you leave and keeps its skill.
- **Crew tab:** pick the console under test and your squad mate's skill (Easy / Medium / Hard).
- **Pilot:** A / D or ← / → steer, S or ↓ brake, Space or Shift boost; pad left stick, LT brake, RT boost. On a
  phone, touch the pilot view left or right of centre, and use two fingers to boost.
- **Navigator:** mouse or touch on the console; on a pad, the right stick moves the cursor and A clicks.
- **Decisions tab:** record which console, which AI behaviour and which swap rule you want. Claude reads the
  shared log at the start of the next round.

The pilot view is a heading-up stand-in for the chase camera. Anything outside the sight cone (120°, 200 u)
is fogged out and its crystals are hidden, which is the limit a 3D chase view puts on the pilot. The navigator
console sits beside it (or below it on a phone).

### The four consoles

| Console | What the navigator does | Reads the world? | Economy |
|---|---|---|---|
| **Gem Feed** | Every skim drops a random gem and every crystal a gem of its element into a 6×6 board. Swap neighbours to line up 3+; a match buffs that element for 8 s. | No. Only the board. | Temporary buffs (`ApplyElementalEffect` with a duration) |
| **Scope Call** | A 360° heading-up radar (420 u). Tap a crystal to call it; it becomes the pilot's waypoint. | A 2D map | None: steering calls only |
| **Lock & Reel** | A short heading-up scope (100 u, grows with Space). Hold the reticle on a drifting blip for 1.5 s and the tractor reels it into the hull. | A moving 2D scope | Crystals caught as usual (one petal each) |
| **Power Split** | Six power pips to route between Charge, Mass, Space and Time. | The hull's gauges | Held levels, capped so nothing is held above 10 |

The empty seat (solo pilot) is the baseline every console is measured against.

### The model, and where its numbers come from

| Lab key | Value | Source |
|---|---|---|
| `minSpeed`, `throttleScaler` | 10, 25 u/s | `Sparrow.prefab` `DefaultMinimumSpeed`, `DefaultThrottleScaler` |
| `yawRate` | 80 °/s | `Sparrow.prefab` `YawScaler` |
| `recovery` | 0.02 /s | `ResourceSystem.elementalRecoveryRate` |
| `swapCooldown` | 0.75 s | `PilotSwap.CooldownSeconds` |
| one crystal = one petal | +1 level | `ResourceSystem.PetalNormalized`, `SkimmerAdjustElementLevelByCrystalEffectSO` |
| element bands | base [0,10], overcharge to 15 drains | `ResourceSystem` resting band |
| domain colours | Jade hull, Ruby rival | `OriginalColorSetSO.asset` via `studio-domains.js` |
| everything else | lab | marked `lab:` in the page's `SHIPPED` block |

Element effects follow the fleet's element conventions, as lab stand-ins: **Time** = rate → speed
(+50% at level 10), **Space** = reach → catch reach (+150%), **Charge** = energy → boost per skim (+100%),
**Mass** = size → shrugs off danger-prism slows (−80%).

The AI in both seats writes inputs only: the pilot AI writes `{steer, boost, throttle}`, the navigator AI
moves a cursor and clicks exactly as a mouse does. Difficulty changes what the AI believes and how fast it
reacts (bearing noise, reaction delay, cursor speed and jitter, blunder rate), never the stick
(`/vessel-ai`).


#### Scorecard

`runBatch()` defaults: 8 seeds × 120 s at 30 Hz, 8 crews per console. Petals/min is crystals caught per
minute. Percentages are against the solo pilot of the same skill. Hard + Hard unless the column says
otherwise. Re-run with the lab's own **Score every console** button (about 6 s).

| Console | Petals/min | Crew lift | Navigator skill | Pilot skill | AI crew | Rookie rescue | Swap cost | Control | Nav actions/min | Hits/min | ± SE |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Solo (empty seat) | 14.06 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 3.31 | 0.89 |
| Gem Feed | 15.94 | +13.3% | 9.8% | 56.9% | +7.0% | +7.6% | +8.2% | 0 | 24.5 | 3.81 | 1.21 |
| Scope Call | 16.00 | +13.8% | 31.1% | 44.4% | +7.4% | +32.2% | −1.0% | 0 | 18.6 | 5.56 | 1.11 |
| **Lock & Reel** | **30.75** | **+118.7%** | **40.9%** | 56.9% | **+88.2%** | **+208.5%** | −1.5% | 0 | 22.4 | 3.13 | 1.31 |
| Power Split | 16.94 | +20.4% | 4.0% | 61.3% | +14.4% | +12.7% | +2.7% | 0 | 202.9 | 4.13 | 0.68 |

Columns:
- **Crew lift:** Hard pilot + Hard navigator against the Hard pilot alone.
- **Navigator skill / Pilot skill:** Hard against Easy in that seat, as % of solo. "Does the play in this seat show?"
- **AI crew:** two Medium AIs against one Medium AI alone. This is the hull an AI squad mate gives you.
- **Rookie rescue:** an Easy pilot with a Hard navigator against the Easy pilot alone. This is the closest
  stand-in for the brief's own case: a weak flyer helped by a good puzzler.
- **Swap cost:** a seat swap every 15 s, with the human proxy paying a 1 s re-orientation each time, against the
  two seatings held (averaged).
- **Control:** the coupling cut (no gem feed, calls ignored, no lock, no pips). It must read 0, and it does on
  every console, so every lift above comes through the console and not through a side channel of the model.
- **Noise:** the standard error of Hard + Hard over the 8 seeds is about ±6–8% of the value. Differences under
  about 10% are a plateau, not a result.

Sweeps (same scorecard, one knob moved; full tables reproduced with `runBatch` after setting `P`):

| Sweep | Gem Feed lift | Scope Call lift | Lock & Reel lift | Power Split lift | Read |
|---|---|---|---|---|---|
| Shipped | +13.3% | +13.8% | +118.7% | +20.4% | |
| `gemBuff` 1 → 3 | +10.2% | (same) | (same) | (same) | Tripling the gem payout does not move Gem Feed. |
| `kTime` 1, `kSpace` 4, `kCharge` 2, `kMass` 1 | +18.1% (skill 8.6%) | (not run) | (not run) | +37.5% (skill 3.2%) | Stronger elements lift the levels consoles; their navigator skill stays small. Solo rises to 19.69. |
| `crystalCount` 10 → 4 (sparse cell) | −13.8% | +18.6% (rookie rescue +50.6%) | +75.9% | −0.7% | Seeing matters more when there is little to see. |
| `pilotFov` 120 → 60 (narrow chase view) | +2.7% | +24.9% (rookie rescue +61.9%) | +107.7% | +10.0% | Scope Call grows as the pilot sees less. |

#### Found

1. **Lock & Reel is the only console that changes what a crew is.** It doubles the hull's catch rate, both seats'
   skill shows (navigator 41%, pilot 57%), and an Easy pilot with a good navigator catches three times as much.
   It works because it is a **second catching channel**: the navigator collects what the pilot cannot reach.
   That is the Goose-shaped job in the brief. It is also the strongest and the most likely to need a ceiling.
2. **A console that pays in element LEVELS is a weak lever, and the navigator's skill barely shows in it.** Gem
   Feed and Power Split both pay in levels. A Gem Feed board pays about 1 level of buff on average, spread over the
   four elements (measured mean 1.06 over a 120 s run, seed 3), which is worth a few percent of speed or reach.
   Tripling the payout leaves its lift inside the noise. Strengthening every element effect (`kTime` 1, `kSpace` 4,
   `kCharge` 2, `kMass` 1) lifts Gem Feed to +18% and Power Split to +38%, but navigator skill still shows only 9% and
   3%: the board's output, not the navigator's play, sets the number. There is a second ceiling over a longer match:
   the pilot's own crystals fill the [0,10] band (levels 7–10 by 120 s in that run), after which a buff can only reach
   the overcharge band, which drains. The element effects here are lab stand-ins, so a real hull's abilities could
   scale harder; the skill finding does not depend on that.
3. **Power Split is busy for nothing.** The Hard navigator clicks about 200 times a minute (the situation's best
   split flips every second) and the gain over an Easy navigator is 4%, inside the noise. Most of its +20% is the
   pips being lit at all. A seat that rewards attention this little is a spectator seat with a chore.
4. **A bad caller costs a good pilot.** Scope Call's navigator skill (31%) is larger than its lift (14%), so a
   Hard pilot following an Easy caller catches less than flying alone (about −17%). The lab's AI pilot obeys every
   call. Whether a call is an order or a suggestion is a design question.
5. **Scope Call is worth more when the pilot sees less.** It is +14% in the shipped cell, +25% with a 60° view,
   and its rookie rescue reaches +62%. In a 3D game, where the chase view really is narrow and behind is invisible,
   it may be worth more than this 2D stand-in shows.
6. **Swapping seats costs nothing measurable** at a swap every 15 s with a 1 s re-orientation (−1.5% to +8%,
   inside the noise on every console). The swap is free to keep as designed.
7. **Two lab bugs were found and fixed before any number above was taken.** The lab hull had no throttle, so the
   AI orbited any crystal inside its 25 u turn circle forever; and its "nothing in sight" search turned in place.
   Per-seed scores ran from 2 to 20 per minute. With the Sparrow's throttle and a roaming search point, a skilled
   pilot's runs sit within about ±15% of each other (L-STU-80).

**Open thread (not a result):** Gem Feed reads −14% in the sparse cell with a small standard error. The control
is 0, so the console causes it, but the mechanism is not shown. One candidate: Time buffs raise speed and with it
the turn radius, which costs catches when crystals are scattered. Check this before reading it as a finding.

#### Checked

- `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/CrewVesselLab.html`: PASS (no console errors,
  desktop 1600×900 fits, phone 390 px does not scroll sideways, `__lab` hooks answer, every `SPEC` key in
  `SHIPPED` and inside its range, the manual clock advances, `runBatch` deterministic within a page and across
  two loads, stage not blank). Both screenshots were read; the first pass found the HUD line overlapping the
  console on a phone and the phone page laid out at 980 px (no viewport meta in the raw file). Both are fixed.
- The control column reads 0 on every console in every sweep.
- Per-seed values were printed for every console × pilot skill (10 seeds × 240 s) before the scorecard was trusted.
- Power Split's AI was traced pip by pip over 25 s to confirm the 200 clicks/min is the situation flipping, not a
  stuck click (an earlier verb, "add, stealing from the biggest column", did loop; it now has add and free).

#### What this lab does not model

- The pilot view is a 2D top-down stand-in for the 3D chase camera.
- Element effects are stand-ins that follow the element conventions. No real hull's four abilities are modelled,
  and no ability fires.
- One hull, one rival AI, no game mode and no scoring rule.
- The rival's trail recycles its oldest prism. The game conserves mass; this lab does not.
- Human players are proxied in the scorecard by AI skill levels, plus a guessed re-orientation time (`swapOrient`).
- Networking. See "What the game would need" below.

#### Decisions needed (designer)

1. **Which console** does the first multiplayer vessel carry? Lock & Reel is the only one that clearly changes the
   crew; Scope Call is the purest "read a map, call the shot" Goose; Gem Feed is the only one with no spatial
   reading at all. A hybrid is possible (radar calls plus a tractor).
2. **Is a navigator's call an order or a suggestion?** (Finding 4.)
3. **Should a levels console stay in the running?** It is weak and skill-blind (Finding 2). Making it pay more
   means larger or longer buffs, and over a long match it meets the [0,10] band. That band is part of the LOCKED
   elemental economy (buffs are temporary; no sustained mechanism holds above 10), so changing it for a crew vessel
   is a question for you, not a tuning change.
4. **Where does the navigator play?** On the same screen (Couch), or on their own device such as a phone or tablet.
   The lab only models the same screen.
5. **Which hull** carries the first crew? The lab flies the Sparrow's speed and turn rate as a stand-in for the
   fleet's fighter.

## What the game would need (recorded for when a pick graduates; nothing built yet)

- **Two players on one hull does not exist.** Today one `Player` flies one vessel, and the arena's
  `PilotSwap` keeps one pilot per hull by only ever exchanging hulls. A crew vessel needs a seat layer: a hull
  with a pilot seat and a navigator seat, each held by a `Player`.
- **The D-pad is already taken in arenas.** `PilotSwapGesture` reads D-pad left/right (and 1 / 2) for the hull
  swap. A crew vessel's seat swap should extend that ring (every seat in every team hull) rather than add a second
  binding for the same gesture.
- **The console's consequences must run on every peer.** Element levels are simulated per peer and the hull's
  owner simulates its own (`PilotSwap`'s stated limit). A navigator on another machine must send inputs that the
  owner applies and that replicate, never write the hull's state directly (`/vessel` rule 48).
- **Both AIs must stay input-only.** `AIPilot` already fits the pilot seat. The navigator seat needs its own
  input-only console AI, as the lab's does.
- **(Round 2) The board is a pure function of the hull's pose**, which already replicates, so any peer, a phone
  included, can draw the navigator's board without new state. What the kite *does* (a catch, a pop, a combo buff) is
  a consequence on shared state, so it must be sent as an input and applied where every peer applies it, never
  decided by the board's own machine (`/vessel` rule 48).
- **Score credit for two pilots in one hull** is undefined. `RoundStats` rides the `Player` and hits are credited
  through `VesselStatus.Player`.

## Publishing

The repo copy is the source. Publish it as its own artifact (not inside the Vessel Studio: this is a lab for a
vessel that does not exist yet), with `capabilities: {db: {}, user: {}}` for the shared decision log.
Round 1 is published at https://claude.ai/artifact/JtQptDHQhNdC2CxPmspfct; republish to that URL so the log stays one log.

