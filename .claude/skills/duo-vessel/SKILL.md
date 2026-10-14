---
name: duo-vessel
description: Use for ANY "two vessels in one" class in Cosmic Shore - a DUO vessel that takes ONE squad slot and is balanced as ONE vessel, but is really two craft that two humans, or a human and an AI, fly separately, with the D-pad (controller) or a phone flip (mobile) switching which half the local player flies. Holds the duo contract (one slot, two hulls, the pilot matrix, the swap rules, per-half cameras, HUDs and AIs, the shared crystal/score/balance budget), the code anchors a duo hooks into and the pieces that do not exist yet, and one design brief per duo under references/designs/ (the first is the Flier & Bird - a Serpent-like path-maker whose alternating-angle trail is a spline the 2.5D Tiny-Wings Bird rides, with primer, heat and flares). Trigger on "duo vessel", "two vessels in one", "two-in-one", "tandem vessel", "co-op vessel", "two pilots one vessel", "switch characters with the d-pad", "phone flip swap", the Flier, the Bird, or any references/designs/*.md here. Load /vessel alongside it for the single-hull contract every half still owes.
---

# Duo Vessel Protocol

A **duo vessel** is one vessel class built from **two craft**. This skill exists so nobody has to
explain that again. Say "a duo vessel" or "a two-in-one" and this file is the explanation. When a
duo teaches something new, it is added here (§8), so the skill grows with each duo.

> **Fly it in the Vessel Studio first.** Every duo gets a page in the ONE Vessel Studio artifact
> (https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa) before Unity changes. Load `/vessel-studio`
> for it, and recommend it to the person in one line. A duo's feel (the pull between the halves,
> the swap, the AI half) is exactly what a page decides faster than a build does. Then
> `/artifact-to-unity` clones the decided page into the game. Never publish a second artifact.

## 0. Read first

- **`references/designs/<duo>.md`**: the brief for the duo you are working on. Read every
  **[open]** and **[read]** tag before building. An [open] item is a question for Garrett, never a
  gap to fill. Briefs today:
  - `flier-and-bird.md`: the Flier (Serpent-like path-maker, primer, flares) and the Bird (2.5D
    Tiny Wings rider of the Flier's trail spline).
- **`references/ANCHORS.md`**: the code a duo plugs into (squad seats, player-to-vessel binding,
  input, D-pad, phone flip, cameras, trails, crystals, homing projectiles), and the list of pieces
  that **do not exist yet**. Re-verify an anchor before relying on it. Anchors drift.
- **`/vessel`** and its `references/CONTRACT.md`: **each half is a vessel** and owes the
  single-hull contract (registration, actions, the four-icon row, elements, morphs, impact
  effects) unless §3 of this file says how the duo shares it.
- **`/vessel-ai`**: every half needs an AI (§2.3). Its non-negotiables (input-only, difficulty
  edits belief, never the stick) apply to each half.
- `Docs/MULTIPLAYER_START_HERE.md`: read it before touching seats, ownership or the swap
  handshake. A duo's two pilots are two network clients when two humans fly it.

## 1. The definition (what makes a vessel a duo)

1. **One slot.** The duo takes **one** seat on a squad: one pick in the vessel select, one entry
   in the team count, and one AI backfill decision. It never takes two.
2. **Balanced as one.** Its power budget is one vessel's budget. Two halves do not mean two
   vessels' worth of damage, speed or score. Balance the **pair** against single hulls, then split
   the budget between the halves (§4).
3. **Two craft, two pilots.** Each half is a real craft with its own body, flight, abilities,
   camera and HUD. Each half is flown by **exactly one pilot** at any moment: a human or that
   half's AI.
4. **Switchable.** The local human can move between the halves: the **D-pad** on a controller and
   a **phone flip** on mobile (§2.2). The half they leave is never pilotless. Its AI takes it on
   the same frame.
5. **Interdependent by design.** The halves are worth more together than apart. One makes what the
   other uses (the Flier's trail is the Bird's ground, and its primer feeds the Bird's explosions).
   A duo whose halves ignore each other is two vessels and should be split.

## 2. The duo contract (every duo owes this, whatever its design)

### 2.1 The pilot matrix

| Squad seat is filled by | Half A | Half B | Swap does |
|---|---|---|---|
| 1 human (solo) | human or AI | the other | swaps the human between halves. The AI takes the half they left |
| 2 humans (co-op) | human 1 | human 2 | **offers** a trade. Both must accept. It is never forced |
| 1 human, AI partner requested | human | AI | same as solo |
| AI backfill | AI | AI | n/a |

- Co-op pairs two humans **into one seat**. The party/lobby must be able to say "these two players
  are one vessel". **This does not exist yet** (see ANCHORS.md).
- If a co-op human leaves, their half falls to its AI on the same frame. The seat survives,
  because the seat is the duo, not the player.

### 2.2 Switching

- **Controller: the D-pad.** One direction (default: D-pad down) means "swap halves". The other
  directions stay free for the halves' own use. **[open per duo]** Which direction, and whether
  other directions are used, is a design call. Record it in the brief.
- **Mobile: a phone flip.** Solo: the flip swaps. Co-op: the flip **offers** a swap to the partner
  (stated in the Flier & Bird brief, adopted as the duo default).
- **Keyboard:** a key bound through the same input action as the D-pad. Never a hard-coded key.
- **What moves on a swap:** input routing, the camera, the HUD (the four-icon row, gauges and
  hints of the new half), the audio listener, and in multiplayer the **input authority** of that
  half. **What never moves:** the seat, the team/domain, the score owner, cooldowns, charges and
  any state of either half. A swap is a change of **pilot**, never a reset of a craft.
- **A swap must be cheap and safe at any moment.** Mid-ability, mid-boost and mid-launch all have
  to work. The half you leave keeps going under its AI from exactly where you left it.

### 2.3 Every half has an AI

The swap means any half can lose its human at any frame. So **each half ships with its own AI
pilot**, held to the `/vessel-ai` non-negotiables. The AI is not optional and is not a "later"
item: a half without an AI cannot be swapped away from. The brief lists each half's AI
priorities (the Flier's: crystal, then flora and fauna, then lay a path for the Bird).

### 2.4 Cameras, HUD, visibility

- **One camera per local human.** It follows the half they fly and retargets on a swap. Each half
  owns its camera settings (e.g. "far cam like the Serpent").
- **Each half owns its HUD.** The four-icon row is per half (§3.3). A swap swaps the row.
- **Per-viewer content is a duo feature.** Some content exists only for one half's pilot (the
  Flier & Bird spline is visible only to the Bird). It is drawn by **that pilot's client only**,
  never by broadcasting a hidden object to everyone. Decide it per viewer, at render time, from
  "which half does the local human fly".

### 2.5 Score, crystals, death

- **Score and kills go to the seat**, whichever half earned them.
- **Crystals**: each brief says whether a crystal restocks one half or both. The default (until a
  brief says otherwise) is that the half that touches the crystal gets the crystal, and the
  brief's crystal clause says what it does for the other half.
- **Death / respawn**: **[open per duo]**. Does one half dying take down the pair, or does it
  respawn alone near its partner? Write the answer in the brief. Never pick one silently.

## 3. How a duo maps onto the vessel system

### 3.1 Class identity

The fleet assumes **one `VesselClassType` = one prefab = one hull**. A duo breaks that. The
working approach (until Garrett picks one; see ANCHORS.md for the code each one touches):

- **A: one class, one composite prefab.** One `VesselClassType` for the pair. The prefab holds
  both halves as two child `IVessel`s, and a `DuoVessel` root owns the seat, the swap and the
  shared budget. Pros: the slot, the select screen and backfill see one vessel. Cons: everything
  that assumes "one vessel transform per player" must learn about two.
- **B: two classes, paired by config.** Each half is its own `VesselClassType`, and an SO pairs
  them. Pros: each half reuses the single-hull pipeline unchanged. Cons: the slot counting,
  selection and backfill must learn to treat a pair as one seat.

Do not choose between them inside a feature branch. It is an architecture decision; ask, and
record the answer here.

New enum members get **explicit numeric values** (CLAUDE.md, Code Style). Never take "the next
free id" without reading the enum.

### 3.2 Abilities and input

- Each half has its own action set. The input layer routes the local human's input to **the half
  they fly** and nothing to the other half (the other half is driven by its AI through the same
  input surface, per `/vessel-ai`).
- Abilities that need a target picked by hand (the Bird's Launch: tap and drag, or the right
  trigger + right stick) go through the input strategy, never a raw `Input` read.

### 3.3 The four-icon row and elements

The LOCKED four-icon row (`Docs/claude/VESSEL_HUD_AND_ELEMENTS.md`) is per vessel. For a duo, the
default reading is: **each half has its own four-icon row** (four abilities, charge, mass, space,
time), shown only while the local human flies that half. **[open, fleet-level]:** whether the
pair shares one element meter (one set of petals, which feeds both rows) or each half has its
own. The default is ONE meter for the pair, because of "balanced as one" (§1.2). The element →
ability mappings themselves go through `/vessel` §3, the design-approval gate. **Never invent
them.**

## 4. Balancing a duo

- Model the pair, not the halves. A duo's output is the halves' outputs **while both are
  flying**. Count the AI half at the AI's real skill, not at a human's.
- The interdependence is part of the budget. If the Bird's damage comes mostly from primed
  targets, the Primer's charges are part of the Bird's damage budget.
- Check the solo case and the co-op case separately. Solo means one human plus one AI, and that
  pair must not be weaker than a single hull. Co-op means two humans, and that pair must not be
  stronger than a single hull flown well. If both cannot hold, the brief must say which one wins.
- The arena fleet table (`/arenagame`) gets **one** row per duo, not one per half.

## 5. Building a duo, in order

1. **Brief.** Copy the newest file under `references/designs/` as the template. Record the
   user's words, tag every line [stated] / [read] / [open], and ask the [open] items you need for
   the next step (AskUserQuestion). Never all of them at once, only the ones blocking.
2. **Studio page.** A page in the ONE Vessel Studio (`/vessel-studio` §2) with **both halves**,
   the swap on a key and the AI on the half you are not flying. Decide the feel there: the pull
   between the halves, the spline, the swap.
3. **Architecture call** (§3.1) once, recorded here.
4. **Seat and swap plumbing** (generic, built once for every duo): the seat that holds two
   pilots, the swap input (D-pad / flip / key), camera, HUD and input retarget, and the co-op
   offer/accept handshake. These are the "not built yet" rows in ANCHORS.md.
5. **Each half** through `/vessel` (registration, actions, row, elements) and `/vessel-ai`.
6. **Duo-specific mechanics** (the brief's own systems: spline, primer, heat, flares).
7. **Clone** the studio's numbers in with `/artifact-to-unity`. Never hand-tune a mapped field.
8. Ship through `/ship`. Every C# commit passes `/verify-unity` (CLAUDE.md).

## 6. Traps (fill this as duos teach them)

- **A pilotless half.** Any path that removes a human from a half (swap, disconnect, pause, death
  of the partner) must hand the half to its AI in the same frame. "One frame without a pilot"
  shows up as a craft that freezes or drifts, and the AI then starts from a stale state.
- **Counting the seat twice.** Team size, backfill and scoreboards that count `IVessel`s, rather
  than seats, will count a duo as two. Look for every place that counts vessels.
- **Broadcasting per-viewer content.** Making a spline invisible with a layer on every client
  still sends it everywhere and costs every client. Build it only on the viewer's client.
- **Resetting on swap.** A swap that re-inits a half (cooldowns, charges, trail) is a bug.

## 7. Glossary

- **Duo vessel / two-in-one:** this skill's subject.
- **Half:** one of the two craft in a duo.
- **Seat / slot:** the one squad place the duo fills.
- **Swap:** moving the local human from one half to the other.
- **Offer:** in co-op, a swap request the partner must accept.

## 8. Keep it alive

- **A new duo:** add `references/designs/<name>.md` and list it in §0.
- **A decision:** record it in the brief's decisions log. If it applies to every duo, promote it
  into §1-§5 here and say which duo it came from.
- **A trap:** add it to §6 with what it cost.
- **An anchor that moved or a piece that got built:** update ANCHORS.md the same day, so the next
  session does not rebuild it.
