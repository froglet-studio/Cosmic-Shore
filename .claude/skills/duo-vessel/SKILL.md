---
name: duo-vessel
description: Use for ANY multiplayer vessel in Cosmic Shore - a CREW vessel (this skill's older word: a DUO vessel), one hull class that takes ONE squad slot and is balanced as ONE vessel but carries two subvessels that two humans, or a human and an AI, fly separately, swapped with the SAME D-pad left/right gesture as the arena pilot swap (a phone flip on mobile). Holds the ONE notion of a crew vessel both shipped ones keep (the seat on the hull's root, ICrewSeat; the card that names both, SO_Vessel.CrewNames - "Wrasse and Grouper", "Goby and Pistol"; the shared seat rules CrewSeat, the shared view layout CrewPictureInPicture and the seat camera that renders the real game with depth from its plane and the dither corridor, CrewViewRender; the Vessel Changer as the way in; check_crew_vessels.py), the duo contract (one slot, one shared element meter, the pilot matrix, the unified swap, per-seat cameras, HUDs and AIs, crystals that restock both, balance as one), the lab-first build order, the code anchors, and one design brief per crew vessel under references/designs/ (the Goby & Pistol - a two-thumb goby that lays the path and a pistol shrimp that plays a 2D side view unrolled from it; the Wrasse and Grouper - a two-thumb hull and a one-thumb kite on its plane). Trigger on "crew vessel", "multiplayer vessel", "duo vessel", "two vessels in one", "two-in-one", "tandem vessel", "co-op vessel", "two pilots one vessel", "two seats one hull", "switch characters", "phone flip swap", Goby, Pistol, Wrasse, Grouper, Kite Navigator, ICrewSeat, CrewSeat, CrewNames, the sidescroller vessel, or any references/designs/*.md here. Load /vessel alongside it for the single-hull contract the hull still owes.
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

## One notion of a crew vessel (both shipped ones keep it; read before touching either)

> **Where the crews stand (2026-10-21, after merging bleeding-edge).** Only the Goby's seat is on `ICrewSeat` today.
> Bleeding-edge grew its own crew pieces for the Wrasse, the Orca and the Honeyguide (`KiteNavigator` without `ICrewSeat`,
> `WrasseCrewBodies`, `OrcaCrew`, `CrewSeatChoice`), so this branch's move of the Wrasse onto
> `ICrewSeat` was withdrawn in the merge in favour of theirs. Their plan takes it from here:
> `Controller/Vessel/Crew/KITE_NAVIGATOR.md` "Two humans, two seats", step 2 (Garrett with Yash). The Wrasse row below
> is that target; `check_crew_vessels.py` lists the Wrasse and the Honeyguide as PENDING until then. (Marlin and Jacks was a crew too
> until 2026-10-21, when Garrett reduced it to the Jacks: one player who herds or flies the two fish, no second seat;
> `R_VesselActions/JACKS.md`.)

**Words.** The code says **crew vessel** (`Controller/Vessel/Crew/`, `SO_Vessel.CrewNames`, `ICrewSeat`); this skill
was written first and says **duo vessel**. They are the same thing. "Multiplayer vessel" in a request means this too.
Say *crew vessel* in new code and docs; each half is a **subvessel**, the extra one a **seat**.

| Crew vessel | Card | Hull (`VesselClassType`) | Seat on its root | The seat's view | Brief |
|---|---|---|---|---|---|
| **Wrasse** | Wrasse and Grouper | Wrasse = 19 (the Sparrow without its kit, two thumbs) | `KiteNavigator`: a one-thumb kite on the hull's x-z plane | top-down orthographic, the real world projected onto the plane | `references/designs/wrasse-and-grouper.md` → `Controller/Vessel/Crew/KITE_NAVIGATOR.md` |
| **Goby** | Goby and Pistol | Goby = 18 (a Squirrel-derived prefab stripped of the Squirrel's kit; pinned: it perches and darts like the Shrike slings, 2026-10-22) | `GobyPistolDuo`: the Pistol, riding a FLAT plane through each dart from perch to perch, downhill either way | side-on perspective square to the plane | `references/designs/goby-and-pistol.md` (its top section is the kit as it is now) |

**The shape both chose** (call it **C** beside §3.1's A and B): **one hull class and one prefab** - a clone of a donor
hull, authored and registered by `Tools/Build/author_<hull>_assets.py` (`author_wrasse_assets.py` is the fuller
pattern, `author_goby_assets.py` the first) - with **the seat as one component on the hull's root** that implements
`ICrewSeat` and is wired to its own config SO. The seat's subvessel is simulated and drawn by the seat; it is not an
`IVessel` of its own. A and B stay the options for a seat that needs a real second hull.

**What every crew vessel keeps** (one piece of shared code or data each; a seat that hand-rolls one of these is a
defect):

| Rule | Where it lives |
|---|---|
| **The card names both subvessels**: `CrewNames` (two entries) joined by `CrewNameTemplate` (`{0} and {1}`) is `SO_Vessel.CardName`, which every card, station and panel shows. `Name` stays the one-word key (the hangar's unlocks are keyed by it). | `SO_Class_<Hull>.asset`, written and checked by the generator |
| **The way in is the Toybox's Vessel Changer** ("bring it to Unity" = fly it from there, CLAUDE.md). The hull is on `ToyVesselRoster.Default`, the Vessel Prefab Container, `DefaultNetworkPrefabs`, both class lists, and has its four `CrystalHullFusionConfig` entries. Claim the enum value after checking bleeding-edge AND the in-flight branches. | the generator `--check`; `EnumIntegrityTests` |
| **The seat wakes** only on the local pilot's own hull while a human flies it, or while the seat itself handed the hull to its AI. | `CrewSeat.ShouldWake` |
| **ONE swap, seats then hulls** (Garrett, 2026-10-23): `PilotSwapGesture` (D-pad left / right, the left / right arrow keys, a two-finger swipe; no key picks a seat outright) steps the PERSON round one list of the domain's seats, the same for everyone on it - the seats of the hull they are on, then the next hull's - on `PilotSwap.CooldownSeconds`. A crew never reads the gesture itself: it implements `ICrewStations` (`SeatCount`, `LocalSeat`, `HostsRiders`, `TakeSeat`) and `CrewStations` moves the person through `TakeSeat`. A double tap on the small view (`CrewSmallViewDoubleTap`) still swaps a crew's two seats. `Docs/CREW_STATIONS.md`. | `CrewStations`, `LocalCrewStation` |
| **The empty seat is never pilotless.** Into the seat: the player's flight input paused, then the hull's `AIPilot` flies it; back: the opposite order, and only an autopilot the seat started is stopped. The seat has its own input-only AI for when the human flies the hull (`/vessel-ai`). | `CrewSeat.HandHullToAI` / `GiveHullBack`; `KiteNavigatorAI`, `PistolAI` |
| **Two views, one layout**: flying the hull, the seat's view is a square in the lower left (`pipSize` 0.32 of the screen height, `viewMargin` 0.015); flying the seat, it fills the screen and the pilot's camera (`CameraManager.ViewCamera`) is lifted into the same square. The pilot camera's rect and depth are always restored. | `CrewPictureInPicture` (it frames the small square with the style guide's `PipViewFrame`; `Dispose` it when the seat lets go) |
| **The seat camera renders the game** (Garrett, 2026-10-18, for both: "use the game's rendering", "as true to the game as possible"): the real prisms, hulls and materials under the game's lights and sky, never a re-styled copy, fog or tint. For its render only, the prisms' depth look measures distance **from the seat's plane** (`PrismDepthPlane`, `depthFullSpreadAt` 60), and the game's occlusion corridor (the screen-door dither) is **re-aimed for the seat's view** so mass in front of what the seat must see dithers instead of hiding it: the Kite's is a cone from its lens down to the board; the Pistol's is the game's own vessel corridor aimed from its lens at the Pistol. | `CrewViewRender` |
| **The seat's own marks** (the kite, motes, rings; the Pistol's tube, aim, primed / hot / held rings) are drawn for the seat's camera only (`RenderParams.camera`), instanced with URP/Unlit set transparent - never `Sprites/Default`, whose instanced path draws at zero alpha. | each seat's view code |
| **A seat takes its triggers from input bindings, never from `InputStatus`'s analog triggers** (Garrett, 2026-10-21: "The grouper can not use boost on mobile"). The hull binds `RightStickAction` / `LeftStickAction` to `CrewTriggerRT` / `CrewTriggerLT` (`CrewTriggerActionSO`) and the seat implements `ICrewTriggers.SetTrigger`; a pad's triggers, the keyboard's shifts and a phone's thumb lifts (lift left = RT, right = LT) all raise those events, while the analog triggers are written by no phone. The card the trigger drives names its input, and the hull's `TouchControlProfiles` row shows the lift glyph. A held trigger is cleared on every seat change: the handler drops releases while the AI flies the hull. | `CrewTriggerActionSO` (Wrasse, Orca); `HoneyguideTriggerActionSO` is the same shape; `author_touch_profiles.py --check` |
| **A seat's own abilities are its own controls, never spare D-pad directions** (Garrett, 2026-10-21). A hull ability is an input the hull binds (`CrewTriggerActionSO` for the triggers, `CrewButtonActionSO` for face buttons); on a phone each seat shows its own buttons round the right thumb while the person is in it (`TouchButtonView`, `TouchButtonLayout`), and a seat whose hull the AI flies presses itself directly (an `InputStatus` press is dropped under autopilot). | `OrcaHUDController`, `GobyPistolButtons` |
| **A second person on another machine** (2026-10-23): the server holds each person's station (`Player.NetStation`). A crew that `HostsRiders` runs a rider's seat on the rider's machine (the Orca's gunners) or relays it to the hull's machine (`ICrewRelay`: the Ratel's board); the hull's machine leaves a seat a rider holds alone (`CrewStations.HeldByRemoteHuman`). Every crew takes a rider (2026-10-23): the Orca's gunners, the Wrasse and the Pistol run on the rider's machine, with what belongs to the hull relayed to the hull's machine (the Wrasse's banked boosts and steals; the Goby's perches and lock come back); the Ratel is relayed to the hull's machine. A seat with no person in it is always the AI's: an own hull parked while its person rides another runs as an AI crew (`CrewStations.IsParkedHere`), and an AI Goby flies the seat's own Goby and Pistol AIs, not the platform autopilot. | `CrewStations`, `Docs/CREW_STATIONS.md` |

**Gates:** `python3 Tools/Build/check_crew_vessels.py` (and `--self-test`; in `/ship`'s gates, `.claude/skills/ship/gates.sh`) holds every
hull that carries an `ICrewSeat` to the card, roster and container rules (its `SeatName` is one of the card's
`CrewNames`), and every class that names a crew to having a seat. `CrewVesselTests` holds the same in the editor. Each hull's generator `--check` holds its own assets.

**Where the two still differ, on purpose or not yet:**
- The Wrasse has its own HUD controller (`WrasseHUDController`, the cards read the seat); the Goby has its own four
  cards (Vortex Sting, Dens, Undertow, Quick Jump, 2026-10-22) but still the Squirrel's HUD controller. Each Goby seat
  has its own controls (Garrett, 2026-10-21: "each seat has its own ui and controls. no need to use the dpad in a weird
  way"): the Goby's Dart and Lock are the hull's own inputs (`CrewButtonActionSO` on Button3 / Button4, a pad's RT / LT
  too, keys Q / F), and on a phone each seat shows its own buttons round the right thumb (`GobyPistolButtons`: Dart and
  Lock; Ghost, Snap and Back), the Orca gunner buttons' pattern. The D-pad is only the swap.
- Both hulls are stripped of their donor's kit (`author_wrasse_assets.py` round 4c; `author_goby_assets.py`
  `strip_squirrel_kit` and the Goby's own impact containers, 2026-10-22: "it should not have the squirrels abilities or
  be anything like the squirrel"). The Goby's model is still on the Squirrel's rig (team task).
- The Kite's lab is behind the game (`T-20261018-3247`); the Goby's lab IS the source (`/artifact-to-unity`).

## 0. Read first

- **`references/designs/<duo>.md`**: the brief for the duo you are working on. Read every
  **[open]**, **[read]** and **[proposed]** tag before building. An [open] item is a question for
  Garrett, never a gap to fill. Briefs today:
  - `goby-and-pistol.md`: the **Goby**, a watchman that perches, darts like the Shrike slings and locks rivals, and the
    **Pistol**, a pistol shrimp that rides a flat plane from perch to perch, builds dens, pulls mass into its plane and
    fires homing vortex rings (2026-10-22; its top section is the current kit, built straight in Unity).
    (Garrett first called them the Flier and the Bird.)
    Its lab is `Docs/Studios/VesselStudio/goby.html` (round 1, 2026-10-14). It is the template for
    the next duo's lab.
  - `wrasse-and-grouper.md`: the **Grouper**, the Sparrow's hull flown on two thumbs, and the **Wrasse**, a one-thumb
    kite on the hull's own plane whose head pops prisms and whose tail lassos motes into effects. Built in its own
    lab (`Docs/Studios/CrewVesselLab.html`) on `cece/compassionate-babbage-gc13io`; the full record is
    `Assets/_Scripts/Controller/Vessel/Crew/KITE_NAVIGATOR.md`.
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

**The duo swap is the arena pilot swap, unified with it, never a second mechanic.** Since 2026-10-23 it is one ring of
STATIONS for every person in freestyle and in a running arena match - the seats of the hull you are on, then the next
hull's (`CrewStations`, `Docs/CREW_STATIONS.md`); the open "ring" question below is answered: seats first, then hulls. A
hull without a person at its helm (an AI's, or one a person left to ride another) is taken by the arena's exchange, and
a free seat on a hull a person flies is ridden. The mobile swap is a two-finger swipe (the phone flip below was not
built).

- Same gesture: **D-pad left/right**, the keyboard's **left / right arrows** (`PilotSwapGesture`; keys 1 / 2 until 2026-10-23).
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
- **A seat view's 2D marks skip its post-processing; an orthographic seat view gets no Panini.** Draw the
  marks (motes, rings, cords, petals) through an `UnprocessedOverlay` so bloom and the tonemapper do not mute
  them into the world; `OrthographicLens` keeps Panini off every orthographic camera without the seat doing
  anything (Garrett 2026-10-22; `check_orthographic_views.py`). The small square wears `PipViewFrame`.

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

**Both shipped crew vessels chose C** (above: one hull, the seat a component on its root), which needs neither A's
composite nor B's paired seat. For a seat that needs a real second hull, **the code leans hard toward B.** Flight, abilities and the AI's stick all go through
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

- **A crew that lets go of a hull after it changed hands** (2026-10-23). A crew whose person sits at a seat has handed
  the hull to its AI; if the hull is then exchanged, the crew unbinds on the frame after and stops "its" autopilot -
  which is now the autopilot of whoever took the hull. `LocalCrewStation` returns the person to the helm (`TakeSeat(0)`)
  before asking for any move off their own hull. Any new path that moves a person off a hull does the same.
- **A pose relay is not a path** (2026-10-23, the Wrasse rider). Anything decided on the hull's machine from where a
  seat's body went - race rings, crystal rings, a sweep - must get the PATH the body flew, in the world the rider saw, not
  a pose sampled a few times a second and placed on the hull machine's own, later frame. A relayed pose lands late and
  off the line, and a batch that arrives at once is one step the ring guard refuses, so the ring is simply missed (the
  negative control in `RingProbeReplayTests` shows it). Send world points; replay them under the guard
  (`RingProbeReplay`).
- **Two readers of one gesture in a frame.** The swap gesture was read by `InputController` AND by each crew, so one
  press could change seat and hull at once. It is now cached per frame (`PilotSwapGesture.DirectionThisFrame`) and read
  by the person's `LocalCrewStation` only.

- **A donor's kit is not only its executors** (the Goby, 2026-10-22). Stripping a hull you cloned from another means
  four places: the executor subtrees, the hull's three binding lists (shared, touch overrides, pad overrides), its
  ability map, and its two impact containers (`VesselImpactorDataContainerSO` on the hull, the skimmer's
  `vesselSkimmerEffectsSO` / `skimmerPrismEffectsSO`). The last is the one missed: the Squirrel's skimmer kept stealing
  prisms and jousting through the Squirrel's container, and its level-5 gates fired off the NEW map's levels. Give
  the hull its own containers (the donor's minus its kit) - never disable the skimmer, which is also what collects
  crystals (`ElementalCrystalImpactor` answers only a `SkimmerImpactor`). `author_goby_assets.py` is the pattern.
- **A card's words are a contract.** `HangarAbilityKindsTests` fails any designed ability whose label and description
  contain none of `HangarAbilityKinds`' words (it would show no stage move). "Quick Jump: Time shortens the jump
  cooldown" is Passive; "... the burst of speed that flings it" is Boost. Classify every new card before shipping.
- **A pinned hull still turns.** A seat that must not let its hull cruise pins it (`SetTranslationRestricted`: the
  fleet's restricted stance), moves it only with `TranslateShip`, and turns it for its own AI with `ApplyRotation`
  while the player's input is paused. Starting the platform `AIPilot` instead flies the hull like its donor.

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
