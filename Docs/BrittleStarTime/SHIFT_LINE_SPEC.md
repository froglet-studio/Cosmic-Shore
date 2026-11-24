# Brittle Star Time: the Shift Line (spec, 2026-11-16)

**Upgrade in place:** `GameModes.BrittleStarTime = 74`, owned by Shombith. Requested by Yash ("make every vessel's Time
minigame super fun", round 5). **Plan step:** `time-arcade`.

**What stays the same:** the Brittle Star kit (no vessel file, prefab, action SO or config is edited) and the four rung
shapes (Gearbox, Feather slalom, Coast-in hairpins, Corkscrew; Shombith, 2026-11-06).

**Floors to lift first** (`TIME_GAME_FUN_SCORES.md`): Rivals 2, Fairness 3, Feedback 4.

Every number below comes from the offline model:
- the shipped course code, flown by `FlightSim` in `Tools/Build/brittle_star_time_course_harness`;
- the command is `run.sh --race`;
- it uses the hull's own speed and turn laws, read off its assets.

The model is a model, not the game. What only Unity can show is listed in section 6.

## 1. What the evidence says (before any design)

**Recorded play.** Shombith flew two races (2026-11-02, `origin/bugledger/shombith`). Both were on the pre-recut course.

- **I1: 175 s.**
  - The first ring took 41 s: he flew 22 u/s in Creation from the spawn pad, 885 u from ring 0.
  - Speed Mode was first lit at 114 s.
  - From then on he took 5.5 s a ring.
- **I4: 135 s**, including 10-16 s overshoot loops at 270 u/s.
- The Medium AI rival finished neither race (11 and 8 of 16 rings).

**What the model shows today:**

| | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| The AI today (shipped Speed law, nose lag, Creation off Speed) | 244 s | 68 s | 78 s | 189 s |
| A reading human (no lag, 0.25 s reaction) | 49 s | 37 s | 30 s | 25 s |

**Why the AI is slow.** The hull's Speed law (`BrittleStarAIBoostPolicy`):
- engages only with 1.5 s of runway to the next ring;
- lets go 0.5 s before every ring, whatever the corner;
- off Speed, the autopilot picks Creation (x0.5, 22.5 u/s).

On 300-900 u legs this means it barely uses Speed Mode.

**Why the question is narrow.** "When do I let go of Speed Mode?" is mostly answered by "when the nose is far off the line":
- A human who lifts only when the nose is more than 30-90 deg off flies within 1-4% of any per-ring plan at I1, I2 and I4.
- Corners tighter than Speed Mode's circle exist only at the I3 hairpin apexes and the I4 return.

This is Redline's lesson again (REDLINE.md: lift posts were measured fake there).

## 2. Three designs

**D1, verb first: shift boards everywhere.**
- A gate of posts before every ring, at the let-go distance for that ring's corner.
- A funnel of dotted rails down each ring's axis.
- Grading: "clean shift" and "flat out".

**D2, spectacle and feedback first: the heat.**
- Redline's grading, but for this hull: a clean thread through the heart throws a shock ring off the gate, and a flat-out
  thread (heart, at 90% of full Speed) throws a bigger one.
- Streaks called at 3, 8 and 16.
- Lap splits, a best-lap call, and a clean-lap chase of flares round the circuit.
- A start hint that teaches Speed Mode, plus a race length re-cut per rung.

**D3, rivals and fairness first: the coach.**
- The mode coaches the AI's Speed wish from the course, through the hull's own autopilot control (input only).
- Every difficulty lifts at its own nose angle, trusts its own share of each corner's speed, and errs per ring by a fixed
  seat hash.
- The AI flies the course's line (`TryOverrideAim`).
- A mode-side nose follow rate per difficulty, as Redline and Switchback set theirs.
- Laps per rung, so a reading pilot lands near 90 s.

## 3. The panel (player, engineer, rules/AI) and the synthesis

**What each judge changed:**
- **Player** (scored the first draft 7/7/7/6/5/4, about 6.0):
  - one start hint per rung (Speed Mode, the posts, the roll), plus a reminder for a dark start;
  - rival pace held steady across the rungs, instead of growing softer as the rungs grow harder;
  - a grade that does not punish I2's half-mouth line;
  - let-go gates drawn as two columns, not a hoop that reads as a ring.
- **Engineer:**
  - the wish is re-asserted every tick, because the driver's `Release` and the executor's own init null it;
  - one `LateUpdate` across the partials, and no `Update`;
  - a distinct `PrismLit` id base, lighting one gate at a time;
  - the line flown through `TryRaceLine` rather than `TryOverrideAim`, so the platform's difficulty mistakes and the
    missed-mouth break-off still apply;
  - the wish released at the whistle and on despawn.
- **Rules/AI:**
  - the coach is input only, and the per-mode policy variant is a mode tuning, as precedent allows;
  - the nose follow is set on every arming;
  - the coach is framed as a scaffold under Plan step `time-arcade`, not the genome;
  - an EditMode test holds that the variant never engages and that no `_I<n>` variant exists;
  - the coach files are added to `check_ai_no_state_writes.py`.

**The synthesis, measured:**
- **Kept from D3:** the coach and the line, laps per rung, and a per-rung, per-skill nose follow and corner share. These
  were searched with `--tune` against a pace of 2.0 / 1.5 / 1.25 / 1.05x the model's reading human (Easy to Legendary).
- **Kept from D2:** the heat, graded by the speed a thread carried. A flat-out ring also asks for the heart: 60% of the
  mouth, wide enough for the slalom's line.
- **Kept from D1, only where it pays:** the let-go gates. They save 4.4% at I3 and 8.3% at I4, against the best line
  flown without them (the bar is 3%). None stand on I1, on I2 or on the coil.
- **Cut from D1:**
  - Funnel rails: -2.3 / -4.3 / -0.7% at I1 / I2 / I4.
  - Gates on flat-out rings.

**The final numbers (model):**

| | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| Laps (rings) | 2 (16) | 3 (24) | 3 (24) | 4 (32) |
| Reading human | 49 s | 54 s | 41 s | 48 s |
| AI today | 244 s | 95 s | 124 s | 368 s |
| Coached Easy / Medium / Hard / Legendary | 98 / 74 / 61 / 52 s | 107 / 80 / 67 / 57 s | 82 / 64 / 51 / 42 s | 96 / 74 / 60 / 59 s |

## 4. The rungs, the length

**Laps per rung:** 2 / 3 / 3 / 4, so the race is 16 / 24 / 24 / 32 rings.

**The authored target** (`brittleStarTimeGateTarget`, 16) stays the I1 race. Higher rungs fly more laps of the same eight
rings (`BrittleStarTimeCourseSource.AuthoredGateTarget`).

**How the laps were chosen:**
- The recorded human at Speed took about 1.6x the model's reading human per ring (5.5 s against 3.4 s at I1).
- With that factor, the estimate is about 78 / 87 / 72 / 80 s, against the ~90 s target.

## 5. What is built

**The line:** `BrittleStarTimeLine`, pure.
- Corner speed from the flyable circle (the inscribed circle plus the mouth).
- The let-go distance: the coast from full Speed plus a 40 u blend-out.
- The let-go gate's six posts.
- The steering line: down the ring's facing; at a ring that faces across the leg into it, at its centre.

**The coach:** `BrittleStarTimeCoach`, pure.
- Skills mapped from `AIDifficultyFor`.
- Light / drop angles, corner share, the seat's let-go error, and nose follow.

**The heat:** `BrittleStarTimeHeat`, pure.
- The grades: Missed, Threaded, Clean, FlatOut, CleanShift.
- The streak and lap rules.

**The controller partials:**
- `BrittleStarTimeController.Line.cs`:
  - lays the let-go posts (two columns of three) on every peer from the broadcast course, as SuperShielded Blue prisms
    (Waystation's lanterns);
  - regrows them;
  - lights a gate in the local pilot's colour (`PrismLit`) on a clean shift.
- `BrittleStarTimeController.Coach.cs` (server):
  - sets each AI's wish through `BrittleStarExecutor.SetAutopilotSpeedWish`;
  - sets its nose follow;
  - flies it down the line.
- `BrittleStarTimeController.Heat.cs`:
  - the local pilot's grades;
  - flares in the world (`ToyFactory.AddSwitchRing` copies of the gate, Redline's shock ring);
  - toasts and FMOD.

**The AI policy variant:**
- `Resources/AIBoostPolicies/BrittleStarAIBoostPolicy_BrittleStarTime.asset` is a mode variant of the hull's policy
  whose straight law never engages (engage runway 999 s).
- So in this mode the coach is the one writer of the wish.
- The hull's own policy is untouched.

**Toasts 410-419** (417 the I3 hint, 418 the I4 roll hint, 419 the dark-start reminder):

| Id | Name | When |
|---|---|---|
| 410 | Hint | The start hint: light Speed Mode |
| 411 | Streak | A streak is called |
| 412 | Lap split | Each lap |
| 413 | Best lap | A lap beats the best |
| 414 | Clean lap | A lap with every ring clean |
| 415 | Flat out | The first flat-out thread |
| 416 | Clean shift | A clean shift |

**FMOD:** 8 empty slots.

**Generator:** `author_brittle_star_time_assets.py` writes the controller's new fields, the toasts, the card and preview
text, and the policy variant.

**Tests:** `BrittleStarTimeLineTests` (EditMode, deterministic). The harness runs them with negative controls.

## 6. What only Unity can show

- The posts' look and their light.
- Whether the AI's Speed presses chatter.
- The coached AI in a six-seat grid.
- That the start hint shows.
- The real finish times: they decide the laps.
- That rival autopilots do not shoot the posts. The posts sit about 55 u off the line, outside the autopilot's 8-12 u scan.

## 7. Proposals for Shombith (the kit and its AI; not changed here)

1. **Off Speed, release to Cruise instead of Creation.** The autopilot picks Creation (x0.5) whenever it does not wish
   Speed (`BrittleStarExecutor.Autopilot`). It coasts at half cruise and lays five trails at every corner. Pressing Speed
   again (Cruise) would keep 45 u/s.
2. **Let the hull's Speed law read the corner, not only the runway.**
   - Today it lets go 0.5 s before every ring.
   - The coach's rule (`BrittleStarTimeLine.LetGoDistance`) could move into `BrittleStarSpeedModeAIPolicySO`, so every
     mode gains it.
3. **The test reads `speedModeMultiplier.Value`, but the runtime evaluates `Min`.** Both are 6 today
   (`BrittleStarTimeCourseTests`).
