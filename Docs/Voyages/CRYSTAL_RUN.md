# Crystal Run, the voyage HUD and the end-of-voyage scoreboard

Milestone 1 (Shombith, 2026-11-16): make Escort the Ark and one crew job voyage great. The crew job is **Crystal
Run** (`job.raid.crystals`). The look is Frogma Rev 113 (`Docs/MENU_REDESIGN_B.md` §209: boards R113Hud, R113Run,
R113Compare, R113Score). Not yet run in the Unity editor: `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## 1. The rules (Shombith's answers, 2026-11-16)

- "Steal these many elemental crystals during the voyage; a few cells attack you, a few don't."
- The goal is **50 crystals** (20 until round 3, 2026-11-16), between the whole squad. Any elemental crystal counts (Space, Time, Charge, Mass); an
  omni crystal does not.
- The leg is **4 cells: 1 calm, then 3 hostile** (`hostileCells` [false, true, true, true]). The breach line is
  **10%** (40% until round 3, so the hold can leak below 30% before the Ark falls).
- Crystals count from the moment the Ark sets sail, through every cell, not only at the stop.
- **Any pilot can bring it**: the job is on every board (`always`), credited to the first seated pilot.
- At the stop the job resolves: met the goal, it succeeds and its pay is owed too; missed, it fails and only what was
  stolen is owed. Owed crystals are **at stake until the Ark docks home**, and lost with the Ark.
- **Finish** sails the Ark home with the squad escorting it; docking banks everything (`VoyageRules.Extract`) and
  ends the voyage. (Prompt 1 said Finish banks at once; Shombith's own answer, "the ark should sail home and players
  should accompany", wins.)
- **Venture further** sails the same work on from the stop to the next place of its kind: the goal **+10** and **one
  more cell** (`ventureGoal`, `ventureCells`), never straight back to where the run set out from.

Content: `Docs/Voyages/voyage_slice.json` (`always`, `countsUnderway`, `cells`, `ventureGoal`, `ventureCells`,
`hostileCells`, `breachHull`) through `Tools/Build/author_voyage_assets.py` (`--check`, `--self-test`). Rules:
`VoyageRaid` (pure, `CosmicShore.Data`): the board's always-offered work, the leg fitted to its cells, the tally by
seat and element, the stop, venturing. The run: `VoyageRun.Raid.cs`. The hostile cells' fauna and assaults are the
Ark-attack work (`VoyageRun.Assault.cs`, `Docs/Voyages/ARK_ESCORT.md` §4.2).

## 2. Toasts

Rev 113 Compare: a few seconds each, top centre, one at a time, a bold line and an optional line under it; an event
(white), a threat (red) or something good (green). They go through the run's one banner path
(`ArkwayRun.ShowBanner(line, seconds, mood, under)` → `ArkwayVoyageHud`), never a second toast system, and never one
per crystal. `VoyageToasts.Show` adds the mood's sound (empty slots on `Voyage.asset`).

| Toast | When | Mood |
|---|---|---|
| STEAL {goal} CRYSTALS | the run sets off | event |
| ENTERING CELL {n}. CALM / HOSTILE. | each new cell | event / threat |
| FINAL CELL: THE DESTINATION | the last cell | event |
| {goal} CRYSTALS. BRING THEM HOME. | the goal is met | good |
| the assault lines (`ArkEscort.asset`) | warned, started / repelled / passed | threat / good / event |
| ARK UNDER ATTACK | the hull starts losing plates (at most every 20 s) | threat |
| BREACH CLOSE | the hull within 10% of the breach line (once, until it climbs out) | threat |
| SAIL THE ARK HOME | Finish | event |
| the escort's own lines | depart, each world (red when hostile), through (green), fallen, overrun (red) | |

The alarm rule is `ArkAlarmToasts.Decide`, from the Ark's signals (`ArkVoyageSignals`).

## 3. The objective box

Top left, 600 wide, a thin left line, no glow (Rev 113 Hud and Run). It reads `VoyageObjective.Source` (the running
`VoyageRun` or `ArkEscortRun`); `VoyageObjectiveText` picks every line, so a test reads each choice.

- **Crystal Run**: CRYSTAL RUN, the goal line, the large `12 / 20` counter and its bar, the split by element with the
  petal flowers, the cells Calm / Hostile (the last named for the destination), the hull with its breach mark, one
  action line.
- **Escort the Ark**: ESCORT THE ARK - DIFFICULTY n, "Keep the Ark's hull above {breach}% to {last world}'s core", the
  worlds as stops (done, now, next, end; Hostile until pacified), the hull with its breach mark, "Ark bound for /
  inside {world}", one action line.
- **Sailing home**: SAILING HOME, what is aboard and at stake.
- The action line, first that applies: an assault under way (kills / needed), one announced (seconds), the hull
  losing plates, the Ark healing, the goal met, the cell hostile or calm.

## 4. The scoreboard

Over the dimmed sea (Rev 113 Score): the kicker and title, the Ark's hull against its breach line, a row per seat
(Crystals, Kills, Off the Ark, Score), the rewards (crystals stolen by element, the job's or escort's pay, at stake
or banked), and the ways out. The voyage scene waits while a board is open.

- Score per seat: crystals × 10 + kills × 5 + creatures felled near the Ark × 15 (`Voyage.asset`, "the scoreboard's
  score").
- Crystal Run at its stop: Finish (B) and Venture further (A).
- Escort through its last world: Finish (B: sail home, then bank) and Venture further (A: difficulty + 1).
- Docked home: one way out, Back to Voyages.
- Lost Ark (either voyage): marked lost, with what went down; Try again (A) and Back to Voyages (B).

No XP, rank bar or pilot trust on the rewards (Shombith, 2026-11-16: not needed).

## 4.1 The fauna attacking the Ark

Every hostile creature inside the Ark's guard radius (the escort's `CountThreats`, both voyages) wears a lit, faceted
red diamond hovering over it, sized to the creature. When any is off screen, a lit red arrow stands at the screen's
edge, in the world just in front of the camera, pointing at the nearest, with one pip per attacker off screen (up to
six). Both are 3D volumes, never a flat ring or a label (`Docs/claude/SHADERS_AND_PERFORMANCE.md`, "In the world, not
on the screen"). `ArkAttackerMarks`; the colour is Rev 113's breach red, traced.

## 5. The escort's pay

An escort that comes through owes `escortCrystalsPerDifficulty` (30) × difficulty crystals of
`escortRewardElement` (Omni), banked when the Ark docks home like everything else.

## 6. The raid window and carried crystals (P1, P6; Shombith, 2026-11-16)

From the playtest ("Shouldnt the ark park at each cell and give us time to steal the cells crystals"), picked from the
Voyage HUD and fun pass doc (https://claude.ai/artifact/JTaRK8Rb8798GrD3FXMFZt).

- **The Ark anchors (P1).** On reaching each cell of a Crystal Run's leg the Ark stops at the core for a raid window:
  `raidAnchorHostileSeconds` (30) in a hostile cell, `raidAnchorCalmSeconds` (30) in a calm one, the final cell
  included. `raidAnchorWarnSeconds` (10) before it sails the horn (`arkHornSound`, shipped empty) and a threat toast
  warn; then it weighs anchor. The strip's job line shows the clock. The hook is `ArkwayRun.AnchorSeconds`; only
  `VoyageRun` sets it, so the escort and the Arkway toy never stop.
- **Carried crystals are at risk (P6).** A crystal a seat takes is carried, not counted. Flying within
  `raidBankRadius` (180 u) of the Ark banks it, and only banked crystals count toward the goal, the owed list and the
  scoreboard. A hit from a hostile danger prism (another domain's, a creature's stinger) spills everything the seat
  carries as loose crystals anyone can take (`ElementalCrystalEjector`), at most once per `raidSpillCooldownSeconds`.
  Whatever is still carried at the stop is lost. `raidCrystalsAtRisk` off counts a crystal the moment it is taken.
- **Words** (voyage_slice.json): ToastAnchored(+Sub), ToastAnchorClosing(+Sub), ToastWeighed, ToastBanked(+Sub),
  ToastSpilled(+Sub), ObjectiveStripAnchor, ObjectiveStripCarried.
- **Not yet:** spilled crystals are per-peer like every loose crystal, and the hull-contact event is raised on the
  peer that runs the contact, so a party friend's spill and bank are only as good as that path; not run in Unity.

## 7. Round 3: hull raids, completing early and the way home (Shombith, 2026-11-16)

"Increase the number from 20 to 50 ... make the cell's fauna attack the hull, if the hull's health drops below 30% it
loses banked crystals one by one. It should dock at a cell for 30 seconds ... if at any cell the ark feels like crystal
goal has been reached we should have the option says complete and set sail ... When set sailing the fauna needs to get
aggressive more and also between cells bigger faunas should start spawning ... The health of the ark should be shown on
the left too." Then: "make the cell big by 50%".

- **Goal 50, 30 s in every cell, breach 10%** (voyage_slice.json and `Voyage.asset`'s defaults).
- **Goal met: no more anchoring.** Once the banked count reaches the goal the Ark weighs anchor at once and does not
  stop again (`RaidAnchorSeconds` answers 0), the goal toast says how to finish, and the strip reads
  ObjectiveStripComplete.
- **Complete and set sail.** While the goal is met, the host's pause window shows one more button under Resume, labelled
  PauseCompleteAndSail (`IPauseMenuAction` / `PauseMenuAction`, a copy of the Resume button made in code, no prefab
  change). It ends the leg where the Ark is (`ArkwayRun.EndRouteHere`): the Ark holds at the cell it is at or bound for,
  the run resolves as at its stop, and the scoreboard asks Venture further or Finish. After Venture further the next run
  (goal +10) offers the same button once its goal is met. A party friend's Ark finishes its own leg and then takes the
  host's next one, as before.
- **The cells' fauna go for the hull.** A Crystal Run's calm cells now lure `raidCalmLureShare` (0.2) of their fauna's
  goal picks onto the Ark; hostile cells keep the escort tier's share and assaults.
- **The leaking hold.** Below `raidHullLeakBelow` (30%) of its hull the Ark loses one banked crystal every
  `raidHullLeakSeconds` (1.5 s): this leg's banked crystals first (`VoyageRaidTally.Leak`, the richest seat's most
  plentiful element), then what earlier legs owe (`VoyageState.LeakOwed`). Each one spills out of the Ark into the sea
  (`ElementalCrystalEjector.ShedOntoSphere`), where any pilot can take it back and bank it again. One toast per dip
  (ToastHullLeaking), the strip's job line (ObjectiveStripLeaking) and the hull line turn to the threat colour. The host
  decides; a friend sees it through the host's count.
- **The way home is harder.** On the return leg every cell may assault the Ark, and every lure is multiplied by
  `homewardLureFactor` (2; calm cells start from the calm share). In the open water between cells, every
  `homewardRaiderSeconds` (12 s) a wave of `homewardRaiderCount` (2) raiders comes in `homewardRaiderDistance` (260 u)
  ahead and to the side of the Ark, while fewer than `homewardRaiderMax` (6) are alive: the predators (else any species)
  of a leg cell held by another colour, born in that cell's colour through `CellLifeSpawnerBase.SpawnFaunaWithDomain`
  and grown `homewardRaiderScale` (1.8x) by `Fauna.GrowBody` in the spawn frame. No cell of another colour, no raiders.
  First wave: ToastRaiders.
- **The Ark's hull on the strip.** A Crystal Run's strip shows ObjectiveStripArkHull ("Ark hull 72%") under the hull bar.
- **Bigger cells.** `cellScale` (1.5; 2.5 since §8) on `Voyage.asset` reaches the corridor as `ArkwayConfig.CellScale`: each voyage
  cell's membrane (`CapsuleMembrane.SetRuntimeScale`, its baked wobble kept) and nucleus are 1.5x, so
  `Cell.MembraneRadius`, the sense radius, the density grids, spawn points, the Ark's approach and the leash all see the
  bigger world, and the spacing grows 1.5x so cells never overlap. The cell's environment mass keeps its authored size.
  The Arkway toy and the escort keep 1.
- **Not run in Unity.** To check by hand: the pause button's look (it copies Resume), whether lured fauna really bring a
  hull under 30% in a calm cell, the raiders' size and whether they leave their cell's water, and the bigger membrane
  against the environment.

## 8. Round 4: bigger cells, short crossings, spread crystals, kaiju (Shombith, 2026-11-17)

"The cell is not big and all crystals and everything come in the same spot, make the cells bigger the distances between
them smaller and more attacks outside the cell big kaiju level fauna attacking the cell."

- **Cells 2.5x** (`cellScale`, was 1.5): a 1200 u membrane is now 3000 u. The nucleus grows with it; the environment
  mass keeps its authored size.
- **400 u of open water between cells** (`cellGap`, was 1200 u at 1.5x). With a gap the corridor spaces cells by their
  membranes, `CellConveyor.CellSpacingFor`: twice the largest authored membrane radius among its worlds, times the
  scale, plus the gap (6400 u centre to centre for 1200 u membranes). The Arkway toy and the escort keep the old rule
  (`CellGap` 0).
- **Crystals spread through the cell** (`scatterLifeSpawns`, `Cell.ScatterLifeSpawns`). The crystals are the
  lifeforms' hearts, and every wave used to land on one feeding ground: the densest mass, which in a voyage cell is the
  environment at the core or, under the lure, the Ark itself. Now each creature without a band or pen, and every
  plant, is born at its own point between the nucleus and the membrane. Where creatures go afterwards is still their
  own (they still seek mass and the Ark).
- **Raiders on the way out too** (`raidersOutbound`): the open-water raider waves of §7 come between the cells of a
  Crystal Run's outbound leg as well, not only on the way home.
- **Kaiju between the cells** (`kaijuPerCrossing` 1, `kaijuMax` 2, `kaijuScale` 12, `kaijuLeadDistance` 900,
  `kaijuSideOffset` 300). Shombith, same day: "the kaiju shouldn't reach when the Ark is in the 30-second stop time ...
  when the Ark is between cells, the kaiju should be between cells, and it should be huge." On every crossing, once the
  Ark is within 900 u of leaving the cell behind it (or in open water), one grazer from a leg cell held against the squad
  is born in the middle of the gap, 300 u off the Ark's line, grown 12x, and waits there for the hull. Never while the
  Ark is anchored. One toast per crossing (ToastKaiju). `VoyageRun.Kaiju.cs`.
- **The raid leash.** Kaiju and raiders wear the run's `IFaunaLeash` (the Angler's contract): steer at the Ark, eat its
  nearest plate, re-checked by the creature's own diet. Before this a raider was only lured through its home cell, which
  could be far behind, so it could swim home instead. Only grazers a leash can hold become kaiju; a predator raider
  keeps its own hunt. `Docs/ECOSYSTEM.md` §0.z records the widening.
- **Party.** Each machine's sea runs its own kaiju and raiders, as before; nothing new is replicated and the protocol is
  unchanged.
- **Not run in Unity.** To check by hand: the 2.5x membrane against the environment, whether the scattered crystals
  read as spread, how long a crossing takes now (the core-to-core leg is 6400 u, the Ark crawls inside the bigger
  cell), whether a 12x kaiju reads as huge and still bites (its mouth reach may not grow with its body), and whether leashed
  raiders now reach the Ark.
