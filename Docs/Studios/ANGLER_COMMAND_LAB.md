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

## Round 7 (2026-10-18): the Sparrow's buttons

The designer asked for the Mass ability on A and the Time ability on B, "just like the Sparrow". The page's pad map is
now **A lure (Mass), B dash (Time), X squad, Y recall**; RT, LT and RB are unchanged. The squad pick and recall took the
two freed face buttons. The page's own keyboard keys are unchanged (F lure, T dash, Q squad, X recall); the game's
keyboard follows its pad (Space lure, T dash, Q squad, F recall).

"The QuadFish don't always listen" was a game-side bug, not a page one: an invisible squad pick (A, the Sparrow's Mass
button, used to cycle it) and orders the creature's own diet refused. Both are fixed in the game
(`R_VesselActions/ANGLER.md` §1k). The page already shows the picked squad, and its world is domain-blind.

Scorecard: unchanged (the bots read the input object, not the pad).

## Round 6 (2026-10-17): the lit queue

The designer asked: *the fauna should keep eating lit prisms in order of how recently each was lit; if one creature is
already heading for the newest, the next goes down the queue, and so on.*

**What was wrong.** A throw's creatures picked only from that throw's own paint. Once it was eaten they went back to
the hull, and a second throw replaced the first. Any prism still lit from an earlier throw stayed lit and was never
eaten unless a creature happened to bite it.

**The page now.** A Sling-painter creature picks from the **lit queue** (`litQueue`): every prism a throw lit that is
still alive and still lit, whichever throw lit it. `pickRecent` (unchanged, in CORE) spreads the school over it: the
fewest claims first, then the newest paint. So the first creature takes the newest prism, the next takes the next
newest, and so on. A creature that finishes its meal picks again from the queue, and only returns to the hull when
nothing lit is left. Erasing a prism takes it out of the queue. Painting a lit prism again moves it to the front (its
stamp is renewed) without a second entry.

Headless check (two throws, 12 prisms then 6 newer ones, 24 creatures):

| | Round 5 | Round 6 |
|---|---|---|
| After the second throw | 14 on the new 6, none on the older 12 | 6 on the new 6 (one each), 12 on the older 12 |
| Older throw's prisms eaten after 40 s | 0 of 12 (still lit) | **12 of 12** |
| Prisms left lit | 12 | **0** |

Scorecard (seeds 1–3):

| Sling painter | Round 5 | Round 6 |
|---|---|---|
| Clears in | 10.9 s | **9.8 s** |
| On target | 87% | 83% (rookie 73%) |

The clear is faster because nothing lit is abandoned. On-target falls because the sim pilot's stray paint, which round 5
left lit and uneaten, is now eaten too: the queue does what the paint says. Erase (LT) is how a pilot takes a stray
mark off the queue.

## Round 5 (2026-10-17): speed back, a smaller and accurate brush, a level camera

The designer flew round 4 and asked for three things. All three are on the page:

| Ask | Page |
|---|---|
| Base flight speed back to what it was | `cruise` 40, `boost` 110 (round 3's) |
| Painting and erasing cones **accurate and smaller** | `paintCone` 3 (was 8), `eraseCone` 1.5 (was 4), `brushNear` 2 u (was 6) |
| No camera y offset when flying; it may rise when luring or dashing | `chaseUp` 0 (was 56); new `chaseLift` 56 and `chaseLiftTime` 0.4 s |

**Why the camera makes the brush accurate.** With no y offset, the chase camera sits on the nose's line, 160 u behind
it, looking ahead along it. The brush's axis is therefore the screen's centre: what is under the middle of the screen
is what paints. With the camera 56 u up, the axis ran below the screen's centre and the aim read high.

The camera rises `chaseLift` (56 u) while the lure is held or the Time dash is moving, and settles back over
`chaseLiftTime`, so the light and the school are seen from above when they matter.

Scorecard (seeds 1–3, re-run on the round-5 page):

| Sling painter | Round 4 | Round 5 |
|---|---|---|
| Clears in | 12.7 s | **10.9 s** |
| On target | 72% | **87%**, the best the painter has scored (rookie 79%) |

The narrower brush paints fewer of a target's neighbours. The Spotlight (it shares `paintCone`) is at 82%. The lure trial
tames six in 16.3 s. The round-4 listen check still holds: 24/24 creatures take the new paint.

## Round 4 (2026-10-17): 2x speed, the lure stops the hull, the camera 4x out, and "they didn't listen"

What the page models changed first; the rest of the designer's round-4 asks are game-only (`R_VesselActions/ANGLER.md`).

| Ask | Page |
|---|---|
| Base flight speed **2x** | `cruise` 80 (was 40); `boost` 220, doubled with it so the boost keeps its ratio |
| The lure **stops** the hull | `lureCruise` 0 (was 12) |
| Camera **4x** out | New keys `chaseBack` 160 and `chaseUp` 56 (were the hard-coded 40 and 14) |
| **"The fauna didn't always listen when I painted a new target"** | Fixed (below) |

**The bug.**

- **Cause:** holding RT gathered only idle and attacking creatures, and the throw skipped any creature still in flight.
- **Why it showed now:** since round 3 a homing throw never gives up, and the brush reaches 3200 u. A creature thrown at a far target could therefore be in flight for many seconds and deaf to every new paint.
- **The fix:** RT now calls back every creature of the squad, one in flight included, and the throw re-throws one still in flight.

The bug reproduced headless (seed 3, missions): throw at one painted set, paint a second while the first throw is in
flight, throw again.

| Page | Creatures still on the first set | Creatures on the new paint |
|---|---|---|
| Old | 24 of 24 | 0 |
| New | 0 | 24 of 24 |

**Scorecard** (seeds 1–3, re-run on the round-4 page):

- **Sling painter:** clears in **12.7 s** (was 10.0), orders in **2.35 s**, **72%** on target (was 85%; rookie 81%).
- **Lure trial:** six tamed in **14.4 s** (was 16.1), quicker because the lure now stops the hull.

The 2x hull outruns its own squad: 32–40 u/s fauna times the 1.6 leash tow is at most 64 u/s, against an 80 u/s cruise.
So the ball is thinner when RT is released, and the AI commander overshoots. In the game the **Time** element now speeds
the tamed fauna (`R_VesselActions/ANGLER.md`); the lab has no elements, so this is the number to watch.

## Round 3 (2026-10-16): 20x reach, newest paint first, the dash, the omni creature, and the element map

The designer's third playtest asked for nine things. What the page models changed first; the rest is game-only.

| Ask | Where it lives |
|---|---|
| Paint and erase reach **20x** (160 → 3200 u), and work **close up** | Page: `paintRange` 3200; new `brushNear` 6 u; `CORE.inBrush` replaces `inCone` (the brush never narrows below `brushNear` at the nose) |
| Thrown fauna go for the **most recently painted** prisms, **regardless of distance**, limited only by speed | Page: `CORE.pickRecent` (fewest claims, then newest paint); a homing throw never gives up |
| The **Time** ability: dash **75%** of the way to the school's centroid | Page: `dashFrac` 0.75, `dashTime` 0.35 s, `CORE.dashPoint`; key T, pad Y |
| An **omni crystal morphs into a random creature** | Page: O makes a random species at the hull, joining the school (replaces round 2's mass tame and `omniTameRadius`) |
| **Space** = paint; its L5 paints **shielded** prisms | Game only (the lab has no elements or shields) |
| **All fauna** find painted prisms more desirable | Game only (the lab's wild fauna do not eat) |
| **Mass** = taming, which also **grows your domain's plants** in the lure's range; its L5 **steals** fauna | Game only (no flora, no rival Anglers in the lab) |
| **Charge** = erase; **locking on to a vessel** sends the squad at it until a new paint, a new lock or death | Game only (no rival vessels in the lab) |

Scorecard (seeds 1–3, re-run on the round-3 page): the Sling painter clears in **10.0 s** (was 10.9), orders in **3.1 s** (was
4.4), **85%** on target (was 90%; rookie 81%). The 20x beam paints more of a target's neighbours in one sweep, and the AI's
eraser trims less of them. The round-1 Spotlight shares `paintRange` and drops to 70% on target. The lure trial tames six
in 16.1 s.

Checked headless:

- `inBrush` paints a prism 3 u ahead and 4 u off-axis (the bare cone would not), and one 2500 u out. It never paints behind the nose or past reach.
- `pickRecent([1,5,5,3], claims [0,1,0,0])` is 2.
- One O press adds one creature.
- The dash lands on the 75% point.
- Parity: 189 cases, 10/10 planted changes caught.

## After the second Unity playtest (2026-10-16)

The designer asked for two things:

- *"if there are no fauna under the angler's control it should spawn one. there should always be at least one fauna that doesn't starve"*
- *"the lit prism should stay lit until eaten or the lit effect is erased with LT"*

The page changed first:

- **The keeper** (`ensureKeeper`): an empty school gets one creature of the first species, already tamed, at the
  school's first slot. Free play now starts with it.
  - `m.keepers` counts it, and it is not counted in `m.tamed`, which scores the lure.
  - The leash capacity checks now read the school's size (`schoolSize`).
  - The lab models no starvation, so the keeper's never-starving is a game-only rule. The not-modelled list says so.
- **Lit until eaten or erased** (`marked`): a thrown-at prism stays lit (75% toward white) until it is killed or erased.
  The eraser now also takes a marked prism, and strikes it from every creature's order (`strikeFromOrders`). A
  creature heading for it picks again.

Checked headless:

- Free play: 0 → 1 creature on the first tick, once.
- A throw marks its prisms.
- Erasing a marked prism unmarks it, empties it out of all 24 creatures' orders, and leaves nobody targeting it.
- `verify_lab.cjs` PASS, with the batch identical. No scored order erases after a throw.

## After the first Unity playtest (2026-10-15)

The designer flew the cloned Angler in Unity. Taming and throwing worked ("which is fun"). Painting and erasing showed
nothing, and the designer asked for one new thing. Two changes followed, the lab first:

- **The omni crystal tames every herbivore near the hull** (the designer's ask).
  - New key `omniTameRadius` (300 u, Lure tab, "Omni tame reach (u)").
  - New `CORE` function `omniTameOrder`: nearest first, ties by index, up to the leash capacity's room.
  - The lab has no crystals, so **O** stands in for the pickup.
  - Headless check in free play, seed 7: one press tamed all 8 wild creatures within 300 u, and a held key counted once.
  - Unity: `AnglerTameByCrystalEffectSO`, run from the Angler's own forked impactor container.
- **Paint is now drawn the lab's way, on the prisms themselves.**
  - The lab's `render()` has always drawn a painted prism 75% toward white, and an ordered one 50%, fading over 1.2 s.
  - The first Unity cut drew a dot at each prism's centre instead, inside the prism, where nobody could see it.
  - Unity now lifts the prism's own colour (`Prism.SetColorLift`) with those numbers.

`verify_lab.cjs` still passes, and the scorecard is unchanged: the batch never presses O.

## Round 2 (2026-10-15): the Sling painter

**The designer's decision** (in chat, 2026-10-15): keep the Angler; command with *the Pack sling combined with
the painter, with the other trigger as an eraser*; then bring it into Unity. Round 2 builds that scheme, scores
it against round 1's, and is the version the Unity port copies.

### The gesture

| Input | What it does |
|---|---|
| **RT (hold)** | Paints prisms with a beam from the nose. The squeeze sets the brush: just past halfway paints `brushMin` (0.35) of the full brush, a full pull the whole 8° cone. Meanwhile the picked squad balls up 12 u ahead. |
| **LT (hold)** | Erases paint with a narrower beam (4° at a full pull, the same squeeze rule), so a neighbour can be picked out of the paint. While LT is held RT does not paint, and the ball keeps gathering. |
| **RT (let go)** | The throw. Each creature in the ball is given its own painted prism (nearest, spread by claims, the round-1 `pickTarget`) and flies at it at throw speed (150 u/s), homing at 12/s. Inside its mouth's reach the throw ends, the creature lands at its own swim speed and eats by its own mouthful rule, and its order is the painted set. Creatures still swimming to the ball join the order without the throw. With nothing painted, it throws like the Pack sling. |
| **X (hold)** | The lure. **It moved here from LT**, because LT is now the eraser. (This write-up first proposed RB. The game's RB is already throttle and boost, and its LB raises no input event, so the page's pad map was aligned to the game's: X lure, A squad, B recall, RB boost.) |
| A squad · B recall · RB boost | the game's own buttons (`GamepadInputStrategy`) |

Every round-1 scheme is still selectable (Command ▸ Command scheme), so the comparison stays one click away.

### Scorecard (seeds 1–3, the whole batch re-run on the round-2 page, 4.4 s)

| Scheme | Clear s | Order s | On target % | Off target | Yours lost | Fails /4 | Rookie clear s | Rookie on % |
|---|---|---|---|---|---|---|---|---|
| **Sling painter** | 10.4 | 4.43 | 91.1 | 21.0 | 5.3 | **0** | 13.9 | **94.1** |
| Sling painter, AI never erases | 10.2 | 3.65 | 84.1 | 46.0 | 5.3 | 0 | 11.7 | 91.6 |
| Spotlight paint | 12.1 | 5.30 | 81.7 | 53.0 | 4.7 | 0 | 11.9 | 82.1 |
| Pack sling | 28.2 | 4.57 | 90.2 | 14.3 | 4.7 | 2 | 28.5 | 89.7 |
| Gaze lock | 9.9 | 2.67 | 92.6 | 15.3 | 5.0 | 0 | 10.2 | 88.6 |
| Feral (baseline) | 6.1 | – | 60.8 | 147.3 | 5.0 | 0 | 7.0 | 63.6 |
| No squad (control) | 50 | 50 | – | 0 | 0 | 4 | 50 | – |

Over seeds 1–8 (skilled, on target %): the Sling painter reads 82.5–98.1 (mean 94.0), and 74.3–95.8 (mean 88.3)
when the AI never erases. Gaze lock's band from round 1 was 90.6–95.1.

Per order (s; Clawfish→Ruby trail · QuadFish→lattice · Tadpoles→knot · all→Gold trail):
seed 1 21.0 · 5.4 · 3.5 · 10.4; seed 2 25.2 · 5.3 · 4.4 · 11.8; seed 3 21.3 · 8.3 · 4.3 · 4.0.

### What it says

- **The combination keeps what each half was good at.** It is as fast as Gaze lock (10.4 s against 9.9 s) and
  as precise (mean 94% against 91–95% over eight seeds), while still choosing particular prisms. It **never
  fails a trail**, where the Pack sling failed both every time, because the throw spreads across whatever the
  brush painted instead of latching onto one 24 u cluster.
- **The eraser is worth about 6 points of precision** (94.0% against 88.3% over eight seeds) for 0.8 s more per
  order to issue. It earns them where a structure sits in front of another: on seed 2 the painted cone caught
  the Gold trail behind the Ruby knot.
- **It is forgiving.** The rookie's precision (94%) is the best of any scheme, because a sloppy sweep is what
  the eraser cleans up. The rookie is about 3.5 s slower per order.
- **What a command cannot fix is the mouthful.** Off-target and friendly losses that remain come from the
  2-second mouthful eating neighbours within 12 u (the game's own rule). With the diet set to "opposing only",
  your losses on the Sling painter are 0, 0, 0 (seeds 1–3).

### Checked

- `verify_lab.cjs` **PASS** on the round-2 page; desktop and phone screenshots read (the brush, 26 prisms
  painted on the Ruby trail, the squad balling up).
- Decoy (every order aimed at the wrong structure): **0%** on target.
- Diet control (`dietAny` 0): 0 of yours lost, seeds 1–3.
- Every number above came from one run of the committed page in headless Chromium.

### Decision needed (designer)

1. **The lure on X.** The other trigger is the eraser now, so the lure needed a new home. RB was proposed first,
   but in the game RB is throttle and boost and LB raises no input event, so the lure is on X (Button3, keyboard Q)
   in both the page's pad map and the Unity Angler.
2. **Trigger depth sets the brush width** (`brushMin` 0.35). If you would rather have a fixed brush, set
   `brushMin` to 1.
3. Round 1's open questions on taming, the lure brake and the leash tow still stand.

## Round 1 (2026-10-15)

*Superseded for the command scheme by round 2 (the designer picked the Sling painter). Kept for the comparison.*

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
