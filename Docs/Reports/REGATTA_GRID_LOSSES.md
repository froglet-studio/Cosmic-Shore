# Why each AI loses in a Regatta grid (measured from the flights, 2026-11-06)

**Plan step:** `time-arena-course` (read-only: nothing here changes the game or an AI). **Asked by:** Garrett, through
the Regatta balancing plan (brief 9). **Owner of the area:** Yash (the vessel AIs and Regatta's course and card).
**Tool:** `python3 Tools/Build/regatta_rival_report.py --dir <flights> --grid` (new on this branch; `--self-test`).

Every number below is read from the real game: the rivals recorded in people's editor races by `HumanFlightRecorder`
(position, rings, velocity and AI state ten times a second). The model numbers it is compared with are the
measured-roster model's (`Tools/Build/regatta_measured_roster.py` on `claude/regatta-overnight-balance`, MOUTH_ALPHA 0.6),
run on the circuits the races were flown on. Nothing here was raced by this session; the cloud has no Unity editor.

## In one minute

1. **Garrett's 2026-11-06 races were flown on the old I4 circuit, not the branch's.** All 183 Regatta flights in the
   ledger (Garrett, Caleb, Yash and an older `owner` folder, 2026-10-16 to 11-06) fly the four circuits `bleeding-edge`
   ships: 54 u mouths at I4, the ring list identical point for point, and three full-colour rails with no neutral
   stretch. The branch's I4 has 68 u mouths and a 59-66% neutral rail. **So no editor race has tested the branch's
   searched circuits yet**, and the playtest cannot say they made the grid worse. On the old I4 circuit the model ALSO
   says the Urchin wins by about 2x (61.7 s against 128-246 s for everyone else), so the playtest agrees with the model
   there.
2. **"Squirrel 14" was Garrett.** In the race flown as a Squirrel, Garrett scored 14 gates; the AI Squirrel scored 3
   (the "Dolphin 3" in the brief). Gates when the Urchin finished, in his three races that day:

   | Race (Garrett's hull) | Urchin | AI Squirrel | AI Serpent | AI Rhino | AI Manta |
   |---|---:|---:|---:|---:|---:|
   | 14:17, Dolphin | 24 | 11 | 11 | 7 | 6 |
   | 15:10, Dolphin | 24 | 11 | 10 | 6 | 10 |
   | 15:44, Squirrel | 24 | **3** | 10 | 5 | **4** |
   | the model says (old I4) | 24 | 10.5 | 8.0 | 7.7 | 11.5 |

   So the model is right or pessimistic for the Urchin, Squirrel and Serpent, a little optimistic for the Rhino, and
   1.2-2.9x optimistic for the Manta. The 15:44 race is the worst of the three for two reasons the record shows: the
   AI Squirrel was flown by the wrong pilot (below), and the Manta missed five mouths.
3. **Traffic is not why they lose.** Of 389 slow-downs AI hulls took since 2026-11-02, 374 began beside a rail and 3
   beside another vessel; no AI spent more than 5% of a race within 40 u of another hull. Grids and duels at I1 are
   no different in a consistent direction. The model's "about 1.25x for a grid's traffic" is not in the data.
4. **What each AI loses to** (I4, the old circuit, AI rivals only, since 2026-11-02):

   | Hull | Races | Seconds (projected to 24 gates) | Model | Real / model | What the record shows |
   |---|---:|---:|---:|---:|---|
   | Urchin | 5 | 62 (60-67) | 61.7 | 1.00 | rides its own rail 93% of the race at 300 u/s; never misses |
   | Squirrel | 2 | 134 | 140.8 | 0.96 | fine when the Skim Race pilot flies it (but see the fault below) |
   | Serpent | 4 | 149 (134-182) | 184.2 | 0.81 | cruise only (boost driver idle 100%); 0.22 missed mouths a ring, ring 5 its worst |
   | Rhino | 6 | 244 (211-298) | 191.6 | 1.27 | **flies at 50 u/s for 78% of the race**: its boost driver waits for "a straight that pays the wind-up back" and I4's legs never qualify |
   | Manta | 6 | 190 (124-373) | 128.5 | 1.48 | **misses 0.36 mouths a ring** (just outside, 1.1-1.5 radii) and loses 8.5 s on each; **rams the rails 9-14 times a race**, each to about 8% of its speed, and is slowed 28% of the race |

   No AI Dolphin, Stoat, Sparrow or Scarab has raced I4 since 2026-11-02 (the Dolphins and Stoats there were people).

## Three AI faults the records show

These are bugs, not balance, and each one turns a race into a rout. They are Yash's area; nothing was changed here.

1. **An AI Squirrel raced by the platform autopilot instead of the Skim Race pilot**, in both races where the person
   ALSO flew a Squirrel (2026-11-05 17:51 and 2026-11-06 15:44): the AI bit (AIPilot.AutoPilotEnabled) was on for 49%
   and 100% of those races, with the Dolphin-style drift boost driver's statuses and 7-19% of the race breaking off
   orbits; it scored 3 gates in 62 s and 16 in 152 s at 75-77 u/s. In the 14 other races since 2026-10-26 the AI bit
   was off (the Skim Race pilot stands AIPilot down) and it flew 70-204 u/s. `SkimRacePilot.SuppressOtherPilots` and
   the `IsInitializedAsAI` / `FlyingForHuman` check in its `Update` are where to look first; two races is thin, so
   the first test is one I4 race as a Squirrel against an AI Squirrel.
2. **An AI Urchin riding a rival's rail.** 2026-11-05 17:51, I4: the Ruby Urchin latched the Jade rail and rode it at
   24 u/s for 45% of the race, slipping off and re-latching it, and scored 10 gates in 152 s (Garrett won that race).
   `RegattaController.TryOverrideAim` steers a FREE Urchin to its own lane (the 2026-10-20 fix), but once it is
   attached it rides whatever it is attached to.
3. **An AI Urchin breaking off its own rail.** 2026-11-06 05:33 (Yash's I2 race): twice the orbit break-off
   (`AIPilot.IsBreakingOrbit`) fired while it rode its own rail at 300 u/s; it flew off, slowed to 65-76 u/s and each
   excursion cost it about 10 s. 14 gates when the race ended.

## What the measured-roster model gets wrong in a grid

- **One mouth exponent for everyone.** MOUTH_ALPHA 0.6 slows every non-rail hull alike as the mouth shrinks. The races
  say the cost of a tight mouth is the hull's own: the exponent each hull's I4 races imply is about 1.15 for the Manta,
  0.9 for the Rhino, 0.5 for the Squirrel and 0.3 for the Serpent, and at I2 the Dolphin is faster than the model
  (about -0.2). A course search on one exponent rewards tight mouths too little for the Manta and too much for the
  Serpent.
- **It prices traffic that is not there** (point 3 above), and it was fitted to grid races whose records list the
  OTHER humans as rivals: on 2026-11-05 three people raced together, each machine recorded the race, and on a client's
  record nothing marks a human rival. The old report counted those people as AIs. `--grid` merges the records and
  drops them (9 human seats since 2026-11-02).
- **The Rhino's speed is its boost driver's choice, not its corners.** The lap model gives the Rhino a corner speed; the
  real Rhino cruises at 50 u/s wherever no straight is long enough for its ramp, so on a course of short legs it is
  far slower than the lap model, and on a course of long legs it may be far faster. A course change moves the Rhino
  through that rule, which no model term has.
- **Rail rams are not in it.** The Manta (9-14 a race, slowed 28%), the Stoat at I1 (4-6, 11-22%), the Squirrel and the
  Dolphin (4-5, 4-12%) lose time to the rails themselves; the model's missed-mouth term lumps it in with the mouths.
  A third to a half of the Manta's rams are at the mouths (within 3 radii), the rest on the legs where its line runs beside the braid.
- **One race is not a measurement.** The Manta's six I4 races span 124-373 s; its six I1 duels 55-131 s. A per-hull
  number needs five races or more; the MOUTH_ALPHA fit used two.

## What each AI needs (proposals for Yash; nothing changed)

- **Manta:** rail clearance on its line (it rams the braid about ten times a race) and steering at tight mouths (it
  crosses 1.1-1.5 radii out, then loses 8.5 s re-attacking; ring 7 of the old I4 costs it 13.8 s a lap). Between mouths its
  boost takes it to about 580 u/s (90th percentile).
- **Rhino:** a boost rule that ramps on the legs a course has, or courses with longer legs. Misses are rare.
- **Urchin:** the two rail faults above. When it is on its own rail the model is exact.
- **Squirrel:** the pilot fault above; otherwise it is close to the model.
- **Serpent:** nothing urgent: it beats the model at I4 with its boost idle; its worst mouths are 5 and 7.
- **Dolphin, Stoat, Sparrow, Scarab:** not measured as AIs at I2-I4 since 2026-11-02.

## What would settle it (real-game tests, none run here)

1. One I4 race on the branch build, with the recorder on, to confirm the circuit is the branch's (68 u mouths, a Blue
   neutral stretch in the flight's `rails`). Until then the branch's circuits are untested in the editor.
2. One I4 race flown as a Squirrel against an AI Squirrel (fault 1), and one as anything else for a control.
3. The Regatta Hull Benchmark at I2-I4, five races a hull, to replace MOUTH_ALPHA with a per-hull number.

## Reproduce

    git fetch --depth=1 origin 'refs/heads/bugledger/*:refs/remotes/origin/bugledger/*'
    # copy every flights/flight_Regatta_*.json into one folder as <name>__<user>.json, then
    python3 Tools/Build/regatta_rival_report.py --dir <folder> --grid --since 2026-11-02 [--hull manta] [--json out.json]

The model column: `regatta_measured_roster.ROSTER` from `claude/regatta-overnight-balance` raced on
`course_port.regatta_settings(I)` and seed from `bleeding-edge` (the circuits the flights record; the generated rings
match the recorded ones). The full per-race read is in the project's shared folder,
`regatta-training/results/grid-losses/`.
