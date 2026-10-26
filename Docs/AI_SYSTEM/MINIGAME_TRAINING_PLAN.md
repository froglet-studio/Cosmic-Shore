# Training the AIs through each vessel's own racing minigame

**Decided by Yash, 2026-10-26.** This replaces "train every AI in Regatta" (`REGATTA_AI_ROADMAP.md`). Yash's words:

> "It's not through Regatta, but it's through creating some mini games, some fun racing mini games of them or modifying
> the existing mini games, racing mini games especially of them. On basis of four intensity level, making them super fun
> and also intense to play according to intensity level. [...] In case of Dolphin, we should keep playing that Dolphin
> minigame, Switchback [...] in different intensity levels with some AIs dolphins as well, and through my repetitive
> gameplay you can collect the data on how I'm navigating. Just like how we train squirrel and stoat. [...] If we have
> four vessels or three vessels, or even two vessels trained to their maximum intelligence in their respective
> minigames [...] then through that data we can use some elemental changes and others to create the regatta entirely
> [...] Regatta should be a racing arena game where every vessel should have some meaning and fun and thrill and
> advantage and disadvantage to win or lose the game."

## 1. The loop, per vessel

1. **One racing minigame per vessel**, four intensities. The minigame is where the vessel's verbs matter most, so a
   person's play there shows how the vessel is meant to be flown.
2. **The intensities teach, then test.** I1 is basic and teaches the vessel's verbs one at a time, as the intensity
   ladder does in every other mode. Each level adds one demand. I4 is intense and fun for someone who has mastered it.
3. **People play it over and over**, with AI rivals of the same hull on the grid. Every race is recorded automatically
   (`HumanFlightRecorder`, every mode). Since 2026-10-25 the record includes each AI's state (`rivalState`), so a
   recording shows how the person flies and why each AI did what it did.
4. **The learner turns the races into AI numbers for that minigame, on its own** (the hourly AI learning sweep,
   `AUTO_LEARNING.md`, runs `learn_drives_from_flights.py --mode <Mode> --write`; nobody runs it by hand). It writes `<asset>_<Mode>` variants that
   only that mode loads (`AIBoostPolicySO.ForMode`), and reports flights per intensity. It needs three finished races
   of a hull before it writes. The Squirrel (Skim Race, `learn_from_flights.py`, per-intensity `SkimRaceAIConfig_I1-I4`)
   and the Stoat are the worked examples.
5. **Repeat until the AI races like the person at every intensity.** The test is the AI rival's own pace in the
   recordings (`regatta_rival_report.py`, extended to the minigame).
6. **Then Regatta is rebuilt from what the trained vessels can do** (§4).

## 2. Which minigame trains which vessel

From `VESSEL_MODE_CHART.md` §4 (live cards). The racing minigame is the one whose score is a race.

| Vessel | Racing minigame | Shape | State (2026-10-26) |
|---|---|---|---|
| **Dolphin** | **Switchback** | open gate race, random gates | **First.** 6 hand-flown races (I1 2, I2 4, all finished). The steering fix, the I1-I4 ladder (§3.3) and the first learned numbers are in (2026-10-26); the AI's pace needs new races |
| Squirrel | Skim Race | skim the track's rails | Trained (`learn_from_flights.py`, per-intensity configs) |
| Stoat | Warpline (also Slingshot) | gate race, field pairs | 4 hand-flown races (I1-I2). AI on hold for Garrett's configuration |
| Rhino | Headlong | lapped gate race | none yet |
| Manta | Redline | gate race | none yet |
| Urchin | Skein | gate race on rails | none yet |
| Sparrow | Breakwater | gate race through breakwaters | 7 hand-flown (I1, I4). The steering fix and the rocket that clears the way are in, the rocket's range and aim learned from Yash's shots (§6) |
| Butterfly | Waystation | fold race | 2 (Caleb, I1). The AI threaded 0 rings (before the fold fix) |
| Scarab | Scarab Scramble / Tollway | goal race / toll race | none yet |
| Serpent | **none** | - | no racing minigame of its own yet: one has to be made or chosen |

## 3. Switchback first (Dolphin)

### 3.1 What tonight's races show (Yash, 2026-10-26, I2, two Medium AI Dolphins each)

- Yash finished all four: 80, 82, 132 and 221 s. The AI Dolphins threaded **0-2 of 20 rings**.
- They are not frozen: about 65 u/s, discharging 4-14% of the time, drifting 10-35%. But they are in the **Through**
  phase (committed to a gate) 58-71% of the race and still miss its 28 u radius mouth.
- This is the defect Regatta fixed on 2026-10-17. A drifting Dolphin locks its course on the gate's "through" point,
  which sits on the ring's AXIS, so a commit made off-axis passes beside the mouth. Regatta moved the through point onto
  the line through the ring's centre (`GateRaceController.ArmRacer`, `aiCommitFromApproachPoint`), and later added a
  re-attack after a missed ring. **Turned on for Switchback on 2026-10-26** (`SwitchbackController.AICommitFromApproachPoint`,
  Yash's call for the training plan; ticket T-20261015-cb8f covers Garrett's other gate races).
- `learn_drives_from_flights.py --mode Switchback` already has three finished races to learn from (I1 and I2 mixed).
  Measured against the AI's numbers, the person:
  - starts a discharge lined up within 27 degrees, not 12;
  - needs only 0.9 s of runway, not 2.5;
  - spends the charge at 0.82, not 0.95.

### 3.2 The intensity ladder before 2026-10-26

Intensity was the course alone (`SWITCHBACK.md`, "Intensity is the COURSE"):

| | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| Mouth diameter | 144 | 56 | 22 | 8.6 |
| Max corner | 45° | 50° | 55° | 60° |
| What it asks | nothing new | threading | precision | precision |

The Dolphin's verbs are skim (bank charge), drift (carve a turn its 110°/s cannot make) and discharge (spend it down a
straight). None of them was taught or demanded by a level. Only the mouth shrank, and I2 was a cliff (72 → 28 u radius).

### 3.3 One new demand per level (built 2026-10-26)

| | I1: Learn to fly | I2: Carve | I3: Charge economy | I4: Gauntlet |
|---|---|---|---|---|
| Teaches | steering through gates | the drift | skim, then spend | all three, under pressure |
| Mouth (radius) | 72 (as now) | 48 | 32 | 20 |
| Course | a smooth flowing arc, gentle corners (≤ 40°: 30° failed every seed in the 480-1080 shell), long legs | **switchbacks**: pairs of 90-150° hairpins only a drift makes | skim ribbons (prism walls) along some legs to bank charge, long straights after them to discharge | tight hairpins, short legs, danger prisms near the racing line |
| Rivals | AI Dolphins on Easy | Medium | Medium / Hard | Hard |
| Fun | flow, speed, no failure | the carve | timing the release | risk vs reward: the fast line is the dangerous one |

Why these numbers: the I4 mouth today (4.3 u radius against a 2.86 u hull) leaves no room for a drift. A person
cannot carve through it, so the ladder ends in precision, not mastery. The proposed mouths keep the same order (each
level tighter) without the cliff.

Built as proposed, with the exact numbers in `SWITCHBACK.md` ("Intensity is the COURSE"): the hairpins in
`SwitchbackCourse` (`SwitchbackCourseTests`), the I3 walls and I4 danger prisms in `SwitchbackFeatures` /
`SpawnableSwitchbackFeatures` (`SwitchbackFeaturesTests`), wired by `author_switchback_assets.py`. The rivals' difficulty
per level is still the player's pick in the launch panel. It is a redesign of Garrett's mode: he can re-tune any of it.

### 3.4 Done 2026-10-26: the AI Dolphins race Switchback (decisions 1 and 3 of §5)

**Why they threaded nothing.** Measured with `Tools/Build/regatta_rival_report.py --mode Switchback` (it now reads any
mode), the AI Dolphins circled gate 0 for the whole race at I2: 0 rings, 22-52 s of circling each. At I1 they got 0-11.
Raced offline on the six recorded courses (new `Tools/Build/switchback_dolphin_sim.py`: the gate steering, the commit
drift, the AI nose lag, the charge economy), two defects showed, and the Regatta steering fixed only the second:
1. **The approach point lay outside the commit radius** (300 u lead against a 260 u commit on any leg over 650 u). The
   Dolphin's 35 u circle kept it 265-335 u out, so it never committed. Now, with the Regatta steering on, the approach
   point stays at most 0.8 of the commit radius (`GateRaceController.ApproachLeadFor`).
2. **A drift committed off-axis locked a line beside the 28 u mouth** (§3.1). This is the Regatta fix.

**What changed:**
- `SwitchbackController.AICommitFromApproachPoint` = true: the Regatta steering, which now carries the approach-point
  cap.
- `DolphinAIBoostPolicy_Switchback` written from Yash's races (`LEARNED_DRIVES_Switchback.md`): discharge lined up
  within 27.4 deg (was 12), with 0.87 s of runway (2.5), at 0.82 charge (0.95).
- Tests: `SwitchbackAISteeringTests`. Other gate races keep their scene switch (Headlong is the negative control).

**The model, seconds for the 20 gates** (Hard, no rivals; the person's time in brackets):

| Course | Today | The fix, the AI's own charge numbers | The fix, learned from Yash |
|---|---|---|---|
| I1 (101) | 0 rings | 104.8 | 100.9 |
| I1 (88) | 0 rings | 115.5 | **88.8** |
| I2 (221) | 0 rings | 190.8 | 169.0 |
| I2 (80) | 0 rings | 184.0 | 197.3 |
| I2 (82) | 0 rings | 166.6 | 180.2 |
| I2 (132) | 0 rings | 188.8 | 181.5 |

**Still open, the next step for the Dolphin:** at I2 the boosted Dolphin (about 250 u/s) misses the 28 u mouths 9-18
times a race through the AI nose lag (`Arcade/REGATTA.md` §6.4.1), so it is still 2x slower than Yash there. The
learned numbers mix I1 and I2; the Squirrel has one config per intensity, the Dolphin's drive does not yet. More races
per intensity come next; the I1-I4 ladder (§3.3, decision 2) is built.

### 3.5 The first races after the fix (Yash, 2026-10-26, 01:40-02:03, before the new ladder)

`regatta_rival_report.py --mode Switchback`, the AI Dolphin rivals (Medium) in the six races flown after the fix
landed (I1, I2, I4 on the old ladder):

| | before the fix (four I2 races) | after (I2 and I4) |
|---|---|---|
| Rings threaded, of 20 | 0-2 | 11-20 (one finished an I4 race) |
| Boosted | 5-16% of the race | 22-33% |

**One AI still flew away**, in the 02:03 I4 race: it missed gate 0's 4.3 u mouth, broke off, and came out of the
break-off with its target exactly behind it (179-180 deg). `AIPilot`'s stick scales with sin(error), which is zero
dead astern, so it wrote no turn and flew 14,600 u away for the rest of the race. **Fixed for every AI in every mode:**
`AIPilot.TurnHardIfBehind` turns at full stick toward any target more than 90 deg off the nose (the old stick was already
full past about 120 deg; only the dead zone near 180 deg changes). Tests: `SwitchbackAISteeringTests.Target*`.

### 3.6 One drive per intensity (the AI learning sweep, 2026-10-25 23:30 UTC)

**What the races after both fixes showed.** Yash's three races at 04:04-04:09 (+05:30; 22:34-22:39 UTC) are the
first flown after the steering fix and `TurnHardIfBehind` (22:12 UTC). `regatta_rival_report.py --mode Switchback
--since "2026-10-26 03:50"`: the AI Dolphins (Medium) threaded 10 of 20 rings at 8.8 rings a minute and finished
none of six; Yash ran 17-20 a minute at I1 and 10 at I4. One I1 rival raced at 17 a minute with no circling; the I4
ones circled 23 s and sat 50 s on gate 4.

**Why numbers alone could not fix it.** The learner takes the fastest three finished races, and those are always the
easiest intensity's: the mode-wide `DolphinAIBoostPolicy_Switchback` was I1's numbers exactly (a 36 deg discharge cone,
0.54 s of runway, spend at 0.96 charge). Raced offline on the recorded courses (`switchback_dolphin_sim.py`, the Regatta
steering on), those numbers missed 31 of the I4 course's 20 u mouths and took 232 s; the I4 races' own numbers (52 deg,
1.1 s of runway, spend at 0.50) missed 12 and took 148 s, and at I1 they stayed within a few seconds of I1's own.

**Built.** `AIBoostPolicySO.ForMode(policy, mode, intensity)` loads `<policy>_<mode>_I<n>` first, then the mode's
variant, then the hull's policy; `AIPilot` passes `GameDataSO.SelectedIntensity`. `learn_drives_from_flights.py` learns
each intensity of a `PER_INTENSITY_MODES` mode (Switchback) from its own races and writes `_I<n>` variants started from
the mode's variant (MIN_FINISHED still applies, so I3, with no races, flies the mode variant). Written:
`DolphinAIBoostPolicy_Switchback_I1`, `_I2`, `_I4`. The I2 and I4 numbers still mix the old ladder's races (28 u and
4 u mouths) with the new one's; the next races at each intensity replace them. Tests:
`RegattaDriveTuningTests.ForMode_AnIntensityWithItsOwnVariant_FliesIt_ElseTheModes`. Not run in Unity
(`Docs/UNITY_VERIFICATION_CHECKLIST.md`).

### 3.7 Each intensity learns from its current course; the mode variant from every intensity (2026-10-26)

Two holes in §3.6's per-intensity learning, closed in `learn_drives_from_flights.py` (`AUTO_LEARNING.md` §7):
- **A retuned ladder is a different race.** The newest flight's mouth radius now decides an intensity's course
  (`on_current_course`), and races on the course it replaced are left out. Switchback's three I4 races on the 4.3 u
  mouths no longer teach I4; the one on the new 20 u course does not reach three, so `_I4` stays as §3.6 wrote it
  until it does. (Offline it is still the best I4 drive the learner has made: 144.7 s on the new course.)
- **The mode variant is what I3 flies**, and learned from the three fastest races it was I1's numbers again. In a
  `PER_INTENSITY_MODES` mode it now learns from the fastest race of each intensity (`fastest_each_intensity`): spend at
  0.92, a 24.9 deg cone, 0.84 s of runway. On the I4 course that takes 197.8 s against 231.6 s for I1's numbers. No
  other mode's learning changes (Regatta's, Bloomrush's and Breakwater's numbers are identical before and after).

The offline model (`switchback_dolphin_sim.py`, Regatta steering, the charge drive; seconds summed over the courses,
an unfinished race counted as 300 s plus 10 per ring left; the person's sum in brackets):

| Drive | I1, 6 courses (501) | I2, 5 old-ladder courses (618) | I4, the new course (120) |
|---|---:|---:|---:|
| One Switchback variant (before §3.6) | 488.8 | 930.1 | 163.0 |
| I1's numbers everywhere | 464.6 | 900.8 | 231.6 |
| **Each intensity's own** (`_I1`, `_I2`, `_I4`) | **464.6** | **834.0** | **144.7** |
| The mode variant learned from every intensity (I3) | 491.6 | 1001.7 | 197.8 |

**What still costs the Dolphin its I2 and I4 races** is the discharge carried into the mouth: the AI misses 64 of
I2's 28 u mouths across five races and 12 of I4's in one, where a Dolphin that never discharges misses none (and is
twice as slow at I1). Most misses come at cruise speed, after an earlier overshoot and break-off: the speed starts the
cascade, the steering after it finishes it. Two leads, offline only, for when the new-ladder races are in:
- the I4 numbers (spend at 0.50, a smaller and earlier pulse) took 560.8 s at I2 against the I2 variant's 834.0;
- letting each discharge run out instead of drifting again at its peak (the drift freezes the peak speed) took I2 to
  548.5 s with no miss, and the I4 course to 126.0 s. The game always re-drifts at the peak
  (`ChargeBoostAIPolicySO`), so this would be a code change; the model's `"peak": False` what-if measures it.

Neither is shipped: the next races at I2 and I4 on the new ladder decide.

## 4. The end state: Regatta rebuilt from the trained vessels

Once two to four vessels race at their best in their own minigames:

- **Regatta is designed from their measured skills**, not tuned blind. Each trained AI shows what its vessel is good
  at: the Dolphin's carve, the Squirrel's skim, the Stoat's warp, the Urchin's rail. Regatta's course gets sections
  that favour each, so every vessel has an advantage somewhere and a disadvantage somewhere else.
- **Elemental changes balance it**: `StartingElements` per hull and intensity, as `regatta_balance.py` does today, but
  set from the trained AIs' real times instead of a model.
- What Regatta already has stays: the race lines per hull (learned from people), the per-AI recording, and the AI
  fixes. Its AI numbers become the trained minigame numbers.

## 6. The AIs use their abilities to win their own races (Yash, 2026-10-26)

> "The sparrow can use its shooting ability to clear up space in front of you for it to race down the track. This way
> we have to train the sparrow AIs. And also other AIs should be able to use their ability in order to gain advantage
> on racing, in their own minigame races."

**How an ability becomes a racing decision.** Every AI hull lists abilities on its `AIPilot` (`abilities`: start, hold
`Duration`, rest `Cooldown`, forever), a blind timer. A hull's drive (`AIBoostPolicySO`, picked per mode by
`ForMode`) can take an ability off that timer (`AIBoostDriver.Drives`) and fire it when it helps. That is the
pattern: one drive per hull per minigame, its numbers learned from people's races (§1).

| Vessel / minigame | The ability that wins the race | AI today |
|---|---|---|
| **Sparrow / Breakwater** | the **rocket** (SkyBurst): a 50 u hole in the station's danger weave around the 18 u eye; the **guns** saw the weave and refill the bay | **2026-10-26:** the rocket is fired at the station, once per station, nose on it, inside its 229 u reach, paid for (`HoldBoostAIPolicySO` "clear the way", on only in `SparrowAIBoostPolicy_Breakwater`). Guns: the timer, 79% of the race along the nose (Yash held them 95-100% in his 10-20 races). **Boost, 2026-10-26:** on the legs, let go 1.25 s before each station (`BREAKWATER.md` "The boost"; offline 79-82 s a lap against 110-131 s, no station clipped) |
| Dolphin / Switchback | drift + charge discharge; the crystal blast on rivals | the charge drive, learned per intensity (§3.6-3.7); no crystals: a racing AI flies the gates only (§6.1) |
| Squirrel / Skim Race | skimming the rails | trained (`learn_from_flights.py`) |
| Stoat / Warpline | the field pair's warp | on hold for Garrett's configuration |
| Rhino / Headlong | the ramp; ramming costs it nothing | the ramp drive (Regatta-tuned) |
| Manta / Redline | Soar | the Soar executor's bands |
| Urchin / Skein | riding its own rail | `UrchinAutopilotDriver` |
| Scarab / Scramble, Tollway | Snap Dash | dashes on straights (`SnapDashAIPolicySO`) |

### 6.1 A racing AI flies the waypoints, never a crystal (Yash, 2026-10-26)

> "While playing any racing game mode, AI should not go for crystals. It should only focus on waypoints. If the
> crystals are the waypoints, then go for crystals. [...] I figured out the AI dolphins were collecting crystals more
> than going through the waypoints to win the race."

Every gate race (Switchback, Breakwater, Headlong, Redline, Warpline, Slingshot, Skein, Waystation, Grizzly Time,
Thresher Time, Wrasse Run) let an AI swing up to 200-220 u off its line for any crystal near its route
(`aiCrystalDetourSlack`, serialized in each scene); only Regatta had opted out. Taking a crystal skips the approach to
the next mouth, so in Switchback a Dolphin that took one usually missed the gate after it. Now
`GateRaceController.AIDetoursForCrystals` is false for every gate race, in code: the AI flies the course's waypoints
and nothing else, whatever the scene says. A mode whose race needs a crystal (one that IS a waypoint, or one the vessel
cannot finish without) overrides it to true; none does today. The cost: an AI Dolphin no longer fires its crystal blast
in a race. Test: `RaceAIWaypointsOnlyTests`.

Modes whose crystals ARE the objective keep them: Skim Race (its crystal count ends the race), Tollway (an omni
crystal forges the ball), The Bends, Rampage and the crystal modes. Sirocco already steers for mass, not crystals.

**Breakwater's steering, first.** The AI Sparrows threaded about 1 station a minute in Yash's five races, circling 56%
of the race: the 280 u approach point lay outside the 240 u commit radius, Switchback's defect. `BreakwaterController`
now flies the Regatta steering too. Offline on the five courses (`switchback_dolphin_sim.py`, `SPARROW_BREAKWATER`):
stuck on three of five and through the danger weave at 12-13 of 15 stations on the other two before; every station
threaded through the eye after.

**The rocket, learned from Yash's shots (2026-10-26).** His Breakwater races fired the rocket 12 times, from 138-354 u
of the station (p10 / p90) and about 16 deg off the nose. `learn_drives_from_flights.py` now measures any policy's
extra press the same way (`measure_presses`: the press's distance to the ring and its nose angle) and wrote them into
`SparrowAIBoostPolicy_Breakwater`: `rocketMinRange` 138.3 (was 60), `rocketMaxRange` 353.6 (220), `rocketAimDegrees`
15.9 (4). It also counts Breakwater's 29 rings as station 0 once and stations 1-14 twice (`ring_index`); before, the
lead-in shifted every ring after the first lap and no Breakwater race measured. The guns stay on the timer: Yash held
them 56-73% of a race, which is what the timer already gives. The same loop then gives each vessel above its own
ability rule in its own minigame.

## 5. What is done, what is next

- **Done (2026-10-26):** the learner reads any mode (`--mode`). The plan is recorded here. Then, on Yash's yes, all
  three decisions:
  1. The Regatta steering is on in Switchback, with the approach-point cap (§3.4).
  2. The I1-I4 ladder of §3.3 is built. Its I2 mouth is 48 u (was 28), which is where §3.4's nose-lag misses were.
  3. The first Switchback Dolphin numbers are written (§3.4). They came from I1 and I2 on the old ladder, so the next
     races re-learn them.
  None of it has been run in Unity (`Docs/UNITY_VERIFICATION_CHECKLIST.md`).
- **Done (2026-10-26, later):** a drive per intensity (§3.6), learned from each intensity's current course with the
  mode variant from every intensity (§3.7); the Sparrow's rocket learned from Yash's shots (§6).
- **Next:** Yash plays Switchback at each intensity, repeatedly. Nothing else: every race is relearned from within the
  hour by the AI learning sweep (`AUTO_LEARNING.md`), which also checks the AI's pace and fixes what numbers cannot.
  Then the next vessel.
