# Tiger Moth Time: the Drop Tower

The Tiger Moth's TIME card is Garrett's **Waystation** (GameModes 58). This page records the work to make it fun on
Shombith's direction: vertical movement, like Flappy Bird.

Round 1 shipped as a separate mode, Waystation Prototype (GameModes 76, PR #1661), so Waystation stayed as Garrett made
it until a replacement was approved. **Round 2 (2026-11-01) made it Waystation**: the Drop Tower course is now
Waystation's own, and 76 is retired and reserved (§5).

The mode doc is `Assets/_Scripts/Controller/Arcade/WAYSTATION.md` ("The Drop Tower").

## 1. What was wrong (measured on Waystation, 2026-10-31)

- **Long gaps.** Fifteen rings in clusters of 3-6, with gaps between clusters of 531-1321 u. Almost every gap costs a
  Drop of 12-20 s recharge (`20 s × min(1, fall/900)`). A cluster takes roughly 5-7 s to fly (estimated), so the Drop
  is often not ready at the next gap.
- **No role for the dust kit** in the race.
- **Intensity changes only the course.** The arena is the shared barren Skim Race cell.
- **Stale text.** The card, toast 118 and the preview still say "Butterfly" and "FOLD". They are left alone here
  because Waystation is not changed; §5 lists them.

## 2. The constraints

- **The vessel kit stays as designed** (LOCKED, Shombith 2026-10-31). The only levers are the track, the environment
  and the AI.
- **Garrett's mechanics are kept and added to:** the clusters, the square exit gates and the belly-aim AI.
- **Flappy maps onto the Drop like this:**
  - Flappy's tap is a short hop.
  - Its scroll is the 67 u/s cruise.
  - Its pipes are walls you pass only by dropping through them, because the fall is incorporeal.
  - The skill is how long you hold.

**What a Tiger Moth takes when it lands inside a prism, confirmed in code:**
- The prisms are triggers and the hull is a sphere of radius 6. When the colliders come back on at the end of the
  landing, `OnTriggerEnter` fires once.
- **Plain neutral prism:** the prism explodes, the throttle drops by ×(1 − min(0.1·volume, 0.5)) for 1 s, and boost is
  reset.
- **Danger prism:** the throttle goes to ×0 and recovers over 3 s, and all four elements take a **permanent** burn,
  because Blue is a rival domain.
- Flying into a wall costs the same as landing in one.

So a plain wall is a speed tax and a danger wall is a full stop. Walls mix both kinds (Shombith, 2026-10-31).

**The Drop's numbers (TigerMothDropExecutor, closed form in `WaystationTower.Fall` / `Travel`):**
- A hold of t seconds falls 900·(t − 0.06) u once t ≥ 0.12 s; the fall eases in below that.
- The landing then carries about 121 u more and turns the hull 90 degrees.
- Short taps are cheap. A 0.1 s tap travels 149 u for 0.8 s of recharge. A 0.2 s hold travels 247 u for 2.8 s.

## 3. Round 1: the pieces, and the one chosen (2026-10-31)

**The rule that decides everything: every Drop is a 90-degree turn.** You land flying along the line you fell, so a run
of hops is a walk on a lattice, not a scroll. Against that rule:
- **Hop Run (walls across the belly side, a ring just past each).** Kept as the unit of every tower. After each hop you
  fly toward the next wall (the PIPE), so the hold decides whether you have the runway to recharge before it.
- **Zigzag tower (drop, roll 180, drop).** This makes a DIAGONAL staircase: two drops with a 180° roll between them
  leave you on your old heading, one step lower and one step on. It cannot start from Garrett's square exit gate,
  because its axis is 45 degrees off the drop line. **Replaced by the square wave**: drop down, drop sideways (no roll),
  roll 180, drop down, drop sideways back. This is the one pattern that goes straight down the drop line from a square
  gate. Every second sideways hop cancels the one before it, so a tower never drifts.
- **Roll gates (rings abeam).** Kept at I3 and I4: a free sideways hop's ring is abeam, so you roll 90 degrees first. A
  roll gate is also how a tower turns away from the cell's edge, a cluster or another tower when the belly's way is
  blocked.
- **Short-hop pacing with one long gap.** Kept. A tower stands in every gap but the **last**, which stays Garrett's long
  drop (1000 u or more, so it leaves the wormhole pair). It comes last so its 20 s recharge never delays a tower.
  Seeds are re-drawn (up to 64) until the last gap is that long.
- **A dark section where the Dust Mode light matters.** Dropped. The dust light is a shader tint on prisms inside the
  capsule (`PrismLit.PublishLight`), not a Unity `Light`, and it switches off during the drop. A dark section would
  mean changing the cell's lighting, which is outside this round.

**A tower step has three parts:**
- **Floor plate:** a square of 8×8 tiles under the press point. A hop too short lands on it.
- **Ring:** 6 u past the plate.
- **Pipe:** across the new heading, just past the next press point. A hop too long hits it.

**How a tower is fitted to its gap:**
- Each step's runway covers the Drop's recharge at the window's far edge or the next roll, whichever is longer, plus a
  0.35 s reaction.
- Hops are added while the next pair still fits the gap and clears the cell, the clusters and the towers already laid.
- What is left over goes into the last hop: the **plunge** into the next cluster, up to 300 u deeper. It does not go into
  a longer runway, because that would widen the window past its authored width.

**Measured numbers.** `Tools/Build/waystation_tower_course.py` (named `waystation_prototype_course.py` in round 1), 200 seeds × 4 intensities, 5 clusters. Every check
passes.

| | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| Hop window, travel | 50..250 u | 66..234 u | 85..225 u | 102..218 u |
| Hop window, hold | ±0.079 s (0.046..0.203) | ±0.065 s | ±0.055 s | ±0.047 s (0.074..0.168) |
| Floor plate under the cruise line | 36 u | 52 u | 71 u | 88 u |
| Drop every (inside a tower, centre hops) | 5.1 s | 4.5 s | 4.2 s | 3.9 s |
| Tower ring radius | 48 | 42 | 36 | 32 |
| Roll gates / danger share | 0 / 0.10 | 0 / 0.20 | 0.5 / 0.35 | 1 / 0.50 |
| Rings in a race (40 seeds) | 18..24 | 23..29 | 28..34 | 34..41 |
| Drops in a race | about 6 | about 7 | about 7 | about 8 |
| Race time (straight-line estimate, optimistic) | about 57 s | about 76 s | about 92 s | about 105 s |
| Gaps left bare (no tower fits) | 2 / 600 | 8 / 600 | 23 / 600 | 48 / 600 |

**Two things the asked numbers cannot both do.** The direction asked for hop windows of ±0.15 s at I1 down to ±0.05 s
at I4, *and* a Drop every 3-5 s. A wider window has a farther far edge, and the far edge's recharge sets the runway:
- At ±0.114 s, I1 dropped to one hop per tower and a Drop every 6.8 s.
- The shipped ±0.079 s at I1 keeps the pace at 5.1 s.
- I4's ±0.047 s is the asked ±0.05 s.

**The AI:**
- Garrett's belly-aim autopilot is inherited unchanged. The scene raises its long-drop threshold from 420 u to 700 u,
  above every tower link (worst 601 u). It then takes only the long gap and the clusters.
- A new hop autopilot takes the towers. It flies the cruise line over the ring, rolls its belly onto the ring's drop
  line, presses through the replicated path when the drop line crosses the ring, and releases at the window's centre.
- Every race is recorded and learned from automatically (`Docs/AI_SYSTEM/AUTO_LEARNING.md`).

## 4. Fun score (the six-dimension rubric of `Docs/AI_SYSTEM/TIME_GAME_FUN_SCORES.md`)

Each dimension is scored 0-10, and anything unknown scores low. Waystation's row in that scores table is now the Drop Tower course's.

| Vessel | Game | Verb | Flow | Ladder | Feedback | Rivals | Fairness | Fun | Floor |
|---|---|---|---|---|---|---|---|---|---|
| Tiger Moth | Waystation (58), clusters only, before 2026-11-01 | 9 | 6 | 7 | 4 | 3 | 4 | **5.5** | 3 Rivals |
| Tiger Moth | **Waystation (58), the Drop Tower course**, est. | 9 | 7 | 7 | 5 | 3 | 4 | **5.8** | 3 Rivals |

**Why each score:**
- **Verb 9:** the Drop is the whole race, short taps and one long plunge.
- **Flow 7:** the 12-20 s waits between clusters become hops every 3.9-5.1 s. The clusters are still flown without a
  Drop.
- **Ladder 7:** each rung narrows the window, raises the danger share and adds roll gates at I3. I4 leaves more gaps
  bare.
- **Feedback 5:** a plate bursts under a bad hold, and a thread is a ring. There is no new reward line.
- **Rivals 3:** the hop autopilot has never been flown, and unknowns score low.
- **Fairness 4:** never played. A danger landing is a full stop plus a permanent element burn, which may be harsh, and
  that is the vessel's rule, not the mode's.

## 5. Round 2: the prototype became Waystation (2026-11-01)

Shombith, after merging PR #1661: "make Waystation Prototype as Waystation". Garrett owns Waystation; this was done on
that instruction, and every commit says so.

**How it was folded in:**
- **One mode, 58.** The card, scene, scoring rule, toasts and preview are Waystation's own, now authored with the
  prototype's words (`author_waystation_assets.py`). GameModes 76 is retired and reserved, like 7, 28, 29, 31 and 47.
- **One controller.** `WaystationController` gained the tower walls, their regrowth and the hop autopilot.
  `WaystationPrototypeController` is gone.
- **One course source.** `WaystationCourseSource` lays the clusters and the towers.
- **The long-drop autopilot.** Its `aiFoldMinDistance` default rose from 420 to 700, so it stands down on every tower
  link. The model reads the number off the controller.
- **The target.** It is now `waystationClusterTarget` = 5 clusters. The finish line is every ring laid (18-41).
  `waystationRingTarget` = 15 is retired.
- **The comeback rate** went from 0.64 to 0.4, sized on the shortest race.
- **The model and its harness** became `waystation_tower_course.py` and `waystation_tower_harness/`.
- **The prototype's assets and registrations** are deleted. The generator removes them on every run, so nothing can
  bring them back.

**What round 1 left stale in Waystation, and what happened to it:**
- **Fixed:** the card description, toast 118 and the preview's ObjectiveText and Notes, which said Butterfly and FOLD,
  now use the Drop Tower's words.
- **Fixed:** `WAYSTATION.md`'s header table (target, comeback, the hull). Its Butterfly- and Fold-era sections are kept
  as the record of the clusters, under a note saying so.
- **Still open:** `waystation_course.py --target` defaults to 24, and so do its harness and `WaystationCourseTests`.
  They exercise Garrett's cluster generator alone, which a ring target still sizes.

## 6. Round 3: Lantern Towers (2026-11-11)

Yash: make the Tiger Moth's Time game super fun, with everything learned from the other Time games (rounds 3 and 4 of
`Docs/AI_SYSTEM/TIME_GAME_FUN_SCORES.md`). Waystation was upgraded in place; no new mode, no new id.

**How it was decided.** Five readers (the kit today, the round 3-4 upgrades, the fun bar, the `/arcadegame` recipe, what
a course can be built from), then three designs from three angles (verb-first, spectacle-first, rivals-first), judged by
a player, an engineer and a rules/AI judge, then one spec. The spec (in the session notes, summarised here) took the
verb-first design's **shelf** (the one feature that turns the hold into a decision), grafted on the spectacle design's
**lanterns, in-world grading and crown gem**, and the rivals design's **AI fixes and proof hygiene**.

**What it is.** WAYSTATION.md "Lantern Towers" has the whole of it. In one line: every hop stands between two lantern
posts that end at the deepest landing that still recharges and rolls by the next press point; from I2 some hops are
tight and their lanterns short; a deep landing lights its lanterns in your colour and a run of them the whole tower;
at I4 a Time crystal on each plunge trades depth for +1 Time; the AI reads the same lanterns with a per-seat error.

### What the measurement changed

The spec put the lanterns at the depth the course was laid for, which keeps the 0.35 s reaction. Measured, that was
worth almost nothing: a reading pilot saved 1.2 / 1.0 / -0.7 % of tower time at I2-I4 against the best fixed depth,
because a fixed pilot simply spends the reaction margin (an "expert" landing at the hard edge saved 4-5 %, so the
course carried the signal and the lanterns hid it). Moving the lanterns' foot to the shelf proper (recharged exactly at
the next press) and widening I4's cut (0.75 share, 40-90 u) gave the numbers below. The judges had flagged exactly this
risk ("if the strict saving falls, tune the share and cuts before adding any feature").

`waystation_tower_course.py --sweep --seeds 200` (release on the nearest 60 fps frame, +-8 u carry; a miss costs one
8 s turning circle). "Strict" is against the BEST fixed plan per rung (the best constant depth, or deep sideways and
a constant down), not against today's AI:

| | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| Reading the lanterns saves (tower time, strict) | -0.5 % | **3.4 %** | **3.8 %** | **3.2 %** |
| ... of the whole race (strict) | -0.2 % | 1.2 % | 1.1 % | 0.9 % |
| ... of tower time against today's AI (the window's centre) | 18.1 % | 14.5 % | 12.8 % | 10.5 % |
| An expert at the hard edge saves (strict, tower time) | 0.3 % | 4.8 % | 5.1 % | 5.3 % |
| Best fixed depth on a down-hop | 250 u | 171 u | 180 u | 167 u |
| A lantern reader's race (median, modelled) | 59.5 s | 85.5 s | 105.2 s | 126.5 s |
| Today's AI on the OLD towers (median, modelled) | 61.9 s | 85.0 s | 110.4 s | 128.7 s |
| The new AI: misses a race, deep landings | 0, 35 % | 0, 55 % | 0, 59 % | 0, 72 % |
| Crown gems a race, new AI / reader | - | - | - | 2.8 / 0.0 |
| Drop every (centre hops) | 5.1 s | 3.8 s | 3.7 s | 3.2 s |
| Lantern segments / wall tiles a course (max) | 96 / 960 | 80 / 960 | 112 / 1,472 | 130 / 1,984 |

I1 is every hop loose, so there is nothing to read (a fixed deep pilot is as good), as the ladder intends. A pilot who
IGNORES the lanterns gets 1-2 more rings and up to 2.7 s more tower time at I2-I4 from the tight hops; one who reads
them finishes as fast as or faster than today's pilot did on the old towers. The race clock is a model: cluster legs
at cruise with a turn allowance, towers flown hop by hop; whole-race medians over the ~90 s target at I3/I4 were
already there before this round.

### Fun score

| Vessel | Game | Verb | Flow | Ladder | Feedback | Rivals | Fairness | Fun | Floor |
|---|---|---|---|---|---|---|---|---|---|
| Tiger Moth | Waystation, the Drop Tower course (before) | 9 | 7 | 7 | 5 | 3 | 4 | **5.8** | 3 Rivals |
| Tiger Moth | **Waystation, Lantern Towers** (est. 2026-11-11) | 9 | 8 | 8 | 6 | 4 | 4 | **6.5** | 4 Rivals/Fairness |

- **Verb 9 -> 9.** The kit is unchanged; the hold is now a depth you choose per hop, priced by the next ring.
- **Flow 7 -> 8.** The lanterns show where to let go before you press, so the next decision is always in front of you;
  tight hops pack the towers (a Drop every 3.2-3.8 s at I2-I4, from 3.9-4.5). The clusters and the bare gaps are unchanged.
- **Ladder 7 -> 8.** One demand per rung: hold into the lanterns (I1), read their length (I2), roll gates (I3, existing),
  the gem (I4). Reading pays from I2 and is measured.
- **Feedback 5 -> 6.** Every graded hop lights in the world; streaks light the tower. Capped at 6: the seven sound slots
  ship empty, and the clusters gained nothing.
- **Rivals 3 -> 4.** The AI reads the lanterns, holds at its live speed, releases on the nearest frame, turns round after
  an overshoot instead of flying away, and four seats differ; its landing budget is a tested invariant. Never flown.
- **Fairness 4 -> 4.** A dust-stolen tile no longer gets a Blue tile stacked on it, and the grade allows a frame; but
  tight hops add a way to miss from I2, and a miss still costs a loop (about 8 s). Unplayed.

### Still open after this round

- **Never compiled by Unity or flown** (`Docs/UNITY_VERIFICATION_CHECKLIST.md`, "Waystation's Lantern Towers").
- **Race length.** The model's whole-race medians (table above) are over the ~90 s target at I3 and I4, as they were
  before this round (the Drop Tower doc estimated 92 s and 105 s, "optimistic"). Tight hops cost a pilot who ignores the lanterns up to 2.7 s of tower time; a reader gains it back.
  If people's I3/I4 races run long, the lever is the cluster count per rung (`waystationClusterTarget`), Garrett's call.
- **The wormhole rule's wording.** `TIGER_MOTH.md` and the Drop SO's tooltip say only a drop paying the FULL recharge
  leaves a pair; the code measures press-to-landed distance, so a pair appears from a fall of about 779 u. Noted in
  `TIGER_MOTH.md` §1 for the vessel's owner; the code is not changed.
