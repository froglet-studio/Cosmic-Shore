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
| Meerkat | 294.3 | 256.7 | 4.0x / 4.0x slower | A 60 u/s cruise; its only speed past that is the wormhole ride (2.5x the entry speed at the throat, out at the speed it went in). Most of each Regatta leg is cruise | Time is the ride's rush (x1 at rest to x2 at level 10). At the 37.5% lever cap (x1.375 -> x2.75) Ref1-Ref4 go 285 -> 248 s (3.4x); doubling it (x2 -> x4) gives 224 s (3.0x). The trained wormhole autopilot is already at its optimum (Burrow Run search flat). Flagged |
| Orca | 386 | 359 (3 of 8 courses DNF at 400 s) | 5.3x / 5.5x slower | A 21 u/s cruise and a x5 burn (about 106 u/s) on a 3 s tank refilling 0.25 to 0.35 a second, with a burning hull's wide turn | Time is the tank's refill (0.25 at rest, 0.35 at level 10). Its ceiling, an endless tank with an AI that burns on every leg (any distance, nose within 45 degrees): 185 s against 366 s on Ref2, still about 2.5x. No Time value or burn rule reaches the field |
| Wrasse (pilot seat) | 144.2 | 124.0 | 2.0x / 1.9x slower | The Grouper hull cruises at 60 u/s and its only pace is the saved boosts (x5 for 1.5 s each) that the kite's Time loops bank; the AI pilot already spends one whenever the next ring is 180 u off and ahead. In Regatta it ran about 900 prism contacts a race | Time banks the boosts (`timeBankScale` x1 at rest, x2 at level 10, #2225). x2 at rest: 133.3 s. Boosts last 2x: 133.3 s. The ceiling, 4x the bank, 2x the length and 4x the cap together: 120.4 s (1.65x). The kite AI valuing Time loops 10x (with twice the race Time motes): 136.6 s. Its Wrasse Run nose pair (run1, 4.37/s, lead 0.69) in Regatta: 142.2 s |
| Angler | 323 | 312 | 4.4x / 4.8x slower | A 40 u/s cruise (the fleet's slowest); its pace is the leapfrog: throw a group of its school ahead at 150 u/s (x2 with Time), then dash 75% of the way to it in 0.35 s. Regatta has no prism mass clusters on the line to paint, so every throw is a plain sling along the nose | AI trained in Lantern Run (loop step 3, 2026-11-19): Regatta 358 -> 323 s card, 349 -> 312 s L5. Its pace levers at the 37.5% cap together (throw speed 150 -> 206, Time's throw scaling x2 -> x2.75, dash 0.35 -> 0.255 s): 308 / 301 s, about 5% more. Flagged |

| Honeyguide | 374 | 363 | 5.1x / 5.6x slower | A 60 u/s cruise; its pace is the Slip, a 480 u/s zip down a prism trail in a colour its battery holds (Ratel patterns, about one charge every 15 s). Solo in Regatta there is almost no trail to ride (inferred: slip levers barely move it), so it flies the course near cruise | Its Slip Line coach was searched three times (2026-11-17, no win). Its Slip Line nose (6.58/s) in Regatta: 369 / 355 s, noise. Its Time levers at the 37.5% cap together (zip 480 -> 660 u/s, slip reach 400 -> 550 u, Time's zip scaling x1.5 -> x2.06): 358 / 351 s, about 4%. Flagged |

## Angler

Measured 2026-11-19 (thread "Train and seat Angler and Honeyguide"): the shipped shuffle (8 courses), solo, real-game judge
(`regatta_judge.py`), seeds 1-5 (lever test 1-3), a 900 s limit, geometric mean of the per-course means, built from
bleeding-edge 05da4be58 with the #2238 boot break reverted (the fix is #2243). 0 DNF. Raw:
`/mnt/project-files/regatta-flywheel/train/Angler/regatta/`.

Its AI was never the limit. Trained in its own Time game (Lantern Run, `time_ai_search.py --hull Angler`, run1), the
leapfrog drive got 30-40% faster on every rung there (I1 201 -> 131 s, I4 240 -> 154 s, fresh seeds, 0 DNF) and 10% faster
in Regatta (shipped AI 358 / 349 s, trained 323 / 312 s). Its own pace levers at the cap together add about 5%. Lantern
Run itself takes 130-150 s for a course its doc estimated at 70-90 s.

The pace that is missing is the hull between throws: a 40 u/s cruise, a sling that only carries it as far as the group
flew, and nothing on a Regatta course to paint, so the throw is never aimed past a ring the way Lantern Run's clusters
let it be. A redesign that keeps "paint your path, zoom along it" needs either paintable mass the course or the hull
itself provides, or a dash that carries real distance. Garrett's call; the Angler stays off the card
(`author_regatta_assets.JUDGE_ONLY_HULLS`) meanwhile.


## Honeyguide

Measured 2026-11-19 (thread "Train and seat Angler and Honeyguide"), the same way as the Angler above: the shipped
shuffle, solo, seeds 1-5 (lever test 1-3), a 900 s limit, 0 DNF. Raw: `/mnt/project-files/regatta-flywheel/train/Honeyguide/`.

Not the AI. Slip Line's coach (the rail it rides, the race line between rides) is near its best: three searches
(`train/Honeyguide/NOTE.md`) found no confirmed win, and its trained nose rate changes nothing in Regatta. The levers that
make a slip faster or longer barely move the time either, which says the Honeyguide rarely slips in a solo Regatta (inferred from the lever test; slips were not counted): a slip
needs a prism TRAIL in a colour the battery holds, and alone on a Regatta course there is none on the line (Slip Line lays
its own rails for exactly this reason). So it cruises at 60 u/s. A redesign that keeps the two seats (the Ratel charges,
the pilot spends) needs trails it can count on outside Slip Line (its own, a rival's, the course's), or a pace term the
pilot earns without one. Garrett's call; the Honeyguide stays off the card meanwhile.

## Wrasse (pilot seat)

Measured 2026-11-19 (thread "Seat Shrike and Wrasse in Regatta"): the shipped shuffle (8 courses), solo, real-game judge
(`regatta_judge.py`), seeds 1-2, geometric mean of the per-course means, built from bleeding-edge at f6d85ce8d (the
#2238 boot break was on trunk then). The Wrasse finishes every course (0 DNF) at 2x the polished five. Raw:
`/mnt/project-files/regatta-flywheel/seat-sw/wrasse/`.

Not the AI's spending: the pilot spends a boost whenever the next ring is 180 u off and roughly ahead, and quadrupling
the bank with twice the boost length only reached 1.65x. The pace that is missing is the hull's 60 u/s cruise between
boosts and the rate at which the kite closes Time loops on a moving board. A redesign that keeps the crew (the kite
earns, the pilot spends) needs a pace term the pilot's own flying earns, or a much richer Time loop. Garrett's call;
the Wrasse stays off the card (`author_regatta_assets.JUDGE_ONLY_HULLS`) meanwhile.

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

## Orca

Flagged 2026-11-19 when it was first judged in Regatta (thread "Train and seat Goby and Orca in Regatta"). It had never
been raced there. The shipped AI helm burns by `OrcaCrew.HelmAIWantsBurn` (Breach's rule without its walls) and
finishes every course it is given time for.

Shipped shuffle (8 courses), solo, seeds 1-3, geometric mean of the per-course means, a DNF counted at 400 s, against
the polished five's median (73.5 s card, 64.7 s L5, `step5/noise/FIELD2.md`):

| Start | Orca s | x five | Finished |
|---|---|---|---|
| card | 386 | 5.3 | 7 of 8 courses (Ref4 DNF on every seed) |
| L5 | 359 | 5.5 | 4 of 8 (Ref4, Course2, Course4, Course6 DNF) |

The Orca's Time element is its boost tank's refill: 0.25 tank-seconds a second at rest, 0.35 at level 10, and the L5
Afterburner keeps refilling under a burn. The lever's ceiling was measured by making the tank endless (refill 50, tank
1000 s), so the AI burns on every leg it is lined up for (card start, seed 1, limit 800 s):

| Course | shipped s | endless tank s |
|---|---|---|
| Ref2 | 366 | 291 |
| Course5 | 380 | 301 |
| Ref3 | 387 | 328 |

That run kept the shipped burn rule (next target 250 u or more away, nose within 8 degrees), which Regatta's gates
rarely allow, so it understated the hull. Checked again the same night with the rule opened up (any distance, nose
within 45 degrees, no minimum tank; Ref2, card start, seed 1):

| Variant | Ref2 s | prism hits |
|---|---|---|
| shipped | 366 | about 1,300 |
| open burn rule, shipped tank | 508 | 716 |
| open burn rule, endless tank | 185 | 497 |

So the AI's burn rule does cost the Orca about half its pace once the tank is not the limit, but with an endless tank
and a helm that burns everywhere it is still about 2.5x the field (Ref2's field is about 73 s). On the shipped tank the
open rule is slower, because the tank empties and only refills while the burn is let go. Neither the Time parameter
nor a better pilot brings it in, so the flag stands. The pace is the hull: a 21 u/s cruise, a x5 burn to about 106 u/s, and a burning turn that widens with speed (`BreachCourse`'s
numbers: turn 44 + 0.055/u). The field averages 200-plus u/s over the same courses. A redesign would have to give the
Orca a race pace (a faster burn or cruise, or a Time ability that carries it), which is a kit change for every mode and
Garrett's call.

Its Time AI was not trained for Regatta: the open-rule, endless-tank run bounds what any burn policy can give at
about 2.5x. Once the hull is redesigned, its Regatta burn rule needs training (a coach like Breach's), since the
shipped rule's 250 u minimum rarely fires between Regatta's gates. Breach's coach already
finishes all 24 of its ladder races (BREACH.md 5.1). Raw: `/mnt/project-files/regatta-flywheel/train/Orca/`.

## Meerkat

Flagged 2026-11-19 (thread "Train and seat Meerkat and Jacks in Regatta"). Shipped shuffle, solo, seeds 1-2, the shipped
AI (the race controller's run1 genes plus the wormhole autopilot), against the polished five's median (73.5 s card,
64.7 s L5):

| | Card s | L5 s | Card / five | L5 / five |
|---|---|---|---|---|
| Shipped kit | 294.3 | 256.7 | 4.0 | 4.0 |
| Time rush at the lever cap, x1.375 -> x2.75 (Ref1-Ref4) | 248 | - | 3.4 | - |
| Time rush doubled, x2 -> x4 (Ref1-Ref4) | 224 | - | 3.0 | - |

Ref1-Ref4 alone run 285 s on the card, so the rush probes are compared with that. The AI is not the limit: a
search over the wormhole autopilot's own knobs (`time_ai_search.py --hull MeerkatDrive`: when to form a straight or a bent
tunnel, how long to shape it) in Burrow Run I1-I4 plus I1 at Time 5 and a wide course found nothing better than the
shipped values (best loss 2.252 against 2.25 for the shipped AI, 12 candidates). What limits the hull is the 60 u/s cruise. The
ride is the only speed past it, and it comes out at the speed it went in, so a Regatta leg is mostly cruise. Raw races:
`/mnt/project-files/regatta-flywheel/train/Meerkat/`.

## Not flagged (levered into the field instead)

- Scarab: Time throttle curve x0.625 -> x0.9375 reshaped to x0.95 -> x1.0 (card 111.2 -> 73.5 s, L5 78.5 -> 64.0 s).
- Sparrow: afterburner (Time BoostSpeedMultiplier) x1 -> x2.25 reshaped to x2 -> x2.75 (card 121.5 -> 74.3 s, L5
  85.2 -> 66.9 s). 2026-11-19: that reshape had been making up for an untrained AI; with the Breakwater drive in
  Regatta (`SparrowAIBoostPolicy_Regatta`) the afterburner is re-fit to x1.75 -> x2.15 (card 71.2 s, L5 65.0 s).

- Goby: Time dart pace (dartPace) x1.0 -> x2.2 reshaped to x1.25 -> x1.95, the level-5 value unchanged (card 83.3 ->
  78.1 s, L5 62.6 s, 15 seeds), after Regatta got its own Goby dart coach (REGATTA.md 6.6).

## Which AI each hull flies in Regatta (audit, 2026-11-18)

A hull can look like a balance outlier because Regatta flies an AI that was never trained. The Dolphin's two-line racing
(single-seed spread 6-10%) came from exactly that: Regatta resolved `DolphinAIBoostPolicy` (the untrained defaults), not
the drive trained in Switchback. `AIBoostPolicySO.ForMode` loads `Resources/AIBoostPolicies/<asset>_Regatta` when it
exists, otherwise the base asset the prefab wires.

| Hull | Regatta flies | Trained in its Time game | Verdict |
|---|---|---|---|
| Dolphin | `_Regatta` = the Switchback drive (from this change) | Switchback | Fixed; Drift Charge re-fit to x1.25 -> x2.76 |
| Sparrow | `_Regatta` = the Breakwater drive (2026-11-19) | Breakwater (#1846; holds up in the real game) | Fixed; afterburner re-fit to x1.75 -> x2.15 |
| Rhino | `_Regatta` = base + cornerSpeed 333 | Headlong | Partly trained; the Headlong genes predate the #2233 fuel tank |
| Swordfish | the Rhino's `_Regatta` (shared asset) | Spearline flies the Rhino's Headlong genes | Never trained for the Swordfish |
| Scarab | base, trained (#2087 wrote it) | Scarab Rally | OK |
| Thresher | `_Regatta` = the Thornline drive | Thornline | OK |
| Squirrel | base, defaults | none (Skim Race trains `SkimRaceAIConfig`) | No trained boost policy (loop step 3) |
| BrittleStar | `_Regatta` = its Speed Mode policy trained in Brittle Star Time with the coach off (2026-11-18) | Brittle Star Time (`time_ai_search.py --hull BrittleStarDrive`) | Fixed; Speed Mode back to x6 -> x8 (card 71.3 s, L5 63.0 s, 15 seeds) |
| Urchin | rail autopilot | Skein genome is Skein-only | Not a boost policy; check separately |
| Manta, Serpent, TigerMoth | - | - | Flagged above; revisit with the redesigns |
| Jacks | `RegattaJacksCoach` (Decoy Run's door planner, 2026-11-19) | Decoy Run's coach is that course's own; the planner is shared | Flagged above (2.15x) |
| Meerkat | the gate race's steering + its own wormhole autopilot | Burrow Run (run1 controller genes; autopilot searched 2026-11-19, shipped values best) | Flagged above (4.0x) |

## Brood Rush (Mass-Space): Shrike, too strong (2026-11-19)

Brood Rush judge (`brood_judge.py`, branch `claude/project-thread-sfixg0`), 3v3 against the Brood Rush thread's tuned
trail-volume field: the Shrike's index is **3.15** (bar 0.9-1.1), and **1.02 with no nests at all** (`nestCooldown`
9999). The nest is the whole edge. A nest is a cup of super-shielded blocks, and since 2026-11-18 the net-control books
count a super-shield at 13.5x its box (`VolumeLedger.ShieldExtra`), so one 12-block nest of 4.5 u cubes books about
14.8k against a ~33k field-mean pilot for the whole match.

| Lever (3 seeds unless noted) | Index |
|---|---|
| shipped | 3.15 |
| `nestBlocks` 12 -> 6 | 2.05 |
| `nestCooldown` 8 -> 24 s | 2.29 (the AI's nest rate is set by its docking, not the cooldown) |
| `nestBlockSize` 4.5 -> 2.5 u | 1.21 |
| `nestBlockSize` 4.5 -> 2.2 u (12-hull field, 8 rounds) | 0.90 |

The block-size knee (about 2.2-2.3 u, about 0.13x the nest's volume) breaks the Shrike's own Mass game:
`author_thornbush_assets.py --check` fails, because Thornbush scores `VolumeRemaining` (box volume, no shield extra), so
at 2.3 u one nest (146) is worth less than four times a nest's share of the trail (510). Thornbush stops being a nest
game, and its comeback rate goes dead. Brood Rush counts nests at 13.5x, Thornbush at 1x, and no Shrike lever fits both,
so the Shrike is **flagged for Brood Rush**. Its Regatta seat is unaffected. Garrett has the choice: the flag, the
shrink with a Thornbush rework, or nests that are plain-shielded (4.5x). Raw data: `regatta-flywheel/seat-sw/brood/`.

The Thresher (index 2.95, from its Mass Mode ball trail) came in on its own Mass lever: `massTrailScale` 1 -> 2 became
0.45 -> 0.9, giving 1.00 in the same field. Its own games pass their checks.
