# Voyages: start here (handoff to Shombith, 2026-11-12)

**Status: Shombith holds the Voyages lane from 2026-11-12 for at least a week** (Garrett: "Shombith is going to be
occupying the voyages swimlane for at least the next week"). This page is the way in: what is built, what is on
`bleeding-edge`, which docs say what, how to debug a run, what has never been tested, and what Garrett has and has not
decided. It adds no design: where it and a design doc disagree, the design doc wins and this page is out of date.

## 1. Where things stand (measured 2026-11-12)

- **Everything voyage is on `bleeding-edge`.** No unmerged branch touches `Docs/Voyages`, `Assets/_Scripts/Controller/Voyages`,
  `Assets/_Scripts/Data/Voyages` or the voyage views (`who_works_where.py query`, nothing in flight). The last voyage
  merges were on 2026-11-08 and 2026-11-09:
  - **#1855** `claude/voyage-scene-port`: voyages in their own scene (`Voyage.unity`), the in-world (diegetic) toys
    removed. Garrett ran it in Unity on his PC: it compiles, `VoyageShellTests` passes 6/6, and the smoke test passes.
  - **#1853** `claude/voyage-stop-stall`: the Ark no longer sits at a job's stop forever. `VoyageRun.StandStop` read
    `_arrival` after `ClearStop()` had nulled it, so every stop that was not home threw an NRE.
  - **#1856** `claude/escort-hook-stale`: escort AI no longer steers by a destroyed hull after a pilot swap. This was
    the cause of the 1,294 `ArkEscortPilot.Target` MissingReferenceExceptions. Ticket **#1688** (T-20261101-1383)
    describes that bug and is still open.
- **The lane is OPEN** on `/swimlanes --lane voyages` until a voyage branch is pushed. The branch you push makes it
  yours. The two waiting items it lists are #1688 above and the 3D galaxy map hand test (§5).
- **Phases.** VOYAGES V1 (one squad, a small map, no strangers) is built as the slice. DEPTH D1 (the living galaxy,
  rivals, verbs, sagas, standing, the harness) is built, and so is most of D2 (wingmates fly escort roles, varied
  outcome lines, standing and the log on the card). **Not built:** D3 and V2 (the networked Ark, rival Arks in the
  world, AI squads met on arrival), V3 (GTC on the backend) and V4.

## 2. Read these, in this order

| Doc | What it is |
|---|---|
| [`VOYAGES.md`](VOYAGES.md) | The design: squads on the HyperSea, travel, GTC, instances, state, phases V0-V4. §10 says who owns what; §11 lists Garrett's open questions. |
| [`SLICE.md`](SLICE.md) | V1 as built: the loop, the 12 cards, content as data, and §5, the reversible choices the slice made (own scene, one board per seated pilot, each job its own voyage). |
| [`DEPTH.md`](DEPTH.md) | The depth pass: the living galaxy, rival crews, work verbs, sagas and relics, standing. §11 covers the harness, §12 the phases and §14 Garrett's 11 defaults. |
| [`ARK_ESCORT.md`](ARK_ESCORT.md) | The escort voyage (3 vessels, 5 worlds, difficulty 1-9) and its sim tuning. §4.1 summarises the night passes and §5 is the galactic map plan. |
| [`VOYAGE_BOARD.md`](VOYAGE_BOARD.md) | The HyperSea as the Milky Way, the board artifact and its sea in the game, the 3D cells. §5 lists 11 open decisions. |
| [`SOURCES.md`](SOURCES.md) | The outside sources (Notion, FigJam, Figma) and which parts of them are out of date. Do not build from those parts. |
| [`night/`](night/) | Write-ups of the overnight escort passes (`*_DONE.md`): chaos ladder, roles, pacify, fauna threats, Ark feel, sim farm, co-op. |

Also: `Docs/Pilots/ARTIFACTS.md` and `CALLSIGNS.md` (pilots, artifacts, the payload), `Docs/AI_SYSTEM/AI_PLAN.md`
(how a squad seat's AI is chosen in a voyage) and `Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md`. The escort world uses
two carve-outs from the ecosystem rules (§0.x armour gnawing, §0.z feeding lure), and ARK_ESCORT §4.1 cites both.

## 3. The code, by job

| Job | Where |
|---|---|
| Rules and state: the pure C#, which the tests and harness run | `Assets/_Scripts/Data/Voyages/`: `VoyageRules`, `VoyageState`, `VoyageContent`, `VoyageRivals`, `VoyageWorld`, `VoyageCourse`, `VoyageChart`, `HyperSea*` |
| Content, as data and never C# literals | `Docs/Voyages/voyage_slice.json` is the spec. `Tools/Build/author_voyage_assets.py` (`--check`) writes `Assets/Resources/Voyages/VoyageContent.asset`. `voyage_content.json` sits beside it. |
| The scene and the run | `Assets/_Scenes/Voyage.unity` (built by `VoyageSceneBuilder`: FrogletTools > Scenes > Build Voyage Scene), `VoyageLaunch` (Set sail), `VoyageScene`, `VoyageHarbour`, `VoyageRun`, `VoyageVault`, `VoyageCourseLink` (a party follows the host's course) |
| The escort | `ArkEscortRun` (and its `.Chaos`, `.Course`, `.Variety` and `.Metrics` partials), `ArkEscortPilot`, the `*Role.cs` files, `IArkEscortHost`, `ArkEscortSim` (+ `Editor/ArkEscortSimLauncher`), with tuning in `ArkEscortSO` |
| The Voyage window (UI) | `Assets/_Scripts/UI/Views/Voyage*.cs`: `VoyageJobsView`, `VoyageOpenCard`, `VoyageMapGraphic`, `VoyageCellDraw`, `VoyageWindowTabs`. The layout comes from `Tools/Build/author_voyage_window.py`. Since `claude/sweet-mccarthy-ow25e2` (2026-11-12) every tab is the Sea table (Rev 89 A and Rev 90, `Docs/MENU_REDESIGN_B.md` section 172): `VoyageBoard` (Voyages), `VoyageSlabPage` and its `VoyageSagaSlab`, `VoyageQuestSlab`, `VoyageEpisodeSlab`, with `VoyageSlab` (the tilt), `VoyageSquadColumn` (seats and steppers) and `VoyageKit` (builders). Built at runtime over the window's pages; the old Saga cards and quest track stay on their pages, hidden, as the data. Not seen in Unity. Since 2026-11-13 (section 184) the Voyages tab is two screens (Rev 98 1A and 2D): `VoyageBoard` (the voyage screen, the flat map with its explorer, `ShowScreen`), `VoyageSquadDeck` (the squad screen), `VoyageSeats` (the squad rules both share with `VoyageSquadColumn`), `VoyageSoftFill` (soft-ended fills). Not seen in Unity. |
| Saving | `Assets/_Scripts/System/Voyages/VoyageStore.cs` (on the device; the backend does not hold voyages yet) |
| Settings | `Assets/Resources/Voyages/Voyage.asset` (`VoyageToyDefinitionSO`, no longer a toybox toy) |
| Dev tools | `Editor/Voyages/VoyageDevTools.cs` (two galaxy tools under FrogletTools) |
| Tests | `Tests/Editor/VoyageRulesTests`, `VoyageShellTests`, `VoyageDepthTests`, `VoyageCardRulesTests`, `VoyageDepthHarness` |
| Depth harness | `Tools/Build/voyage_depth_harness/run.sh --scorecard 60 200`. It sails 200 galaxies for 60 voyages each, and its scorecard is a test (DEPTH §11, §15). |

## 4. Debugging a voyage

- **Garrett's Editor.log** is at `C:\Users\gradi\AppData\Local\Unity\Editor\Editor.log`. Voyage and Arkway info logs
  are on verbose `CSLogChannel`s and are off by default, so grep for `Exception` and `at CosmicShore` stack lines
  first. To see the traces, turn the channels on in FrogletTools > Toolbox > Logging.
- **The two bugs found most recently were both lifetime bugs**: a value read after it was cleared (#1853), and a hook
  left on a hull that died (#1856). After a pilot swap, `AIPilot.TakeModeHooksFrom` moves the escort seat's hooks
  onto the hull the person left. Anything that holds a hull or a stop across a swap, a stop or a scene load should be
  checked for the same shape.
- **Cloud sessions can compile without Unity**: `python3 Tools/Build/unity_refcompile/dotnet_from_nuget.py`, then
  `export DOTNET_ROOT=/root/.dotnet` and `bash Tools/Build/unity_refcompile/run.sh`. About 21 sandbox errors in
  UGS/Vivox/Cinemachine files are expected. What matters is "0 in files changed".
- **`check_generated_assets.py` flags 15 guid refs in `Voyage.unity`.** The scene was copied from Menu_Main and
  inherits them (Input System default actions, lighting data, Cell overrides). They are not regressions.
- **The escort sim never saves** (every seat is AI), so each sim run sets out from the same place.

## 5. Never opened in the editor

Every voyage section in `Docs/UNITY_VERIFICATION_CHECKLIST.md` is still red. The newest are "Voyages: own scene
(Voyage.unity)", "The Voyages tab as Rev 74 C3, and the Quests window", "the Ark no longer stalls", "escort AI no
longer steers by a destroyed hull", "The Voyage window's galaxy map draws the cells in 3D", "Voyages, deep" and "The
voyage slice". The only editor run on record is Garrett's for #1855 (compile, `VoyageShellTests` 6/6, smoke PASS).
The rest of the voyage loop has never been played by a person: jobs, vaults, wrecks, docking home, rivals, the Kaiju
and the escort card. A hand pass through the checklist sections is the cheapest way to find what is really broken.

Known gaps carried from ARK_ESCORT §4 "Known gaps": the Grizzly+Urchin+Falcon squad deploys only one AI wingmate (one
of those hulls does not spawn as an AI companion), and the sim's single samples are noisy (±20 hull points).

**The Voyage window's own open work** (from the two-screen build, `Docs/MENU_REDESIGN_B.md` section 184, 2026-11-13):

- **Hidden, not retired.** `VoyageOpenCard` (922 lines), `VoyageJobCard` and the job-card grid are unreachable while
  `VoyageJobsView.board` is on (measure: `grep -rn "VoyageOpenCard\b" Assets/_Scripts --include=*.cs`; its static
  `IsMySeat` / `CanEdit` are still read by `VoyageSeats`). Retire them once the two screens are accepted in Unity, after
  moving those two rules into `VoyageSeats`.
- **`VoyageSquadColumn` serves only the locked Saga tab.** When the Saga opens, give it `VoyageSquadDeck` (or the
  voyage screen's squads) instead, and delete the column and `VoyageKit.Stepper`.
- **The lobby line is not built.** Rev 95-98 draw "Mirel is in your lobby, not in this squad" over Configure squad; the
  window has no reach to the party's members. It needs a read of the party roster (`Docs/claude/MULTIPLAYER_AND_SOCIAL.md`
  § Party), which is Shombith's backend area.
- **The pad does not press the drawn keycaps** (X Configure squad, B back, Y Save as preset). LB / RB were taken off on
  Shombith's word ("just clicks work fine", 2026-11-13); the keycaps' wiring is a separate decision.
- **The tabs' frames are switched off with `SetActive(false)`**, which the kit's show / hide convention
  (`KitVisibility.SetShown`, `Docs/claude/MENU_AND_LAVA_LAMP.md`) avoids. Here it is permanent removal, not a toggle;
  the clean fix is the window's prefab without the frames (`Tools/Build/author_voyage_window.py`).

## 6. Leftover branches (checked 2026-11-12)

Garrett (2026-11-12): "we don't want to be packrats hoarding branches and code. we want to curating the good and
cleaning the vestigial noise." What each unmerged voyage branch still holds:

| Branch | Holds | Verdict |
|---|---|---|
| `claude/voyage-coop` (3 commits, 2026-10-31, `209c1a2e7`) | Escort party co-op: a shared, seeded course; corridor worlds kept local to each machine (`Cell.IsSatellite`); human seats with AI filling the rest; pacification on every peer; the report for parties. 13 files, ~550 lines, with `ArkEscortRun.Coop.cs` new. Compiled and passed 62/62 EditMode tests at the time. No two-client test was ever run ([`night/coop_DONE.md`](night/coop_DONE.md)). | **Worth porting.** It is the co-op half of Garrett's first priority (§8). It is about 1,300 commits behind and touches `Ark.cs`, `CellLifeSpawnerBase.cs`, `FaunaNetworkSync.cs` and `ArkwayRun.cs`, so port it by hand onto a fresh branch rather than merging it. Each machine still runs its own Ark; the host-owned networked Ark (V2) is the next step past it. |
| `claude/voyage-sim-farm` (13 commits, 2026-10-31, `d4a6827fd`) | A Windows farm player and a co-evolution driver that tunes the escort's difficulty ladder against the AI squads (`Tools/SimFarm/`, `SimFarmBuild.cs`, seeded runs in `ArkEscortSim.cs`). [`night/sim_farm_DONE.md`](night/sim_farm_DONE.md) records how it works and what it found. | **Not needed now.** It tunes difficulty, not the squad's roles, and Froglet Compute is paused. Keep it under an archive tag and delete the branch. |
| `claude/ark-escort-voyage` (1 commit, `3b7cdb69d`) | The escort card's difficulty buttons no longer cover Set sail. | **Already on `bleeding-edge`** (`VoyageJobCard.ShowStepper`, `StepWidth`). Delete. |
| 15 other `claude/voyage-*` branches | Fully merged, or with only a merge commit beyond `bleeding-edge` (`voyage-scene-port`). | Delete. |

**Shombith: this cleanup is yours** (Garrett, 2026-11-12: "tell shombith to clean it after he is sure there is
nothing of value"). Look through the branches above first. If nothing in them is worth keeping, archive sim-farm
and delete the rest from a full local checkout. Claude cloud sessions cannot push tags or delete branches they did
not create.

```
git fetch origin claude/voyage-sim-farm
git push origin d4a6827fdb66761916aead0dada2926c709b47b1:refs/tags/archive/voyage-sim-farm
git push origin --delete claude/voyage-ark-core claude/voyage-ark-feel claude/voyage-chaos claude/voyage-escort-roles \
  claude/voyage-fauna-params claude/voyage-fauna-threats claude/voyage-integration claude/voyage-night \
  claude/voyage-pacify claude/voyage-pacify-tune claude/voyage-scene claude/voyage-scene-port claude/voyage-variety \
  claude/voyage-verify claude/ark-escort-voyage claude/voyage-sim-farm
```

`claude/voyage-coop` is not in that list. Delete it after you have ported what you want from it.

## 7. Who else works near here

From VOYAGES §10 and DEPTH §13. Tell these people when you change their area, and queue the change with
`approvals.py add` for their `/approve`:

- **The Voyage window and menus**: Shombith (yours already).
- **The rules, content, the run, the escort and the squad layer** (pilots, artifacts, `PilotLoadoutStore`): Garrett.
- **Multiplayer, party, Relay and AI squads** (`Ys-bleeding-edge`): Yash. V2 and V3 need him.
- **The Ark, the Arkway and the ecology**: run `who_works_where.py query` before touching them.

## 8. Garrett's decisions

### Answered 2026-11-12 (these override any default below)

1. **Priority: the squad, alone and together.** "Anything blocking a single player experience (3 AI on three
   vessels that understand their role on the squad and perform it when not played) or multiplayer experience
   (networked ark etc.) should take priority. we want to get into a testing loop where our opinions of the experience
   are guiding each iteration. we can't do that until we can play it both alone and together without fundamental
   issues preventing a fun time."
2. **Shombith decides.** "he can decide anything. don't get in his way. he is racing to get voyages fun and he needs
   to do whatever it takes to get there fast." Every open decision below is his to settle. Record the ones you
   settle in the doc that owns them.
3. **Squads are always 3 vessels.** "vessels have a seat count which affects the number of pilots (ai or player
   controlled)." This answers VOYAGES §11 question 1: the squad is 3 vessels, and the number of pilots is the sum of
   their seats (a crew vessel brings two).
4. **An AI's role comes from its cards.** "each ai should ultimately have behavior that is determined by their
   vessel, pilot, element, and artifact." The three basic archetypes to test first, "to derisk the idea of voyages":

   | Archetype | Signalled by | Does |
   |---|---|---|
   | **Guardian** | the Charge element, and a pilot whose vision shows enemy fauna | protects the Ark |
   | **Cultivator** | a Squirrel in Mass, with a pilot whose vision shows crystals | cultivates mass in the cell so the Ark can heal |
   | **Destroyer** | a Dolphin in Space, with a pilot whose vision shows opposing prisms | helps the Squirrel take the cell by destroying enemy mass in the nucleus |

   **The gap today:** `ArkEscortRoles.Create` picks a role from the hull alone (Sparrow destroyer, Squirrel,
   Dolphin, Serpent wall, Scarab ring, anyone else a heart chaser). `DefaultElement` fixes each hull's element. The
   pilot, its vision and the artifact play no part, so a role is not yet chosen by the cards. `Docs/AI_SYSTEM/AI_PLAN.md`
   (vessel and element choose the genome, the pilot biases it) is the plan this joins.
5. **The cells feel like cheap imitations.** "the cells are still not spawning a proper nucleus, shards, etc. ...
   we need to make the hypersea feel like our galaxy condensed into a giant living organism that we interpret as
   ecology." A lead that has not been checked: SLICE §5 says `Voyage.unity`'s cell boots the empty Barren world, and
   the escort sails its own rolled worlds (`ArkEscortVariety`), not cells built from the course. Compare a voyage cell
   with an arcade cell at the same intensity (nucleus, membrane, cytoplasm shards, flora, fauna). The `/ecology` and
   `/world-look` skills cover the Cell.
6. **Branches: curate, don't hoard.** See §6.

### Settled earlier, recorded in the docs

- Voyages have their own scene, kept apart from freestyle (2026-11-01).
- Each job is a voyage of its own (2026-10-28).
- Found artifacts stay locked until docked home (2026-10-28).
- No fixed 4-person cap (2026-10-28).
- The Voyage window has one map, the Voyage Board's sea (2026-11-01).

### Still open, now Shombith's call

Each has a default the build already follows:

- VOYAGES §11, questions 3-10 (1 is answered above).
- DEPTH §14, decisions 1-11.
- VOYAGE_BOARD §5, decisions 1-11.
