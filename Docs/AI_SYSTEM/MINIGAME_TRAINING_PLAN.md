# Training the AIs through each vessel's own minigames

**Serves `AI_PLAN.md` step `time-arcade`** (Garrett's one AI plan, LOCKED 2026-11-02: one universal AI per vessel per element, trained in that element's arcade games over I1-I4; where this page and `AI_PLAN.md` disagree, `AI_PLAN.md` wins). **Decided by Yash, 2026-10-26, restated 2026-10-27 and 2026-11-01.** **Focus since 2026-11-01 (§10): no new vessels; each existing vessel's Time minigame, then the arena games. Yash flies the Dolphin in Switchback (§9) himself; the Stoat (Slingshot) and the Meerkat (Warpline) are the vessels Claude sessions build with him (§8).** This replaces "train every AI in Regatta" (`REGATTA_AI_ROADMAP.md`).

**The plan in one paragraph (2026-10-27).** The AIs are not trained in Regatta. Each vessel is trained in its OWN
minigames: first its **Time minigame** (the card whose genre petal is Time, usually its race: the Stoat's is Warpline),
then its other own minigames, racing or not. People pick a vessel and play its minigames over and over; every match is
recorded and learned from on its own (`AUTO_LEARNING.md`), so the AI keeps getting better there. Then the **arena
games** (the cards that seat several vessels: Regatta, Broadside, Brood Rush, Astro League) are **restructured from the
skills the vessels learned**: an arena game is a mix of those skills, so ANY vessel can play it and still fit. Regatta
itself may stay a track race or become something else; that is not decided. Until then the learning sweep reports
Regatta races and learns nothing from them (`ai_learn.HOLD`). Yash, 2026-10-27:

> "We are not using the regatta scene to train the AIs, rather we are using the AIs own mini-games, time mini-games, to
> train them. [...] we will pick each vessel, we will keep playing their own time mini-games, whether racing or other
> time mini-games. And through that they will keep improving in those mini-games, and then we will restructure the
> regatta, arena game mode based on their own skills. [...] arena games should be a mixed combination of the skills
> acquired by the vessels. So that we can use any vessel to play these arena games, and they will still be a good fit."

Yash's words of 2026-10-26:

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
4. **The learner turns the races into AI numbers for that minigame, on its own** (the daily AI learning sweep, 03:47 IST,
   `AUTO_LEARNING.md`, runs `learn_drives_from_flights.py --mode <Mode> --write`; nobody runs it by hand). It writes `<asset>_<Mode>` variants that
   only that mode loads (`AIBoostPolicySO.ForMode`), and reports flights per intensity. It needs three finished races
   of a hull before it writes. The Squirrel (Skim Race, `learn_from_flights.py`, per-intensity `SkimRaceAIConfig_I1-I4`)
   and the Stoat are the worked examples.
5. **Repeat until the AI races like the person at every intensity.** The test is the AI rival's own pace in the
   recordings (`regatta_rival_report.py`, extended to the minigame).
6. **Then Regatta is rebuilt from what the trained vessels can do** (§4).

## 2. Which minigame trains which vessel

From `VESSEL_MODE_CHART.md` §4 (live cards). Every vessel has one minigame per element; its **Time minigame** is the one
whose genre petal is Time (`ModeGenre`: a race, a goal race or a crystal race), and it is trained first. Its other own
minigames (Charge: pilots, Mass: making, Space: destroying) come after, and the learner already reads them (§1 step 4,
`AUTO_LEARNING.md` §5).

| Vessel | Time minigame | Shape | State (2026-10-27) |
|---|---|---|---|
| **Dolphin** | **Switchback** (Blast Run since 2026-10-29) | gate race, blocked gates cleared by the key crystal's blast | **First, Yash's own.** The Blast Run AI rebuilt in four steps on 2026-11-02 and its difficulty levels set to Yash's time bands (§9.1): offline I4 medians 140 / 129 / 120 / 112 s, Easy to Legendary. Learned charge drive per intensity (§3.6-3.7). Not yet run in Unity since the steps (the soak is handed to a PC session) |
| Squirrel | Skim Race | skim the track's rails | Trained (`learn_from_flights.py`, per-intensity configs) |
| **Stoat** | **Slingshot** (since 2026-10-27; Warpline before) | gate race, the catch orbit alone since 2026-11-02 (§11) | **Yash's own (2026-10-27).** Its 15 Warpline races taught the pair and sling numbers; they start its Slingshot variants (`StoatDipoleConfig_Slingshot`, `StoatSlingConfig_Slingshot`) with the Regatta steering on (§8). No Slingshot race recorded yet |
| **Meerkat** | **Warpline** (since 2026-10-27; Burrow Run is its 8-ring twin) | gate race, the wormhole | **Yash's own (2026-10-27).** The AI forms its tunnels through the replicated press path (`MeerkatSolitonExecutor.Autopilot`); no learner reads the Meerkat yet (§8) |
| Rhino | Headlong | lapped gate race | none yet |
| **Manta** | **Redline** | gate race, laps of a circuit cut for Soar | **Started 2026-11-03 (Yash, §13).** No race recorded yet. The AI flies the Regatta steering; its Soar bands are searched on a Redline model of the shipped AI (`redline-manta-time`) |
| Urchin | Skein | gate race on rails | none yet |
| Sparrow | Breakwater | gate race through breakwaters | 7 hand-flown (I1, I4). The steering fix and the rocket that clears the way are in, the rocket's range and aim learned from Yash's shots (§6) |
| Butterfly | Waystation | fold race | 2 (Caleb, I1). The AI threaded 0 rings (before the fold fix) |
| Scarab | **Scarab Rally** (new 2026-10-27; Scarab Scramble is now its Mass card) | ring race: throttle, drift, juke | none yet |
| **Tiger Moth** | **Waystation** (the Drop Tower course since 2026-11-01) | Drop Tower race: short hops through plates, then one long drop | none yet on the towers. The belly-aim autopilot takes the clusters and the long drop; the hop autopilot (`WaystationController.TickHops`) takes the towers. No learner reads it yet |
| Scarab | Scarab Scramble / Tollway | goal race / toll race | none yet |
| Grizzly | Grizzly Time | race | report only: `grizzly-v3` (jtgjones) is reworking the bombs |
| **Thresher** | **Thresher Time** | gate race, the sling | **Next after the Stoat (Yash, 2026-11-02, §12).** 15 editor flights, 8 finished (I1 9 / 5, I2 3 / 1, I4 3 / 2, none at I3), learned into `ThresherAIBoostPolicy_ThresherTime` (2026-11-01; two of its three races are Garrett's). The AI plants on almost every corner and, since 2026-11-02, launches aimed like Garrett |
| Wrasse | Wrasse Run | crew race | report only: the crew's kite spends the boost (`KiteNavigator`) |
| Meerkat | **Burrow Run** | gate race, the wormhole | new (2026-10-27): the studio's AI (`aiGatePlan`) ported line for line; no hand-flown races yet, and no learner reads its flights (the sweep reports them) |
| Serpent | **Serpent Time** (new 2026-10-27) | circuit race on the fuel pellets | none yet |
| Brittle Star | **Brittle Star Time** (new 2026-10-27) | circuit race on Speed Mode | none yet |

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
- **2026-10-29:** Switchback is now Blast Run only; the Switchback boost policies below were learned on the Classic
  ladder and stay until Blast Run races are recorded and the learner (which now ships `BlastRun` only) is re-run.
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

## 4. The end state: the arena games rebuilt from the trained vessels

Once two to four vessels play at their best in their own minigames, the **arena games** are restructured from what they
learned. An arena game is a mix of the vessels' skills, so any vessel can play it and still be a good fit (Yash,
2026-10-27). Regatta is the first of them; whether it stays a track race is open. What follows is the Regatta case:

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
| Stoat / Warpline | the field pair's warp; the sling | pairs and slings learned from Yash (`StoatDipoleConfig_Warpline`, `StoatSlingConfig_Warpline`, 2026-10-26) |
| Rhino / Headlong | the ramp; ramming costs it nothing | the ramp drive (Regatta-tuned) |
| Manta / Redline | Soar | the Soar executor's bands (0.35 / 0.75); searched by `redline-manta-time` (§13) |
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
- **Done (2026-10-27):** the plan restated (each vessel's own Time minigame first, then its other own minigames; the
  arena games rebuilt from the skills; Regatta races reported, never learned from). The Stoat's hold lifted and its
  Warpline AI fixed (§7).
- **Next:** Yash plays Warpline and Switchback at each intensity, repeatedly. Nothing else: every race is relearned from within the
  hour by the AI learning sweep (`AUTO_LEARNING.md`), which also checks the AI's pace and fixes what numbers cannot.
  Then the next vessel.

## 7. The Stoat in Warpline (2026-10-27)

**What Yash's races show.** 15 races (2026-10-25..27, I1-I4, Easy to Legendary rivals), all finished by Yash in
66-129 s. The AI Stoats finished **none of 30**: 0-9 of 10 rings, at about 140 u/s, as fast as Yash (median 100,
116 at the rings). On a clean leg the AI flew 800-1,000 u in 6-9 s; but it crossed a ring's plane 10-180 u off its
26-48 u mouth 1-11 times a race, and every miss cost a 3,000-7,800 u loop back round.

**Why.** Two causes, both measured offline on the 15 courses (`switchback_dolphin_sim.py --mode Warpline`, the Stoat at
60 u/s cruise and 48 deg/s, the warp as a hold to ~140 u/s; the pair's bend of the path is not modelled, so it is
pessimistic on turns):
1. **Warpline did not fly the Regatta steering**, and its 180 u approach point lay outside its 160 u commit radius
   (Switchback's and Breakwater's defect). Shipped steering: none of 15 finished.
2. **The autopilot held the warp until 60 u from the ring** (`StoatDipoleConfig.autopilotLetGoNear`). At 140 u/s and a
   fixed 48 deg/s the Stoat turns on a 167 u circle, so from 60 u it cannot line up on the mouth. With the steering fixed
   and the let-go at 60 u it still missed 180-280 mouths over the 15 courses; at 200 u and more, none.

Yash threads half the rings while still warping and otherwise lets go about 50 u out: he sees the pair bend his path
into the ring, which the AI's steering does not use. So the let-go is NOT learned from him; it is set from the model.

**What changed.** The steering and the learned numbers landed first, from a session working in parallel on Yash's
"re train Stoat" (2026-10-26, 11 races, `WARPLINE.md` §3.1); this section adds the warp's let-go (`WARPLINE.md` §3.2).
- `WarplineController.AICommitFromApproachPoint` = true (the Regatta steering with the approach-point cap).
- `Resources/AIBoostPolicies/StoatDipoleConfig_Warpline` and `StoatSlingConfig_Warpline` (Warpline only,
  `AIModeTuning`), written by the learner from Yash's races: hold a warp 1.0-2.36 s (was 0.8-15), the next pair 1.52 s
  after an orbit cap (2); in Time mode sling from 125 u at any 8 deg turn, every 0.95 s, let go within 16.6 deg. And by
  hand: `autopilotLetGoNear` 250 (was 60), which the learner keeps.
- Garrett's Stoat configuration (`Assets/_SO_Assets/VesselActions/Stoat/`) is untouched: a person's Stoat, and the AI
  in every other mode, fly as before.

**The model, all 15 courses** (median race seconds; Yash's median 92):

| | warp 120 u/s | 140 u/s | 170 u/s |
|---|---|---|---|
| Shipped steering, let go 60 u | none finish | none finish | none finish |
| Regatta steering, let go 60 u, learned hold | 214 s, 183 misses | 243 s, 242 misses | 204 s, 199 misses |
| **Regatta steering, let go 250 u, learned hold** | **92 s, 0 misses** | **87 s, 0** | **82 s, 0** |

Tests: `SwitchbackAISteeringTests.Warpline_*`. Not run in Unity (`Docs/UNITY_VERIFICATION_CHECKLIST.md`). Slingshot,
the Stoat's other race, keeps its scene steering until its own races show the same thing.

## 8. Yash's two: the Stoat in Slingshot, the Meerkat in Warpline (2026-10-27)

> "Stoat and Meerkat are the two vessels that I really wanna complete myself ... I want to build the entire kit of these
> two vessels ... Stoat will use Slingshot and Meerkat will use Warpline. These will be two time game modes of these two
> vessels." (Yash, 2026-10-27)

- **The cards.** `ArcadeGameWarpline` seats the Meerkat (`author_warpline_assets.py`); Slingshot stays the Stoat's.
- **The Stoat's AI in Slingshot.** `SlingshotController.AICommitFromApproachPoint` is on: its scene carried Warpline's
  180 u lead against a 160 u commit, the defect that stopped every AI Stoat in Warpline (§7). Its pair and sling variants
  start as copies of the Warpline ones learned from Yash (the same hull, the same warp, the let-go at 250 u); the sweep
  relearns them from Slingshot races (`learn_drives_from_flights.py` reads any mode, and copies an existing variant first,
  so the hand-set let-go is kept).
- **The Meerkat's AI in Warpline.** Its autopilot is the one Burrow Run flies; no learner covers the Meerkat yet, so its
  races are recorded and wait (`ai_learning_seen.json` "waiting", hull with no learner). A Meerkat learner (the tunnel's
  bend, length and when to press RT, measured from Yash's flights) is the next learner to join `ai_learn.py`.
- **The courses.** Both races cut Slingshot's circuit. `FrogletTools > Game Modes > Time Race Prototyper`
  (`Docs/TIME_RACE_PROTOTYPER.md`) shows each intensity's course, lets the person change it, save it as a named preset
  and play it. A saved course is a different race: the learner tells courses apart (by ring radius, `on_current_course`)
  only in its `PER_INTENSITY_MODES` (Switchback, §3.7), and Slingshot and Warpline are not in it yet.
- **Their four abilities** are prototyped in the Vessel Studio first (the Stoat studio round 28, the Meerkat studio round
  6) and reach Unity only on Yash's say-so (`/vessel` §3).

## 9. Next: the Switchback Dolphin, Yash's brief (2026-11-01)

The next AI session works on the Dolphin in Switchback alone. Yash's words:

> "I want a very good switchback AI where the dolphins will actually drift towards the crystal while aiming at the
> obstacles in switchback's intensities. And then while hitting the crystal, it will just blow with its own dolphin
> blast. It will blow the obstacles. Then it will go towards the target ring in either little bit drifting and going
> faster or directly straight. As a user, I can tell you that when I am spamming my drift little bit in few intervals,
> then I can actually go drifting towards the target, which is faster than just using the thumbstick to go forward. And
> yeah, so generally dolphin users, they in 1 second or 0.5 seconds, they keep spamming the triggers."

**What already exists (read first).** Switchback is Blast Run since 2026-10-29 (`SWITCHBACK.md` "Switchback is Blast
Run", "Blast Run", "AI: the blast planner", "AI: any prism in the way", "AI: the emergency sweep", "Watching the AI").
`SwitchbackBlastPlanner` picks Direct / Thread / Blast at each blocked gate; `SwitchbackBlastAI` (server only) flies a
Blast to the gate's key crystal with the nose held on the obstacle (`TryOverrideAim`, `TryOverrideDriftLook`), so the
crystal's blast clears it. Switchback opts back in to crystals (`SwitchbackController.AIDetoursForCrystals` = true),
the one exception to §6.1, because its key crystals are part of the course. The charge drive is
`DolphinAIBoostPolicy_Switchback[_I<n>]` (§3.6-3.7).

**What is new in the brief:**
1. **Drift into the crystal with the nose on the obstacle**, at every intensity (the planner may still choose Thread or
   Direct where no blast pays).
2. **Drift-spam as the way to fly fast toward the target**: short drift presses every 0.5-1 s, which Yash says beats
   holding the stick forward. Nothing in the AI does this today: the AI holds one commit drift per gate and discharges
   on a straight (`ChargeBoostAIPolicySO`, `AIPilot` commit drift). Neither the learner nor any doc measures it.
3. **After the blast, to the ring** by drift-spam or straight, whichever is faster there.

**How to measure Yash's drift-spam (no new recording needed).** Every Switchback race already records each drift
press and release at its exact time (`buttonEvents`: `[t, InputEvent, pressed]`, the input names in `buttonNames`) and
the drifting state bit per 10 Hz sample (`state`, `stateBits.drifting`), plus speed, the next ring and crystals. The
first step is a measure of the press period, the press length, the speed gained against holding straight, and where
in a leg people spam; then a model of it in `switchback_dolphin_sim.py` before any AI change. Yash plays Switchback at
each intensity as usual; the races reach the learner on their own (`AUTO_LEARNING.md`).

**Measured offline (2026-11-01, `SWITCHBACK.md` "Offline: the Blast Run simulator").** The shipped AI, alone, median of
12 courses: I1 about 90-100 s, I2 145-185 s, I3 200-255 s, I4 210-540 s (Hard and Legendary do not always finish I4).
A harder level is slower: its prism dodging pulls it off line and it loops back for missed rings. With dodging off it
finishes I4 in about 120-145 s at every level. Blasting as flown today saves about as much as its setup turn costs. The
plan that follows from it, in order: dodge only what costs (Danger prisms, never inside the last stretch to a ring);
make the blast free (fly THROUGH the key on the racing line with the nose drifted onto the door, no setup turn behind
it); set the planner's hit costs from the measured ones; then drift-spam, from Yash's Blast Run races.

### 9.1 Done 2026-11-01..02: the Blast Run AI in four steps, and difficulty by nose lag

All in `SWITCHBACK.md` ("Offline: the Blast Run simulator" to "Lag by difficulty"), measured on the offline simulator
`Tools/Build/switchback_sim_harness` (it compiles the SHIPPED course, Blast Run layout, planner and avoidance C# and races
a kinematic Dolphin with the game's drift, nose lag and prism costs; `run.sh eval|trace|selftest`).

| Step | What | Offline effect (median s) | State |
|---|---|---|---|
| 1 | Dodge only Danger prisms, and nothing in the last 200 u to a ring (`aiDodgeDangerOnly`, `aiNoDodgeNearRing`) | I3 Legendary 334 -> 156; I4 Legendary never finished -> 172 | shipped |
| 2 | Strike the key crystal straight, the drift aiming the blast at the door; no setup leg behind the key (`aiStrikeSetupLeg` off); the lane fan batched on Jobs + Burst (`SwitchbackSenseBatch`) | 27-28 s faster than the setup leg at Legendary | shipped |
| 3 | The planner's plain-hit cost is the measured 0.25 s (was 0.6) | no measurable race change | shipped |
| 4 | Drift carving (drift-spam toward the ring, Yash's brief) | with the AI nose lag: much slower; with none: every time halves, carving adds ~10 % | measured, NOT shipped |
| 4, option 2 | The AI nose's follow rate by intensity and difficulty (`aiNoseFollowByIntensity`, `VesselTransformer.AINoseFollowPerSecond`; every other mode keeps the fleet lag) | I4 140 / 129 / 120 / 112, I1 88 / 82 / 69 / 61, inside Yash's bands (Easy 130-150 ... Legendary 100-120 at I4, 10 s steps elsewhere) | shipped |

**Why the nose lag is the difficulty lever here.** At one nose rate the four levels finish within a few seconds of each
other: the lag, not the rest of the skill, set the time. Yash asked for the levels 10 s apart in 20 s bands; one ladder
for every intensity could not hold that, so the rates are a row per intensity. This departs from `/vessel-ai` §3.1 (Hard
is the shipped pilot, the other levels add mistakes, one setting per level for every intensity); the skill records it as
Switchback's exception, and `AI_PLAN.md` §7 lists it for the plan.

**Real-game tools added on the way:** the per-AI prism hit counter and race summary (`SwitchbackHitStats`, `aidiag hits`),
per-AI flight stats at race end (speed, drift share, taps/min, tap length) to compare with people's flights, the headless
soak (`SwitchbackSoakRunner` + `SwitchbackSoakLauncher`, `-sbSoak "I:D:races"`, offline host), the AI Diagnose panel's
Switchback overlays, and profiler markers (`Switchback.AI.Sense`, `Switchback.AI.Sweep`, `Switchback.SenseBatch.*`).

**The universal search (2026-11-02, Yash: "do the recommended ones").** People's races keep teaching the sweep, and
its per-intensity files are now the SEEDS of one Dolphin search, never its result (`AI_PLAN.md` decision 1). The
Froglet Compute job `switchback-dolphin-time` (evaluator `switchback_fit`) searches ONE charge drive for I1-I4 at
every difficulty level, its first generation the four shipped drives. A first local run (260 evaluations, this
session) found a drive that, on 8 FRESH courses per intensity, beats what the game flies today at every level
(composite Easy 1.63 vs 1.73, Medium 1.49 vs 1.52, Hard 1.36 vs 1.39, Legendary 1.22 vs 1.29; all 144 races
finished); I4 medians Easy 140 / Medium 128 / Hard 116 / Legendary 102 s against today's 147 / 134 / 120 / 115. It is
NOT promoted: the team's idle machines now run the job longer, and promotion is Yash's (the job's `promote`). Two things
to know first: Medium's I1 median got slower (92 s vs 79 s) while its composite improved, and a new drive moves the
difficulty bands, so `aiNoseFollowByIntensity` is re-measured with it before shipping. A first try raced Hard only and
lost I4 at Easy; that is why the job races every level.

**Next:** the Unity soak of steps 1-4 and the difficulty table at I1-I4 (each level should step ~10 s; real races ran
~1.6x the model in the step 1 soak, so the rows may need scaling), then drift-spam from Yash's Blast Run races (§9: his
press period and length are recorded in every race's `buttonEvents`).

## 10. The order of work from 2026-11-01: minigames first, no new vessels (Yash)

> "For now we won't be making any more new vessels. We will be focusing on making the mini games. The time mini games,
> all our existing vessels, awesome. And then finally we will make arena games such as regattas. So for now you can for
> me focus on stoat and meerkat. But other than that, I'm totally focused on dolphins, switchback, time game, where I'm
> making awesome AI so the game feels hard to play as well as challenging." (Yash, 2026-11-01)

1. **No new vessels.** Every prototype (the concept ten, the Siphonophore, the Omega crews, the calm three, the Orca's
   play styles) is parked and catalogued in `Docs/Studios/CONCEPT_FLEET.md` §13. A session brings one back only when
   Yash asks for it by name.
2. **The Time minigames of the existing vessels, made awesome.** One per vessel (§2), four intensities that teach then
   test (§1), and an AI that makes it hard and challenging to win: rivals of the same hull, learned from people's races
   (`AUTO_LEARNING.md`), at every intensity.
3. **Then the arena games**, Regatta first, rebuilt from the skills the vessels learned in their own minigames (§4).

**Who is on what.**

| Work | Who | Where it is written |
|---|---|---|
| The Dolphin in **Switchback** (Blast Run): an AI that drifts into the key crystal with the nose on the obstacle, blasts it and drift-spams to the ring, so the race is hard to win | **Yash** plays it at every intensity; the AI sessions build from his races | §9; `Assets/_Scripts/Controller/Arcade/SWITCHBACK.md` |
| The **Stoat** in **Slingshot** and the **Meerkat** in **Warpline**: the kit, the race and the AI | Claude sessions with Yash | §7, §8; `Docs/Studios/STOAT_PLAY_STYLES.md`, `Docs/Studios/MEERKAT.md` |
| The **Thresher** in **Thresher Time**: an AI that slings well at I1-I4 | after the Stoat, Claude sessions with Yash | §12; `Assets/_Scripts/Controller/Arcade/THRESHERTIME.md` |
| The other vessels' Time minigames | later, one vessel at a time, in the §2 order | §2 |
| The arena games (Regatta and the rest) | after the Time minigames | §4; `REGATTA_AI_ROADMAP.md` (on hold) |

## 11. The next vessel: the Stoat, on the orbit alone (Yash, 2026-11-02)

> "We have made the AIs of dolphin of switchback amazing, now let's move to another vessel [...] I think we keep only the orbit
> effect of stoat, and remove the pathfinding field trajectory effect [...] if you think stoat is the next best vessel to go to
> Ragatta, then add it." (Yash, 2026-11-02)

**Decision: the Stoat is next, and it was never off Regatta's card** (on it since 2026-10-17; `author_regatta_assets.py`
`HULL_ORDER`). Its kit is the orbit alone (`Docs/Studios/STOAT_FIELD_TRAJECTORY_PARKED.md`). Its Time game is **Slingshot**.

**Why it is not because it is fast.** Raw speed says the opposite:

| Hull | Cruise / top, u/s | Own Time game, flights (finished) | Regatta, modelled seconds I1-I4 | For | Against |
|---|---:|---|---|---|---|
| **Stoat** | 60 / 164 warped (336 at Time 10) | Slingshot 4 (1), Warpline 15 | 60 / 61 / 65 / 94 AI at Time 10 (gate steering only) | In the pack with the Dolphin and Squirrel; Yash's own vessel (§8); the steering fix, a learner and starting numbers exist; the race asks a skill (where to turn, a timed release) | Hairpins cost it about twice the field trajectory's time; Mass and two level-5s have nothing to act on while parked; Yash's `stoat/skim-sling` branch rebuilds the same Time mode |
| Manta | 180 / 720 | Redline 0 | 25 / 31 / 35 / 35 | The fastest hull, a learner and a drive model; tops the compute queue | Already twice the field: the plan's job is closing that gap, so more speed widens it. No races, and Redline lacked the Regatta steering (it flies it since 2026-11-03, §13) |
| Rhino | 50 / 840 | Headlong 0 | 51 / 63 / 75 / 61 | The highest top speed, a learner and a drive model | No element reaches it, so the balance stage cannot level it. No races, and Headlong lacks the Regatta steering |
| Sparrow | 35 / 135 | Breakwater 12 (9) | 74-90 at Time 10 (competent-pilot model) | The most proven remaining game | The slowest modelled hull on the card; no drive model |

Dolphin 54-72 s and Squirrel 48.5-59.5 s are the marks to race (`REGATTA_BALANCE_PLAN.md`). So the Stoat is the one hull that
can already meet them at the card's Time level. The Manta came next until §12 put the Thresher there; it is still the choice
if raw speed is what is wanted.

**What changed for the AI.** A held pair is now the hull's only speed, so the AI also presses for the warp alone on a long leg
(`aiWarpMinDistance`): 56-62 s a Slingshot race in the model against 70-82 s without it (`stoat_orbit_sim.py`; the table is in
the parked doc). Every number here but the editor races is a model.

**Next, in order.** Yash plays Slingshot at each intensity: the sweep relearns the sling rows on its own (`AUTO_LEARNING.md`).
Then the Stoat's `drive_fit` model joins stage 1 of `REGATTA_BALANCE_PLAN.md`, and Regatta takes the trained numbers (§4).

## 12. After the Stoat: the Thresher, in Thresher Time (Yash, 2026-11-02)

> "I need another vessel to race, I think garrett will like the vessel that has a ball attached to a rope, and moves using
> the tension of rope and momentum of ball [...] Thresher's time game need to be made first before regatta, thats the rule,
> make AIs good in their vessel's respective time game, then we will design regatta based on those AI skills and game
> modes." (Yash, 2026-11-02)

**Decision: the Thresher is the vessel after the Stoat, and it trains in Thresher Time, not Regatta.** The Stoat stays first
(Slingshot, §11). The Thresher is **not** added to Regatta's card now: its AI gets good in its own Time game at I1-I4 first,
and Regatta is designed afterwards from what the trained AIs can do (§1 step 6, §4). Yash's picks to race Regatta are the
Squirrel, the Dolphin, the Stoat and the Thresher. The Sparrow was weighed for that seat and lost it: it is the slowest
modelled hull (74-90 s against the field's 48-72 s, §11), and its skill is shooting a way clear, which a plain race does not
ask. It stays on Regatta's card.

**Why the Thresher.** It is Garrett's vessel: he merged every Thresher change and flew the last on 2026-10-30, so it has the
team's acceptance the Stoat is still waiting for. And its race asks a skill: plant the ball, swing, release on the line
(`THRESHERTIME.md` §2: a sling pays as a corner, not on a straight).

**Where its AI stands (2026-11-02).**
- People's races: 15 editor flights, 8 finished (I1 9 / 5, I2 3 / 1, I4 3 / 2, none at I3). The sweep wrote
  `ThresherAIBoostPolicy_ThresherTime` from them on 2026-11-01 (`LEARNED_DRIVES_ThresherTime.md`).
- In Thresher Time the AI flies that learned variant (`AIBoostPolicySO.ForMode`), not the base policy `THRESHERTIME.md` §4
  describes (a ring at least 70° off, a 15° release cone). The variant plants when a ring is at least 6.7° off and 115 u
  away, at most every 2.9 s, winds while planted, and lets go within 62.8° of the ring or after 3.2 s. It always lets go
  unaimed: the ×4.8 aimed launch is still a human's only. **Since 2026-11-02 (later the same day) it launches aimed**:
  under the launch view it aims at the ring and lets go after 1.54 s, Garrett's median (`THRESHERTIME.md` §4 "The aimed
  launch"); the 62.8° cone, learned from his arms, is back to the base's 15° for the plain let-go.
- No offline model of Thresher Time, so no compute job.

**Garrett's races** (his machine reports as `gradi`, his pilot name is Kyoki; collected 2026-11-02 from
`origin/bugledger/gradi`). Every race he sent is already on the sweep's seen list (`ai_learning_seen.json`), and nothing
needed collecting by hand. Two were matches with Caleb, Shombith and Yash; each person's file is their own hull.

| Race (his clock) | Intensity | Result | Plants | Planted, of it both triggers full | Peak u/s |
|---|---|---|---:|---|---:|
| 2026-10-29 17:55 | I1 | 15 / 16 rings; Caleb won in 108.5 s | 10 | 23.2 s, 21.2 s | 1,966 |
| 2026-10-30 19:54 | I1 | **won, 64.6 s**, rivals Legendary (learned from) | 7 | 7.0 s, 6.8 s | 3,985 |
| 2026-10-31 13:53 | I2 | **won, 106.1 s** (learned from) | 12 | 18.6 s, 17.8 s | 4,595 |
| 2026-10-31 13:56 | I4 | 14 / 16 rings; Shombith won in 120.1 s | 8 | 23.1 s, 15.1 s | 4,576 |

What his races teach that the AI cannot take yet: **85% of his planted time is the aimed launch** (both triggers full), and
it is where his speed comes from (peaks of 2,000-4,600 u/s; the other pilots who plant little peak at 170-410). Caleb flies
the same way. The learner read when he plants and lets go, so the AI copied his timing but not his aim; it now reads the
aimed launch too (`learn_drives_from_flights.py` `aimed_launch`: 68% of the plants in the races it learns from) and the AI
flies it. The Thresher's
Toybox play, where Garrett tuned it from 2026-10-08, was never recorded: the recorder records matches only, and no
Freestyle flight exists from anyone.

**For later.** The Thresher and the Stoat race the same verb (orbit, then release for a kick), so two of the four share it.
Garrett's yes comes before it goes on Regatta's card: it is his vessel, and a mode takes a kit as it is (CLAUDE.md, "A game
mode never changes the vessel"). Its ball threads rings as the hull does (`THRESHER.md`, Garrett), which Regatta has to
measure when it is designed.

**Next, in order, once the Stoat's Slingshot is done.** (1) Yash plays Thresher Time at I1-I4 (I3 has no race yet); the
sweep relearns from it on its own. (2) The AI makes slinging pay at every intensity (§6), measured against Yash's races. The aimed launch is in
(2026-11-02) and raced headless at I1: an AI Thresher won in 77 s and 116 s, where the same courses with the aim off
left the AIs at 1-4 of 16 rings (`THRESHERTIME.md` §4 "Measured in the real game"). At I2-I4 an AI Thresher won every race too (I2 97.8-148.2 s, I3 100.6 s,
I4 110.8 s), and the races were watched in a Unity window. What is left: people's races at I3, the aim-off A/B at
I2-I4, and more courses.
(3) A Thresher Time model joins stage 1 of `REGATTA_BALANCE_PLAN.md`. (4) Then Regatta is designed from the four trained hulls.

## 13. The Manta, in Redline (Yash, 2026-11-03)

> "Let's make Manta AI training, what is the time game mode of manta?" (Yash, 2026-11-03)

**The Manta's Time game is Redline** (`GameModes.Redline` 53, `Assets/_Scripts/Controller/Arcade/REDLINE.md`): five
laps of an eight-ring circuit cut around the Manta's full-Soar turning circle, where every corner asks how much Soar it is
worth. Nobody has raced it yet, so the AI trains on a model first and on people's races when there are some.

**Correction (2026-11-04).** The race has been five laps (40 gates) since the 2026-10-26 ~90 s target pass; the model
raced three until 2026-11-04, so every race time in this section's first half is a three-lap time. It now reads the
laps from `RedlineCourseSource.DefaultLaps`. The five-lap numbers are in `REDLINE.md` §5.1 and in §13.1 below; the
findings did not change.

**What was wrong with the model the queue raced.** `drive-manta-redline` (Shombith's machine ran it 34,070 times)
raced `manta_drive_sim.py`, which is Regatta's model: a point mass at full throttle with no nose lag. Its
best bands (BoostStickBand 1.0, YawsteryStickBand 0.63: always Soar) were an artefact of that. Redline flies none of the
three. The AI's throttle is the skill throttle (Lerp(0.2, 0.9, intensity x 0.25): 37.5% at I1), the AI nose follows
its command at 1.5/s, and Redline flew the old gate steering. The editor's own AI Mantas in recorded Regatta races
reached 12-21 of 24 rings in about 100 s at I1, where that model finished all 24 in 25-35 s.

**The new model**, `Tools/Build/manta_redline_sim.py`, ports what an AI Manta does in Redline line for line: the
course, the gate aim, `AIPilot`'s stick, skill throttle and orbit break-off, the Soar triggers, and the hull's turn law
with the nose lag. On Regatta's I1 course it laps in 28 s; the editor's Hard AI Mantas lapped it in 23-28 s
(`--self-test` holds the trigger shape, the stick's signs, determinism, and three negative controls).

**What it measured, and the one game change** (`REDLINE.md` §5.1 has the table):
1. On the old steering the shipped AI finished 4 of 12 races over I1-I4, none at I3 or I4, and no pair of Soar bands
   changed that. **Redline now flies the Regatta steering** (`RedlineController.AICommitFromApproachPoint`,
   `RedlineAISteeringTests`), as ten other gate races do: 12 of 12.
2. A full-throttle floor (Regatta's) is faster at I1 and loses races at I4, so the skill throttle stays.
3. **The nose lag is the biggest lever left**: with none, the AI is 1.7x faster at I1 and 5x at I4. That is
   `AI_PLAN.md` decision 7 (Yash), now asked for the Manta too.
4. The bands matter once the steering is right: 0.15 / 0.75 finished every course with I4 at 228 s, against the
   shipped 0.35 / 0.75's 326 s. The old job's 1.0 / 0.63 finishes none of I3-I4.

**The search**: `redline-manta-time` (evaluator `redline_fit`, plan step `time-arcade`) searches ONE Soar drive for
I1-I4 with the plan's composite (mean + half the worst intensity). Its first generation is the shipped executor bands,
`MantaSoarAITuning_Regatta` (learned from people's Regatta races), and `MantaSoarAITuning_Redline` once the sweep writes
it. The answer is a proposal for Yash: it becomes `MantaSoarAITuning_Redline` through the learner's writer, never by
hand. Its machines need the evaluator on `bleeding-edge` and the simulator on `ai`. A first local run (120
evaluations, 2026-11-03) found 0.118 / 0.83. On 6 FRESH courses an intensity it scored a composite of 23.95 against
the shipped bands' 24.91 and the Regatta genome's 24.94. It finished 5 of 6 at I4 (median 295 s, against 318 s and
4 of 6 for the shipped bands), but ran 3-6 s slower at I1-I3. A small gain, not promoted. The old job's 1.0 / 0.63
scored 38.21 and finished none of the 12 races at I3-I4. The bands are a small lever; the nose lag is the big one.

### 13.1 The ladder: Soar alone, introduced at I1, intense at I4 (Yash, 2026-11-04)

> "I want a plan that gonna introduce the new player to the time ability of Manta in intensity 1, and in intensity 4,
> it will get to intense. No other elemental abilities needed, only time." (Yash, 2026-11-04; "Build it")

Built the same day (`REDLINE.md` §1, §4, §4a, §5.1):
- **The course asks for Soar alone.** No corner of any seed is tighter than the 156 u both triggers released make, so
  no level needs the Yawstery pivot (Mass): one corner that costs Soar a level, 0 / 1 / 2 / 3 (I2 217 u at half Soar, I3
  184 u and 228 u, I4 171 u, 199 u and a 233 u knife-edge), through mouths of 110 / 88 / 72 / 44 (`RedlineCourse`,
  `RedlineCourseTests`, run out of Unity by `Tools/Build/redline_course_harness`).
- **Only Time is fed.** The cell's omni crystal is off; Time crystals (the elemental set's, one level each, +3% Soar top
  speed) sit 0 / 1 / 2 / 3 a lap, on the line at I2-I3 and inside the apexes at I4 (`RedlineTimeCrystals`, laid on
  every peer by `RedlineController`, `RedlineTimeCrystalsTests`).
- **I1 teaches.** The Test Flight Lesson already teaches the Time ability; Redline's Mentor tips now say hold Soar, ease
  both triggers for a corner, wind it back early, and what a Time crystal does (`Tips_Redline.asset`). The card says
  "both triggers".
- **The AI** finishes every level on five laps: 183 / 181 / 193 / 538 s (24 of 24 model races). It does not steer for
  the crystals: with the fleet nose lag the speed they buy made it miss more mouths (1 of 6 at I4 at a 150 u slack),
  and a full-throttle floor buys nothing the ladder needs. **So I4 is intense for a person but not yet against the
  AI**: with no nose lag it runs 95 s at I4. That is decision 7 again.

**Next, in order.** (1) Run Redline in the editor at I1-I4 with AI Mantas (`Docs/UNITY_VERIFICATION_CHECKLIST.md`):
they should finish and re-attack a missed mouth, and every corner should go with both triggers eased. (2) Yash races
Redline at each intensity: every race is recorded and the sweep learns `MantaSoarAITuning_Redline` on its own after
three finished races. (3) Decision 7 for the Manta's nose rate, which also unlocks the AI taking Time crystals
(`RedlineController.aiTimeCrystalSlack`). (4) The searched drive is promoted with Yash's yes, and stage 2 of
`REGATTA_BALANCE_PLAN.md` races it. (The card art was re-rendered for the new I2 course the same day.)
