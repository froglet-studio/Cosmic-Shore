# Game Modes & Controllers

> Moved verbatim from the root `CLAUDE.md`, which indexes every topic file. Paths in this file are relative to the repository root.

### Game Modes & Controllers

#### GameModes Enum (`Assets/_Scripts/Data/Enums/GameModes.cs`)

60 game modes with explicit numeric IDs (highest is `Tandava(62)`; IDs 7, 31 and 47 are skipped). Single-player: `Elimination(1)` through `ProtectMission(27)` — except `Rampage(2)`, repurposed as a multiplayer party game (the **Dolphin-only** destruction race, Scurry's destructive analog; see `_Scripts/Controller/Arcade/RAMPAGE.md`). Multiplayer: `Rampage(2)`, `MultiplayerFreestyle(28)`, `OnlineDuelForTheCell(29)`, `Multiplayer2v2CoOpVsAI(30)`, `CoOpWildlifeBlitz(32)`, `SkimRace(33)`, `Joust(34)`, `Scurry(35)`, `AstroLeague(37)`, `BroodRush(38)`, `Cleave(39)`, `WildlifeLiberation(40)`, `DogFight(41)`, `Bends(42)`, `ScarabScramble(43)`, `Salvo(44)`, `Switchback(45)`, `Hijack(46)`, `Tollway(48)`, `Headlong(49)`, `Breakwater(50)`, `Skein(51)`, `Bloomrush(52)`, `Redline(53)`, `WreckingBall(54)`, `Undertow(55)`, `Regatta(56)`, `Broadside(57)`, `Waystation(58)`, `Dustup(59)`, `Tapestry(60)`, `Sirocco(61)`, `Tandava(62)`. Meta-mode: `Maelstrom(36)` — the session-level meta that draws a random mode + intensity per round from an AUTHORED pool (`MaelstromData.asset`'s `GameQueue`) and chains them via sequential `Single` loads. The pool is **all SIXTEEN** domain-scored arcade modes that fit the Maelstrom card as of the ladder pass — `Bloomrush(52)` and `Redline(53)` both MEET every criterion (domain-scored, in Build Settings, 2-4 players / 2-3 domains) and are deliberately NOT in it, because which tier a mode enters at is the pick-up-difficulty judgement that pass exists to make and neither has been played yet — and **`IntensityTiers` is the ladder over it** — cumulative, ordered by how quickly a new player picks a mode up, so intensity widens the draw as well as raising each game's own intensity (L1 6 modes / L2 10 / L3 13 / L4 16; full table + rationale in `Docs/MaelstromSystem/ARCHITECTURE.md §1.1`). Adding a mode is one asset edit — every consumer is pool-length-agnostic — but a candidate must be domain-scored (standings fold through `ScoringRuleSO.ResolvePlacementOrder`), have its scene in Build Settings, and have a player/domain range containing the Maelstrom card's 2-4 players / 2-3 domains, which is NOT re-checked at draw time, and have a scene that can HAND BACK (`Scoreboard.continueButton` is a per-scene `[SerializeField]` and `if (continueButton)` is the only thing between the host and `MaelstromController.AdvanceToNextGame()`, so a pool scene that leaves it null stalls the tournament on that round with the scoreboard up and no way forward — satisfied structurally today, since all 16 pool scenes instance `CORE/GameCanvas.prefab`, which wires it, and none overrides it to null). **That last criterion is what excludes `AstroLeague(37)` and `BroodRush(38)`** — both pin `MaxDomainsAllowed = 2` because the mode has exactly two goals (a rule, not a host preference), so a 3-domain Maelstrom drawing one would hand it a team shape it cannot express AND have `NormalizeUnassignedHumans` move the Gold pilot off Gold for that round, leaving the standings carrying a domain nobody is playing for. A vessel-locked mode needs no extra wiring: `GameDataSO.SyncFromArcadeGame` publishes the drawn card's `Vessels` list and clamps the lobby pick, so the round forces its own hull (fifteen of the sixteen are single-hull). The draw is a **BAG, not a roll** (`MaelstromDataSO.DrawnGames`): a shuffle deals every drawable mode ONCE before any mode comes round again, refilling only when the pool is exhausted and still never dealing the same mode back-to-back across that seam — because a race to 6 on `{2,1,0}` can decide in three rounds, so an immediate-repeat guard alone left a 16-mode pool free to deal the same mode on rounds 1, 3 and 5, which reads as the shuffle being broken. Note it excludes previous MODES, not the previous vessel or arena, and the bag does not help there (over a dealt bag the same-hull adjacency rate is `Σ n_g(n_g−1)/N(N−1)`, identical to the old roll) — the wide pool SHARPENS that wrinkle rather than diluting it, because the modes cluster on hulls (Sparrow ×5, Dolphin ×3): measured same-hull-back-to-back is **26.7% at L1** and 14.2% at L4, worst at the most accessible setting. Rampage/The Bends and DogFight/Salvo each share an arena as well as a hull. **The draw now happens at HUB ENTRY rather than at launch, and that one move is what the whole between-round screen rests on** (`MaelstromController.PrepareNextRound`, idempotent — one draw per hub visit, so the arena a player is looking at cannot change under them): the hub STANDS the drawn mode's arena through `Cell.RequestCellSwap` and lets the party fly it while the countdown runs, which needs the pick several seconds before the load AND needs it to be the same pick on every machine. It therefore travels as replicated STATE (`MaelstromRoundTicket` on `Player.NetMaelstromRound` — server-write, everyone-read, written identically onto every player), never as an RPC: an announcement reaches exactly the peers synchronized at the instant it is sent, and a peer still inside Netcode scene synchronization when the host draws would sit in front of a hub with no arena in it (the argument `ArcadeConfigSyncManager.LobbySnapshot` already records). **The ready-up is ONE deadline with two values** — 30s on hub entry, SNAPPED to 3s once every connected human has pressed READY — so "everybody readied" and "nobody readied" end the same way and the view needs one rule (`SecondsRemaining <= 3`) to decide whether the 3-2-1 is on screen; the snap is one-way, because letting one player push it back out is a griefing lever on a screen whose job is to get everyone into the next round. **`MaelstromLobbyNetwork` is retired** for the reason it never worked: it was a `NetworkBehaviour` that had to be PLACED in the Maelstrom scene and never was (`lobbyNetwork: {fileID: 0}`), so the ready-up had not run once and the hub fell through to a local fallback where only the host's button did anything — `MaelstromLobby` is a plain MonoBehaviour the view ENSURES, with the state on `Player`, so there is nothing left to place. *A component that has to be placed is a component that can be missing, and the failure is silent.* The screen is three screens on two roots — **hub** (READY + flyable preview), **stats** (NEXT, the same root, preview look-only via `ModePreviewWindow.SetFocusEnabled(false)`), **summary** (Play Again / Main Menu / STATS back to the stats screen) — so a decided tournament lands on the stats screen and NEXT is what asks for the trophy. **The AI roster is dealt ONCE** (`MaelstromDataSO.MaelstromAISeats`, name **+ domain**): it used to persist names only, and because the summary resolves an AI's face by name the faces came along too, while the DOMAIN was recomputed every round by the balanced-placement pick — so the moment a pilot changed domain the bots re-balanced around them and the opponent you were racing became a team-mate, across a tournament scored per DOMAIN. The bot's HULL deliberately does NOT persist (fifteen of sixteen modes lock to one vessel). **The FIELD is fixed at `MaelstromDataSO.SeatCount` (4) every round** — the party's humans plus AI for the rest, so a full party brings no AI and a solo player brings three; it is deliberately NOT the launch modal's player stepper, because the placement table is per DOMAIN and a field that changed size between rounds would be scoring a different game each time. The seats are dealt at HUB ENTRY (not by whichever round first backfills) using the spawner's OWN `GetBalancedDomain` against the same two count dictionaries, so moving WHEN the deal happens cannot change WHAT it deals — and they **fly the hub**, one autopilot vessel per bot in the drawn round's hull and its own colour, despawned immediately before the launch because the round's scene spawns the same seats itself. See `Docs/MaelstromSystem/ARCHITECTURE.md`. `AstroLeague(37)` is hypersea soccer **played with a sword** — a **Rhino-only** standalone domain minigame in which the ball resolves a contact ON THE BLADE (`SkimmerSwingKinematics`: the bounce normal comes off the point of the sword that touched and the strike speed is that point's true velocity, so a swung tip fires the payload far harder than the hull, with an extra tip bonus on top). Its cell is also the reference case for **`Cell.NucleusIsControlZone = false`** — a mode that repurposes the nucleus as PLAY GEOMETRY must declare it is not a claim, or the nucleus' fauna-sanctuary rule makes every prism in the arena inedible and the food web silently does nothing (see `Docs/ECOSYSTEM.md §25`) — and for **`Cell.FaunaExclusionRadius`**, the inner-wall mirror of the cell fauna pen, which holds the cleanup crew outside the court until the volume phase ladder says the pitch is crowded. See `_Scripts/Controller/Arcade/ASTROLEAGUE.md`. `BroodRush(38)` (display name "Brood Rush") is the nucleus-control fauna-wave race (see `_Scripts/Controller/Arcade/BROODRUSH.md`). `Rampage(2)` is the **Dolphin-only** demolition race — first domain to DESTROY 2000 hostile prisms wins (`ScoringMetric.PrismsDestroyed`). It is the mode that turns a VESSEL'S PRIVATE ECONOMY into a contested object: the Dolphin banks blast energy **only by skimming** and discharges it **only on a crystal**, so a belt of cacti and other breakable flora rings the membrane (five species on staggered planting shells at 0.76-0.94 of the membrane radius, core left open) and the arena carries a SCARCE supply of neutral crystals respawning in the nucleus — graze to charge, race for a crystal, aim at the thickest forest, fire a 2400-long cone whose GAPE is the energy you banked. Mixed rosters are excluded for a structural reason, not exclusivity: any vessel that can shoot without a crystal ignores the prize and it stops being worth fighting over for anyone. Five general rules came out of it, all platform-wide (`Docs/ECOSYSTEM.md §27`): (1) **a flora planting band is measured from the CELL CENTRE, not the crystal** — all three `Flora.Plant` implementations dispersed around `cellData.CrystalTransform` while `ResolvePlantRadius` and every docstring said "a fraction of the cell's membrane radius"; the two agree only while a mode's crystals sit in the core (now `Flora.ResolvePlantCenter`); (2) **a species plants in a volume-uniform BAND, not on a shell** (`Flora.plantRadiusCellFractionMin`, default 0 = legacy single shell) — a shell's space grows as r², so a uniform-in-radius draw crowds the inner edge and leaves the rest of the cell empty; the band's inner edge is CLAMPED outside the nucleus in code while that nucleus is a CONTROL ZONE, because nucleus mass is then the territorial claim and is excluded from the fauna targeting grids (a cell that sets `NucleusIsControlZone = false` has declared it has no such interior and the clamp lifts — `Docs/ECOSYSTEM.md §42`; the one reason that survives, a crystal respawning in that volume, is accepted as clutter rather than as unreachable mass); (3) **the omni-crystal respawn volume IS the nucleus, and no scene may override it** — `CrystalManager.GetAnchorlessSpawnRadius` resolves nucleus → `noNucleusSpawnRadius` (a fallback for a cell with NO nucleus, e.g. Dog Fight's Boneyard) → crystal `SphereRadius`; the nucleus is the visible marker of the cell's core, and a crystal that respawns elsewhere makes that marker a lie. A mode that wants a different crystal volume RESIZES ITS NUCLEUS (a `CellConfigDataSO` pointing at a resized `NucleusPrefab`), which moves both together; (4) **a cell whose prisms are not nominal must author its volume ladder, never inherit the `count x 16` derivation** — a cactus leaf is 5x5x3 = 75 volume, 4.7x nominal — and when this was authored the level spread multiplied it again by 4.31x (that spread is retired, `Docs/ECOSYSTEM.md` §40, so the factor is now exactly 1 and the arena boots that much lighter; Frenzy arriving LATER is the safe direction, and `Tools/Build/rampage_intensity.py` prints the re-measure note) — so the inherited thresholds were an order of magnitude too low and would have pinned the cell at Frenzy with planting frozen and a sparse arena that never regrew. (5) **environment mass is hostile by COLOUR, and it is credited by whoever SIMULATES the attacker** — `StatsManager` recorded prism destruction server-only on the stated assumption that "a prism sits at the same place on the server", which is true of a TRAIL (laid from replicated vessel motion) and false of flora/fauna (`CellNetworkSync`: every peer runs its own spawner off local `Random` rolls), so a client scored nothing for the entire living world and could only ever score off the other pilot's trail. Fixed with `Player.ReportEnvironmentPrismDestroyed_ServerRpc` — the third instance of the same owner-detects-server-records round-trip as `ReportFaunaKill_ServerRpc`/`ReportCombatHit_ServerRpc` — plus `StatsManager.OwnsAttacker`, which stops the server double-crediting environment kills it saw a REMOTE player make. `PrismStats` now carries the prism's `OwnDomain` so unrostered mass is friendly iff it wears the attacker's colour (`Domains.Blue` stays hostile to all), applying to the world the rule trails always had. Its corollary: a crystal collection resolves server-only, so the collecting pilot's own vessel effects (blast, resource spend, elemental level) never ran on their machine — `CrystalManager.ReplayVesselCrystalEffects` replays them on the vessel's OWNER while the server keeps sole authority over collection, respawn and stats. It also moved the AI's DRIFT look-direction off a flat 180-degree flip and onto a hostile-mass cluster from `Cell.GetExplosionTarget` (the fauna hunting query), platform-wide — and records the corollary that a mode whose objective is a crystal must NOT install an `AIPilot.SetExternalTargetProvider` hook, because that overrides crystal seeking outright. It has **4 intensities** the platform way (`CellTypeChoiceOptions.IntensityWise` over four `CellConfigDataSO`s, list order = intensity), and **intensity here is DENSITY, SIZE and SCARCITY around a fixed point: intensity 4 IS the shipped, play-tested arena and nothing about it moves, while 1 is that same arena made bigger, DENSER and easier to hit.** Five axes, all pointing the same way: flora **5.00x / 3.67x / 2.33x / 1.00x** the authored PLANT COUNT (`SpawnProfileSO.FloraPopulationScale` — 295 / 217 / 137 / 59 plants, 49,150 / 36,160 / 22,820 / 9,830 prisms), prisms **1.60x / 1.40x / 1.20x / 1.00x** the authored leaf (`SpawnProfileSO.FloraPrismScale`), nucleus **500 / 400 / 300 / 200** prefab scale (world radius 490 / 392 / 294 / 196u, one `NucleusPrefab` per config), crystals **2x players / players / players-1 (min 1) / exactly 1**, wildlife **1x / 2x / 3x / 4x**. The crystal is the Dolphin's only blast trigger, so its count IS how contested cashing out is. **Flora POPULATION is the ONE axis of that ladder that costs colliders** — size, nucleus and crystal count are all free — and it is priced on two separate lines, gated against cells the game already ships rather than against invented numbers: LOD-cullable prisms bounded by the cell's own `FrenzyEnter` COUNT backstop (**50,000** at intensity 1, since Frenzy freezes planting AND growth, against Atlantis' ~69,000) and **always-on heart crystals bounded by the plant CAP** (**440** against the Lattice cell's 1,080), one per live plant and culled by no phase. The per-plant BUDGET stays 1.00x at every level: more flora means more PLANTS, not bigger ones — growing the budget multiplies prisms without multiplying the thing the player reads, and compounds with `FloraPrismScale` on the very same prisms. Three things that generalise (`Docs/ECOSYSTEM.md §43`): **`SpawnProfileSO.FloraPrismScale`** is the third flora scalar (how big each PRISM is, beside how many plants and how big each plant gets), resolved on the `Cell` like its siblings and applied once in `Flora.Initialize` **before `base.Initialize`** — which binds and stamps the prefab's own seed prism, so applying it later leaves that one prism at the authored size; it is **not** a revived lifeform level, because it is a property of the CELL and every plant of a species in it is the same size (§40 intact); and its volume exponent is **PER FAMILY** — `BranchingFlora` lays `leafSize` on all three axes (s³) while `PhyllotacticFlora` reads only `leafSize.x/y` as a cross-section and takes its lengths from its own `segment`/`reach` (s²), so a phyllotactic strut gets THICKER, not longer, and assuming s³ everywhere overstates a 1.6x forest by 1.6x. A **lattice** species is exempt via `Flora.PrismSizeFixedByGrowthRule` — the guard §40 kept with no reader, doing exactly the job it was kept for. Because volume is the spine, each intensity's volume ladder is the play-tested intensity-4 ladder **scaled by its own forest ratio**, so intensity 4 reproduces to the digit and every level holds Frenzy at 4.11x its mature forest. ⚠ **Intensity 1 is now the heaviest cell in any arcade mode and is NOT profiled** — the arithmetic clears both shipped reference cells, which is the gate, but it is not a frame time; `SCALES[0]`'s population scale is the dial if it needs to come down. Four general capabilities came with the two passes (`Docs/ECOSYSTEM.md §28`, `§29`): (a) **`SpawnProfileSO.FloraPopulationScale` / `FloraPlantBudgetScale`** let a cell scale its whole forest without forking the per-species assets — apply them in BOTH spawners or they are dead code in the very modes that need them, since `IntensityWise` also swaps `RandomLifeSpawner` for `IntensityWiseLifeSpawner`; (b) a fix for a race that was ALREADY LIVE in every IntensityWise scene — **`Cell.AssignConfig` is sticky and its intensity arrives only in the config ClientRpc**, while a client's cell bootstraps off its first crystal ~600 ms earlier, so the client silently built intensity 1's arena for the whole match. `GameDataSO.GameConfigSynced` now gates the choice, and the deferral was made retryable (`InitilizePostFirstCellItem` used to latch on its first line, which would have left a deferred cell with no spawner at all); (c) **`SpawnProfileSO.FaunaPopulationScale`**, the fauna twin of (a) — it multiplies a species' `InitialSpawnCount`, `PopulationSize` AND `MaxLivePopulation`, because the CAP is what bounds a standing population and a scalar that moved only the floors is clamped away above ~1.5x and reads as doing nothing. Fauna has FOUR producers, not two (both spawners, `Fauna.TryReproduce`, the freestyle `Microscene` conveyor), so the resolution lives on the **Cell** — `Cell.ResolveFaunaPopulation` / `ResolveFaunaCap` / `IsFaunaAtCap`, the one object every producer already holds, and there is now no direct read of `cfg.MaxLivePopulation` outside the config and the profile. It gates PRODUCTION only; nothing is culled to meet a lowered scale; and (d) **`CrystalManager.CrystalCountMode.IntensityScaled`** — `max(1, round(players x CrystalsPerPlayer) + ExtraCrystals)` per intensity, list order = intensity. It needs NO `GameConfigSynced` gate (unlike (b)) because both intensity readers are server-side and clients receive the count as the replicated slot-list length — the difference between a value a client computes and one it receives. See `_Scripts/Controller/Arcade/RAMPAGE.md`. `Cleave(39)` is the **Rhino-only slicing race** — first domain to DESTROY its intensity's hostile-prism target wins (`ScoringMetric.PrismsDestroyed`, Rampage's metric and machinery), and the arena IS the score. **Intensity is WHICH PLACE you cut, not how much of it there is**: four unrelated arenas with four different verbs, one `CellConfigDataSO` each via `CellTypeChoiceOptions.IntensityWise` — **The Panes** (nine flat slabs at nine angles, each a corduroy of ribs with its own grain, with a dense straight BEAM down every pane-pair intersection: commit to a line), **The Swell** (five WIDE WAVY ROADS meandering closed circuits through the ball, painted as carriageways — gold crown, blue shoulders, jade verges — so a pilot who finds one holds the blade down and FOLLOWS it, and the only traps sit on the OUTER verge of the tighter corners: running wide on a bend bites, the racing line never does), **The Cage** (three nested rinds of prism bone, triangular openings, tightening inward, each rind tilted onto its own axis: peel inward) and **The Twistbands** (three interlocked one-sided Möbius ribbons carrying a plated deck that ROTATES about its own direction of travel, so holding a cut means continuously rolling the sword — the one arena that asks for the swordsmanship rather than the line). It replaced a ladder that was 2/3/4/5 nested shells — the same arena four times — and that is why the mode was renamed from "Cleave". 15,380 / 14,277 / 14,731 / 16,423 prisms at radius 2160 / 2160 / 720 / 720; the kept cage rung's geometry is unchanged. **Intensity is SPACE as well as place, and the envelope is a PER-INTENSITY table**: the family was first scaled a uniform 2x (360, smaller than a nucleus, to **720**) after a playtest read it as a little ball in an empty cell, and rungs 1 and 2 then went **3x further again** — 2160 radius, the Panes at **3x the rib spacing** and the Swell re-authored outright from corrugated sheets into wavy roads (same prism count, same ball, concentrated into a handful of surfaces you can STAY on rather than spread across seven you can only cross), so the two easy rungs are vast open places you cross rather than dense objects you peel. Their destruction target came down 2000 -> 400 and then back up to **1200** when the prism dial re-cut those arenas into three times as many pieces, and they carry their own 3600-radius `CleaveMembrane.prefab`, because the standard 1200 one would be INSIDE their arena. **The envelope became a TABLE the moment the rungs stopped being one size** (`SliceArenaGeometry.OuterRadiusI1..I4` + `OuterRadiusFor(intensity)`): three systems are sized against it — the AI's stations, the player spawn ring and the membrane — and one shared radius across a 2160-vs-720 spread either parks every AI inside the two big arenas or 3,000 units from the two small ones. Two of those three needed a new per-intensity surface (`ServerPlayerVesselInitializer.spawnRingRadiusFloorByIntensity`, `EndConditionOverridesSO.cleavePrismTargetByIntensity`), and all three resolve SERVER-side, so none meets the config-sync race a CLIENT deriving an intensity-keyed value would. **THREE dials per rung, and they do different things**: `LengthScale` is the SIMILARITY (how big is this place), `PrismScale` is what it is MADE of (how big ONE prism is, plus the along-grain step that keeps a run of them continuous) and `GapScale` is the across-grain STEP (how much of it is mass). Rules that generalise to any arena scale-up. **A count that is a ratio of two lengths does not move**, so the similarity leaves prism counts and the collider budget untouched (`floor(radius/step)`, `round(arc/step)`) — at 2x the harness re-measured IDENTICAL counts and exactly 8x volumes, which is also the proof no constant was left unscaled, since one would have moved a count. **A GAP scale is the one dial that moves a count** (down, by G) — and **it is only definable for an arena whose across-grain density is a STEP**: the Cage's density is a rib/hoop COUNT on a sphere, so a gap authored for it would be silently INERT, which is why `SpawnableRibcage.AssertNoGapScale` says so loudly rather than leaving a comment in a file nobody edits when they change the table. **It is also only WANTED where the surface is a set of BARS rather than a road** — the Swell and the Twistbands both HAVE a step and both author 1, because each lays a continuous plated deck a blade is held against and opening its lanes is a lattice the sword rattles through, not a lighter arena; rung 2 spent that dial once and the answer was to re-author the arena instead. **PRISM SIZE IS NOT PART OF THE SIMILARITY, and finding that out cost a whole pass.** It was for two passes, on the sound reasoning that a rib at 3x the spacing with the same plank is a dotted line rather than a bar — and what that produced at 6x was an arena built from 102-132-unit slabs, i.e. **LOW POLY**. *Destroying lots of small prisms is the fun; destroying one big one reads as cheap geometry.* So prism size became its own dial pinned at **2 on all four rungs** (`SliceArenaGeometry.PrismScaleI1..I4`) and the ALONG-grain step went with it, which is what keeps a rib continuous without making the pieces enormous, while the ACROSS-grain layout stayed on the similarity so the place stays spread out. **The cost is a COUNT**, because a count that is a ratio of two lengths only stays invariant while both lengths share a scale: the two 6x rungs rose by `S/P` = 3 (Panes 5,107 -> 15,380, Swell 4,607 -> 14,277), which forced their target 400 -> 1200 — *a target is a fraction of the arena, so re-cutting the arena re-prices it*. **One fleet-wide number is honest because rungs 3 and 4 prove it**: their `LengthScale` was already 2, so they re-measured byte-for-byte identical, and a wrong split anywhere would have moved them. Three more things fall out. Prism SIZE is free in colliders (only COUNT costs one), so spending that dial DOWNWARD is the one direction that is not free. Shrinking the prisms bought back the volume ladder's float32 resolution on rung 1 (345M -> 38.5M, ulp 32 -> 4, under a Rhino trail prism's 4.5) — rung 2 is still over the line at 103M. And **a shared prefab's scale ceiling was the only thing in the project saying the prisms had grown absurd, and it said it silently**: `PrismScaleAnimator` clamps PER AXIS inside the setter with no log and no return value, 363 of 404 prism prefabs inherit `maxScale` 10, and at 6x three of the Panes' lengths (102/108/132) cleared even `SpawnablePrism.prefab`'s 100 — so the four arenas opt into **`CellEnvironmentSpawnableBase.AdmitsAuthoredPrismScale`**, which routes their lay through `Prism.AdmitTargetScale`. Every size they state now (longest 44) is inside that window, so the opt-in is a standing GUARD rather than a fix. That opt-in is opt-in rather than global because the prefab is shared by ~30 spawnables and admitting a size one of them RELIES on the clamp to cut is a behaviour change for that one; and it must run **AFTER `Initialize`**, because `Initialize` -> `ResetState` -> `RestoreAuthoredScaleWindow()` undoes the widening and then re-clamps the target (the trap `ScarabSwitch.TryLay` already records). **Keep the factor an exact power of two where you can**, so every scaled constant is bit-exact and no `floor` boundary or noise sample can land on the other side of itself; 6 (= 2 x 3) is not, so those rungs are re-MEASURED rather than assumed. The numbers that are NOT pure scales are the spawn ring and the membrane — at 2x the ring went 576 -> **1050**, not 1152, because the MEMBRANE did not scale, and at 6x the membrane DOES scale so the ring is a clean x3 (3150) of that play-tested pair — *when only part of a system scales, the interfaces between the scaled and unscaled halves are what has to be re-derived by hand*. **A uniform k x similarity is a k³ change in what a float32 volume accumulator has to hold**, and that has a cliff: `Cell.liveVolumeTotal` is a float32 RUNNING total, the 6x rungs reached baselines of 345M and 898M where its ulp is 32 and 64, and a Rhino trail prism is **4.5** — so adding one was a no-op and the volume phase ladder could not move at all on either. The prism dial fixed half of it (rung 1 is now 38.5M, ulp 4); **rung 2 is still frozen** at 103M. Harmless only because this cell authors no flora and no fauna, which is exactly why it is a GATE (`cleave_budget` check 7 computes the ulp, reads the spawn profile, and fails the day somebody gives Cleave a food web) rather than a paragraph. The retired assertion is worth as much as the new one: **a monotone prism COUNT across the ladder stopped meaning anything the moment the target became per-intensity** (and the counts are deliberately not monotone now, though the prism dial has since brought all four within 15% of each other — which leaves the ladder FLAT in colliders at 23% under the ceiling, so there is no cheap rung left to spend), so it was replaced by the thing that actually has to hold, *every rung's arena holds a comparable multiple of its own target in hostile mass* (measured 7.4x-9.3x, asserted 4x-12x). ⚠ `bonusLevels = deficit x rate` makes the comeback rate a function of the TARGET, and this mode has now paid it TWICE: at the 500/2000 ladder the inherited 0.01 bought 1.25 element levels at a quarter-of-target deficit, and the cut to 400 would have taken that to exactly 1.00 — sitting ON the one-whole-level floor with no margin — so the rate went to **0.0125** in the same edit (1.25 at 400, later 3.75 at 1200, 4.69 at 1500) and `author_cleave_assets.py` now FAILS the build on the floor rather than leaving it to a paragraph (the fifth outing of that trap, after Dog Fight, The Bends, Wildlife Liberation and Tollway). Five things generalise. (1) **Nothing in any arena is shielded, and the binding reason is the AI**: a super-shielded prism can only be popped by an ENERGIZED blade, energizing needs the both-triggers stance, and `AIPilot` never pulls a trigger (`RHINO_ENERGY_SWORD.md` says so outright) — so hardened mass is mass an all-AI domain can never score against, in a mode scored on destroying it. The Tollway rule, reached from a new direction, and now ASSERTED rather than documented. (2) **Four arenas that vary independently still need ONE envelope** — `SliceArenaGeometry.OuterRadius` (360) — because the AI's stations (`× 1.3`), the spawn ring (576, this cell has no nucleus) and the membrane (1200) are all sized against it and none can be told which intensity is running; the ordering is asserted on the prism's FAR CORNER, not its lay point, since a lay at exactly 360 still puts geometry outside 360. (3) **A generator whose output can be CULLED by noise should be MEASURED by running it, not modelled** — `Tools/Build/cleave_arena_harness` COMPILES the four shipped `Spawnable*.cs` straight out of `Assets/` against a Unity shim and counts what they emit, with every source that could move a count hashed into the committed JSON so a stale measurement fails loudly; it reproduces the cage's shipped 14,731 exactly, which is what proves it faithful. (4) **The model it replaced was wrong by exactly 2×**: `ribcage_budget.py` computed `E[k³]` for `Jit(s, 0.2)` as `(1.2⁴−0.8⁴)/(4×0.2)` = 2.08 where the range's WIDTH is 0.4, so the answer is 1.04 — its own comment said 1.04. Since **volume is the spine** (`CellPhaseThresholds.Compute` steps on volume and uses count only as a Frenzy backstop), every shipped volume threshold was ~2× the arena's real volume and the cell's live volume sat permanently below `RestlessEnterVolume`: the ladder described a cell twice as heavy as the one that exists and never moved. ⚠ `Tools/Build/wildlife_cage_budget.py` still carries the identical expression and is deliberately untouched — correcting it moves Wildlife Liberation's fauna-release gate, so it is a balance change, not a bug fix. (5) **A `--check` that never reads the disk is not a check**: this mode's generator used to re-run its in-memory validation and print "no files written", passing whatever the assets actually said; it now diffs every authored file against disk. Its Rampage scene clone also STANDS DOWN once the scene exists rather than asserting on a donor that has moved on — the `author_dogfight_assets.py` trap, avoided deliberately. See `_Scripts/Controller/Arcade/CLEAVE.md`. Meta sentinel: `Random(0)`. Note: IDs 7, 31 and 47 are skipped — 7 was the retired standalone arcade Freestyle game (freestyle now lives in Menu_Main as the lava lamp; see "Lava-Lamp Mode"), 31 was never assigned, and 47 was **Drumfire**, the Dolphin-only rhythm range removed in 2026-09 because it read as Rampage (same hull, same weapon, same verb) without offering enough of its own; its lane geometry survives as a platform capability (`ApproachLaneGeometry`, `CrystalManager.CrystalPlacementMode.ApproachLanes`, `ScoringMetric.VolumeDestroyed` and its comeback pair — all kept, all currently unused). Do not reuse any of the three IDs.

`Tollway(48)` is the **Scarab-only ring race** — and the mode built on the one Scarab idea no
shipped mode had ever used: **a switch pays its PLACER when ANY ball threads it, friend or
enemy** (`SCARAB.md §5` calls it "the design's best idea"). Plants grow in the court and a ring
may only be **grafted onto a living plant's heart**; every ball that threads it — yours, theirs, a
stray off the wall — pays the pilot who planted it and raises a
255-prism scarab-wing monument on the spot, so **the arena is built out of the scoring** and the
scoreboard is readable off the terrain. Rings are CONSUMED when they pay and must be replanted,
which is why the switch's charge had to start recharging in the same branch (`SCARAB.md §5.2`) —
before it a pilot could place exactly one ring per life and the mode was not buildable. **The
anchor is the mode's load-bearing rule and it shipped without one once**: with placement
unconstrained and any ball paying the ring's owner, the whole game was ONE MOVE — plant a ring in
front of your own ball, nudge it through, repeat — and a minigame with no shot to get better at is
not infinitely replayable. An anchor fixes the WHERE and deliberately not the FACING (the ring's
axis is still the course the placer flew in on), so what is left is which plant to claim — a read
of where the traffic is — and a real shot: drive a ball across the court into a mouth two dozen
units wide. It is a guarantee rather than a tuning, because a ring's position is no longer a
function of the ball at all. The general rule, which belongs to any future place-a-structure
ability: **if a player picks both where a scoring surface goes and what goes through it, the two
collapse into one move — constrain one of them.** **The anchor rule belongs to the VESSEL, and the
mode's first cut built its own sockets instead** — `TollwayTollPosts`, a seeded band of emblems
that the controller built, replicated, drew, tested and re-derived in Python. It worked and it was
the wrong owner: `ScarabSwitchAnchors` now snaps EVERY Scarab's switch onto the nearest free flora
crystal on its flight path, in every arena, and Tollway only supplies the plants and the refusal.
A flora crystal arrives with four things a bespoke socket had to be given by hand — it is placed by
the FOOD WEB (so the set is alive: grazeable, re-seeded), it is already drawn and already a thing a
pilot flies at, it is already replicable (`FloraConfigurationSO.NetworkSynced`), and it is a
joustable heart, so killing an anchor to deny it AND take an element level is counter-play nobody
designed. General rule: **before a mode builds a set of points of interest, check whether the
platform already grows one** (`Docs/ECOSYSTEM.md §42`). It forced one platform correction:
`Flora.ResolvePlantRadius` clamped its band outside the nucleus UNCONDITIONALLY, so a mode whose
nucleus IS the court could not seed a plant inside its own arena — both stated reasons for that
clamp ARE the control zone, and the clamp now reads `Cell.NucleusIsControlZone`, which is §25.1's
trap from the other side (there a mode inherited the nucleus' DIET semantics with its geometry and
its food web silently did nothing; here it inherited the planting exclusion). Its playtest
correction is the second half of the constrain-one-of-them lesson: the
admission test asked whether a free anchor lay within 70u of the RING CENTRE, which the ability puts
**150u ahead of the nose**, so a press was admitted only in a shell 80-220u from a post and refused
at every range inside 80 — and the HUD arrow points AT an anchor, so following it closed the only
window that worked. The AI was unaffected because it presses on a pacing timer while still
approaching, which is why the first report read "the AI placed rings at the right points, I could
not place any at all". Two rules: **when an ability's effect is offset ahead of the vessel, a
proximity gate on the OFFSET POINT is an annulus, not a radius** — the hole in the middle is
point-blank, exactly where a guided player will be, so gate the PATH (`[ship, ring centre]`), not
the projected point; and **a refusal that only logs is indistinguishable from a dead button**, which
is how a placement rule nobody could satisfy reached playtest — a refused press now posts a toast,
fenced to the refused pilot's own machine as a side effect on the way OUT of the resolver, so the
pure-function-of-replicated-state contract still holds. **A mode that RELIES on the snap must make
its flora `NetworkSynced`** — placement re-executes on every peer and nothing about a placed switch
is replicated, so the anchor set has to agree, and flora are per-peer by default (every machine
rolls its own spawner); Tollway is the first shipped user of `FloraNetworkSync`, which puts the
planting DECISION (species, root pose, domain, element) on the wire and therefore the heart's
position with it. Freestyle and Scramble author no flora at all, so there the snap is an assist, a
miss places free, and their behaviour is byte-for-byte unchanged. Occupancy reads
`ScarabSwitch.Live`, so the claim book cannot desync further than the switch list already does. First
DOMAIN to the toll target (default **4** — re-derived down TWICE, 12 → 8 when a toll became
anchored and 8 → 4 when the switch went to ONE ring at a time on a 60 s recharge, taking the
comeback rate 0.5 → 0.75 → 1.5 with it, since `bonusLevels = deficit × rate` makes the rate a
function of the target) wins on `ScoringMetric.Goals`, reused because the SHAPE of
the race is Astro League's; what differs is what a goal IS. Three rules invert its two siblings:
the scoring surfaces are **placed by players onto the arena's own living plants and spent on use**
(so which plant the next ring goes on is the strategy layer); **you score off other people's shots** (so the defensive play and the
economic play are the same play — rings belong where the enemy's balls are going, and herding
your own ball through an enemy ring scores AND refunds for them); and a ball is **not** spent by
a toll (Scramble detonates a scored ball because its hoops are permanent and its balls scarce;
here it is the other way round, so one shot through two rings is the signature `CHAIN` toast).
Intensity is **traffic** — court radius up, crystal count down, and `CellTypeChoiceOptions.IntensityWise`
over four cell configs each growing **its own anchor species — one per GROWTH FAMILY**
(Spire `PhyllotacticFlora` → Gyroid `AssembledFlora` → Cacti `BranchingFlora` → Quasicrystal
`AssembledFlora`), so the marker grows with the court (570 → 5,103 volume per plant) and each
setting is visibly a different KIND of place rather than four sizes of one. The generator asserts
the roster spans as many families as four intensities can and that no family takes more than an
even share, with both bars **derived from what the project ships** (it counts the distinct `Flora`
subclasses across the flora prefabs — which is also what excludes SeaweedFlora's `SegmentSpawner`
and oldWallFlora's `GyroidAssembler`, two prefabs in that folder carrying components that are not
`Flora` at all) rather than written as literals. **A lattice species keeps its own per-plant
budget**: a gyroid octagon is 24 prisms around one crystal and a quasicrystal heart cell is one
vertex's strut tree, so a cell-imposed number truncates a shape mid-figure — the same "plant COUNT
is the only lever" rule `Docs/ECOSYSTEM.md §32.7/§36` records, met here from the arena side. Two
traps came out of authoring across three families that do not share a shape of authoring, and both
generalize. **A SENTINEL IS NOT A MEASUREMENT**: `FloraVariantTuning`'s `LeafSize: {0,0,0}` and
`MaxTotalSpawnedObjects: -1` mean *keep what you have*, and reading the zero as a real leaf priced
the Cacti at 56.25 volume against its true 75 (one element authoring the sentinel, three omitting
the block, averaged) — a measurement layer must resolve a sentinel the way the RUNTIME does, and a
`\d+` regex that skips `-1` by accident rather than by rule is the same bug waiting. And **a
component's fileID in a `FloraPrefab` reference differs by family** (`PhyllotacticFlora` and
`BranchingFlora` share `7514956980722975813`, `AssembledFlora` uses `8186157953239024492`), so it
is copied per species from the shipped element asset — a wrong one resolves to no component at all
and grows nothing, silently. **The ring-mouth rule an earlier pass shipped here is RETIRED**: it
proved each species' body rises out of the 24u ring planted at its heart, which measured something
real and gated on something that does not exist — *"The ball NEVER physically collides with prisms
— it passes through ALL of them and resolves them by domain via a per-tick spatial scan"*
(`AstroLeagueBall`), so a plant cannot block its own ring and what its mass in the mouth actually
does is get resolved by domain as the ball passes (an opposing plant destroyed, an own-domain one
shielded — the food web and the scoring meeting each other). General rule: **before gating a design
on a clearance, find out what actually has to pass through the gap.** Its platform contributions are
`ScarabSwitch.OnThreaded` + a `Live` roster (at the merge base a threading raised the dais and
told nobody, so nothing outside the class could observe the event the ability is built around) and
**`PlaceSwitchActionExecutor.PlacementResolver`**, the sibling of `ScarabBallForge.ForgeGate` — a
mode's veto on WHERE a ring may go, null everywhere else so freestyle and Scramble are unchanged.
Two constraints travel with it: it is consulted on EVERY peer so it must be a pure function of
replicated state, and it runs BEFORE the charge is spent so a refusal costs the pilot nothing.
Two general rules it records: **an AI's PLACED STRUCTURE must go through
`R_VesselActionHandler.PerformShipControllerActionsReplicated`, never `AIPilot.abilities`** — an
AI runs server-only, so a local `StartAction` lays conserved mass on one machine and shows it to
nobody, and here it is not cosmetic (an AI that cannot plant a ring cannot score, so an all-AI
domain would be an opponent that could not play); and **when a mode's SCORE IS A MONUMENT its
volume ladder must be restated in monuments** — a toll is a 50,773-volume dais, so Scramble's
gates would both be crossed before the race was half run, and the Tollway cells are forked from
Scramble's for that (Restless = trail band + the standing anchor forest + 3 monuments, Frenzy + 7,
out of the 10 a maximum-length 4-toll match can raise). **Both gates are stated as fractions of a
maximum-length match, and that is the third outing of the comeback-rate trap in this one mode**:
the assert that kept Frenzy out of the early race was written as `trailBand + 8 × daisVolume`,
where 8 was the toll target transcribed as a literal — it stopped meaning anything the moment the
target halved while still passing. *A threshold that is a function of the target must be written
as one.* The spawn profiles are forked too, because Scramble authors `SupportedFloras: []` and this
mode's scoring sockets are plants: 14 NetworkSynced flora per intensity — **14 always-on heart
colliders at every setting**, which is what keeps the collider budget flat while everything else
about the field changes (420–1,540 prisms, 7,986–71,441 volume, so BOTH phase ladders are
per-intensity where a single-family roster could share the count one). A quarter of the plants are
Charge and therefore shielded, so the field thins unevenly as the cleanup crew grazes it. The Scarab's switch **recharge is drawn**
as the fleet's clockwise depleting veil on the MASS card (`ScarabHUDController` →
`VesselHUDView.SetAbilityCooldown`), off the resource event the controller already had: the pip
count says how many rings you HOLD, the veil says whether the button does anything right now — a
tank of three cannot say both on one dial. **The switch itself is now ONE RING AT A TIME**
(`chargesPerFullMeter 1`, `maxLiveSwitches 1`, `rechargeSecondsPerCharge 60` — a VESSEL change, so
it moves freestyle and Scramble too): with a three-charge bank the interesting decision was *when
to spend the stack*, and a pilot who banked could answer a rival's ring by planting three of their
own, which left the anchor rule constraining only WHERE while the bank covered three wheres at
once. What keeps the loop turning is that a threading refunds the WHOLE meter, so a ring somebody
uses is free and only a wasted one costs the minute. See
`_Scripts/Controller/Arcade/TOLLWAY.md`.

`Headlong(49)` is the **Rhino-only circuit race**, and the first mode cut against a VESSEL'S OWN
TURNING GEOMETRY rather than against an arena. A closed loop of switch rings is laid through the
cell and every pilot flies **laps** of it in order; the first DOMAIN whose LEAD RUNNER threads
the last gate of the last lap wins, on the SAME metric, fold, goal row and objective icon as
Switchback (`ScoringMetric.SwitchesThreaded`) — reused, not added, because a lapped circuit's
gates are threaded in order exactly as an open chain's are. **The mode is cut against a CURVE,
not a threshold.** The Rhino's ramp boost pays full power only while the pilot holds full
throttle and near-zero stick, and past that it does not switch off — it GRADES DOWN, lerping the
boost multiplier toward plain cruise as the stick goes over (`RampBoostActionSO.MultiplierFor`,
`RHINO_RAMP_BOOST.md`). Composing that one lerp with two formulas that were already there — turn
rate is linear in stick, and max turn rate grows with speed — gives a continuous speed/radius
trade from **622 u at 840 u/s** down to **29 u at cruise** (332 u at 1200 u/s before the
2026-09-25 Rhino retune — top speed to 70%, `RotationThrottleScaler` 0.5 → 0.2 — which nearly
doubled the flat-out circle in a fixed shell and made every level harder; level 1's safety floor
went 0.62 → 0.75 to keep it hairpin-free, `RHINO_RAMP_BOOST.md`), so a corner is an OPTIMISATION (the
largest speed whose radius fits, traded against how long the following straight is) rather than
a binary classification. Before the grading there were exactly two points on that curve and
therefore one decision per corner, which a play test reported as nothing to master; authoring
`straightnessGraceBand` back to the engage threshold restores that latch exactly. It is the
Rhino's mode because its turn radius **converges** with speed (`RotationThrottleScaler` 0.2,
asymptote `180/(pi*r)` = 287 u; 115 u at the pre-retune 0.5) where every other hull's grows without bound. **Intensity is
what MIX of corners a lap asks for** — a median lap costs speed on 2 / 4 / 4 / 5 of its 8
corners, its hardest corner taking a pilot to 92% / 66% / 47% / **34%** of top speed (measured
after the 2026-09-25 retune; 1 / 1 / 2 / 3 and 99 / 82 / 65 / 37% before it) — and level
4 spends its whole 360-degree turning budget on three corners, so it is a TRIANGLE with gates
down its sides: three braking zones and three long straights to wind the ramp back up. **A FLOOR
IS A PERMISSION, NOT A DEMAND**, and that is the finding worth carrying: the first ladder
authored only a lower bound on corner radius and trusted a rising random perturbation to
"genuinely PRODUCE sharper corners", and measured over 600 seeds it did not — a symmetric
perturbation makes as many corners wider as narrower and the floor then deletes exactly the
courses that got interesting, so **93% of intensity-4 corners were takeable at full speed with
no lift at all** while every test in the suite passed. `CornerProfile` now authors the TURN
ANGLES a lap is built to (angles rather than radii because a closed loop turns through exactly
360 degrees, so an angle profile is satisfiable by construction while a radius profile can ask
for eight corners the ring cannot give, saturate every vertex of the solver together and hand
back the neutral ring), and the ladder is asserted on the MEDIAN lap's cornering demand rather
than on its extreme. Three things generalise. (1) **The gate-race machinery is now a platform**
(`GateRaceController` + `RaceGateRing` + `RaceGateTurnMonitor` + `RaceGateObjectiveProvider` +
`GateRaceScoringRuleSO` + `RaceCourseGeometry`), extracted from `SwitchbackController`'s 780
lines rather than forked; a subclass supplies its NAME, its COURSE and whether that course
WRAPS. **LAPS cost one method** — `RingIndexFor` wraps a threaded count onto a ring and
`RaceLength` is `rings x laps` — and `SwitchThreadScoring` needed no change at all, because its
validation is a bare equality against the pilot's own count with **no upper bound**; a cap there
would have made laps a special case in the one place that has to stay a single equality test.
(2) **A generator whose constraint is SHAPE need not be able to fail.** Switchback's walk solves
reachability and can legitimately return null; `HeadlongCircuit` solves each vertex's turn angle
by bisection against the authored profile and then RELAXES the whole shape toward a neutral ring
— always legal — until the shell, the mouth separation and a SAFETY corner floor hold. Three
shape rules came out of building it to a profile: **both shape terms must be CONTRAST**
(measured against the ring's own mean, or an over-asked profile drives every vertex out together
and inflates the ring into a LARGER regular octagon — gentler corners, in the name of tightening
them); **radius alone cannot cut a tight corner** (on a circle the corner radius is exactly
`BaseRadius x cos(gap/2)`, so a narrow gap shortens the leg and softens the turn in the same
proportion and the two cancel — a hairpin needs a vertex driven OUT between two driven IN with
its gaps pulled together); and **demanding corners must be dealt APART rather than shuffled
freely**, because two adjacent vertices both asking to be the spike cancel each other and the
solver settles for two medium corners (measured: adjacent 135 and 100 degree targets both came
out at ~92). A fourth is about the GATES: a gate faces its corner's bisector, so **the
presentation cap must cover half the level's hardest turn** — a 149-degree hairpin presents its
mouth 74.4 degrees off the arrival line however the jitter is spent, and a cap below that clamps
the jitter budget to zero at exactly the corners that most need orienting. (3) **Gate 0 is
placed by a RIGID ROTATION of the finished circuit** onto the spawn ring's pole — free, because
every property the relaxation established (corner radii, turn angles, leg lengths, distance from
the centre) is rotation-invariant. The extraction also found a real bug: the AI target provider
tested `index >= _course.Count` and indexed `_course[index]` directly, so on a lapped circuit
every AI would have finished after one lap and loitered. See
`_Scripts/Controller/Arcade/HEADLONG.md`.

`Skein(51)` is the **Urchin-only cable race**, and the first gate race whose rings sit ON the
arena's own structure rather than in the space between it: the course is a **(2,3) torus knot**
wrapped in a cable of helical rails, and you fly it by RIDING (the Urchin's prismscape grind at
300 u/s against its 65 u/s cruise, a **4.2x** speed advantage that is what makes riding the only
sane line). First DOMAIN whose LEAD RUNNER threads the last ring wins, on the same
`ScoringMetric.SwitchesThreaded` / `BestByDomain` fold and the same `GateRaceController` platform
as Switchback and Headlong — reused, not added. Every rail is **open** and **broken**: a strand
runs 750 u, then 900 u of nothing, and the far side of that hole is provably a DIFFERENT curve
(`prove_no_self_bridge`), so the mode's verb is *change strands* and every break is AIMED — the
generator trims each one until its tangent ray passes within 12 u of a live foreign rail inside the
pilot's own decision window. **The arena is authored in the PILOT'S TIMES, not in distances**, which
is the rule to carry: doubling `TrailFollower.FriendlyTerrainSpeed` moved every one of them, the
three constants written as `seconds x speed` re-derived themselves and the three written as
distances each had to be found by hand — so anything describing what the PILOT does is a time and
anything describing what the GEOMETRY is stays a distance. **Intensity is how much of the race
NAMES A CURVE**: a ring is either PINNED to one strand (reaching it costs a whole transfer — ride to
a break, take its aimed launch) or a COLLAR centred on the spine and wider than the cable, which
every strand threads and which therefore asks for nothing. Intensity 4 pins every ring over six
laps (the shipped arena, unchanged draw for draw); intensity 1 pins none over one, and **promises
that the next ring is already on screen as you thread this one** — measured 100% against the 32.5
degree half-frame a riding pilot actually sees (`GraphicsSettingsData.DefaultFieldOfView` 90 less
the speed tunnel's saturated 25, halved), so the objective arrow is decoration there. Two things
generalise from that ladder: **a course's readability is a property of its SPACING against the
arena's curvature, not of which lane the next objective is in** (past 600 u of a knot that turns 360
degrees in 8,030, "same strand" and "different strand" measure the SAME off-heading angle — both
candidate fixes were the same non-fix); and **when a rung promises something, assert it AT THAT RUNG
and assert the ORDERING everywhere else**, because a bound every rung must pass is a bound the
hardest rung sets, which is the opposite of a ladder. `Tools/Build/skein_budget.py` is the offline
authority — it PROVES the knot, the cable, the launches, the paint and the ring course over a seed
sweep, FAILS the build, carries 14 negative controls that must all fire, and now also parses
`SkeinCourse.cs`'s own per-intensity arrays so the ladder cannot drift between the model that proved
it and the generator that runs it. Its **known open limitation** is the one a rail race on a
fixed-timestep trigger is always exposed to, and it is another outing of the platform's own *"a
fixed-timestep trigger is a SAMPLE, not a test"* rule: a prism attach is dispatched from
`OnTriggerEnter`, sampled once per 0.04 s, while `MoveShip` TELEPORTS — so a launched pilot doing
255–306 u/s jumps **10.2–12.2 u** between chances to be noticed, against a catch chord of
**7.5–8.6 u** through the target rail's tube. `measure_attach_latch` reports the exact
`P(latch) = min(1, chord/step)` per intensity — **81–87%**, so roughly one aimed launch in six
slips past the rail it was aimed at (survivable: the pilot is still gliding and catches it on a
later pass). It is REPORTED rather than gated because the obvious fix is a fatter prism and
`prove_shield_clearance` is the wall that runs into — at MASS 5 a rail's armour reaches `1.5 ×
leafSize`, so an 8-wide rail's armour meets its neighbour's and "which rail am I on" loses its
answer; the structural fix is a swept vessel attach through the existing
`ImpactorBase.AcceptImpacteeFromSweep` seam, which would fix Hijack's re-attach at the same time.
The general rule it leaves behind: **a dimension chosen to clear a SPEED is a function of that
speed, and nothing fails when the speed moves** — the prism was sized against a 150 u/s grind, the
grind doubled and gained a 1.2× launch kick in the very next round, and the only thing that
changed was a paragraph of arithmetic quietly going on describing the old vessel.
`prove_vessel_mirror` now re-reads all five vessel constants out of `Urchin.prefab` and
`GunVesselTransformer.cs` on every run, so the model can no longer assume a vessel the project
does not ship. See `_Scripts/Controller/Arcade/SKEIN.md`.

`DogFight(41)` is the **Sparrow-only gun duel** — 2-4 pilots hunt each other through the
**Boneyard**, an apocalyptic wreck-field of hollow hulks and rubble canyons built for close
encounters and hiding places (inspired by Scurry's intensity-4 Atlantis, and its opposite: a
world that fell rather than grew). A **bullet hit scores 1** — BOTH of the Sparrow's direct-fire
modes, full-auto rounds and turret-stance prism rounds, since they are one weapon class — a
**rocket scores by HOW CLOSE it got** — 10 for the warhead shockwave, 20 for the prism blast, 30
for a direct strike — and the first **DOMAIN** to the point target (default 90) wins. **Those
three tiers are RANKED, not additive**: one skyburst reaches a pilot through three concentric
radii and a victim inside the inner one is always inside the outer ones, so `VesselCombatHitLatch`
folds all three onto ONE window per victim and pays the best tier achieved (a centre-punch is 30,
not 60). The latch **upgrades** rather than first-wins, and that ordering is forced by geometry:
the warhead is both the largest radius and the fastest to expand, so on an ordinary proximity kill
the CHEAPEST tier lands first and first-wins would pay a centre-punch as a graze — `TryAdmit`
reports what an admission supersedes and `CombatHitScoring.Credit` pays only the difference,
without re-counting the raw missile hit. It never revises DOWN. Its metric,
`ScoringMetric.CombatPoints`, is the platform's first whose source is **vessel-vs-vessel gunnery**
rather than prisms, crystals or the ecology — and the weighting lives in the mode's own
`ScoringRuleSO.PointsForCombatHit` (0 everywhere else), so hits are COUNTED platform-wide and
SCORED only here. It is a TEAM race and not a free-for-all for a structural reason:
`Projectile.DisallowImpactOnVessel` refuses own-domain contact, so two players sharing a domain
could not fight at all — domains ARE the sides. Shipping it also gave `AOEConicSkyBurst.prefab`
the explosion container it never had, so a skyburst's BLAST can now reach a pilot instead of only
its direct hit. **A bullet could not reach a pilot at all until 2026-09, and the wiring was
never the problem**: PhysX samples a trigger once per FIXED timestep (0.04 s here) while the
projectile mover TELEPORTS `position += Velocity·Δt`, so a Sparrow round covered 15 u between
samples at its base 375 u/s against a ~6 u hull window — ~60% of otherwise-perfect shots passed
straight through a pilot, ~97% at SPACE 10, with a correctly-wired scoring effect the whole time.
Prisms were already swept (`sweptPrismDetection`); vessels were not. Both gun rounds now carry
**`sweptVesselDetection`**, the exact twin — a capsule OVERLAP rather than a sphere cast, because
a cast ignores colliders it starts already inside, which is precisely the round-is-mid-hull case —
and the trigger path is suppressed for vessels on a sweeping round so nothing double-dispatches.
General rule: **a fixed-timestep trigger is a SAMPLE, not a test, and a fast enough projectile is
invisible to it** — and the failure presents as a weapon that works on the arena and not on
people, because the arena is swept and the people are not. **Both sweeps rent their scratch
buffers BY DEPTH**, because dispatching a swept contact runs the effect list synchronously and a
chain-firing effect re-enters the sweep mid-iteration — a shared list then drops the parent's
remaining contacts and re-dispatches the child's (measured). The prism sweep asserted
non-reentrancy in a comment and the Urchin disproved it; the vessel sweep repeated the assertion
and it was true only because `ProjectileChainFire` happens to be authored as a projectile-PRISM
effect, so the ship arm cannot reach it. **An absence guarded by which container an asset was
dropped into is not guarded by the code** — take the class, not the instance. Two tuning lessons
are recorded there and generalize: (1) **a comeback rate is a
function of the target** — `bonusLevels = deficit x rate`, so `ComebackRatePerScoreDeficit`
survived a 500 → 120 → 90 target change and quietly became worth 0.2 of a level, and the generator
now FAILS if a quarter-of-target deficit buys under one whole element level (the mode leans on
this: Mass stretches the Sparrow's fired prisms **and now their hit sphere too**, so the trailing
side's rounds both look and land bigger — the other three rise with it, because equal-elements is
the law, and Mass is simply the only one wired to that vessel's gun output); (2) **a cell with NO
NUCLEUS must author `CrystalManager.noNucleusSpawnRadius`** — that field falls back to the
nucleus radius, so without it every omni crystal falls through to its own `SphereRadius` and
spawns on the arena's exact centre, where a big faceted sphere reads as the objective (it was
mistaken for an Astro League ball). The fix is the radius, never switching the crystal off.
Two further lessons came out of its first playtest and are recorded there: (3) **a weapon is born
at its MUZZLE, so a muzzle transform is gameplay, not decoration** — the Sparrow carries a
separate gun pair per fire mode and the turret's had drifted to `z = 15.13` against the bullets'
`1.30`, so every turret round spawned 15 units past a close-range target and the whole fire mode
did nothing, with correctly-wired scoring; and (4) **an AI break-off must be a LATCHED decision,
not a function of the enemy's current position** — recomputing the escape point each frame makes
it flip the instant the AI passes its target, which welds the two ships into a grinding circle
and hides every standoff weapon the AI owns. See `_Scripts/Controller/Arcade/DOGFIGHT.md`.

`Bends(42)` (display name "The Bends") is the **Dolphin-only debuff duel** — a dogfight with no
guns in it. Every pilot flies a Dolphin, whose one offensive act is a cone armed **only by
skimming** and fired **only by touching a crystal**; Rampage paid you for aiming that cone at a
forest, and this mode changes nothing about the vessel and pays you only for catching an
**opposing pilot** in it. A caught pilot takes the blast's all-element decaying debuff — one
**bend**, 1 point — so nothing is destroyed and nobody is removed: the victim is simply worse
at the mode for four seconds (narrower cone, shorter reach, slower crystal seeding, weaker
boost), which makes the whole fight about that window. **First DOMAIN to 3 wins** — a race to 3
like Joust, so a bend is a whole-match event rather than a tick and a blast that catches two
opponents at once takes two thirds of the match. It is a team
race for the same structural reason as Dog Fight (`ExplosionImpactor.AcceptImpactee` declines
own-domain vessels, so you cannot bend a teammate at all). It **reuses Rampage's arena outright**
— the same four per-intensity cactus-forest `CellConfigDataSO`s, referenced not forked, so
intensity still means crystal SCARCITY — which is the "the Cell owns the environment" rule applied
to a whole world: the two modes want the same place because they are the same vessel economy, and
differ only in what you aim at. Its load-bearing platform change is that
**`AOEConicExplosionImpactorDataContainer` shipped EMPTY**: the Dolphin's blast had always
destroyed every prism it engulfed and done nothing at all to a pilot in the same volume. It now
carries the (authored, previously unwired) elemental debuff plus a combat-hit report, so the blast
debuffs a pilot in EVERY mode and only this mode's `ScoringRuleSO.PointsForCombatHit` pays for it
— the same counted-everywhere/scored-once split Dog Fight established. Four general lessons came
out of it: (1) **a validator that tests for one enum member and collapses the rest onto a default
encodes the enum's current SIZE** — `Player.ReportCombatHit_ServerRpc` mis-filed every client's
`CombatHitClass.Debuff` as `Bullet`, which this mode pays 0 for, so a client could fight a whole
match and score nothing while the host scored normally (now `Enum.IsDefined`, already the idiom two
methods below it); (2) **a blast that is REPLAYED onto a second machine double-credits** — a
crystal collection resolves server-side and `NetworkCrystalManager.ReplayVesselCrystalEffects`
re-runs the vessel effects on the owning client, so unlike a pooled local projectile a client's
one blast exists on both the server and that client, and `VesselCombatHitLatch` is per-machine and
cannot see across the wire; the gate is `IPlayer.IsNetworkOwner` (never `IsLocalUser`, which would
drop every AI's hits); (3) **a comeback rate is a function of the TARGET, and re-targeting a mode
silently kills it** — the same trap Dog Fight recorded, hit again 20x harder when this mode's
target went from 60 to 3: the rate that bought 6 element levels at a quarter-of-target deficit
would have bought 0.3 of one at the same FRACTION of the race, so it was rescaled 0.4 → 4.0 and
`author_bends_assets.py` now FAILS the build if a quarter-of-target deficit stops buying a whole
level; (4) **a score must not be able to disagree with the effect it is scoring** —
an elementally immune victim takes no drain, so the scoring effect is authored to require a
debuffable victim, which also turns immunity into real counter-play for free — and its corollary,
**a ward has a SCOPE, because "immune" is not one promise**: the Dolphin's Time-5 Drift Ward was
authored against DANGER PRISMS and, held as an unscoped grant, also cancelled the crystal blast's
debuff, which is this mode's only scoring event — in a mode where every pilot is a Dolphin and the
comeback buff hands Time 5 to whoever is LOSING, so falling behind bought a hard counter to the
only way you could be scored on. Fixed platform-wide rather than per-mode: an elemental debuff now
names its SOURCE CLASS (`ElementalDebuffSources`: `DangerPrism`/`Explosion`/`VesselContact`/
`Other`) and a ward holds a MASK, so an ability earned against the arena cannot cancel a weapon
another pilot aimed. Two invariants keep it honest — `All` is `~0` (a serialized "everything" ward
must cover a class added later) and an unclassified debuff falls in `Other` (so a new class can
never silently widen a narrow ward, and forgetting to classify fails safe); and (5) **the AI
needed a narrower hook than steering** — `AIPilot.SetExternalTargetProvider` replaces crystal
seeking outright and would disarm every AI in a mode whose weapon is fired BY a crystal, so
`AIPilot` grew **`SetDriftLookTargetProvider`**, an opt-in override for the DRIFT LOOK-DIRECTION
alone (where the nose points once the course is already locked on the objective), defaulting to the
hostile-mass cluster it already used. See `_Scripts/Controller/Arcade/BENDS.md`.
`ScarabScramble(43)` is the **Scarab-only hoop-court party game** — the accessible sibling of
Astro League and the platform's designated **beachhead mode**: fly your SKIMMER through a bright
(omni) crystal anywhere in the sphere court and the crystal BECOMES your ball, in place and at
rest (no button, no meter) — the skimmer reaches past the hull, so the ball is finished by the
time the ship arrives and the hull then strikes a real ball rather than a faked launch. **Only an
OMNI crystal forges**: an elemental crystal is the platform's element economy, so spending one on
a ball meant a Scarab could never level an element it flew past — it now falls through to the
HULL and collects normally, which is also why the Scarab's `vesselCrystalEffects` (the hull's OMNI
branch) is deliberately EMPTY, the skimmer sphere strictly containing the hull. The BLAST forge
was already omni-only by construction (`ExplosionImpactor.SweepCrystals` only picks up
`OmniCrystalImpactor`), so the two paths now agree.
Roll, bat or bank it through any of the arena's glowing hoops and your
**DOMAIN** scores; first domain to the goal target (default 10, `EndConditionOverridesSO`) wins.
Its whole rule set points at new players: ownership is **permanent**
(`AstroLeagueBall.SetOwnershipLockedServer` — a ball is its maker's colour from birth to death),
scoring is gated on **ARMING** (a crossing scores only when the ball's last touch belongs to its
owning domain, so shoving an enemy ball through a ring scores nothing — there is literally no
wrong way to touch anything), the one enemy act that converts a ball is the **juke-dash STEAL**
(`ScarabJukeController.IsJukeStrikeWindowOpen`, read by the ball's strike path — the committed
skill move converts, the casual bump never does; since the juke went **analog** a partial nudge is
explicitly NOT a steal, `SCARAB.md §3.7`), goals **stop nothing** (the scored ball
detonates and play flows on — no kickoffs, no world-stops), and the court is a **sphere** whose
centre-focusing walls recycle wild shots back toward the hoops (SCARAB.md §4.3's boundary-death
is deliberately NOT used — walls reflect) — a wall the mode does not build, see below. Multi-carom goals get the "BANK x{n}" toast — the
sphere manufactures the mode's signature screamer for novices. It lands the mode-side ball work
SCARAB.md §4.2-§4.5 left open (multi-ball via `AstroLeagueBall.Live` + `ScarabBallForge.OnForged`
adoption, per-ball attribution via a forger/last-toucher ledger, and a ball ceiling) and fixed
the forged ball's unreplicated `SetSizeScale` (`n_SizeScale`). **That ceiling is per CELL and
lives on the BALL, not on the mode** — `AstroLeagueBall.cellBallLimit` (4): when a further loose
ball enters a cell, every loose ball in it detonates regardless of domain, the arriving one
included, announced by `CellOverload_ClientRpc` so it is ONE networked event on every peer and
using the same per-ball detonation as the nucleus overload below; embedded/hidden balls do not
count, and nothing is ever culled on a clock. It replaced a per-DOMAIN cap enforced at FORGE
time (`ScarabBallForge.ForgeGate`, which survives as an unused mode-policy hook), and the reason
generalises: **a rule enforced at one PRODUCER can only ever see that producer** — a ball enters
play two ways, forged from a crystal and knocked loose out of the nucleus, so the forge-time gate
was blind to half of them by construction. Counting what is actually IN the cell notices every
route, needs no producer to remember to ask, and is the count the player can see. Its own
corollary is an ORDERING one: a forged ball is DESPAWNED by its own detonation, so the
announcement RPC must be sent BEFORE the detonation loop, and the ball's server tick must stop
touching itself after triggering one. **The WALL lives on the ball for the sibling reason** — a
ball bounces off its cell's nucleus by itself, in every cell, from whichever side it is on
(`AstroLeagueBall.ResolveNucleusBoundary`), so this mode's court is nothing but
`Cell.SetNucleusWorldRadius(courtRadius)` and it installs no per-ball boundary. It used to push a
matching sphere onto every ball it adopted, and the generalisation there is the mirror of the
producer rule above: **a rule a MODE installs can only ever hold in that mode** — every ball a
Scarab forged in freestyle or the menu flew straight through the core, because nothing outside
Scramble was there to hand it a wall. `AstroLeagueBall.SetBoundary` survives as the override for a
court whose shape a nucleus radius cannot express (Astro League's polytopes, whose nucleus is
mesh-morphed to match). Two details that generalise to any such self-resolved boundary: which side
is read from POSITION rather than from the event that put the ball there (each regime pushes
*away* from the surface, so it is self-reinforcing and cannot oscillate), and the side must be
STICKY behind a dead band of one ball radius plus one tick of travel at top speed — containment
runs BEFORE the physics step, so a ball can legitimately end a tick just past the wall it was
reflected off, and re-classifying on that would eject it rather than pull it back. The Scarab also brings a PLATFORM ability the mode
merely inherits: it passively seeds balls of its domain **embedded in the nucleus**, which anyone
can knock OUTWARD into the cytoplasm (where they live on, bouncing off the nucleus from outside)
or INWARD into the nucleus — in this mode the court, so that is a second source of scoring balls.
Bank one too many inside and the core OVERLOADS, detonating every ball in a domain-coloured blast
(own-domain prisms are drawn LIT in the blast's domain colour — `Docs/LIT.md`, a temporary shield until 2026-09 — other domains are destroyed). **A HELD BUTTON turns the Scarab's HULL from a wall into a HAND, and the DRIFT is just the drift**
(`SCARAB.md §3.7`, `§3.8`, `§3.9`). The juke is **analog** — deflection is the dash's
strength, and only a perimeter push spins, steals or blasts, so a pilot can trim their line beside a
ball without touching it; **one push is one GESTURE**, begun immediately at whatever it has reached
and upgraded to committed whenever it reaches the limit, because deciding a juke's character on the
frame it crosses the engage threshold asks about the pilot's THUMB SPEED rather than their intent (a
fast flick came out committed and an identical slower push came out a nudge that could then never
upgrade, so the plate fired for quick hands only). **A HELD BUTTON that turned the hull into a HAND was built here and CUT** (`SCARAB.md §3.8`,
kept as a retirement record) — a hull strike negated a ball's velocity and then let it through, on
`InputEvents.Button2Action`. Four rounds, four general traps worth more than the feature: **when
successive correct fixes keep buying diminishing amounts of the same complaint, the defect is one
layer below the one being fixed** (it rode a fully-held DRIFT for two playtests, and a drift is a
control the pilot is STEERING with, so a threshold on it makes "nothing may happen at full drift"
impossible to state); **a value smoothed for one consumer is not a reading of the thing it was
smoothed from** (the retired `VesselTransformer.DriftHold01` named itself like a trigger reading
and was `_frameTriggerSum`, the value the drift BLEND runs on — eased on any non-analog device,
derived there from the drift TIER FLAGS rather than the trigger at all, and written only inside an
`Update` that early-returns while the vessel is stationary, so it FREEZES rather than going stale.
The accessor was retired with the mechanic and the trap moved onto `_frameTriggerSum`'s own doc
comment, because **a public surface that must never be read is a trap generator, not a trap
record**); **a rule inherited "for free" from a shared path is
only free while the new act agrees with what that rule was protecting** (riding the ordinary strike
path handed the reversal the touch ledger and the cooldown pacing for nothing, and also the
approaching-contact gate and the depenetration, both of which were protecting *the ball never
travels through a hull* — precisely what a grab-and-fling has to do); and **a fall-through is a
DECISION, not a neutral outcome** (below a speed threshold a phased contact fell through to the
ordinary strike, so the hull BATTED the ball, which is the one thing the held button promised could
not happen). Two more survive as live rules elsewhere: **a bound `ShipActionSO` needs no
networking of its own** — `R_VesselActionHandler` round-trips every press and release through the
server, so an executor runs on EVERY peer including the server and the server reads the flag off
its own replica; and **a capability an AI acquires by accident is a design decision nobody made**
(an AI drift is BINARY, so while the modifier rode the drift every bot reversed every ball it
struck).
**The BLAST is no longer a modifier at all — the plate CLAIMS ITS OWN MIRROR IMAGE, always.**
`AOECylindricalExplosion.mirrorAboutStartPlane` reflects the swept cylinder through the plane the
plate starts on, doubling the volume about the emitter while leaving the IMPULSE untouched — so the
velocity is uniform across the whole field and the two halves therefore do opposite things: the
forward half throws mass away from the pilot and the back half **drags mass forward through them**.
An asymmetry of effect out of a symmetry of volume is why this beats a second blast pointing
backwards, which would just push everything away in two directions. It replaced a held-drift
INVERSION (a SPAWN TRANSFORM that started the plate at the far end and walked it back — elegant,
provably the same swept volume, and strictly less: it could only ever claim one half of the space
at a time). The trap it records is that **the volume is written down in FOUR places and they must
all move together** — the trigger `BoxCollider` (where vessel and BALL contacts resolve), the
plate's visual cylinder (the player's only read of the back half), the Burst
`AOECylinderSweepQueryJob` (`axial = |s|`, so one frame claims both signed slabs) and
`ExplosionImpactor.SweptCylinder.Contains` — plus the crystal broadphase sphere, which had to be
re-centred on the emitter or the blast under-reaches behind the pilot, which is the half the mirror
was added for. The Burst tiling survives by construction (reflecting a partition of `[0, L]`
through 0 partitions `[−L, 0]` the same way), and `Tools/Build/verify_scarab_cavitation_plate.py`
now **pins each of the four transcriptions to the source it was copied from** and asserts the flag's
path prefab → impactor → Burst job, because comparing four copies against each other is only
evidence about the C# if the copies are faithful — and *a serialized bool that nothing forwards is
the exact shape of a feature that is authored, documented, and does nothing.* Three more consequences
of doubling a volume, each of which was a live defect: **a MIRRORED plate cannot be blocked**
(`shouldContinue = false` says "the expanding FRONT stopped here", a statement about one front, and
a plate claiming `|axial|` evaluates mass BEHIND the pilot on frame 1 — so in Scarab Scramble the
pilot's own dais, which pays out super-shielded sun cores, silently cancelled their own weapon);
**a launch direction re-derived from `(target − origin)` has assumed a blast SHAPE** (the crystal→ball
forge radiated from a point, so a crystal astern forged a ball flying backwards while every prism
beside it flew forward — it now asks `ExplosionImpactor.BlastImpactVector`, which answers with the
radial for a sphere and the sweep axis for a plate); and **which HALF a target sits in is a
GEOMETRIC question** — one dot product against the blast's own start plane, which doubles as the
mirror test since an un-mirrored cylinder's volume is `s ∈ [0, depth]`, where re-reading the
authored flag would be a second source of truth for one fact. **And a held ability must be torn down where the
vessel goes quiet**: the release edge is an INPUT EVENT, so it never arrives for a vessel whose input
is paused or that hands over to autopilot — `R_VesselActionHandler.ReleaseHeldInputs` now releases
what it holds before it unsubscribes, which fixes the Dolphin's Echo Sight and every future hold at
the same time. That retired button in turn REPLACED a
held-drift GRAPPLE (the hull stuck to a ball and orbited it, flinging on release) which worked
exactly as specified and was rejected in playtest as not fun; the parametric orbit, its
attach/release latch, the camera anchor hold and `VesselTransformer`'s external-motion mode were all
deleted with it rather than kept "just in case", because an unreferenced subsystem is eventually
mistaken for a live feature. Three findings are kept in `SCARAB.md §4.7` as a retirement record and
generalise past the Scarab: **a value a system PUBLISHES is not the value it INTEGRATES** (an exit
velocity must replay the driver's own last write, never be rebuilt from `Course × Speed`, which has
already been through `throttleMultiplier`); **"the camera's x axis lies along the orbit axis" and
"the camera sits in the plane the hull is swinging in" are the same statement**, so aligning an axis
constrains where the camera IS, not only its roll; and **an edge and a level are not interchangeable
across a tick** — a `NetworkVariable` only carries the value it holds when the tick serialises, so a
hold that drops and returns between two ticks is coalesced away and the server never sees it, which
means *to send a transition shorter than a tick, send something that COUNTS*. **A seeded ball is an
ORDINARY LIVE BODY, not a pinned one** — `n_Embedded` is bookkeeping (containment suspended, not
counted among the cell's loose balls) and the ball itself notices it has left
(`AstroLeagueBall.TickNucleusDepartureServer`), so every force reaches it and none has to know the
ability exists. It was a kinematic pin for two passes, and both halves of that were defects: kinematic
meant no BLAST could move it (`ApplyBlastServer` writes velocity into a body that does not integrate —
so the Scarab's own dash punch, whose whole reach onto a ball it does not touch is that blast, did
nothing), and the per-tick position pin fought the hull depenetration every contact frame, which is
what made a seeded ball jitter in and out of the shell. Dislodging is ONE WAY: a ball that has been
knocked loose can never be seeded again. See SCARAB.md §4.6. The cell follows the Astro League template (nucleus = court,
`NucleusIsControlZone = false`, cleanup crew held out by `FaunaExclusionRadius` until Restless)
with its OWN ladder authored for Scarab trail volume (10-40/prism, no lining floor — never copy
the AL numbers, which ride a 30k structural floor). ⚠ **That ladder counted trail only, and every
pilot here carries the switch**: one struck switch pays a 50,773-volume dais, 4x the old
`FrenzyEnterVolume`, so the first payout crossed both gates at once and the ladder stopped carrying
information. Re-authored BUILD-PACED (Restless 164,000 / Frenzy 391,000 = the trail band + 3 and 7
spent switches), at the stated cost that **Restless no longer fires from trail alone** — and
Restless is this mode's fauna-release gate, so a switch-less match now sits in Calm far longer than
its authoring intended. General rule: **when a vessel's ability places an order of magnitude more
mass than a mode's own traffic, that mode's volume ladder stops describing the mode and starts
describing the ability** — re-derive it from the ability, and name what the old reading paid for.
See `_Scripts/Controller/Arcade/SCARABSCRAMBLE.md` § Known limitations and `SCARAB.md` §8.

`WildlifeLiberation(40)` is the **Sparrow-only hunt** — three concentric cages at 1050 / 600 / 200 divide the arena into rooms, and every tier of wildlife (a very heavy swarm of small creatures, much bigger ones, and the biggest and toughest) roams **all** of it on ONE shared band, including the open water outside the outer cage where players spawn; the first **DOMAIN** to 30 summed kills wins. **The three-tier PEN was replaced by that single roam band (0..1180)** on request — locking a tier per room read as three stacked aquariums around a boss room, so the fight converged wherever a player broke in and the apex creatures were findable at exactly one radius. Two things came out of it, both platform-wide. (1) **A uniform-in-radius draw is not a dispersal**: `CellLifeSpawnerBase.RandomPointInBand` drew `Random.Range(inner, outer)`, which gives every radial SHELL the same headcount while a shell's space grows as r² — measured, that put **63% of a population inside the innermost quarter-VOLUME** of a whole-arena band, so widening the band alone would have made the clumping WORSE. `RandomBandRadius` now takes the cube root of a uniform draw between the cubed walls (25/25/25/25). It hid for as long as it did because every authored band was a thin annulus (660..990 moves its mean radius 2.6%), and it is the same finding `§27` records for flora planting, reached independently on the fauna side. (2) **When you remove a constraint, find what it was silently buying**: the pens stopped 60u short of every wall, so a creature's cage was outside its band and therefore *not food* — one arena-wide band puts all three cages inside it, and in this nucleus-less cell herbivores eat opposing-domain mass, so the triad-painted bars are now grazeable and the cage erodes as a match runs. Accepted deliberately (the food web working, in a mode whose subject is the ecology), bounded by the same pass's 250→30 target and 15% population cut. **Never answer it by shielding the bars** — a shield reaches 1.5× `leafSize` (`§35`), which on a 26u bar laid every 34u fuses the sparse lattice into a solid tube and costs the one-hit break-in the mode is built on; raise `RoamInner` off 0 or cut `POPULATION_SCALE` instead. **The roster has been merged TWICE, and both merges were arithmetic**: `species × room` (8 configs per intensity) collapsed to `species × level` (6) when the three pens became one band, and `species × level` collapsed to **`species` (4)** when `Docs/ECOSYSTEM.md §40` retired lifeform levels. The `L1`/`L2`/`L5` assets are re-cut as `Wildlife <Species> 1..4` and populations were preserved exactly through both merges (519 seed / 1,198 cap / 4,155 body prisms after `POPULATION_SCALE`). **What the second merge COST, stated plainly: the within-species size mix is gone** — `InitialLevel` was how "a level-5 shark among level-2 ones" read, and every shark in the arena is now the same size. The swarm-to-kaiju spread the mode is named for survives BETWEEN species (a 1-prism QuadFish against a ~26-prism worm colony), and because a heart is now a species constant it carries the reward the tiers used to: a shark's heart is 4.60 world scale against a worm segment's 2.28, so **a shark kill pays double a worm segment's**. If the within-species mix is wanted back, the honest lever is a per-element `FaunaVariantTuning.BaseBodyScale` — an element that is genuinely a bigger animal — never a level axis. Re-targeting it 250 → 30 also produced the **third** outing of the comeback trap Dog Fight and Bends already record — `bonusLevels = deficit × rate`, so the rate is a function of the TARGET: the card had inherited Rampage's `0.01` against a target 8× smaller (already only 0.625 of a level at a quarter-of-target deficit) and 30 would have made it 0.075. Rate is now `0.35` (2.6 levels, matching Dog Fight's curve — the nearest sibling by structure), and the generator FAILS the build if a quarter-of-target deficit ever stops buying a whole level. See `_Scripts/Controller/Arcade/WILDLIFE_LIBERATION.md`. It is an ordinary domain race and that is deliberate: a per-PLAYER (free-for-all) winner shipped here briefly and was **reverted**, because the mode seats up to four players while the platform has only three playable domains, so a full lobby always has teammates and a per-individual winner bypasses every domain surface (winner banner, HUD panels, scoreboard ordering, `ResolvePlacementOrder`). Do not re-derive it. Its metric, `ScoringMetric.LifeformsKilled`, is the first whose source is the ECOLOGY rather than prisms or crystals — and the first that needs an RPC, because fauna are client-local so a client's kill is invisible to the server (`Player.ReportFaunaKill_ServerRpc`; the round-trip stays correct once fauna network sync lands). Shipping it made **every creature in the game killable by shooting its body prisms** (previously only the worm colony was — see `Docs/ECOSYSTEM.md §24`) and generalized the cell's single fauna pen into a per-species BAND. **The Sparrow's missile warhead now kills creatures in a 95-unit sphere and lands straight on this metric**, and two of its rules were written for this mode specifically: **wildlife is quarry whatever colour it wears** (the creature kill deliberately does NOT read the blast's friendly-fire flag — fauna spawn in ONE colour, so borrowing it let the Sparrow's CHARGE-5 *prism* upgrade silently switch off wildlife kills for a pilot sharing the swarm's colour, in the only mode scored on killing wildlife, and the comeback system hands that upgrade to whoever is LOSING), while the proximity fuze deliberately does NOT arm on own-domain wildlife (a rocket that armed on friendly creatures could not cross a swarm) — the fuze picks TARGETS, the blast reaches everything. It also forced a platform fix: **`IsEmbedded` is not `IsAlive`**, so jousting a corpse's still-embedded heart re-ran the sealed death for a SECOND kill credit and freed the heart mid-wither; `Fauna.Predated` now declines an already-dead creature, the guard `LifeForm.Jousted` has always had and that `Fauna` — a SIBLING of `LifeForm`, not a subclass — never inherited. Open: `Predated` is authority-gated, and the four `Wildlife Shark` assets are the game's only `NetworkSynced` fauna, so a CLIENT's warhead cannot kill a shark (body-prism fire still can, because that path has a ServerRpc and the joust does not). See `_Scripts/Controller/Arcade/WILDLIFE_LIBERATION.md`.
`Salvo(44)` is the **Sparrow-only demolition race** — Dog Fight's inverse in the SAME
Boneyard (the cell configs, spawn profiles, scavengers and `SpawnableBoneyard` variants are
reused verbatim, not forked — the cell is per-arena, not per-mode): there the wreckage is
cover and shooting it scores nothing; here **tearing it apart IS the score**. First DOMAIN to
destroy the hostile-prism target (default **700**, `EndConditionOverridesSO.salvoPrismTarget`)
wins on `ScoringMetric.PrismsDestroyed` — the Rampage/Cleave metric and machinery, zero new
scoring code. The mode is built on the Sparrow's SHIPPED missile economy: guns are free chip
damage, a skyburst costs **half the missile tank** (`SkyBurstGunAction.ammoCost 0.5` against
max 1), and since 2026-09 the tank refills by **destroying hostile prisms** — which in this mode
IS the objective (0.01 per prism, 50 per rocket; `VesselRearmOnPrismDestruction`). The omni
crystal's old set-it-full effect is retired and it now grants a debuff WARD instead, so the arena's
crystal ABUNDANCE (`CrystalCountMode.PlayerCountPlusExtra` + 5, Scurry's shape, Rampage's inversion)
now buys the wingman reload and the ward rather than being the only way to rearm — the mode's
crystal-run rhythm is correspondingly weaker and wants a playtest (`SALVO.md`). **The reason to play it
together is the WINGMAN RELOAD**: an omni crystal collected by ANY pilot reloads the missile
bays of every pilot on the collector's domain (`SalvoController.HandleOmniCrystalCollected` —
server-side off the `EventOnCrystalCollected` SOAP channel, since omni collection resolves
server-only — → `RefuelDomainMissiles_ClientRpc`, an idempotent set-to-full on every peer;
legitimate because ammo is LOCAL state each machine simulates for its own vessel). One pilot
flies the crystal line, a wingman camps the densest wreckage and fires every reload the runner
buys. AI is the platform default (crystal seeking IS its ammo line — Salvo is deliberately NOT
in `ServerPlayerVesselInitializerWithAI`'s seek-players set, and the controller installs no
external target provider, per the Rampage rule); the objective arrow reuses
`RampageObjectiveProvider` (nearest managed omni crystal — here the reload, there the blast
trigger). See `_Scripts/Controller/Arcade/SALVO.md`.

`Switchback(45)` is the **Dolphin-only gate race** — a course of **randomly placed and randomly
ORIENTED** switch rings scattered through the cell, flown in ORDER by every pilot, and the first
DOMAIN whose **LEAD RUNNER** threads the last gate wins. It is the third Dolphin mode and the one
that asks the vessel for nothing but flying: no target, no weapon of its own — skim to bank
energy, drift to carve a corner its 110 deg/s turn rate could not otherwise make, boost the
straight that follows. Four things it establishes are platform-level. (1) **ORDERED gates make one
replicated int carry a whole race**: a pilot may only thread their NEXT gate, so
`IRoundStats.SwitchesThreaded` (`ScoringMetric.SwitchesThreaded = 9`) is simultaneously the score,
the progress bar, the index of the ring to test this frame, and the token the server validates a
report against — `gateIndex != stats.SwitchesThreaded` rejects a client claiming the last gate from
the starting line AND a duplicate report of one already paid, with no per-gate state and no
bitmask. (2) **A domain's score is not always a SUM.** Every pilot flies the same course, so
summing teammates would give a two-pilot domain twice the course and the win over a one-pilot
domain that flew further. `ScoringRuleSO.DomainValue` is the new seam — default
`ScoringMetrics.SumByDomain` (byte-for-byte the old behaviour everywhere), overridden here to the
new `ScoringMetrics.BestByDomain` — and it is ONE virtual rather than four overrides because a
domain's score is read in FIVE places that must never disagree (`Remaining`, `ResolveWinner`,
`ResolvePlacementOrder`, `DomainDelta`, and `MultiplayerDomainGamesController`'s HUD domain boxes);
a mode that overrode only its end condition would win on the lead runner while the score row above
it showed the team's sum. The design consequence is deliberate — **a teammate cannot add to your
score**, so team play is interference, and the Dolphin's blast cone (which debuffs a rival pilot in
every mode since The Bends wired it) is already on the course because the vessel passively seeds
its own crystals in EVERY scene. (3) **"Randomly oriented" is only playable if it is CONSTRAINED
BY CONSTRUCTION.** A gate faces the flow BISECTOR of its corner and the jitter that makes it random
is spent from what is LEFT of a presentation cap after the corner has taken its half
(`presentation <= halfTurn + jitter <= cap`), so no gate is ever edge-on; and the turn cap holds
because the walk only advances its heading when a gate is PLACED and BACKTRACKS when a shell wall
leaves no legal escape — letting the heading rotate between failed attempts is the tempting
shortcut and composes two 55 deg rotations into a 110 deg hairpin between two placed gates.
`SwitchbackCourseTests` sweeps 400 seeds x 4 intensities and asserts both caps, shell containment,
mouth separation, and that **every corner clears the Dolphin's turning circle at BOOST** (Dubins
`leg > 2R sin(turn)`, R = 180.7u). (4) **The course TRAVELS, the seed does not** — it is
deterministic on purpose (a specified xorshift32, no `System.Random`, no `UnityEngine.Random`), but
the server broadcasts the geometry rather than the seed, because a shared seed would rest on
`Mathf.Sin`/`Acos` agreeing to the last bit across Mono and IL2CPP and one flipped branch inside
the walk yields a COMPLETELY different course. Two more things it records: the start is **provably
fair** because pilots spawn on an EQUATORIAL ring and gate 1 sits on that ring's POLE
(equidistant from all of them — under a Symmetric/tetrahedral formation no such point exists);
and a gate is a **NEUTRAL switch and a marker, not mass** — one renderer, ZERO colliders, painted
`Domains.Blue`, so the reserved domain colours stay with the switches that grant a domain, and
"which ring is mine next" is answered by the per-viewer objective arrow rather than by repainting
shared world geometry. Intensity is the **COURSE** (mouths 72->42, corners 45->60 deg, jitter
30->60 deg), never the arena: gate COUNT is constant because it is the end-game target, authored
once in `EndConditionOverridesSO.switchbackGateTarget` and read BOTH by the monitor (the target)
and the controller (how many rings to lay), so the course and the number counting it cannot drift.
See `_Scripts/Controller/Arcade/SWITCHBACK.md`.
`Hijack(46)` is the **Urchin-only heist race**, and the first mode on the platform whose score
is OWNERSHIP rather than destruction: **nothing here is ever destroyed — mass only changes
hands**, which is what lets a whole competitive mode run inside the conserved-mass law with no
food web, no respawn and no despawn. First DOMAIN to STEAL 750 prisms wins
(`ScoringMetric.PrismsStolen = 10` → `IRoundStats.PrismStolen`, a stat
`StatsManager.PrismStolen` and `Player.ReportPrismStolen_ServerRpc` have been accumulating in
every mode since long before a mode read it — so the metric needed **zero** new gameplay
plumbing). It is deliberately a COUNT rather than `VolumeStolen`: a friendly ride GROWS a prism,
so a volume metric would quietly pay a re-stealer more than the pilot who took it first.

The arena, the **Switchyard** (`SpawnableSwitchyard`), exists to make each of the Urchin's three
shipped verbs pay, and adds no vessel mechanics at all. Three great-circle RAILS of radius 900
ring a hollow core, 8 stations each, meeting at 6 big spiny BURRS on the axis crossings plus 12
small ones mid-arc. Each rail is 40 prisms of the Track Projector's own `(3,3,6)` scale on its
OWN open `Trail` (a shared trail has one pair of ends, so 23 of the 24 could never launch), and
each burr is one `Trail { Dimension = Volume }`. **THE LAUNCH IS AIMED BY GEOMETRY, NOT BY A
BONUS**: a circle's tangent at `g` short of a station passes through that station's radial at
`R/cos g`, `R·tan g` further on, so every burr centre is placed at exactly `900/cos 12.5° =
921.9u` and a pilot who grinds a rail to its end and does not steer flies straight into the
cluster 199.5u ahead. Three things generalise from building it. (1) **A launch contract is a
claim about TANGENTS, which looks right in a diagram and is wrong in the build** — the prism
spacing is DERIVED so the 40 prisms span the arc endpoint-to-endpoint (8.0554u); authoring a
round 8.0 centres 312u of prisms inside a 314u arc, insets the terminal prism ~1u and tilts the
launch 0.32° off the burr, which `Tools/Build/hijack_budget.py`'s `prove_launch_geometry()`
caught before a line of C# was written. (2) **Resolving identity by ROUNDING a float is a
tolerance with a cliff in the middle of it** — a quantize-to-whole-units key that matched a rail
to its burr was written first and rejected when the model measured a burr coordinate sitting
0.049 of a unit from a `.5` boundary; proximity (burr centres are 705u apart, a rail's target
lands within 1e-13u of its own) has no boundary to land on. (3) **A generator written CLOSED FORM
can be MIRRORED rather than estimated** — there is no `System.Random` draw anywhere in
`BuildEnvironment`, so the Python model reproduces it exactly and `author_hijack_assets.py`
imports it, which is what makes the cell's `PhaseThresholds` unable to drift from the arena that
has to satisfy them.

Painting is the full triad, **exactly equal per domain and no Blue**: each rail runs in three
domain THIRDS rotated by `(j+k)`, so every rail offers every domain a fast third (fair from any
spawn slot) and the 15x speed cliff at each boundary IS the tutorial that stealing is the score;
each big burr wears the THIRD domain of the two rings that cross there, so the biggest prizes are
hostile to both approaches by construction. A two-domain lobby finds the third colour's mass
hostile to both sides and, by the 3-fold symmetry, equidistant — symmetric unclaimed loot, which
is why it is a REAL domain rather than `Domains.Blue`. **Intensity scales burr mass and nothing
else** (2,772 / 3,978 / 7,218 / 9,930 prisms): the rail network, the radii, the launch gaps and
the spawn ring never move, so a bigger yard is a longer, more contested match at a fixed target
rather than a scarcer one. Every prism is `PrismKind.Plain` — **zero always-on mesh colliders are
authored**; the only ones that can appear are a MASS-5 pilot's own ride armour.

**There is deliberately NO food web, and the reason is the comeback**: in a nucleus-less cell
herbivores eat OPPOSING-domain mass and the leader's colour is by definition the most abundant,
so a swarm would preferentially eat whatever the TRAILING team had just stolen — an anti-comeback
current in a mode whose whole economy is contested ownership. **The launch also pays nothing
bespoke, deliberately**: no per-launch bonus, no airtime multiplier. It pays through geometry
(burrs on rail-end tangents), physics (a spike's velocity composes with the vessel's, so a volley
thrown at grind speed reaches ~3.5x further) and economy (only riding banks ammo) — scoring the
RECORD of a manoeuvre rather than its effect is the scripted-outcome cheat, and Dog Fight and
Bends weight distinct scoring EVENTS, where the analogue here is the prism itself. One authored
field is load-bearing: **`ram: 0 → 1` on `Urchin.prefab`'s AIPilot** (the Rhino's shipped value).
`AIPilot` writes `XDiff = (LookingAtCrystal && ram) ? 1 : throttle` and
`GunVesselTransformer.ReadThrottle` is SIGNED around a 0.5 rest, so the authored
`defaultThrottle 0.6` reads as +0.2 signed throttle = **60 u/s on a friendly rail, below the
vessel's own 65 u/s cruise** — an AI Urchin would grind slower than it flies and carry nothing off
a launch. See `_Scripts/Controller/Arcade/HIJACK.md`.

`Breakwater(50)` is the **Sparrow-only station race** — a polar start gate plus a closed
fourteen-station **circuit** hung on a
randomly generated walk through the cell, every pilot flying the same course in ORDER, and the first
**DOMAIN** whose **LEAD RUNNER** finishes the last of its two laps wins. Each station is a shallow **dish**
of plates that flares back toward you (167 down to 75 of them, the landmark and the ammunition), its
throat welded shut by a triple-rake weave of **DANGER** bars around an 18-unit **EYE** ringed by a
twelve-block keystone collar. You arrive with two rockets in the bay and make one choice: **fire**
(the skyburst's spherical blast vaporises a door), **saw** (turret stance, stop dead, grind the weave
open) or **thread** (fly the eye at 1.46x hull clearance with danger bars a hull-width away). What
actually SCORES is none of those — it is the `RaceGateRing` at the port radius, so the three verbs
are three PRICES for one crossing rather than three scoring events, which is why no rule is needed
to say a sawn station counts as much as a shot one. **The walls you shoot ARE the ammunition** (50
hostile prisms buy a rocket, `ammoPerPrism 0.01` against `SkyBurstGunAction.ammoCost 0.5`), so
opening one door roughly funds the next and the economy closes with no pickup on the course.
**ONE new enum value lands in the whole branch**: a station is a switch threaded in order — the same
fact `ScoringMetric.SwitchesThreaded(9)` already records — so the metric, `SwitchThreadScoring.Credit`,
`Player.ReportSwitchThreaded_ServerRpc`, `GameDataSO.SwitchTargetCount`, the
comeback (which reads the rule's `DomainValue`), the `BestByDomain` lead-runner fold, the goal-stack row and the launch-panel
icon are REUSED verbatim, both metric-9 rows already exist in `ObjectiveIconSet`/`ModeControlsLibrary`
so the objective row costs no asset edit, and `BreakwaterScoringRule` is a SECOND ASSET on the
existing `SwitchbackScoringRuleSO`. **"THREAD SWITCHES 3/14" is literally correct rather than a
compromise**, because the goal row is keyed on the METRIC and never on the mode. `SwitchbackGateRing`
was **PROMOTED** to `Racing/RaceGateRing` (`git mv` of the `.cs` AND its `.meta`, guid intact) with a
PRIMITIVE `Build(index, position, axis, radius, theme, bloom)` signature — a shared gate STRUCT was
the alternative and loses, since a Switchback gate carries one radius for a whole course while a
Breakwater station carries a per-station port radius, so the struct would exist only to be passed.
Five findings are worth more than the mode. **(1) The design rested on a claim about the skyburst
that is FALSE.** `AOERadialBlocks.CreateRay` computes `angleStep` and **never uses it**, and
`rayIndex` never reaches the direction — so `AOEConicSkyBurst.prefab` lays **72 SHIELDED prisms on a
20-degree forward CONE SHELL at random azimuth**, in the shooter's own domain, over 12 frames: a
**cairn**, not a 12-ray starburst, and **there is no bore**. The door is cut by a DIFFERENT prefab
the same detonation spawns — `AOEExplosion.prefab`, plain and SPHERICAL, `Lerp(100,170,Clamp01(Charge))`
as a DIAMETER = **radius 50 u at resting Charge, 85 at Charge 10, still 85 at 15**. That correction is
what makes the intensity ladder honest in both directions: the tightest port (42) sits UNDER the
resting radius so **a pilot with no upgrade at all clears a whole plug with one rocket** — closing
Wildlife Liberation's recorded trap, a core promise gated behind a level the comeback hands the
LOSER — while the widest (72) sits OVER it so WHERE you cut is a real choice. **(2) The branch's
stated go/no-go gate did not exist.** The spec called flipping `shielded: 1 -> 0` on that prefab
mandatory, on the grounds that the cairn would mint ~4,032 always-on convex `MeshCollider`s;
`shieldMeshCollider.enabled = true` appears **NOWHERE** in the codebase (four sites, all `= false`),
exactly as this document already states — *a shield swaps the MESH and the mass, never the collider*
— so **no shipped prefab was touched** and no other mode was perturbed. *A go/no-go gate is worth
verifying before you pay for it.* The real reason never to shield a plug is GEOMETRY and is not
contingent: a shield reaches 1.5x leafSize, which on a 12-unit rake pitch fuses the weave solid and
deletes both the saw and the thread. **(3) The ladder is ONE DIAL, anchored on
intensity 1** — a leg of length `L` turning `θ` covers `L·cos θ` ALONG the track and `L·sin θ`
ACROSS it, so raising the TURN CAP alone decreases the first and increases the second, which is
exactly the shape that forces a rolled, strafing entry instead of a flat sweep. I1 is pinned and
the cap climbs `45/55/65/75` (`legs 300-460/300-433/300-407/300-380`), collapsing along-track 3.1x
(269 -> 88) while opening across-track 1.2x (269 -> 328). **The MINIMUM leg is pinned at 300 on
every level, and that is what pays for it**: `sin` caps at 1, so `2R = 260.2` is the hard ceiling of
the Dubins requirement `leg > 2R·sin(turn)` at ANY turn angle — a 300 minimum clears it outright, so
flyability stops being measured and becomes GUARANTEED, and the cap can rise as far as the
presentation cap allows with nothing to re-check. Its first cut failed 21% of seeds by running the
LONGEST legs with the TIGHTEST cap (the gentlest corners at the easiest level), inside a
660-unit-thick shell where a long leg with little turn available walks into the wall and cannot come
back: **long legs and tight corners are the same constraint pulling opposite ways**, which is why
the shipped ladder never trades them off — it moves one dial and holds the other still.
**(3a) The course is a START GATE plus a closed CIRCUIT — fifteen stations, 29 crossings.** It
replaced an out-and-back that re-flew the same stations REVERSED, which play-tested exactly as it
reads: being sent back through the rings you came. **The start gate is what makes a circuit fair,
and it is not decoration.** Fairness here is *pilots spawn on an `EquatorialRing` and the first gate
sits on that ring's pole, so every pad is equidistant* — make that first gate the first gate of a
closed LOOP and the approach is AXIAL while a closed loop's tangent at an axial point is
PERPENDICULAR: measured over 400 seeds × 4 intensities, presentation at that gate ran **12.8-90.0°
with up to 73.5° of spread ACROSS PADS**, so one pilot gets a 14° face-on approach and another 90°
edge-on to the same ring. That is STRUCTURAL, not tuning — inside the 420..1080 shell no circle can
cross the polar axis at radius ≥ 420 with a near-axial tangent, because `c+R ≤ 1080`,
`R²−c² ≥ 420²` and `c/R ≥ 0.866` are jointly unsatisfiable. So the start gate sits OFF the circuit,
on the axis, with its axis ALONG the pole, which makes every pad equidistant **and** face-on
(measured spread **0.0000 on both** — strictly fairer than the old rule, which equalised distance
only). **The circuit is CONSTRUCTED, never searched**, because the walk could not be steered home
(a closing walk failed **55-76% of seeds**): a ZIGZAG RING hits an exact turn angle in closed form
(`cos(turn) = (|s|²cos φ − 4z²)/(|s|² + 4z²)` for `φ = 2π/N`, N even so the zigzag closes), which
solved for a target chord and turn IS the intensity dial —
`|s| = chord·√((1+cos T)/(1+cos φ))` along track, `2z = √(chord²−|s|²)` across it. Three further
rules come out of it: **wander must be LOW-FREQUENCY** (harmonics k=1,2 move neighbours TOGETHER, so
the outline changes a lot while adjacent spacing barely moves — per-station jitter had to be cut to
~20% of nominal to fit the chord band, which made every course look alike); **the shrink terminates
by construction** (at amplitude 0 the loop is a regular zigzag ring, which is legal, so there is no
failure rate to report — 0 in 1,600 courses, where rejection sampling had 55-76%); and **the
rotational DOF are SPENT, not randomised** (two put the entry station where a polar start gate is
exactly one chord away, the third spins the loop so its tangent there already points down the entry
leg — randomising them is why the entry leg was almost never in the chord band). Two discrete
choices are searched and the score is **quantised to 0.1°** so float noise cannot flip a branch and
make the model and the shipped C# disagree about a whole course. The one cost, stated plainly: the
MERGE from the start gate onto the circuit is a hard corner (**66.9° worst, ~61° mean**), exempt from
the turn cap — which describes the circuit — and bounded only by Dubins, which the 300-unit minimum
leg guarantees at ANY angle (`2R·sin(66.9°) = 239.4 < 300`); it happens once per race and reads as a
racing start. **One replicated int still carries the whole race** — `RingForCrossing` folds the
crossing count into a ring (crossing 0 is the start gate, everything after walks the circuit forward
and wraps, so the fold is a plain modulo where it used to be a zigzag), so `SwitchesThreaded` is
still score, progress bar, validation token AND ring index with no per-lap state; but **the fold
picks which ring to TEST while the token that TRAVELS is the CROSSING**, because the server validates
`gateIndex != stats.SwitchesThreaded` and reporting the folded ring would have every lap-2 report
rejected as a duplicate. `EndConditionOverridesSO` splits the one number that used to do two jobs:
`breakwaterStationTarget` (stations LAID = arena mass) and `breakwaterLaps`, with the race target
DERIVED so it can never ask for a crossing the course cannot offer. It also retired
`FirstStationDistance`: the start gate's distance is SOLVED (wherever the axis is one chord from the
entry station), so the authored value was read by nothing — *a config that cannot affect anything is
worse than absent*. **(3b) A named constant is an INPUT to
the arithmetic, not a claim about what the geometry EMITS** — the plug's rakes were clipped on
their CENTRELINE, and a bar is 3 units wide, so a line standing at exactly `EyeRadius` was a clean
`d < eye` false and left six bar bodies straddling 16.5..19.5: the collar advertised an 18-unit
mouth the weave did not have (1.34x hull, not the documented 1.46x). The tests now measure the
EMITTED boxes, not the constant that names them, against a negative control that restores the old
clip. *An exact tangency inside a clipping test is a warning, not a reassurance — it makes the
wrong branch deterministic instead of intermittent, which is worse, because it looks decided.*
**(3c) A spawn ring inside a generated arena's own shell is a placement constraint nothing else
will state** — pads sit at 480 inside the 420..1080 walk, and 7 of 14,400 measured pad-cases put a
pilot INSIDE a station's Danger weave at match start; the walk now rejects any station whose
bounding sphere plus four hull radii reaches a pad (measured: zero cost to the generation rate),
and the pad set is the UNION over 2/3/4 seats rather than the live roster, so a seat added between
generation and spawn cannot invalidate geometry every peer already holds. **(4) `MinSeparation`
must stay BELOW the minimum leg** and is therefore DERIVED, `min(0.9*MinStep, 4*PortRadius)`:
`TooClose` tests a candidate against every placed station INCLUDING its predecessor, so the spec's
authored 420 against a 260 minimum leg would have rejected most of the step range and starved the
walk. **(5) The C# was proved against the model, not merely compiled** — `Tools/Build/breakwater_arena.py`
mirrors the xorshift32 walk and the station builder's `Hash01` bit for bit, giving **160 courses
position-identical (worst delta 0.002 u)** and every station's prism count and per-prism volume
matching to **0.0000%**, which only became true once the model was taught to sum the dish's real
jitter draws rather than price it at nominal. Everything else is the platform's: the course TRAVELS
as geometry rather than as a seed (15 stations = 360 bytes, the whole wire cost of a 4,575-prism
arena, since every station is closed form from its pose); detection is owner-detects/server-records
on `IsNetworkOwner`; the arena is `Domains.Blue` everywhere so every pilot's rounds pay ammo on every
door and the Sparrow's CHARGE-5 own-domain sparing can never make a station unopenable; there is **no
food web**, because in a nucleus-less cell herbivores eat opposing-domain mass and would graze the
doors open on their own; and the AI opens its own doors with **NO new AI code** (its rounds leave
along the NOSE and the two-waypoint provider holds the nose on the plug for the whole run-in, which
rests on `Sparrow.prefab`'s authored `drift: 0`) while an AI that fires nothing still flies the axis,
which IS the eye — **the pre-open eye is the designed FLOOR on AI competence**. A failed walk is
**RESEEDED (3x) before it is SHORTENED**, because halving the count answers a one-in-a-thousand roll
by shipping that one match a race half the length of every other. Every asset is authored by
`Tools/Build/author_breakwater_assets.py`, which is the **only mode generator in the repo whose
`--check` passes** (measured: switchback / drumfire / salvo / hijack all exit 1 today, drumfire on
exactly the spent-one-shot `assert` that makes a `--check` vacuous) — it imports the arena model for
the `PhaseThresholds` rather than retyping them, and its scene clone STANDS DOWN once the scene is
committed rather than asserting, so a re-run cannot rewrite a `GlobalObjectIdHash`. **Nothing has
been run in the editor.** See `_Scripts/Controller/Arcade/BREAKWATER.md`.
`Bloomrush(52)` is the **Manta-only bomb-tag party game** — the vessel's accessibility thesis
as a mode: nobody has to learn a button. Skim the reef to arm bombs, graze wildlife and rival
Mantas to plant them (silently — no indication to the target, ONE bomb per target so a tag is
also DENIAL), then reach a crystal before the fuses burn down and **Kabloom** the whole board
at once. It is the family's first **timed highest-score** mode: a **120-second round** (the
scene's `NetworkTimeBasedTurnMonitor` is the only end condition), scored on the new
`ScoringMetric.VolumeDestroyed` (hostile prism VOLUME, domain-summed — the Manta's kit is about
volume) with **fuses beaten** as the tiebreaker (`IRoundStats.FusesBeaten`, the fourth instance
of the owner-detects → server-records stat round-trip). "Beat the fuse" needs no scoring
special case — a crystal-cashed bloom is authored BIGGER than a fuse fizzle, so timing out
pays a fraction by construction. The arena is **Rampage's cactus forest referenced verbatim**
(the Bends precedent — the scene is a donor clone of MinigameBends), with the omni-crystal
ladder re-cut toward ABUNDANCE (crystals are the detonator: `3p+2 / 2p+1 / p / p−1` by
intensity) and the mode's own intensity dial being the **FUSE** — 30/25/20/20 s, pushed
through the static `MantaBombRules.FuseSecondsOverride` on every peer at countdown end (after
the config-sync gate) and cleared on despawn. Bombs are LOCAL objects simulated by the
planter's owner machine; peers see blooms via `MantaBombNetworkRelay` (originator-skip
ClientRpc). AI needs nothing: crystal seeking IS the cash-out line, and no external target
provider is installed (the Rampage rule). See `_Scripts/Controller/Arcade/BLOOMRUSH.md` and
`_Scripts/Controller/Vessel/R_VesselActions/MANTA_STING_KABLOOM.md`.

`Redline(53)` is the **Manta-only circuit race** — the third mode on the gate-race platform's
lapped shape, and the second cut against a VESSEL'S OWN turning geometry. The Manta's two held
inputs share two triggers: **Soar is their OVERLAP** (`min(LT, RT)`, ×4 cruise to 720 u/s) and
**Yastri their DIFFERENCE** (60°/s of trigger yaw), so easing one trigger is trading boost for
yaw on one linear scale and every corner asks *how much Soar is it worth?* — answered on the curve
`RedlineCourse.CornerRadiusAtBoost` (237 u flat out, 172 at half, 82 with one trigger released;
speed lags by the 1.5/s `LERP_AMOUNT`, so a lift costs ~2 s to win back). **It has no generator
of its own**: `HeadlongCircuit` is shared, `RedlineCourse` supplies the Manta's settings, and the
solver's one change is an ABSOLUTE `CornerFloorRadius` so a second vessel's floor is stated in its
own units (Headlong leaves it 0 and is bit-for-bit unchanged). Intensity is the corner mix —
0 / 1 / 2 corners costing Soar at levels 1–3 and at level 4 two hairpins (31%, 56% of top speed)
plus a **knife-edge** third on the full-boost radius, measured over 400 seeds and asserted by
`RedlineCourseTests`. Two findings generalise: **the solver's REACH, not its profile, is the
ladder's dial** — raising level 2's asked-for corner 118°→130° changed nothing while raising
`AngularSpread` 2.0→3.0 produced the corner, and a SMALLER base circle made every corner WIDER
(shorter legs at the same turn are bigger circles); and **eight gates beat six for a fast hull**
— longer legs at the same turn lost even level 4 its third corner, so the Manta's runway is the
legs the solver leaves long, not a lower gate count. It also fixed a fleet-wide AI gap: `AIPilot`
writes only stick and throttle, so an AI Manta could never Soar; `MantaAnalogTurnBoostExecutor`
now carries an **autopilot drive** (both triggers at a boost intent that is how straight the stick
is — full under `aiBoostStickBand` 0.35, fading to nothing at full deflection), gated on
`AIPilot.AutoPilotEnabled` rather than on the player being an AI so the lava-lamp Manta flies the
same kit. The scene's `maxPlausibleSpeed` is 1400 (the inherited 400 rejects a Time-10 Manta's
30 fps step within a unit) and its AI approach numbers are sized to the 237 u circle. See
`_Scripts/Controller/Arcade/REDLINE.md`.

`WreckingBall(54)` (display name "Wrecking Ball") is the **Scarab-only demolition race** —
Rampage's analog for the hull whose weapons are a BALL and a PLATE. A sphere court (Scramble's:
the nucleus resized, `NucleusIsControlZone = false`) is grown full of Rampage's five breakable
flora, every bright crystal a Scarab flies through becomes its ball, and every hostile prism that
ball plows through or the juke dash's cavitation plate shreds is credited to the pilot; first
DOMAIN to 1,500 wins on `ScoringMetric.PrismsDestroyed`, Rampage's metric and machinery. **Its
platform change is that a forged ball SCORES FOR ITS PILOT**: `AstroLeagueBall` named itself
"Astro League" as the attacker of every prism it ate — a name on no roster — so nothing a ball
ever ate scored for anyone. `n_PilotName` (replicated: the prism scan runs on every peer and
environment mass is credited by the machine that simulates the ATTACKER) is stamped with the
forger at the forge and re-stamped by every vessel strike, so a ball a rival bats away scores for
them from then on — the Scarab way. **The forest is INSIDE the court, and that is the arena**: a
ball outside its nucleus bleeds speed ×6 (SCARAB.md §4.1c), so Rampage's cell — forest at
0.76–0.94 of the membrane, far outside any nucleus — would kill every ball that reached the
trees. The four `WreckingBall Cell Config`s fork Scramble's cell for exactly two reasons: the
five species are Rampage's byte-for-byte except their planting band, re-cut into 0.28–0.92 of the
720u court with their layering preserved (the clamp that would push them outside the nucleus
lifts with `NucleusIsControlZone = false`, `Docs/ECOSYSTEM.md §42`); and the volume ladder is
Rampage's play-tested intensity-4 ladder scaled by each cell's forest ratio (an ESTIMATE pending
the in-editor baseline). Intensity is DENSITY and SUPPLY around a fixed 720u court — plants
1.0×/0.85×/0.72×/0.6× Rampage's 59 (flatter than Rampage's ladder because the court band holds a
fifth of Rampage's shell volume), crystals `2p / p+1 / p / p−1`, wildlife 1×–4×. The AI is
Scramble's roller re-aimed at `Cell.GetExplosionTarget` (bowl the ball into the densest hostile
stand) plus the second platform change, **an AI Scarab can dash**:
`ScarabJukeController.TryAutopilotDash` runs a committed dash through the ordinary fire path on
the simulating machine — the juke was inert under autopilot, so an all-AI domain could not play
(the Tollway rule). Authored by `Tools/Build/author_wrecking_ball_assets.py` on the new shared
`Tools/Build/arcade_mode_lib.py`; the `/arcadegame` skill is the recipe. See
`_Scripts/Controller/Arcade/WRECKING_BALL.md`.

`Undertow(55)` is the **Scarab-only cavitation duel** — The Bends for the hull whose blast is a
sideways PLATE rather than a cone, fought in **Wildlife Liberation's caged arena, referenced and
read-only** (the cell is per-arena, not per-mode). The juke dash's plate is the only weapon and
two things it does score: an opposing pilot caught in it takes the plate's all-element debuff
(one BEND, 3 points) and a creature whose heart it reaches dies the Squirrel's joust (one KILL,
1 point); first DOMAIN to 12 wins. **Its platform change is that the plate now SCORES and KILLS
THROUGH A HEART, platform-wide**: the Scarab's cavitation container had carried the debuff since
the hull shipped and no scoring report and no lifeform-crystal effect, so it gains a Debuff-class
`VesselCombatHitByExplosionEffectSO` (`requireDebuffableVictim` — the score follows the drain;
`requireOwningMachine` — the plate exists on one machine today, and this is what keeps a future
replay from double-crediting) and the Sparrow warhead's
`ExplosionWitherLifeformByCrystalEffectSO` (fauna only, own domain NOT spared, for the reason
Wildlife Liberation records). Counted everywhere, paid only here. **Two stats, one fold, no new
metric**: `UndertowScoringRuleSO` keeps `metric = CombatPoints` and folds kills in through
`ScoringRuleSO.DomainValue` — the Switchback seam, so the five domain-score readers agree — at the
stated cost that the per-player HUD card (`LiveMetric`, not virtual) sees bends alone (the
comeback reads `DomainValue` since 2026-09, so it sees both); the scoreboard's secondary line carries the
breakdown and both raw counts ride the final-score snapshot. A nucleus-less arena forced the two
scene edits the Bends donor lacked: Wildlife Liberation's own spawn ring (1150, equatorial — the
donor's "500 outside the nucleus" collapses to 500u inside the middle cage with no nucleus) and
`noNucleusSpawnRadius = 480` (the Dog Fight rule). The AI hunts the nearest opposing pilot
(humans ×3) and dashes through `TryAutopilotDash` when a rival is inside the plate's reach, else
when the densest hostile mass is. Authored by `Tools/Build/author_undertow_assets.py`. See
`_Scripts/Controller/Arcade/UNDERTOW.md`.

`Regatta(56)` is the **ARENA race** — every playable hull (Manta, Dolphin, Rhino, Urchin, Squirrel,
Serpent, Sparrow, Scarab) on the same closed circuit of eight switch rings, three laps, first
DOMAIN whose LEAD RUNNER threads the last gate wins, on the gate-race platform with
`ScoringMetric.SwitchesThreaded` reused. It is an **Arena** card (`ArenaGames` + the
`ArenaLaunchPanel` vessel carousel; `arcade_mode_lib.register_arena_card`) and the first built for
the whole fleet, so its arena is what makes a mixed grid a race: **three super-shielded rails, one
per playable domain, braided along the racing line** (`SpawnableRegattaRails`, one prefab per
intensity; `RegattaCourse` runs a closed Hermite through the rings with each ring's axis as its
tangent, so the spine crosses every ring plane at the centre and a rider threads a gate by riding;
lanes 22 u off the spine on a rotation-minimising frame plus exactly ONE twist per lap, which is
what keeps the three lanes within 1.9% of one length — measured over 240 seeds by
`Tools/Build/regatta_course_harness/`, which compiles the pure course files with Roslyn and RUNS
them). An Urchin grinds the rail in its colour at 300 u/s and pays nothing at corners, a Squirrel
skims it for the boost energy that is its only speed, and — because a shield swaps the mesh and
never the collider — ~2,000 super-shielded prisms cost zero always-on colliders and **no cone,
gun, plate or spike can remove one**; only the Rhino's energised sword can, and a rider bridges
the hole at full pace. Rails are DOMAIN-painted because `TrailFollower` keys friendly terrain on
domain (a neutral rail is hostile to every rider). **The grid is balanced by the CARD through a
platform capability this mode introduced: `SO_ArcadeGame.StartingElements`** — per-hull (and
optionally per-intensity) starting element levels (`VesselStartingElements` in Data), applied in
`VesselController.Initialize` on every spawn path on every machine, shipped to clients inside the
config-sync RPC (element levels are simulated on the OWNING machine and never replicate), and
cleared by the menu so no handicap follows a pilot home. Before it, `SO_Vessel.InitialResourceLevels`
was read only on the legacy single-player launch path and every hull started every multiplayer
match at rest. The table is authored by `Tools/Build/regatta_balance.py`, an offline lap-time
model over the MEASURED circuits (constants read off the prefabs by key), and **the honest result
is a residual spread of 6.0× at intensity 1 (8.7× at rest) and 5.3× at intensity 4**: an element
spans ~1.5× on the five hulls it reaches (Time = Soar / afterburner / Serpent boost / Scarab
ceiling / Dolphin fill) and nothing on the Urchin or the Squirrel, while straight-line speeds
span 34×. (It reaches the Rhino's ramp ACCELERATION since Broadside's playtest filled that slot,
and deliberately not the ramp's CEILING, which is most of what a lap is bounded by. ⚠ That was
FALSE as shipped until 2026-09-18: the fleet-wide `Multiplier(Element.Time)` read in
`VesselTransformer.CurrentBoostAmount` applied the same element to the ceiling too, undeclared on
the map and unmodelled here, so `regatta_balance.py`'s Rhino row was right about intent while the
BUILD was wrong. The element-scaling unification removed that read; the model's output is
unchanged, so no handicap was re-authored — see
`Docs/ElementalAbilitySystem/ELEMENT_SCALING_UNIFICATION.md`. General rule: **when a model and a
build disagree, find out which one is wrong before recording it as a modelling gap.**) The generator asserts that spread under 6.1× and asserts the measured course
JSON's source hash against the shipped C#, so neither a vessel retune nor a course edit can ship
on stale numbers. What would close the rest is recorded, not faked: an elemental endpoint on the
Rhino's ramp CEILING (a `/vessel` change — its acceleration already has one), or a pursuit start. Three general rules: **a per-hull
handicap is a fact about the CARD, not the vessel** (a prefab edit moves every mode; a mode-local
multiplier is a cheat); **a structure several hulls use differently must be checked hull by
hull** (what each can ride, skim, ram, and remove — `.claude/skills/arenagame/SKILL.md` §1 is
that table); and **the AI is per hull** — only the Manta's boost has an autopilot drive, so an AI
Sparrow, Serpent, Dolphin or Scarab races at cruise, stated in the doc. Authored headless and
never run in the editor. **Its first playtest found two defects in the ARENA surface, not the
mode, and both reached every arena card.** (1) **The carousel offers EVERY hull the card lists**
— `ArcadeGameConfigureModal.BuildAvailableShips` filtered the card's `Vessels` by the HANGAR's
purchase lock (`SO_Vessel.IsLocked`), six of the eight class assets are authored locked and the
commerce surfaces are de-scoped, so every arena carousel held exactly Squirrel and Scarab. The rule:
**a card's `Vessels` list is the authority on what a mode admits; the hangar lock gates the
hangar** — an arcade card already flies its one hull whether or not the pilot bought it
(`ResolveModeVessel` never asks), so honouring the lock in the arena made the same hull flyable
on a Rampage card and hidden on the Regatta card. (2) **The Scarab's icon WAS the Sparrow's** —
`SO_Class_Scarab.IconActive` pointed at the codex bake `tool_vessel-changer__scarab.png`, which is
byte-identical to the Sparrow's, because every asset-reading mesh harvester
(`CodexImageBaker.HarvestModel`, `ToyModelBuilder.TryBuild`) saw what the ASSET shows: the
Scarab's hull is generated at Awake and its wrapped Sparrow model is hidden at Awake, so on the
asset the real hull is an empty `MeshFilter` beside a still-enabled Sparrow. Both harvesters now
skip `IProceduralElementMorphSource.HiddenLegacyModelRoot` and harvest **`IProceduralHullSource`**
(the Scarab answers it off the asset with the same parts `EmitParts` lays), the codex bake is
therefore correct on its next run, and the card icons are rendered from the SHIPPED hull by
`Tools/Build/render_scarab_card_icons.py` (`--check`, fails on a hull retune until re-run) into
`CardImages/Scarab.png` / `Scarab_Inactive.png`. `ArenaRosterTests` holds both: every arena hull
has both icons, no two hulls on a card wear byte-identical icon FILES (the two sprites were
different assets with the same pixels, which a reference check cannot see), and the roster
builder never consults the lock. General rule: **a harvester that reads a prefab asset sees the
asset, and a hull that builds or hides itself at Awake looks like a different ship there.**
**Its second playtest found two more, both readable only off the rendered frame**: the SELECT
VESSEL button was a CLONE of the Play button's rect that never got moved (same parent, anchors
and offset, hidden on confirm — so it read as "a strange button over Play that goes away when
clicked"; it now lives inside the carousel under the icon, and
`Tools/Build/author_arena_launch_panel_layout.py --check` proves the two rects disjoint), and
the Urchin's `IconActive`/`IconInactive` pointed at sprite guids no `.meta` owns — a missing
sprite draws a SOLID WHITE QUAD, so the carousel showed a white square and called it the Urchin
(`author_urchin_card_icons.py` re-points the class asset at `Urchin_Square.png` and derives the
inactive; `check_vessel_class_icons.py` is the general gate). **A widget cloned from a sibling
inherits the sibling's PLACE; a dangling sprite reference fails as a white rectangle, never as an
error.** See `_Scripts/Controller/Arcade/REGATTA.md`.

`Broadside(57)` is the **ARENA brawl** — Regatta's fighting twin. Regatta asked what every hull
does with a racing line; this asks what every hull does with a rival in front of it. Seven hulls
loose in Dog Fight's **Boneyard** (referenced, not forked — the cell is per-arena, and its
wreckage feeds every hull's economy: a Squirrel skims it, an Urchin grinds it, a Dolphin skims it
for seed energy, a Sparrow's rounds pay ammo off it, a Rhino's sword has mass to cut, a Scarab
forges balls from its crystals), first DOMAIN to the point target on `ScoringMetric.CombatPoints`.
**A hit is priced by its VERB, never by its hull** — a round 1, a contact strike 8, an area debuff
12, a rocket 10/20/30 — so no hull is named anywhere in the scoring path and the card can gain one
without the rule learning anything. **Measured on the shipped containers, only FOUR of the eight
playable hulls could land a scoreable hit at all**, which is what the mode had to fix, and all of
it is counted platform-wide and PAID only here: a new **`CombatHitClass.Strike`** (a contact hit —
a VERB, not a hull: the Rhino's sword and the Squirrel's joust share it because both answer *"I
flew into them"*); **`VesselCombatHitBySkimmerEffectSO`**, whose AUTHORITY had to differ from a
projectile's — a projectile is a pooled LOCAL object and may raise unconditionally, but a skimmer
overlap is observed on **every peer**, so an unconditional raise double-credits and the latch is
per-machine (gate on `IsNetworkOwner`, never `IsLocalUser`, or every AI goes un-scored); the
**Urchin's spike container had `projectileShipEffects: []`** — a spike passed straight through a
rival pilot and did nothing, the third instance of the empty-container gap Dog Fight found in the
skyburst and The Bends in the Dolphin's cone; and **`CombatHitScoring`'s raw tally lost its
else-arm**, which had been filing a Rhino's SWORD as a *bullet* (a new `IRoundStats.StrikeHitsLanded`
carries it) — *an else-arm in a tally is the pricing trap the enum's own doc already records,
one layer down*. **The Serpent is not on the card**: at the time it was cut it had no anti-vessel verb
authored (0/4 abilities), and seating a pilot who cannot score is worse than a shorter roster.
⚠ **That premise expired with the elemental economy** (`Docs/ELEMENTAL_ECONOMY.md` §5): the
Serpent's sniper cone now strips elements from any opposing pilot it passes through, so it CAN
affect another vessel. It is still off the card because what that round is WORTH has not been
priced — it carries an authored placeholder magnitude rather than a `CombatHitClass`, and adding
one is a Broadside balance decision, not a wiring change. Re-open the roster question when it is
priced; do not read the exclusion as evidence the hull is still inert.
Balance rests on one measurement: **`VesselCombatHitLatch`'s window, not the fire rate, is what
bounds scoring against one victim** (the Sparrow's 90-rounds-per-second full-auto is capped by a
0.05 s window long before its trigger is), and the windows are authored **per asset**, which is
the mode's finest lever — the Rhino's sword takes 1.4 s because a swung blade is in contact
*continuously*, the Squirrel's joust 1.0 s because it must re-earn a fresh overtake, the Urchin's
spike 0.12 s because a volley is ~10 projectiles inside ~0.1 s. Result **1.91x spread at rest →
1.35x tuned**, and the general rule it records: **a brawl is more balanceable than a race, because
the mode owns the windows** — a race's rate is bounded by vessel speed, which a card cannot touch.
Stated honestly, the per-hull CONNECT FRACTION is an estimate rather than a measurement, so the
model's MINUTES are a floor rather than a prediction (it prices a rate against a victim you are
already engaged with and does not model the search between engagements), and **Time reaches
nothing on the Urchin or the Squirrel**, so those two get no handicap row and the residual is
reported rather than faked. **Its first playtest changed three things and the third generalises.**
(1) **The target scales with TEAM SIZE** — `perPilot × (1 + 0.6 × (teamSize − 1))` → 100 / 160 /
220 / 280, because the latch is per (shooter, victim, class) so two pilots on one victim BOTH
score and a flat total would make a 4v4 a quarter the length of a 1v1; the fraction is under 1 so
a fuller side still finishes sooner. It resolves on the SERVER and rides
`CombatPointTurnMonitorBase`'s existing NetworkVariable, so a client never computes a target, and
team size is the MEAN pilots per fielded domain (a lopsided 2v1 must not hand the pair a free win
nor ask the lone pilot for a total they cannot reach). Re-targeting was the **eighth** outing of
the comeback trap — 0.02 bought half a level at 100 where it bought three at 600 — now 0.12, sized
against the SOLO target so a fuller lobby only ever buys more. (2) **An AI draws a random hull
from the CARD**: `PickAIVesselType` read the roster through `gameList`, a per-scene
`[SerializeField]` that MinigameBroadside, MinigameRegatta AND MinigameDogFight all leave null, so
it fell through to a hardcoded Sparrow and both ARENA cards fielded eight identical hulls; it now
reads `GameDataSO.AllowedVesselClasses`, which `SyncFromArcadeGame` publishes and
`ResetRuntimeData()` deliberately does not clear. *A per-scene serialized reference is a per-scene
chance to forget — when the same fact is already published on a shared runtime object, read it
there.* (3) **The sword now requires being FASTER than its victim, and the RHINO GOT ITS TIME
SLOT.** It shipped `requireFasterThanVictim: 0` on the reasoning that a sword connects on its own
terms, and what that bought was a card paying the Rhino to PARK — holding the blade alongside a
rival paid 8 points every 1.4 s for station-keeping while charging paid 8 and left you 1200 units
away — so the mode rewarded the exact opposite of the hull's identity. The vessel's half is the
rule: **`RampBoostActionExecutor` has always read `Multiplier(Element.Time)`** and scaled
`accelerationPerSecond` by it, while the map's Time entry was an `(open design slot)` authored
1.0/1.0 — **a capability live in code and flat in data reads exactly like a capability that does
not exist, and it will be written down as one** (this document, BROADSIDE.md and the balance model
all recorded "Time reaches nothing on the Rhino" as a measurement; before writing that down,
check the executor, not the map). It is now **Ramp Spool** (2.5/0.5, no L5 upgrade invented — the
slot is filled, not designed), a FLEET change accepted as one, so Headlong's Rhino spools faster
too. Two modelling corollaries: Time is the ramp's WIND-UP RATE and not its ceiling, so the model
integrates `min(top, cruise + a·t)` over a 2 s brawl straight and takes the ratio of means (2.22×
at full, SATURATING once the hull tops out inside the straight) rather than feeding the raw 2.5×
acceleration ratio in — with all three constants READ off `Rhino.prefab`, because the first cut
hardcoded `cruise 60 / top 1210` and was stale inside the week when a parallel branch zeroed that
prefab's `DefaultMinimumSpeed` and moved both by 10 u/s, so **a constant copied out of an asset is
true only on the day it is copied**; and the level picker was **a coin toss with two faces** (sort around the
median, hand the lower half +1 and the upper half −0.5), so a hull that gained a real endpoint
flipped from slowest straight past everyone to fastest — `solve_levels` now moves each hull to the
level nearest the ANCHOR, the median rate of the hulls Time cannot reach, which is the part of the
roster no handicap can move. The AI needs almost nothing:
Broadside joins Joust and Dog Fight in the seek-players set, so every bot hunts through the
platform whatever hull it drew (most of this roster lands a hit by ARRIVING), and only the two
stick-gesture weapons need a trigger pulled — the Scarab's dash and a replicated Urchin spike TAP
(an AI runs server-only, so a local press would fire spikes on one machine and show them to
nobody). Its four AI templates are `vesselClass: 0` (Random), so a bot grid is a mixed grid too.
**Pricing the verbs exposed that the fleet's ELEMENTAL DRAINS were priced by nothing**: every
shipped drain asset carried a flat **−0.5** on every element it touched, whatever the attack, so a
Manta bloom and a Dolphin cone were both worth 12 and the bloom bit HALF as deep (it drains two
elements, not four), and a warhead grazing for 10 drained exactly as hard as a cone worth 12. A
hit's BITE now tracks its PRICE — `total drain over the elements it touches = points × (2.0/12)`,
per-element magnitude `= total / element count` — derived by
`Tools/Build/author_combat_debuff_magnitudes.py` (`--check`), which reads the prices off
`BroadsideScoringRule.asset` and each element count off the drain asset so neither side can drift.
Three rules come out of it. **TOTAL is what is proportional, never per-element** — a 12-point hit
is worth 12 points of bite however it spreads them, so the Manta's two-element bloom goes to
**−1.0** per element and lands the same total as the Dolphin's four at −0.5; per-element
proportionality is what had left it permanently under-delivering. **The drain inherits the balance
the price already has** — the balance pass flattened POINTS PER SECOND to a 1.33× spread by tuning
the latch windows, and drain-per-second is `hits/s × magnitude × duration/2`, so with magnitude
`= k × points` the drain carries that spread for free; tying the two together is what removes the
need for a second balance pass, and it is why the PER-HIT magnitude is the right thing to make
proportional rather than the sustained pressure. **The anchor is a play-tested number and does not
move**: the Debuff class keeps the shipped −0.5 × 4 exactly, so
`ScarabCavitationDebuffByExplosionEffect` — the asset the Dolphin's cone and the Scarab's plate
SHARE — is untouched, which is what leaves **The Bends and Undertow**, the two modes scored
ENTIRELY on that drain, unaffected by construction (the generator FAILS if it drifts). The
quantity that actually bounds a drain is not the per-hit magnitude but what a saturating attacker
HOLDS a victim at, because temporary effects **stack** (`ResourceSystem.ApplyElementalEffect` adds
an entry per call): `|M| × duration / (2 × cooldown)`, gated at 3.0 levels against the −5 floor.
Stated plainly, three scoring verbs carry **no drain path at all** — a round (Bullet), the Rhino's
sword (Strike), and the skyburst's BLAST and DIRECT tiers, which fold onto the shockwave's drain
and add nothing — so a centre-punch worth 30 drains exactly as hard as a graze worth 10; arming
one is giving a weapon a new property in five shipped modes rather than tuning a number, so the
generator REPORTS the magnitude each would take (−0.167 / −1.333 / −3.333 / **−5.000**, the last
being the whole progression band in one hit) instead of authoring it. One assert was written here
and **deleted**: *"a dearer hit must bite harder"* is true by construction, so its negative control
came back green — *a check nobody has watched fail is a check nobody should trust.*
See `_Scripts/Controller/Arcade/BROADSIDE.md`.

`Waystation(58)` is the **Butterfly-only migration race** — and the first mode on the gate-race
platform whose course is half FLOWN and half TELEPORTED. The rings come in **clusters** (coils you
weave) laid a **FOLD** apart; every pilot flies the same course in ORDER and the first DOMAIN whose
LEAD RUNNER threads the last ring wins, on `ScoringMetric.SwitchesThreaded` and the `BestByDomain`
fold — reused, not added, so the mode adds **no metric, no turn monitor and no scoring class** (its
rule is a second asset on `GateRaceScoringRuleSO`). It exists because the Butterfly's Fold
collapsed to **one reach along the heading**, so it cannot be aimed once begun: the last ring of
every cluster is a separate **exit gate** laid on the line to the next cluster, and threading it
well is what buys a cheap jump — the decision is EARNING THE LINE before the press, on the fleet's
slowest-turning hull. **A teleport threads NOTHING**, and the rule is the platform's rather than
the mode's: `VesselTransformer.TeleportCount` states the fact and `GateRaceController` declines any
step containing one. It is a **COUNTER rather than a distance** because the step guard already
there fails the other way round — `maxPlausibleSpeed` rejects a LONG jump by accident and credits a
SHORT one, so no distance test can be the answer. **Intensity is how much coil there is between
folds, how tight it is wound, and how hard the exit line is** (rings per cluster 3→6, coil radius
160→120, exit cone 40°→90°), and three numbers are deliberately NOT tables (exit lead 120, approach
cap 45°, wander 80°) because each is pinned by something that does not vary with intensity — a
table there would be four copies of one constraint. **Every number in the course is MEASURED**:
`Tools/Build/waystation_course.py` mirrors the shipped C#, reads the tables AND the hull's
constants out of the repo (so a vessel retune moves the course or fails it), sweeps 200 seeds × 4
intensities and FAILS the build unless every corner clears the Butterfly's own turning circle by
20% (measured **1.21×–1.70×** of 85.3 u), every ring's whole MOUTH stays in the cell's shell, every
fold gap is inside the fold's **RESTING** reach (a mode whose hardest jump needed Time 5 would play
differently depending on a crystal the comeback hands the LOSER), every fold gap is LONGER than the
cluster it leaves, no two clusters interpenetrate, and the exit gate's facing is within the
authored cone exactly. `--compile` then **builds and RUNS** the shipped `WaystationCourse.cs` +
`RaceCourseGeometry.cs` verbatim out of `Assets/` against a `Vector3`/`Mathf` shim and compares
**gate for gate** — 3,880 gates to **0.0059 u** — so those six checks are statements about the game
rather than about a transcription of it. Four findings outlive it. **(1) A positional clamp is not
a containment strategy for a walk**: pulling a step's endpoint back into the shell SHORTENS the leg,
and a leg is exactly what the corner radius is measured on — the clamp that kept rings in the cell
was simultaneously producing corners at **38 u** against a hull that needs 85, and it only fires at
the wall so no per-corner tuning could find it. **(2) A single capped re-aim cannot contain
anything** — the steered version reached **3,576** units from the cell centre inside a 1,200
membrane, because a bounded turn cannot undo an unbounded walk. Containment is now a property of
the CONSTRUCTION: every cluster centre lies exactly on one sphere and a hop is a **geodesic step**
on it, so there is no clamp, no rejection, no retry and no failure path. **(3) A coil ring cannot
be an aiming device** — its facing is its own tangent, whose tilt off the coil plane is fixed by
`pitch / (CoilRadius · step)`, so it rolls in AZIMUTH and never in ELEVATION (measured 167° off the
fold against a 72° cone); the exit gate is its own gate and faces the next cluster **from its own
position**, because a fold starts where the pilot is. **(4) A corner is measured on CHORDS**, so
the approach cap must clamp the chord — clamped against the ring's TANGENT it bounded the wrong
angle and the real corner ran 45% under it. An AI **folds** (`aiFoldMinDistance` / `aiFoldAimDegrees`
/ `aiFoldRetrySeconds`), driven from the CONTROLLER in the Broadside/Tollway/Hijack shape with the
numbers read out of the ability's own asset through the new
**`R_VesselActionHandler.TryGetBoundAction<T>`** — the sibling of `TryGetInputForAction` that also
hands back the SO, so an autonomous pilot deciding HOW LONG to hold a held ability does not copy a
reach speed into a mode. It presses through `PerformShipControllerActionsReplicated` (an AI is
simulated server-only) and **fails safe**: all three gates refuse by not pressing, and a bot that
never presses flies the course exactly as before. That drive needed **`GateRaceController.OnServerTick`**,
and the reason is worth carrying: **a Unity MESSAGE on a base class is a slot a subclass can take
without being told** — a subclass `void Update()` would have hidden the base's and stopped crossing
detection, the whole race, with nothing in the console. Toasts are **two idle hints and nothing
else**, because the platform has no gate-threaded hook and *an enum member nothing raises reads
exactly like a feature*. See `_Scripts/Controller/Arcade/WAYSTATION.md`.

**The Butterfly's other three element games** — `Dustup(59)`, `Tapestry(60)`, `Sirocco(61)` —
complete the set Waystation began: **one game per element, and each one's genre petal IS that
element** (`ModeGenre`: Time race / Charge pilots / Mass making / Space destroying), so the card's
petal and the element that scales the verb are the same fact. **Dustup** (Charge) is a dust duel in
Dog Fight's Boneyard: the Scale Dust hangs BELOW the hull, so a rival is hit by flying OVER them,
and each pass is one `CombatHitClass.Strike` point off a reporter the dust container has carried
since the hull shipped (counted everywhere, paid only here; the generator asserts both halves).
**Tapestry** (Mass) is **timed** (150 s) and scored on the new **`ScoringMetric.VolumeRemaining`
(12)** — volume standing at the whistle, because a COUNT would pay a narrow Dust-mode line exactly
as much as a 5x-20x Mass-mode brush, and a live stock that raids can lower cannot end on a
first-past-the-post target. It runs in the bare **Barren** cell for Hijack's reason (a food web
eats the trailing team's fresh paint) with a peer-local elemental crystal scatter as its
intensity dial (24/16/10/6), and its comeback rate is DERIVED from the hull's own wake (265 volume
per key × 7.44 keys/s, read off `Butterfly.prefab`). **Sirocco** (Space) is an erosion race
through Rampage's forest on Rampage's rule (`PrismsDestroyed`, target 600): Space lengthens the
capsule, so reach is swath. One platform piece is shared: **`ButterflyAutopilotModeDriver`** —
`AIPilot` writes a stick and a throttle and nothing else, and a Butterfly spawns in Mass mode, so
without it every AI would fly with its weapon off (the Tollway rule); it presses the mode switch
through `PerformShipControllerActionsReplicated`, finds it by capability
(`TryGetBoundAction<SpreadWingsActionSO>`), and READS the mode back rather than counting presses,
because a toggle a driver counts drifts the first time a press is lost or doubled. All three are
authored headless (`author_dustup/tapestry/sirocco_assets.py`, `--check`) and have not been run in
the editor; their targets are reasoned, not measured. See `DUSTUP.md`, `TAPESTRY.md`, `SIROCCO.md`.

**ARENA SEATING — six seats, one pilot per hull, and a human can take an AI teammate's ship.**
One authored bit, `SO_ArcadeGame.ArenaRules` (on the four `ArenaGames` cards and nowhere else,
published as `GameDataSO.IsArenaMatch`), carries three rules. Arena cards go to
`MaxPlayersAllowed: 6`, but what the lobby offers is `SO_ArcadeGame.MaxSeats` =
`min(MaxPlayersAllowed, distinct hulls listed)`, because **every hull is flown by exactly one
pilot** — settled by a server-arbitrated lobby claim (`Player.NetArenaHullClaim`: SELECT VESSEL is
a CONTEST two guests can press in one frame), an AI draw from the hulls left, and a spawn-time
backstop for any path that skipped the lobby. And a human may hand their hull to the AI and take
an AI teammate's mid-match (**D-pad left/right, keyboard 1/2**, `PilotSwap`) — the Cellular Duel
vessel exchange generalised, so nothing spawns and hull exclusivity survives any number of swaps;
score follows the PILOT. Astro League lists three hulls and therefore seats three. General rule it
records: **a hull that changes machines MID-FLIGHT exposes every piece of simulation state that was
decided once at spawn** — the vessel's replica NetworkVariable subscription now follows ownership
(`OnGainedOwnership`/`OnLostOwnership`, idempotent) and the transformer re-seeds its integrator
(`AdoptCurrentMotion`), and **a stopped autopilot must leave its hull holding nothing** —
`StopAIPilot` now releases the commit drift and any cycled ability it started, which played as an
unrecoverable spin the first time a human took over an AI's hull mid-drift. The same rule holds for
anything a MODE hangs on a bot: its steering hooks follow the bot across a swap
(`AIPilot.TakeModeHooksFrom`), never the hull it spawned in. **Every lobby AI seat is a placement**
(`ReconcileAiPlacements`) - the card's minimum is placed balanced ONCE and then fixed, never re-balanced
on redraw, because a seat the host can see must be a seat the host can move. Seat caps live in more
than the card: the launch modal's Add AI ceiling (`MatchSeatCeiling`, 6 on an arena card) and the
replicated `LobbySnapshot`'s AI slots (6) both had to move with it. `Docs/HomeHub/ARCHITECTURE.md` §3.9.

`WildlifeLiberation(40)` is the **Sparrow-only hunt** — three concentric cages at 1050 / 600 / 200 pen three tiers of wildlife (a very heavy swarm of small creatures outside, much bigger ones in the middle room, the biggest and toughest in the core), plus a fourth tier loose in the open water outside the outer cage where players spawn; the first **DOMAIN** to 250 summed kills wins. It is an ordinary domain race and that is deliberate: a per-PLAYER (free-for-all) winner shipped here briefly and was **reverted**, because the mode seats up to four players while the platform has only three playable domains, so a full lobby always has teammates and a per-individual winner bypasses every domain surface (winner banner, HUD panels, scoreboard ordering, `ResolvePlacementOrder`). Do not re-derive it. Its metric, `ScoringMetric.LifeformsKilled`, is the first whose source is the ECOLOGY rather than prisms or crystals — and the first that needs an RPC, because fauna are client-local so a client's kill is invisible to the server (`Player.ReportFaunaKill_ServerRpc`; the round-trip stays correct once fauna network sync lands). Shipping it made **every creature in the game killable by shooting its body prisms** (previously only the worm colony was — see `Docs/ECOSYSTEM.md §24`) and generalized the cell's single fauna pen into a per-species BAND. See `_Scripts/Controller/Arcade/WILDLIFE_LIBERATION.md`.

Many single-player modes (1, 3-6, 9-25, 27) reference scenes that no longer exist on disk — their `SO_ArcadeGame` assets still exist and appear in the Arcade UI, but launching them would fail. (`Rampage(2)` used to be in this set; it now has a real scene as a multiplayer mode.)

#### Controller Hierarchy

```
MiniGameControllerBase (abstract, NetworkBehaviour)
│   Template Method: rounds → turns → countdown → gameplay → end
│
├── SinglePlayerMiniGameControllerBase (abstract)
│   ├── SinglePlayerSlipnStrideController  — procedural course with intensity scaling
│   ├── SinglePlayerWildlifeBlitzController — blitz scoring (only BenchmarkStressTest uses it now)
│   └── WildlifeBlitzMiniGame             — minimal variant
│
└── MultiplayerMiniGameControllerBase (abstract, NetworkBehaviour)
    │   Server-authoritative turn/round/game flow via ClientRpc
    │
    ├── MultiplayerFreestyleController     — sandbox, per-player activation
    ├── CoOpWildlifeBlitzMiniGame    — co-op, own ready-sync
    │
    └── MultiplayerDomainGamesController
        ├── SkimRaceController              — crystal race, deterministic track, golf scoring
        ├── JoustController      — collision tracking, golf scoring
        ├── OnlineDuelForTheCellController — vessel ownership swap between rounds
        ├── ScurryController — minimal (1 round, 1 turn)
        ├── AstroLeagueController             — hypersea soccer (Rhino-only, sword strikes), server-simulated ball, golden goal
        ├── BroodRushController             — nucleus-control fauna-wave race, brood scoring
        ├── CleaveController                      — Rhino-only slicing race; four unrelated arenas, one per intensity
        └── RampageController                 — Dolphin-only destruction race (Scurry's destructive analog), prisms-destroyed scoring
        └── WildlifeLiberationController       — Sparrow-only three-cage hunt, ecology-scored (creatures killed)
        └── DogFightController                  — Sparrow-only gun duel in the Boneyard; first DOMAIN to the gunnery-point target
        └── BendsController                      — Dolphin-only debuff duel; first DOMAIN to the bend target
        └── ScarabScrambleController             — Scarab-only hoop-court party game ("roll your ball home"); first DOMAIN to the goal target
        └── SalvoController                     — Sparrow-only demolition race in the Boneyard; crystal-fueled missiles, wingman reload, prisms-destroyed scoring
        └── HijackController                    — Urchin-only rail heist in the Switchyard; grind, launch, cascade; prisms-STOLEN scoring (nothing is destroyed)
        └── TollwayController                    — Scarab-only ring race; player-placed rings that ANY ball pays, monuments raised by the scoring
        │
        └── GateRaceController (abstract)         — the shared gate-race platform: course broadcast, rings, crossing detection, the owner-detects/server-records round trip, AI steering, final scores. A subclass supplies its NAME, its COURSE, whether that course WRAPS, and how many rings at the front are a LEAD-IN the laps skip (`LeadInGates`, default 0 — a closed circuit cannot start fairly on its own). It also offers **`OnServerTick`** — a named hook rather than a `virtual Update`, because `Update` is a Unity MESSAGE and a subclass declaring its own would HIDE the base's and take crossing detection down with it, silently (Waystation's autopilot fold is its one caller)
            ├── SwitchbackController              — Dolphin-only gate race; an OPEN chain flown once
            ├── HeadlongController                — Rhino-only circuit race; a CLOSED loop flown in LAPS, every corner cut to the Rhino's flat-out turn radius
            ├── BreakwaterController              — Sparrow-only station race; a polar START GATE (the platform's one lead-in) plus a closed circuit of danger-woven dishes, fire/saw/thread. 202 lines: the course, the lead-in and the stations, and nothing else
            ├── SkeinController                   — Urchin-only cable race; the rings sit ON the arena's own rails, so the course is a thing you RIDE rather than fly between
            └── RedlineController                 — Manta-only circuit race; Headlong's solver cut to the Manta's 237u full-boost circle, laps, and an autopilot that Soars
            ├── RegattaController                 — the ARENA race: every playable hull, three domain-coloured super-shielded rails braided along a lapped circuit, a start line behind gate 0 (`IPlayerSpawnLine`); the grid balanced by the card's `StartingElements`
            └── WaystationController              — Butterfly-only migration race; an OPEN chain dealt into CLUSTERS (coils you weave) laid a FOLD apart, so the course is half flown and half teleported. A teleport threads NOTHING (`VesselTransformer.TeleportCount`), and an autopilot folds through the base's new `OnServerTick` seam
        └── BroadsideController                 — the ARENA brawl: seven hulls in the Boneyard, each fighting with the weapon it has; a hit priced by its VERB (round 1 / strike 8 / debuff 12 / rocket 10-30), never by its hull
        └── BloomrushController                 — Manta-only bomb-tag party game; 120 s timed round, volume-destroyed scoring, fuses-beaten tiebreak
        └── WreckingBallController              — Scarab-only demolition race in a court grown full of Rampage's forest; the ball and the plate both score prisms-destroyed
        └── UndertowController                  — Scarab-only cavitation duel in Wildlife Liberation's cages; bends (3) + creature kills (1) folded through DomainValue
        └── DustupController                    — Butterfly-only dust duel in the Boneyard; a pass OVER a rival is a Strike point (CombatPoints)
        └── TapestryController                  — Butterfly-only timed painting war in the Barren cell; volume standing at the whistle (VolumeRemaining)
        └── SiroccoController                   — Butterfly-only erosion race through Rampage's forest; prisms-destroyed scoring
        └── TandavaController                   — the first CO-OP ARENA card (Rhino / Squirrel / Sparrow, one domain): one scripted swarm hunts flora through a small CLOSED cell crowded with a seven-fork reef, its speed set by how threatened it feels - healthy it LUNGES at pilots, hurt it flees; feeding, a serpent rolls up round its meal in a formation drawn each meal (coil, wrap, figure-eight), its plates go out as danger guards round it and it cannot heal. Banked, the Great Serpent becomes a Many-Headed Serpent, rises into the Lord of the Dance inside a halo of switch rings (the one time the cell changes colour, gold), and becomes the Sea Lion, whose last feast completes the cycle; every match draws one of three variants of each form; the match ends on the swarm's outcome, never on a clock (`TANDAVA.md`)
```

#### Game Launch Pipeline

1. **`SO_ArcadeGame` asset** — static config (mode, scene, captains, player/intensity ranges, scoring)
2. **`ArcadeGameConfigSO`** — ephemeral UI state (selected game + intensity + players + vessel)
3. **`GameDataSO`** — shared SOAP runtime state (all game params + SOAP events)
4. **`SceneLoader.LaunchGame()`** — subscribes to `OnLaunchGame`, loads scene. Game config is synced to clients by `MultiplayerMiniGameControllerBase.OnNetworkSpawn()` in the game scene
5. **Game controller** — scene-placed `MiniGameControllerBase` subclass drives turn/round/game lifecycle
