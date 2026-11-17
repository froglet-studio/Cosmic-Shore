# Crystal Run, the voyage HUD and the end-of-voyage scoreboard

Milestone 1 (Shombith, 2026-11-16): make Escort the Ark and one crew job voyage great. The crew job is **Crystal
Run** (`job.raid.crystals`). The look is Frogma Rev 113 (`Docs/MENU_REDESIGN_B.md` §209: boards R113Hud, R113Run,
R113Compare, R113Score). Not yet run in the Unity editor: `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## 1. The rules (Shombith's answers, 2026-11-16)

- "Steal these many elemental crystals during the voyage; a few cells attack you, a few don't."
- The goal is **50 crystals** (20 until round 3, 2026-11-16; 90 for an hour after the storyboard, §12, then back to 50), between the whole squad. Any elemental crystal counts (Space, Time, Charge, Mass); an
  omni crystal does not.
- The leg is **4 cells: 1 calm, then 3 hostile** (`hostileCells` [false, true, true, true]). The breach line is
  **10%** (40% until round 3, so the hold can leak below 30% before the Ark falls).
- Crystals count from the moment the Ark sets sail, through every cell, not only at the stop.
- **Any pilot can bring it**: the job is on every board (`always`), credited to the first seated pilot.
- At the stop the job resolves: met the goal, it succeeds and its pay is owed too; missed, it fails and this run's
  haul is lost (§9; until 2026-11-17 what was stolen was still owed). Owed crystals are **at stake until the Ark docks home**, and lost with the Ark.
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

## 9. Round 5: more life, the lag, a missed goal (Shombith, 2026-11-17)

"I need more fauna inside the cells and more crystals and it is sometimes very laggy. When we miss the goal nothing
happens."

- **More fauna and crystals.** `cellLifeScale` (2) on `Voyage.asset` multiplies every voyage cell's fauna and flora
  populations (seed floors and caps) on top of the leg's pacing (`VoyageRun.OnPrepareCell`). Every lifeform carries one
  crystal, so this doubles the crystals too.
- **The lag.** Shombith's voyage profile of 2026-11-17 01:55 (`BugLedger/shared/reports/shombith/perf/prof_Voyage_*`)
  had a 64 ms spike frame, 41 ms of it one fauna spawn batch: 10 creatures instantiated in a frame (27 ms) and 6 console
  warnings (7.9 ms). Two fixes:
  - `NetworkSceneObjectGuard.NeutralizeStray` read the prefab's hash after destroying the NetworkObject, so its
    once-per-prefab warning fired on every creature birth (the capture's log: 10-11 per species). It reads the hash
    first now.
  - A satellite cell (a voyage's corridor world) instantiates 2 creatures a frame instead of 6
    (`RandomLifeSpawner.SatelliteFaunaSpawnBatchPerFrame`): the same waves over more frames.
  - Not measured: the 12x kaiju's cost. Profile again in a voyage with `prof` if the lag stays.
- **A missed goal** (Shombith's pick on the decision card). At the stop, a run short of its goal: a threat toast
  (ToastGoalMissed, "GOAL MISSED: {done} / {goal}", ToastGoalMissedSub), this run's haul is lost (`raidMissForfeits`,
  `VoyageRaid.Arrive(..., forfeitOnMiss)`; what earlier legs owe is kept), and the board offers Try again (A: the same
  goal and cells from here, `VoyageRaid.Retry`, caption ScoreRetryRaid) or Finish (B: sail home). No Venture further on
  a failed run.
- **Not run in Unity.** To check: the 2x populations' frame time with the 12x kaiju, and the missed-goal board.

## 10. Round 6: the kaiju roster and a phone pass (Shombith, 2026-11-17)

Asked: "The kaijus are laggy can you show me as an artifact which are the kaijus you are selecting ... keep in mind we
are also designing it for the mobile too." The roster page: https://claude.ai/artifact/RqzuAL8XkiFELBtweLNqcw.

- **Who becomes a kaiju** (`VoyageRun.IsKaijuSpecies`): a LightFauna herbivore that takes a leash, from a leg cell held
  against the squad. In the corridor worlds that is the Brittlestar (every world) and the QuadFish (Garland). The
  TadPole (a Boid) is no longer drafted: it steers by its flock, so a 12x tadpole kept flocking with normal ones. The
  Shark never was (a predator eats creatures, not plates).
- **One kaiju alive** (`kaijuMax` 2 to 1). One per crossing already; the second was only a leftover from the last gap.
- **Phones** (Voyage.asset "Voyage - phones", applied when `Application.isMobilePlatform`; the editor keeps the PC
  numbers): `mobileCellLifeScale` 1 (PC 2), `mobileKaijuScale` 8 (PC 12), `mobileRaiderMax` 3 (PC 6), and a voyage cell
  births one creature a frame (`RandomLifeSpawner`, PC 2).
- **Measured** (Shombith's 22:02 capture, commit baeb72fb): CPU-bound, 18.4 ms average, 29.9 ms p99, GPU 2 ms, 66
  fauna and 81 flora in two corridor cells; the spike is still creature births (~2.3 ms each, mostly Awake and the rig).
  Both cells were Jade-held, so with a Jade squad no kaiju was alive in that capture: the kaiju's own cost is not
  measured yet. Not run in Unity.

## 11. The squad beside the Ark, and the launch (Shombith, 2026-11-17)

Shombith: "The vessel should always spawn side by side with the ark. Their spawn points should be two besides the ark and
one on top of the ark. You can show the intro as the vessels flying outside the ark from the tube and making the
formation and then the camera transitions behind the player and we start the game. The game's objective shows up too."

- **Why it lined up before.** The Arkway lays the Ark 150 u ahead of the pilot on the pilot's own heading
  (`ArkwayRun.BeginVoyageAsync`), and the wingmates were set down abeam and astern of it, strung out behind.
- **The slots** (`ArkFormation`, settings `VoyageToyDefinitionSO.ArkFormation`): seat 1 off the Ark's port side, seat 2
  off starboard, seat 3 over its back, all facing its heading, level with the hull's middle plus `alongShare` of its
  length, and `abeamMargin` / `aboveMargin` clear of the hull's own extent (read from its renderers once per voyage, so the
  wide ArkReverse shell is cleared). The seat picks the slot, so every machine in a party puts the same pilot in the same
  place. Both the escort and Crystal Run use them as the voyage opens, and a wingmate past its leash comes back to its slot.
- **The launch** (`ArkSortie`): the intro's second shot (`VoyageIntroSO.Shot.Launch`) watches the Ark from ahead while the
  squad leaves the hangar. Until then each vessel waits on its pad (`hangar.slot.0..2`, seat 1 nearest the doors), the
  doors held open (`ArkModel.HoldHangarOpen`); then one every `launchStagger` s it flies straight out past the jaws and
  turns out and back into its slot (`launchSeconds`). The reel then shows your vessel and the wingmates in formation, the
  title with the objective, and swings in behind you on your vessel's own camera. Skipping finishes the launch in the slots.
- **Round 2 (Shombith, same evening: "I need a more close up view of the vessels when they come out and I only saw 2 not
  the 3rd one ... your vessel controlled by the user comes last and the camera follows that to gameplay").** The reel is
  now: the Ark wide, the title with the objective, then one close-up per wingmate as it launches, and last your own
  vessel's launch, the camera swinging from ahead of it round to behind it and on into your vessel's own camera. Each
  vessel's shot launches that vessel (`ArkSortie.Launch(Transform)`); launch order is the hangar pad order (the first to
  go nearest the doors). A wingmate whose seat is not known yet takes a free seat, and one released after the voyage
  opened joins the hangar (or its slot) instead of being left where it spawned (`FormUpLate`) - the likely missing third.
- **While held** a vessel is stationary, its trail penned, its colliders off (it must not take plates off its own Ark)
  and an AI wingmate's AI and escort role wait; all of it comes back once, at the Ark's speed, however the intro ends.
- **Party:** a machine moves only the vessels it owns (its pilot; on the host, the AI wingmates). A friend's vessel is
  launched by the friend's machine from its own Ark, as each machine sails its own Ark (PARTY.md).
- **Mobile:** no new per-frame search: the hull's extent is read once, the launch is a few transform writes a frame for
  four seconds.
- **Not run in Unity.** To check: the launch path clears the jaws and the shell's wing tips, the side slots read as side
  by side at play speed, and the hand-over lands behind your vessel.

## 11. Round 7: choppy fauna, empty stops (Shombith, 2026-11-17)

"The game is also very choppy, the faunas outside are choppy too, the ark stops at cells where there is literally
nothing to collect."

- **Choppy fauna** (`Fauna.SkipFarBodySyncThisFrame`). Beyond 250 u from the camera a creature's body prisms (index
  entry, shell, render matrix) refreshed every third frame. Voyage cells are thousands of units across, so nearly every
  creature there stepped at a third of the frame rate. The skip now never lets the prisms fall more than about two
  pixels (0.002 rad from the camera) behind the body and never applies to a leashed creature (kaiju, raiders), so only
  still or slow distant bodies skip.
- **Spawn hitches**: a voyage cell births one creature a frame on every platform (`RandomLifeSpawner`, was 2).
- **Empty stops** (`raidAnchorMinLife` 6, `VoyageRun.CellLife`): on arrival the Ark anchors only when the cell holds at
  least six lifeforms the squad can take a crystal from (each carries one); otherwise it sails through. Only life of
  another colour counts: creatures whose colour is not the squad's, and the plants of a cell held by another colour. A
  calm cell still held by the squad's colour has nothing to steal, so the Ark sails through it.
- Not run in Unity.

## 12. The storyboard pass: the leg cell by cell (Shombith, 2026-11-17)

Shombith marked his changes on the storyboard page (https://claude.ai/artifact/GiZT5uNi56zpXZur9QqP4X) and sent its
Copy changes JSON. Built:

- **Goal 90** (was 50); Venture further still adds 10.
- **PC numbers** on `VoyageToyDefinitionSO` (code defaults, not serialized in `Voyage.asset`): `kaijuScale` 14 (was 12),
  `cellLifeScale` 2.5 (was 2), `cellGap` 500 u (was 400). Phone values unchanged (kaiju 8x, life 1x).
- **The leg is authored cell by cell** (voyage_slice.json, `JobTemplate` / `VoyageJob`):
  - `cellWorlds` Caldera, Orrery, Garland, Geode: cell i stands the corridor world of that `CellName`
    (`VoyageRaid.NameCells`, after `FitCorridor`). A name the pool lacks, and every cell past the list (a ventured
    fifth), keeps the world the day drew. The destination's own world is overridden too.
  - `cellAnchorSeconds` 20 / 30 / 30 / 40: the Ark's window in each cell (`VoyageRaid.PerCell`, past the end the last
    entry), in place of the calm and hostile windows. The empty-cell rule (`raidAnchorMinLife`) and goal met still skip it.
  - `cellLife` 0.8 / 1.3 / 0.9 / 1.4: each cell's life before the voyage's life scale, in place of the lane's pacing
    (`VoyageRaid.LayCellLife`); cells past the list keep the pacing.
- **A missed goal offers Venture further** (Shombith's note: "the ark can still collect crystals and complete the voyage
  [and] the difficulty will get harder"). The board offers Venture further (A: the next run, goal + 10 and one more
  cell) in place of Try again, and Finish (B). Venturing brings this run's haul along, owed and at stake again; Finish
  sails home without it (`raidMissForfeits` still forfeits at the stop). "Harder" is the venture step (goal and cells);
  nothing else is raised. ToastGoalMissedSub says so. `VoyageRaid.Retry` stays for tests and is no longer offered.
- **Not run in Unity.** To check: the four worlds stand in that order, the stop clock reads 20 s in Caldera and 40 s in
  Geode, and a missed run's Venture further carries its crystals into the owed list.

## 13. Round 8: the frame rate falls after a few cells (Shombith, 2026-11-17)

"After we cross like 3 cells the FPS in my editor drops to 11." The 23:55 capture (Hesperides and Garland standing,
3,015 draws a frame against 690 at 22:02): Hesperides in Frenzy with 222 plants and 36,656 prisms. The costs that spike
all scale with prisms: the Serpent wall's sweep (13.7 ms in the spike frame), fauna prism scans (10.2 ms), projectile
sweeps (9.7 + 7 ms), the crystals' shimmer (171 calls).

- **The cell behind retires once the Ark is clear of it** (`CellConveyor.QueueDepartedOnceClear`, 600 u past its
  membrane, called from `ArkwayRun.TickCorridor`). It used to wait until the Ark reached the NEXT core; with 2.5x cells
  and the Ark crawling inside them, that kept three full worlds standing (plus one draining) and the one behind kept
  growing. The retirement is unchanged: off-screen gated, frame-sliced, nothing dies.
- **Plants at their own scale** (`cellFloraScale` 1, `Cell.RuntimeFloraShare`): `cellLifeScale` 2 doubled plant caps
  too, and plants are the cell's growing prism mass. Creatures stay at 2x (their crystals), plants go back to authored
  (on a phone, never above `mobileCellLifeScale`).
- Not run in Unity.

## 14. Straying from the Ark: the 5-4-3-2-1 recall (Shombith, 2026-11-17)

"When the player goes far beyond the ark there should be the teleporting feature but only with a warning 5 4 3 2 1 ...
if the player does not come inside the threshold during that timer they lose all their crystals."

- **The rule is the Arkway's leash**, which the voyage already ran at three cell radii (about 9,000 u at 2.5x cells, so
  nobody reached it). Voyage.asset now sets it: `strayRadiusCells` 1.25 (radii of the cell the Ark is at; the Ark
  anchors at the core, so the whole cell stays raidable, about 3,750 u) and `strayCountdownSeconds` 5
  (`VoyageToyDefinitionSO.BuildArkConfig` writes them into `LeashRadiusFactor`/`LeashGraceSeconds`).
- **The warning** is a threat toast in the toast's own spot that counts 5, 4, 3, 2, 1 (`ToastStray`); the line under it
  names the crystals at stake when the pilot carries any (`ToastStrayCarrySub`, else `ToastStraySub`). Other toasts wait
  behind it. Flying back inside (0.95 of the radius) clears it. The ghostly leash shell (`ArkLeashShell`) already draws
  the line.
- **The recall**: still out when it ends, the pilot is set back in their own formation seat (seat 1 port, 2 starboard,
  3 over the back, `ArkFormation.SlotPose`), speed kept, trail penned for the jump, and every crystal they carry
  unbanked is gone (not spilled into the sea). Toast `ToastRecalled` with `ToastRecalledLostSub` or `ToastRecalledSub`.
- Code: hooks on `ArkwayRun` (`LeashCountdownText`, `RecallPose`, `Recalled`), the countdown band in
  `ArkwayVoyageHud.ShowCountdown(line, under)`, the voyage's side in `VoyageRun.Stray.cs`. Each machine leashes its own
  pilot (a party friend's carried crystals are their own). AI wingmates keep their own leash (`BringWingmates`).
- Not run in Unity.

## 13. Goal back to 50, and the scoreboard's missing buttons (Shombith, 2026-11-17)

"Bring the score back to 50, also still the scoreboard when i lost there was no button to sail home or quit."

- **Goal 50** again (voyage_slice.json); Venture further adds 10 (60, 70, ...).
- **Why no board had buttons.** The seats table gave your own row a second `Image` on the same object
  (`VoyageKit.Block` twice). Unity's `AddComponent<Image>` returns null when the object already has one, so
  `img.color` threw a NullReferenceException (Bug Ledger E-6b08617cca, from `ArriveRaid`) and the board stopped building
  before the reward tiles and the buttons. Every board with your row in it (missed, met, lost, docked) came up without a
  way out. Your row is now one fill, lifted toward the light.
- **Lost Ark: Back to Voyages ends the voyage** (`ShowLost` sets `Close` to `GiveUp`), so the scene returns to the menu
  instead of only closing the board.
- **Not run in Unity.** To check: a missed goal shows Venture further and Finish, a lost Ark shows Try again and Back to
  Voyages, and each button (and A / B on the pad) does what it says.

## 14. AI wingmates steal and bank a little more (Shombith, 2026-11-17)

"The AI doesnt try to collect crystals and bank to the ark that much dont tune it much just a little bit I have a plan
for that." A light nudge over the escort roles, on `Voyage.asset` ("Voyage - AI wingmates in a Crystal Run"), each off
at 0, and only in a Crystal Run (`VoyageRun.Wingmates.cs`, through the new `ArkEscortPilot.Errand` hook):

- **Bank run:** a wingmate carrying `wingmateBankAt` (3) crystals flies to the Ark until it banks them.
- **Crystal hunt:** for `wingmateSeekSeconds` (8) of every `wingmateSeekPeriod` (25 s), while no creature threatens the
  Ark, a wingmate lets its hull's own pilot hunt crystals. Seats are a third of the period apart, so two never hunt at once.
- The roles are untouched and still press their abilities. Queued for Garrett's `/approve` (escort AI). Not run in Unity.

## 15. Finishing a met run, and a faster Ark (Shombith, 2026-11-17)

"When the goal is completed there should be an option to finish the voyages right there and if selected set sail home
they will get 2x the rewards and as usual venture further." And: "make the ark fly faster."

- **A met run's board has three ways out** (left to right; `VoyageHud.ThreeWays`):
  - **Finish here (X):** banks everything owed at the stop, as it is, and ends the voyage
    (`VoyageRules.Extract(..., anywhere: true)`; the Ark counts as home again, so the next voyage starts there).
  - **Sail home (B):** the Ark sails home escorted as before, and docking multiplies every crystal owed by
    `homeEscortRewardScale` (2) (`VoyageState.HomeRewardScale`, set by the button, reset by docking).
  - **Venture further (A):** as before.
  - At 1 the bonus is off and the board is the old two buttons. Already at home, there is no Finish here.
- **The Ark is 1.5x faster** in a voyage (`arkSpeedScale` on `Voyage.asset`, over Wander_WithArk's 18 u/s crawl and 4x
  cruise; the escort pacing scales too). The Arkway toy keeps its speed.
- Words: ScoreFinishHere(+Line, +Key), ScoreSailHome(+Line, {scale}). Not run in Unity.
