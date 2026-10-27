# Pilot callsigns: the pilot's VISION, on the left bumper

**Status:** PROPOSED (Garrett, 2026-10-27, rev 2). Nothing is built. Rev 1 (earlier the same day) had callsigns light
when a pilot's ability fired; Garrett replaced that with this: **the pilot grants a vision power**. Rev 1's tagging of
the 40 pilots is withdrawn.

The pilot system this sits on is Shombith's in-flight work on `claude/pensive-dijkstra-qxoxo0` (`Docs/Pilots/
PILOTS_AND_ARTIFACTS.md` and `pilots_and_artifacts.json` there). There, a pilot rewrites one vessel ability. This
proposal does not change that work; §7 lists what the two need to settle.

**Read first:** `Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md` (the fundamentals list) and the Dolphin's Echo Sight
(`EchoSightActionExecutor`, `EchoSightVesselHighlighter`): the game's existing "touches nothing but photons" ability,
which this reuses.

## 1. The idea

**Hold the left bumper on any vessel and you see what your pilot sees.** One kind of thing in the world lights up and
**everything else is suppressed**: dimmed and drained of colour, still there, still solid, just quiet.

- **Each pilot's sight is one callsign.** A predator-spotter, a crystal-finder, a bandit-hunter.
- **The pilot's power is knowing, not doing.** The kit does things, elements set how strongly, and artifacts and the
  payload are what you fight over. The pilot decides where to look.
- **Seeing one thing costs you everything else.** While LB is held you fly half-blind to the rest of the world, and
  that is the price.
- **A squad is a set of eyes.** Eight pilots cover eight of the fourteen sights. Building the squad means choosing what
  it can find and what it will never see coming. With the D-pad hull swap (`PilotSwap`), changing hulls also changes
  whose eyes you look through.

## 2. The fourteen callsigns

Built from the fundamentals list. **Lit, Toys, Prism movement and Cells have no callsign** (Garrett, 2026-10-27).
Elementals splits into one sight per crystal, and Flora & Fauna and Vessels each split in two.

| Fundamental | Callsign | Holding LB lights | Where the game already knows it |
|---|---|---|---|
| Domain | **ENSIGN** | your domain's mass: what is yours to defend, and the gaps in it (§7: or the rivals' mass instead) | each prism material's `_PrismLitDomain` float (`Docs/LIT.md`) |
| Mass | **MANGER** | the mass cluster the fauna are heading for | `Cell.GetDensestRegionAnyDomain`, `BlockDensityGrid` (the herbivores' goal; shielded mass is never in it) |
| Elementals: Mass crystal | **ANVIL** | every Mass crystal | `Element.Mass` (`Assets/_Scripts/Data/Enums/Element.cs`) |
| Elementals: Charge crystal | **VOLT** | every Charge crystal | `Element.Charge` |
| Elementals: Space crystal | **ZODIAC** | every Space crystal | `Element.Space` |
| Elementals: Time crystal | **SUNDIAL** | every Time crystal | `Element.Time` |
| Elementals: Omni crystal | **OPAL** | every Omni crystal | `Element.Omni` |
| Prisms | **FACET** | prisms that aren't plain: shielded, super-shielded, danger. Super-shielded mass is what a payload is for | prism state (`PrismStateManager`) |
| Flora & Fauna: predators | **TALON** | every predator | `FaunaDiet.Predator` |
| Flora & Fauna: herbivores | **DOE** | every herbivore | `FaunaDiet.Herbivore` |
| Vessels: allies | **WINGMAN** | every vessel on your side, through mass | the vessel list, by domain |
| Vessels: opponents | **BANDIT** | every opposing vessel, through mass | the vessel list, by domain |
| Switch | **NEEDLE** | every ring that activates something: gates, Scarab switches, goals | `ScarabSwitch`, `RingThreadProbe`, `AstroLeagueGoal` |
| Ark | **GALLEON** | the Arks, and the payload aboard | `Ark` |

A crystal sight includes **every crystal of that element**: field crystals, ejected crystals (`EjectedCrystal`), and
the hearts lifeforms carry (`LifeFormCrystal`). So VOLT also shows which creatures are carrying Charge.

Flora has no sight in this list. A plant sight is the obvious fifteenth if someone wants one.

**How the words were picked.**
- **Each one evokes what it sees.** A manger is where the animals go to eat, a sundial reads time, and bandit and
  wingman are what pilots call hostiles and friendlies on the radio.
- **Fourteen different first letters** (A B D E F G M N O S T V W Z), so a glyph or a call is never mistaken for
  another.
- **No word that already means something in the game:** no element, vessel, pilot or mode name, and none of the
  ecology's own words. Larder (the thief's hoard), Herd and Pasture (the Jacks' herding), Grazer (a Substrate
  species), Meadow (a Wanderway recipe) and Zenith (in the random player-name list) were all rejected for that reason.
  WINGMAN already appears in the Spawn Matrix for an AI ally released in your domain, which is the same meaning.

The words are player-facing text, so they are authored fields and never C# literals (`CLAUDE.md`, "All player-facing
text has a human-facing control").

## 3. How it looks

Echo Sight already solved most of this. The tools are reused, not rebuilt:

- **Lit mass (ENSIGN, MANGER, FACET).** Prisms are lit and dimmed by the prism shader from global uniforms, as `PrismLit`
  does. That costs nothing per prism on the CPU and adds no colliders. ENSIGN is a comparison against the domain
  float every prism material already carries. MANGER is a sphere volume around the cluster. FACET is a per-material
  match on prism state.
- **Lit things (crystals, fauna, vessels, rings, Arks).** These use Echo Sight's two-part mark
  (`EchoSightVesselHighlighter`): the thing is driven to its saturated colour, plus an additive halo ring drawn
  `ZTest Always` (`EchoSightHalo.shader`) so it reads through mass. Fauna and crystals can number in the hundreds, so
  their marks must be instanced or shader-side, not one quad each. Measure before choosing (Performance Standards,
  `Docs/claude/SHADERS_AND_PERFORMANCE.md`).
- **Everything else is suppressed.** Unlit prisms dim through the same uniforms. The rest of the world (nebula,
  membrane, flora, hulls not seen) dims and desaturates through a post-processing volume whose weight follows LB.
- **Colour.**
  - Domain things (vessels, mass) light in their domain colour.
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

- **Pad:** hold LB. Today LB alone does nothing on a vessel.
- **The conflict: LB + RB is the look-back view** (`RearViewGesture`, a rising edge on both shoulders). With LB as sight
  and RB as the payload, pressing both would open the sight, grab the payload and flip the camera at once. Two
  options:
  - **(a) Move the look-back view off the shoulders on the pad. Recommended.** C stays on the keyboard.
  - **(b) Keep the chord, and have the sight and the payload ignore a press that becomes the chord within about
    0.1 s.** That delays both on every press.

  Garrett merged the look-back view last (#1404), so it's his call.
- **Keyboard and phone:** need a binding and a touch gesture. Phones have per-hull touch profiles
  (`Docs/claude/ARCHITECTURE_CORE.md`, Input Strategy).

## 6. How to build it (on Shombith's pilot types, once they land)

1. **`PilotVision` enum** with fixed values: `None = 0`, then ENSIGN 1 … GALLEON 14 in §2's order. These are fixed
   numbers so Unity's serialization never drifts. Claim the numbers only after checking `bleeding-edge` and the
   in-flight branches.
2. **`PilotSO` gets one `vision` field**, written by `author_pilot_assets.py` from the spec.
3. **`CallsignSetSO`** (in `Resources/Pilots/`): per callsign, the word, a one-line meaning, the glyph (from
   `/ability-icon`'s kit), the plate glyph, the open sting (empty), and the fade time.
4. **A `PilotVision` component on every vessel.** It reads LB for the local pilot, publishes the sight's uniforms, runs
   the suppression volume, and lights the hull plate (on every peer, from one replicated "looking" bit). Each sight is
   a small provider, so a fifteenth sight is one class plus its row:
   - **prism predicate:** ENSIGN, FACET
   - **volume:** MANGER
   - **target set:** crystals by element, fauna by diet, vessels by side, switches, Arks
5. **Tests:**
   - every callsign has a provider and an authored row;
   - no word collides with an element, vessel, pilot or mode name;
   - the sight writes nothing but rendering state, checked the way Echo Sight's tests check it.
6. **`/verify-unity`** before any C# commit (LOCKED).

## 7. Open decisions

| Decision | Who decides |
|---|---|
| Accept the fourteen words, or rename some | Garrett and Shombith |
| ENSIGN shows your own mass, or the rivals' | Garrett |
| What a pilot is: Shombith's ability rewrite (2026-10-26), this vision power (Garrett, 2026-10-27), or both (each pilot rewrites one ability AND carries one sight) | Garrett and Shombith, together |
| LB + RB look-back conflict (§5) | Garrett |
| A plant sight, and whether artifacts get a sight once they are a fundamental | Garrett, through the fundamentals-curation process |
| BANDIT on the Dolphin: its Charge level-5 upgrade already marks vessels inside the Echo Sight cone. BANDIT shows them all, everywhere, which may make that upgrade redundant on that hull | whoever owns the Dolphin's element map |
| Where the plate sits on each hull | `/vessel-model` |

Found earlier while reading Shombith's roster, still open for him: three of his pilots (Mason, Tender, Ironclad)
**regrow prisms**, and *Mass is conserved*, so each needs an answer for where that mass comes from.
