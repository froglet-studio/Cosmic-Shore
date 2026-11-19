# Vessels flagged for redesign (elemental loop step 5)

Garrett, 2026-11-18 13:58 UTC: "don't cheat. our goal is to get our vessels balanced, not give them handicaps or
cheats so they can compete. if the tuning levers aren't working then flag the vessel for a redesign." And at 13:59:
"this applies to vessels that are too fast or too slow. either can be flagged for redesign."

A hull is listed here when its own vessel levers (its element parameters, using the knee method of step 5) cannot bring
it within the Regatta bar (1.10x of the polished five) without gutting the kit. A listed hull goes back to loop step 1
for Time on the board (`elemental_loop.py set <Vessel> Time 1 redo`). No course, per-mode or starting-Time handicap
props it up meanwhile.

How it was measured: the shipped Regatta shuffle (8 courses, `w3/shuffle/SET.md`, #2211), each hull racing solo in the
real game headless (`regatta_judge.py`), 3 seeds, geometric mean of the per-course medians. The card start is rest
(Time 0) for every hull, and the L5 start is Time 5, with no StartingElements rows since this change. The polished
five run 68.5-74.3 s on the card and 63.5-66.9 s at L5. Sweeps: `/mnt/project-files/regatta-flywheel/step5/r6/` and
`r8norows/`.

| Vessel | Card s | L5 s | vs the five | Mechanism | Levers tried (best result) |
|---|---|---|---|---|---|
| Serpent | 178.6 | 115.7 | 2.6x / 1.8x slower | Solid Fuel Pellets: pace comes from pellet burns, which last 1x at rest and 2.8x at Time 10; it raced Regatta at Time 10 only through its starting row | Pellet duration (Time): reaches the field only when it starts at 2.8 and peaks at 3.0 (card 78.7 s, L5 69.5 s), so Time no longer scales the ability. That is the row moved inside the kit, so it is flagged instead |
| Manta (L5) | 86.1 (card, in the field without its -5 row) | 27.9 | 0.42x at L5 | Soar, its Time level-5 upgrade: a held boost that turns 60 s laps into ~20-30 s | Soar's BoostSpeedMultiplier (Time, acts only once Soar unlocks) at x0.375: L5 43.5 s, about 0.66x |
| TigerMoth | 152.4 | 122.0 | 2.1x / 1.8x slower | One Drop per ring, each after a 90-degree line-up (heading square to the ring, belly rolled onto it), at a 67 u/s cruise between drops. Not the recharge: from 2x cheaper on, the Drop is back before the next line-up ends | Drop recharge (the Drop's own parameter; Garrett's 2026-11-18 pick, cruise kept): 2x / 2.5x / 3x / 4x / 5x cheaper than design all run card 152.4 s and L5 122 s, so 2x (shipped, #2207) is the knee and nothing past it moves. Time throttle slot, knee x2.0: card 104 s, L5 84 s (1.55x / 1.27x), and Waystation breaks (I4 2 of 3 DNF). Patch held: `step5/tigermoth/time-throttle-x1.9-2.1.patch` |
| Jacks | 158.3 | 153.6 | 2.15x / 2.37x slower | One herd-and-travel cycle per ring: the hull holds while the jacks run out at 208 u/s, lay the decoy past the ring and meet behind it, then the hull travels through. Between cycles it flies at the rig's 50 u/s. Time 5's second decoy rarely finds a second ring on the board, so L5 is no faster | No Time lever acts on race pace (Time scales the herd's speed, `herdSpeed`, not the jacks). Jack run speed (`dogSpeed`, kit) at the 37.5% lever cap: Ref1-Ref4 geo mean 162 -> 118 s, about 1.6x. Flagged |

## Serpent

Garrett's direction (2026-11-18), verbatim: "serpent will probably need a way to turn its wall 90 degrees so it can
easily drop a wall and keep flying without having to do the 90 degree turn like the tiger moth. being constrained to a
plane in order to grow its boost charges is already challenge enough. we will still need a way to keep stop and seed
indistinguishable to an opponent. so we will need to reorient both, but that is hard because the decoy becomes the
supershielded prism at the center of the wall, so its current orientation made sense."

Garrett is doing the Serpent redesign in another project. Its kit changes land on bleeding-edge from there; re-judge it
on the shuffle set when they do.

## Redesigned: Rhino and Swordfish, the ramp runs on ploughing and slicing (2026-11-18)

Garrett, 2026-11-18, verbatim: "so the rhino and sword fish have natural levers for their design changes. they like to
slice and plow. make their boosting resource tied to their slicing and plowing. that will slow them down over longer
time scales, while letting them keep their speed and reward them for their fun. win-win while keeping the identity and
feel of the speed"

Before: card 33.5 / 32.1 s (Rhino) and 38.6 / 37.5 s (Swordfish), about 0.5x of the five, on a free ramp that spools
to 16.8x (840 u/s) on any straight. The best lever (half the ramp top) reached only about 0.6x / 0.68x.

The kit change (every mode, `RHINO_RAMP_BOOST.md` § "Fuel"): the ramp burns a tank (0.1 a second at full power), each
prism the Rhino ploughs adds 0.0018 and each prism the Swordfish's bill slices adds 0.004 x Time (x1 at rest, x4.5 at
level 10). A full tank still reaches the 840 u/s top; an empty one tops out at 2.5x (125 u/s). The tank starts empty.

| Vessel | Card s | L5 s | vs the five (card 68.5-74.3, L5 63.5-66.9) |
|---|---|---|---|
| Rhino | 70.0 | 66.9 | inside at both starts |
| Swordfish | 70.8 | 65.8 | inside at both starts |

Measured like the table above (shuffle set, real-game judge, 3 seeds, geometric mean of per-course medians), on the
branch that ships it. What the search showed: without the empty-tank ramp the pace was bistable (27 s or 160-190 s per
course); a full starting tank left the short courses unchanged; the Swordfish's Time element had no pace term, so its
L5 start was slower than rest until Time scaled the slice fuel (decision card to Garrett). Sweeps:
`/mnt/project-files/regatta-flywheel/step5/fuel/`.

Back to loop step 1 for Time: the fun sign-off of the new ramp is a person's (Headlong for the Rhino, Spearline for the
Swordfish). Garrett chose "Time multiplies slice fuel" on the card (2026-11-18 21:39 UTC), as built.

### The AIs retrained with the tank (2026-11-18, Plan step: time-arcade)

- **Refuel gene** (`RaceAIRefuelGenesSO`, `Resources/RaceAIRefuelGenes`; `GateRaceController.RefuelDetour`): under a fuel
  threshold, turn toward the nearest live, unshielded prism between the vessel and its gate if the detour costs at most
  a slack. Searched with each drive by `time_ai_search.py` (`refuel_row`). The search left it OFF for both hulls: the
  shipped Rhino drive plus the gene at slack 30 / 47 / 120 was 50-60% slower on Headlong (confirm, seeds 11-12), and
  the Swordfish's winner sits at slack 0.3. Both rows ship at 0. Fuel comes from flying the line, which already ploughs
  or slices plenty.
- **Rhino, Headlong**: 36 candidates (I1-I4 and I1/I4 from Time 5), none beat the shipped drive with the tank in place;
  `RhinoAIBoostPolicy_Headlong` is unchanged.
- **Swordfish, Spearline**: its own policy at last (`SwordfishAIBoostPolicy`; the prefab named the Rhino's), and its own
  `_Spearline` drive from the search (crystal detour 168.7 in `Resources/RaceAICrystalGenes`, lead 2.82 at nose
  8.28/s). Confirm on fresh courses (seeds 101-103), every race finished: I1 174 -> 147 s, I2 170 -> 147, I3 167 -> 154,
  I4 215 -> 152. The ladder is now flat (I1-I4 within 5%); the I1-I4 fun sign-off should look at that.
- **Regatta variants**: copying each hull's home drive into Regatta was measured and rejected. The Rhino with Headlong's
  drive went 70.8 / 66.8 -> 90.6 / 84.6 s (card / L5), the Swordfish with its Spearline drive 75.1 / 69.8. So
  `RhinoAIBoostPolicy_Regatta` is unchanged and `SwordfishAIBoostPolicy_Regatta` holds what the Swordfish flew there
  before. Fixed-frame judge, shuffle set, solo, seeds 1-5, geo mean of per-course means:

| Vessel | Card s | x five | L5 s | x five |
|---|---|---|---|---|
| Rhino (unchanged) | 70.8 | 0.96 | 66.8 | 1.03 |
| Swordfish (own policy, crystal gene) | 72.2 | 0.98 | 65.5 | 1.01 |

The tank numbers did not need a re-fit. Data: `/mnt/project-files/regatta-flywheel/step5/fuel/retrain/` and
`/mnt/project-files/regatta-flywheel/train/{Rhino,Swordfish}/fuel1*/`.

## TigerMoth

Flagged 2026-11-18 after the Drop recharge curve. Garrett asked for a cheaper Drop with the cruise kept ("do b but try to
keep its cruise speed where it is and decrease the cooldown on the drop more"). Measured on the shuffle set, solo, 3
seeds, against the polished five (card mean 71.2 s, L5 66.8 s):

| Drop recharge vs design | Card s | L5 s | Card / five | L5 / five |
|---|---|---|---|---|
| 2x cheaper (10 s, shipped #2207) | 152.4 | 122.1 | 2.14 | 1.83 |
| 2.5x (8 s) | 152.5 | 121.9 | 2.14 | 1.82 |
| 3x (6.7 s) | 152.4 | 121.9 | 2.14 | 1.82 |
| 4x (5 s) | 152.4 | 121.9 | 2.14 | 1.82 |
| 5x (4 s) | 152.4 | 122.1 | 2.14 | 1.83 |

The curve is flat past 2x, so the knee is the shipped 2x and nothing is pushed further. What limits the hull is the
Drop's geometry. It falls along the belly, so every drop costs a 90-degree turn to put the ring abeam and a roll to put
the belly on it. A pilot (the autopilot's `RegattaTigerMothDrop`) gets one drop per ring that way, and flies the rest at
67 u/s. That is the same 90-degree constraint Garrett names for the Serpent's wall. Raw races:
`/mnt/project-files/regatta-flywheel/step5/tigermoth/drop-knee/`.

## Jacks

Flagged 2026-11-19. Before this the Jacks had no Regatta AI at all: off its own game's coach an AI Jacks keeps its
herding rhythm and crawls at 50 u/s (Ref1: 4 of 24 gates in 600 s). `RegattaJacksCoach` (Regatta's controller) now runs
Decoy Run's door cycle on every Regatta ring (`DecoyRunCourse.PlanCycle`, the same planner the Jacks race their Time game
with): roll the board onto the ring, herd, lay the decoy past it, take off behind it, travel through. Measured on the
shuffle set, solo, 3 seeds, geometric mean of per-course means (`regatta_judge.balance_score`):

| | Card s | L5 s | Card / five | L5 / five |
|---|---|---|---|---|
| Coach, shipped kit | 158.3 | 153.6 | 2.15 | 2.37 |
| Coach, jack run x1.375 (`dogSpeed` 520 -> 715, Ref1-Ref4 only) | 118 (refs) | - | ~1.6 | - |

What limits the hull is the cycle, not the AI: every ring costs a herd (the jacks' run, both swaps) and a travel, and the
take-off is a teleport that threads nothing (`DECOY_RUN.md` 1). Regatta's legs (461-858 u) are longer than Decoy Run's
(300-560 u), so the run is the cost. No Time parameter acts on it. Raw races:
`/mnt/project-files/regatta-flywheel/train/Jacks/regatta/`.

## Not flagged (levered into the field instead)

- Scarab: Time throttle curve x0.625 -> x0.9375 reshaped to x0.95 -> x1.0 (card 111.2 -> 73.5 s, L5 78.5 -> 64.0 s).
- Sparrow: afterburner (Time BoostSpeedMultiplier) x1 -> x2.25 reshaped to x2 -> x2.75 (card 121.5 -> 74.3 s, L5
  85.2 -> 66.9 s).

## Which AI each hull flies in Regatta (audit, 2026-11-18)

A hull can look like a balance outlier because Regatta flies an AI that was never trained. The Dolphin's two-line racing
(single-seed spread 6-10%) came from exactly that: Regatta resolved `DolphinAIBoostPolicy` (the untrained defaults), not
the drive trained in Switchback. `AIBoostPolicySO.ForMode` loads `Resources/AIBoostPolicies/<asset>_Regatta` when it
exists, otherwise the base asset the prefab wires.

| Hull | Regatta flies | Trained in its Time game | Verdict |
|---|---|---|---|
| Dolphin | `_Regatta` = the Switchback drive (from this change) | Switchback | Fixed; Drift Charge re-fit to x1.25 -> x2.76 |
| Sparrow | base, defaults | Breakwater (#1846, model-only search) | Untrained: next to fix, then re-fit its afterburner |
| Rhino | `_Regatta` = base + cornerSpeed 333 | Headlong | Partly trained; the Headlong genes predate the #2233 fuel tank |
| Swordfish | the Rhino's `_Regatta` (shared asset) | Spearline flies the Rhino's Headlong genes | Never trained for the Swordfish |
| Scarab | base, trained (#2087 wrote it) | Scarab Rally | OK |
| Thresher | `_Regatta` = the Thornline drive | Thornline | OK |
| Squirrel | base, defaults | none (Skim Race trains `SkimRaceAIConfig`) | No trained boost policy (loop step 3) |
| BrittleStar | `_Regatta` = its Speed Mode policy trained in Brittle Star Time with the coach off (2026-11-18) | Brittle Star Time (`time_ai_search.py --hull BrittleStarDrive`) | Fixed; Speed Mode back to x6 -> x8 (card 71.3 s, L5 63.0 s, 15 seeds) |
| Urchin | rail autopilot | Skein genome is Skein-only | Not a boost policy; check separately |
| Manta, Serpent, TigerMoth | - | - | Flagged above; revisit with the redesigns |
| Jacks | `RegattaJacksCoach` (Decoy Run's door planner, 2026-11-19) | Decoy Run's coach is that course's own; the planner is shared | Flagged above (2.15x) |
