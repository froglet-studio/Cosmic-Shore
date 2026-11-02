# The voyage vertical slice: one loop, every system, playable

**Status: BUILDING** (Garrett, 2026-10-28). Branch `claude/voyage-vertical-slice`. Nothing here has been opened in the
Unity editor yet; §6 is the hand-test list. The design is [`VOYAGES.md`](VOYAGES.md) (owned by the *Design voyages on
the hypersea* thread); this file is the slice that tests it, phase V1 of §9 cut down to one loop that uses everything.

> "work backwards from a voyage where we have working pilot vision powers, arks, payloads, a galactic map, a artifact
> we are finding, equipping and using ... the ability to build squads with all 12 configuration options having
> interesting choices that are implemented. and a voyage list that feels like traveling from one job to another
> stringing the stories of voyages together. chaining events from one job into the next ... fun, fulfilling and
> rewarding as a shared experience with friends and alone demonstrating limitless repeatability." (Garrett, 2026-10-28)

No factions yet (Garrett, same message). A pilot's **faction type** (merchant, hunter, pirate...) stays, because it
is what decides which jobs that pilot brings; joining, captains and emissaries wait.

## 1. The loop

1. **Build the squad** in the Ark's Squads tab (shipped): three vessels, each with vessel, element, pilot and artifact.
2. **Pick a voyage in the Voyage window** (the app shell, never the Toy Box or the world; Garrett, 2026-10-28: "the
   galaxy map should be in the app shell not the game. the jobs should be the different voyages"). Each pilot seated
   in the squad drums up one job from the news the squad's port holds, so the three pilots you seat decide the three
   jobs you are offered; each job is a voyage card in a grid under the **galaxy map** (the locations, the lanes, where
   you are; pan, zoom and tap it to choose where to go), with the way home as one more card when you are away. Opening a
   card expands it over the others and frames its course on the map (the window's layout: `VOYAGE_BOARD.md` §3).
3. **Set sail.** The game enters freestyle and your squad deploys: you fly your seat's vessel, friends who joined fly
   theirs, and every other seat is an AI wingmate flying its slot's vessel.
4. **(retired)** The in-world job board and the floating 3D chart were removed on 2026-10-28: Garrett could not tell
   the voyage from the Arkway with them in the world.
5. **Sail the leg.** The Ark flies the lane's cells (the Arkway's corridor, drawn from the lane instead of a shuffle
   bag). Fauna graze the hull, payloads come off it, sights find what matters.
6. **Arrive.** The destination's experience stands around the Ark: a vault field (super-shielded shells with an
   artifact inside, opened with a payload), a wreck (a fallen Ark's spilled artifacts), a contested core, or a port.
7. **Resolve the job.** Deliver the dispatch, open the vaults, hold the core. The outcome writes **news** (dispatches) to
   ports, and the news is what the next board is made of. Jobs chain: a delivered sighting becomes a hunter's job, a
   hunt that leaves a wreck becomes a salvage rumour, a convoy schedule becomes a pirate's tip.
8. **Choose the next voyage.** The banner sends you back to the Voyage window (leave freestyle); the squad is anchored
   at the job's destination with what it found still aboard and still at stake (`VoyageRules.Anchor`), and the window
   offers the jobs from there, grown from the news the outcome wrote. Docking at your harbour (the way home card)
   extracts: every artifact aboard joins your collection. An Ark that falls loses everything aboard (to the raider, or
   spilled for anyone); leaving freestyle mid-leg gives the voyage up and loses what was found.

Limitless repeatability comes from the chain, not from content volume: each outcome seeds new jobs at new ports,
vaults restock, and the board is always the three seated pilots crossed with the news that has reached this port.

## 2. Every one of the 12 cards does something on a voyage

| Card | What it changes on a voyage | Built |
|---|---|---|
| **Vessel** (x3) | the hull that seat flies: its kit, its role around the Ark | shipped hulls; seats spawn from the slot (§4 step 2) |
| **Element** (x3) | that vessel starts levelled in its element (the element system's own levels) | `ResourceSystem.SetElementLevel` at deployment |
| **Pilot** (x3) | that vessel's sight on LB, **and the job that pilot brings to the board** | sights in `PilotSightProviders`; jobs by the pilot's faction type |
| **Artifact** (x3, may be empty) | what that vessel's payloads become on RB; it rides in the Ark, so it is at stake | Javelin, Aegis, plus Hydra and Aftershock (§4 step 3) |

Sights that matter on a voyage and are built in this slice: CITADEL (vaults), GALLEON (Arks, wrecks and loose
artifacts), BANDIT and WINGMAN (who is near the Ark), the four crystal sights and OPAL, IVY, TALON and DOE (what will
eat the hull). ENSIGN, VOLT and FLUME were already built.

## 3. The slice's content (authored data, never C# literals)

- **The map**: one harbour per domain (Celadon, Carmine, Aurum), the Tollmoor port, two vault fields (Lantern Reach,
  the Hollow) and two cores (Verdigris, Saltmarch, where a wreck lies at the start), joined by lanes of 2 to 4 cells.
  Each sits on the real HyperSea, our Milky Way, at the Voyage Board's position (2026-11-01,
  [VOYAGE_BOARD.md](VOYAGE_BOARD.md)): the job cards draw the sea around them and the course an Ark sails round the reefs.
- **Pilot people**: one person per callsign (name, faction type, a line of story), layered on the 18 callsign pilots.
- **Job kinds**: courier (merchant), hunt (hunter), salvage (pirate), defence (aristocrat), and the way home.
- **Events**: what each outcome writes to which port's news, and which job seed each piece of news makes.

All of it is `Docs/Voyages/voyage_slice.json`, written into `Resources/Voyages/VoyageContent.asset` by
`Tools/Build/author_voyage_assets.py` (`--check`, `--self-test`).

## 4. Build order (each a pushed increment)

1. **Built.** The voyage core, pure C# (`Assets/_Scripts/Data/Voyages`): map, crew people, news ledgers, the board,
   outcomes and chaining, the voyage record, extraction and giving up, with edit-mode tests (`VoyageRulesTests`, run
   headless too).
2. **Built.** The squad drives the seats (`SquadDeployment`): each seat's pilot, artifact and element come from the
   deployed squad's slot, AI seats included; 15 of the 18 sights (RAMPART, HORNET and NEEDLE are still to build).
3. **Built.** Hydra and Aftershock (`Payload/Transforms`), the artifacts the vault fields and the seed wreck hold.
4. **Built.** The voyage in the world (`Controller/Voyages`): the **Voyage** toy, `VoyageRun` sailing the Arkway with a
   route (additive hooks in `ArkwayRun` and `CellConveyor`; with no route the Arkway is unchanged), the job beacons,
   the 3D chart beside the Ark, vault shells, wreck spills, the core hold, docking home, the fallen Ark, giving up.
   Saved on the device by `VoyageStore`.
5. **Built.** Friends: the host's course (embark, each leg with the host's galaxy and the leg's worlds, the end) is a
   Netcode named message (`VoyageCourseLink`), so no networked object is added to the scene. Each friend deploys their
   own seat and sails each leg with their own Ark, fauna and finds, and docks home with what they found; the board
   and the news are the host's. Nothing counts people.
6. **Hand tests** (`Docs/UNITY_VERIFICATION_CHECKLIST.md`, "The voyage slice") and a ticket (suggested, approved by a
   person).

## 5. Choices made for the slice (reversible, say if any is wrong)

- **It runs in freestyle, not a new mode scene.** Freestyle already has the party's network session, AI companions, the
  Ark, payloads and pickups. A voyage `GameModes` id and its own scene (VOYAGES.md §7.4) come once the loop is proven.
- **The board is the seated pilots' jobs**, one each, rather than the whole crew's: it makes the pilot card change the
  voyage. A recruited crew bigger than the squad comes with factions.
- **Friends sail one course, each machine runs its own Ark and fauna** (the Arkway plan's Phase 4 shape). The
  host-owned networked Ark is V2.
- **No fixed 4-person cap is built in.** Garrett, 2026-10-28: squad vs squad needs at least 6 people, three squads at
  least 9, more with multi-seat vessels; peer-to-peer limits constrain it for now. Nothing in the slice counts people.
- **Found artifacts are locked until found** (Garrett, 2026-10-28, on the decision card): Hydra and Aftershock
  (`ArtifactSO.FoundOnVoyages`) are held only once one has been docked home from a voyage
  (`PilotLoadoutStore.Grant`); every other artifact stays free (Shombith's rule).
- **Each seat's artifact rides in the Ark** from the moment it sails, so it is at stake with the finds: a raided or
  wrecked Ark takes it too. The player's collection keeps it (artifacts are free), the wreck carries a copy.
- **Each job is a voyage of its own** (Garrett, 2026-10-28): one card per job in the Voyage window, set sail, do the
  work, leave freestyle and pick the next. A job still open when the player leaves at the stop is done if its work is,
  left undone if not. Story lines and outcomes are the voyage banner.
- **AI wingmates escort the Ark**: one that ranges past the leash is brought back to its flank, and they are taken back
  when the voyage ends. Taking the helm of another seat is not built yet.

- **The Voyage window has one map, the Voyage Board's sea** (Garrett, 2026-11-01): the real Milky Way around Tumult,
  above the job cards; OPEN on a card expands its brief (squad, mission, intensity, pay) with Set sail and focuses the
  map on that course, cell by cell ([VOYAGE_BOARD.md](VOYAGE_BOARD.md) §3). The lanes' cell counts still
  set a leg's length in play; whether a leg should sail its real cells is open ([VOYAGE_BOARD.md](VOYAGE_BOARD.md) §5).
- **The Voyage window's third voyage is the job cards** (Garrett, 2026-10-28: "put it in as the third voyage", then "it
  is confusing that it is still in the toybox"): `VoyageJobsView` on the window's Voyages list stands one
  `VoyageJobCard` per job, copies of `MissionView.prefab`'s `Voyage3`, each with a `VoyageMapGraphic` in its art's
  place. The runner is `VoyageHarbour`, which `Toy_Voyage.asset` stands in freestyle with the toybox but which is no
  toy: no station, not in the Toy Box. Its words are the content's `hud` lines (`CardRoute`, `CardHomeRoute`,
  `CardUnderway`, `NoWork`, `ChooseNext`) and the map's colours are the spec's `toy` block.

## 6. Hand tests in the Unity editor

Kept in `Docs/UNITY_VERIFICATION_CHECKLIST.md`, section "The voyage slice", as each increment lands.

## 7. The Ark escort

A voyage of its own beside the jobs: any three vessels escort the Ark through five worlds at a difficulty from 1 to 9,
tuned by an AI sim. [ARK_ESCORT.md](ARK_ESCORT.md).
