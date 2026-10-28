# Voyages: squads on the HyperSea

**Status: DESIGN DRAFT** (Garrett, 2026-10-28; first draft written the same day). Nothing in this document is built.
It is the plan that `Docs/Pilots/ARTIFACTS.md` §8.5 asks for before vaults, hunting and extraction can be built.
Every default marked **(default)** is a guess waiting for Garrett's answer; §11 lists them.
Sources beyond the repo, and what in them is out of date: [`SOURCES.md`](SOURCES.md). In short: the FigJam board *How players get, do, and learn things*, the Figma file *Cosmic Shore for Alpha* (page *UI for vLater*: the old Port faction boards), the Notion design docs *Mission Overview*, *Initial Onboarding*, *Ark Design Document* and
*Pilot Design Document* (faction types, notoriety, the Ark's bunks). 

> "voyages are what we have instead of missions as the fourth game location after toybox, arcade, arena, come
> voyages. the arkway is a toy that also uses the ark and is the most voyage like of the toys. it is meant to get a
> taste of a freestyle voyage. the actual voyages should be the culminating experience of the game where squads
> explore the hypersea and have unique experiences at specific locations in the galaxy. meeting other players on a
> shared galactic map (the hypersea). you leave one location and travel to another. our job will be to conduct
> galactic traffic control so players encounter each other in peer to peer instances that are managed and make sense
> to both players respective missions." (Garrett, 2026-10-28)

## 0. In one paragraph

A squad (three vessels, each with its vessel, element, pilot and artifact cards, and one Ark that carries the
artifacts) sets sail from its home port onto **one shared galactic map, the HyperSea**. The map is a set of named
**locations** joined by **lanes**. The squad picks where to go from the jobs its own pilots drum up at port, each
for their faction, and much of that work is carrying information, which only couriers on the HyperSea can move
faster than light. Each lane is flown with the Ark, the way the Arkway flies it, and each location is a real cell with an experience of its own: a vault field, a lair, a wreck, a
contested core, a harbour. When squads' paths cross, **galactic traffic control** (a small server-side director)
decides who meets whom, and puts them together in one managed peer-to-peer instance chosen so that the meeting
makes sense for both sides' missions. Whatever is aboard the Ark when the squad gets home is theirs.

## 1. Where voyages sit in the game

| Location | What it is | Artifacts | Other players |
|---|---|---|---|
| Toybox | freestyle, the toys (the Arkway is Wander's *With Ark*) | no | your party |
| Arcade | single-vessel games, scored | no | your party |
| Arena | squad vs squad matches (`SO_ArcadeGame.ArenaRules`) | **no** (Garrett, 2026-10-27) | matched |
| **Voyages** | **the culminating experience: travel the shared HyperSea** | **yes, optional per slot** | **anyone you meet** |

- **The Arkway stays a toy.** It is a taste of a freestyle voyage: one Ark, one player's corridor of cells, no map,
  no other squads, nothing at stake. Voyages reuse its machinery (§4) and do not change it.
- **One word, two meanings, for now.** The Arkway's own code and plan call a toy run a "voyage" (`ArkwayVoyageHud`,
  `ArkwayRun.BeginVoyageAsync`, `ARKWAY_PLAN.md`). In this document a voyage always means the fourth location; an
  Arkway run is called an Arkway run. Renaming the Arkway's identifiers is its owner's call, not part of this design.
- **Voyages replace missions.** The Home hub already says Voyage (`Docs/MENU_REDESIGN_B.md` §69 and §71). Today the
  Voyage window lists Tandava as Voyage 1, launched through the arena launch panel. In this design Tandava becomes the
  first **location experience** (§5), *Tandava's lair*, rather than a voyage of its own. The window's layout is
  Shombith's (§10).
- **Universality.** A voyage is the same HyperSea under the same rules (`Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md`
  § Universality). Nothing in a voyage gets a rule the rest of the game lacks. Every location's drama is composed from
  the fundamentals: domain, mass, cells, elementals, flora and fauna, vessels, the Ark.

## 2. The squad and what it brings

Already decided (Garrett, 2026-10-27; `Docs/Pilots/ARTIFACTS.md`, `Docs/Pilots/CALLSIGNS.md`):

- A squad is **three vessels**. Each vessel has its own pilot (required) and its own artifact (optional).
- **One Ark per squad.** The artifacts brought ride in it. Taking down an Ark takes everything aboard; fauna felling
  it spills the artifacts for anyone.
- A squad is a domain for now (Jade, Ruby or Gold).

New on 2026-10-28 (Garrett):

- **The crew lives on the Ark.** Your Ark carries a crew of pilots, more than the three who fly (the Notion *Ark
  Design Document* starts it at three bunks and lets it grow). Each pilot is a unique person layered on a callsign that
  pilots in other crews share, a crew holds one of each callsign, and each pilot belongs to one faction. The crew
  decides which jobs you can find (§6.2), and your captain decides your faction (§6.2.1).

New here:

- **(default)** **A squad is 1 to 3 people.** Any seat with no person in it is flown by its AI, exactly like an arena
  backfill or a freestyle companion (`Docs/CREW_STATIONS.md`: "every seat no person holds is the AI's"). A solo
  player sails with two AI wingmates.
- **The squad's party is its crew.** A squad of more than one person is a party (`HostConnectionService`,
  `PartyInviteController`). The party leader charts the course.
- **The Ark is the squad's clock and its stake.** As in the Arkway, the Ark sets the pace of travel, and its hull is
  the payload supply (`ArkHold`). A voyage ends when the squad docks at home, or when its Ark falls.

## 3. The HyperSea map

### 3.1 Shape

**(default)** **One fixed, authored galaxy that every player shares.** It is not generated per voyage, because
"meeting other players on a shared galactic map" needs everyone to be on the same map, and because a place you can
name ("the Lantern Reach vaults") is a place people can plan to meet at, talk about and come back to.

- **Locations** are the nodes. Each is one cell with an authored cell config (`Cell` / `CellConfigDataSO`, the same configs
  the cell selector and the Arkway draw from) plus an **experience** (§5) and a set of **mission hooks** (what a squad
  comes here for).
- **Lanes** join locations. A lane has a length (in Ark travel time), a danger (how much fauna it carries, from the
  cells along it) and sometimes a **crossing**: a point along the lane where traffic control may hold an encounter
  (§6.5).
- **Regions.** Each domain has a home region around its harbour. The middle of the map is contested. Distance from
  home is risk: the far locations hold the best finds and the longest trip back.
- **Folds.** A crystal wormhole (`Docs/CRYSTAL_WORMHOLE.md`) can be a lane: a short, risky shortcut. It is the one
  place the map is not "fly the whole way".

### 3.2 Size

Start small: **one harbour per domain plus about 9 locations** (12 in all), so that two squads out at once have a
real chance of meeting. The map grows by adding locations, never by reshaping existing ones, so names stay true.

### 3.3 Where the map lives

The map is **data**, written by a generator (the repo's pattern: one `author_*.py` owns the assets and has `--check`):

- `HyperSeaMapSO` (Unity): locations, lanes, regions, each location's cell config and experience. This is what the
  client draws and flies.
- The same map as JSON for the server (§6), produced by the same generator, so the client and traffic control can
  never disagree about what is next to what.

### 3.4 What the player sees

The screens follow the full-release information architecture on the FigJam board *How players get, do, and learn
things* (section *Information Architecture for Full Release*), brought up to date with the decisions here.

- **Voyages (the hub's fourth location).** A mission select that offers **three jobs** at a time from your crew's
  board (§6.2), each with its estimated time, intensity (1 to 4, the game's one difficulty scale), reward range, a
  summary and, expanded, the lore that led to it. Beside it: the squad picker (one of four saved squads, each slot's
  vessel, element, pilot and artifact), the main story track ("the next leg of the journey") and the episodes.
- **The chart.** Choosing a job puts its destination on the chart: the galaxy, your squad's position, where you can
  sail from here, and, faintly, the other squads that are out (how many and roughly where, never who until you
  meet). The chart is a world fact, so it is shown in 3D in the world, not as flat text (the LOCKED "show a world fact
  in the world" rule, `Docs/claude/SHADERS_AND_PERFORMANCE.md`). Its natural home is a star chart aboard the Ark.
- **The Ark.** Crew (every pilot's callsign, name, faction, faction type and a backstory that grows with key
  events; **promote to captain**, which changes your faction; discontinue a pilot), the four saved squads, the
  converter, decor, bunks bought with omnicrystals, and **pilot search**: spend omnicrystals to search by vessel or
  by faction type, then recruit one pilot from a choice of three.
- **The Port.** Friends, and the faction: chat, clout, the daily faction leaderboard, promotions (below), and
  invitations (squad, party, an Ark link). **Emissaries** arrive on Home as a toast to accept or reject.
- **Out of date on the board** (do not build): pilots and artifacts "compatible with this class of vessel" (any
  vessel seats any pilot, and no artifact names a vessel class, `ARTIFACTS.md` rule 3), "Missions" as the hub name,
  and the Hangar's per-class artifact picker.

## 4. Travel

### 4.1 A leg

**(default)** A leg is **flown, Arkway style**: the squad leaves a location, the Ark gets under way down the lane,
and the cells along it stream ahead and recycle behind (`CellConveyor`, satellite cells, `Cell.SatelliteEcologyEnabled`,
`Cell.RuntimePopulationScale`). The Ark's wake draws fauna; the nucleus of each cell is sanctuary; the crossings are
dangerous. All of that is already true in the Arkway (`Docs/ECOSYSTEM.md` §41, `Docs/ToySystem/ARKWAY_PLAN.md`).

What a voyage adds to the Arkway corridor:

- **A destination.** The corridor is drawn from the lane's cells, not from a shuffle bag, and it ends at the
  destination location.
- **A real length.** A lane takes minutes, not forever (target: 2 to 5 minutes per lane; a voyage is 3 to 6 legs,
  about 20 to 40 minutes).
- **Leaving and arriving are hand-offs** (§7.3): the place where the squad's instance may change.

### 4.2 Why fly it and not jump

FTL-style jumping would be cheaper, but the Ark's crossing is where the stakes are: fauna on the hull, payloads
spent, the choice of whether to linger in a nucleus. That is the voyage's middle, and it is what makes arriving
feel earned. Folds (§3.1) are the jump, used sparingly.

### 4.3 Leaving early and coming home

- A squad can turn back on any lane. Home is always reachable.
- **Extraction** is docking at your own harbour (or, later, any friendly harbour). Only then do the artifacts aboard
  join the collection (`ARTIFACTS.md` §6, step 5).
- If a player quits mid-voyage, their seat goes to the AI and the voyage continues for the rest of the squad. If the
  whole squad quits, the voyage ends where it stands. **(default)** Artifacts aboard an abandoned voyage are lost,
  like an artifact nobody collects. That is harsh, so it is listed in §11.

## 5. Locations and their experiences

A location is a cell plus an **experience**: a composition of shipped systems and a reason to come. A location never
gets a scripted event; it gets an arrangement (a creature, a structure, a field of vaults) that the fundamentals then
play out. The first set:

| Experience | What is there | Mission hooks | Built from |
|---|---|---|---|
| **Harbour** | your domain's home port. Safe: no rival squad is ever placed here | depart, extract, refit (Ark screen, upgrades) | the Ark screen, `ArkHold` |
| **Vault field** | super-shielded shells with an artifact inside | hunt | super-shield, the plain payload's crack and the Grizzly sniper's (`SuperShieldCrack`), `ArtifactPickup` |
| **Lair** | one great creature in a closed cell. *Tandava's lair* is the first | hunt (a big find for a kill), raid (fight the squad that came for it) | Tandava (`GameModes.Tandava`, its director and scene) |
| **Wreck** | a fallen Ark: spilled artifacts, its hull mass grazed by fauna | salvage, race | `ArkFall`, `ArtifactPickup` |
| **Contested core** | a cell whose nucleus claim decides who the waves fight for | hold, raid | the nucleus claim, fauna waves in the controlling colour |
| **Hoard** (proposed, the ecology owner's call) | thieves' stashes (`Docs/BUILDERS_AND_THIEVES.md`) | hunt | the thieves |
| **Fold** | a crystal wormhole: a shortcut lane with a gauntlet at each mouth | travel | `Docs/CRYSTAL_WORMHOLE.md` |

- **A location's experience runs the same with one squad or two.** Two squads at a vault field race each other for
  the vaults; one squad at a vault field just hunts. Traffic control decides whether the second squad is there; the
  location does not need to know.
- **Locations remember nothing at first** (§9, phase V3 adds the galactic war).
- **Vaults restock** per instance, so a location is worth revisiting.

## 6. Galactic traffic control

Galactic traffic control (GTC) is the server-side director that decides **who meets whom, where, and in which
instance**. It is the part of the design that makes the map feel shared, and the part that most needs Shombith,
because it is backend (§10).

### 6.1 What GTC knows

Every squad out on a voyage files a **flight plan** with GTC whenever it chooses a leg:

| Field | Why |
|---|---|
| squad id, domain, party members, how many are people | who, and how many people seats it takes |
| from, to, departure time, expected arrival | where and when the squad will be |
| the job for this leg (§6.2), and the squad it targets, if any | what the squad came for, and whom |
| stake: how much is aboard the Ark (artifact count and level band) | fair meetings |
| region / connection quality | a playable peer-to-peer instance |
| recent encounters | so the same two squads are not thrown together over and over |
| friends and rendezvous requests | so friends can meet on purpose |

### 6.2 Jobs: your crew finds the work

> "your crew of pilots are unique with their own stories, races, and other flavor fluff, but their callsign is tied
> to their ability and shared with other members of other crew. so you can only have one of each callsign and that
> pilot will be tied to a particular faction of a general type. when they are at port they will drum up work related
> to their faction." (Garrett, 2026-10-28)

A voyage does not start from a menu of mission types. It starts from **your crew**:

- **A pilot is a person and a callsign.** The person is unique to your crew: their name, story, race and look. The
  callsign is their ability (`Docs/Pilots/CALLSIGNS.md`), and other crews have pilots with the same callsign. **A crew
  holds at most one pilot of each callsign.**
- **Every pilot belongs to one faction.** A faction is one of many of its **faction type**, and each faction type
  belongs to a domain. For example, a made-up "Black Tide" would be one of dozens of pirate factions, and pirates are a Ruby faction
  type. The faction types come from the Notion design docs (*Mission Overview*, *Initial Onboarding*):

  | Domain | Faction types |
  |---|---|
  | Ruby (freedom) | hunter, pirate, explorer, activist, thief |
  | Gold (commerce) | merchant, bounty hunter, gatherer, contractor, sport |
  | Jade (order) | aristocrat (militant, diplomat), researcher, preservationist, first responder, priest |

- **At port, each pilot drums up work for their faction.** The port's job board is whatever your crew's pilots
  bring you, so two crews docked at the same port see different boards. A job names a destination, a reward and the
  faction paying it. A **job is the mission** that GTC matches on (§6.1).

The first jobs, one per faction type in Notion's roll-out order (merchant, hunter, aristocrat), plus the two that
prey on them:

| Pilot's faction type | Finds | The squad sets out to | Natural opponents |
|---|---|---|---|
| **Merchant** | a **courier contract**: carry cargo or a sealed dispatch from here to a named port, by a deadline | arrive with it aboard | a pirate holding a tip on this very contract |
| **Hunter** | a **sighting**: a kaiju (a lair, §5) or a vault field nearby | kill it or open the vaults, bring the find home | other hunters chasing the same sighting (a race) |
| **Aristocrat (militant)** | a **defence**: hold a named cell's core for the faction | keep the nucleus claim | whoever is told to take it |
| **Pirate** | a **tip**: word of another squad's courier contract carrying something valuable | intercept that squad and take down its Ark | that courier squad |
| **Bounty hunter** | a **bounty**: a named squad whose notoriety put a price on it | find that squad and take down its Ark | that squad |

- **Escort is gone, and haul is now the courier contract.** "Haul and escort sound too similar" (Garrett,
  2026-10-28). A squad that wants to protect someone else's courier takes the Jade defence job. A Jade defence job
  can also name a moving thing (a courier's Ark) rather than a cell.
- **Bounties come from notoriety.** In the Notion docs, Ruby's currency is notoriety. It grows quickly with missions
  and decays over time, and the more of it a squad has, the bigger the bounty on it. A bounty job points at a real
  squad.
- **Tips and bounties point at real squads.** A pirate's tip is another squad's real contract, and a bounty is
  another squad's real notoriety. When GTC pairs the two, the meeting makes sense to both sides by construction: one
  squad is carrying the cargo and the other came to take it. This is the strongest form of "make sense for both
  players' missions".
- **The other faction types come later.** Activists, thieves, gatherers, researchers, preservationists, first
  responders and priests each get a job when their location experience exists. A thief steals information (§6.3).

#### 6.2.1 Pilots, captains, factions and emissaries

> "a unique person layered on a callsign. so a new pilot in your ark crew grants access to new voyage types and a
> new vision ability (denoted by callsign) when the pilot is added to a squad. pilots can also be promoted to captain
> which how you join a faction. you join your captain's faction. as factions fill with users we spawn new factions of
> the same type and mint new pilots in the new faction instead of overcrowding factions of optimal size. players can
> send emissaries from their faction to a friend. if that friend accepts them into their crew then they can both
> engage with the same faction." (Garrett, 2026-10-28)

- **A pilot is a person layered on a callsign.** The callsign record (`Docs/Pilots/CALLSIGNS.md`, the squad layer's
  17) is the ability and the sight. The person (name, story, race, faction) is a record of its own that points at one
  callsign. Two crews can each have a VOLT, and they are different people with the same ability.
- **A new pilot opens two things.** Recruiting a pilot into the Ark crew adds their faction's jobs to what the crew
  can find at port (§6.2). Seating that pilot in a squad also brings their callsign's sight (LB) into the voyage.
- **Your captain decides your faction.** Any pilot in the crew can be promoted to **captain**. Promoting one is how a
  player joins a faction: the player joins the captain's faction. Changing captain changes faction.
- **Factions split instead of crowding.** Each faction has an **optimal size** (**(default)** 50 members, the size
  the old Port boards show as "34/50 members"). When every faction of a type is full, the backend spawns a new
  faction of the same type with its own name. From then on, new pilots of that type are minted into the new faction,
  so recruiting a pirate pilot puts you in the youngest pirate faction with room, not the oldest one.
- **Emissaries bring friends together.** A player can send an **emissary**, a pilot from their own faction, to a
  friend. If the friend accepts the emissary into their crew, both players can take that faction's jobs, and once the
  friend promotes the emissary to captain they are in the same faction. The Notion *Ark Design Document* adds that an
  emissary opens a bunk of its own, and that factions grant free emissaries by rank.
- **Allies.** Your captain's faction is your **primary** faction: you are a member, with its chat, rank and clout.
  Every other pilot's faction is an **ally**: you take its jobs through that pilot, but you are not a member (Garrett,
  2026-10-28, confirming the old Port boards' primary and ally slots).
- **Ranks inside a faction** (from the FigJam board, to confirm): members join a team, team leaders form teams,
  generals receive special jobs that affect several teams, and faction leaders receive jobs that affect the whole
  faction. A promotion can be accepted, rejected or resigned.
- **What GTC gets out of it.** Faction membership is a meeting preference: squads of the same faction are matched
  as co-op where their jobs allow it, and a faction's own rival factions (a pirate faction and the bounty hunters
  hunting it) are what the hostile pairings feed on.

What the old Port boards in Figma (*Cosmic Shore for Alpha*, page *UI for vLater*, 2024) already drew for factions,
for reference: a **primary** faction and **ally** faction slots (buy a slot, replace a faction, "You will always be
able to rejoin this faction later"); faction **recruiters** who notice you when you upgrade classes and set high
scores; a faction screen with members online, chat and a daily faction challenge with its own leaderboard; **faction
missions** paid for with a ticket that arrives every 24 hours (the example is the Pirates' *Raid of the Celestial
Booty*); and **knowledge requests**, where a member asks for knowledge of a vessel's ability and other members
**impart** it. Those boards predate voyages. The captain rule above replaces how you pick a primary faction there.

The pairing table GTC uses is written as data, so it can be tuned without code. Each pair of jobs has a weight (how
good a meeting it is) and a **relation**: co-op, race or hostile. The instance announces the relation to both squads
on arrival. Nobody is told "you are now in PvP". They see who is here and what they came for.

PvP stays petals only (LOCKED, `Docs/claude/IMPACT_EFFECTS_AND_AUDIO.md`). "Hostile" means your Ark is fair game,
your scored hits silence each other, and vaults and wrecks are a race. It never means vessels destroying vessels.

### 6.3 Information: the news travels by courier

> "many hauling jobs will be shuttling information and that information will make it to the port you are at and that
> will effect the voyages your crew can find. information is one of the most valuable resources across the galaxy and
> the navigating the hypersea is the only way for couriers to beat the speed of light." (Garrett, 2026-10-28)

- **A dispatch is cargo.** Many courier contracts carry a **dispatch**, a sealed piece of information, in the Ark's
  hold. It rides there by id, the same way an artifact does (`ArkHold`). When the Ark falls, the dispatch goes to the
  credited squad with everything else aboard.
- **A port only knows what has reached it.** Each port keeps a **news ledger**: the dispatches delivered there. A
  pilot can only drum up work from news their port has. A kaiju sighted out at the Lantern Reach becomes a hunter's
  job only at ports a courier has brought that news to. Until then, the job does not exist there.
- **News makes jobs.** A delivered dispatch adds entries to the port's ledger, and each entry is a job seed. A
  sighting gives hunters work, a convoy schedule gives pirates tips, a bounty posting gives bounty hunters targets,
  and a call for aid gives the Jade a defence job. Delivering a dispatch therefore changes the voyages that every
  crew docked at that port can find.
- **News ages.** A dispatch is worth most when fresh: a sighting goes stale once the kaiju is killed, and a tip is
  useless once the convoy has docked. So speed matters, which is why the courier's flight is a real crossing and why
  folds (§3.1) are valuable.
- **Stolen news lands somewhere else.** A dispatch taken from a fallen courier can be delivered by the raider to a
  port of their choosing, or sold to the pirates instead. Who carries information, and where, decides what the galaxy
  knows.
- **Nothing is minted.** Dispatches are born from events, the way mass in the HyperSea comes from somewhere: a lair
  spawning, a squad's notoriety crossing a line, a contract posted, a cell's health changing. They are not invented to
  fill a board. This is the information twin of the conservation rule (`Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md`).
- **Where it lives.** The news ledgers are backend state (§8), written when a courier's delivery is confirmed at
  extraction. In V1 (§9) they are simulated on the device from authored events. In V3 a real courier's delivery
  changes other players' boards.

### 6.4 When GTC decides: at arrival

The main encounter point is **arrival at a location**. When a squad's Ark nears its destination, GTC looks at every
squad arriving at the same location in a window around that time (**(default)** 60 s either side) and picks the best
pairing by: job weight, stake fairness, connection, the human cap, and freshness. Then:

- **A targeted job comes first.** A tip or a bounty names a squad. GTC routes the hunting squad to meet its target at
  the target's next arrival (or, from V4, at a crossing), if the target's job allows a hostile meeting there.
- **A match:** both squads are told which instance to join (§7). They arrive into the location together.
- **No match yet:** the squad arrives alone. GTC can still bring a second squad in while the first is there, as long
  as the location's experience has not resolved (the vaults are not all open, the creature is alive).
- **Nobody waits** **(default)**: no squad is ever held on a lane or outside a location waiting for a match. If no
  real squad fits within a few seconds, the squad arrives alone, and GTC may place an **AI squad** instead (§6.6).

### 6.5 Crossings (later)

Some lanes have a crossing: a point mid-lane where two lanes meet or where a lane passes near another location.
GTC can hold an encounter there between squads whose courses cross at about the same time (a pirate intercepting a
courier). This is phase V4; arrivals come first because they are simpler (§9).

### 6.6 AI squads

The HyperSea should never feel empty, and a solo player should get the same kinds of meeting a group does. An AI squad
is three AI vessels with an AI-flown Ark, filed with GTC like any squad, and placed by the same rules. It carries
artifacts and dispatches that are really at stake (a raid on one pays), and it takes jobs from its own pilots' factions, so an AI courier can be a pirate's real tip. Because AI learning is automatic and learns from recorded
human play (`CLAUDE.md` § AI learning is automatic, `Docs/AI_SYSTEM/`), AI squads get better at voyages the more
people play them. What an AI squad needs from the AI work (a voyage policy for each job: courier, hunt, defence, pirate, bounty) is Yash's area (§10).

### 6.7 Rules GTC never breaks

1. **No more than 4 people in one instance.** "A party is 4. Never 6, anywhere" (`Docs/MULTIPLAYER_START_HERE.md`
   §2). Two squads of two people meet; a squad of three meets a squad of one, or an AI squad. Vessels are not capped
   by this: two squads are six vessels and two Arks, the rest flown by AI.
2. **No rival squad in a harbour.**
3. **Friends who asked to meet are matched first**, and never with a third squad that would push past the cap.
4. **A squad that just lost its Ark to another squad is not matched with that squad again on the same voyage.**
5. **A meeting must make sense for both sides.** A pairing whose job weight is zero is never made, however long
   anyone has been out.

## 7. Instances

### 7.1 What an instance is

An instance is **one location (or one lane) with one or two squads in it**, run peer to peer the way every match
already runs: a UGS Multiplayer session over Relay, Netcode for GameObjects, **one player's machine as the host**
(`MultiplayerSetup`, `HostConnectionService`). No dedicated servers, in keeping with "as free as possible"
(`Docs/MULTIPLAYER_START_HERE.md` §1).

### 7.2 The squad's own session

A squad's party session (Relay, up to 4) is already its network: under the eager-Relay design it persists across
scene changes and is torn down only by leaving the party (`ServerPlayerVesselInitializer`'s notes). **A squad sails
in its own session.** A lane flown alone is an instance of one squad, hosted by the party leader. A solo player is a
host of one, exactly as in the menu today.

### 7.3 Two squads meeting: one hosts, one joins

Netcode for GameObjects has no host migration and no merging of two sessions. So a meeting is a **hand-off**, the same
move a party invite already makes (`PartyInviteController`: host to client, local to Relay, with recovery):

1. GTC names the **host squad** (**(default)** the one with the better connection, ties to the one that arrived
   first) and gives the other squad the host's session to join.
2. The guest squad's people leave their own session and join the host's, as an accepted invite does. Their vessels,
   pilots, artifacts and **their Ark** spawn in the host's instance from their squad record (§8), not from the old
   session's objects.
3. Both squads arrive behind the cell-change veil (`EnvironmentLoadVeilContentSO`), so the hand-off is hidden by the
   arrival the player expects anyway.
4. **Leaving:** when the guest squad sails on, its leader starts a fresh session and the squad rejoins it at the
   departure veil. The host squad stays.
5. **If the host drops:** the instance ends for both. Each squad restarts its own session from its last squad record
   and is placed back at the location alone. This is the cost of having no host migration; Block 6 of the multiplayer
   hardening programme (reconnection grace) is the natural place to make it softer.

A guest squad of AI only (an AI squad) needs no hand-off: the host spawns it, as the arena spawns AI backfill
(`ServerPlayerVesselInitializerWithAI`).

### 7.4 One voyage mode, many locations

**(default)** Voyages are **one game mode** (one new `GameModes` id, claimed only after checking `bleeding-edge` and
every in-flight branch, `CLAUDE.md` § Team) whose controller loads a location's cell config and experience from the
map. Locations are data, not one mode each. Tandava's director is reused inside its lair; the Tandava mode itself
stays as it is.

## 8. State and authority

A peer-to-peer host is a player's machine. Anything that outlives an instance cannot be trusted to it alone.

| State | Lives | Authority |
|---|---|---|
| The map | generated data, on the client and the server | the generator |
| A squad's voyage record: course, legs flown, what is aboard the Ark, the Ark's hull | the backend | the backend, written at each hand-off |
| What happens inside an instance: grabs, cracks, the Ark's hull, an Ark's fall | the instance | the host, as the arena's lobby hull claim is today (`ARTIFACTS.md` rule 9) |
| An artifact changing hands (a fall, a pickup stowed) | the backend | **both squads' reports agree** (below) |
| The collection, track levels, crystals | the player profile | the backend (Shombith; `ARTIFACTS.md` §8.3) |

- **Escrow at departure.** When a squad sets sail, the artifacts it brings move from the collection into the voyage
  record. They are no longer "at home" until extraction puts them back.
- **Two witnesses for a transfer.** When an Ark falls to a raider, the host reports it, and the guest's client
  reports what it saw. The backend applies the transfer when they agree, and flags it when they do not. A host
  cannot invent a fall the other side did not see. This is the fair-play shape the AntiCheat work is already
  building for (`Docs/AntiCheat/README.md`); the details are Shombith's.
- **The networked Ark, and who hosts it.** Today the Ark is the local toy's: `ArkHold` and `ArtifactPickup` run with
  `IsAuthority` true, so whichever machine holds the Ark arbitrates (`ARTIFACTS.md`, top). Garrett wants the server to
  arbitrate every grab, transfer and spill. In a voyage the "server" is the **instance host** (§7.3): **every Ark in
  an instance, the guest squad's included, is a NetworkObject owned by the host**, rebuilt in each instance from the
  voyage record's hull count and hold. `ArkHold`, `PayloadCarrier`'s grab, `ArkFall` and `ArtifactPickup` then run
  only on the host, and clients ask by RPC, the shape of the arena's lobby hull claim. A guest squad therefore trusts
  the rival squad's machine for the life of the meeting, which is why anything that outlives the instance goes
  through the backend's two witnesses above. This is the "server-owned Ark" that `ARTIFACTS.md` §8.5 waits on.
- **Artifacts load in a voyage.** `VesselPilotLoadout` drops artifacts when `GameDataSO.IsArenaMatch` is set; the
  voyage mode leaves it off, so slotted artifacts load (Garrett, 2026-10-27: voyages allow artifacts, never require
  them).
- **Extraction is the main backend dependency.** The collection and artifact track levels are still device-local
  (`PilotLoadoutStore`); only the elemental crystals are on the profile. Until extraction, escrow and transfers write
  the collection server-side, theft and extraction can be faked by editing a device. That move to the profile is
  Shombith's (`ARTIFACTS.md` §8.3), and it gates phase V3.

## 9. Phases

Each phase is playable on its own and proves the riskiest thing left.

| Phase | What | Proves | Networking |
|---|---|---|---|
| **V0** | this document, answered | the shape | none |
| **V1** | **one squad, a small map, no strangers.** The crew's job board at port (courier, hunt, defence) with news simulated from authored events, the chart, 4 to 6 locations (harbour, vault field, Tandava's lair, a wreck), flown legs, extraction, the voyage record kept on the device | that a voyage is fun alone | the squad's own party only |
| **V2** | **the networked Ark and AI squads.** The Ark as a host-owned network object; AI squads met at arrival, placed by a GTC rule set that runs on the client | meetings and stakes, without a backend | party only |
| **V3** | **GTC on the backend; two real squads meet at arrival.** Flight plans, the pairing table, the hand-off (§7.3), escrow and two-witness transfers | the shared HyperSea | UGS sessions + Relay + backend |
| **V4** | **crossings, rendezvous with friends, the galactic war** (which domain holds each location, summed from instance results, shown on the chart) | the living map | as V3 |

V1 can start as soon as the questions in §11 are answered. It needs nothing from the backend.

### 9.1 V1, ready to build

What V1 needs, in build order. Each step is one branch with its own proof, and each reuses what §1-§8 name rather
than adding beside it. Claim every shared number (`GameModes` id, enum values) only after checking `bleeding-edge`
and the in-flight branches (`CLAUDE.md` § Team).

1. **The map as data.** `HyperSeaMapSO` (locations, lanes, regions; each location a `CellConfigDataSO` plus an
   experience) written by `Tools/Build/author_hypersea_map.py` (`--check`, `--self-test`), which also writes the
   server JSON. Start with one harbour per domain and the four V1 locations.
2. **The crew and the job board.** A crew pilot record (name, story, race, faction, callsign id) over the shipped
   callsign `PilotSO`s, one per callsign per crew, stored on the device beside `PilotLoadoutStore` until the backend
   holds it. A job seed table (faction type to job, §6.2) and a port news ledger fed from authored events (§6.3).
   The Voyage window shows three jobs. Every word on it is an authored field (the LOCKED text rule).
3. **The voyage mode and a flown leg.** One voyage `GameModes` id and controller that loads a location from the map,
   and a leg flown with the Ark through the lane's cells (`CellConveyor`, `ArkwayRun`'s corridor, drawn from the
   lane, not a shuffle bag). `GameDataSO.IsArenaMatch` stays off so artifacts load.
4. **Location experiences.** Harbour, vault field (vaults are new: super-shielded shells with an `ArtifactPickup`
   inside), Tandava's lair (Tandava's director inside a lair cell, after Shombith's yes on the Voyage window) and a
   wreck (an `ArkFall` spill placed on arrival).
5. **The voyage record and extraction.** Legs flown, what is aboard, the hull count, on the device. Docking at home
   moves what is aboard into the collection. A delivered dispatch writes the destination port's ledger.
6. **Hand tests and a ticket.** One Froglet Tasks ticket for the branch (suggested, approved by a person), with the
   editor hand tests as its checklist (`/verify-unity` gates every C# commit).

## 10. Who works where (ask before changing)

| Area | Whose | What this design needs from it |
|---|---|---|
| The Voyage window, the menus (`Docs/MENU_REDESIGN_B.md`) | Shombith's menu redesign | the chart in the Voyage window; Tandava moving from "Voyage 1" to a location |
| UGS, Cloud Code, the backend, fair play | Shombith (Yash does the Cloud Code work for now) | GTC, the voyage record, escrow, two-witness transfers (V3) |
| Multiplayer, party, Relay (`Ys-bleeding-edge`) | Yash's multiplayer programme | the squad-to-squad hand-off (§7.3), reconnection grace |
| AI | Yash | AI squads' voyage policy (§6.6) |
| The Ark, the Arkway, the ecology (`ECOSYSTEM.md` §41) | the Arkway's owner (check `who_works_where.py` before touching it) | the corridor drawn from a lane; the networked Ark |
| Pilots, artifacts, the payload (`claude/squad-payload-pilots-artifacts`) | Garrett's squad-layer build, in flight | vaults and extraction land here once V1 exists |

## 11. Open questions for Garrett

Each has a default the draft is written to; a different answer changes the section named.

1. **People per squad.** Is a squad 1 to 3 people with AI in the other seats (default), or always one person flying
   three vessels? (§2, §6.7)
2. **The 4-person cap.** Two squads of three people would be 6, and the multiplayer rules say never more than 4.
   Keep the cap, so a full squad of three only ever meets a solo squad or an AI squad (default)? Or should voyages be
   the one place that raises it? (§6.7)
3. **One fixed galaxy** that everyone shares (default), or a fresh map per season? (§3.1)
4. **Flown lanes, Arkway style** (default), or jumps on a map with the fights only at locations? (§4)
5. **Can a squad refuse a meeting?** The default is no: you see who arrived and what they came for, and you decide
   what to do about it. Opt-in PvP would make the pirate and courier jobs hollow. (§6.4)
6. **Abandoned voyages.** If the whole squad quits mid-voyage, is what is aboard lost (default), or returned home?
   (§4.3)
7. **The first five jobs** (courier, hunt, defence, pirate, bounty; escort dropped, haul is the courier contract).
   Right set, and is Notion's faction-type table still current? (§6.2)
8. **Faction size and ranks.** Is 50 the optimal faction size, and do the FigJam board's ranks (member, team leader,
   general, faction leader) still hold? (§6.2.1)
9. **Does the galactic war belong in voyages** (which domain holds each location, summed across all players), and is
   a domain the right side to fight for, or should squads belong to factions of their own? (§9, V4)
10. **Tandava** becomes the first lair location instead of "Voyage 1". That changes Shombith's Voyage window, so it
   needs his yes too. (§1, §5)
