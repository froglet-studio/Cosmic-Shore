# The Arkway — a plan for an epic adventure

*Status: proposal, September 2026. Owner: the Arkway toy (`ArkwayToy` / `ArkwayRun` /
`CellConveyor` / `Ark`). Record of what exists: `Docs/ECOSYSTEM.md §41`,
`Docs/ToySystem/ARCHITECTURE.md § Arkway`. Backlog: `Docs/ToySystem/BACKLOG.md § Arkway`.*

## Vision

You escort a living mothership through a chain of alien worlds that are already alive and
already somebody's. Every world you cross either shelters your Ark or eats it, and which one it
does is decided by the same food web that decides everything else in Cosmic Shore — no script,
no boss, no timer. The Arkway is the hypersea's first **journey**: the pull of "one more cell",
the dread of a crossing, the relief of a harbour, and a ship whose scars are the record of where
you took it. It is the first vehicle of the **Ark** fundamental and the stepping stone to
faction missions — venturing out for a reason, and coming home changed.

The experience we are aiming at is the one *Journey*, *FTL*'s jump map and a *Sea of Thieves*
voyage share: a legible destination, real stakes on the way, and drama that the player caused
rather than watched. The constraint that makes it ours is that every beat has to **emerge** from
Domain, Mass, Cells, Elementals, Flora & Fauna, Vessels, Toys, the Switch and the Ark composing.
A scripted attack is the same defect as a scripted fitness function.

## Pillars

1. **The Ark is the protagonist you protect, not the vehicle you ride.** You fly beside it.
   Its pace is the voyage's clock (the one clock a toy may own — the player opts in, sustains it
   and can end it). Its hull is ordinary conserved mass in your domain, so the world treats it
   exactly as it treats your trail.
2. **Every world is a real cell with a real claim.** A traversal cell keeps its nucleus and its
   control zone (`Cell.NucleusIsControlZone` default). Who holds the nucleus decides the colour
   the fauna waves spawn in. The core crystal is there because every cell has one.
3. **Drama is the food web, spatialised.** The nucleus is sanctuary; the exterior is grazed by
   any domain. The Ark is safe under a core and exposed on every crossing. Its wake is a line of
   food that leads a swarm to the ship. Nothing here is a threat script; it is `IsPreyForHerbivore`.
4. **The corridor is a place, not a treadmill.** Three cells stand at once and a struck cell
   takes the mass laid in it. What you did in a cell stays until the corridor recycles it, and
   the leash (three cell radii) is wide enough to range, explore and come back.
5. **Legible stakes.** The player must be able to answer, by looking: where is the Ark, how
   hurt is it, who holds this cell, when does the next wave come. Every one of those is a HUD
   surface that already exists or has a home.
6. **Bounded forever.** The voyage is infinite at fixed memory, and performance is a design
   constraint, not a QA step: the census line (`CSLogChannel.CellLifecycle`) is the toy saying
   what it holds.

## The loop and the arc

**Moment to moment.** Read the gauge — who holds this cell, how far the claim is. Decide: take
the cell (lay mass through the nucleus, which is the shipped node-control rule) or defend the
hull (hunt the swarm, lay decoy mass away from the wake, ride the wake back to the ship). Collect
the core crystal on the way through. Feel the Ark slow into the nucleus — the breath — then
watch it make way across open water at four times the speed, with the swarm behind. The next
cell blooms ahead before you reach it.

**First two minutes — the harbour.** The entrance station stands where you sailed from. The Ark
is beside you, under way. The arrow points at it only when you have lost it. The first crossing
is short and safe. The first bites land inside the first cell, close enough to see.

**First ten minutes — the rhythm.** Two or three cells. The player learns the sanctuary /
feeding-ground geometry without being told, because the hull only ever takes damage outside the
core. The first time a cell they claimed spawns a wave **in their own colour** is the payoff
that teaches the whole game: control is protection, and it is spatial.

**First hour — the journey.** Hull attrition becomes the resource. Which cells to linger in and
which to hurry through becomes a decision. A party escorts one Ark with the vessel classes doing
what they do (a Sparrow hunts the swarm, a Squirrel out-lays a nucleus fast, a Dolphin blasts a
wave off the hull). The voyage ends when the player says, or when the Ark falls — and either way
they come home.

## Roadmap

Each phase names its **goals** (measurable where possible), the **mechanics** that serve them,
the **fundamentals** they compose, and the **risks**. Nothing below adds a fundamental; two
items are flagged as needing sign-off if they grow into one.

### Phase 0 — The voyage reaches the player

The toy has failed four play tests in a row on its start sequence, and every one was a different
silent failure: the wake laid inside the arena-ready reveal watch (a hang), the Ark sailing away
at cruise behind the veil, the vessel flying kilometres during the veiled build while the Ark
stayed put, and an early exit nobody could see in the console. Nothing else on this plan matters
until a voyage opens with the Ark in view every time.

- **Goal:** five of five starts in Menu_Main open with the Ark within 200 u of the vessel and the
  arrow either pointing at it or correctly hidden; zero hangs; any failure names its stage in the
  console.
- **Root cause (found by the multi-lens investigation, `Docs/ECOSYSTEM.md` §41.3.3.3):** the
  voyage opened when the run's own arena-build BRACKET closed, ~2 s after the hull laid — but
  the load VEIL holds 30–90 s more while the traversal cells settle, and nothing pauses the pilot
  under it. Every earlier fix moved the opening, none keyed it on the veil. Behind the screen
  the Ark sailed, the pilot flew blind, the hull could be grazed to nothing, the DISEMBARK ring
  stood dead ahead of the docked pilot, and a receding Ark was "on screen" so the arrow hid.
- **Mechanics (shipped on the branch):** the voyage opens on `PrismTrailBuilder.IsLoadGateHolding`
  dropping — dock repose, entrance, arrow, banner, `_running`, `SetUnderway` all wait for it;
  one departure point (`_home`) for the corridor, the Ark and the entrance, with the entrance
  abeam on the port side; a toy pass during the build is ignored rather than toggling the
  unseen voyage off; the arrow keeps pointing at an on-screen Ark further than 900 u; the host
  revert can no longer pick a lingering traversal satellite; `LogVoyageStart` is always on;
  only the FIRST cell stands behind the veil and the second streams in beside live play
  (`CellConveyor.StandAhead`), halving the blind opening.
  Earlier: `Ark.SetUnderway` gates movement; the wake is laid `watchForReveal: false`;
  `PollArenaReady` counts progress, not change; every build exit names its stage.
- **Still to do:** a QA entry (`Docs/QA/QA_BACKLOG.md`) with the exact steps and the console lines
  to expect; measure the veil on the Lattice boot world with one cell behind it (target ≤ 20 s);
  return `LogVoyageStart` to its channel after three consecutive green tests.
- **Fundamentals:** Toys (the switch), Cells (satellite build), Vessels (repose).
- **Risk:** the veil's hard cap is 180 s; a corridor that stands two 10k-prism worlds behind it is
  slow on a laptop. Standing the second cell unveiled halves the hold.

### Phase 1 — Legible stakes

- **Goal:** a first-time tester can say, unprompted, how hurt the Ark is, who holds the cell they
  are in, and which way the Ark is — within their first two minutes.
- **Mechanics:** the Ark's hull integrity on the voyage HUD (`ArkwayVoyageHud` gains a persistent
  line: `ARK 87%` in the pilot's domain colour, blooming from the label the Ark already carries);
  the pause-button gauge already reads the nucleus claim of the cell the player is in, and the
  fauna-spawn ring already ticks the current cell — surface both in the tutorial toast the first
  time each changes; an FMOD `EventReference` on `Ark` for a hull plate lost, shipped **empty**
  per the audio convention, so the audio owner can voice it; the arrow hides when the Ark is on
  screen and shows the distance when it is not (`ObjectiveIndicator` already draws distance).
- **Fundamentals:** Domain (colour is the language), Cells (the claim), Toys (the HUD is the toy's).
- **The trap this phase must not teach:** taking a cell colours its next fauna wave but does
  **not** spare the hull. Outside the nucleus the diet is spatial and any-domain (§41.3.1), so a
  pilot's own swarm eats the Ark just as readily. Nothing in the HUD, the toasts or the wording
  may imply "claim = safety". Whether it *should* protect the Ark is open question 1 — a real
  design question, not a wording one.
- **Risk:** a HUD that says too much turns the escort into a spreadsheet. Four readouts, no more.

### Phase 2 — The fight is real

- **Goal:** a passive escort (the player flies beside the Ark and does nothing) loses the Ark in
  three to five cells; an active one keeps it indefinitely. Measured with the census line and the
  hull count at each crossing.
- **Mechanics (all tuning, no new code paths):** `arkWakeSpacing` (more prisms per unit of
  travel is the only honest way to make the wake compete with a 10,000-prism forest on a
  *count* grid); `populationScale` and `prismStride` per traversal cell; `arkSpeed` and
  `arkCruiseSpeedFactor` (the crossing is the danger — its length in seconds is the difficulty);
  the corridor authored as **LEGS** rather than a flat config list — each entry on
  `ArkwayToyDefinitionSO.cells` carrying its config *plus* `populationScale`, `prismStride` and
  `InitialFaunaReleaseTier`, with `Barren`-class **harbour legs** inserted between heavy worlds as
  release beats. Escalation and rest then live entirely in Cell data, riding the arrival profile
  as the tension curve, with no scripting and no new fundamental; `RuntimePopulationScale` rising
  with cells crossed (production gating, which the ecology permits) thickens the swarm as the
  voyage lengthens.
- **ARMOUR — a defence verb with no new code.** A pilot shields hull plates with abilities they
  already fly (the Squirrel's Heavy Trail, the Rhino, the Charge tier), and the shipped law does
  the rest: `Fauna.IsShieldedMass` is never food, and shielded mass leaves the cell's targeting
  grids. The cost is shipped too — a shield reaches 1.5× `leafSize`, so an armoured hull is a
  visibly thicker, slower ship. Nothing to build; it wants only a HUD that makes it discoverable.
- **Also:** the Ark's hull is ~150 prisms in the pilot's domain and sways the nucleus claim a
  little when it passes through — deliberate, but if it reads as self-protection the lever is a
  smaller hull, never an exemption in the books.
- **Fundamentals:** Flora & Fauna (the diet), Mass (the wake), Cells (the ladder).
- **Risk:** the density grid counts prisms, not volume, so a large sparse wake is invisible to
  it. If spacing alone cannot make the swarm come, the next honest lever is a volume-weighted
  density view — a change to the ecology's targeting, which needs the `/ecology` protocol and
  sign-off, not a weight on the Ark.

### Phase 3 — Consequence and memory

- **Goal:** two voyages are never the same, and what happened on one is visible on the next.
- **Mechanics:**
  - **A cell you took stays taken while it stands.** Already true; make it *readable* — the
    membrane of a claimed traversal cell tints toward its controller's domain (a material
    property on the Cell's own membrane, driven by `DominantDomain`, which every cell already
    computes). Cells behind you wear your colour; cells ahead wear theirs.
  - **Repair is ADOPTION, through a dry-dock switch.** A switch ring on the Ark's flank — drawn
    at its own trigger radius in neutral Blue, per the switch law — re-parents prisms the pilot
    lays through it onto the hull container (`Ark.AdoptPrism`, honouring the §41.2 mover contract:
    the adopted prism keeps its `PrismSpatialIndex` registration and its `NotifyPositionChanged`).
    The hull is then exactly the plates that exist: mass conserved, no HP pool, no clock, no
    regeneration. It composes Switch × Mass × Ark and adds no fundamental. *Note the collision to
    check first: `SelfTrailContactConfigSO` declines a pilot's own FRESH ribbon from the skimmer's
    prism branch, so adoption must not be built as a skimmer effect on newly-laid mass.*
  - **The corridor remembers, within a session.** When a traversal cell retires, snapshot
    `Cell.GetControlVolume` per `CellConfigDataSO`; when that config is stood again, re-lay the
    claim (stride-aware) before its ecology comes up. Cells become "mine" or "lost" with no score
    and no persistence. *Care: this is one step from pre-awarded control — the reason Caldera and
    Ourobor lay nothing inside the nucleus radius — so it needs the `/ecology` protocol.*
  - **The Ark carries what it crossed.** When the voyage ends and the Ark comes home, the mass
    it is carrying — the hull plates that survived — is the record. Extend this the ecology's
    way: the Ark **collects the core crystal** of each cell it passes through (a crystal is a
    pickup; the Ark is a mover with prisms; the `Crystal` impactor chain decides who may collect)
    and each collected element **regrows one hull plate** through the canonical lay path. Repair
    is then production paid for by a crystal, not a timer. *Flag: this makes the Ark a crystal
    collector, which is a new reader of the Elementals fundamental — sign-off before building.*
  - **The voyage log** as a Codex page (`Docs/CODEX.md`'s Tools kingdom already has the Arkway):
    cells crossed, plates lost, claims made — written by `ArkwayRun.End`, read by the codex.
- **Fundamentals:** Domain (the tint), Elementals (the crystal), Mass (conserved through repair),
  Toys (the codex entry).
- **Risk:** repair that is too cheap removes the stakes Phase 2 built. One plate per crystal,
  one crystal per cell, and the crossing costs more than one plate — the numbers must keep the
  attrition curve.

### Phase 4 — The party aboard

- **Goal:** two to four pilots escort one Ark and it is *better* with more of them, not merely
  louder.
- **Mechanics:** invite the party's vessels aboard — every party member's vessel is leashed to
  the same Ark (the leash is a shared constraint, which is what makes it an escort); the AI
  companions the Lifeform Matrix releases are leashed too and fly the flank; roles are the vessel
  classes, un-scripted (the Sparrow's guns kill fauna, the Squirrel's trail claims a nucleus
  fastest, the Dolphin's blast clears a wave off the hull, the Rhino's sword breaks a swarm).
  The voyage stays **machine-local** for v1 (satellites, fauna and the Ark are this machine's,
  as the Wanderway is) and the party sees each other's *vessels* flying the same corridor
  geometry because the corridor is deterministic from `seed`.
- **Fundamentals:** Vessels (roles by class), Toys (the leash), Cells (a shared corridor from one seed).
- **Risk:** a machine-local voyage means two players' Arks diverge the moment a fauna wave
  differs. Making the Ark authoritative is an RPC on the host, the same class of work as the
  Wanderway's recorded backlog item — scoped, not blocked.

### Phase 5 — Toward faction missions

- **Goal:** a voyage has a *reason* and a *consequence* beyond itself.
- **Mechanics (design, not code — needs sign-off):** a **destination** — a named far cell the
  Ark is bound for (the corridor's shuffle-bag becomes an authored route); a **return** — the Ark
  that comes home **seeds the home cell** with flora of the worlds it crossed, mass it carried
  and conserved, so the lava lamp remembers the voyage (the Cell Selector's world swap is the
  existing way a home world changes; this is the emergent one); and **faction identity** — the
  Ark's domain is the faction, and the worlds it claimed are the faction's. None of this is a
  scored mode; all of it composes Domain, Mass, Cells and the Ark.
- **Risk:** every one of these is one step from "a mission with objectives", which is a mode,
  not a toy. The line to hold: the toy never ends by itself and never keeps score.

## Anti-patterns — what we will not build

- **A threat script.** No aggro table, no "fauna target the Ark", no wave that spawns because the
  Ark arrived. If the swarm does not come, the lever is the ecology's own targeting.
- **A clock on the Ark.** No hull regeneration on a timer, no decay, no lifespan. Repair is paid
  for (a crystal); loss is an active force (a bite).
- **A score or an end condition.** A voyage ends when the player ends it or the Ark falls. A
  "distance sailed" number on screen is a score wearing a costume.
- **Waypoints everywhere.** One arrow, and only when the Ark is lost. The cells are their own
  landmarks; the wake is the map.
- **A bespoke arena.** Every traversal cell is a `Cell` with a `CellConfigDataSO`. The corridor
  never builds a parallel environment, spawner or culler.
- **Cutscenes.** The Ark slowing into a harbour *is* the cinematic. Camera work, if any, is the
  speed tunnel and the vision band doing their platform-wide jobs.

## Where this plan came from

Phases 1-5 were drafted three ways in parallel — a pacing-and-arc draft, an
emergence-discipline draft and a faction-story draft — and scored by two independent judges
against the shipped code and the locked invariants. The pacing draft ranked first (16.5),
the faction-story draft second (15.5), the emergence draft third (14). What is written above is
the pacing draft's spine with the best idea grafted from each of the others: the corridor legs
and harbour beats, dry-dock adoption, armour, "the corridor remembers", and the standing warning
that claim is not safety. The judges also caught one shared error worth recording — every draft
at some point implied that taking a cell protects the Ark, which §41.3.1 says it does not.

The run's own synthesis and final-plan stages never executed (usage limits), so this section is
the human-readable merge, not a machine-generated one.

## Open questions for the team

1. **Should control protect the Ark again?** Today the exterior of a nucleus cell is grazed by
   every domain, so taking a cell colours its swarm but does not spare the hull. The alternative
   — a colour-gated diet outside the nucleus — is a change to the shipped nucleus-cell rule and
   affects Brood Rush. The current design (sanctuary is spatial, control is colour) is coherent;
   is it the one we want?
2. **Harbour or pass-through?** The Ark slows into each nucleus and sails on. A voyage that
   *pauses* at each core until the escort chooses to leave would make "when do we go" the
   player's decision — and the crossing a commitment.
3. **How big is an Ark?** 150 prisms is a rounding error against a world and a 4.4× crystal
   worth of hull. Bigger reads better and sways control more; the collider budget is the ceiling.
4. **What comes home?** Phase 5's "the Ark seeds the home cell" is the most exciting idea here
   and the one most likely to need a fundamentals conversation.

## How we will know it is epic

Signals a play tester would report without being asked:

- "I lost the Ark and found it again by following the wake."
- "That cell's fauna were *mine* — I saw them ignore the hull and go for the forest."
- "I hurried it through that world. I wasn't going to make the same mistake twice."
- "It slowed down going into the core and I could breathe."
- "Two hull plates left. I turned back."
- "My friend took the left flank, I took the right."
- "I want to see what's in the next one."
- Nothing about loading, nothing about a missing Ark, nothing about a missing arrow.
