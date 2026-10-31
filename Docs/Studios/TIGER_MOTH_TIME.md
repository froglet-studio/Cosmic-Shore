# Tiger Moth Time: the Drop Tower

The Tiger Moth's TIME card is Garrett's **Waystation** (GameModes 58). This page records the work to make it fun on
Shombith's direction: vertical movement, like Flappy Bird.

The work ships as a separate mode, **Waystation Prototype** (GameModes 76). Waystation stays as Garrett made it until he
approves a replacement (Shombith, 2026-10-31).

The mode doc is `Assets/_Scripts/Controller/Arcade/WaystationPrototype/WAYSTATION_PROTOTYPE.md`.

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

**Measured numbers.** `Tools/Build/waystation_prototype_course.py`, 200 seeds × 4 intensities, 5 clusters. Every check
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

Each dimension is scored 0-10, and anything unknown scores low. The prototype's row is also in that scores table.

| Vessel | Game | Verb | Flow | Ladder | Feedback | Rivals | Fairness | Fun | Floor |
|---|---|---|---|---|---|---|---|---|---|
| Tiger Moth | Waystation (58), as measured | 9 | 6 | 7 | 4 | 3 | 4 | **5.5** | 3 Rivals |
| Tiger Moth | **Waystation Prototype (76)**, est. | 9 | 7 | 7 | 5 | 3 | 4 | **5.8** | 3 Rivals |

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

## 5. If Garrett approves: replacing Waystation

These are left untouched in Waystation on purpose:
- The card description, toast 118 and the preview's ObjectiveText and Notes still say Butterfly and FOLD
  (`author_waystation_assets.py`).
- `waystation_course.py --target` defaults to 24, and so do its harness and `WaystationCourseTests`. The shipped target
  is 15.
- `WAYSTATION.md`:
  - it says target 24, comeback 0.4 and "six checks"; the code has 15, 0.64 and seven;
  - it cites the retired `WaystationFoldTests` and `aiFoldAimDegrees`;
  - it says `maxPlausibleSpeed` is 400; the scene has 1000.

The replacement is either to point the Waystation card at the prototype's scene and controller, or to fold
`WaystationTower` into `WaystationCourseSource`. The generator, the model and the tests here are the record of both.
