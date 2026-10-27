# Pilot callsigns: one per fundamental, lit when a pilot ability fires

**Status:** PROPOSED (Garrett, 2026-10-27). Nothing is built. The pilot system it sits on is Shombith's in-flight work on
`claude/pensive-dijkstra-qxoxo0` (`Docs/Pilots/PILOTS_AND_ARTIFACTS.md` and `Docs/Pilots/pilots_and_artifacts.json`
there, built on `claude/modest-sagan-0ybcgo`). This doc does not change that work. The tags in §5 are a first pass for
Shombith to accept or correct before they go into his spec.

**Read first:** `Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md` (the fundamentals list this is built from).

## 1. The idea

Every fundamental in the game gets a **callsign**, a single radio-style word. Each pilot carries the callsign of the
fundamental its pilot ability works through: one, or two at most. **Every time the pilot ability fires, that pilot's
callsigns light up**, on the hull where everyone can see them and on the pilot's card in the menus.

What this gives the game:
- **It teaches the fundamentals.** A player learns the game's rules by watching which word lights when a pilot does
  something. "Siphon lit ENSIGN" means "that just changed whose prisms those are".
- **It shows rivals what is in the seat.** You learn an enemy's pilot the first time its callsign flashes.
- **It gives squad building a vocabulary.** A squad of eight reads as a spread of callsigns: four KEELs and no ENSIGN
  says something about how that squad plays, the way a deck's colours do.
- **It shows where the pilot roster is thin.** §5's count shows which fundamentals no pilot reaches yet.

It works whichever way pilots end up being defined: as Shombith's design (a pilot rewrites one vessel ability), or as
Garrett's 2026-10-27 direction (a pilot has its own ability that doesn't touch the kit, elements or artifacts). Either
way, a pilot ability fires and its callsigns light.

## 2. The twelve callsigns

One per entry in the fundamentals list, in that list's order.

| Fundamental | Callsign | Lights when the pilot ability... |
|---|---|---|
| Domain | **ENSIGN** | changes whose something is: steals, converts or claims for a domain |
| Mass | **BALLAST** | makes mass or spends it, when that is the point of the ability (making a wall, carving a tunnel) |
| Cells | **HOLDFAST** | acts on the cell itself (membrane, nucleus, volume) or on who controls it |
| Elementals | **CATALYST** | moves petals: steals, ejects or burns them, or raises or drains a level |
| Prisms / Prismscapes | **FACET** | changes what a prism is or how prisms join: shielded, danger, tar, mine, bonded, rail shape |
| Prism movement | **AVALANCHE** | sets prisms moving (a moving prism destroys what it hits, `Docs/PRISM_MOTION.md`) |
| Flora & Fauna | **REEF** | acts on lifeforms: leads, sends, feeds, grows or plants them |
| Vessels | **KEEL** | changes how a vessel moves, who flies it, or what chases it (speed, seats, decoys, lock-ons) |
| Toys | **PINWHEEL** | uses a toy. Freestyle only, since toys have no score |
| Switch | **NEEDLE** | makes, throws or threads a ring that activates something |
| Lit | **LANTERN** | makes a force volume that lights mass: a pending, armed, lingering or reflected force |
| Ark | **GALLEON** | acts on the Ark: escorts, boards, docks or carries for it |

**How the words were picked:**
- **Each one evokes its fundamental.** You thread a needle, a holdfast is what anchors kelp to its rock, a galleon is
  a mothership carrying treasure.
- **Twelve different first letters** (A B C E F G H K L N P R), so a glyph or a radio call is never mistaken for
  another.
- **No word that already means something in the game.** It is never an element (Mass, Charge, Space, Time), a vessel,
  one of the 40 pilots or a mode, because "a word that is load-bearing elsewhere cannot quietly carry a second meaning"
  (the Spawn Matrix lesson, `DESIGN_PHILOSOPHY_EMERGENCE.md`). That is why Mass's callsign is BALLAST and not MASS. A
  few of the words appear inside code as plain nouns (`reef` in ecology comments, `facet` in mesh code, `needle` in
  geometry). None names a system.

The words are player-facing text, so the code never spells them (`CLAUDE.md`, "All player-facing text has a
human-facing control"). They are authored fields (§6), and anyone can rename one in the inspector.

## 3. Rules

1. **Two callsigns at most per pilot, primary first.** This is the arcade card's genre-petal rule
   (`Docs/HomeHub/ARCHITECTURE.md` §3.7): a pilot ability that needs three is one nobody can describe in a sentence.
2. **Every use lights it.** It lights for as long as the ability's effect lasts: a thrown sword lights KEEL and
   BALLAST until it returns. An instant ability lights for a short flash, about 1 s, authored in the callsign set.
   A secondary callsign lights more faintly than the primary.
3. **AI pilots light it too.** An AI uses the same inputs and the same ability code (the input-only rule,
   `Docs/claude/FTUE_DIALOGUE_AI.md`), so its callsigns light the same way.
4. **The callsign is declared for each pilot, then proven.** For now each pilot's callsigns are declared in the pilot
   spec. A field written by hand can disagree with the behaviour under it; the toy-category lesson,
   `DESIGN_PHILOSOPHY_EMERGENCE.md` § Toys, is why `ToyCategory` lives in code. So the second step (§6, step 5) checks
   each declaration against what the ability actually triggers in play: `PrismMotion` means AVALANCHE, a domain steal
   means ENSIGN, a petal transfer in `ResourceSystem` means CATALYST, and so on.
5. **Lighting decides nothing.** It is a picture, like Lit: nothing reads a callsign to settle a hit, a score or a
   steal.

## 4. Where it lights

- **On the hull, in the world.** Each hull carries a **callsign plate**, a marking with its pilot's one or two
  glyphs. The plate is dim normally and lights in the pilot's domain colour while the ability is live. This is
  required, not a style choice: a fact about a vessel in the world is shown in the world, in 3D, never as 2D text on
  the screen (LOCKED, `Docs/claude/SHADERS_AND_PERFORMANCE.md`). The plate is also flat-free: it is lit and faceted,
  like everything else in the world. Your own plate is visible from the chase camera, so the HUD needs nothing.
- **The ability row is not touched.** The four-icon ability row is LOCKED (`Docs/claude/VESSEL_HUD_AND_ELEMENTS.md`).
  The callsign does not add a fifth icon.
- **On the pilot's card in the menus.** The Hangar's PILOTS tab and the squad screen show a **callsign strip**: all
  twelve glyphs, the pilot's own lit and the rest dim. This is how a player learns the twelve before they fly.
- **On the radio.** Each callsign has a short call sting, one `[SerializeField] EventReference` per callsign, shipped
  **empty** (the FMOD rule, `Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md`). It is your pilot calling their sign. Because
  it is audio, it never needs a HUD element.

## 5. First pass: the 40 pilots in Shombith's spec

Tagged from each pilot's rule in `pilots_and_artifacts.json` (`claude/pensive-dijkstra-qxoxo0`, 2026-10-26). These
tags are **for Shombith to correct**. The tag is what the pilot's rewrite *adds*, not what the base ability already
did.

| Vessel | Pilot | Ability rewritten | Callsigns |
|---|---|---|---|
| Sparrow | Shrapnel | Skyburst | **BALLAST** + AVALANCHE |
| Sparrow | Flux | Pulsefire | **AVALANCHE** |
| Manta | Reaper | Sting skim | **FACET** + CATALYST |
| Manta | Astrofoil | Soar | **LANTERN** |
| Serpent | Scales | Seed Wall | **FACET** |
| Serpent | Siphon | Sniper Shot + Seed Wall | **ENSIGN** + AVALANCHE |
| Rhino | Tomahawk | Sword - special move | **BALLAST** + CATALYST |
| Rhino | Dozer | Plough | **AVALANCHE** + ENSIGN |
| Squirrel | Tenacious | Drift trail | **FACET** + CATALYST |
| Squirrel | Trace | Skimming | **ENSIGN** |
| Dolphin | Slingshot | Charge and release | **KEEL** |
| Dolphin | Riptide | Trail | **FACET** |
| Urchin | Bushy | Track Projector | **FACET** |
| Urchin | Heist | Chain Spikes | **ENSIGN** |
| Butterfly | Maestro | Piano keys | **BALLAST** |
| Butterfly | Damsel | Dust mode | **LANTERN** |
| Thresher | Krush | Wrecking ball | **KEEL** |
| Thresher | Trapper | Plant | **FACET** + KEEL |
| Scarab | Mason | Switch - the wall | **NEEDLE** + BALLAST |
| Scarab | Echo | Ball forge | **BALLAST** + AVALANCHE |
| Stoat | Strider | Pathfinder | **KEEL** |
| Stoat | Comet | Time mode | **KEEL** + BALLAST |
| Goby | Tender | Dens | **BALLAST** |
| Goby | Snap | Vortex rings | **BALLAST** + KEEL |
| BrittleStar | Jugger | Ring throw - special move | **NEEDLE** |
| BrittleStar | Nul | Speed mode | **FACET** |
| Shrike | Timebomb | Void Lance | **BALLAST** |
| Shrike | Ironclad | Nests | **BALLAST** + REEF |
| Angler | Glimmer | Paint | **LANTERN** + REEF |
| Angler | Pack | Lock-on | **REEF** + KEEL |
| Wrasse | Feist | Afterburner | **KEEL** |
| Wrasse | Lariat | Kite tail | **ENSIGN** |
| Orca | Longshift | Gunner outrigger | **KEEL** |
| Orca | Pod | Gunner missiles | **KEEL** |
| Starling | Murmur | The swarm | **KEEL** |
| Starling | Roost | Between objectives | **FACET** |
| Jacks | Pincer | Herding | **REEF** |
| Jacks | Shoal | Herding | **REEF** + BALLAST |
| Honeyguide | Comb | The Dynamo | **CATALYST** |
| Honeyguide | Raid | The Slip | **ENSIGN** |

### What the count shows

| Callsign | Pilots (any) | As primary |
|---|---|---|
| BALLAST | 11 | 8 |
| KEEL | 11 | 8 |
| FACET | 8 | 8 |
| ENSIGN | 6 | 5 |
| AVALANCHE | 5 | 2 |
| REEF | 5 | 3 |
| CATALYST | 4 | 1 |
| LANTERN | 3 | 3 |
| NEEDLE | 2 | 2 |
| **HOLDFAST** | **0** | 0 |
| **PINWHEEL** | **0** | 0 |
| **GALLEON** | **0** | 0 |

- **No pilot reaches Cells, the Ark or Toys.** Those are the open lanes. HOLDFAST and GALLEON are exactly where the
  voyage pilots belong: escorting the Ark, holding a cell while the payload moves, and extraction. That makes them the
  natural first pilots for the squad-as-deck voyage work.
- **BALLAST and KEEL are crowded.** One pilot in four makes or breaks mass as its point, or changes how a hull moves.
  A squad screen that shows the strip will make that obvious. Spreading the next pilots across the empty lanes keeps
  squads distinct.

### Found while tagging (for Shombith)

Three pilots **regrow prisms**: Mason (bricks regrow from their neighbours), Tender (a den regrows any prism taken
from it) and Ironclad (a nest regrows its super-shielded blocks). Regrowth makes mass, which is why each lights
BALLAST. *Mass is conserved*, and lifeform reproduction and spawning are the only sources
(`DESIGN_PHILOSOPHY_EMERGENCE.md` § Mass and § Elementals). So each regrow has to draw on mass that already exists
(the den's own stock, the nest's flora) or be ruled an authorized exception, as the Angler's keeper was. Worth
settling before those three are built.

## 6. How to build it (once the pilot system lands)

Build on Shombith's types (`PilotSO`, `PilotArtifactCatalogSO`, `VesselPilotLoadout`, `author_pilot_assets.py`), never
beside them:

1. **`PilotCallsign` enum** with fixed values: `None = 0`, then ENSIGN 1 … GALLEON 12, in §2's order. These are fixed
   numbers so Unity's serialization never drifts. Claim the numbers only after checking `bleeding-edge` and the
   in-flight branches.
2. **The spec gets a `callsigns` array per pilot** in `pilots_and_artifacts.json`, at most two entries, primary first.
   The generator writes it onto `PilotSO`, and its `--self-test` rejects a pilot with none, more than two, or an
   unknown word.
3. **`CallsignSetSO`** (in `Resources/Pilots/`, next to the catalog). One row per callsign: the word, a one-line
   meaning, the glyph sprite (from `/ability-icon`'s icon kit, white and shape-only like the ability icons), the plate
   glyph, the call sting `EventReference` (empty), and the flash length. Every text field is authored. An empty field
   shows nothing.
4. **The light is raised where the ability fires.** The pilot's ability code calls `PilotCallsigns.Light(status)`
   when it fires, and `Release` when its effect ends. The pilot abilities already run on every peer from the
   replicated seat (Tomahawk's throw does), so each peer lights the plate locally and **no new network message is
   needed**. The plate lights through a `MaterialPropertyBlock` on the shared material.
5. **The proof (later).** A play-mode test runs each built pilot's ability once and records which choke points it
   passed through: `PrismMotion`, prism create and destroy, domain swap, `ResourceSystem` petal moves, the Lit bank,
   fauna leash, switch threading. The test fails when that set disagrees with the declared callsigns, so a declaration
   can't drift from the behaviour.

The plate's place on each hull is a `/vessel-model` question: either a glyph on the Body slot or a small plate mesh.
Ask before adding a fifth material slot.

## 7. Open decisions

| Decision | Who decides |
|---|---|
| Accept the twelve words, or rename some | Garrett and Shombith |
| Correct the 40 tags in §5 and fold them into the pilot spec | Shombith (the pilot spec is his) |
| What a "pilot ability" is: a rewrite of one vessel ability (Shombith, 2026-10-26) or a pilot-only ability that leaves the kit alone (Garrett, 2026-10-27). The callsigns work with either | Garrett and Shombith, together |
| Whether the payload or artifacts become a thirteenth fundamental, and so get a thirteenth callsign | The fundamentals-curation process (`DESIGN_PHILOSOPHY_EMERGENCE.md`) |
| Where the plate sits on each hull | `/vessel-model` |
| How the three regrowth pilots keep mass conserved (§5) | Shombith |
