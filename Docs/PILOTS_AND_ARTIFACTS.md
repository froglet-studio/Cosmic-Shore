# Pilots & Artifacts — the other half of a squad mate's pre-match kit

**Status:** DESIGN (milestone 1 scoping). Captured from a design conversation between Garrett and
Shombit on 2026-10-12, dictated live and written up here. The sections marked **DECIDED** record
what that conversation settled; the sections marked **PROPOSED** are this write-up's own
suggestions for how to land it and are for Garrett and Shombit to accept, strike or amend.
**Implementation is under way:** slices 1 and 2 of milestone 1 (the data foundation and the
authored default pilots and keywords) have landed and are not yet editor-verified. §10 has the slice plan, what was measured before building, and what the
code does differently from the sketch in §7.

**2026-10-26 (Shombith): the content and three of its rules moved on.** The roster is now
`Docs/Pilots/PILOTS_AND_ARTIFACTS.md` (rev 2, with its spec `Docs/Pilots/pilots_and_artifacts.json`): 40 pilots, two
per Hangar vessel besides the default, and 45 artifacts. Decided there, and they win over this document where they
differ: **pilots are not tied to the elements** (every authored `ElementalBoost` is none; the field stays as a lever);
**a vessel has three artifact slots** and an artifact is found at **rank I, II or III**; the eight spare portraits stand
in only until the artist paints those pilots. The slice-1 and slice-2 code below is the system it is built on
(§10.1 row 2b).

**Read first:** `Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md` (the fundamentals, *Elementals
governs ALL buffing and debuffing*), `Docs/ElementalAbilitySystem/ARCHITECTURE.md` (the element →
ability → level-5 upgrade contract), `Docs/ELEMENTAL_ECONOMY.md` (petals, the pot, the empty start).

---

> **SUPERSEDED FOR PILOTS (Garrett, 2026-10-27).** The pilot concept is now
> [`Docs/Pilots/CALLSIGNS.md`](Pilots/CALLSIGNS.md): **a pilot grants a vision power.** Hold the left bumper and the
> one thing your pilot sees (one of eighteen callsigns built from the fundamentals) lights up while everything else
> is suppressed. A pilot no longer swaps any part of a vessel's kit, so §2 below (and every "pilot as kit side-grade"
> line in §0, §4 and §6) is history, kept for the record.
>
> **SUPERSEDED FOR ARTIFACTS TOO (Garrett, 2026-10-27).** The artifact concept is now
> [`Docs/Pilots/ARTIFACTS.md`](Pilots/ARTIFACTS.md): **an artifact transforms the payload**, a parcel of the Ark's
> hull that any vessel grabs and fires to crack super-shielded mass. An artifact no longer turns a "fifth knob", so §3
> is history too. The squad-as-a-deck frame (§1), the compatibility keywords (§4) and the data foundation built from
> §7 still stand.

## 0. The one-paragraph version

Every squad mate brings **four choices** into a match: a **vessel**, an **element**, a **pilot**
and an **artifact**. A squad is three squad mates — you plus two friends, two AI, or any mix — so
a saved squad is twelve choices, and it reads and plays like a **saved deck in a deck builder**:
you build several, you pick one before a game. Elements are the fleet's universal four knobs
(quantitative everywhere, qualitative at level 5). **Pilots are the qualitative half of that
picture turned inside out** — a pilot swaps part of a vessel's kit for a different kit, a
side-grade, with elemental boosts as the balancing lever. **Artifacts are a genuine upgrade**: a
quantitative change to a parameter the four elements do *not* reach, found in the HyperSea,
optional to carry, and **at risk** when you carry one. Both need **compatibility keywords**
because, unlike elements, they reach into vessel-specific kit.

---

## 1. The squad as a deck (DECIDED)

| Per squad mate | Today | After this design |
|---|---|---|
| Vessel | chosen | chosen |
| Element | chosen (the primary element you start levelled in) | chosen |
| Pilot | — (the vessel flies its "base kit") | chosen; **a vessel always has a pilot** |
| Artifact | — | optional; **not every vessel carries one** |

- A squad is **three squad mates**: the player plus two friends, two AI, or a mix. Each slot
  carries all four choices.
- Saving a squad configuration is saving a deck. Before a game the player picks **which squad to
  play**, the way a deck builder asks which deck. This is the player-facing frame for the whole
  feature and it should be the frame of the UI.
- The existing persisted `Squad` (`Assets/_Scripts/System/Squads/Squad.cs`, cloud mirror
  `SquadCloudData`, key `SQUAD_DATA`) already holds **one** squad of three slots with
  `(VesselClassType, Element)` per slot. This design grows each slot to `(vessel, element, pilot,
  artifact)` and grows the system from one squad to **several named squads with an active one**.
  Where a slot's pilot is unset, it resolves to the vessel's **default pilot** (§2.3).
  *Measured 2026-10-13 (§10.2): that one-squad record was dormant. Nothing read it at match
  start and nothing ever wrote `SQUAD_DATA`. Slice 1 reshaped `SQUAD_DATA` into the squad book.*

## 2. Pilots (SUPERSEDED 2026-10-27 by `Docs/Pilots/CALLSIGNS.md`)

### 2.1 The state of the art this is built against: elements

Restated so the contrast in 2.2 is exact (authority: `ElementalAbilitySystem/ARCHITECTURE.md`,
`ELEMENTAL_ECONOMY.md`, and `python3 Tools/Build/element_ability_table.py` for the live table):

- Each of the four elements owns **one ability** on each vessel and scales **one parameter** of
  it, continuously, across the whole range. **Quantitative everywhere.**
- Levels run **−5 … 15**. The base band is 0 … 10. Above 10 is overcharge and drains back down to
  10; below 0 is deficit and recovers up to 0. Both outer bands are reachable by transients only.
- At **level 5** the ability gains a discrete new beneficial characteristic — the level-5 upgrade.
  **One qualitative change per element, unlocked at 5.**
- A **fully upgraded** vessel in an element lets the player **start the match at level 5** in that
  element when they select it. Even at max upgrade there are still **ten levels to gain in-game**
  before the overcharge drain starts taking it back. (Note on current wiring: the only shipped
  mechanism that resembles "start at level N" is the legacy `Captain.Level`, which writes
  `0.1 × Level` into the captain's primary element — see §5.1. Nothing seeds a pilot's levels at
  spawn in the arcade modes today; `ELEMENTAL_ECONOMY.md` §2.1 records that the pot starts empty
  by design. This design assumes the "start at level 5" on-ramp exists or is built; it is the
  seam a pilot's elemental boost plugs into.)

### 2.2 What a pilot is

**A pilot is mostly the qualitative change; the quantitative changes stay in the elements.** The
mirror image of an element:

| | Element | Pilot |
|---|---|---|
| Quantitative | across the whole range | **none by default** (see the balance lever below) |
| Qualitative | one, unlocked at level 5 | **the point of the pilot** — always on |
| Scope | the fleet's universal interface; every vessel has exactly four | reaches into a vessel's **specific kit**; compatibility is per vessel (§4) |
| Direction | upgrade (more is better) | **side-grade** (different, not better) |

- A pilot's qualitative ability **typically makes the vessel better in some way** — it swaps out
  one thing for something better *at that thing*. But **one pilot against another is a
  side-grade**: you give up the kit you had to get the kit the pilot brings.
- **The balance lever is elemental.** When one pilot's qualitative ability is found to be weaker
  than another's, juice the weaker one with **a little elemental boost**: the pilot comes with
  more starting level in some element. Selecting such a pilot is then **another way to change your
  starting elements**, in exchange for the (probably weaker) qualitative ability. Quantitative
  balance lives in the element system, so it stays inside *Elementals governs all buffing*.

### 2.3 A vessel always has a pilot — the base kit IS the default pilot's kit

The team waffled on this earlier and settled it here: **a vessel always has a pilot.** What has
been thought of as the vessel's base characteristics is better read as *how that vessel flies when
its default pilot is giving it some part of its kit*. Consequences:

- Every vessel ships with a **default pilot** that owns the part of the current kit a pilot can
  swap. The default pilot is a real pilot entry, not an absence. (The legacy data already has a
  shape for this — `SO_Captain_Arcade_Default_<Vessel>` — see §5.1.)
- Swapping to another pilot **changes that part of the kit in side-grade fashion**, never in
  upgrade fashion. If a candidate pilot reads as a strict upgrade over the default, either it is
  mis-scoped (it belongs to an artifact's quantitative lane, or it needs a cost) or the default
  pilot is under-specified.

## 3. Artifacts (SUPERSEDED 2026-10-27 by `Docs/Pilots/ARTIFACTS.md`)

### 3.1 What an artifact is

- **An artifact is an actual improvement.** This is where real upgrades live, and this is why
  **not every vessel carries one**.
- **Carrying an artifact is a risk.** Choosing to put an artifact on a vessel is choosing to take
  it into the HyperSea, where it can be **lost — stolen by a pirate, or what have you**. (The
  thief and fortress species in `Docs/BUILDERS_AND_THIEVES.md` are the shipped precedent for
  "something takes what is yours and carries it to a visible hoard"; the pirate that takes an
  artifact is a design hook, not yet a species.)
- Artifacts are **something of value to be found in the HyperSea**. They can carry unique
  characteristics and unique quality.
- **The lane split, kept:** the team had originally said *pilots strictly qualitative, artifacts
  strictly quantitative*, and is **sticking with it**, because the split is what keeps pilots
  special — an artifact must not be able to hand you the same ability a pilot does.
  **Exception, rare and by explicit decision:** with a good reason an artifact may carry a
  qualitative feature. Most artifacts touch a quantitative feature.

### 3.2 The fifth knob

A vessel has far more than four parameters. The team polishes and fixes most of them and then,
**very intentionally and selectively, picks four** — one per ability — as the knobs the element
system exposes. **Artifacts break that rule a little, on purpose:** an artifact gives players
access to **one more knob**, and **ideally a parameter the elements do not touch.**

The worked example from the conversation: suppose the element on a boost ability was given the
**cooldown**, for time or for simplicity, where the **top speed** or the **acceleration** were
also obvious candidates. An artifact is how a player reaches one of the knobs that was *not*
chosen.

So where elements are the universal interface from the game to any vessel, **artifacts need
access to more bespoke parameters inside vessels** — parameters that *can* be shared between
vessels but are not expected across all of them. That is the second reason for keywords (§4).

### 3.3 Where this sits against the fundamentals (PROPOSED framing)

`DESIGN_PHILOSOPHY_EMERGENCE.md` says *if a buff or debuff isn't expressed through elementals,
that's a smell.* An artifact's quantitative change is by construction a buff the elements do not
express. Rather than let that read as a violation, record it as a **sanctioned carve-out with a
boundary**:

- An artifact changes a **vessel's own parameter** (a fifth knob on *your* hull). It never buffs
  or debuffs **another** vessel, never moves petals, and never reaches the environment. Anything
  vessel-on-vessel or vessel-on-world still goes through elementals and the elemental economy.
- An artifact's boost is a **starting configuration**, set before the match and fixed for the
  match, exactly like choosing a vessel. It is not a mid-match buff with a timer.
- A pilot's **elemental** boost (§2.2) is not a carve-out at all: it is a starting level, and
  starting levels are already the element system's business (`SO_ArcadeGame.StartingElements`
  is the arena precedent).

Run the fundamentals-curation process on this framing before implementation starts; it belongs in
the **Vessels** fundamental's description once accepted.

## 4. Compatibility keywords (DECIDED)

Both pilots and artifacts reach into vessel-specific kit, so **both need keywords.**

**Rule 1 — generalise first.** Wherever possible, define the pilot or artifact against a concept
most vessels share, so it works on more vessels. The conversation's example: a pilot that
*shields your trail*. Most of the fleet has a trail, so it mostly just works. A hypothetical
vessel that swings around the map and drops a big prism at the end of each pendular swing has no
trail — but it does have **prism production**. Call a trail the vessel's **base prism
production**, and the pendulum vessel still has base prism production, so the pilot still applies.

**Rule 2 — when generalising fails, name the set.** Sometimes no shared concept exists. Then
create a **keyword that describes the collection of vessels** having the parameter the artifact
changes quantitatively or the kit the pilot changes qualitatively. A pilot or artifact declares
the keywords it requires; a vessel declares the keywords it satisfies; a slot only offers the
pilots and artifacts whose requirements the chosen vessel meets.

**Shared vocabulary (PROPOSED starting list).** Keywords are a vocabulary the fleet grows into,
not a per-feature flag. Start with the generalised concepts the fleet already has in code and
only add a bespoke one when a concrete pilot or artifact needs it:

| Keyword | Meaning | Vessels that satisfy it today |
|---|---|---|
| `BasePrismProduction` | lays mass continuously or on a cadence (a trail is the 1-D case) | the whole fleet except the Butterfly's slab, which is 2-D production and still qualifies |
| `Boost` | a speed burst with a cooldown or a charge | Dolphin (Charge Fill Rate), Sparrow (Afterburner), Squirrel (Boost Ring), Urchin (Slip), Grizzly (Rush) |
| `Projectile` | fires something with a flight time | Sparrow, Serpent, Grizzly |
| `Blade` | a contact skimmer that cuts or steals | Rhino, Squirrel, Thresher's ball |
| `CrystalBlast` | a vessel crystal explosion | Dolphin, Manta, Rhino, Squirrel |
| `Attach` | can ride a prismscape (`TrailFollower` / `BlockscapeFollower`) | Urchin, Squirrel |
| `Tether` | tows or anchors something on a chain or line | Thresher |

Keywords live as a small enum with fixed numeric values (serialization drift rule), or as a
`KeywordSO` list if designers need to add them without a code change — pick one at implementation
time; the enum is the simpler default.

## 5. What already exists, and how this maps onto it

### 5.1 The legacy Captain system is the pilot's ancestor

`SO_Captain` (`Assets/_Scripts/ScriptableObjects/SO_Captain.cs`) is `vessel + primary element +
InitialResourceLevels + portrait + flavour + AIBehaviorDescription`. `Captain.Level` (1–5, XP-gated
in `CaptainManager`, upgrades priced through the dead PlayFab `CatalogManager`) sets the primary
element's starting level to `0.1 × Level` — i.e. **"a fully upgraded captain starts the match at
level 5 in their element"**, which is exactly the element on-ramp §2.1 describes. The assets
(`Assets/_SO_Assets/Captains/Arcade/`, `…/Elemental/`) are named `Default_<Vessel>`,
`<Element>_<Vessel>`, `Freestyle_<Vessel>`. The hangar's captain upgrade surface is de-scoped
(`MENU_PROGRESSION_AND_IAP.md` §6.4) and `PortSquadView` carries a comment that the captain system
was removed from vessels and the squad system is inactive.

**Mapping (PROPOSED):**

| Legacy | Becomes |
|---|---|
| `SO_Captain.Vessel + PrimaryElement` (one asset per vessel × element) | split: the **element** is the squad slot's own choice; the **pilot** is per vessel, not per element |
| `Captain.Level → 0.1 × Level` in the primary element | the **vessel's** element upgrade state, read at match start as the starting level; the pilot adds its boost on top |
| `SO_Captain.InitialResourceLevels` | `PilotSO.ElementalBoost` — the balance lever of §2.2 |
| `SO_Captain_Arcade_Default_<Vessel>` | the vessel's **default pilot** of §2.3 |
| `AIBehaviorDescription` | keep — a pilot is also the natural home for an AI squad mate's behaviour flavour |
| `Squad` struct, one squad, `(class, element)` × 3 | several named squads, `(vessel, element, pilot, artifact)` × 3, one active |

Do not fork: either rename and reshape `SO_Captain` into the pilot or retire it — not both side
by side. Run `/refactor` on it first; the row "captain system removed from vessels" is a claim to
measure.

### 5.2 Other seams

- **Loadout** (`Assets/_Scripts/System/LoadOut/Loadout.cs`): `(Intensity, PlayerCount,
  VesselType, GameMode, IsMultiplayer)`. A loadout is a *game* configuration; a squad is a *crew*
  configuration. Keep them separate and let the pre-game flow pick one of each.
- **Elemental ability maps** (`Assets/Resources/ElementalAbilityMaps/<Vessel>.asset`,
  `ElementalAbilityMapSO`): the four knobs. An artifact's parameter should be checked against the
  map so it never duplicates a knob an element already owns on that vessel
  (`element_ability_table.py --json` is the reader to build that check on).
- **`SO_ArcadeGame.StartingElements`**: the arena per-hull handicap writes starting levels per
  vessel class. A pilot's elemental boost should compose with it additively at the same seam
  (`MultiplayerMiniGameControllerBase.ReapplyStartingElementsToLiveVessels` is where starting
  levels reach live vessels), not through a second path.
- **AI squad mates**: AI backfill already drives the same executors through cloned action SOs
  (`ARCHITECTURE.md` §2e), so a pilot that swaps kit via the action/executor path reaches AI squad
  mates for free. A pilot that only changes HUD or presentation does not need to.
- **Replication**: an artifact or pilot that changes a world-outcome (anything a remote client
  observes — shielded trails, pierce) needs the same replicated, authoritative home the level-5
  unlocks need (`ARCHITECTURE.md` §2f). Cosmetic and local-feel changes do not.

## 6. Milestone 1 — the minimal experiment (DECIDED scope, PROPOSED content)

**Scope:** for each vessel, **two pilots and two artifacts**, so there is simply **an example of
choice** on every slot. Minimalist by intent; this is an experiment with the shape of the feature,
not a content drop. Count the default pilot as one of the two where it is a real swap-able kit.

**Acceptance for the milestone:**

1. A squad slot shows vessel, element, pilot, artifact; pilot defaults to the vessel's default
   pilot; artifact defaults to none.
2. The pilot and artifact pickers show only entries whose keywords the chosen vessel satisfies.
3. Two pilots are selectable per vessel and produce a visible, playable kit difference in at least
   one arcade mode. Two artifacts are selectable per vessel and change a parameter no element on
   that vessel reaches.
4. Several named squads can be saved; one is active; the pre-game flow asks which.
5. Nothing about loss, theft or the pirate ships in milestone 1 — artifacts are **flagged** as
   at-risk in the UI copy and the risk itself is milestone 2.

**Candidate content (PROPOSED — pick, strike or replace).** Each pilot row is a qualitative swap
of part of the current kit; each artifact row names a parameter this write-up believes no element scales on that
vessel; the content pass must verify every one against the vessel's element map, because the table
names the ability an element owns but not always which parameter of it. Vessel kit facts are from `element_ability_table.py` on 2026-10-12.

| Vessel | Pilot A (default kit, named) | Pilot B (the side-grade) | Artifact 1 | Artifact 2 |
|---|---|---|---|---|
| Manta | Yastri trail as shipped | **Shielded wake**: base prism production lays shielded prisms, but the trail is thinner — the `BasePrismProduction` generalised pilot from §4 | Sting cooldown | Kabloom crystal-blast travel speed |
| Dolphin | Echo Sight as shipped | **Seed caster**: crystal seeding plants two smaller team crystals instead of one | Charge Boost top speed | Echo Sight reveal duration |
| Rhino | Energy sword as shipped | **Cleaver**: a cut prism's halves are launched apart instead of parting in place; blade width is shorter | Sword crystal-blast radius | Sword swing arc |
| Squirrel | Boost Ring as shipped | **Shepherd's hook**: Steal also pulls the stolen petal's crystal toward you on a miss; joust window narrower | Boost ring exit speed | Skim dwell-to-fill time |
| Serpent | Sniper Shot as shipped | **Tracer**: a missed shot leaves a short marker prism where it lands; magazine smaller | Solid Fuel Pellet count | Scope zoom speed |
| Sparrow | Pulsefire as shipped | **Broadside**: cannons fire in alternating sweeps for a wider cone at lower rate | Afterburner acceleration | Missile bay capacity |
| Urchin | Chain Spikes as shipped | **Rail rider**: Track Projector lays a rail you can attach to mid-flight; projection range shorter | Slip cooldown | Spike chain hop count |
| Butterfly | Dust Mode as shipped | **Monarch's march**: Fold carries a short slab segment through the fold; Fold cooldown longer | Cruise speed (fleet's slowest hull, by design — a deliberate knob for a deliberate hull) | Dust settle time |
| Thresher | Winch as shipped | **Anchor**: Plant also drops a shielded prism at the anchor point; chain shorter | Ball mass (swing momentum) | Winch reel-in rate |

Grizzly, Termite, Falcon and Shrike are out of milestone 1 (not playable or planned).

**Guardrails on the content pass:**

- A pilot that only *adds* and takes nothing away is an artifact in disguise. Every Pilot B row
  above names what it gives up; keep that column mandatory.
- An artifact that touches a parameter the vessel's element map already scales is a duplicate
  knob. Check the map before authoring.
- No artifact buffs or debuffs another vessel, or touches a petal (§3.3).

## 7. Data model sketch (PROPOSED)

Shapes only; the implementation design is its own document. Config lives in SOs; per-slot state
lives in the squad record; nothing stateful on a shared SO.

```
PilotSO : ScriptableObject
  string Name, Description, Flavor; Sprite Portrait, Icon
  VesselClassType Vessel                      // a pilot is per vessel; the DEFAULT pilot is one of these
  bool IsDefault
  List<VesselKeyword> Requires                // §4; empty = any vessel of this class
  KitSwap Swap                                // which ShipActionSO / executor config replaces which
  ElementalBoost Boost                        // §2.2: Element + int petals, usually 0
  string AIBehaviorDescription                // kept from SO_Captain

ArtifactSO : ScriptableObject
  string Name, Description, Lore; Sprite Icon
  List<VesselKeyword> Requires
  ParameterRef Parameter                      // a named vessel parameter the element map does NOT own
  float Value / Multiplier                    // quantitative; one knob per artifact
  Rarity Rarity                               // the "unique quality" of §3.1
  bool AtRisk = true                          // milestone 2 reads this

enum VesselKeyword { None = 0, BasePrismProduction = 1, Boost = 2, Projectile = 3, Blade = 4,
                     CrystalBlast = 5, Attach = 6, Tether = 7 }   // static values, never reordered

SquadSlot  { VesselClassType Vessel; Element Element; PilotId Pilot; ArtifactId Artifact; }
Squad      { string Name; SquadSlot Leader, RogueOne, RogueTwo; }
SquadBook  { List<Squad> Squads; int ActiveIndex; }           // replaces the single Squad record
```

`VesselKeyword` could equally be a keyword SO list; the enum is the simpler default and the
serialization-drift rule applies either way.

**As built in slice 1** (§10.3 has the reasons): `Requires` is a `VesselRequirement` (one optional
vessel class plus keywords) on BOTH pilots and artifacts; pilots and artifacts carry a stable string
`Id`, which is what a slot saves; the squad type is `SavedSquad`, because `CosmicShore.Core.Squad`
already exists; `KitSwap`, `ParameterRef`/`Value` and `Rarity` are not on the assets yet.

## 8. Open questions

1. **Where does the pre-match element pick live today?** The design assumes a player selects
   the element they start levelled in. The arcade configure modal picks domain and vessel; the
   element on-ramp is currently the legacy captain level. Decide whether the element choice is
   part of the squad slot (this doc's assumption) or part of the per-game loadout.
2. **How is "fully upgraded in an element" earned and stored now?** `Captain.Level` with
   PlayFab-priced XP is dead. The replacement store for per-vessel element upgrade level needs a
   home before pilots can boost on top of it.
3. **What is the pirate?** A fauna species in the thief lineage, a rival player's verb, or a
   match outcome (lose the match, lose the artifact)? Milestone 2.
4. **Do friends' squad mates bring their own pilot and artifact, or the host's?** A friend in a
   slot is their own player with their own saved squads; this doc assumes each human brings their
   own slot and only AI slots are authored by the host.
5. **Enum or SO for keywords?** §4, decide at implementation.

## 9. The long game

Pilots and artifacts are **fundamentals-adjacent content lanes with no ceiling**. Once the game's
fundamentals have stabilised, these are two of the lanes that keep producing: new seasons can
bring **new artifacts to discover** and **new pilots to join your crew** with new abilities, without
touching the fleet's four-knob contract or the elemental economy. Milestone 1 is the smallest
honest test of the shape; everything after it is content.

## 10. Implementation (milestone 1)

### 10.1 Slices

Each slice lands on its own. Foundation first, so the screens and the content have something real
to stand on. The design page that tracks this live is the Pilots & Artifacts artifact on claude.ai
(Build plan section).

| Slice | What | Status |
|---|---|---|
| 0 | Measure the legacy Captain and Squad code before reshaping anything (§5.1's claims) | **Done** 2026-10-13, §10.2 |
| 1 | Data foundation: `VesselKeyword`, `VesselRequirement`, `ElementalBoost`, `PilotSO`, `ArtifactSO`, `PilotArtifactCatalogSO`, `SO_Vessel.Keywords`, the squad book in `SQUAD_DATA`, edit-mode tests | **Landed** 2026-10-13, not editor-verified (`Docs/UNITY_VERIFICATION_CHECKLIST.md`) |
| 2 | Author the nine default pilots, every milestone vessel's keywords and the catalog asset, from a generator with a `--check` gate | **Landed** 2026-10-13, not editor-verified, §10.4 |
| 2b | The 2026-10-26 roster: 40 pilots and 45 artifacts from `Docs/Pilots/pilots_and_artifacts.json` through this generator; `PilotSO.ability` / `portraitIsStandIn`; `ArtifactSO` stat, ranks and `Apply`; `slotsPerVessel`; the Hangar's PILOTS and ARTIFACTS tabs read the catalog (`PilotLoadoutStore`, device-only for now) | **Landed** 2026-10-26 on `claude/pensive-dijkstra-qxoxo0`, not editor-verified; `Docs/Pilots/PILOTS_AND_ARTIFACTS.md` § Implementation |
| 3 | Match start: a slot's starting element level plus the pilot's `ElementalBoost` through the `StartingElements` seam (§5.2); bind an artifact to its one parameter | Next |
| 4 | Pilot kit swap through the action/executor path, so AI squad mates get it for free; replicated where the result is a world outcome | Queued |
| 5 | Squad screen: four pickers per slot, named squads, one active, the pre-game "which squad" question; retire `PortSquadView`, `SquadSystem`, `Squad` and the captain screens in the same change | Queued |
| 6 | Content: nine side-grade pilots and eighteen artifacts, each artifact checked against its vessel's element map (`element_ability_table.py --json`) | Queued |

### 10.2 What slice 0 measured

- **The squad is dormant.** `SquadSystem` (local `squad.data`) has one consumer, `PortSquadView`,
  on the nav-locked PORT screen. Nothing at match start reads a squad and no AI squad mate is
  spawned from one. `SquadSystem.CaptainList` is never assigned, so the leader and rogue getters
  always return null.
- **`SQUAD_DATA` had no writer.** `SquadRepository` was loaded and reset by `UGSDataService` and
  written by nothing, and `SquadCloudData.Initialized` was never set. No record holds data worth
  migrating, which is why slice 1 reshapes the key in place instead of adding one.
- **A latent menu-load exception.** On an install with no `squad.data`, `SquadSystem.Init` built a
  `Squad` from null captain defaults and threw from `PortSquadView.Start`, which runs on every
  Menu_Main load because the PORT screen is hidden by alpha, not deactivated. Found by reading the
  code, not observed in a run; slice 1 guards it.
- **The captain assets are unreferenced by any scene or prefab.** 64 `SO_Captain` assets
  (`Captains/Arcade/` 28, `Captains/Elemental/` 36) plus 7 in `_SO_Assets/_TEMP/`. Their only
  inbound references are `ElementalCaptains.asset` (wired into `Bootstrap.unity`'s inert
  `CaptainManager`) and legacy `Captains:` keys left on 22 arcade cards and two `SO_Class_*`
  assets. `CaptainManager.OnEnable` is empty since the PlayFab retirement. `PilotSO` therefore does
  not reuse `SO_Captain`: a captain is one asset per vessel AND element, a pilot is per vessel. The
  captain types retire with the old squad screen in slice 5, so the two never ship side by side
  as live systems.
- **Name clash.** `CosmicShore.Core.Squad` (the legacy struct), `UGSDataService.Squad` and
  `UGSKeys.Squad` already exist, so the saved squad is `SavedSquad`.

### 10.3 Slice 1: what landed, and where it differs from §7

| Type | Where | Notes |
|---|---|---|
| `VesselKeyword` | `Data/Enums/` | The §4 starting vocabulary, values 0–7, pinned by a test |
| `VesselRequirement` | `Data/Structs/` | `Vessel` (default `Any`) + `Keywords`; `IsMetBy(class, keywords)` is the one compatibility rule. A hull that is not concrete (`Random`, `Any`) meets nothing |
| `ElementalBoost` | `Data/Structs/` | `Element` + `Petals` (one petal = one level); applied in slice 3 |
| `SquadSlot`, `SavedSquad`, `SquadBook` | `Data/Structs/` | Pure data. The book keeps the active squad pointed at the same squad through add and remove, and `Normalize` repairs a partial record. Names are cut to printable ASCII, 24 characters, because the UI font has no other glyphs |
| `PilotSO`, `ArtifactSO` | `ScriptableObjects/PilotsAndArtifacts/` | Identity, `VesselRequirement`, `IsDefault` and `ElementalBoost` (pilot), `AtRisk` (artifact) |
| `PilotArtifactCatalogSO` | same | Pickers (`GetPilotsFor` lists the default first), match-start resolve (`ResolvePilot` falls back to the default for a stale or incompatible id, `ResolveArtifact` to none) and `Validate` for authoring errors. Lookups are quiet probes |
| `SO_Vessel.Keywords` | `ScriptableObjects/SO_Vessel.cs` | Empty on every asset until slice 2 |
| `SquadCloudData` | `System/CloudData/Models/` | Schema 1: `SchemaVersion` + `Book`. The repository normalizes on load. The one-squad fields are gone (they were never written) |

Differences from the §7 sketch, each the simpler of two options:

- **Artifacts can bind a vessel class too.** §7 gives a pilot a `Vessel` and an artifact only
  `Requires`. Most candidate artifacts in §6 are one hull's own parameter (Sting cooldown, Ball
  mass), and keywords-only would mint a single-vessel keyword for each. Both kinds now use one
  `VesselRequirement`, and a generalised pilot (Shielded wake) is just one left on `Any`.
- **Slots save string ids, not asset references,** because the record is JSON in cloud save. A
  stale id falls back rather than failing, so a pilot or artifact can be retired without
  corrupting saved squads. Ids must never be renamed once players have saved them.
- **Not on the assets yet:** `KitSwap` (slice 4) and the artifact's parameter and value (slice 3),
  because their shape depends on how those slices reach the vessel. Artifacts reach parameters the
  way `ElementalFloat` does (serialized on the asset that owns the number), so the binding is a
  slice 3 design question rather than a field to guess now. `Rarity` waits for a content decision
  on what the tiers are.

Out of slice 1 on purpose: no asset is authored, nothing in a scene reads any of this, and
`PortSquadView` still runs the legacy squad.

### 10.4 Slice 2: the authored assets

`Tools/Build/author_pilot_assets.py` writes them and is their source; edit its `MILESTONE` table
and re-run rather than editing an asset. `--check` reports drift and writes nothing, `--self-test`
holds its negative controls. It owns:

| Asset | What |
|---|---|
| `_SO_Assets/Pilots/Default/Pilot_<Vessel>_Default.asset` × 9 | Each milestone vessel's default pilot: id `pilot.<vessel>.default`, bound to its class, no keywords, no boost |
| `_SO_Assets/Pilots/PilotArtifactCatalog.asset` | The nine default pilots in fleet order; no artifacts yet |
| `keywords` on `_SO_Assets/Classes/SO_Class_<Vessel>.asset` × 9 | §4's table as written; only that key is touched |

Three things to know:

- **The keyword table is §4's PROPOSED one, transcribed.** It is now live data, so the content pass
  (slice 6) must confirm each keyword against the vessel's real kit before a pilot or artifact
  leans on it.
- **A default pilot's display name is its kit's name** (Yastri Trail, Echo Sight, …), from §6's
  "Pilot A (default kit, named)" column. Character names and portraits are a content decision;
  the 24 legacy portraits in `_Graphics/Pilots/` are the obvious pool.
- **Nothing references the catalog yet,** so it does not ship in a build. Slice 3 (match start)
  or slice 5 (squad screen) wires it, whichever lands first.

`PilotCatalogAssetTests` loads the real assets: the catalog validates clean, and every milestone
vessel declares `BasePrismProduction` and resolves to its own default pilot, listed first.
