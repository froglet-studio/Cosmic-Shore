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
| Rhino | 33.5 | 32.1 | 0.48x (twice as fast) | Ramp boost spools to 16.8x on straights | Time spool x0.25: card 45.4 s. Ramp top halved (8.4x): card 41.9 s, L5 41.5 s. Still about 0.6x |
| Swordfish | 38.6 | 37.5 | 0.56x | The Rhino's ramp (16.8x top); its Time element scales sword and swipe rate, not pace | Ramp top halved (8.4x): card 46.7 s, L5 46.5 s. About 0.68x |
| Manta (L5) | 86.1 (card, in the field without its -5 row) | 27.9 | 0.42x at L5 | Soar, its Time level-5 upgrade: a held boost that turns 60 s laps into ~20-30 s | Soar's BoostSpeedMultiplier (Time, acts only once Soar unlocks) at x0.375: L5 43.5 s, about 0.66x |
| TigerMoth | 151.9 | 120.8 | 2.2x / 1.8x slower | Pace is bounded by the Drop recharge cadence, not cruise speed | Time throttle slot, knee x2.0: card 104 s, L5 84 s (1.55x / 1.27x), and Waystation breaks (I4 2 of 3 DNF: it reaches tower rings before the Drop is back). Patch held: `step5/tigermoth/time-throttle-x1.9-2.1.patch` |

## Serpent

Garrett's direction (2026-11-18), verbatim: "serpent will probably need a way to turn its wall 90 degrees so it can
easily drop a wall and keep flying without having to do the 90 degree turn like the tiger moth. being constrained to a
plane in order to grow its boost charges is already challenge enough. we will still need a way to keep stop and seed
indistinguishable to an opponent. so we will need to reorient both, but that is hard because the decoy becomes the
supershielded prism at the center of the wall, so its current orientation made sense."

Garrett is doing the Serpent redesign in another project. Its kit changes land on bleeding-edge from there; re-judge it
on the shuffle set when they do.

## Not flagged (levered into the field instead)

- Scarab: Time throttle curve x0.625 -> x0.9375 reshaped to x0.95 -> x1.0 (card 111.2 -> 73.5 s, L5 78.5 -> 64.0 s).
- Sparrow: afterburner (Time BoostSpeedMultiplier) x1 -> x2.25 reshaped to x2 -> x2.75 (card 121.5 -> 74.3 s, L5
  85.2 -> 66.9 s).
