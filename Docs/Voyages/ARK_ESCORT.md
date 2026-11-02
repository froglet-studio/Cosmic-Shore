# The Ark escort

**Status: BUILT, tuned by sim** (Garrett, 2026-10-29). Branch `claude/ark-escort-voyage`.

> "a voyage that ... is attractive for 3 vessel squad to protect their ark and escort it through the hypersea. the
> voyage should go through 5 cells and one of them should be very challenging to survive ... a squad of any three
> vessels, set a difficulty from 1-9 and have an easy time with 1 but an impossible challenge with 9 ... 3 should be
> fun, 4 should be challenging" (Garrett, 2026-10-29)

## 1. What it is

A voyage card of its own in the Voyage window (beside the pilots' jobs): **Escort the Ark**, with a difficulty
stepper (1 to 9, remembered per device) and Set sail. Setting sail enters freestyle, deploys the active squad (you
fly your seat, AI wingmates fly the others, each in its slot's vessel, pilot, artifact and element) and the Ark sails
five worlds in order. The escort ends when the Ark reaches the last world's core (made it through) or is overrun.

| # | World | Cell config | Its creatures |
|---|---|---|---|
| 1 | Garland Shallows | Garland | brittlestars, quadfish, sharks |
| 2 | Scarab Reef | Scarab Scramble | tadpoles, brittlestars, piranhas |
| 3 | The Boneyard | Boneyard 1-4 (by difficulty) | scavengers |
| 4 | The Swarm | Swarm | three swarms, the substrate packs (hunters, locusts, leeches, a leviathan...), builders, NCA lizards |
| 5 | **The Maw** (the gauntlet) | Wildlife Liberation 1-4 (by difficulty) | quadfish, brittlestars, sharks, worm colonies, clawfish |

Nothing is scripted. A world that starts **hostile** is controlled by a domain opposing the squad's, so its fauna wear
that colour and graze the Ark's hull (`Cell.SetReplicatedStartingController`); taking the world back is laying more
of your own mass in it than they have. Every AI seat escorts (`ArkEscortPilot`): seat 0 hunts the hostile creature
nearest the Ark, seat 1 the next, seat 2 the next, each holds a station off the bow when there is nothing in reach.

## 2. Difficulty

`Assets/_SO_Assets/Toys/ArkEscort.asset` (`ArkEscortSO`) holds the five worlds and nine tiers. A tier is only numbers
the corridor already had:

| Field | What it does |
|---|---|
| Population | multiplies each world's fauna and flora seeds and caps (`Cell.RuntimePopulationScale`), times the world's own Threat |
| RegrowSeconds | the Ark's reactor: seconds per regrown hull plate (`ArkReactorSO.WithRegrowSeconds`) |
| HostileShare | chance each world (from its HostileFrom difficulty up) starts hostile |
| ArkSpeed | a slower Ark spends longer in each world |
| BreachHull | the Ark is **overrun** (escort lost) below this share of its hull |

A world can also name a **ladder** (its biome's own harder configs, easiest first) that difficulty 1-9 climbs, and a
**HostileFrom** difficulty below which it starts as the corridor leaves it.

Why a breach line: the Ark's shielded plates (about a fifth of the hull) are never grazed, so measured in the sim the
hull bottoms out near 20% and the Ark never truly falls to fauna. The breach line is the escort's loss condition; a
true fall (the last plate) still ends it too.

## 3. Tuning (the escort sim)

`ArkEscortSim` plays escorts with every seat on AI and writes one JSON report per run (each world's hull at entry,
lowest and exit, peak threats, how it ended). Unattended, from the command line:

```
Unity.exe -projectPath . -executeMethod CosmicShore.Editor.ArkEscortSimLauncher.Run \
  -arkEscortSim "1:Sparrow,Rhino,Manta;5:Squirrel,Dolphin,Serpent" -arkEscortOut Logs/escort.jsonl \
  -arkEscortTimeScale 3 -arkEscortQuit
python3 Tools/Build/ark_escort_report.py Logs/escort.jsonl
```

The measured runs and what each tuning pass changed are in §4.

## 4. Tuning log

All runs at time scale 3, every seat on AI. "Fell in" is the world the Ark was overrun in.

| Pass | What changed | What the sim showed |
|---|---|---|
| 1 | first tiers (population 0.25-2.2x) | d1, d3 and d9 all survived; d9's lowest hull 18%. The Swarm world never bit. |
| 2 | the Swarm cell made a leg, steeper tiers | every difficulty survived with the hull bottoming at 19-26%: **the Ark's shielded plates are never grazed**, so it cannot fall to fauna. |
| 3 | `BreachHull` (overrun line) per tier | a loss condition at last; but d1 lost half its hull in the last world, and squads made no difference. |
| 4 | `HostileFrom` per world (one more hostile world per difficulty) | d1-d2 clean (100%, 94%), d3 tense (22%), d4-d9 overrun, all at The Boneyard. Three different squads: identical numbers, **0 kills** in hostile worlds. The escort AI was flying at the creatures' bodies and killing nothing. |
| 5 | escort AI flies at the creature's **heart** (a joust kills it); the hungriest world (Boneyard) moved last as The Maw | 20-50 kills per world, squads now differ (lowest hull 23% vs 31% at d3), d4 falls in the final world. |
| 6 | breach lines spread 0.2-0.72 | Sparrow+Rhino+Manta: d1-d3 survive; overrun in world 5 at d4-d5, world 4 at d6, world 3 at d7, world 2 at d8. d9 at 2.6x population **lost the GPU device** (D3D12 fence) in the editor. |
| 7 | populations capped at 2x (1.4x at d9), d3 breach 0.15, d4 0.25 | Squirrel+Dolphin+Serpent: same shape (d4-d6 world 5, d7 world 4). The Maw strips the Ark to its plates at any population: its scavengers barely follow the population scale. |
| 8 | **world ladders**: Liberation Deep and The Maw climb their biome's own 1-4 configs with difficulty | Sparrow+Rhino+Manta: d1 100%, d2 94%, d3 survives; overrun d4 world 5, d5 world 4, d6 world 5, d7 world 3, d8 world 3, d9 world 2. Squirrel+Dolphin+Serpent: d3 survives, d4 world 3, d5 world 4, d7 world 5 (the runs are noisy one at a time). |

| 8b | squad agency check | one Sparrow alone vs Sparrow+Rhino+Manta at d4: **identical** (24%). Boneyard scavengers strip the Ark to its plates whatever the escort does: no world for a squad to win. |
| 9 | **The Maw is now Wildlife Liberation** (its ladder; its damage follows population and the squad), The Boneyard is a mid world that turns hostile from 6 | Sparrow+Rhino+Manta: d2 41%, d3 45% survive; overrun in The Maw at d4 (26%), d5 (36%), d6 (41%); at The Boneyard at d7 and d8; in Scarab Reef at d9. A lone Sparrow at d3: 23% (one plate-share from losing). Squirrel+Dolphin+Serpent: d3 24% survive, d4 25% overrun, **d5 survived at 71%**. |

What this means for play: 1 is a cruise and 2 a first scare. 3 is fun: The Maw takes the hull to about half with a
full squad, and a lone or weak escort finishes one notch above the line. 4 asks the squad to keep a third of the hull
through The Maw, and the AI squads fall just short (24-28%), so a better squad or better flying wins it. 5 and 6 are
the same fight with a higher line and hungrier worlds (an AI squad won one 5 in two tries). From 7 The Boneyard wakes,
and each difficulty ends the voyage a world earlier, until 9 is over in the second world.

### The shipped tiers (pass 9)

| d | Population | Regrow (s/plate) | Ark speed | Breach | Hostile worlds |
|---|---|---|---|---|---|
| 1 | 0.2 | 0.3 | 1.3 | - | Garland |
| 2 | 0.3 | 0.4 | 1.2 | - | + Scarab, The Maw |
| 3 | 0.45 | 0.6 | 1.1 | 20% | same |
| 4 | 0.6 | 0.9 | 1.0 | 30% | + The Swarm |
| 5 | 0.75 | 1.3 | 0.95 | 38% | same |
| 6 | 0.9 | 1.9 | 0.9 | 45% | + The Boneyard |
| 7 | 1.05 | 2.8 | 0.85 | 50% | all |
| 8 | 1.2 | 4 | 0.8 | 58% | all |
| 9 | 1.4 | 6 | 0.75 | 72% | all |

The Swarm cell is authored hostile, so it is hostile at every difficulty; below 4 its creatures are too few to reach
the Ark. The Boneyard's and The Maw's rungs: 1-3 the biome's config 1, 4-5 config 2, 6-7 config 3, 8-9 config 4.

### Confirmation (after pass 9, no tuning changes)

Eight escorts on the shipped tiers, mixing squads to see whether the lines hold beyond the squad they were tuned on.

| d | Squad | Result | Lowest hull | Where it was decided |
|---|---|---|---|---|
| 2 | Manta+Dolphin+Rhino | Survived | 56% | The Maw ends at 64% |
| 3 | Manta+Dolphin+Rhino | Survived | 57% | The Maw ends at 63% |
| 3 | Sparrow+Rhino+Manta | Survived | 32% | The Maw ends at 38% |
| 4 | Squirrel+Dolphin+Serpent | Survived | 31% | The Maw, one point above the 30% line |
| 4 | Sparrow+Rhino+Manta | Overrun | 29% | The Maw, one point below the line |
| 5 | Manta+Dolphin+Rhino | Overrun | 37% | The Maw, against 38% |
| 6 | Squirrel+Dolphin+Serpent | Overrun | 44% | The Boneyard, against 45% |
| 7 | Sparrow+Rhino+Manta | Overrun | 36% | Garland cost 31%, then The Boneyard took it under 50% |

The lines hold: 2 and 3 are won with room to spare, 4 is a coin toss decided by a point or two, and 5 and up are lost
by a few points, so a better escort turns them. One sweep stopped itself mid-run in the editor (a lobby error) and
its last three escorts were rerun.

### Known gaps

- The sim's runs are single samples and noisy (the same squad at the same difficulty has differed by 20 points of
  hull). The tiers are set so each line sits just above what the AI squads reach; a human escort is the real test.
- The Boneyard strips the Ark to its plates whatever the escort does, which is why it only wakes at 6.
- The Grizzly+Urchin+Falcon squad deployed only one AI wingmate in every run: one of those hulls does not spawn as an
  AI companion.
- The Voyage window card and its stepper were compiled and wired but not looked at on screen (no screen access while
  the sim ran).

### 4.1 Since pass 9: the voyage-night passes (2026-10-30 to 11-01)

The tables above are the October tuning. The overnight passes on `claude/voyage-night` changed the escort under them,
and each pass's write-up is in [`night/`](night/) (`*_DONE.md`). In short:

- **Plates are food, and the Ark can truly fall** (`night/ark_core_DONE.md`): creatures gnaw armour in escort worlds
  (an `ECOSYSTEM.md` §0.x carve-out), so every tier's `BreachHull` is now **0**. The breach line in §2 is kept as an
  optional field; an escort is lost when the last plate goes.
- **The chaos ladder** (`night/chaos_DONE.md`) replaced the pass-9 tiers: regrowth only in a held nucleus
  (`Ark.RegrowAllowed`), the feeding lure (`Cell.SetFeedingLure`, an `ECOSYSTEM.md` §0.z carve-out), faster waves,
  armour bites for every escort creature, and every world hostile from d1. Its final table is the shipped
  `ArkEscort.asset`; in sims d4 won 8/8, d7 1/8 and d9 0/4.
- **Variety, roles, pacification, fauna threats, the Ark's feel**: `night/variety_DONE.md`,
  `night/escort_roles_DONE.md`, `night/pacify_DONE.md`, `night/fauna_threats_DONE.md`, `night/ark_feel_DONE.md`.

## 5. On the galactic map (the HyperSea)

**Status: wired, minimal** (`claude/voyage-ship`, 2026-11-01). Garrett asked for the escort to "integrate with the
galactic map work as best we can". The map is the Voyage Board's HyperSea (`Docs/Voyages/VOYAGE_BOARD.md`,
`HyperSea.cs`, `VoyageChart.cs`) and the slice's places (`voyage_slice.json`). The escort now starts and stops at
real places on it; its worlds and its tuning are unchanged.

### What is wired

| Piece | Where | What it does |
|---|---|---|
| The course | `VoyageRules.EscortRoute` | From where the squad's Ark lies (`VoyageState.At`, the same saved galaxy the jobs use) to the nearest place it can berth that is not here, by lanes: a **port**, or the squad's **own harbour**. Never a rival domain's harbour (VOYAGES.md §6.7 rule 2). Ties go to the location listed first. Null at sea, or when nothing qualifies. |
| Arriving | `VoyageRules.EscortArrived` | An escort that **made it through** berths the Ark at its destination: `At` and `LastPort` move there and a day passes, so the next board is drawn from the port it reached. An **overrun** (or abandoned) escort moves nothing: the Ark limps back to where it set out. It never moves an Ark that is out on a job. |
| The run | `ArkEscortRun.Course.cs` | Plans the course at Set sail, names it under the departure banner (`ArkEscortSO.courseLine`, `{from} TO {to}`) and the made-it banner (`berthedLine`, `BERTHED AT {to}`), and saves the arrival. **The escort sim never saves** (every seat AI), so every sim run sets out from the same place. |
| The report | `ArkEscortReport.From`, `To`, `CourseLightYears`, `CourseCells`, `CourseIntensity` | Where each run sailed, measured on the real sea. The summary line reads `(loc.harbour.jade > loc.tollmoor, N ly)`. |
| The card | `VoyageJobsView.ShowEscort` | Names the route with the jobs' own authored `CardHomeRoute` (`{from} TO {to}`), and its course line is the real sea's (`CardCourse`: light years, the thickest water). While the escort is out, the window's one galaxy map frames the escort's course, as it does a job's. |
| Tests | `VoyageRulesTests` (two), `VoyageShellTests.ShippedContent_GivesTheArkEscortACourseOnTheRealSea` | The berth rule, the arrival rule, and the shipped content: every domain's harbour sails to Tollmoor on a course the chart can measure, and back home from there. |

With the shipped slice that means: from any harbour the escort sails to **Tollmoor** (the one port; every harbour has
a lane to it), and from Tollmoor it sails **home**. From a vault field or a core it sails to the nearest berth.

### What it does not do yet (the plan)

These stay inside what both designs already say; none of them is built.

1. **Worlds from the course.** The escort still sails its own rolled worlds (`ArkEscortVariety`), 4-5 of them,
   whatever the lane. The real sea puts the slice's places 27-62 cells apart (VOYAGE_BOARD.md §5 decision 8). The
   seam is `HyperSeaCourse.Steps` (each cell's density and danger): pick each escort world by the sea it crosses
   (spray and froth the gentle worlds, thick water The Maw and Scarab Reef), and set the leg count from the course's
   cells. It waits on decision 8 (short legs for play, or real lengths).
2. **Difficulty from the course.** `VoyageChart` gives each course an intensity, I1 to I4. It could set the stepper's
   default, or the ladder's floor. That is Garrett's call: today difficulty is the player's own choice, 1 to 9.
3. **The escort as a job.** VOYAGES.md §6.2 says a Jade defence job "can also name a moving thing (a courier's
   Ark)". That job, with the squad's own Ark as its target, is the natural home for the escort on the board: its pay,
   its faction and its news. Today the escort is a card beside the jobs, with no pay and no news written.
4. **A fall on the map.** The escort carries nothing aboard, so an overrun leaves no wreck. Once an escort carries
   artifacts (`VoyageState.Aboard`), an overrun should call `VoyageRules.ArkLost` at the location it was bound for,
   which leaves the wreck and its news.
5. **Party.** A friend's machine follows the host's job voyages through `VoyageCourseLink`. The escort's course is not
   sent, so only the host's card names it.
6. **The sky.** `HyperSeaSkybox.shader` already draws a galactic plane (`_GalacticNormal`). It could be tilted by the
   Ark's height above the midplane (`VoyageLocation.GalaxyZ`) and tinted by `HyperSea.SeaColor` of the water the Ark is
   in. That needs a per-run material copy, never a write to the shared skybox asset.
