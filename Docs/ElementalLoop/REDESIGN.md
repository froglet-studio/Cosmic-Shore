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
| Orca | 386 | 359 (3 of 8 courses DNF at 400 s) | 5.3x / 5.5x slower | A 21 u/s cruise and a x5 burn (about 106 u/s) on a 3 s tank refilling 0.25 to 0.35 a second, with a burning hull's wide turn | Time is the tank's refill (0.25 at rest, 0.35 at level 10). Its ceiling, an endless tank with an AI that burns on every leg (any distance, nose within 45 degrees): 185 s against 366 s on Ref2, still about 2.5x. No Time value or burn rule reaches the field |

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
