# Voyages — where it stands (report for Garrett)

**From:** this branch's session, 2026-11-07. **Branch:** `cece/magical-pascal-5uveyi` (the depth pass). The slice,
the Voyage Board's sea in the window, the deep-zoom map, the four-tab window and the unique pilots and artifacts are
all on `bleeding-edge`. **Status:** built and measured headless (38 edit-mode tests green outside Unity, the scorecard's
bars all hold, the reference compile has no errors in the changed files). **Nothing in this pass has been played in the
Unity editor yet.** That is the next step (section 4).

## 1. In one minute

- **The HyperSea lives without you.** Every place has pressure, blooms, restocks and visits; the galaxy ticks a day on
  every job taken, every escort berthed and every day away (up to five). Cores call for aid when pressure crosses a
  line, picked vault fields are lit again and that is news, unclaimed wrecks are grazed away, the Leviathan is sighted
  at a lair and every port knows it; fell it and the next one is worse.
- **Nine rival crews sail the same sea by the same rules.** They draw jobs from the same templates over the same news,
  by temper (bold, careful, greedy), and their outcomes write the same events: a hunter crew spends the sighting before
  you reach it, a courier crew changes a port's board, a crew that fails in rough water falls and leaves a wreck. Meet
  one at a stop and its three hulls stand there in its domain: a co-op crew of your colour, a race for the finds, or a
  hostile crew come for a laden Ark or a notorious one. A crew that takes your Ark is promoted and remembered.
- **Every one of the 16 faction types brings work**, each in its own verb and place: couriers and convoys, hunts and
  culls, holds and retakes, salvage and hoard raids, bounties, surveys of uncharted places, liberating pens, lifting a
  rival harbour's news, harvesting blooms, shoring breaches, exhibition runs, studies, tending nurseries without a
  kill, rescues at derelicts, vigils at shrines, parleys. Nine verbs the world already measures; a new job is a template.
- **Four sagas end in relics**, each asking three different pilots across several voyages: the Lantern Keeper (a Hydra,
  Lantern of Oriel), the Salt Accord (an Aegis, Seal of Still Water), the Tide That Remembers (an Aftershock, Memory of
  the Reef), the Grin's Tooth (a Javelin). A relic exists once in your galaxy, rides in the Ark at stake, and is named by
  its saga in the Ark's hoard.
- **Standing and notoriety.** Each faction type trusts you more with every job done; trust widens the work, unlocks a
  saga's later chapters and raises pay. Pirate, thief and bounty work makes you notorious; at three a price is on your
  Ark at every port and bounty hunters answer it. Two finds aboard and the pirates hear of it.
- **The board is about six**: your seated pilots' jobs first, the rest of your crew's after, priced from one table
  (base x intensity x lanes x standing, fading with the news's age), with warnings from the world as it is.
- **The galaxy grew** from 8 places to 25 around Tumult, three of them uncharted until you find them.
- **Your AI wingmates fight for the Ark** by the Ark escort's own roles (the Sparrow destroys, the Squirrel
  cultivates, the Dolphin, Serpent and Scarab theirs), through a host interface the escort and the voyage share.
- **No two outcomes read the same**: the lines a crew reads most have several ways of saying it, picked by the day and
  the place. The open card shows how far each pilot's faction trusts you and your crew's own log.
- **Measured** (`Tools/Build/voyage_depth_harness/run.sh --scorecard 60 200`): distinct (job, destination) pairs in a
  crew's first 20 voyages 8.6 -> 15.4; jobs grown from news 23% -> 75%; faction types with work 3 -> 16; rival events
  0 -> 28 per 10 days; galaxies with a relic in 60 voyages 0% -> 99%; meetings with another crew 0 -> 1,570.

## 2. Try it

1. Pull `cece/magical-pascal-5uveyi`, open Unity and wait for the compile. Run `VoyageRulesTests` and `VoyageDepthTests`.
2. Menu_Main > the Voyage window. Expect about six voyage cards and 25 places on the map in six colours; Cinder Shoal,
   the Pale Anchorage and Keeper's Rest are not drawn until you find them. Open a card: SUITS a role, PAYS a price that
   grows with the course, and the world's warnings.
3. Follow the Lantern Keeper: a hunter's card whose news names half a chart at Lantern Reach, then an explorer's to
   Keeper's Rest, then a priest's vigil once the Priest type is Known. The relic comes aboard; dock at home to keep it.
4. Sail where a rival crew is bound (the card says so) and meet them; let a laden Ark fall to a pirate crew and read the
   story. The full hand-test list is `Docs/UNITY_VERIFICATION_CHECKLIST.md`, "2026-11-07 — Voyages, deep".

## 3. What changed in the game (Unity)

| What | Files |
|---|---|
| The living galaxy, the rival crews, the sagas, the verbs, standing and notoriety, charting, the logbook: pure rules | `Data/Voyages/VoyageWorld.cs`, `VoyageRivals.cs` (new), `VoyageRules.cs`, `VoyageContent.cs`, `VoyageState.cs`, `VoyageEnums.cs` |
| The deep galaxy content: 25 places, 40 lanes with pacing profiles, 32 job templates, 29 events, 9 rival crews, 4 sagas, the Kaiju, the world's rates, every line | `Docs/Voyages/voyage_slice.json` -> `Assets/Resources/Voyages/VoyageContent.asset` and the twin `Docs/Voyages/voyage_content.json` (`Tools/Build/author_voyage_assets.py`, 43 planted defects caught) |
| The run: verbs counted from the game's own stats, the leg's pacing, hostile worlds, rival hulls, the race clock, chronicles, relics | `Controller/Voyages/VoyageRun.cs`, `VoyageHarbour.cs` |
| The window: `{role}`, `{warnings}`, the job's own pay and intensity, a sea-today beat, uncharted places hidden | `UI/Views/VoyageJobsView.cs`, `VoyageMapGraphic.cs`, `VoyageEdgeGraphic.cs`, `VoyageMapSky.cs` |
| The headless harness and the scorecard as a test | `Tools/Build/voyage_depth_harness/`, `Tests/Editor/VoyageDepthHarness.cs`, `VoyageDepthTests.cs` |
| Every faction type recruits | `Data/Inventory/FactionCatalog.cs` |
| The escort roles' host interface, implemented by the escort and the voyage; wingmates armed as escort pilots | `Controller/Voyages/IArkEscortHost.cs` (new), `ArkEscortContext.cs`, `ArkEscortPilot.cs`, `SquirrelRole.cs`, `ArkEscortRun.cs` (one line each), `VoyageRun.cs` |
| Line variants, the standing token and the log beat | `VoyageContent.cs`, `VoyageRules.PickLine`, `VoyageJobsView.cs`, `VoyageContentSO.cs` |

## 4. What is untested

- Nothing has run in the Unity editor. The world-side verbs (gather, lay, raze, cull, observe) read `IRoundStats` and
  `Fauna.Killed`; their goals (a raze of 1,500 volume, a lay of 120 prisms, a cull of 5) are first guesses to tune by play.
- Rival hulls at a stop are AI companions in the rival's domain; what they do to the Ark is whatever the arena AI does
  to another domain's mass. Their own Ark waits for the host-owned networked Ark (V2).
- The race clock retires a shell every 45 s while a racing crew stands; the number is authored (`world.rivalVaultSeconds`).
- The friend's course message carries the whole record (bigger now): fragmented delivery is used, the size is not measured.

## 5. Decisions waiting on Garrett

`Docs/Voyages/DEPTH.md` §14: tiers (2 and 5), relics once per galaxy, rivals on the player's templates, the bounty at
notoriety 3, days away ticking the galaxy, the thief's lift taking news, sport's exhibition run, a board of six from
the crew, chapters posted at every port, a galaxy awake on day 0, every type recruitable.

## 6. Who is working here, what landed, the open tickets

This block is refreshed by `python3 Tools/Build/team_report.py area voyages --write`. Any Claude session asked for
a report on voyages rebuilds it from git and the team-task issues, never from memory (`Docs/TEAM.md` rule 10).

<!-- team_report:begin (generated by Tools/Build/team_report.py; do not edit) -->
_Generated 2026-11-07 from git (last 14 days), who_works_where.py and the team-task issues: `python3 Tools/Build/team_report.py area voyages --days 14`._

### Recent changes (25+)
- 2026-11-07 `f98b07383` feat(pilots): unique pilots, unique artifacts and the account inventory — on cece/magical-pascal-5uveyi, bleeding-edge
- 2026-11-02 `357dd6460` fix(voyages): ship review of the Voyage window - same-frame RB, idle poster rebuilds, Ark crew draft — on cece/magical-pascal-5uveyi, bleeding-edge
- 2026-11-02 `e3f105c23` feat(voyages): Voyage window in four tabs with the Rev 49 C open voyage card — on cece/magical-pascal-5uveyi, bleeding-edge
- 2026-11-02 `7a9332353` feat(voyages): readable galaxy-map names at every zoom, and the 20 ly cells — on cece/magical-pascal-5uveyi, bleeding-edge
- 2026-11-02 `a1922d789` feat(voyages): the galaxy map zooms like the Voyage Board - stars, clusters, landmarks, tiers, cards — on cece/magical-pascal-5uveyi, bleeding-edge
- 2026-11-02 `f3863c635` feat(voyages): Voyage Board deep zoom to Sol and Tumult, lanes by heading, course profile — on cece/magical-pascal-5uveyi, bleeding-edge
- 2026-11-01 `94c1050bf` feat(voyages): the Voyage window as Garrett sketched it - Saga | Episodes, a galaxy explorer, the voyage grid — on cece/magical-pascal-5uveyi, bleeding-edge
- 2026-11-01 `e322950b0` feat(voyages): Ark escorts sail between real places on the HyperSea — on cece/magical-pascal-5uveyi, bleeding-edge
- 2026-11-01 `8b250da40` fix(escort-sim): close the privacy overlay through its own Finish so the waiting boot resumes — on another branch
- 2026-11-01 `a678d09f1` fix(escort-sim): close the first-run privacy overlay unanswered so a sim (and the smoke test) can boot — on another branch
- 2026-11-01 `74640bba2` docs(voyages): ark feel pass write-up (alarm, lighting, solid hull, leash shell, cell shards, sims, shots) — on cece/magical-pascal-5uveyi, bleeding-edge
- 2026-11-01 `7c6fdcb9b` fix(escort-sim): close the first-run privacy overlay unanswered so a sim can boot — on cece/magical-pascal-5uveyi, bleeding-edge
- 2026-11-01 `22d31eb77` test(voyage): the voyage scene is built, listed and carries no freestyle; docs — on another branch
- 2026-11-01 `9f93e4f5b` feat(escort-sim): each sim run sets sail into the voyage scene; -voyageSmoke play test — on another branch
- 2026-11-01 `9f66cbc98` feat(voyage): voyages launch into their own scene, not freestyle — on another branch
- 2026-11-01 `469b515e2` feat(voyages): one galaxy map in the Voyage window; job cards open to their brief — on cece/magical-pascal-5uveyi, bleeding-edge
- 2026-11-01 `51e455dc9` tool(escort): -arkEscortShots renders the Ark, tunnel and leash shell to PNGs from a sim — on cece/magical-pascal-5uveyi, bleeding-edge
- 2026-11-01 `d8661e21d` feat(escort): each escort world runs its own cytoplasm pointing at its own crystal — on cece/magical-pascal-5uveyi, bleeding-edge
- 2026-11-01 `6a60c47a4` feat(voyages): the Voyage Board's real Milky Way sea on the job cards — on cece/magical-pascal-5uveyi, bleeding-edge
- 2026-11-01 `e8e3c6164` docs(escort): chaos pass write-up (regrow gate, lure, bite floors, ladder, sims) — on cece/magical-pascal-5uveyi, bleeding-edge
- 2026-11-01 `9909b3db8` tune(escort): armour bite floors per tier; regrow only in pacified (or still-friendly) nuclei — on cece/magical-pascal-5uveyi, bleeding-edge
- 2026-11-01 `47a6e2a2a` feat(escort): regrow only in a held nucleus; feeding lure and faster waves — on cece/magical-pascal-5uveyi, bleeding-edge
- 2026-11-01 `4aebc6fa7` fix(escort): the player sails in their own vessel, never swapped to the seat's hull or left on autopilot — on cece/magical-pascal-5uveyi, bleeding-edge
- 2026-10-31 `4c3bacbf0` docs(voyages): verification sim results — on cece/magical-pascal-5uveyi, bleeding-edge
- 2026-10-31 `338b2147c` docs(voyages): verify pass write-up, final (sims not run: owner's editor open for the hour) — on cece/magical-pascal-5uveyi, bleeding-edge

### Also in flight on other branches
- `claude/voyage-scene` (Garrett, 2026-11-01): fix(escort-sim): close the privacy overlay through its own Finish so the waiting boot resumes
- `claude/voyage-sim-farm` (Garrett, 2026-10-31): fix(simfarm): sims are silent - listener muted at boot, farm player built with Unity audio disabled
- `claude/voyage-coop` (Garrett, 2026-10-31): Merge remote-tracking branch 'origin/claude/voyage-night' into claude/voyage-coop
- `claude/voyage-pacify` (Garrett, 2026-10-31): feat(escort): pacify a world the squad holds past escort.pacifyShare
- `claude/tandava-arena-mode` (unattributed, 2026-10-29): fix(menu): Set sail opens the card on the modal that launches it

### Open tickets (Froglet Tasks)
- T-20261028-1c37 [open] dev: Give every Voyage an Ark: each squad's voyage carries its Ark (one per squad of three), built as the Arkway's is - the ArkReverse model through ArkModelDefinitionSO, its omni reactor and regrowth - with the server-owned Ark and its hold that Voyages need. (who: anyone with the Voyages backend)
<!-- team_report:end -->
