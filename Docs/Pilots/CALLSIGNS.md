# Pilots: the pilot grants vision (callsigns, on the left bumper)

**Status: THE PILOT CONCEPT** (Garrett, 2026-10-27: "take over this as the concept for pilots"). Design only: no
vision code exists yet. The one piece of code it needed first has landed: the look-back camera moved off the shoulders
to D-pad down (§5).

**What this replaces:**
- `Docs/PILOTS_AND_ARTIFACTS.md` §2 (2026-10-12), where a pilot swaps part of a vessel's kit. That section now points
  here.
- The ability-rewrite pilots on Shombith's in-flight branch `claude/pensive-dijkstra-qxoxo0` (2026-10-26: 40 pilots,
  each rewriting one vessel ability, with Tomahawk built). **That branch is not touched.** What becomes of its roster,
  its portraits, its Hangar tabs and the Tomahawk throw is for Garrett and Shombith to settle together (§7). Its
  types (`PilotSO`, the catalog, the saved squads, `VesselPilotLoadout`) are what this is built on.

**Read first:** `Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md` (the fundamentals list) and the Dolphin's Echo Sight
(`EchoSightActionExecutor`, `EchoSightVesselHighlighter`): the game's existing "touches nothing but photons" ability,
which this reuses.

## 1. The concept

**Hold the left bumper on any vessel and you see what your pilot sees.** One kind of thing in the world lights up and
**everything else is suppressed**: dimmed and drained of colour, still there and still solid, just quiet.

- **A pilot is a pair of eyes.** Each pilot carries one callsign, and the callsign is what they see.
- **A squad is a 12-card deck** (Garrett, 2026-10-27): three vessels, each with four cards (vessel, element, pilot,
  artifact). The four parts of a slot each do one job:

  | Part | What it decides |
  |---|---|
  | Kit | what the vessel does |
  | Elements | how strongly it does it |
  | Artifacts and the payload | what everyone fights over ([`ARTIFACTS.md`](ARTIFACTS.md)) |
  | Pilot | where to look |

- **Seeing one thing costs you everything else.** While LB is held you fly half-blind to the rest of the world. That
  is the price, and it is the only one.
- **A squad is a set of eyes.** Its three pilot cards cover three of the seventeen sights. Building the deck means
  choosing what the squad can find and what it will never see coming. With the D-pad hull swap
  (`PilotSwap`), changing hulls also changes whose eyes you look through.

## 2. The seventeen callsigns

Built from the fundamentals list, in that list's order. **Lit, Toys, Prism movement and Cells have no callsign**
(Garrett, 2026-10-27). The splits:
- Elementals is one sight per crystal.
- Prisms is one sight per prism state.
- Flora & Fauna is plants, predators and herbivores.
- Vessels is allies and opponents.

| # | Fundamental | Callsign | Holding LB lights | Where the game already knows it |
|---|---|---|---|---|
| 1 | Domain | **ENSIGN** | your domain's mass: what is yours to defend, and the gaps in it (§7: or the rivals' mass instead) | each prism material's `_PrismLitDomain` float (`Docs/LIT.md`) |
| 2 | Mass | **MANGER** | the mass cluster the fauna are heading for | `Cell.GetDensestRegionAnyDomain`, `BlockDensityGrid` (the herbivores' goal; shielded mass is never in it) |
| 3 | Elementals: Mass crystal | **ANVIL** | every Mass crystal | `Element.Mass` (`Assets/_Scripts/Data/Enums/Element.cs`) |
| 4 | Elementals: Charge crystal | **VOLT** | every Charge crystal | `Element.Charge` |
| 5 | Elementals: Space crystal | **ZODIAC** | every Space crystal | `Element.Space` |
| 6 | Elementals: Time crystal | **SUNDIAL** | every Time crystal | `Element.Time` |
| 7 | Elementals: Omni crystal | **OPAL** | every Omni crystal | `Element.Omni` |
| 8 | Prisms: shielded | **RAMPART** | every shielded prism | `prismProperties.IsShielded` |
| 9 | Prisms: super-shielded | **CITADEL** | every super-shielded prism: what a payload is for | `prismProperties.IsSuperShielded` |
| 10 | Prisms: danger | **HORNET** | every danger prism: the mass that burns your petals | `prismProperties.IsDangerous` |
| 11 | Flora & Fauna: plants | **IVY** | every plant | `Flora` |
| 12 | Flora & Fauna: predators | **TALON** | every predator | `FaunaDiet.Predator` |
| 13 | Flora & Fauna: herbivores | **DOE** | every herbivore | `FaunaDiet.Herbivore` |
| 14 | Vessels: allies | **WINGMAN** | every vessel on your side, through mass | the vessel list, by domain |
| 15 | Vessels: opponents | **BANDIT** | every opposing vessel, through mass | the vessel list, by domain |
| 16 | Switch | **NEEDLE** | every ring that activates something: gates, Scarab switches, goals | `ScarabSwitch`, `RingThreadProbe`, `AstroLeagueGoal` |
| 17 | Ark | **GALLEON** | the Arks, and the payload aboard | `Ark` |

A crystal sight includes **every crystal of that element**: field crystals, ejected crystals (`EjectedCrystal`), and
the hearts lifeforms carry (`LifeFormCrystal`). So VOLT also shows which creatures and plants are carrying Charge.

**How the words were picked.**
- **Each one evokes what it sees.** A manger is where the animals go to eat, a citadel is the fortress that does not
  fall, a hornet stings whoever touches it, and bandit and wingman are what pilots call hostiles and friendlies on the
  radio.
- **Seventeen different first letters** (A B C D E G H I M N O R S T V W Z), so a glyph or a call is never mistaken for
  another.
- **No word that already means something in the game:** no element, vessel, pilot or mode name, and none of the
  ecology's own words. These were rejected for that reason:

  | Rejected | Already used for |
  |---|---|
  | Larder | the thief's hoard |
  | Herd, Pasture | the Jacks' herding |
  | Grazer | a Substrate species |
  | Meadow | a Wanderway recipe |
  | Fortress | a builder species |
  | Keep | the Angler's keeper |
  | Zenith | the random player-name list |

  WINGMAN already appears in the Spawn Matrix for an AI ally released in your domain, which is the same meaning.
  Hornet appears once as a description of the Serpent's head, which names no system.

The words are player-facing text, so they are authored fields and never C# literals (`CLAUDE.md`, "All player-facing
text has a human-facing control").

## 3. How it looks

Echo Sight already solved most of this. The tools are reused, not rebuilt:

- **Lit mass (ENSIGN, MANGER, RAMPART, CITADEL, HORNET).** Prisms are lit and dimmed by the prism shader from global
  uniforms, as `PrismLit` does. That costs nothing per prism on the CPU and adds no colliders.
  - ENSIGN is a comparison against the domain float every prism material already carries.
  - MANGER is a sphere volume around the cluster.
  - The three prism-state sights match the prism's state on its material: shielded, super-shielded and danger prisms
    already wear their own materials (`SO_MaterialSet`: `ShieldedBlockMaterial`, `SuperShieldedBlockMaterial`,
    `DangerousBlockMaterial` and their transparent twins). Each gets one per-material float, stamped where
    `ThemeManager.PaintPrismTier` already stamps `_PrismLitDomain`.
- **Lit things (crystals, plants, fauna, vessels, rings, Arks).** These use Echo Sight's two-part mark
  (`EchoSightVesselHighlighter`): the thing is driven to its saturated colour, plus an additive halo ring drawn
  `ZTest Always` (`EchoSightHalo.shader`) so it reads through mass. Plants, fauna and crystals can number in the
  hundreds, so their marks must be instanced or shader-side, not one quad each. Measure before choosing (Performance
  Standards, `Docs/claude/SHADERS_AND_PERFORMANCE.md`).
- **Everything else is suppressed.** Unlit prisms dim through the same uniforms. The rest of the world (nebula,
  membrane, the lifeforms and hulls not seen) dims and desaturates through a post-processing volume whose weight
  follows LB.
- **Colour.**
  - Domain things (vessels, mass, lifeforms in their cell's colour) light in their domain colour.
  - Crystals light **white**, because elements have no colour (LOCKED). The crystal's own shape says which element it
    is.
- **Continuity.** Pressing LB fades the sight up over a short time and releasing it fades back. Nothing pops.

All of this happens **in the world, in 3D**, which is what the LOCKED rule asks for: a world fact is shown in the world,
never as 2D text or a ring on the screen.

## 4. Rules

1. **It touches nothing but photons** (Echo Sight's rule). No outcome reads it. It moves no camera, changes no speed,
   protects nothing, scores nothing. That keeps it outside *Elementals governs all buffing*: information is not a
   buff to a vessel parameter.
2. **What you see is yours.** The sight is local: no other player's screen dims because you held LB.
   - **Others see that you are looking.** Your hull's callsign plate (a glyph marking, `/vessel-model` decides where)
     lights in your domain colour while you hold LB, so rivals learn your pilot and when you are scouting.
   - **It never takes slots in Lit's shared peer bank.** Lit learned this the hard way: a held, personal light must not
     compete with the lights that carry information for everyone. The sight gets its own uniforms.
   - **Its names are new.** Any new global shader array gets a name Unity has never been asked to bind (`Docs/LIT.md`,
     the bank-rename lesson).
3. **One callsign per pilot.** A pilot is one pair of eyes.
4. **AI.** An AI pilot does not need eyes, since it reads the world's data directly. Optionally, its doctrine leans on
   its sight: a TALON pilot's AI steers clear of predators, a MANGER pilot's AI goes where the fauna go. That gives the
   pilot an identity when you are not in the seat.
5. **Sound.** Each callsign has one `[SerializeField] EventReference` for the moment the sight opens, shipped empty
   (the FMOD rule).

## 5. Controls

| Pad | Does | Status |
|---|---|---|
| **LB** (hold) | the pilot's sight | proposed; LB is unbound on a vessel today |
| **RB** | grab and activate the payload | proposed; RB is free on every vessel since 2026-10-27 |
| **D-pad down** | look-back camera | **moved here 2026-10-27** (`RearViewGesture`, was LB + RB together) |
| D-pad left / right | swap hulls (`PilotSwap`) | shipped |
| **Y** | the three abilities that were on RB | **moved here 2026-10-27** |

- **The look-back camera is now D-pad down.** It moved because LB + RB together would have opened the sight, grabbed
  the payload and flipped the camera at once. C stays on the keyboard. D-pad down was free in flight. The theater's
  D-pad transport (`TheaterGesture`) is read only while the local pilot's input is paused, which is exactly when the
  look-back gesture is not polled.
- **Every RB ability moved to Y** (Garrett, 2026-10-27), so RB is free on every vessel. Measured that day, RB raised
  `FlipAction` (the name is legacy) and three vessels bound an ability to it. All three now sit on `Button4Action`
  (pad Y, keyboard F), which none of them used:
  - Brittle Star: Charge Mode (Charge)
  - Grizzly: Weapon Systems (Mass)
  - Shrike: Targeting Array (Charge)

  RB also writes `Throttle`, but that is read only when `toggleManualThrottle` is on, and no shipped prefab turns it
  on, so RB was never boost. The keyboard's E (the old `FlipAction` key) is free for the payload too.
- **Keyboard and phone** need bindings and a touch gesture. Phones have per-hull touch profiles
  (`Docs/claude/ARCHITECTURE_CORE.md`, Input Strategy).

## 6. How to build it (on the pilot types from `claude/pensive-dijkstra-qxoxo0`, once they land)

The session that builds it starts from `Docs/prompts/PILOTS_ARTIFACTS_PAYLOAD_BUILD_PROMPT.md`.

1. **`PilotVision` enum** with fixed values: `None = 0`, then ENSIGN 1 … GALLEON 17, the # column of §2. These are
   fixed numbers so Unity's serialization never drifts. Claim the numbers only after checking `bleeding-edge` and the
   in-flight branches.
2. **`PilotSO` gets one `vision` field**, written by `author_pilot_assets.py` from the spec.
3. **`CallsignSetSO`** (in `Resources/Pilots/`): per callsign, the word, a one-line meaning, the glyph (from
   `/ability-icon`'s kit), the plate glyph, the open sting (empty), and the fade time.
4. **A `PilotVision` component on every vessel.** It reads LB for the local pilot, publishes the sight's uniforms, runs
   the suppression volume, and lights the hull plate (on every peer, from one replicated "looking" bit). Each sight is
   a small provider, so an eighteenth sight is one class plus its row:
   - **prism predicate:** ENSIGN, RAMPART, CITADEL, HORNET
   - **volume:** MANGER
   - **target set:** crystals by element, plants, fauna by diet, vessels by side, switches, Arks
5. **Tests:**
   - every callsign has a provider and an authored row;
   - no word collides with an element, vessel, pilot or mode name;
   - the sight writes nothing but rendering state, checked the way Echo Sight's tests check it.
6. **`/verify-unity`** before any C# commit (LOCKED).

## 7. Open decisions

| Decision | Who decides |
|---|---|
| What becomes of the 40 ability-rewrite pilots, their portraits, the Hangar PILOTS / ARTIFACTS tabs and the Tomahawk throw on `claude/pensive-dijkstra-qxoxo0` | Garrett and Shombith, together |
| Which pilot carries which of the seventeen sights | the pilot roster's author |
| ENSIGN shows your own mass, or the rivals' | Garrett |
| Whether artifacts get a sight once they are a fundamental | Garrett, through the fundamentals-curation process |
| BANDIT on the Dolphin: its Charge level-5 upgrade already marks vessels inside the Echo Sight cone. BANDIT shows them all, everywhere, which may make that upgrade redundant on that hull | whoever owns the Dolphin's element map |
| Where the plate sits on each hull | `/vessel-model` |
