# Learning from players: record the SITUATION, not just the sticks

**The ask (Yash, 2026-10-15).** "Input for training AI: we need to also record the situations. What target the player
was aiming at, where the ring or the target is, where the obstacles are, how we skim the obstacles, how quickly we
change direction, how we ignore some skims when we must keep speed. All these parameters need to be calculated to
understand the situation that is right for that manoeuvre. This is how we can actually train the AI, not just by input."

This plan extends `AI_TRAINING_PLAN.md` L3 (learn from players) and the recorder built for it on 2026-10-15
(`HumanFlightRecorder.cs`, `Tools/Build/learn_from_flights.py`, schema `cosmic-shore.flight/2`). It does not replace them.

## 1. Why sticks alone cannot teach

A recording of sticks says *what* the pilot pressed, never *why*. The same stick input is right in one situation and a
crash in another. To learn, every moment of a flight has to be stored as a triple:

| Part | Question it answers | Example |
|---|---|---|
| **Situation** | What could the pilot see and what did they have? | Ring 5 is 240 u ahead, 35° left; a rival rail crosses 40 u ahead; boost is full; a skim is 12 u off the line |
| **Action** | What did they do? | Pitched left at 80°/s, held boost, did not take the skim |
| **Outcome** | What did it cost or win? | Threaded ring 5 at 610 u/s, 0.8 s faster than the Hard AI from the same spot |

The AI then learns **"in situations like this, do that, because it paid off"**, which it can use on a course it has
never seen. Copying sticks only works on the exact course it was recorded on.

## 2. What we record and what we compute

The rule: **record what cannot be recomputed; compute everything else offline.** That keeps the recorder cheap (it
runs while a person plays), and lets us add a new feature later and re-run it over every flight we already have.

### 2.1 Recorded once per race (static world)

Regatta's world is static, so it is copied once at the end (the recorder already does this for the pilot's own rail):

- every ring: position, axis, mouth radius, order, laps;
- every rail: its prisms, up axes, contact-shell size, which domain owns it;
- the hull, its domain, intensity, the difficulty of the rivals, the build and the course fingerprint.

### 2.2 Recorded 10 times a second (dynamic world)

What `flight/2` has today: position, heading and up, speed, boost, the four stick axes, throttle, rings threaded.

Added in `flight/3`:

- **Hull state:** velocity vector, yaw / pitch / roll rates, the speed verb's state (charge, ramp, soar, skim boost),
  drift held, element levels.
- **Rivals:** each other hull's position, velocity and hull class (8 hulls × 7 numbers is small).
- **Things that move or vanish:** crystals present (position), prisms destroyed since the last sample (ids only).
- **Who was flying:** human or AI-holding (already skipped today), and the camera mode the human was using.

### 2.3 Computed offline, per sample (the situation, in the pilot's own frame)

All in **hull-relative** terms (distance, bearing left/right, elevation up/down, closing speed), so a situation on one
course is comparable with one on another.

| Group | Features |
|---|---|
| **Target** | Next ring: distance, bearing, elevation, angle between the hull's heading and the ring's axis, offset from the mouth's centre, mouth radius. The ring after it: the same, plus the turn angle between the two legs. Time to mouth at current speed. |
| **Line-up** | Heading error to the mouth; to the approach point; whether a straight line from here threads the mouth; the turn radius needed vs the hull's minimum turn radius at this speed. |
| **Obstacles** | The nearest N rail points / prisms inside a forward cone: distance, bearing, closing speed, own rail or rival rail. Gap to the nearest contact shell. Rivals within 100 u: bearing, closing speed. |
| **Skim** | Is a skimmable surface within skim reach now; the cheapest skim within the next 2 s and the detour it costs (extra distance, extra heading change); the skim boost it would give. |
| **Manoeuvre** | Turn rate (°/s), how fast the turn rate changes (jerk), throttle change, boost press / release, distance flown since the last ring. |
| **Race** | Lap, place, time behind the leader, rings left. |

### 2.4 Inferred labels (what the player was trying to do)

These are guesses from what happened next, so each carries a confidence:

- **Intended target.** For each candidate (the ring mouth, the approach point, a crystal, a rail point to skim, a rival
  to block), how well does it explain the next 0.5–1.0 s of flight? The best-explaining candidate is the label. This is
  how "which target was the player aiming at" is answered.
- **Manoeuvre phase.** Cruise, approach, line-up, thread, exit, skim run, recovery after a miss, boosting straight.
  Cut where the intended target or the turn rate changes sharply.
- **Skipped opportunity.** A skim (or crystal) was within reach and cheap, and the player held the line. Recorded with
  what it would have cost (§2.3 Skim) and what the player kept instead (speed into the next ring). This is the "ignoring
  some skims when we must keep speed" case, made measurable.
- **Mistake.** A missed mouth, a rail hit, a break-off: the moments the AI should *not* copy.

### 2.5 Outcome, per segment

For every manoeuvre segment: time spent, speed in and out, ring threaded or missed, and **time vs the Hard AI from the
same spot** (the artifact can fly the AI from any recorded state; Unity's AI Lab will too). That last number is what
separates "the player did something smart" from "the player did something".

## 3. How the AI learns from it (in this order)

Each step is useful alone; the next starts only when the one before shows the data is worth it.

1. **Situation-dependent numbers (cheapest, explainable).** Today the AI's knobs are constants or simple rules (the
   approach lead is `min(480, 0.4 × leg)`). Fit them as **curves over the situation** from the player's good segments:
   - commit distance as a function of leg length and the turn into the ring;
   - turn aggressiveness as a function of speed and heading error;
   - skim height and guard margins (what `learn_from_flights.py` already does, now per situation instead of one average);
   - boost timing as a function of distance to the mouth and line-up.
   Stored in the hull's AI config SO as small tables, so a person can read and review them. Same input-only AI.
2. **Decision rules for the choices.** The discrete choices (take this skim or skip it; boost now or hold; which of two
   targets) learned as small decision trees or logistic rules from the labelled moments (§2.4), e.g. "skip a skim when
   the detour exceeds X° within Y u of the mouth". Reviewable, ported to C# as a table. This is the step that teaches
   the AI *when* to ignore a skim.
3. **The situation library (testing).** The labelled moments become test cases: put the AI in the exact recorded
   situation (artifact first, then the Unity AI Lab) and score how often it picks what the good player picked, and how
   much time it loses or gains. This is how we know the AI really learned.
4. **Behaviour cloning, later.** A small network from the §2.3 features to the sticks, trained offline on the good
   segments only, run by Sentis behind the same input-only contract (`AI_TRAINING_PLAN.md` L3 step B, T5). Only if steps
   1–2 leave a measurable gap.

Good data only: segments from finished races, and moments that beat the Hard AI from the same spot, count most. Mistakes
(§2.4) are excluded or used as "do not do this" examples.

## 4. Where it is built

| Piece | Where | Notes |
|---|---|---|
| Recorder `flight/3` (§2.1, §2.2) | `HumanFlightRecorder.cs` (extend) | Cheap per sample; no feature maths in the game |
| One **feature spec** | `Docs/AI_SYSTEM/situation_features.json` | The names, units and formulas of §2.3, so Python, the artifact and C# compute the same thing; golden files test it |
| Offline feature + label extraction (§2.3–2.5) | `Tools/Build/situation_features.py` (new, beside `learn_from_flights.py`) | Re-runnable over every saved flight |
| Learning (§3 steps 1–2) | `learn_from_flights.py` (extend) | Writes reviewed SO tables, never lower safety margins (its existing rule) |
| Prototype and viewer | Training AI for Regatta artifact | Record your flight in `flight/3`, show each moment's situation and label on the stage (target line, skipped-skim marker, phase colour), and run the situation library against the AI |
| Situation library in Unity | The AI Lab (`AI_TRAINING_PLAN.md` T1) | Spawn a hull in a recorded state |
| Other modes | A per-mode situation provider (targets, hazards, resources) | Regatta first; Skim Race (crystals, track) next; the same groups of §2.3 |

Privacy and storage stay as `AI_TRAINING_PLAN.md` L3 steps 2–3 say: editor and dev builds now, players only after
their own consent question; no personal data is recorded.

## 5. Order of work

| Step | What | Status (2026-10-16) |
|---|---|---|
| **S1** | Feature spec (§2.3) and labels (§2.4) | **Written**: `Docs/AI_SYSTEM/situation_features.json` (every feature, label and learning rule, with its exact definition). Waiting on your sign-off: say what is missing or wrong |
| **S2** | Artifact: record `flight/3`, compute the features in the page, replay them | **Built**: the Training AI for Regatta page records every race you fly by hand; its **Flights** tab replays one on the stage (your hull as a white ghost, a line to the target you were going for coloured by phase, green / red / amber dots for skims taken / skipped and mistakes), shows every feature of the moment, lets you **correct** a stretch's phase and target, and compares each leg with the Hard AI flown from the same spot. `Docs/Studios/RegattaAI/situation.js` |
| **S3** | `situation_features.py` over Unity's flights | **Built**: `Tools/Build/situation_features.py` reads flight/2 and flight/3, line for line the same as `situation.js`; `--self-test` runs both on `Tools/Build/situation_fixtures/` and fails on any difference (with a negative control). First real flight (your Squirrel, 2026-10-16 02:31, 24 rings): commit about 431 u out (the AI's is 420), 22 skims taken and 7 skipped, a skim taken when its detour is 10 u or less (69% of your choices) |
| **S4** | Unity recorder `flight/3` | **Built, not editor-verified**: `HumanFlightRecorder` adds both triggers, the course and every other pilot's position and rings each sample. Rail hits and skims are still inferred (speed drop, boost rise) |
| **S5** | Learning step 1 (commit curve) and step 2 (skim rule, boost angle), with the situation library as the test | **Built in the page**: *Learn from my flights* learns from your clean legs only, then puts today's AI and the learned AI in your own moments (skim choices, leg starts) and reports how often each does what you did and how fast; a learned AI that is slower than today's is not offered. *Use as this hull's trained AI* hands it to the Training tab (races, trained vs untrained, Copy JSON). **Not yet in Unity**: the learned commit curve and skim rule need their two knobs ported to `GateRaceController` / the Skim Race pilot (with the AI's owners) |
| **S6** | Behaviour cloning | Only if S5 leaves a measured gap |

Measured with AI-flown "flights" as stand-ins for yours (the mechanism, not your skill): the learned Manta raced your legs
in 74.3 s against today's 80.0 s and the learned Sparrow in 168.9 s against 191.7 s; a Squirrel learned from an Easy AI
was slower (173 s against 107 s) and was correctly not offered.

Everything on `Ys-bleeding-edge` first. The recorder and `learn_from_flights.py` are session 013Gr's work from today
(Yash's sessions): extend them, do not fork them. The genetic trainer (`Assets/_Scripts/Utility/AITraining/`) belongs to
studyholic, Garrett and Shombith; any use of it is agreed with them first.

## 6. Questions for you

1. Is the feature list in §2.3 what you mean, or are there situations you read while flying that are missing?
2. The phase names in §2.4: do they match how you think about flying a ring?
3. Start with S2 in the artifact (fast to see and correct), or straight in Unity?
