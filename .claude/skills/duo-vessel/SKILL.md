---
name: duo-vessel
description: Use for ANY "two vessels in one" class in Cosmic Shore - a DUO vessel that takes ONE squad slot and is balanced as ONE vessel, but is really two craft that two humans, or a human and an AI, fly separately, swapped with the SAME D-pad left/right gesture as the arena pilot swap (a phone flip on mobile). Holds the duo contract (one slot, two hulls, one shared element meter, the pilot matrix, the unified swap, per-half cameras, HUDs and AIs, crystals that restock both halves, balance as one), the lab-first build order, the code anchors a duo hooks into and the pieces that do not exist yet, and one design brief per duo under references/designs/ (the first is the Goby & Pistol - a two-thumb goby (flies like the Squirrel) that lays the path and a pistol shrimp that plays a warped 2D sidescroller unrolled from the goby's ribbon, with primer, heat and flares). Trigger on "duo vessel", "two vessels in one", "two-in-one", "tandem vessel", "co-op vessel", "two pilots one vessel", "switch characters", "phone flip swap", Goby, Pistol, the Flier, the Bird, the sidescroller vessel, or any references/designs/*.md here. Load /vessel alongside it for the single-hull contract every half still owes.
---

# Duo Vessel Protocol

A **duo vessel** is one vessel class built from **two craft**. This skill exists so nobody has to
explain that again. Say "a duo vessel" or "a two-in-one" and this file is the explanation. When a
duo teaches something new, it is added here (§8), so the skill grows with each duo.

> **The lab comes first (Garrett, 2026-10-14).** Prototyping **both halves together** in a lab is
> essential to a duo's first steps. Build it as a page in the ONE Vessel Studio artifact
> (https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa) with `/vessel-studio` and `/labmaker`
> before any Unity work. Recommend it to the person in one line. Then `/artifact-to-unity` clones
> the decided page into the game. Never publish a second artifact.

## 0. Read first

- **`references/designs/<duo>.md`**: the brief for the duo you are working on. Read every
  **[open]**, **[read]** and **[proposed]** tag before building. An [open] item is a question for
  Garrett, never a gap to fill. Briefs today:
  - `goby-and-pistol.md`: the **Goby**, a two-thumb path-layer (it flies like the Squirrel) with primer and flares, and the
    **Pistol**, a pistol shrimp that plays a warped 2D sidescroller unrolled from the Goby's path.
    (Garrett first called them the Flier and the Bird.)
    Its lab is `Docs/Studios/VesselStudio/goby.html` (round 1, 2026-10-14). It is the template for
    the next duo's lab.
- **`references/ANCHORS.md`**: the code a duo plugs into (seats, the arena pilot swap, input,
  phone flip, cameras, trails and the spline, crystals), and the list of pieces that **do not
  exist yet**. Re-verify an anchor before relying on it. Anchors drift.
- **`/vessel`** and its `references/CONTRACT.md`: **each half is a vessel** and owes the
  single-hull contract (registration, actions, the four-icon row, morphs, impact effects), except
  where §3 of this file says how the pair shares it.
- **`/vessel-ai`**: every half needs an AI (§2.3).
- **`/vessel-studio`** and **`/labmaker`**: the lab (§5 step 2).
- `Docs/HomeHub/ARCHITECTURE.md` §3.9: the arena pilot swap, which the duo swap IS (§2.2).
- `Docs/MULTIPLAYER_START_HERE.md`: read it before touching seats, ownership or the co-op
  handshake.

## 1. The definition (what makes a vessel a duo)

1. **One slot.** The duo takes **one** seat on a squad: one pick in the vessel select, one entry
   in the team count, and one AI backfill decision. It never takes two.
2. **Balanced as one.** Its power budget is one vessel's budget. Balance the **pair** against
   single hulls, then split the budget between the halves (§4).
3. **Two craft, two pilots.** Each half is a real craft with its own body, flight, abilities,
   camera and HUD. Each half is flown by **exactly one pilot** at any moment: a human or that
   half's AI.
4. **Switchable.** The local human moves between the halves with the arena's swap gesture (§2.2).
   The half they leave is never pilotless: its AI takes it on the same frame.
5. **Interdependent by design.** The halves are worth more together than apart. One makes what the
   other uses (the Goby's trail is the Pistol's ground, and its primer feeds the Pistol's
   explosions). A duo whose halves ignore each other is two vessels and should be split.

**What duos are for.** A duo can pair two different kinds of play, so **a half can welcome a
player the 3D game does not** (Garrett, 2026-10-14). The Pistol is a 2D sidescroller made from the
3D match, so someone who does not want to fly in 3D can still contribute to their team and
experience the world. When designing a duo, ask what player each half is for.

## 2. The duo contract (every duo owes this, whatever its design)

### 2.1 The pilot matrix

| Squad seat is filled by | Half A | Half B | Swap does |
|---|---|---|---|
| 1 human (solo) | human or AI | the other | swaps the human between halves. The AI takes the half they left |
| 2 humans (co-op) | human 1 | human 2 | **offers** a trade. Both must accept. It is never forced |
| 1 human, AI partner | human | AI | same as solo |
| AI backfill | AI | AI | n/a |

- Co-op puts two humans **into one seat**. The party/lobby must be able to say "these two players
  are one vessel". **This does not exist yet** (ANCHORS.md §3).
- If a co-op human leaves, their half falls to its AI on the same frame. The seat survives,
  because the seat is the duo, not the player.

### 2.2 Switching: ONE swap, the arena's (LOCKED, Garrett 2026-10-14)

**The duo swap is the arena pilot swap, unified with it, never a second mechanic.**

- Same gesture: **D-pad left/right**, keyboard **1/2** (`PilotSwapGesture`).
- Same path: `Player.RequestPilotSwap` → server validation → ownership transfer → `ApplyLocal`
  (`PilotSwap`).
- What differs is only **which hulls are in the ring**:
  - **Duo on any card:** the ring holds the duo's two halves.
  - **Duo on an arena card:** **[open, fleet-level]**. Either the partner half joins the team's
    ring, or the duo ring takes priority. Decide once and record it here.
- **Mobile: a phone flip** fires the same swap. Solo, it swaps; co-op, it offers (§2.1).
  - Adding the flip to the shared gesture also gives the arena a mobile swap. That is a fleet
    change, so flag it in the PR.
  - The flip today raises `InputEvents.FlipAction`, which is an **ability input**. The Grizzly
    binds it, and on a pad it is the right shoulder. So a duo's halves must not bind FlipAction,
    and the Grizzly's binding has to be resolved before the flip swaps on arena cards.
- **Co-op (human ↔ human)** is new. `PilotSwap` only trades a human with an AI hull today. The
  offer/accept handshake is an extension of it, never a separate path.
- **What moves on a swap:** input, camera, HUD, the local-pilot systems
  (`VesselController.ChangePlayer` retargets them), and the hull's network ownership. **What never
  moves:** the seat, the team, cooldowns, charges, the shared meter, and any state of either half.
  A swap changes the **pilot**. It never resets a craft.
- **A swap is safe at any moment**: mid-ability, mid-boost, mid-launch. Release held inputs
  (`ReleaseHeldInputs`). The half you leave carries on under its AI from exactly where it was.

### 2.3 Every half has an AI

The swap means any half can lose its human at any frame. So **each half ships with its own AI
pilot**, held to the `/vessel-ai` non-negotiables. A half without an AI cannot be swapped away
from, so the AI is not a "later" item. The brief lists each half's AI priorities.

### 2.4 Cameras, HUD, visibility

- **One camera per local human.** It follows the half they fly and retargets on a swap. Each half
  owns its camera, and the two may be entirely different kinds. The Pistol's is a **far side
  camera beside it**, sidescroller-style, which is a camera mode the game does not have yet.
- **Each half owns its HUD row** (§3.3). A swap swaps the row.
- **Per-viewer content is a duo feature.** The Pistol's whole warped world, spline included, is
  seen only by the Pistol's pilot. Build it **on that pilot's client only**, never by
  broadcasting a hidden object to everyone. Decide it at render time from "which half does the
  local human fly". Everyone else sees an ordinary craft in 3D.

### 2.5 Score, crystals, death

- **Score follows the PILOT** (the `PilotSwap` rule): what you earn in either half is yours. **The
  seat's score is the sum of its two Players.** Team totals are unchanged by construction.
- **Crystals restock BOTH halves fully** (Garrett, 2026-10-14, adopted as the duo default),
  whichever half took the crystal. A half-specific crystal bonus comes on top when that half
  takes it.
- **Death / respawn: [open per duo].** Does one half dying take down the pair, or does it respawn
  alone near its partner? Write the answer in the brief. Never pick one silently.

## 3. How a duo maps onto the vessel system

### 3.1 Class identity

The fleet assumes **one `VesselClassType` = one prefab = one hull**. A duo breaks that. There are
two options (ANCHORS.md has the code each one touches):

- **A: one class, one composite prefab.** One `VesselClassType` for the pair, with both halves as
  child `IVessel`s under a `DuoVessel` root.
  - Pros: the slot, the select screen and backfill see one vessel.
  - Cons: everything that assumes one Player flies one Vessel must change.
- **B: two hulls, two Players, one seat.** Each half is its own `VesselClassType` and is flown by
  its own Player (human or AI). An SO pairs them into one seat.
  - Pros: each half reuses the single-hull pipeline, and the swap is literally `PilotSwap`.
  - Cons: slot counting, selection and backfill must learn to treat a pair as one seat.

**The code leans hard toward B.** Flight, abilities and the AI's stick all go through
`Player.InputStatus`. `PilotSwap.ApplyLocal` already trades hulls between a human Player and an
AI Player, with ownership, camera, HUD, local-pilot systems, motion and AI restart all handled.
Recommend B unless a design needs something only A gives.

Do not choose between them inside a feature branch. It is an architecture decision: ask, and
record the answer here. New enum members get **explicit numeric values** (CLAUDE.md); read the
enum before taking a value.

### 3.2 Abilities and input

- Each half has its own action set. Input goes to **the half the local human flies**. The other
  half is driven by its AI through the same input surface.
- A half's input can be a different genre. The Pistol has **no yaw and no off-plane movement**:
  its stick and aim are 2D. Map the input strategy to the half; never hide a 3D control the half
  cannot use.
- Hand-picked targets (the Pistol's Launch: tap and drag, or the right trigger + right stick) go
  through the input strategy, never a raw `Input` read.

### 3.3 The four-icon row and ONE shared meter (Garrett, 2026-10-14)

- **Each half has its own four-icon row** (charge → mass → space → time), shown while the local
  human flies that half.
- **The pair has ONE element meter.** One set of petals feeds both rows. That is "balanced as
  one" made literal.
- The element → ability mappings go through `/vessel` §3, the design-approval gate. **Never
  invent them.**
- **Trap:** element levels are simulated on the machine that owns a hull and do not replicate
  (stated in `PilotSwap`). A meter shared by two hulls, which may be owned by two machines, needs
  one authoritative home. Design that before the meter is wired.

## 4. Balancing a duo

- Model the pair, not the halves. A duo's output is both halves' output **while both are
  flying**. Count the AI half at the AI's real skill.
- The interdependence is part of the budget. If the Pistol's damage comes mostly from primed
  targets, the Primer's charges are part of the Pistol's damage budget.
- Check solo (a human plus an AI) and co-op (two humans) separately. Solo must not be weaker than
  a single hull; co-op must not be stronger than a single hull flown well. If both cannot hold,
  the brief says which wins.
- A crystal restocks both halves, so a crystal is worth more to a duo than to a single hull.
  Count it.
- The arena fleet table (`/arenagame`) gets **one** row per duo.

## 5. Building a duo, in order

1. **Brief.** Copy the newest file under `references/designs/` as the template. Record the user's
   words, and tag every line [stated] / [read] / [open] / [proposed]. Ask only the [open] items
   that block the next step (AskUserQuestion).
2. **Lab (required first build).** One page in the ONE Vessel Studio with **both halves
   together**:
   - both halves' views on screen at once;
   - the swap on the arena keys, with an AI on the half you are not flying;
   - the duo's signature mechanics;
   - a headless scorecard for the maths that has to be right (for the Goby & Pistol, the ribbon
     unrolling).

   The brief's lab section says what that duo's lab must hold. Decide the open feel questions
   there and log them.
3. **Architecture call** (§3.1) once, recorded here.
4. **Seat and swap plumbing** (generic, built once for every duo): the two-Player seat, the duo
   ring in `PilotSwap`, the phone-flip swap, the co-op offer/accept, and the shared meter. These
   are the "not built" rows in ANCHORS.md.
5. **Each half** through `/vessel` (registration, actions, row) and `/vessel-ai`.
6. **Duo-specific mechanics** from the brief (for the Goby & Pistol: the warped world, spline,
   primer, heat, flares).
7. **Clone** the lab's numbers in with `/artifact-to-unity`. Never hand-tune a mapped field.
8. Ship through `/ship`. Every C# commit passes `/verify-unity` (CLAUDE.md).

## 6. Traps (fill this as duos teach them)

- **A pilotless half.** Any path that takes a human off a half (swap, disconnect, pause, death of
  the partner) must hand the half to its AI in the same frame.
- **Counting the seat twice.** Team size, backfill and scoreboards that count `IVessel`s, rather
  than seats, will count a duo as two.
- **Broadcasting per-viewer content.** A spline made invisible with a layer on every client still
  costs every client. Build it only on the viewer's client.
- **Resetting on swap.** A swap that re-inits a half (cooldowns, charges, trail) is a bug.
- **A held button across a swap.** Release held inputs on every swap.
- **A second swap.** `PilotSwap` is THE swap. Extend its ring and handshake; never write another.
- **A shared meter with no owner.** Element levels do not replicate across owners (§3.3).
- **A half that can cash the other half's payoff alone.** In the Goby & Pistol lab's first round, the
  Goby's flares set off the primes meant for the Pistol. The "Goby alone" baseline then scored chain
  kills, and the duo's reason to exist went missing from the table. For each payoff, name the half that
  cashes it, and check the baseline without that half scores none of it.
- **An unrolled world folds where the path crosses itself.** If one half's world is built from the other
  half's path (the Pistol's sidescroller), loops, helices and turns tighter than the band fold it. Measure
  this with an unroll test that has a control which must fold (a flat circle), before tuning anything else.

## 7. Glossary

- **Duo vessel / two-in-one:** this skill's subject.
- **Half:** one of the two craft in a duo.
- **Seat / slot:** the one squad place the duo fills.
- **Swap:** the arena pilot swap, moving the local human between halves.
- **Offer:** in co-op, a swap request the partner must accept.
- **Ribbon / unrolled world:** the Pistol's 2D world, the Goby's path with its yaw straightened
  out (brief §4).

## 8. Keep it alive

- **A new duo:** add `references/designs/<name>.md` and list it in §0.
- **A decision:** record it in the brief's decisions log. If it applies to every duo, promote it
  into §1-§5 here and name the duo it came from. Round 2 of the Goby & Pistol promoted four:
  - the unified swap;
  - crystals restock both halves;
  - one shared meter;
  - the lab comes first.
- **A trap:** add it to §6 with what it cost.
- **An anchor that moved or a piece that got built:** update ANCHORS.md the same day.
