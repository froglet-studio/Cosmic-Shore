# Angler Command Lab

A `/labmaker` browser lab for the **Angler**, a proposed strategist vessel that lures, tames and commands fauna
(design: [`ANGLER.md`](ANGLER.md)). It answers one question: **what is the most satisfying way to send particular
tamed fauna at particular prisms in a 3D cell, and what does each way cost?**

- Page: [`AnglerCommandLab.html`](AnglerCommandLab.html), a single file with three.js r128 from cdnjs.
- Verify: `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/AnglerCommandLab.html`
- Test hook: `window.__lab` (alias `window.__anglerLab`): `reset(seed, {scheme, mode})`, `tick(dt, n, input)`,
  `runMission(scheme, seed, 'skilled' | 'rookie', mode, decoy)`, `runBatch({seeds})`, `score()`, `state()`, `gl()`.
- Branch: `cece/zealous-feynman-po8k8d`. Live page (its own decision log): https://claude.ai/artifact/CwuwJcgaGkJqBYC5UHfdE6

**It models a design.** The Angler does not ship, and no game code lets anything command a creature. The
creatures, the cell and the way they eat are the game's own, read from the assets.

## What it reads from the game

| Number | Lab value | Source |
|---|---|---|
| Cell radius / nucleus | 1200 / 200 | `CapsuleMembrane.prefab` radius; `Nucleus.prefab` localScale 400 (radius 200 assumes a unit-diameter mesh) |
| Diet | any domain outside the nucleus | `Cell.IsPreyForHerbivore` (nucleus cells); toggle 0 = opposing only |
| Clawfish | speed 18–32, reach 36, turn 7 | `ClawfishFaunaDataSO.asset` |
| QuadFish | speed 16–28, reach 40, turn 6 | `QuadFishFaunaDataSO.asset` |
| LightFauna mouthful | 2 s hold, 8 bites within 12 u, 25° facing | `LightFaunaDataSO` C# defaults (no asset stores them) |
| Commanded aggression | L1: speed ×1.25, reach ×1.4 | `LightFauna.SpeedByAggression` / consume-radius rows (the level used is lab-only) |
| Tadpole | speed 10–15, dash ×10 arrival-capped, reach 20, tick 1.5 s | `TadPoleFauna.prefab` (Boid forager) |
| Tadpole mouthful | 2 s hold, 6 bites within 10 u, 30° facing | `Boid` C# defaults (not in the prefab) |
| Domain colours | Jade / Ruby / Gold | `studio-domains.js` (`OriginalColorSetSO`) |

Everything else (the Angler's speeds, the lure, the leash, every command scheme) is lab-only and labelled
that way in the page.

## Round 1 (2026-10-15)

### What it is

You fly the Angler (keys, a pad with the game's dual-stick mix, or touch) with a tamed squad of 6 Clawfish,
6 QuadFish and 12 Tadpoles. A mission gives four orders on one fixed battlefield outside the nucleus:

1. Clawfish → the Ruby trail
2. QuadFish → the Gold lattice
3. Tadpoles → the Ruby knot (a flora-like ball)
4. The whole squad → the Gold trail, which passes just behind the Ruby knot

Your own Jade trail threads between them. An order is done when 60% of its structure is gone, and fails at 50 s.

Every scheme uses one trigger (RT / Space / left mouse), so both thumbs keep flying. The game flies on two
sticks and leaves only the triggers and bumpers free. LT is the lure, LB cycles the squad (All → Clawfish →
QuadFish → Tadpoles) and B recalls.

| Scheme | The gesture |
|---|---|
| **Spotlight paint** | Hold: a beam from the nose paints every prism it sweeps. Let go: the squad eats what is painted. |
| **Pheromone dart** | Tap: a dart flies along your aim and sticks in the first prism. The squad eats that prism's domain within 24 u. |
| **Gaze lock** | Keep the reticle on a structure for 0.35 s until its brackets lock, then tap: the squad takes the whole structure. |
| **Lasso loop** | Hold and fly a loop round the target. Close it, or let go: everything inside the loop (within 40 u of its plane) is the order. |
| **Pack sling** | Hold: the squad balls up ahead of the nose. Aim and let go: the ball is thrown, and each creature latches onto the first enemy prism it passes, then eats that domain within 24 u. |
| Feral (baseline) | No command. Tamed creatures attack any enemy prism within 40 u of themselves. |
| No squad (control) | The squad never attacks. Every kill column must read zero. |

The scorecard flies every scheme through the same four orders with an **input-only commander AI** (it writes
the same input object a pad does). The skilled commander notices an order in 0.15 s with 0.4° of believed aim
drift. The rookie notices in 0.6 s with 3° of drift. Only what the AI believes changes, never its stick.

### Scorecard (seeds 1–3, re-run 2026-10-15 with `runBatch({seeds:[1,2,3]})`, 4.3 s)

| Scheme | Clear s | Order s | On target % | Off target | Yours lost | Fails /4 | Presses/order | Rookie clear s | Rookie on % |
|---|---|---|---|---|---|---|---|---|---|
| Spotlight paint | 12.1 | 5.30 | 81.7 | 53.0 | 4.7 | 0 | 1.00 | 11.9 | 82.1 |
| Pheromone dart | 21.6 | 7.39 | 82.7 | 45.7 | 4.3 | 0.67 | 1.12 | 19.6 | 78.2 |
| **Gaze lock** | **9.9** | **2.67** | **92.6** | 15.3 | 5.0 | 0 | 1.00 | **10.2** | 88.6 |
| Lasso loop | 18.6 | 13.56 | 82.2 | 53.7 | **1.0** | 0 | 1.00 | 18.9 | 80.8 |
| Pack sling | 28.2 | 4.57 | 90.2 | **14.3** | 4.7 | **2** | 1.17 | 28.5 | **89.7** |
| Feral (baseline) | 6.1 | – | 60.8 | 147.3 | 5.0 | 0 | – | 7.0 | 63.6 |
| No squad (control) | 50 | 50 | – | 0 | 0 | 4 | – | 50 | – |

Lure trial (free play, the AI flies at the nearest wild pod with the lure held): **six creatures tamed in
16.5 s** (skilled) and 16.8 s (rookie).

Per order, in seconds (skilled; orders: Clawfish→Ruby trail · QuadFish→lattice · Tadpoles→knot · all→Gold trail; 50 = failed):

| Scheme | Seed 1 | Seed 2 | Seed 3 |
|---|---|---|---|
| Spotlight paint | 26.1 · 7.4 · 5.9 · 14.6 | 21.8 · 8.3 · 4.6 · 8.3 | 26.2 · 7.7 · 6.1 · 8.2 |
| Pheromone dart | **50** · 11.2 · 4.6 · **50** | 43.6 · 11.0 · 4.6 · 21.2 | 38.3 · 7.3 · 3.5 · 13.5 |
| Gaze lock | 17.8 · 12.2 · 1.9 · 8.5 | 17.9 · 9.2 · 4.3 · 8.1 | 16.3 · 10.3 · 4.7 · 7.6 |
| Lasso loop | 26.2 · 18.9 · 10.4 · 17.1 | 26.4 · 19.0 · 10.4 · 16.7 | 28.2 · 21.6 · 12.3 · 16.0 |
| Pack sling | **50** · 5.7 · 12.4 · **50** | **50** · 5.5 · 4.0 · **50** | **50** · 7.7 · 3.2 · **50** |
| Feral | 11.3 · 6.5 · 4.8 · 3.5 | 11.0 · 5.7 · 4.1 · 3.2 | 10.9 · 5.8 · 4.4 · 2.3 |

### What it says

- **Gaze lock wins every column a structure-sized order can measure**: the fastest commanded scheme (9.9 s),
  the quickest to issue (2.7 s) and the most precise (93%). The catch is that it picks a whole structure. It
  cannot pick *particular prisms* inside one, and these four orders never ask it to.
- **Spotlight paint is the only scheme that picks arbitrary prisms.** It costs about 11 points of precision
  against gaze (82% vs 93%; over seeds 1–8 paint ranges 77–87 and gaze 91–95), because the cone also catches
  whatever sits behind the target. It is the scheme for "these prisms, not that structure".
- **The pheromone dart is precise per press and slow on lines.** One dart covers 24 u, so a 125-prism trail
  needs a dozen darts: 38–44 s for order 1, and a fail on seed 1. Its rookie loses the most precision
  (78% vs 83%), because a dart that drifts off the target sticks in whatever it reaches.
- **The lasso is the slowest to issue (13.6 s: you must fly the loop) and loses the fewest of your own prisms
  (1.0).** Its precision (82%) matches paint's, since a loop round a trail encloses everything near it. It is
  the most "Cosmic Shore" gesture (the command IS a flight path), and that is a feel question the numbers
  cannot answer.
- **The pack sling is the fastest scheme on blobs, the second most precise (90%), and useless on lines.** On
  the Ruby knot it clears in 3.2–4.0 s on two of three seeds, faster than anything but feral. On both trails it
  fails every time, because one throw latches one 24 u cluster and a trail is a line.
- **Commanding buys about 20–30 points of precision for 4–6 s per order.** Feral is fastest (6.1 s) because it
  sets the whole 24-creature pack loose every time, and it eats 147 prisms nobody ordered (61% on target).
- **Every prism of yours that is lost is lost to the diet, not to the command.** With `spareOwn` no order
  targets your prisms. A mouthful is still domain-blind in a nucleus cell (the game's rule), so a bite next to
  your trail eats it (1–5 per mission). With the diet set to "opposing only", losses are 0 on paint, gaze and
  feral (seeds 1–3).
- **The lure must also be the brake.** With the lure held at cruise (40 u/s), six creatures were never tamed
  in 90 s (seeds 1–3), because a lured creature (16–38 u/s) cannot keep up with the esca. With the lure slowing
  the hull, six were tamed in 14–19 s: at 20 u/s 16.9 / 14.0 / 16.3 s, at 12 u/s 15.3 / 18.8 / 15.4 s, at
  6 u/s 14.3 / 19.5 / 17.8 s (seeds 1–3).
- **A squad slower than the hull is a squad you never see.** Without a tow, followers at their game speeds
  (at most 40 u/s at L1) sat on average 79 u from a 40 u/s hull (max 102), behind the chase camera. With the
  proposed leash tow (`leashTow` 1.6, FOLLOWING creatures only; attacks run at the game's speeds) and the
  school's ring beside the hull (`formBack` 6), the average is 37 u. Clawfish and QuadFish hold the ring.
  Tadpoles still trail about 40 u, because the game's arrival-capped dash only speeds them up when they are far
  from their goal.

### Checked

- `verify_lab.cjs`: **PASS** (no console or page errors, no WebGL fault, the layout fits at 1600×900, no
  sideways scroll on an iPhone 13, every SPEC key in SHIPPED within range, the manual clock advances,
  `runBatch()` is identical twice in one page and across a fresh load). Screenshots read: desktop and phone.
- **Control**: "No squad" kills nothing (0 on, 0 off, 0 yours).
- **Planted defect the metric must catch**: `runMission(scheme, 1, 'skilled', 'missions', true)` makes the
  commander aim every order at the wrong structure. On target reads **0%** on paint, dart, gaze, lasso and
  sling.
- **Diet control**: with `dietAny` 0, "yours lost" is 0 on paint, gaze and feral (seeds 1–3), so the friendly
  losses are the mouthful's, not the targeting's.
- **Noise band** (on-target, seeds 1–8, skilled): paint 77.4–87.3, gaze 90.6–95.1, dart 75.0–91.1. A gap
  smaller than these ranges is a plateau, not a result.
- Full batch: 7 schemes × 3 seeds × 2 pilots + the lure trial in 4.3 s.
- Every number above came from one run of the committed page (`runBatch`, `runMission`, `setParam`) in
  headless Chromium, on the final file.

### Found (bugs the lab exposed, fixed in this round)

- **A creature that arrives at its meal sideways circles it forever.** At minimum speed its turn could never
  catch the bearing, and on top of the prism (d ≈ 0) "facing it" is undefined. A feeding creature now brakes to
  a hover and turns 4× faster (the Boid's `feedingTurnBoost`), and a meal under 3 u counts as faced.
  **Open thread (not a claim):** check whether the game's `LightFauna` can do the same. It eats from 36–40 u,
  so it rarely gets that close, but a creature put down inside its own reach is the case to test.
- **Followers trailed the hull by their own gap**: their speed was proportional to the gap, so they settled
  ~27 u behind their slot. The hull's speed is now fed forward.
- **Recall only happened before the first order**, so a squad kept eating the last order's structure and
  inflated "off target". The commander now recalls before every order.
- **The sling AI let go on the first frame** because it counted the gathering ball as "busy". It now counts
  only attacking or thrown creatures.
- **three.js r128 instanced meshes** sized their colour buffer from the instance count at the time of the
  first `setColorAt` (it was 0, so every creature drew grey). They were also culled by the geometry's bounds
  at the origin, so creatures 450 u out vanished. Colours are now written before the count is set, and culling
  is off.

### Decision needed (designer)

1. **Which command scheme, or which pair?** Round 2 should test the obvious hybrid: **gaze to lock a structure,
   paint to trim it** (lock the knot, sweep the part you want). It keeps gaze's speed and precision and adds
   paint's "particular prisms".
2. **Taming and the one-colour rule.** `ECOSYSTEM_DESIGN_PRINCIPLES.md` locks "a cell never holds two fauna
   colours". The lab keeps tamed creatures in the cell's colour on a leash. Alternatives: they turn your colour
   (breaks the rule, so it needs your say-so), or taming only works in a cell your domain controls.
3. **The lure as a brake** (`lureCruise` 12 u/s) and **the leash tow** (`leashTow` 1.6). Both are proposals the
   numbers argue for.
4. **The sling's trail problem.** Keep it as the anti-blob tool, or let a throw fan out along the aim
   (a spread knob) and re-score.
5. **Friendly fire.** Accept that a mouthful next to your own trail eats it (emergent, the game's diet), or
   have tamed creatures spare the leash-holder's domain (an exemption the emergence rule says to ask about).

### What this lab does not model

Listed on the page under About: the flight model is not `VesselTransformer`; there is no command hook in the
game (`Fauna.Goal` is overwritten on every behaviour tick, so a port needs a persistent goal provider); no
separation, starvation, breeding, predators, shields, danger prisms, rival vessels or network; one stand-in
battlefield; the mouthful takes its neighbours nearest-first where the game uses its spatial index's order;
the commander AI is one competent style, not the best possible pilot.
