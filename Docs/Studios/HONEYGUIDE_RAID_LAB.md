# Ratel Raid Lab

A browser lab (`/labmaker`) that lets a person play the **raid** proposed for the Ratel's seat of the Honeyguide, and
measures what it does to the seat's rhythm. Page: `Docs/Studios/HoneyguideRaidLab.html`. Live artifact:
https://claude.ai/artifact/5ZGDVoET4mreTbvQ1YHrMo (private to Garrett until shared from its Share menu; its own decision log).

**The question** (Garrett, 2026-10-29): the Ratel "is a fun puzzle but it needs something to break up the repetition of
pure pattern building ... you can't escape the time pressure of fiddling with your next three moves." Then, on the
proposal (`Docs/Studios/ONE_THUMB_SEAT_ARCS.md` §3): "Ratel: lets have the ap cap go to 6. lets do the 3 ability pick as
well. those will go in for sure, but show me the raid in an artifact."

**Who else works here:** the Honeyguide and Ratel are Garrett's crew vessel
(`Assets/_Scripts/Controller/Vessel/Crew/HONEYGUIDE.md`); its lab is the Honeyguide Lab
(`Docs/Studios/HoneyguideLab.html`, https://claude.ai/artifact/4B7oZwb5aYxFJR7XKyWx7v). This lab changes neither. It
copies the lab's Dynamo and adds the raid around it.

## What the page is

- **The build is the real Dynamo.** The `dynamo-core` script block is copied line for line from the Honeyguide Lab's
  round 7 working copy (2026-10-29), with the round 7 Breath that Garrett decided: AP bank 6 (3 a spin, what is not
  spent is kept) and a pick of three abilities every 4 patterns, the clock stopped while the Ratel picks. The pattern
  cards, abilities, port engines and palette are the lab's tables, also copied. The numbers are
  `HoneyguideConfig.asset`'s plus the Breath. Only the cards that change the Dynamo itself fire (below).
- **A hive is found.** A stand-in Honeyguide flies a strip at the top. About every 40 s (seeded) it finds a hive (in the
  game: a hostile surface or volume, `PrismscapeTopology.DimensionOf`, that a Peck marks). The **Hive** card lights
  beside the three pattern cards for 20 s. The Ratel taps it when it chooses.
- **The raid.** The spin clock stops. The 37 cells fill with the hive's comb: patches of Ruby and Gold (hostile), a
  little of our own Jade (cannot be broken), holes, shielded cells (white rim, two passes), danger cells (red, they
  sting) and super-shielded cells (steel, never chained, never broken). The thumb drags a chain through touching cells
  of one colour, and letting go breaks every plain prism in it.
  - A chain that breaks 3 or more pays one **honey**: a free prism of its domain for the build.
  - Every 4 broken bank +1 AP, up to the bank.
  - A chain that breaks 6 or more sends a charge of its domain to the battery.
  - Dragging into a danger cell stings: the chain breaks and costs 1 AP.
  - The raid ends when the comb has no chain left, after 12 s, or when the stand-in pilot leaves the hive (it holds
    85% of raids).
- **The feast.** The clock stays stopped (up to 10 s). The honey rides the cursor until placed, and the Ratel picks one
  of three: a new ability, a new port engine, or a card swapped. Then the spin restarts.
- **The AI Ratel** plays the build with the Honeyguide Lab's planner (`aiPlan`, hard or easy) and raids with a
  longest-chain search. The rookie picks among the four best chains, cuts them short and sometimes slides into a
  danger cell. Its policy for when to raid is a setting: finish the pattern in hand (default) or go at once.
- **Every number** of the hive, raid and feast is a proposal, on a slider, with the candidate underlined when changed.
- **The decision log** is shared (the artifact's `decisions` collection), with a browser fallback.
- **Test surface:** `window.__lab` (`manualClock`, `tick`, `reset`, `score`, `runBatch`, `state`, `SHIPPED`, `SPEC`,
  `P`, `PLATFORM`, plus `input`, `hive`, `bestChains`, `selfTest`).

## Measured

`window.__lab.runBatch({seeds:[1,2,3,4,5,6], seconds:180, skills:['skilled','rookie']})`, candidate settings, the same
seeds in every row (run headless in Chromium, about 2.5 s):

| Rules | Ratel | Patterns /min | AP wasted /min | Charges /min | Hostile broken /min | Raids /min | Honey /min | Stings /min | Rest share | Clock stopped | Raiding | Actions /s | Arc (15 s CV) |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Round 6 (bank 3, random draw) | skilled | 4.56 | 0 | 4.72 | 0 | 0 | 0 | 0 | 12% | 9% | 0% | 0.21 | 0.19 |
| Round 6 | rookie | 3.00 | 0 | 3.17 | 0 | 0 | 0 | 0 | 11% | 6% | 0% | 0.21 | 0.16 |
| Breath (bank 6, pick of three) | skilled | 5.22 | 0 | 5.22 | 0 | 0 | 0 | 0 | 8% | 5% | 0% | 0.28 | 0.27 |
| Breath | rookie | 2.95 | 0 | 2.95 | 0 | 0 | 0 | 0 | 10% | 4% | 0% | 0.24 | 0.20 |
| **Breath + Raid** | skilled | **6.83** | 0.28 | **8.67** | **28.9** | 1.33 | 2.78 | 0 | 12% | 10% | 15% | 0.99 | **0.87** |
| **Breath + Raid** | rookie | 3.50 | 0 | 4.78 | 13.2 | 1.17 | 1.34 | 0.06 | 13% | 9% | 19% | 0.59 | 0.63 |
| Idle Ratel (control) | either | 0 | 11.67 | 0 | 0 | 0 | 0 | 0 | 100% | 0% | 0% | 0 | 0 |

- **Rest share**: seconds holding AP with no move for 2 s, plus the seconds the clock is stopped (picks, feasts).
- **Arc**: actions a second in 15 s windows (one spin each); the spread of the windows over their mean. 0 means every
  spin looks the same; it is the number for "repetitive".
- **The noise band** (seeds 1-3 against 4-6, skilled): patterns /min ±0.6, arc ±0.1, broken /min ±2. So Breath's
  patterns gain (+0.7) is at the edge of the noise, and the raid's (+2.3) and its arc (+0.7) are well outside it.
- **The idle control** reads 0 everywhere except AP wasted (11.67 /min) and rest (100%), which are those columns'
  ceilings.

**What it says:**
- **The raid turns the flat line into an arc.** Round 6's arc is 0.19: every spin is the same burst then wait. The raid
  takes it to 0.87 (skilled) and 0.63 (rookie). The charts on the Scorecard tab show it: round 6 is a comb of equal
  spikes, the candidate has a tall peak each raid.
- **The raid feeds the build instead of taking time from it.** Patterns a minute rise 4.56 → 6.83 (+50%) with the clock
  stopped for 25% of the match (raids and feasts), because the honey (2.8 free prisms a minute) and the banked AP finish
  patterns. Charges rise 4.72 → 8.67, so the Honeyguide has more slips.
- **The squad gets something it did not have**: about 29 hostile prisms broken a minute by the skilled Ratel, 13 by the
  rookie. Today the Ratel breaks none.
- **Breath alone is a small change for the AI** (patterns +14% skilled, none for the rookie, arc 0.19 → 0.27). The AI
  never wastes AP under either bank, so this scorecard cannot measure the pressure Garrett named. Only a person can.

## Checked

- `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/HoneyguideRaidLab.html`: **PASS** at desktop 1600×900 and
  an iPhone 13. No console errors; the desktop fits the window; the phone does not scroll sideways; the hooks are
  present and do not throw; every `SPEC` key is in `SHIPPED` and inside its range; the manual clock advances;
  `runBatch()` is deterministic within a page and across two loads; the stage is not blank.
- Screenshots read at both sizes for the build, the pick of three, a raid with a chain held, and the feast. Fixed from
  them: the AI note covered the board's bottom row, the pattern shapes overlapped their card names, the toast covered
  the pilot strip, and the feast named a port engine by its index.
- `window.__lab.selfTest()`: 7 of 7, each a planted case. A flat seat reads arc 0 and an on/off seat arc 1. A
  super-shielded cell and a cell of the other domain are refused. A danger cell stings, breaks the chain and takes the
  AP. A shield takes the first pass while the plain prism beside it breaks. `runBatch` is deterministic.

## Found

1. **The first arc measure could not see the problem.** Over 5 s bins, round 6 scored 1.34 and the raid 1.30, because
   one spin is itself a burst then a wait. Measured per spin (15 s windows), the burst repeats inside every window, so
   a board that only builds reads flat (0.19) and the raid's peaks show (0.87). The page uses the 15 s measure.
2. **The AI does not feel time pressure.** It spends every AP under a bank of 3 or 6, so AP wasted is 0 in every AI row.
   Whether the bank of 6 relieves "fiddling with your next three moves" is a question for a phone in Garrett's hand.
3. **The AI picks at once, so its valleys are short.** The clock is stopped 5-10% of a match for the AI. The old reveal
   held 3.5 s; a person reading three abilities, or a feast, will hold it longer.
4. **A skilled raid clears the comb in about 7 s** (15% of the match raiding over 1.33 raids a minute), well inside the
   12 s cap. The cap matters for a person and for combs with many shields or stings.
5. **No bug in the copied Dynamo** turned up. The copy runs the lab's core unchanged; the raid sits outside it and
   writes the Dynamo only through `D.boardEffect` ('charge'), `st.free` (honey) and `st.ap` (banked AP, capped at the
   bank).

## Decision needed (designer)

- **Keep the raid, change it, or drop it**, after playing it as the Ratel on a phone (tap **You**, then **Find a
  hive**). Record it in the page's Decisions tab.
- **Chains**: drag through one colour (this page), or another gesture?
- **The pay**: honey from a chain of 3, +1 AP every 4, a charge from 6. Too rich? The raid lifts patterns 50%.
- **Stings**: break the chain and cost 1 AP, or something else?
- **The feast's three**: a new ability, new ports or a card swap. Right three?
- **Who decides when to raid**: the Ratel (this page), or does the pilot's arrival start it?

## Not modelled

- The Honeyguide. The pilot is a stand-in: it spends a charge about every 7 s, finds a hive on a seeded clock and holds
  it 85% of the time. Whether a real pilot finds and holds hives, and how a hive is chosen in the world, is not tested.
- The pilot-side card effects. Only the cards that change the Dynamo fire: Amanita Furnace, Big Red Button, Dragon
  Saddle, Zero Point Amplifier (its drain only), Triplesynth, Universal Translator, Anode Ray, 5D Printer, Square-Peg
  Hammer, Reactor Cilia, Chrono-Compressor and Vorticity Module. The 13 trigger upgrades and the passives do nothing.
- The comb is a seeded stand-in for a projection of real prisms, not a capture of a real structure.
- What the broken prisms are worth to the squad: counted, not scored.
- Sound, haptics, network play, and what the Honeyguide sees while the Ratel raids.
- The AI is a heuristic. A person plays differently; the scorecard shows the shape, and a phone decides.
- Lab §1.5's four element games: this lab asks one question (the raid, a Space-kind destruction game) and has no
  Time, Mass or Charge games.

## Next

Garrett plays it on a phone and records the decisions. If the raid stays, round 2 tunes the pay and the stings from
that log. Then it moves into the Honeyguide Lab as a round, and through `/artifact-to-unity` into `DynamoCore.cs`
and `HoneyguideDynamoView.cs`. In Unity, the hive is a real prismscape the Peck marks.
