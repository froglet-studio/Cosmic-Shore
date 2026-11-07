# Voyages, deep: the living HyperSea

**Status: BUILDING** (Garrett, 2026-11-07; branch `cece/magical-pascal-5uveyi`). This is the depth pass on the voyage
slice ([`SLICE.md`](SLICE.md)): what was shallow, measured; the systems that make a voyage worth coming back to; how
each one is built from the game's own fundamentals (never a scripted event); and the scorecard that measures it. The
plan it serves is [`VOYAGES.md`](VOYAGES.md); nothing here changes a decision recorded there. Numbers marked
**(default)** are the author's and wait for Garrett.

> "we are not going deep enough on voyages. we must stretch ourselves to make the most fun and replayable experiences
> possible that connect to stories, allow players to extract unique artifacts, and bring them back over and over again
> to try new vessels on new adventures with their friends. right now it is still very shallow." (Garrett, 2026-11-07)

## 0. What was shallow, measured

The slice that shipped on 2026-10-28 (`voyage_slice.json`, `VoyageRules`) is one clean loop, and the loop is thin:

| | The slice | Why it reads shallow |
|---|---|---|
| Places | 8 (three harbours, one port, two vault fields, two cores) | every voyage is to one of five destinations |
| Experiences | 4 (harbour, port, vault field, core) | a vault field is the only find; a core is a timer |
| Jobs | 6 templates from 4 of 16 faction types | 12 pilot cards bring no work at all |
| News | 3 tags (sighting, wreck, aid); 6 events | every chain writes "vaults are lit" or "a core asks for help" |
| Finds | 2 artifact kinds (Hydra, Aftershock), restocked on every visit | nothing is rare, nothing is named, nothing is once |
| The world | changes only when the player acts | nobody else sails; the galaxy waits |
| Story | a 40-line log | no arc, no payoff, no rival, no memory |
| Risk | the stake aboard, lost on a fall | no one hunts a laden Ark; the way home is never contested |

The depth harness (§11) measures the same thing in numbers. On the old content, 40 galaxies of 60 voyages each (2,329
jobs) offered 8.6 distinct (job, destination) pairs in a crew's first 20 voyages, grew 23% of jobs from news, gave work
to 3 of the 16 faction types, met nobody, found no relic, and sailed to eight places, three of them the harbours.
The same harness on this pass's content is §15.

## 1. The thesis

**Depth is a product, not a sum:** a world that moves without you, times rivals who want what you want, times choices
with teeth, times arcs with payoffs you own, times a crew that remembers. Each factor is a system of its own, authored
as data, run by pure rules an edit-mode test can hold, and composed from fundamentals the game already has (domain,
mass, cells, elementals, flora and fauna, vessels, the Ark). None of them scripts a location. Together they make the
HyperSea a place with weather, neighbours, history and treasure, where the same twelve cards make a different voyage
every time and a different squad finds different work.

The design philosophy holds throughout (`Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md`): a location gets an
arrangement and the fundamentals play it out; nothing is minted (every piece of news is born from an event);
universality (one HyperSea, one rule set, rivals included: they take jobs from the same templates by the same rules);
PvP is petals only; a game mode never changes a vessel. Every word a player reads is authored (`voyage_slice.json`).

## 2. The living galaxy

Every location carries **live state** (`LocationState` on the saved record, repaired by `Normalize`):

| Field | Range | What it is in play |
|---|---|---|
| `Pressure` | 0-3 | how hostile the place is now; adds to the course's intensity for the board, and at 2 a core calls for aid |
| `Holder` | a domain or none | who holds a core's nucleus; another domain's hold means its waves wear that colour and graze your hull (the escort's `SetReplicatedStartingController` rule) |
| `Bloom` | 0-3 | how rich the cell is in crystals; a nursery at 3 is a harvest |
| `Sealed` / `RestockDay` | n / day | how many vault shells still stand, and when a picked field is lit again |
| `Charted` | bool | whether this crew has found the place (§8) |
| `Visits`, `LastVisitDay` | | the logbook's half |

**The tick.** Every day (a job taken, an escort berthed, or a day that passed while the player was away, at most
five at a time) the galaxy moves by rules with authored rates (`voyage_slice.json` `world`):

- pressure drifts toward a place's authored `restPressure`, and rises where a rival crew failed or a hold was lost;
- blooms grow a step every `bloomDays` at nurseries and are spent by a harvest;
- a picked vault field restocks after `restockDays`, and **the restock is news** (a sighting at the nearest port);
- a wreck that nobody strips is grazed down: after `wreckDays` its artifacts are gone (a lost Ark that nobody
  salvages is lost, as `ARTIFACTS.md` §5 says of uncollected pickups);
- rival crews sail and resolve (§3), and what they do is news by the same events the player's jobs write.

Every roll is a stable hash of the day and the thing rolled (`VoyageRules.Hash`), so the same galaxy on the same day
is the same on every machine and in every test, and a friend following the host's course sees the host's galaxy.
"Nothing is minted" is kept: a sighting exists because a field restocked, a wreck because an Ark fell, an aid call
because pressure crossed a line, a harvest because a bloom ripened.

## 3. Rival crews

The HyperSea has other squads in it from the first voyage. A **rival crew** is authored data (`rivals`): a name, a
faction type and domain, a home port, three hulls, a strength (1-3), a temperament (bold, careful or greedy) and its
lines. Each has live state on the record: where it is, where it is bound and when it arrives, what it holds, its
notoriety, its grudge against the player's crew, and whether it lies wrecked.

- **Rivals take the same jobs by the same rules.** An idle crew at a port draws from the same templates the player's
  pilots draw from, over the same news ledger, biased by its temperament (bold crews take the roughest course, greedy
  the best pay, careful the nearest). It sails a day a lane. On arrival it succeeds with a chance set by its strength
  against the course's intensity and the place's pressure, and **its outcome writes the same events**: a rival
  hunter that opens the vaults spends the sighting before you get there; a rival courier carries news to a port and
  changes that port's board; a rival that fails at intensity 3 may fall, and its fall is a wreck and a salvage
  rumour. The galaxy's news is now mostly other people's doing, as Garrett's §6.3 asks.
- **Meeting one.** When the player's Ark arrives where a rival stands, the arrival names them and the **relation**:
  **co-op** (your domain), **race** (another domain, both after the finds) or **hostile** (a pirate with a tip on your
  laden Ark, a bounty hunter on your notoriety, or a grudge of 2 or more). In the world their three hulls stand as AI
  vessels in their domain. Nothing new is scripted for them: another domain's vessels destroy your mass by the arena
  rules the AI already plays, and the Ark's hull is your mass, so a hostile crew near the Ark is a real threat to
  everything aboard, and a co-op crew is three more wingmen. At a vault field a race is a race: every
  `rivalVaultSeconds` the rival opens a shell you have not, and what they take is theirs.
- **Grudges and bounties.** Beating a crew to a race or stripping a wreck it claimed raises its grudge; a grudge of 2
  sends it after you (its next job is wherever you are bound). Your notoriety (§6) at 3 or more puts a bounty on you
  at every port's news, and bounty-hunter crews read it like anyone else.
- **What waits for V2.** A rival's Ark in the world, with its hold at stake, is the host-owned networked Ark of
  `VOYAGES.md` §8; a rival squad with people in it is galactic traffic control (§6-7). Until then a rival's fall is
  resolved by the tick, and its vessels, not its Ark, stand in the world.

## 4. Work verbs: a job for every faction type

A job's work is one **verb** with a **goal**, and the world observes it with what it already measures:

| Verb | Done when | Observed by |
|---|---|---|
| `Arrive` | the Ark reaches the destination standing | the route's end (couriers, the way home, surveys, parleys) |
| `Stow` | `Goal` artifacts stowed here | `ArkHold.Artifacts` (hunts, salvage, hoard raids) |
| `Hold` | the pilot stays within the hold radius of the Ark for `Goal` seconds | `TickCore` (defence, patrol, rites, retaking a core) |
| `Gather` | the squad collects `Goal` crystals since arrival | the seats' `IRoundStats.CrystalsCollected` (harvests, exhibition runs) |
| `Cull` | `Goal` hostile creatures fall near the Ark | `Fauna.Killed` within the guard radius (lair hunts, clearing a derelict) |
| `Lay` | the squad lays `Goal` prisms in the cell | `IRoundStats.BlocksCreated` (a contractor's seawall) |
| `Raze` | the squad destroys `Goal` volume of hostile mass | `IRoundStats.HostileVolumeDestroyed` (liberating a pen) |
| `Observe` | the pilot keeps a living creature within reach for `Goal` seconds | the cell's live fauna (a researcher's study) |
| `Meet` | the Ark arrives while the named rival stands there | the arrival's rivals (bounties) |

Two modifiers: `HullFloor` (a convoy must arrive with the hull above a share) and `NoKill` (a preservationist's tend
is undone by a kill). `WorkDone` is one switch over the verb, so a new kind of work is a template, not a controller.

Every faction type now brings work, each in its own verb and place:

| Type (domain) | Standing work | News-grown work | Verb, where |
|---|---|---|---|
| Merchant (Gold) | courier: carry a dispatch | convoy: a cargo contract to a port under pressure | Arrive at a port; HullFloor on the convoy |
| Hunter (Ruby) | a rumour of vaults | a sighting; a lair hunt (a `Lair` is a cell whose fauna are predators at pressure) | Stow at a vault field; Cull at a lair |
| Aristocrat (Jade) | patrol a core | answer a call for aid; retake a core another domain holds | Hold at a core |
| Pirate (Ruby) | raid a hoard (the thieves' stash) | strip a wreck | Stow at a hoard or wreck |
| Bounty hunter (Gold) | | a bounty on a notorious crew | Meet the crew where it stands |
| Explorer (Ruby) | survey an uncharted place | follow a chart fragment | Arrive; the place is charted (§8) |
| Activist (Ruby) | | liberate a pen (fauna held behind another domain's mass) | Raze at a pen |
| Thief (Ruby) | lift a dispatch from a rival port | | Arrive at a port; the freshest news there is yours to carry |
| Gatherer (Gold) | | harvest a bloom | Gather at a nursery |
| Contractor (Gold) | | shore a breach: a core under pressure | Lay at a core; pressure drops |
| Sport (Gold) | an exhibition run: skim the course | | Gather on the way, anywhere |
| Researcher (Jade) | study a creature | study a bloom | Observe at a lair or nursery |
| Preservationist (Jade) | tend a nursery | | Hold at a nursery, NoKill |
| First responder (Jade) | | rescue a stranded crew at a derelict | Hold at a derelict; its crew comes aboard |
| Priest (Jade) | a vigil at a shrine | the ninefold rite (a saga) | Hold at a shrine |
| Diplomat (Jade) | | carry an accord: a parley between a port and a crew | Arrive at a port; the rival's grudge eases |

(Sport's place is the Arena, `SOURCES.md`; the exhibition run is the one voyage a sport pilot brings, and it asks
nothing of a vessel's kit: skimming crystals is the universal mechanic.) **Standing tiers (§6) widen the work:** a
Known hunter's hunt stands two vaults, a Trusted one's three, and a saga chapter (§5) is offered only at the tier its
author set.

## 5. Sagas and relics

A **saga** is a chain of chapters, each a piece of news that becomes a job, authored in `sagas`. A chapter names the
faction type whose pilot must be seated to find it, the standing tier that unlocks it, the job it becomes (any verb),
the kind of place it goes to (or one named place), its news line, and the chapter it opens on success. The chain is
the same news-and-event machinery every job runs on; what a saga adds is **a thread**: the same story across several
voyages, found by seating different pilots, ending in a **relic**.

A **relic** is a unique artifact that exists once in a crew's galaxy: a kind (Hydra, Aftershock, Javelin, Aegis), an
authored name and lore, and a fixed seed, so its id is the same for every player (`artifact.hydra#<saga seed>`) and the
account inventory (`Docs/Pilots/INVENTORY.md`) rolls its numbers from that seed as it does any found artifact. The
finale's place holds the relic in a vault for the arriving squad; it is stowed, carried home at stake like any find,
and named in the Ark's hoard by the saga, not by the generator's epithets. A relic found is recorded on the voyage
record, and the saga does not run again (**(default)**; a season could reseed it).

The first four sagas, one per domain and one for the pirates, each asking three different pilots:

| Saga | Chapters (the pilot it asks for) | Relic |
|---|---|---|
| **The Lantern Keeper** | a hunter's sighting at Lantern Reach opens a vault with half a chart; an explorer follows it to the uncharted Keeper's Rest; a priest's vigil there lights the lantern | *Lantern of Oriel* (Hydra) |
| **The Salt Accord** | a merchant carries the first word of a truce to Tollmoor; a diplomat carries the accord to the Red Reef Rovers; an aristocrat holds Saltmarch while it is signed | *Seal of Still Water* (Aegis) |
| **The Tide That Remembers** | a researcher studies the Nursery of Verdigris; a preservationist tends it through a bloom; a gatherer brings the bloom's first crystals to the Deepreed shrine | *Memory of the Reef* (Aftershock) |
| **The Grin's Tooth** | a pirate strips the Rusted Grin's own wreck; a thief lifts their chart from Gullbone Landing; a bounty hunter meets what is left of the crew at the Hollow | *Grin's Tooth* (Javelin) |

Sagas are authored data, so a fifth is a JSON entry. The generator refuses a chapter that opens nothing and no saga
that no squad could ever finish (a chapter asking a faction type with no pilot).

## 6. Standing and notoriety

- **Standing** with each faction type lives on the record: a success for that type's pilot adds one, a failure takes
  one away (never below zero). Tiers, **(default)**: Stranger 0-1, Known 2-4, Trusted 5 and up. Standing widens the
  work (§4), unlocks saga chapters (§5), and raises pay (`standingPay` per tier). The real roster, where standing is
  faction membership with ranks (`VOYAGES.md` §6.2.1), is the backend's; this is its device-side stand-in, as the
  voyage record itself is.
- **Notoriety** rises with pirate, thief and bounty work and with every rival wrecked, and decays one step every
  `notorietyDecayDays`. At 3 a bounty on the crew is posted at every port (visible to the player as news about
  themselves), and bounty-hunter crews answer it. Notoriety is the Ruby currency of the Notion *Mission Overview*
  (`SOURCES.md`), built as the design asked.
- **Grudges** are a rival's standing with you, in the other direction (§3).

## 7. Risk, reward and the way home

- **Pay follows the course and the standing.** A job's pay is its template's base times the course's intensity
  multiplier times its hops multiplier times the standing tier's, all authored (`economy`). The board shows the pay as
  it did; the far and the rough pay more.
- **A laden Ark draws pirates.** Once two or more artifacts are aboard, the nearest port's news carries a **rumour**
  about your Ark, and pirate crews read rumours as tips. The player can read it too: the stake is visible and so is
  the hunt for it. Going home is a decision made against that.
- **Pressure is weather.** A place at pressure 3 is a harder arrival than the sea alone says, and the board's
  intensity shows it; a crew that cannot afford a fight can wait a day for it to ease, or go elsewhere.
- **Everything else stays as decided.** The stake aboard is lost on a fall or an abandoned voyage; only docking at
  home extracts; found artifacts are locked until found (`SLICE.md` §5).

## 8. Discovery: the chart grows

Some places are **uncharted**: on the map and the board they do not exist until this crew finds them. A place is found
by an explorer's survey (standing work that goes to the nearest uncharted place a lane reaches), by following a chart
fragment (news a vault chart or a saga writes), or by sailing into it on a saga. Charting writes news of the place at
the nearest port, and from then on every pilot's work can send the squad there. The map grows by adding places, never
by reshaping them (`VOYAGES.md` §3.2). The first uncharted places: the Keeper's Rest, the Pale Anchorage and the
Cinder Shoal.

## 9. The crew remembers

- **Chronicles.** A pilot whose job succeeds gets a line in their chronicle (`PilotRecord.AddChronicle`, the
  backstory that grows with key events), written from an authored template: "Opened the vaults at Lantern Reach."
  The crew's people carry the voyages they led.
- **The logbook.** The record counts voyages sailed, legs, finds extracted, relics, Arks lost, places charted, rivals
  wrecked, and each place's visits and first visit. The story log keeps its last lines as before.

## 10. In the world

`VoyageRun` grows a verb watcher and a rival stand; the Arkway, the Ark, the payload and the vaults are unchanged.

- **Verbs.** On arrival the run marks each seat's round stats (`GameDataSO.TryGetRoundStats`) and the Ark's hull; it
  then counts crystals gathered, prisms laid, hostile volume razed, creatures culled within the guard radius
  (`Fauna.Killed`), the pilot's seconds within reach of a living creature, and seconds held, and asks
  `VoyageRules.WorkDone` with the tally. Progress is a banner line from authored templates (`HudProgress`).
- **Rivals.** A rival at the stop stands its three hulls as AI vessels in its domain (`RequestSpawnAiCompanion`, the
  Spawn Matrix's own chain), abeam of the Ark at the find distance, and takes them back when the squad sails on. The
  authored line of its arrival is the banner. A race's clock retires vault shells the rival opens.
- **Wingmates.** Unchanged in this pass: the escort roles (`IArkEscortRole`) are bound to `ArkEscortRun`'s context,
  and the escort files have Garrett's night branches in flight; lending the roles to a voyage's wingmates is the next
  runtime step (§12), done through an interface the escort implements, never by copying a role.
- **Every line is authored.** The new banners, progress lines, relation lines and chronicle templates are fields of
  `voyage_slice.json`'s `hud`, written into `VoyageLines` by the generator.

## 11. Measuring depth: the harness and the scorecard

`Tools/Build/voyage_depth_harness/` compiles the pure voyage data and rules with the edit-mode suites against a Unity
stub and runs them with NUnitLite (the `crystal_depth_harness` pattern), and `VoyageDepthHarness` plays thousands of
voyages headless: random squads of three pilots, a policy that takes the best-paying job six times in ten and any job
otherwise, outcomes rolled against intensity, the galaxy ticking between voyages. The scorecard it prints is also a
test, so a content change that flattens the game fails before it ships. The bars **(default)**:

| Measure | Bar | Why |
|---|---|---|
| boards away from home with no faction work | under 3% | a dead board is the loop stalling |
| distinct (job, destination) pairs in a crew's first 20 voyages | at least 12 | variety a player feels |
| share of jobs grown from news rather than standing work | at least 45% | the chain is the game |
| rival events (a job resolved, a wreck, a bounty) per 10 days | at least 4 | the world moves without you |
| galaxies in which a random crew finishes a saga within 60 voyages | at least 70% | the arcs are reachable |
| relics granted twice in one galaxy | 0 | once means once |
| news items alive at a port | at most 24 | boards stay legible |
| every faction type offering work at a harbour on day 0 | 16 of 16 | every pilot card matters from the start |
| every location reachable, every uncharted place chartable | all | no dead content |
| authored text: non-empty, ASCII, tokens known | all | the LOCKED text rule |

The old content's numbers (§0) are the baseline; the new content's are recorded in §15 as they are measured.

## 12. Phases

| Phase | What | This branch |
|---|---|---|
| D1 | the living galaxy, rivals, verbs for every faction type, sagas and relics, standing and notoriety, charting, the logbook, the harness | **built here** |
| D2 | wingmates that escort by the escort roles through a shared host interface (`IArkEscortHost`); authored line variants so no two outcomes read the same; the crew's standing and logbook on the open card | this branch, second commit |
| D3 | rival Arks in the world (the host-owned networked Ark, `VOYAGES.md` §8) and real meetings (GTC, §6-7); standing on the backend as faction membership | Shombith (backend), Yash (multiplayer, AI) |

## 13. Who works where (and what this touches)

| Area | Whose | What this pass does there |
|---|---|---|
| Voyages: rules, content, the run | Garrett (this branch) | everything above |
| The Voyage window (`VoyageJobsView`, `VoyageMapGraphic`) | Shombith | the map and the board skip uncharted places through one rule (`VoyageRules.IsVisible`); no layout change. **Affects: Shombith's Voyage window** |
| The account inventory (`PilotLoadoutStore`, `PilotRecord`) | the squad layer (Garrett) | chronicle lines on a job's success; a relic's authored name and lore on the granted record |
| The Ark escort (`ArkEscort*`) | Garrett's night branches in flight | untouched |
| AI companions (`RequestSpawnAiCompanion`) | the toybox / multiplayer | reused as the Spawn Matrix does, no change |

## 14. Decisions for Garrett (defaults taken)

1. **Tiers.** Stranger 0-1, Known 2-4, Trusted 5+ (default). Different thresholds change §6 only.
2. **A relic is once per galaxy** (default), or reseeded by season.
3. **Rivals share the player's templates** (default), so a rival's work is exactly a pilot's; or rivals get a smaller
   list of their own.
4. **The bounty on the player posts at notoriety 3** (default).
5. **Days pass while away** (default: real days since the last save, at most five ticks), or the galaxy waits.
6. **The thief's lift** takes a port's freshest news (default) rather than copying it.
7. **Sport's exhibition run** stays a voyage (default), or sport pilots bring nothing and belong to the Arena alone.
8. **The board is about six** (default, the 2026-10-30 meeting): the seated pilots' jobs lead it, one each, and the rest
   of the crew fills it with its most interesting work (a saga chapter first, then news-grown work, then by standing).
   The slice offered the seated three alone.
9. **A saga's chapter stands at every port** (default), so the crew reads its own story wherever it docks; or only at
   the port nearest the last chapter's place, which the harness showed loses most crews the thread.
10. **A new galaxy is awake on day 0** (default): the hoard is stocked, the pens and distress calls are heard, the
    rival crews are moving when the squad first looks. Or the sea is still until the first job is taken.
11. **Every faction type is recruitable** (default, since every type brings work): new pilots are minted into all
    sixteen, not the four of `INVENTORY.md` §3.3.

## 15. Measured

`Tools/Build/voyage_depth_harness/run.sh --scorecard 60 200`, 2026-11-07: 200 galaxies x 60 voyages (11,457 jobs,
12,000 days), the same policy on both contents.

| Measure | Bar | The slice (2026-10-28) | This pass |
|---|---|---|---|
| boards with no faction work | under 3% | 0% | 0% |
| distinct (job, destination) pairs in the first 20 voyages | at least 12 | 8.6 | 15.4 |
| jobs grown from news | at least 45% | 23% | 75% (2,904 of them saga chapters) |
| rival events per 10 days | at least 4 | 0 | 28.3 |
| galaxies in which a crew found a relic within 60 voyages | at least 70% | 0% | 99% (529 relics, none twice) |
| news alive at a port, at most | 24 | 4 | 17 |
| faction types with work at a harbour on day 0 / ever offered | 16 / 16 | 3 / 4 | 16 / 16 |
| places sailed to | | 8 | 25 (every one) |
| meetings with another crew | | 0 | 1,570 (539 co-op, 267 races, 90 hostile) |
| the Kaiju met / felled | | 0 / 0 | 713 / 283 |
| Arks lost | | 1.8% of jobs | 4.8% of jobs |
| finds extracted per docking | | 0.61 | 2.50 |

What the numbers say: a crew that sails sixty voyages now sees every place, every kind of work and other crews, finds a
story's end about every twenty voyages, and loses an Ark about once in twenty. The verb mix (holds 29%, stows 22%,
arrivals 14%, culls 11%, razes 8%, gathers 6%, studies 5%, meets 4%, lays 1%) says the Guardian and Destroyer roles have
the most to do and the Cultivator the least; the lay goals and the breach rate are the first numbers to tune by play.
Intensities ran I1 6%, I2 41%, I3 35%, I4 18%: Tumult's water is thick, and the pay table says so.
