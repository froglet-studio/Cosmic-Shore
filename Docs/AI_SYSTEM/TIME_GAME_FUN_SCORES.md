# Time minigame fun scores (estimate, 2026-10-27)

Garrett, 2026-10-27: make every vessel's Time game fun, because it is what makes the vessel fun to fly between
places on a voyage. Score each one, then raise the floors.

**Scoring:** six dimensions, each 0-10. Fun is their mean; Floor is the lowest one.
- **Verb:** is the hull's own speed verb the way to win?
- **Flow:** is the next target always clear, with no dead air?
- **Ladder:** does each intensity add one demand, with no cliff?
- **Feedback:** is there a moment-to-moment reward?
- **Rivals:** do the AI rivals race and finish?
- **Fairness:** is the game free of soft-locks, and do people finish it?

These are estimates read from the mode docs, the code and `VESSEL_MODE_CHART.md` §3 (the recorded flights). Nobody has
played them in the editor for this report. Where there are no flights, Rivals and Fairness are scored low: unknown counts
against a mode, not for it.

| Vessel | Time game | Verb | Flow | Ladder | Feedback | Rivals | Fairness | Fun | Floor |
|---|---|---|---|---|---|---|---|---|---|
| Dolphin | Switchback | 7 | 6 | 8 | 4 | 5 | 6 | **6.0** | 4 Feedback |
| Stoat | Warpline | 9 | 7 | 5 | 4 | 3 | 7 | **5.8** | 3 Rivals |
| Sparrow | Breakwater | 6 | 6 | 7 | 4 | 4 | 7 | **5.7** | 4 Rivals/Feedback |
| Manta | Redline | 9 | 7 | 8 | 4 | 2 | 3 | **5.5** | 2 Rivals |
| Butterfly | Waystation (clusters only, before 2026-11-01) | 9 | 6 | 7 | 4 | 3 | 4 | **5.5** | 3 Rivals |
| Tiger Moth | Waystation, the Drop Tower course (est. 2026-11-01; replaced the row above, `Docs/Studios/TIGER_MOTH_TIME.md`) | 9 | 7 | 7 | 5 | 3 | 4 | **5.8** | 3 Rivals |
| Thresher | Thresher Time | 6 | 7 | 7 | 5 | 4 | 4 | **5.5** | 4 Verb/Rivals |
| Rhino | Headlong | 9 | 7 | 7 | 3 | 1 | 2 | **4.8** | 1 Rivals |
| Grizzly | Grizzly Time | 8 | 7 | 7 | 3 | 2 | 2 | **4.8** | 2 Rivals |
| Urchin | Skein | 8 | 5 | 5 | 4 | 3 | 3 | **4.7** | 3 Ladder cliff |
| Wrasse | Wrasse Run | 7 | 6 | 6 | 5 | 2 | 2 | **4.7** | 2 Rivals |
| Squirrel | Skim Race | 5 | 7 | 6 | 5 | 2 | 2 | **4.5** | 2 Fairness |
| Scarab | Scarab Scramble (now its Mass card) | 2 | 6 | 5 | 7 | 3 | 3 | **4.3** | 2 Verb |
| Scarab | **Scarab Rally** (new) | 8 | 6 | 7 | 4 | 2 | 3 | **5.0** est. | 2 Rivals (unplayed) |
| Serpent | **Serpent Time** (new) | 7 | 7 | 6 | 4 | 2 | 3 | **4.8** est. | 2 Rivals (unplayed) |
| Brittle Star | **Brittle Star Time** (new) | 7 | 7 | 7 | 4 | 2 | 3 | **5.0** est. | 2 Rivals (unplayed) |

## What the floors say

1. **Design is mostly good. Proof is missing.** Verb scores 7-9 for nine of the twelve games. Most floors sit on Rivals and
   Fairness, because almost nobody has raced these modes. Flying each one at I1 and I4 with AI rivals is the cheapest way
   to raise them. The races are recorded automatically and the daily AI learning sweep learns from them
   (`AUTO_LEARNING.md`).
2. **Feedback is a 3-5 everywhere.** No gate race had a sound for threading a ring.
   - **Done (this change):** `GateRaceController` now has two FMOD slots, `gateThreadedEvent` and `courseFinishedEvent`,
     played for the local pilot only.
   - They come from the same live progress that lights the next ring, so they stay quiet on a rollback.
   - They ship EMPTY (the FMOD convention), so audio must author the two events and wire them on each mode scene.
   - Every ring race inherits them: Switchback, Warpline, Slingshot, Headlong, Redline, Skein, Breakwater, Waystation,
     Thresher Time, Grizzly Time, Wrasse Run and Regatta.
3. **Three design floors need an owner's call** (not changed here):
   - **Scarab Scramble:** the Scarab's throttle never decides a goal. Two options:
     - make a Scarab gate race its Time card;
     - make a full-throttle strike carry the ball further. That changes the ball shared with Astro League.
   - **Skein:** I3 and I4 run 127 s and 190 s against the ~90 s target, and the next ring is almost never on screen.
     - Cut the laps from 4/6 to 3/4. Laps and ring spacing are tied together (`SKEIN.md`), so this is not a one-number
       edit.
     - Pin every third ring at I1, so the rail is taught rather than optional.
   - **Skim Race:** I3 and I4 change the lap length and halve the mouths at once, and nobody has finished a race (0 of 7).
     - Proposed: `intensityFourMouthFraction` 0.5 → 0.75.
     - Proposed: laps [3,3,2,2] → [2,2,1,1], through the End Game Conditions window only.
4. **AI numbers belong to the learning sweep** (Yash). Proposals, for him to confirm:
   - **Headlong:** turn on `AICommitFromApproachPoint`, the fix that made the Switchback AI race. Headlong is the plan's
     negative control today.
   - **Butterfly:** raise `aiFoldAimDegrees` from 12 to 18.
   - **Wrasse:** raise `aiBoostRingDistance` from 180 to 250.
   - **Thresher:** lower `plantMinTurnDegrees` from 70 to 55.
5. **Serpent has no Time game.** The cheapest one is a clone of Redline or Thresher Time on `HeadlongCircuit`, with the
   course cut for pellet-boosted speed. Check enum ids on `bleeding-edge` and the open branches first: `SerpentCharge`
   holds 67 on an in-flight branch.

Re-score a game after people have raced it. The chart's §3 rows are what move Rivals and Fairness.

## 2026-10-27, round 2: the missing Time games

Garrett asked for a ball-free Scarab ring race, a Serpent Time game, and either the other missing Time games or a
note on what blocks each one.
- **Built:** Scarab Rally (72), Serpent Time (73), Brittle Star Time (74). Their mode docs are under
  `Assets/_Scripts/Controller/Arcade/`. All three are authored headless and nobody has flown them yet, which is why
  their Rivals and Fairness scores are low.
- **The Scarab's switch:** it is now always placed around an elemental crystal, in every game (`SCARAB.md` §5).

**Still without a Time game, and what blocks each:**

| Vessel | What blocks it | Cheapest shape |
|---|---|---|
| Shrike | Its Time verb is a tether that anchors on blocks, so a course needs block pillars to anchor on; its autopilot is untested there | Warpline-style circuit plus pillars |
| Honeyguide (+ Ratel) | The second seat needs an AI or an empty-seat rule | Breakwater clone (it flies the Sparrow airframe) |
| Orca | It is not confirmed that the helm (not only the gunners) has an autopilot | Redline clone |
| Angler | Its speed comes from the school it tames, so a course needs fauna on it; it has no AI | Needs a design decision |
| Goby (+ Pistol) | The Time verb belongs to the Pistol seat, but the Goby flies the race | Needs a design decision: which seat races |
| Jacks | Racing gates conflicts with herding | Needs a decision: hull flight or herding |
| Starling | The pilot does not steer (a passive swarm), so a gate race contradicts the vessel | Needs a different kind of Time goal |
| Meerkat | Already has one, Burrow Run (71), on `Ys-bleeding-edge` | - |
| Termite, Falcon | Not live vessels (no ability map, not in the Vessel Changer) | - |

## 2026-11-07, round 3: the unowned floors, raised

Garrett: "solve all the problems with all of the time games ... find the floors that no one is working on and make
those incredible. if it is a problem with the vessel ... propose fixes to the vessel." Yash's (Dolphin, Manta, Stoat,
Thresher, Meerkat) and Shombith's (Angler, Brittle Star, Serpent) games were left alone; Squirrel's Skim Race is the bar.

| Vessel | Game | Change | PR | Verb | Flow | Ladder | Feedback | Rivals | Fairness | Fun (est.) |
|---|---|---|---|---|---|---|---|---|---|---|
| Rhino | Headlong | the stampede: hedges of mass to plough (I1), apex Time crystals (I2), thorn hedges with an escape window (I3), a Time-5 start with thorns where a clean exit reaches Momentum (I4); the AI takes thorn windows | #1796 | 9 | 7 | **9** | **5** | 2 | 2 | **5.7** |
| Urchin | Skein | I3 at 3 laps: rides 32 / 63 / **95** / 190 s, next ring on screen 100 / 58 / **25** / 0 % | #1798 | 8 | **6** | **7** | 4 | 3 | 3 | **5.2** |
| Shrike | **Thornline** (new, 79) | thorn to thorn on the Void Sling, a ring a leg; plain thorns and rim crystals (I2), snipes (I3), the smallest thorns (I4) | #1800 | 9 | 7 | 8 | 5 | 2 | 3 | **5.7** est. |

Each change is measured offline (Headlong: `headlong_stampede_model.py`, a reading pilot beats the best fixed plan by
1 / 4 / 4 % at I2-I4; Thornline: `thornline_course_harness/run.sh --sweep`, 17 / 29 / 36 % on the reload clock). None
has been flown; Rivals and Fairness stay low until people race them.

**Checked and left as they are** (ladders sound, generators pass `--check`; the floor is that nobody has raced them):
Scarab Rally, Breakwater, Grizzly Time, Wrasse Run, Waystation.

### Proposals for the vessels and their AIs (not changed: each is the owner's call)

| Vessel | What the Time game exposed | Proposal | Owner to ask |
|---|---|---|---|
| Scarab | The AI holds full throttle and never drifts or jukes, so it races Scarab Rally's hairpins on the full-throttle circle and cannot learn the mode's verbs | give the Scarab autopilot a drift on corners tighter than its ~102 u circle and a juke when the next mouth is off its line (input-only, through the replicated press) | Garrett (his #1786 is in the Scarab AI files) |
| Shrike | The autopilot presses Mass (detach and nest) after 3 s docked (`aiDockSeconds`), but a plain thorn's reload is 3.1 s at Time 0, so an AI leaves before the sling is ready and reloads at the undocked rate | raise `aiDockSeconds` past the docked reload, or detach only when the sling is ready and has no anchor | Garrett (the Shrike's designer) |
| Shrike | Whether `AIPilot` can hold a zero-radius ray on a 32 u block at 1,000 u (2°) is unmeasured; if not, the AI crawls in Thornline | measure in the editor first (Thornline's checklist step 7); the fix is the AI's aim, not bigger thorns | Yash (AI) |
| Rhino | The Headlong AI never reaches the ramp's top speed (the scorecard's Rivals 1) | Garrett's own #1790 turns on `AICommitFromApproachPoint`; #1796 only adds the thorn-window choice beside it | Garrett |
| Starlings | Hairpins take 2-3x as long as open water (the Starlings lab), which matters for Regatta | a turn-rate floor for the swarm's centroid, decided in the Starlings lab before Unity | Garrett |

### The four new vessels still without a Time game

Orca, Honeyguide, Goby and Jacks are Garrett's own, all from October. Their hulls fly networked (as the Wrasse's does in
Wrasse Run), but each one's second-seat trick runs on one machine only: the Orca's gunners and their bursts, the Ratel's
Dynamo board and its battery, the Goby's Pistol. A race built on those tricks would only work for the person flying.
One-paragraph designs, each built on what the hull does today:

- **Jacks, "Decoy Run"**: the Jacks' Time card IS travel: herding, a pull lays a decoy at a jack; flying, a pull flies the
  hull to it at 900 u/s (published, so every peer sees it). Rings in pairs across the cell: herd the jacks to drop the
  two decoys beyond the next rings, switch to flight, and pull through them. I2 moves rings out of the board's plane, I3
  needs Time 5's second decoy, I4 puts fauna in the way. Fully networked today.
- **Goby, "Perch Run"**: a Thornline cousin. The Goby perches and darts, and a dart into open space makes its own perch,
  so the course is rings at dart range with open water between, and the skill is how far each dart goes. Blocked on
  whether the dart is networked (`GobyPistolDuo` simulates locally).
- **Honeyguide, "Slip Line"**: rails of each domain between rings; the Slip zips along a rail whose domain the battery
  holds. The Ratel's patterns decide which rails you can ride. Blocked on the battery being replicated.
- **Orca, "Convoy"**: the helm's boost tank through rings, with walls only a gunner can clear. Blocked on bursts and
  prism breaks replicating (today they do not).

## 2026-11-07, round 4: the four new vessels get Time games

All four are Garrett's hulls. Each race is built on what the hull's own flight does, which every peer sees; the
second-seat tricks that run on one machine are used only where the owner's machine is the one that needs them. The
review page for all four: https://claude.ai/artifact/LGPLko2T267Ac2q1nn4YSE.

| Vessel | Game | The ladder (I1 / I2 / I3 / I4) | Reading the course saves (I1-I4) | PR | Verb | Flow | Ladder | Feedback | Rivals | Fairness | Fun (est.) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Jacks | **Decoy Run** (80) | doors / skewers / skewers on rolled flats / Time 5 knots with hairpins | 0 / 14 / 13 / 20 % | #1809 | 9 | 6 | 8 | 5 | 2 | 3 | **5.5** est. |
| Goby | **Perch Run** (81) | darts / hairpins (length) / coral (line) / reef with doubles | 7 / 22 / 25 / 37 % | #1812 | 9 | 7 | 9 | 5 | 2 | 3 | **5.8** est. |
| Honeyguide | **Slip Line** (82) | three rails / one coloured rail, stubs / leaning rails / chains | 0 / 1.1 / 0.6 / 0.9 % (the catch is unmeasured) | #1822 | 8 | 6 | 6 | 6 | 2 | 3 | **5.2** est. |
| Orca | **Breach** (83, was sketched as "Convoy") | open water / hard turns / danger walls before rings / both | 1.6 / 10.7 / 10.5 / 17.1 % | #1824 | 8 | 7 | 8 | 4 | 2 | 3 | **5.3** est. |

Vessel changes (all small, all Garrett's to confirm): the Jacks' take-off mark and the Goby's reach ring (instruments a
person already needed), and one AI race coach per hull (`IJacksRaceCoach`, `IGobyRaceCoach`, `IHoneyguideRaceCoach`,
`IOrcaRaceCoach`), inert outside its race. **Found:** the shipped Orca AI helm never burns in any mode; a default
gate-race coach from Breach's burn rule is proposed, not made. Slip Line's spending decision measured at about 1% and is
documented as such. None of the four has been flown; Rivals and Fairness stay low until people race them.

## 2026-11-11, round 5: the ten most-played vessels

Ranked from 518 recorded flights on the `origin/bugledger/*` branches (the analytics read key was unavailable). Each
vessel's Time game is upgraded in place by its own session; each adds its own row.

| Vessel | Game | The change | Reading the course saves (I1-I4) | Verb | Flow | Ladder | Feedback | Rivals | Fairness | Fun (est.) | Floor |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Manta | Redline (53) | **Soar lit from the gun**: since 2026-11-07 Soar is the Time level-5 upgrade and the card seeded no Time, so every Manta raced at 180 u/s; the card now starts at Time 6 (1104 u/s). 8 laps; the AI on a race line through the mouths' centres with a throttle floor by difficulty (every difficulty finishes every level offline, Legendary 60-91 s against an ideal person's 38-89); the heat: shock rings off clean and redhot threads, called streaks, lap splits, clean-lap chases, the redline (toasts 360-366, 9 empty FMOD slots). REDLINE.md §0 | 0 / 0 / -11 / +0.3 % (a miss loops back in ~1.4 s at Soar speed; the re-cut for the flown hull is next) | 9 | **8** | 7 | **6** | **5** | **4** | **6.5** est. (was 5.5) | 4 Fairness |
