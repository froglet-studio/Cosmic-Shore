# Spearline - the Swordfish's Time race

> **Status.** Authored headless on 2026-11-15 (`claude/swordfish-game-kit`), for Garrett's ask to build every missing
> elemental game ("four games (one for each element is ideal) make all the missing games you can"), on the ids
> `Docs/ElementalLoop/GAME_KIT_ROSTER.md` reserved for it (row 6: `GameModes` 96, hint toast 171). The seal ladder is
> held by `SpearlineSealsTests` against a model of the hull and the bill. **Nothing here has been run in the Unity
> editor, and nobody has flown it.**

`GameModes.Spearline = 96` is the **Swordfish's Time card**. Its Space card is Cleave; Riposte (97) is its Charge card
and Palisade (98) its Mass card.

## 1. Why this race, for this hull

The Swordfish is all sword (`R_VesselActions/SWORDFISH.md`). Its **Time** ability is **Quickdraw**, the sword's RATE:
Time divides the swipe recovery (0.35 s at rest -> 0.14 s at Time 10), the energize hold (1 -> 0.4 s) and the energize
cooldown (5 -> 2 s), x1 -> x2.5, floored x0.5. At Time 5, **Flurry** takes the swipe recovery away altogether. It flies
on the Rhino's airframe: the ramp winds it to 840 u/s on a straight line, and its turn radius converges with speed.

So a Swordfish race should ask the sword to work at speed, and the faster it swings the less the race costs. Spearline
does that with one thing:

- **Seals.** A seal is a pane of prisms in a ring's mouth (a hex grid, 9 u pitch, 7.5 u faces), laid from the ring.
- **A sealed ring counts only through a hole.** Of the seal prisms within **22 u** of where you cross the ring's plane,
  a share must already be gone (`SpearlineSeals.OpenShare`, judged in `SpearlineController.ThreadsGate`). Cross a
  sealed ring that is still closed and it does not count: it is a missed mouth, and you come round again.
- **The hull alone is not enough.** The Swordfish's hull rams a prism for free (`VesselDamagePrismEffect`, no slow),
  but it punches a lane only about as wide as itself (37% of the hole in the model). The bill cuts a band across the
  pane on every stroke. So each seal asks for strokes on the way through, at speed, and Quickdraw is how many you get.

The course is **Headlong's circuit** (`HeadlongCircuit`, the same airframe's ramp and turn model, 8 rings a lap, 3
laps, 24 rings), so the corners already ask what Headlong asks (hold the line and keep the ramp, or turn and lose it).
The seals add the second question: how much of that speed to give up at each ring to cut it open.

The vessel is untouched (the LOCKED mode rule): the bill, the ram, the shields' two hits, Quickdraw and Flurry are the
shipped kit. The mode's levers are the seals, their regrowth and the circuit.

## 2. The ladder (`SpearlineSeals.ForIntensity`, held by `SpearlineSealsTests`)

| | I1: paper | I2: lattice | I3: mail | I4: plate |
|---|---|---|---|---|
| Ring radius (Headlong's) | 96 | 72 | 58 | 46 |
| Sealed rings (of 8) | every 2nd (1, 3, 5, 7) | every ring but the first | as I2 | as I2 |
| Shielded seals | none | none | every 2nd seal | every seal |
| Share of the hole that must be gone | 45% | 60% | 60% | 75% |
| What opens it (model) | one stroke to a side | a stroke each side | as I2, twice on a shielded seal | both sides and the bill stood up |
| A cut seal prism grows back after | 4 s | 3.5 s | 3 s | 2.5 s |

The model (`SpearlineSeals.ModelHullRadius` 6 u, `ModelStrokeHalfWidth` 5 u, plus half a prism face either way) is
what the tests hold the rungs against: the hull alone never opens a seal; each rung opens with its strokes and not with
one fewer. Those two numbers are read off the airframe and the bill's capsule by eye, not measured in the editor, and
are the first thing a playtest should move.

**Regrowth.** A cut seal prism grows back once its time is up AND the field has passed: no hull is within the ring's
radius + 60 u of it, and no pilot's next ring is this one. So the leader cuts, and the pack close behind flies through
the leader's holes (a comeback current: trailing pays less); a pilot who comes round for a second pass keeps what they
already cut.

## 3. The AI

None of its own. The gate race's steering (Headlong's, `AICommitFromApproachPoint`) flies the line, and the Swordfish's
shipped sword hand (`SwordfishSwordAutopilot`) swings at any hostile prism in reach and energizes in a crowd of them, so
an AI cuts seals as it reaches them. A crossing that did not open the seal is a missed mouth, and the race's re-attack
brings the AI round; every pass cuts more. **Watch item:** the sword hand thinks every 0.15 s and strokes for 0.3 s,
so at full ramp it may reach a seal too fast to cut it open, and loop. If AIs loop at I3-I4, the fix is in the mode
(a slower approach line into sealed rings through `TryOverrideAim`), never in the vessel.

## 4. Files

| | |
|---|---|
| Controller | `Spearline/SpearlineController.cs : GateRaceController` (seals, regrowth, the hole rule, the hint) |
| Course | `Spearline/SpearlineCourseSource.cs` (Headlong's `BuildCircuit`, 3 laps) |
| Seals | `Spearline/SpearlineSeals.cs` (pure: ladder, layout, crossing, open share) |
| Tests | `Tests/Editor/SpearlineSealsTests.cs` |
| Generator | `Tools/Build/author_spearline_assets.py` (`hull_time_race`, scene cloned from Thornline's) |
| Target | `EndConditionOverridesSO.spearlineGateTarget` (24) |
| Toasts | the shared race beats (129-132), the comeback, and `SpearlineHint` (171) once at the start |

## 5. Verification status

- Authored headless: generator `--check`, the reference compile and the static gates (see the PR).
- `SpearlineSealsTests` are edit-mode tests and have not been run in the editor.
- **Not run in Unity, not flown, no card art rendered yet** (the card falls back to the placeholder until `/cardart`
  has a recipe for it). Hand test: fly I1 and I2 in the Toybox; confirm a stroke opens a seal, a straight ram does not
  count the ring, and the seal grows back after the pack.
- No offline race model: the AI cannot be trained on it by self-play yet (`/compute-scout`'s coverage lens will list it).
