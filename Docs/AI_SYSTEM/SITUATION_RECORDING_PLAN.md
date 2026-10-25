# Learning from players: record the SITUATION, not just the sticks

**The ask (Yash, 2026-10-15).** "Input for training AI: we need to also record the situations. What target the player
was aiming at, where the ring or the target is, where the obstacles are, how we skim the obstacles, how quickly we
change direction, how we ignore some skims when we must keep speed. All these parameters need to be calculated to
understand the situation that is right for that manoeuvre. This is how we can actually train the AI, not just by input."

This plan extends `AI_TRAINING_PLAN.md` L3 (learn from players) and the recorder built for it on 2026-10-15
(`HumanFlightRecorder.cs`, `Tools/Build/learn_from_flights.py`; schema `cosmic-shore.flight/2` then, `cosmic-shore.flight/3`
since 2026-10-16). It does not replace them.

**Decision (Yash, 2026-10-16): training data comes from playing in the Unity editor only.** "Let's not use artifact to
train, we will play in editor only for perfect training data." Then (2026-10-16): "delete the artifact, it's not needed;
only tests will happen in the Unity editor itself." The Training AI for Regatta page was deleted with
`Docs/Studios/RegattaAI/`. Recording, learning and testing all happen on the editor side: `HumanFlightRecorder` records,
`Tools/Build/situation_features.py` turns flights into situations and learns, the Third Eye is where the AI is watched.

**Caleb's review (2026-10-15), §7: learn from the pilot's VIEW, not only the world. V1-V4 built 2026-10-16** (camera
recorded, view features, labels gated on what was seen, measured reaction lag); V5 waits on S5 / S6. Status in §7.

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

Regatta's world is static, so it is copied once, when recording starts (**recorded since 2026-10-16**: `ringList`, `laps`,
`rails` with `ownRail`, `rivalDifficulty`, `build`, `unity`, `courseSeed`, `courseIntensity`; the pilot's own rail stays
in `lane`):

- every ring: position, axis, mouth radius, order, laps;
- every rail: its prisms, up axes, contact-shell size, which domain owns it;
- the hull, its domain, intensity, the difficulty of the rivals, the build and the course fingerprint.

### 2.2 Recorded 10 times a second (dynamic world)

What `flight/2` had: position, heading and up, speed, boost, the four stick axes, throttle, rings threaded. Every record
is now `flight/3`.

Added in `flight/3`:

- **Hull state:** velocity vector, yaw / pitch / roll rates, the speed verb's state (charge, ramp, soar, skim boost),
  drift held, element levels.
- **Rivals:** each other hull's position, velocity and hull class (8 hulls × 7 numbers is small).
- **Things that move or vanish:** crystals present (position), prisms destroyed since the last sample (ids only).
- **Who was flying:** human or AI-holding, and the camera mode the human was using.

**Recorded since 2026-10-16** (all of the above, `HumanFlightRecorder`; columns are read by name, so the schema stays
`flight/3`): `vx vy vz` (course x speed), `pitchRate yawRate rollRate` (deg/s in the hull's frame, from the rotation
since the last sample), `charge`, `state` (bits in the header's `stateBits`: boosting, discharging, drifting, slowed,
attached, stationary), `lvC lvM lvS lvT`, `held` (AI-holding samples are now kept and marked; every reader skips them),
`camx camy camz camfx camfy camfz fov camMode` (Caleb's V1 view); `rivalVel` (vx, vy, vz per rival, aligned with
`rivals`); `crystals` (the 12 nearest: x, y, z, element); `railEvents` ([t, rail, prism, 1 destroyed / 0 restored]).
Since 2026-10-17 also: the hull's resource meters `m0..m3` (names in the header's `meterNames`, the boost meter's
index in `boostMeter`; the Dolphin's charge is read from them) and `timeMode` (the Stoat's Mass / Time mode).
Since 2026-10-25 also **each rival's state** (`rivalState`, aligned with `rivals`; columns named in `rivalStateColumns`):
its `state` bits (the pilot's six plus `rivalStateBits`: `ai`, `breakingOff` = `AIPilot.IsBreakingOrbit`, `restricted`
= translation restricted), its `boost` (BoostMultiplier), `boostStatus` (an index into `rivalBoostStatuses`, the AI boost
driver's status with its numbers blanked to `#`; -1 for none), `phase` (`AIPilot.ObjectivePhase`, -1 for none) and
`courseNose` (degrees between the hull's course and its nose). Why: an AI Rhino held 50 u/s through two whole Regatta
races (2026-10-23) and the record could not say whether its ramp driver was waiting, braking or stood down.
`regatta_rival_report.py --hull <hull>` prints each race's shares. Not
recorded: the hull-specific speed-verb phases (ramp, soar, skim boost) beyond `boost`, the meters and `state`; they have
no shared field on the vessel status.

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
same spot** (Unity's AI Lab, T1 in `AI_TRAINING_PLAN.md`, flies the AI from a recorded state). That last number is what
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
   situation (in the editor: the Unity AI Lab) and score how often it picks what the good player picked, and how
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
| One **feature spec** | `Docs/AI_SYSTEM/situation_features.json` | The names, units and formulas of §2.3; `situation_features.py` implements them and its golden file tests them |
| Offline feature + label extraction (§2.3–2.5) | `Tools/Build/situation_features.py` (new, beside `learn_from_flights.py`) | Re-runnable over every saved flight |
| Learning (§3 steps 1–2) | `learn_from_flights.py` (extend) | Writes reviewed SO tables, never lower safety margins (its existing rule) |
| Viewer | (none: the page was deleted 2026-10-16) | Watching happens in the editor's Third Eye; a recorded flight's situations are read with `situation_features.py --json` |
| Situation library in Unity | The AI Lab (`AI_TRAINING_PLAN.md` T1) | Spawn a hull in a recorded state |
| Other modes | A per-mode situation provider (targets, hazards, resources) | Regatta first; Skim Race (crystals, track) next; the same groups of §2.3 |

Privacy and storage stay as `AI_TRAINING_PLAN.md` L3 steps 2–3 say: editor and dev builds now, players only after
their own consent question; no personal data is recorded.

## 5. Order of work

| Step | What | Status (2026-10-16) |
|---|---|---|
| **S1** | Feature spec (§2.3) and labels (§2.4) | **Written**: `Docs/AI_SYSTEM/situation_features.json` (every feature, label and learning rule, with its exact definition). Waiting on your sign-off: say what is missing or wrong |
| **S2** | A replay of a flight with its situations | Built in the page on 2026-10-16, then **deleted with it** (tests happen in the editor). The maths lives on in `situation_features.py`; an in-editor replay (the Third Eye drawing a recorded flight) is the editor-side follow-up if wanted |
| **S3** | `situation_features.py` over Unity's flights | **Built**: `Tools/Build/situation_features.py` reads flight/2 and flight/3, now the only implementation (it matched the page's `situation.js` exactly before the page was deleted); `--self-test` checks the fixture flight against its golden output, with a negative control. First real flight (your Squirrel, 2026-10-16 02:31, 24 rings): commit about 431 u out (the AI's is 420), 22 skims taken and 7 skipped, a skim taken when its detour is 10 u or less (69% of your choices) |
| **S4** | Unity recorder `flight/3` | **Built, not editor-verified**: `HumanFlightRecorder` adds both triggers, the course and every other pilot's position and rings each sample; since 2026-10-16 everything else in §2.1-2.2 (hull state, element levels, `held`, camera, rival velocities, crystals, every ring and rail, rail holes as `railEvents`). Skims are still inferred (speed drop, boost rise); rail hits can now be read from `railEvents` |
| **S5** | Learning step 1 (commit curve) and step 2 (skim rule, boost angle) | **Built in `situation_features.py --write`**, over editor flights only: from clean legs in the faster half of your own times for each ring, once a hull has 3 editor flights; `--write` would write `learned_situations.json` and `LEARNED_SITUATIONS.md` for review. **Neither file has been written yet** (planned output, not in the repo). **Not yet in Unity's steering**: the commit curve and skim rule need their two knobs ported to `GateRaceController` / the Skim Race pilot (task T-20261015-7fc3). The situation-library test (the AI put in your own moments) moves to the editor with that port |
| **S6** | Behaviour cloning | Only if S5 leaves a measured gap |


Everything on `Ys-bleeding-edge` first. The recorder and `learn_from_flights.py` are session 013Gr's work from today
(Yash's sessions): extend them, do not fork them. The genetic trainer (`Assets/_Scripts/Utility/AITraining/`) belongs to
studyholic, Garrett and Shombith; any use of it is agreed with them first.

## 6. Questions for you

1. Is the feature list in §2.3 what you mean, or are there situations you read while flying that are missing?
2. The phase names in §2.4: do they match how you think about flying a ring?
3. ~~Start with S2 in the artifact, or straight in Unity?~~ Answered 2026-10-16: training data from the Unity editor only.

## 7. Review: add the pilot's VIEW, not only the world (Caleb, 2026-10-15)

**The point (Caleb).** A player decides from a screen of moving objects and spaces. To fly *like* them, and not only
as well as them, the AI has to learn from some version of what they saw. A list of keystrokes means little without
the picture the keys answered.

**Verdict on S1-S5.** Most of the idea is already here. The hull-relative situation in §2.3 is a cheap symbolic
stand-in for the screen. Recording pixels is **not** the fix (see below). But every feature and label is computed
from the **whole world as it was**, not from **what the pilot could see at that moment**. For learning *behaviour*,
three differences matter:

1. **The screen is not the hull frame.** The chase camera trails the hull. `CustomCameraController` has a 0.2 s
   follow smoothing and a rotation lerp, and look-behind mirrors the offset. The FOV is a player setting
   (`DisplayGraphicsSettings.FieldOfView`), reshaped by `CameraAspectFit`. A ring 35° off the nose can be centred on
   one player's screen and off another player's screen.
2. **The pilot sees only part of the world.** They see what is inside the frustum and not hidden behind a rail wall.
   Today `gap`, `aheadPrisms`, `skimDetour` and the label candidates count every prism and rival in range, in any lane,
   seen or not.
   - **The worst case is the skipped-skim label.** A skim the pilot never saw is recorded as "skipped to keep speed".
     The skim rule in S5 then learns a reason the pilot never had.
   - The same goes for a missed mouth when the ring was off screen. That is a *navigation* error, not a *control* error.
3. **Pilots react late.** Seeing and pressing takes about 0.15-0.3 s, plus the camera's lag. Pairing situation(t) with
   sticks(t) blurs every curve. The pairing should be situation(t - lag) with sticks(t), with the lag measured per pilot.

Why this is behaviour and not only skill: much of human "style" comes from these limits. Humans react late to what
comes onto the screen. They do not take a skim they cannot see. They overshoot a ring that appears late. An AI that
learns human curves from all-seeing features learns the wrong causes, and it can never make human-looking mistakes.
The game already has the right place for this on the AI's side: belief handicaps (`GateRaceHandicap`,
`SkimRaceHandicap.ReactionSeconds`). Today those numbers are picked by hand. Flights can **measure** them.

**Why not record frames.**
- **Size:** 10 Hz of even low-res frames is megabytes a race, against a few KB today.
- **Data:** learning from pixels needs orders of magnitude more flights than a few testers will ever fly.
- **What it would learn:** the art, VFX and HUD, which change every build.
- **No consumer:** the AI is input-only and hand-structured. It reads features, not images.

Regatta's world is recorded once (§2.1). So with the **camera pose** recorded, the screen can be re-drawn
*symbolically*, offline, exactly and cheaply, and re-run over every flight whenever a view feature is added.

### What to change (smallest first)

| Step | Change | Where |
|---|---|---|
| **V1** | **Record the camera each sample:** position, rotation (quaternion), vertical FOV, aspect, and camera mode / look-behind. About 10 floats, so the file stays in KB. §2.2 already lists "the camera mode" but S4 did not record it. Read the camera that renders the player's view through `CameraManager`, cached once, with no per-sample search or allocation. S4 is not editor-verified and no player data exists yet, so adding this to `flight/3` now is cheaper than a `flight/4` later (Yash's call). | `HumanFlightRecorder.cs` |
| **V2** | **A `view` feature group,** from the camera pose and the static world. (As built, `situation_features.py` only: `situation.js` was deleted with the page.) For the target ring, the ring after it, the skim candidate, the nearest prism ahead and each rival, compute: `onScreen`, `screenX` / `screenY` (-1..1), `angularSize`, `occluded` (a ray from the camera against the rail prisms' contact boxes) and `tau` = angular size / its growth rate (time to contact, the cue humans judge closing by). Plus a coarse **view grid**, 8×5 screen cells, each holding the nearest depth and kind (own rail, rival rail, ring, rival hull, empty). That grid is the "compressed screenshot". | `situation_features.json`, then `situation.js` and `situation_features.py` (the self-test keeps the twins equal; regenerate the fixture) |
| **V3** | **Gate the labels on what was seen.** A target candidate counts only if it is on screen or was seen within the last ~1 s (memory). A *skipped skim* needs the skim visible and not occluded for at least ~0.3 s before the choice. Otherwise label it `unseen` and leave it out of the skim-rule fit. Split *mistake* into: missed with the ring visible (control) and missed with the ring off screen (navigation). | the same three files |
| **V4** | **Measure the reaction lag** per pilot: the shift that best correlates the target's `screenX` with the yaw stick (and `screenY` with pitch). Fit S5's rules on situation(t - lag). `learn_from_flights` reports the lag. Proposing it as a measured `ReactionSeconds` per difficulty goes to the handicaps' owners (Garrett) and changes no AI by itself. | `situation_features.py` / `situation.js`, `learn_from_flights.py` |
| **V5** | *Later, only if S5/S6 show the gap:* an **AI perception filter**. The AI's belief updates only for what lies inside a virtual chase camera's view, with the measured lag. This is a belief handicap, so the input-only contract is unchanged. It is the step that makes the AI fly *like* a person and not only as fast. | the AI's handicap layer, with its owners |

~~Also in the artifact's Flights tab: draw the pilot's frustum on the replay.~~ Gone: the page, its Flights tab and
`situation.js` were deleted on 2026-10-16. `situation_features.py` is the only implementation.

*Optional audit, not training data:* in development builds, one tiny greyscale capture every 2 s (async GPU readback,
nothing on the frame) to check that V2's view grid matches what was really on screen. Drop it if it costs a frame.

**Questions for Yash.** (1) Camera into `flight/3` now, or a `flight/4`? (2) Should V3's visibility gate apply to the
flights already recorded? They have no camera, so their skim labels would become `unseen`. Or should those flights be
kept but marked as all-seeing?

### Status (2026-10-16): V1-V4 built, not editor-verified

Yash: "add the missing camera fields and build V2 to V4". Both questions were answered with the cheaper default:
(1) the camera went into `flight/3` (readers take columns by name, so no `flight/4` was needed); (2) flights recorded
without a camera are kept and read as **all-seeing**: their labels are not gated, so the golden test still holds.

| Step | What was built | Where | Checked by |
|---|---|---|---|
| **V1** | Each sample: camera position, forward and up (= its rotation), vertical FOV, aspect, mode, look-behind (`camx..camfz`, `camux..camuz`, `fov`, `camAspect`, `camMode`, `camRear`). Read from `CameraManager.ViewCamera` (resolved once a frame; nothing searched or allocated) | `HumanFlightRecorder.cs` | `unity_refcompile`; NOT run in Unity |
| **V2** | `view` per sample: for the target ring, the next ring, the skim candidate, the nearest prism ahead and each rival, `onScreen`, `sx` / `sy`, `angSize`, `occluded`, `tau`; the 8 x 5 view grid (`gridKind`, `gridDepth`); `visible`. A rail prism is not hidden by its own rail's next 8 prisms (the 64 u stretch being looked at); a ring is hidden only when its centre and four rim points all are | `situation_features.py` (`Viewer`, `view`) | self-test: straight ahead is centre screen, right is right, behind / far off-axis are off screen, a prism on the line of sight hides, one beside it does not (negative control), tau = 1.0 s at 100 u / 100 u/s, the grid shows the ring |
| **V3** | A target candidate counts only if it was in plain view within 1 s; a SKIPPED skim needs its prism in plain view for the 0.3 s before the choice, asked of that prism from the camera at each of those samples, else `seen: false` and left out of the skim fit; a missed mouth is `cause: control` (ring in view) or `navigation` | `intent`, `skim_choices`, `analyze`, `learn` | self-test on the fixture with a chase camera: every skipped skim seen; the same flight with the camera looking behind: ring on screen 9% of the time and every skipped skim unseen and left out; no camera: nothing left out, labels unchanged |
| **V4** | The reaction lag: the shift 0..1 s that best lines the ring's screen position (or, with no camera, its bearing / elevation) up with the yaw / pitch sticks, refined between samples. Per hull, the median of its flights; the commit, skim choices and boosts are then read that long before the action and the rules fitted on those. Reported by `situation_features.py` (per flight and per hull; `learned_situations.json` once `--write` is run) and by `learn_from_flights.py` | `lag_from_signals`, `measure_lag`, `hull_lag`; `learn_from_flights.reaction_lag` | self-test: a stick 0.25 s late measures 0.25 s, pure noise measures nothing; the fixture, flown by the Medium AI whose reaction is 0.25 s (`SkimRaceDifficultySO.MediumReactionSeconds`), measures **0.26 s** from the hull and from the screen |

**First real reading.** Yash's flight of 2026-10-16 02:31 (no camera, so from the hull's frame) measures a lag of
**0.0 s** (fit 0.35). A person flying a course they know anticipates the next ring instead of reacting to it, so a
near-zero lag is plausible, but one race is not a measurement. Camera flights will say more.

**Not done.** V5 (an AI perception filter) waits on S5 / S6 as §7 says. A measured `ReactionSeconds` per difficulty is a
proposal for the handicaps' owner (Garrett) once 3+ editor flights exist; nothing here changes an AI. The artifact
replay ideas in §7 went with the page (deleted 2026-10-16).

## 8. What the first 29 editor flights say (2026-10-17)

Data: Yash 23 named Regatta flights at I1 (Jade and Gold lanes), `belac` 6 at I3 (Ruby); all Squirrel, Medium rivals.
**Which count is which.** 14 = the first I1 batch `RACING_LINE.md` §2 measured (2026-10-16); 29 = the set this section
and the Squirrel learning run used (`LEARNED_FROM_FLIGHTS.md`, 2026-10-16); 32 = the Squirrel flights on the bugledger
branches when §9 was written; 46 = every stored Squirrel flight that evening (`RACING_LINE.md` §2.1). On 2026-10-17
(after `git fetch`) the bugledger branches hold 49 Squirrel, 5 Urchin, 2 Serpent and 4 unnamed Regatta flights.
The racing line, the per-leg mistake map and the capture plan are in **`RACING_LINE.md`** (built from the same flights
by `racing_line.py`); this section adds what the situation and rail readers found. The 23/24 recorder bug (the last
ring never sampled) was fixed in `HumanFlightRecorder` and `learn_from_flights.finished_of` by the same session as
`RACING_LINE.md`.

| Finding | Number | What it means for the AI |
|---|---|---|
| **When they skim, pilots use the rail's - face**, the side every ring is on (each ring sits 22 u off every rail on that face) | 94% of skimming, every lane, both pilots; 13.6 u from the rail's centre (4.6 u outside the 9 u shell) | The Regatta Squirrel AI skimmed the **+ face** (`SkimHeight` +4.35 before the change, 11.9 u from the centre) and had to cross the rail to reach a ring: a likely cause of its rail hits. `learn_from_flights` learns the face as `SkimHeight`'s sign (the driver's own convention); it is now **-6.498** (applied 2026-10-17, plan step 1 below) |
| **Speed is the race** | race time vs mean speed r = -0.99; vs mean skim boost r = -0.91 (about 3.0 in the fast races); vs slowed episodes r = +0.55 (17 won races) | Optimise held speed and fewer hits. How many skims are taken or skipped does not move the time (r 0.04 / 0.14) |
| **The AI is far behind** | when Yash won, the Medium rivals had 14 of 24 rings (median, 9..21) | About 58% of a human's pace; no AI flight is recorded, so it cannot yet be compared leg by leg |
| **Humans anticipate** | reaction lag 0.06 s (median of 24 camera flights, from the screen; fit 0.4-0.5) | Medium's 0.25 s and Easy's 0.5 s are slower than a person who knows the course (task T-20261016-f695, Garrett) |
| **The ring is always in view** | target ring on screen 100% of the time, behind a rail at most 1% | The V3 gate rarely fires in Regatta (0-3 skips per race moved to "unseen") |
| Commit and skim rules | commit fit hit both clamps (a 60, b 1.0; median 490 u against the AI's 420); "take a skim when the detour is <= 14 u" agrees 67% | Weak signals: below the line, the face and the speed |

**The plan** (with `RACING_LINE.md` §3-4; nothing reaches the game without the owner's OK and an editor test):

1. **Test the ring face first**, the cheapest likely win. **Applied 2026-10-17** (Yash: "do it"):
   `SkimRaceAIConfig_Regatta.asset` got `SkimHeight` -6.498 plus the learned look-ahead, mass margin and stick gain.
   **Re-learned 2026-10-25:** races written 23/24 before the recorder fix now count as finished, so the three fastest
   finished races changed:
   - `SkimHeight` -5.948 (same face);
   - `LookaheadSeconds` 1.03;
   - `LookaheadMin` 70;
   - `StickGainPerDegree` 0.025.

   See `LEARNED_FROM_FLIGHTS.md`. Waiting on the editor race against the old AI (checklist entry "Regatta Squirrel
   AI skims the ring face").
2. **Port the racing line** (`RACING_LINE.md` §3, task T-20261015-7fc3): the averaged faster-half line per leg as the
   pilot's target, with the face above.
3. **Record the AI seats too** with the same recorder, so every AI race is a flight file and the AI's speed, boost,
   hits and leg times sit beside the human's.
4. **Score changes offline**: extend `Tools/Build/skimrace_sim_harness` to Regatta's course (`RegattaCourse` already
   runs headless in `regatta_course_harness`) and tune for held speed and fewer hits over hundreds of seeded races.
5. **Difficulty from data**: Hard's reaction from the measured human lag; Easy and Medium make mistakes where people
   make them (`RACING_LINE.md` §3).
6. Keep flying the `RACING_LINE.md` §4 captures (`rail`, `drift`, `abilities`, `hard`, other intensities).

## 9. The other Regatta AIs: Rhino, Dolphin, Manta, Urchin, Stoat (2026-10-17)

Yash: "move forward with doing the same for the other 5 AIs that are allocated for Regatta. I will play those as well
and record data of their flight styles." The Squirrel learns its skim line (`learn_from_flights.py`, §8). The other
five of `REGATTA_AI_ROADMAP.md`'s six fly the shared gate steering plus ONE drive per hull that decides when to spend
its speed verb; **`Tools/Build/learn_drives_from_flights.py`** learns each drive's numbers from how a person spends
the same verb, measured exactly as the AI measures it (`AIBoostContext`: the angle between the course and the target
ring, the larger stick axis, the runway = distance / speed).

| Hull | The AI decides with | What your flights teach it | Written as (Regatta only) |
|---|---|---|---|
| Rhino | `RhinoAIBoostPolicy` (`RampBoostAIPolicySO`) | when you start the ramp (lined up within, stick under, runway left), when you stop it, your steering while ramping, your shortest ramp and rest, and `cornerSpeed` (your median speed at each ring you thread) | `Resources/AIBoostPolicies/RhinoAIBoostPolicy_Regatta` |
| Dolphin | `DolphinAIBoostPolicy` (`ChargeBoostAIPolicySO`) | when you discharge, and at what charge (the boost meter, recorded as `m0..m3` with `meterNames` and `boostMeter`); reported only: `redriftShareOfPeak`, the speed your next drift froze over the discharge's top speed (the AI drifts again at the peak by design, 2026-10-17) | `…/DolphinAIBoostPolicy_Regatta` |
| Manta | `MantaAnalogTurnBoostExecutor`'s Soar bands | the stick band you Soar flat inside, the yaw band past which you split the triggers: fitted to the executor's own trigger formula | `…/MantaSoarAITuning_Regatta` (`MantaSoarAITuningSO`) |
| Urchin | `RegattaController`'s rail riding + `UrchinAutopilotConfig` | how much of the race you ride, and on your own rail: **report only** (its Regatta riding numbers are scene fields) | nothing |
| Stoat (on the card 2026-10-17) | three autopilots: `StoatModeAction` (when to switch to Time mode), `StoatDipoleConfig` (Mass mode's field dipole), `StoatSlingConfig` (Time mode's catch sling) | the angle you switch to Time mode at; Mass mode: your squeeze, the ring's distance when you press, how long you hold, how often, how far round you let it carry you; Time mode: the turn and distance you sling at, how often, your squeeze, where the ring is when you let go. Needs the new `timeMode` column | `StoatModeAction_Regatta`, `StoatDipoleConfig_Regatta`, `StoatSlingConfig_Regatta` (read through `AIModeTuning`, autopilot numbers only) |

**Every other mode is untouched.** A variant is looked up per mode, falling back to the hull's own asset:
`AIBoostPolicySO.ForMode` (used by `AIPilot` where it builds the drive) and `MantaSoarAITuningSO.LoadForMode` (used by
the Manta executor, asking `AIPilot.TryGetGameMode`). A policy variant copies the hull's asset and overrides only fields
that asset has, so it survives either Rhino ramp drive (`cece/relaxed-hopper-r62hbd` carries a second, older
`RampBoostAIPolicySO`; both share the six engage / release fields). Nothing is written for a hull below 3 finished
flights; it learns from its fastest 3, like the Squirrel. Seconds the AI held your hull are measured separately as "the
AI today", so flipping the Third Eye's You / AI to AI for a lap gives a side-by-side.

**Checked:** `learn_drives_from_flights.py --self-test` (synthetic pilots with known rules: the Rhino's start / stop
rule recovered within a degree and 0.02 stick, a random pilot rejected as a negative control, the Dolphin's spend charge
0.80, the Manta's bands 0.50 / 0.80 exactly; under 3 finished races, unfinished races and AI-held seconds write
nothing); `RegattaDriveTuningTests` (no variant = the hull's own policy; the Manta trigger formula). On 2026-10-17 the
only flights of these hulls are 5 Urchin (3 by `belac`, 2 by Yash; 2 finished; report only: rides 87% of the race, 94% on
its own rail); no Rhino, Dolphin, Manta or Stoat flight exists yet, so nothing has been written.

**What to fly** (Regatta I1 first, the course the Squirrel data is on; store every race):

| Hull | Races | Fly it | Also |
|---|---:|---|---|
| Rhino | 5+ to the finish | ramp on every straight you trust, let go before the corners | in one race, Third Eye ▸ Pilots ▸ **AI** on your hull for a lap, then back |
| Dolphin | 5+ | drift to charge, discharge on the straights | the same AI lap |
| Manta | 5+ | Soar on both triggers, pivot with one in the hairpins | the same AI lap |
| Urchin | 5+ | ride your own rail as much as you can | the same AI lap |
| Stoat | 5+ | race it your way: Mass mode's dipole on the straights, Time mode (B) slings into the hard corners | the same AI lap |

Then `python3 Tools/Build/learn_drives_from_flights.py --write` writes the variants and `LEARNED_DRIVES.md`; race the
learned AI against the old in the editor (delete one variant asset to compare).
