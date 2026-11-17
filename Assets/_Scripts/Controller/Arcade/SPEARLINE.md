# Spearline - the Swordfish's Time race

> **Status.** Authored headless on 2026-11-15 (`claude/swordfish-game-kit`), for Garrett's ask to build every missing
> elemental game ("four games (one for each element is ideal) make all the missing games you can"), on the ids
> `Docs/ElementalLoop/GAME_KIT_ROSTER.md` reserved for it (row 6: `GameModes` 96, hint toast 171). **Upgraded in place
> by the fun pass of 2026-11-16** (Yash's round 5, `Docs/Spearline/FUN_PASS.md`): the hole rule, the armour, the worn
> seal, the mouths, five laps, the AI's read and stroke, the feedback. Held by `SpearlineSealsTests` (run headless by
> `Tools/Build/spearline_harness`, with negative controls) and timed by `Tools/Build/spearline_race_model.py`.
> **Nothing here has been run in the Unity editor, and nobody has flown it.**

`GameModes.Spearline = 96` is the **Swordfish's Time card**. Its Space card is Cleave; Riposte (97) is its Charge card
and Palisade (98) its Mass card.

## 1. Why this race, for this hull

The Swordfish is all sword (`R_VesselActions/SWORDFISH.md`): a long, thin bill on the nose. One trigger lays it flat
out to that side; both stand it up. It flies on the Rhino's airframe: the ramp winds it to 840 u/s on a straight line,
and its turn radius converges with speed. Its hull rams a plain prism for free (`VesselDamagePrismEffect`, no slow).

So a Swordfish race asks the sword to work at speed. Spearline does that with **seals**: a seal is a pane of prisms in
a ring's mouth (a hex grid, 9 u pitch, 7.5 u faces), laid from the ring.

- **A sealed ring counts only through a hole.** The hole is the connected run of gone prisms the crossing sits in: the
  prisms the hull takes out itself, plus every gone prism joined to them (`SpearlineSeals.HoleSize`, judged in
  `SpearlineController.ThreadsGate`). It must hold at least **8** prisms. Cross a sealed ring that holds and it does
  not count: a **closed pass**, and you come round.
- **The hull alone is not enough; one stroke INTO the pane is.** As the hull crosses, the bill cuts one line, up to its
  length, out of the crossing point. The hull alone makes a hole of 3-7 prisms; the hull plus a stroke into the pane
  makes 8-19, whatever the bill's true width (2.4 or 5 u) and wherever the crossing falls on the grid. A stroke out of
  the pane near its rim cuts air and can fail: swing **into** the pane.
- **Armour is the read (I2+).** On an armoured seal a sector of the pane is shielded (`PrismKind.Shielded`). A shield
  only cracks under a hull or a stroke and the prism stands, so a stroke into the armour cuts nothing, and a crossing in
  it has no hole at all. The pilot reads where the plain face is, crosses it and swings into it. The sector faces a hash
  of the ring (`SpearlineSeals.ArmourAngle`), so it is the same every lap: read on lap one, known after. A cracked
  shield regrows with the seal.
- **The worn seal.** Each closed pass lowers that pilot's need at that seal by 2 prisms, so the fifth pass counts
  whatever it hits (`SpearlineSeals.MaxPasses`). No seal can trap anyone, human or AI.

**Why the rule changed (2026-11-16).** The shipped rule judged the share of a 22 u disc round the crossing that was
gone. With the bill's real width the hull plus one stroke reached at most 42% of that disc, under every rung's need
(45-75%), so no single pass opened a seal; I2-I4 asked for "a stroke each side", which one crossing cannot make. And
whether the hull alone read 17% or 37% depended on where the crossing fell against the grid, which no pilot can see.
The connected hole separates the hull from a stroke with no grid luck.

The course is **Headlong's circuit** (`HeadlongCircuit`, the same airframe's ramp and turn model, 8 rings a lap, **5
laps, 40 rings**), with Spearline's own mouths. The seals sit on the **gentlest** rings of each lap
(`SpearlineSeals.SealedRings`, never ring 0), so a seal is met at speed and the hard corners stay open and still ask
Headlong's question (hold the line and keep the ramp, or turn and lose it).

The vessel is untouched (the LOCKED mode rule): the bill, the ram, the shields' two hits, Quickdraw and Flurry are the
shipped kit. The mode's levers are the seals, their armour, their regrowth, the circuit and the AI.

**What the race does not ask: Time.** Quickdraw shortens the swipe recovery, which starts when a trigger is released;
a pilot holds the stroke through the pane and never re-presses at one seal, so the recovery never binds. The fun pass
measured it and did not invent a use (a kit proposal is in §6).

## 2. The ladder (`SpearlineSeals.ForIntensity`, held by `SpearlineSealsTests`)

| | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| Mouth (u; Headlong's was 96 / 72 / 58 / 46) | 96 | 84 | 74 | 66 |
| Sealed rings a lap (the gentlest) | 2 | 3 | 4 | 5 |
| Armour on every seal | none | 120 deg | 180 deg | 240 deg |
| Hole needed / wear per closed pass | 8 / 2 | 8 / 2 | 8 / 2 | 8 / 2 |
| A cut prism (or cracked shield) grows back after | 4 s | 3.5 s | 3 s | 2.5 s |
| Seal prisms a lap | 734 | 813 | about 844 | about 815 |
| The question | swing into the pane | read the armour | a smaller face, one more seal | a third of the pane, a small mouth, one more seal |

**One more seal at I3 and I4 (2026-11-17).** With the AI's ramp fixed (#2173), the race judge found I1-I3 nearly
level: 141 / 150 / 155 s at Legendary. The AI's pace is a ramp-and-coast cycle per ring that barely depends on the
corners (Spearline's own gentler and sharper corner rows left I1 at 141 s and I3 at 157 s, so they were dropped).
What does climb is the work at the seals, so I3 and I4 each seal one more ring a lap: 2 / 3 / 4 / 5. Legendary now
runs 141 / 150 / 159 / 208 s and Medium 174 / 182 / 185 / 220 s (seeds 1-2). Every rung climbs, but I2 to I3 only by
1-6%. The bigger I2-I3 demand is reading the armour, which a perfect-reading Legendary AI does not pay and a person
does. Garrett's playtest is the judge of that step.

**Regrowth.** A cut seal prism grows back once its time is up AND the field has passed: no hull is within the ring's
radius + 60 u of it, and no pilot's next ring is this one. So the leader cuts, and the pack close behind flies through
the leader's holes (a comeback current); a pilot who comes round keeps what they already cut. The dead prism is released
to its pool before the new one is laid (Spearline owns its seal prisms' exit).

**Measured** (`spearline_race_model.py`, 40 courses a rung, a MODEL): reading saves 0 / 36 / 37 / 57% of race time
against the best fixed habit; a human reader (one factor fitted to the recorded Headlong I1 race) takes about 85 / 88 /
96 / 109 s; the best fixed habit 85 / 138 / 150 / 253 s; 4 laps would fall to 69-71 s at I1-I2.

## 3. The AI (`SpearlineController.Coach.cs`)

- **The read.** For its next sealed ring, the AI takes `SpearlineSeals.Read`: of 37 crossings on a polar grid inside the
  mouth and the three strokes it can make from its current roll (left, right, both = stood up), the one whose model
  hole opens the seal (by 1 spare prism) nearest the centre, from the seal as it stands now (its own earlier cuts and
  cracks included). The gate steering is moved there with `AILaneOffset`. The read refreshes 5 times a second and
  freezes inside 150 u.
- **The stroke.** The coach owns the AI's shipped sword hand (`SwordfishSwordAutopilot.Coach`, an
  `ISwordfishSwordCoach`) inside 500 u of a sealed ring: triggers at rest while closing (so the hand never stands the
  bill up to energize in front of a pane - its crowd rule fires on any seal), the read's trigger from 0.35 s before the
  plane until 30 u past it. The hand writes the trigger mirrors as a pilot's fingers do: input only.
- **The ramp** (2026-11-17). Spearline had no ramp tuning of its own, so `AIBoostPolicySO.ForMode` gave the
  Swordfish the Rhino's untuned default (`RhinoAIBoostPolicy`: align 12 degrees, stick 0.35, corner speed 600). Under
  it the AI seldom pressed the ramp, coasted back to the hull's 50 u/s floor after every ring and crawled 300-500 u
  to the next one: 247-384 s a race in the real game (race judge, Medium and Legendary, seeds 1-2), one I4 timeout,
  against the model's 31-40 s. `Resources/AIBoostPolicies/RhinoAIBoostPolicy_Spearline` is Headlong's trained ramp
  (the same airframe on the same circuit): 141 / 150 / 155 / 191 s Legendary and 174 / 182 / 174 / 212 s Medium at
  I1-I4, every race finished. Still open: I1-I3 barely climb (0.95-1.06x a rung), and a 650 u leg never lets the
  ramp reach 840 u/s (it peaks near 500).
- **By difficulty** (Easy / Medium / Hard / Legendary): the chance it does not read a seal and crosses the centre with
  a right-hand stroke 30 / 15 / 5 / 0%, and a fixed lane error up to 10 / 7 / 4 / 2 u.
- **A closed pass** is the gate race's own: the AI comes round to the same approach. The worn seal and its own cut
  bound it (the model's AIs never need more than 5 passes at a seal; Hard and Legendary race within 1% of the reader).

## 4. Feedback (`SpearlineController.Feedback.cs`, local pilot only, nothing replicated)

- **In the world:** the plain face of your next armoured seal is drawn lifted (`Prism.SetColorLift` 0.35) from 900 u,
  so the face that cuts reads against the shields; a seal threaded on the first pass flashes round its hole (0.9,
  fading over 0.6 s).
- **Toasts** (`GameToastConfig_Spearline`, authored): the hint (171, reworded), `SpearlineSealHeld` (400, the first
  closed pass), `SpearlineCleanStreak` (401, a run of 3 / 6 / 12 first-pass seals), `SpearlineArmourHint` (402, the
  first armoured seal), and the shared race beats. 403-409 are reserved and spare.
- **Sounds:** `sealOpenedEvent`, `sealHeldEvent`, `cleanStreakEvent`, shipped EMPTY; the base race's two slots.
- The fun-pass fields (the AI's read, the feedback, the sounds) are not written into the scene: they ship at their C#
  defaults, as Redline's do.

## 5. Files

| | |
|---|---|
| Controller | `Spearline/SpearlineController.cs : GateRaceController` (seals, regrowth, the hole rule, the worn seal), `.Coach.cs` (the AI), `.Feedback.cs` |
| Course | `Spearline/SpearlineCourseSource.cs` (Headlong's `BuildCircuit` with Spearline's mouths, 5 laps) |
| Seals | `Spearline/SpearlineSeals.cs` (pure: ladder, sealed rings, armour, laying, the hole, the stroke model, the read) |
| The AI's hand | `Controller/AI/Rhino/SwordfishSwordAutopilot.cs` (`ISwordfishSwordCoach`, the `Coach` seam) |
| Tests | `Tests/Editor/SpearlineSealsTests.cs`; headless: `bash Tools/Build/spearline_harness/run.sh [--self-test]` |
| Model | `py -3 Tools/Build/spearline_race_model.py [--self-test]` |
| Generator | `Tools/Build/author_spearline_assets.py` (`hull_time_race`, scene cloned from Thornline's) |
| Target | `EndConditionOverridesSO.spearlineGateTarget` (40) |
| Toasts | the shared race beats (129-132), the comeback, `SpearlineHint` (171) and 400-402 |

## 6. For Garrett (the Swordfish's owner): kit proposals, not changes

- **Give Quickdraw something to do in a race.** Its recovery starts on release, so a held stroke never meets it. If
  Time also sped the bill's swing-out (`analogSmoothing`, 0.04 s) or let a stroke re-press mid-pane, a Time race could
  ask for two strokes at one seal. Today Spearline is a sword race, not a Time race.
- **The sword hand's crowd rule** (`SwordfishSwordAutopilot`, energize when 6 hostile prisms are in reach) stands the
  bill up in front of any pane. Spearline's coach covers it in this mode; other prism-dense modes may want the same.
- **A death cause on `Prism`** (cut by a bill vs rammed) would let the feedback name a clean cut.

## 7. Verification status

- Headless: `spearline_harness` (11 tests, 8 negative controls), `spearline_race_model.py --self-test`, the generator's
  `--check`, the static gates. The controller partials and the autopilot seam were read by an independent reviewer, not
  compiled (no Unity on the authoring machine). The review found no compile error and no rule break, and these were
  fixed: the ladder allocated on every read (every frame), the AI's read allocated on every refresh, a crossing that
  ended exactly on the plane skipped the hole test, the coach held the triggers anywhere in the slab of a seal's plane,
  a closed pass did not refresh the AI's frozen read, regrowth could release a prism no longer the seal's, a null
  guard, and the regrow clock now runs from when the field passed (as documented).
- **Watch item:** the stood-up stroke holds both triggers, the energize stance; at a Time rate of about x2.6 (Time ~10)
  the 0.39 s hold would ignite the blade. Spearline seeds no Time, so it should not arise.
- **Not run in Unity, not flown, no card art rendered yet.** Hand test (`Docs/UNITY_VERIFICATION_CHECKLIST.md`): fly I1
  and I3 in the Toybox; confirm a straight ram does not count a seal and a stroke into it does; the armour reads; the
  worn seal lets a ram through by the fifth pass; an AI strokes through a seal.
