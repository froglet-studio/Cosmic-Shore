# Spearline fun pass (2026-11-16, round 5)

Yash asked for every vessel's Time minigame to be made super fun, building on the earlier Time-game rounds
(`Docs/AI_SYSTEM/TIME_GAME_FUN_SCORES.md`). This is the Swordfish's: **Spearline** (`GameModes.Spearline = 96`),
upgraded in place. The mode doc is `Assets/_Scripts/Controller/Arcade/SPEARLINE.md`; this file is the spec and the
record of how it was decided and what the numbers threw out.

## 1. How it was decided

A workflow ran five readers (the Swordfish kit, the earlier upgrades, the fun bar and recorded play, the `/arcadegame`
recipe, the course building blocks), three designs (verb first, spectacle first, rivals and fairness first) and three
judges (player, engineer, rules and AI). The player and rules judges picked the verb-first design ("cadence": sided
Danger guards before the rings, same-side strokes, Flurry earned and lost); the engineer picked spectacle first. The
synthesis took cadence as the spine.

**Building it, the spine did not survive the geometry.** Read against the shipped code:

- **The bill cuts at the crossing, beside the hull, not ahead of it.** A stroke lays the bill flat to one side of the
  nose, so it cuts a line out of the crossing point as the hull goes through. It cannot clear a guard out of the
  hull's lane ahead, so "sided guards" measured only their own model. Dropped.
- **Quickdraw never binds.** The swipe recovery starts when a trigger is released (`ShieldSwipeActionExecutor`); a
  pilot holds the stroke through the pane. Same-side cadence would need re-presses inside one 2 u pane crossing.
  Dropped, with "Time is speed" (no metric could pass honestly). A kit proposal goes to Garrett instead (SPEARLINE.md §6).
- **The shipped hole rule could not be met in one pass.** With the bill's real width (capsule 4.8 u) the hull plus one
  stroke reached at most 42% of the judged 22 u disc, under every rung's need (45 / 60 / 60 / 75%), and I2-I4 asked
  for "a stroke each side", which one crossing cannot make. The hull alone read 17-37% by grid alignment alone.
- **A Blue AI never swings at Blue seals** (a judge's finding) is not a bug: Blue is the neutral sentinel and never a
  racer's team (`Domains.cs`).

What survived from the panel: the mouth ladder (96 / 84 / 74 / 66), fewer sealed rings so the ramp breathes, the
worn seal (no soft lock, the same for humans and AIs), releasing dead seal prisms, the AI's energize fix, an AI that
reads the seal, the 3D feedback and the empty sound slots.

## 2. Today's fun, before the pass

| Verb | Flow | Ladder | Feedback | Rivals | Fairness | Fun | Floor |
|---|---|---|---|---|---|---|---|
| 5 | 3 | 3 | 3 | 1 | 2 | **2.8** | 1 Rivals |

- Verb: the swing is the bill's own verb, but no single pass opened a seal at I2-I4.
- Flow: every I2-I4 seal (7 of 8 rings) needed a second pass, and a closed pass re-latched the original approach.
- Ladder: one 25% mouth step, and I1 was the heaviest rung in prisms.
- Feedback: the hint and the shared beats only; no sound of its own.
- Rivals: the AI's sword hand energized (stood the bill up) in front of every seal, and could not open one in a pass.
- Fairness: grid luck decided the hole; a shielded seal looped a pilot.

## 3. The design built

1. **The connected hole** (`SpearlineSeals.HoleSize`): a crossing counts through the run of gone prisms it sits in, at
   least 8. The hull alone makes 3-7, the hull plus a stroke into the pane 8-19, at bill widths 2.4 and 5 u.
2. **Armour** (I2+): a shielded sector, 120 / 180 / 240 degrees, facing a hash of the ring. It cracks and stands. Read
   it, cross the plain face, swing into it.
3. **The worn seal**: every closed pass lowers that pilot's need there by 2; the fifth pass counts anything.
4. **Mouths and seals**: 96 / 84 / 74 / 66; 2 / 3 / 3 / 4 sealed rings a lap, the gentlest; 5 laps (40 rings).
5. **The AI**: `SpearlineSeals.Read` picks the crossing and stroke; `AILaneOffset` steers there; the sword hand's new
   `Coach` seam holds the stroke and suppresses its energize stance near a pane. Miss-read 30 / 15 / 5 / 0%, lane error
   10 / 7 / 4 / 2 u by difficulty.
6. **Feedback**: the next armoured seal's plain face lifted, the hole flashing on a first-pass thread, toasts 400-402,
   three empty FMOD slots.
7. **Corpse release**: a dead seal prism goes back to its pool before its replacement is laid.

## 4. Measured (a model: the shipped C# lays and plays the seals; Python times them)

`bash Tools/Build/spearline_harness/run.sh --self-test`: 11 tests pass; 8 negative controls fire (need 6 lets the hull
through; need 11 defeats a thin stroke; no wear can trap; armour that breaks; Headlong's mouth step; the hardest corners
sealed; armour round the whole I2 pane; a read blind to armour).

`py -3 Tools/Build/spearline_race_model.py` (40 courses a rung; human = k x model, k = 2.76 fitted on the one recorded
Headlong I1 race; a closed pass costs a 3.1 s loop in the model):

| | Hull alone opens on pass 1 | Reading saves (vs best fixed habit) | Human reader | Best fixed habit | AI Easy / Med / Hard / Legend (model s) |
|---|---|---|---|---|---|
| I1 | 0% | 0% | 85 s | 85 s | 30.9 / 30.9 / 30.9 / 30.9 |
| I2 | 0% | 36% | 88 s | 138 s | 40.9 / 32.3 / 31.9 / 31.9 |
| I3 | 0% | 37% | 96 s | 150 s | 52.2 / 67.0 / 34.7 / 34.6 |
| I4 | 0% | 57% | 109 s | 253 s | 75.1 / 65.5 / 40.0 / 39.4 |

- I1 is the intro by design: any stroke into the pane opens it, so there is nothing to read.
- The saving is almost all closed passes avoided: a fixed habit fails 0.8 / 1.5 / 2.9 seals a lap at I2-I4.
- Easy and Medium swap places at I3: each difficulty sits on its own seat's fixed draws (40 courses), so between
  neighbours the order is noise. Hard and Legendary race within 1% of the reader, and no AI needs more than 5 passes.
- The model's I4 bare circuit x k is 2.71 s a gate against the recorded 3.65; that race was Headlong's Time-burn
  spiral, which Spearline does not have. Read I4's 109 s as a floor.
- **What was dropped by the numbers**: 4 laps (69-71 s at I1-I2, under the 75 s bar); armour on every second I2 seal
  (reading saved 0.3% - nothing to read).

## 5. What only Unity can tell

Whether the bill really cuts the band the model says (`ModelStrokeReach` is read off the capsule, not measured), whether
the hull clips the pane before its transform crosses at 840 u/s (the hull's own prisms are counted as gone at the
crossing), whether the shields read from 900 u, whether the AI's stroke lands in time at full ramp, the feel, and the
sound. `Docs/UNITY_VERIFICATION_CHECKLIST.md` has the hand test.
